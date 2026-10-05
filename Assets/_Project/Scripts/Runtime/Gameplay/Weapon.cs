using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using BillGameCore;

namespace ZombieWar
{
    public partial class Weapon : MonoBehaviour
    {
        static readonly Color MuzzleLight = new Color(1f, 0.72f, 0.38f);

        [Tooltip("Fallback roster. The run weapon comes from the loadout (LoadoutState.ApplyTo); " +
                 "this list only arms the player in scenes and tests that have no loadout, and feeds " +
                 "the editor pose/grip tools.")]
        [SerializeField] private List<WeaponData> weapons = new();

        [Tooltip("GunMount inside WeaponRig - follows the chest via a MultiParentConstraint " +
            "(in-stream, see WeaponIKController). NOT a child of either hand: the hands IK toward " +
            "the weapon's grips, so a hand-descendant mount would be a circular dependency.")]
        [FormerlySerializedAs("weaponSocket")]
        [SerializeField] private Transform weaponMount;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private Texture2D recoilNoiseTexture;

        [Tooltip("Screen-space trauma punched on every shot. Sells recoil in 3rd-person independent " +
                 "of the IK-grip feedback loop (which visually absorbs most of the gun-mount kick).")]
        [SerializeField] private float cameraShakeOnFire = 0.25f;
        private CameraFollow _cameraFollow;

        [Tooltip("Transform kicked on fire (spring), child of GunMount. The hand IK targets are " +
                 "children of this pivot inside the rig stream, so kicking it recoils the gun AND " +
                 "both hands together in the same frame.")]
        [SerializeField] private Transform recoilPivot;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private float gizmoAimLength = 6f;

        // Two-Bone IK Constraint targets (set up in the Editor - see Docs/Reference/Technical/EDITOR_SETUP_CHECKLIST.md)
        // must be repointed whenever the equipped weapon instance changes.
        public event Action<WeaponGripPoints> OnWeaponEquipped;

        private int _currentIndex;
        private WeaponData _currentData;  // the equipped weapon; source of truth for Current
        private GameObject _currentInstance;
        private WeaponGripPoints _currentGripPoints;
        private Vector3 _weaponRestLocalPosition;
        /// <summary>
        /// Owed firing time, in seconds. Continuous fire is driven by draining this, not by a
        /// countdown cooldown.
        ///
        /// The difference matters at low frame rates. A "fire once then set cooldown = interval"
        /// scheme silently loses cadence whenever a frame is longer than the interval: a 20 rounds/s
        /// weapon at 30 FPS can only ever fire 30 times a second, so the weapon quietly becomes worse
        /// on weaker hardware. Accumulating elapsed time and spending it in whole shots keeps the
        /// authored rate honest regardless of frame length.
        /// </summary>
        private float _fireAccumulator;

        /// <summary>
        /// Hard bound on shots resolved in a single frame.
        ///
        /// A defensive cap, not a balance knob. It stops a hitch, a breakpoint or a resumed pause
        /// from resolving an unbounded burst in one frame - which would spike allocation, audio and
        /// physics all at once, exactly when the game is already struggling.
        /// </summary>
        private const int MaxCatchUpShotsPerFrame = 4;

        private float _recoilNoiseSeed;
        private float _recoilNoisePhase; // advance 1 cell/phat ban (golden-ratio => blue-noise-ish)

        // Recoil spring (drives recoilPivot): back-kick (-Z) + muzzle climb (pitch up) + side sway (yaw).
        private Vector3 _pivotRestPos;
        private Quaternion _pivotRestRot;
        private Vector3 _recoilPos, _recoilPosVel;
        private float _recoilPitch, _recoilPitchVel;
        private float _recoilYaw, _recoilYawVel;

        // The weapon actually in the hand. Null until an equip succeeds - a roster entry that was never
        // instantiated is not a weapon. Every caller must null-check: an unarmed player is a
        // recoverable state, an exception on the fire path is not.
        public WeaponData Current => _currentData;

        // Grips of the currently-equipped weapon instance (null until first Equip). Lets late-enabling
        // listeners (e.g. WeaponIKController) sync their state without waiting for the next equip event.
        public WeaponGripPoints CurrentGrips => _currentGripPoints;

        /// Full roster + current index for HUD selection / pose tuning. Data-driven: shrink the
        /// `weapons` list to limit what the roster shows - HUD rebuilds from this automatically.
        public System.Collections.Generic.IReadOnlyList<WeaponData> Weapons => weapons;
        public int CurrentIndex => _currentIndex;

        /// <summary>Shots per second the equipped weapon currently sustains, after permanent star
        /// upgrades AND the run's level-up cards.</summary>
        public float CurrentFireRate
        {
            get
            {
                var data = Current;
                return data == null ? 0f : EffectiveFireRate(data);
            }
        }

        // Permanent star level of the equipped weapon. It only changes in the menu, so it is read once
        // per equip instead of scanning the profile on every shot and every pellet hit.
        private int _starLevel = 1;
        // M10: the equipped skin set multiplies damage (owner: skins add power).
        private float _skinBonus;

        // Card stats are consumed at the two spots weapon numbers leave the data layer: rate here,
        // damage in ApplyHit (through SkillRuntime.ModifyHitDamage). Outside a run there is no
        // SkillRuntime and both fall back to 1x.
        private float EffectiveFireRate(WeaponData data) =>
            WeaponUpgradeMath.EffectiveFireRate(data, _starLevel)
            * (ZombieWar.Skills.SkillRuntime.Active?.FireRateMultiplier ?? 1f);

#if UNITY_EDITOR
        // Editor-only hooks for the Grip Tuner inspector (WeaponEditor). The equipped gun model is a
        // child of recoilPivot; its LOCAL transform == gripLocalPosition/Euler (recoil kicks the pivot,
        // not the instance) so reading it back is a clean capture.
        public Transform EditorInstanceTransform => _currentInstance != null ? _currentInstance.transform : null;

        // Push tuned values onto the live instance so the user sees the change in Play Mode before saving.
        public void EditorApplyGrip(Vector3 localPos, Vector3 localEuler, Vector3 localScale)
        {
            if (_currentInstance == null) return;
            _currentInstance.transform.localPosition = localPos;
            _currentInstance.transform.localRotation = Quaternion.Euler(localEuler);
            _currentInstance.transform.localScale = localScale;
            _weaponRestLocalPosition = localPos;
        }

        public void EditorApplyAuthoredGripPositions()
        {
            ApplyAuthoredGripPositions(Current, EditorInstanceTransform, CurrentGrips);
        }
#endif

        private void Awake()
        {
            // Bullets fly over the obstacles of a baked map (water, lava, rocks): never hit that layer.
            int navObstacle = LayerMask.NameToLayer("NavObstacle");
            if (navObstacle >= 0) hitMask &= ~(1 << navObstacle);
            _recoilNoiseSeed = UnityEngine.Random.value * 100f;
            EnsureRecoilPivot();
        }

        // Pivot ngoi GIUA weaponMount va gun model. Sung + grip + muzzle deu la con cua no.
        // Rest = identity (0,0,0 / no rotation) nen spring luon keo ve DUNG rest, khong troi.
        // PlayerRigBuilder tao san RecoilPivot duoi GunMount; day chi la fallback runtime.
        private bool _pivotRestCaptured;

        private void EnsureRecoilPivot()
        {
            if (recoilPivot == null && weaponMount != null)
            {
                var go = new GameObject("RecoilPivot");
                recoilPivot = go.transform;
                recoilPivot.SetParent(weaponMount, false);
                recoilPivot.localPosition = Vector3.zero;
                recoilPivot.localRotation = Quaternion.identity;
            }
            // Chi capture rest pose MOT LAN. Neu capture lai moi lan equip thi offset recoil
            // dang do (spring chua hoi ve 0) bi nuong vao rest => moi lan doi sung lech them 1 ti.
            if (recoilPivot != null && !_pivotRestCaptured)
            {
                _pivotRestPos = recoilPivot.localPosition;
                _pivotRestRot = recoilPivot.localRotation;
                _pivotRestCaptured = true;
            }
        }

        // Xoa sach trang thai spring + snap pivot ve dung rest (goi khi doi sung).
        private void ResetRecoil()
        {
            _recoilPos = Vector3.zero; _recoilPosVel = Vector3.zero;
            _recoilPitch = 0f; _recoilPitchVel = 0f;
            _recoilYaw = 0f; _recoilYawVel = 0f;
            if (recoilPivot != null)
            {
                recoilPivot.localPosition = _pivotRestPos;
                recoilPivot.localRotation = _pivotRestRot;
            }
        }

        private void Start()
        {
            // The spawner equips the loadout weapon before Start runs. Only a scene or test with no
            // loadout reaches the roster fallback. An empty or all-invalid list leaves the player
            // unarmed rather than throwing out of Start - a thrown Start would skip everything after it.
            if (_currentInstance != null) return;
            if (!EquipFirstUsable())
                Debug.LogError("[Weapon] `weapons` has no usable entry (empty, or every entry is null " +
                               "or missing its prefab). Player starts unarmed.", this);
        }

        /// Equips the first roster entry that actually resolves, so one bad asset in the middle of
        /// the list cannot leave the player weaponless.
        private bool EquipFirstUsable()
        {
            if (weapons == null) return false;
            for (int i = 0; i < weapons.Count; i++)
                if (EquipWeapon(i)) return true;
            return false;
        }

        private void Update()
        {
            TickAutoFire(Time.deltaTime);
            UpdateRecoilSpring();
        }

        // Springs the mount back to rest every frame. Fire adds an impulse (see ApplyRecoil).
        // Pitch = hat nong len, Yaw = lech trai/phai (noise-driven), Pos.z = giat lui.
        private void UpdateRecoilSpring()
        {
            if (recoilPivot == null) return;
            float ret = Mathf.Max(0.01f, Current != null ? Current.recoilReturnDuration : 0.12f);
            _recoilPos = Vector3.SmoothDamp(_recoilPos, Vector3.zero, ref _recoilPosVel, ret);
            _recoilPitch = Mathf.SmoothDamp(_recoilPitch, 0f, ref _recoilPitchVel, ret);
            _recoilYaw = Mathf.SmoothDamp(_recoilYaw, 0f, ref _recoilYawVel, ret);
            recoilPivot.localPosition = _pivotRestPos + _recoilPos;
            recoilPivot.localRotation = _pivotRestRot * Quaternion.Euler(-_recoilPitch, _recoilYaw, 0f);
        }

        /// <summary>
        /// Equips the run weapon. Refuses a weapon whose grip/muzzle anchors are not authored yet, or
        /// one that fails to instantiate, and keeps the previous weapon in both cases.
        /// </summary>
        public bool Equip(WeaponData data)
        {
            if (data == null) return false;
            if (!data.IsPlayable)
            {
                Debug.LogError($"[Weapon] Refusing to equip '{data.name}' — authoringStatus is " +
                               $"{data.Authoring}. Grip/muzzle anchors are hand-authored by the owner; " +
                               "this weapon is data-only until that pass is done.");
                return false;
            }
            return EquipData(data);
        }

        /// Jump directly to a roster weapon by index (editor pose tuning). Play-mode only.
        public void EquipIndex(int index)
        {
            if (!Application.isPlaying || weapons == null || index < 0 || index >= weapons.Count) return;
            EquipWeapon(index);
        }

        /// <returns>False when the index is out of range or the entry cannot be equipped. The current
        /// index is only advanced on success, so a failed switch never strands
        /// <see cref="Current"/> on an entry that was never instantiated.</returns>
        private bool EquipWeapon(int index)
        {
            if (weapons == null || index < 0 || index >= weapons.Count) return false;
            if (!EquipData(weapons[index])) return false;
            _currentIndex = index;
            return true;
        }

        /// <returns>False when the data or its prefab is missing. Rejecting here rather than further
        /// in keeps a half-equipped state impossible: either the instance exists and _currentData
        /// matches it, or neither changed.</returns>
        private bool EquipData(WeaponData data)
        {
            if (data == null || data.weaponPrefab == null)
            {
                Debug.LogError(data == null
                    ? "[Weapon] Equip refused: WeaponData is null."
                    : $"[Weapon] Equip refused: '{data.name}' has no weaponPrefab.", this);
                return false;
            }

            if (_currentInstance != null) Destroy(_currentInstance);
            _currentData = data;
            _starLevel = PlayerProfile.GetWeaponLevel(data.WeaponId);
            var skin = Skins.WeaponSkins.Find(PlayerProfile.GetEquippedSkin(data.WeaponId));
            _skinBonus = Skins.WeaponSkins.DamageBonus(skin);

            // Signature cards are gated by the family in hand, so the build learns it at equip time -
            // not only when a level-up happens to open.
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (skills != null) skills.EquippedFamily = data.weaponClass;

            // Doi sung KHONG duoc don mot loat dan: xoa sach thoi gian ban con no lai.
            _fireAccumulator = 0f;

            // The outgoing weapon's retrigger voice must not keep speaking under the new gun.
            StopFireAudio();

            // Sung + grip + muzzle deu la con cua recoilPivot => recoil kick pivot la ca cum theo.
            EnsureRecoilPivot();
            ResetRecoil(); // doi sung giua luc recoil chua hoi => phai snap pivot ve rest, khong de lech ton dong
            _currentInstance = Instantiate(data.weaponPrefab, recoilPivot != null ? recoilPivot : weaponMount);
            _currentInstance.transform.localPosition = data.gripLocalPosition;
            _currentInstance.transform.localRotation = Quaternion.Euler(data.gripLocalEuler);
            _currentInstance.transform.localScale = data.gripLocalScale;
            if (skin != null) _currentInstance.AddComponent<Skins.WeaponSkinApplier>().Apply(skin);
            _weaponRestLocalPosition = _currentInstance.transform.localPosition;
            _currentGripPoints = _currentInstance.GetComponent<WeaponGripPoints>();

            // Restore authored marker positions before IK receives the equipped-weapon event.
            // Prefab marker positions remain the fallback for weapons that have not been captured yet.
            ApplyAuthoredGripPositions(data, _currentInstance.transform, _currentGripPoints);

            OnWeaponEquipped?.Invoke(_currentGripPoints);
            return true;
        }

        private static void ApplyAuthoredGripPositions(
            WeaponData data, Transform weaponRoot, WeaponGripPoints grips)
        {
            if (data == null || !data.useAuthoredGripPositions || weaponRoot == null || grips == null)
                return;

            if (grips.RightHandGrip != null)
                grips.RightHandGrip.position = weaponRoot.TransformPoint(data.rightHandGripRootPosition);

            if (data.twoHanded && grips.LeftHandGrip != null)
                grips.LeftHandGrip.position = weaponRoot.TransformPoint(data.leftHandGripRootPosition);
        }

        private static void SpawnMuzzleFlash(WeaponData data, Vector3 position, Vector3 direction)
        {
            if (data.muzzleFlashPrefab == null) return;
            // M8: heat ramps and an armed Quickstep grow the flash.
            float scale = ZombieWar.Skills.SkillRuntime.Active?.MuzzleFlashScale ?? 1f;
            FxPool.Play(data.muzzleFlashPrefab, position, Quaternion.LookRotation(direction), scale);
        }

        // Pooled one-shot mesh tracer (MeshTracer handles the stretch + fade animation itself).
        private void SpawnTracer(WeaponData data, Vector3 from, Vector3 to)
        {
            if (data.tracerPrefab == null) return;
            // M8: the weapon signatures show on the bullet (Quickstep, Breach, Longshot, heat ramps).
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (skills != null && skills.TryTracerLook(_shotPlan, Vector3.Distance(from, to), out var tint, out var thick))
                TracerPool.Play(data.tracerPrefab, from, to, tint, thick);
            else
                TracerPool.Play(data.tracerPrefab, from, to);
        }

        // A single stretched particle standing in for a bullet-trail smoke effect - cheaper than a
        // real trail renderer and good enough at hitscan speed (the "particle" the brief asks for).
        private static void SpawnSmokeTrail(WeaponData data, Vector3 from, Vector3 to)
        {
            if (data.smokeTrailPrefab == null) return;

            float length = Vector3.Distance(from, to);
            if (length < 0.001f) return;

            // Pivot cua quad/particle nam giua, nen phai dat o MIDPOINT: scale z=length
            // se keo deu ra 2 phia = dung tu muzzle -> hit. Neu dat o 'from' thi mot nua
            // se tho ra sau nong sung (gian 2 chieu - loi cu).
            var mid = (from + to) * 0.5f;
            var trail = FxPool.Play(data.smokeTrailPrefab, mid, Quaternion.LookRotation(to - from));
            if (trail == null) return;
            trail.transform.localScale = new Vector3(trail.transform.localScale.x, trail.transform.localScale.y, length);
        }

        // Recoil = 1 impulse day vao spring tren recoilPivot. Spring (UpdateRecoilSpring) tu keo
        // ve rest. Grip nam tren mount nen tay IK bam theo => tay rung cung sung. Duong dan KHONG
        // bi anh huong (raycast doc lap). Recoil = HAT LEN (pitch) + LECH TRAI/PHAI ngau nhien (yaw).
        private void ApplyRecoil(WeaponData data, float scale = 1f)
        {
            if (recoilPivot == null) return;

            // Sample 1 cell / 1 phat ban: advance phase theo golden-ratio (low-discrepancy => phan bo
            // kieu blue-noise, cac phat cach deu khong cum) thay vi scroll theo Time.time (2 phat nhanh
            // se trung pixel). Neu chua gan recoilNoiseTexture, chinh phase da la chuoi tot => xai thang.
            _recoilNoisePhase = Mathf.Repeat(_recoilNoisePhase + 0.61803398f, 1f);
            Vector2 noise = NoiseTextureSampler.Sample(recoilNoiseTexture, _recoilNoisePhase, _recoilNoiseSeed);
            float side = Mathf.Abs(noise.x) > 0.0001f ? noise.x : (_recoilNoisePhase * 2f - 1f);

            float inv = scale / Mathf.Max(0.01f, data.recoilKickDuration);

            // Giat lui theo -Z.
            _recoilPosVel += new Vector3(0f, 0f, -data.recoilKickDistance) * inv;
            // Hat nong LEN (thanh phan chinh) - bien do recoilAimKickAngle.
            _recoilPitchVel += data.recoilAimKickAngle * inv;
            // Lech TRAI/PHAI (yaw) - noise/blue-noise dieu khien, bien do rieng recoilSideKickAngle.
            _recoilYawVel += side * data.recoilSideKickAngle * inv;
        }
    }
}
