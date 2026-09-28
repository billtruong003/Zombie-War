using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using BillGameCore;

namespace ZombieWar
{
    public class Weapon : MonoBehaviour
    {
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
        /// Drives continuous automatic fire.
        ///
        /// There is no fire button: a target inside range is the only thing that makes the weapon
        /// shoot, and from M4 it then shoots without interruption - no magazine, no reload gap.
        ///
        /// Cadence is spent from <see cref="_fireAccumulator"/> rather than gated by a countdown, so
        /// the authored rate survives long frames. The clamps below are the whole safety story:
        ///
        /// - With no valid target the accumulator is held at one interval, so re-acquiring a target
        ///   shoots immediately, but standing idle for a minute banks one shot, not sixty.
        /// - While firing it is capped at the per-frame catch-up budget, so a hitch or a resumed
        ///   pause cannot dump a burst.
        /// - Pause needs no special case: `Time.deltaTime` is zero at `timeScale` zero, so nothing
        ///   accumulates while paused.
        /// </summary>
        private void TickAutoFire(float deltaTime)
        {
            var data = Current;
            float interval = ShotInterval(data);

            if (!HasValidTarget(data, out Vector3 aimDirection) || interval <= 0f
                || float.IsInfinity(interval))
            {
                // Hold exactly one shot ready. Not zero, or the player would eat a full interval of
                // delay every time an enemy walks into range; not "whatever accumulated", or a quiet
                // minute would empty itself into the first target seen.
                _fireAccumulator = float.IsInfinity(interval) ? 0f : interval;
                return;
            }

            _fireAccumulator += deltaTime;

            float cap = interval * MaxCatchUpShotsPerFrame;
            if (_fireAccumulator > cap) _fireAccumulator = cap;

            int shots = 0;
            while (_fireAccumulator >= interval && shots < MaxCatchUpShotsPerFrame)
            {
                if (!FireOnce(aimDirection)) break;
                _fireAccumulator -= interval;
                shots++;
            }
        }

        /// <summary>Seconds between shots at the current effective rate. Infinity when it cannot fire.</summary>
        private float ShotInterval(WeaponData data)
        {
            if (data == null) return float.PositiveInfinity;

            float rate = EffectiveFireRate(data);
            // A zero or negative authored rate is a data defect. Failing safe means never firing,
            // rather than dividing by zero and spraying every frame.
            return rate > 0f ? 1f / rate : float.PositiveInfinity;
        }

        /// <summary>Degrees the visible aim may lag the selected target and still fire. Tighter
        /// wastes cadence during ordinary tracking; looser sprays misses during target switches.
        /// The aim sweeps at 720°/s, so even a 150° switch clears this gate in ~0.2 s.</summary>
        [SerializeField] private float fireAlignmentConeDegrees = 5f;

        /// <summary>Assumed half-width of an enemy body for the distance-scaled part of the
        /// alignment gate (production zombie colliders are ~0.7 wide).</summary>
        private const float CloseTargetBodyRadius = 0.35f;

        private bool HasValidTarget(WeaponData data, out Vector3 aimDirection)
        {
            aimDirection = Vector3.forward;

            // Unarmed is a valid state (empty loadout, every asset invalid), and this runs every
            // frame - an unguarded Current here would throw once per frame, forever.
            if (data == null || _currentInstance == null) return false;

            var player = PlayerMovement.Instance;
            if (player == null || !player.HasTarget) return false;
            if (player.AimTargetDistance > data.range) return false;

            // Alignment gate (M5.1.2 CP1): while the visible aim is still sweeping toward the
            // selected target, HOLD fire. The caller's no-target path keeps the accumulator pinned
            // at one interval, so alignment recovery fires promptly but can never dump a banked
            // burst. Scripted/test fire (no live ITargetable) skips the gate.
            //
            // The gate widens with the angle the target's body actually subtends: at 10 m a body is
            // ~2° wide and the authored cone governs, but at 0.6 m it is ~30° wide - a fixed cone
            // there starved fire completely in a surrounding pack (measured live: 1 kill in 6 s of
            // SMG fire), because every nearest-target reshuffle re-opened a 5° gate the sweeping
            // aim kept missing.
            var target = player.AimTarget;
            if (target != null && target.Transform != null)
            {
                Vector3 toTarget = target.Transform.position - player.transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance > 0.01f)
                {
                    float subtended = Mathf.Atan2(CloseTargetBodyRadius, distance) * Mathf.Rad2Deg;
                    float gate = Mathf.Max(fireAlignmentConeDegrees, subtended);
                    if (Vector3.Angle(player.AimDirection, toTarget) > gate) return false;
                }
            }

            aimDirection = player.AimDirection;
            return true;
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

        /// <summary>
        /// Fires one shot if the weapon is ready, spending the cadence budget.
        ///
        /// Kept public because external callers (tests, scripted sequences) fire a single shot
        /// through it. It respects the same accumulator the automatic loop drains, so calling it
        /// cannot outrun the weapon's authored rate.
        /// </summary>
        public void TryFire(Vector3 aimDirection)
        {
            float interval = ShotInterval(Current);
            if (float.IsInfinity(interval)) return;
            if (_fireAccumulator < interval) return;

            if (FireOnce(aimDirection)) _fireAccumulator -= interval;
        }

        /// <summary>Resolves exactly one shot. Returns false when the weapon cannot fire at all.</summary>
        private bool FireOnce(Vector3 aimDirection)
        {
            // M7.2b — ramp cards (Bullet Hose, Heavy Pressure, Run & Gun) only ramp if something
            // tells them the trigger is actually held. One notify per resolved shot.
            ZombieWar.Skills.SkillCombatDriver.Instance?.NotifyFiring();

            // M7.2c — the shot plan is now CONSUMED rather than discarded. Breach Round's bonus
            // pierce and Shockwave Belt's cone were computed here and thrown away, which is what
            // made both cards half-stubbed.
            _shotPlan = ZombieWar.Skills.SkillRuntime.Active?.OnShotFired()
                        ?? default(ZombieWar.Skills.SkillRuntime.ShotPlan);
            if (_shotPlan.shockwave) FireShockwave(aimDirection);

            var data = Current;
            // Nothing equipped, or Current fell back to a roster entry that was never instantiated -
            // the muzzle lookup below dereferences _currentInstance, so both must be real.
            if (data == null || _currentInstance == null) return false;

            // Ban theo TRUC NONG (MuzzlePoint.forward = +Z cua muzzle) => tracer luon thang ra tu
            // nong, khong "meo" so voi than sung. Auto-aim + Multi-Aim IK lo viec chia nong vao zombie.
            bool hasMuzzle = _currentGripPoints != null && _currentGripPoints.MuzzlePoint != null;
            Vector3 muzzlePosition = hasMuzzle
                ? _currentGripPoints.MuzzlePoint.position
                : _currentInstance.transform.position;
            Vector3 muzzleForward = hasMuzzle
                ? _currentGripPoints.MuzzlePoint.forward
                : aimDirection;

            // Hit-test from the player's own axis, NOT the muzzle. The muzzle sits ~0.9 units in
            // front of the chest, so an enemy that walks inside that offset is BEHIND the ray origin
            // and Unity raycasts never hit a collider they start inside - the nearest attacker
            // became permanently unkillable while chewing on the player (M5 audit S1). The muzzle
            // stays the visual origin for tracer/flash; only the physics ray moves back. Range is
            // extended by the pull-back so the effective reach out of the barrel is unchanged.
            Vector3 rayOrigin = new Vector3(transform.position.x, muzzlePosition.y, transform.position.z);
            float rayRangeBonus = Vector3.Distance(rayOrigin, muzzlePosition);

            // M5.1.2 CP1 — the authoritative base direction is the VISIBLE aim, never the target.
            //
            // M5.1.1 briefly snapped the ray straight at the selected target; that made every
            // bullet a homing shot and could hit an enemy 150° away from where the gun visibly
            // pointed. The contract is restored to: selected target → smoothed AimDirection →
            // visible body/muzzle → shot. The muzzle tracks AimDirection within ~1.5° (measured),
            // so firing along the aim keeps gun, flash, tracer and hit as one coherent line. What
            // guards the hit rate instead is the ALIGNMENT GATE in HasValidTarget - the weapon
            // holds fire until the visible aim has swept inside the cone. Locomotion never enters.
            //
            // Fire-time staleness re-check: the aim lock is updated in FixedUpdate, so the target
            // can die within the frame. A stale target refuses the shot BEFORE any side effect -
            // no flash, no audio, no recoil, no cadence spend (return false leaves the
            // accumulator's shot budget unspent; TickAutoFire stops the burst).
            var player = PlayerMovement.Instance;
            var target = player != null ? player.AimTarget : null;
            if (target != null)
            {
                if (!target.IsTargetable || target.Transform == null) return false;
                Vector3 toTarget = target.Transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude > data.range) return false;
            }

            Vector3 shotDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : muzzleForward;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _probeShotId++;
            _probeMuzzlePos = muzzlePosition;
            _probeMuzzleFwd = muzzleForward;
            _probeAim = shotDirection;
            _probeTargetDir = Vector3.zero;
            _probeTargetValid = target != null && target.IsTargetable;
            _probeTarget = target != null && target.Transform != null ? target.Transform : null;
            if (_probeTarget != null)
            {
                Vector3 td = _probeTarget.position - rayOrigin;
                td.y = 0f;
                if (td.sqrMagnitude > 0.0001f) _probeTargetDir = td.normalized;
            }
#endif

            int pellets = Mathf.Max(1, data.pelletCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _probeRayCount = pellets;
#endif
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = (pellets > 1 || data.spreadAngle > 0f)
                    ? ScatterDirection(shotDirection, data.spreadAngle)
                    : shotDirection;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _probeRayIndex = i;
#endif
                FireRay(data, rayOrigin, muzzlePosition, rayRangeBonus, dir);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Third argument is the ACTUAL ray direction of this shot (the authoritative snapshot),
            // not the muzzle's visual forward - evidence probes must see what the physics saw.
            ShotProbe?.Invoke(data, muzzlePosition, shotDirection, aimDirection, _lastPierceHits, _lastPierceBlocked);
#endif

            SpawnMuzzleFlash(data, muzzlePosition, muzzleForward);

            PlayFireAudio(data);
            ApplyRecoil(data);
            ShakeCamera(cameraShakeOnFire);
            return true;
        }

        /// <summary>Fire intervals shorter than this route through the retrigger voice. At 0.15 s
        /// (≈6.7 shots/s) a typical 0.3-0.5 s fire clip already overlaps itself 2-3 deep, which is
        /// where the per-key voice policy starts dropping shots (audible gaps - M5.1 CP7).</summary>
        private const float HighRateAudioInterval = 0.15f;

        private AudioCueHandle _fireCueHandle;

        /// <summary>
        /// Same-key voice budget for a slow weapon's guaranteed transients.
        ///
        /// Three rotating voices cover a handgun's ~0.3-0.4 s of audible body even at its fastest
        /// authored 6.5 shots/s, so a recycled voice has already spent its report. Multi-pellet
        /// weapons get one more: their blast body runs ~0.7-0.9 s and AA12 reaches 3 shots/s. The
        /// choice reads authored DATA (pellet count), never a weapon name.
        /// </summary>
        private static int TransientVoiceBudget(WeaponData data) => data.pelletCount > 1 ? 4 : 3;

        /// <summary>
        /// True for the families whose shots must read as separate reports: sidearms, shotguns and
        /// marksman rifles. Read from the authored <see cref="WeaponData.weaponClass"/> - the design
        /// property that already states the family - rather than matched from a weapon name, and
        /// stable when a fire-rate perk moves the cadence.
        /// </summary>
        private static bool WantsDiscreteReports(WeaponData data) =>
            data.weaponClass == WeaponClass.Sidearm
            || data.weaponClass == WeaponClass.Shotgun
            || data.weaponClass == WeaponClass.Marksman;

        // Cadence grammar:
        //
        //   discrete families (sidearm/shotgun/marksman)
        //       -> guaranteed bounded transient. The ordinary cue path counts a voice busy for the
        //          whole imported clip (1.25 s handgun), so at 4 shots/s the global perKeyLimit=3
        //          silently swallowed every fourth visible shot - the missing pistol reports a human
        //          playtest heard. Bounded recycling guarantees each visible shot a transient.
        //   automatic families (SMG/AR/LMG), or anything driven past the merge threshold
        //       -> one managed retrigger voice: per-bullet transients above ~6.7 shots/s merge
        //          anyway, and this keeps voice growth at zero under sustained fire.
        private void PlayFireAudio(WeaponData data)
        {
            if (string.IsNullOrEmpty(data.fireSfxKey)) return;

            bool discrete = WantsDiscreteReports(data) && ShotInterval(data) >= HighRateAudioInterval;
            if (discrete)
                Bill.Audio?.PlayGuaranteedTransient(data.fireSfxKey, SfxPriority.High,
                                                    TransientVoiceBudget(data));
            else
                _fireCueHandle = Bill.Audio?.RetriggerCue(data.fireSfxKey, _fireCueHandle, SfxPriority.High)
                                 ?? AudioCueHandle.None;
        }

        // A switch or teardown must not leave the previous weapon's retrigger voice ringing.
        private void StopFireAudio()
        {
            Bill.Audio?.StopCue(_fireCueHandle, 0.05f);
            _fireCueHandle = AudioCueHandle.None;
        }

        private void OnDisable() => StopFireAudio();

        // Screen shake is what actually reads as "recoil" in 3rd-person; the gun-mount spring is
        // largely cancelled by the hand IK chasing the grips. Camera lookup is cached.
        private void ShakeCamera(float amount)
        {
            if (amount <= 0f) return;
            if (_cameraFollow == null && Camera.main != null)
                Camera.main.TryGetComponent(out _cameraFollow);
            if (_cameraFollow != null) _cameraFollow.Shake(amount);
        }

        private void FireRay(WeaponData data, Vector3 rayOrigin, Vector3 muzzlePosition,
                             float rayRangeBonus, Vector3 direction)
        {
            float rayRange = data.range + rayRangeBonus;

            // PiercingLine (sniper/railgun): bắn 1 đường xuyên hết zombie. Docs/Reference/Design/WEAPON_DESIGN.md §3.
            if (data.fireMode == FireMode.PiercingLine)
            {
                FireRayPiercing(data, rayOrigin, muzzlePosition, rayRange, direction);
                return;
            }

            // SingleHitscan / MultiPelletHitscan: dừng ở target đầu tiên.
            Vector3 hitPoint = muzzlePosition + direction * data.range;
            bool didHit = Physics.Raycast(rayOrigin, direction, out RaycastHit hit, rayRange, hitMask);

            if (didHit)
            {
                hitPoint = hit.point;
                ApplyHit(data, hit.collider.GetComponentInParent<IDamageable>(), hit, rayOrigin, 1f);
            }
            if (data.splashRadius > 0f)
                Splash(data, hitPoint, didHit ? hit.collider.GetComponentInParent<ZombieBase>() : null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EmitRay(data, rayOrigin, direction, hitPoint,
                didHit ? hit.collider.transform : null, blocked: false, pierceHits: 0);
#endif

            SpawnTracer(data, muzzlePosition, hitPoint);
            SpawnSmokeTrail(data, muzzlePosition, hitPoint);
        }

        // Launcher blast: every other living enemy within splashRadius of where the shot landed takes
        // a share of the shot's damage (the direct hit already took the full hit in ApplyHit).
        private void Splash(WeaponData data, Vector3 at, ZombieBase direct)
        {
            float damage = WeaponUpgradeMath.EffectiveDamage(data, _starLevel) * data.splashDamageFraction * (1f + _skinBonus);
            int found = ZombieWar.Skills.TargetQuery.GatherEnemies(at, data.splashRadius, hitMask);
            for (int i = 0; i < found; i++)
            {
                var enemy = ZombieWar.Skills.TargetQuery.CandidateEnemy(i);
                if (enemy != null && enemy != direct) enemy.TakeDamage(damage);
            }
            if (data.splashFx != null)
                FxPool.Play(data.splashFx, at + Vector3.up * 0.1f, Quaternion.identity, Mathf.Min(data.splashRadius / 2f, 1.3f));
            if (!string.IsNullOrEmpty(data.splashSfxKey)) Bill.Audio?.Play(data.splashSfxKey, at, 0.8f);
            ShakeCamera(0.2f);
        }

        // Áp damage (range falloff + dmgMult) + impact FX + knockback cho 1 hit.
        private void ApplyHit(WeaponData data, IDamageable dmg, RaycastHit hit, Vector3 origin, float dmgMult)
        {
            if (dmg != null)
            {
                float distance = Vector3.Distance(origin, hit.point);
                float dist01 = data.range > 0f ? distance / data.range : 0f;
                float damage = WeaponUpgradeMath.EffectiveDamage(data, _starLevel)
                               * dmgMult * data.RangeFalloff(dist01) * (1f + _skinBonus);

                // Every damage-shaping card, Damage Up included, resolves here.
                var skills = ZombieWar.Skills.SkillRuntime.Active;
                if (skills != null)
                {
                    // Real values, not placeholders: a constant here would silently disable
                    // Execution Round, Point Blank, Longshot and Focus Fire.
                    var targetHealth = hit.collider.GetComponentInParent<Health>();
                    int targetId = targetHealth != null
                        ? targetHealth.transform.GetInstanceID()
                        : hit.collider.transform.GetInstanceID();
                    float healthFraction = targetHealth != null && targetHealth.Max > 0f
                        ? targetHealth.Current / targetHealth.Max
                        : 1f;

                    // Hunter's Mark is consumed by this hit, so read it before the damage does.
                    bool empowered = ZombieWar.Skills.StatusCarrier.Has(targetId, ZombieWar.Skills.StatusKind.Marked, Time.time);
                    damage = skills.ModifyHitDamage(damage, targetId, distance, healthFraction, Time.time);
                    skills.ApplyHitStatuses(targetId, Time.time);

                    // M7.2c legibility — a status the player cannot see is a status they will call a
                    // bug. Marks are WORLD-SPACE (owner owns every UI prefab, so nothing goes in the HUD).
                    var fx = ZombieWar.Skills.SkillFxDirector.Instance;
                    if (fx != null && targetHealth != null)
                    {
                        var t = targetHealth.transform;
                        float now = Time.time;
                        if (empowered)
                            fx.MarkEnemy(t, new Color(1f, 0.85f, 0.2f, 1f), 0.5f);      // Hunter's Mark paid out: gold
                        else if (skills.IsExecutionTarget(healthFraction))
                            fx.MarkEnemy(t, new Color(1f, 0.15f, 0.15f, 1f), 0.5f);     // Execution: red lock-on
                        else if (ZombieWar.Skills.StatusCarrier.Has(targetId, ZombieWar.Skills.StatusKind.Exposed, now))
                            fx.MarkEnemy(t, new Color(1f, 0.45f, 0.15f, 0.9f), 0.6f);   // Breach: orange
                        else if (ZombieWar.Skills.StatusCarrier.Has(targetId, ZombieWar.Skills.StatusKind.Slow, now))
                            fx.MarkEnemy(t, new Color(0.4f, 0.8f, 1f, 0.9f), 0.6f);     // Concussion: ice blue
                        else if (ZombieWar.Skills.StatusCarrier.Has(targetId, ZombieWar.Skills.StatusKind.Marked, now))
                            fx.MarkEnemy(t, new Color(1f, 0.9f, 0.2f, 0.9f), 0.6f);     // Hunter's Mark: gold
                        else if (skills.FocusFireActive)
                        {
                            // Focus Fire: the lock-on grows with every stacked hit on the same enemy.
                            float stacks = ZombieWar.Skills.StatusCarrier.Get(targetId, ZombieWar.Skills.StatusKind.HitCount, now);
                            if (stacks >= 2f)
                                fx.MarkEnemy(t, new Color(1f, 0.55f, 0.2f, 0.95f), 0.45f, 2f, 0.22f + 0.035f * Mathf.Min(stacks, 8f));
                        }
                    }
                }

                dmg.TakeDamage(damage);
            }
            if (data.impactPrefab != null)
                FxPool.Play(data.impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            ApplyKnockback(data, hit);
        }

        // Weapon-authored physical response. Routed through the enemy's own push API rather than a
        // Rigidbody impulse. The original reason was that a NavMeshAgent overwrote any Rigidbody
        // motion the same frame; since M4 there is no agent, but the routing stays because the enemy
        // still owns its displacement - one owner means the shove cannot fight the steering motor.
        private void ApplyKnockback(WeaponData data, RaycastHit hit)
        {
            if (data.knockback <= 0f) return;
            hit.collider.GetComponentInParent<ZombieBase>()?.ApplyPhysicalPush(data.knockback);
        }

        // The plan produced by the skill runtime for the shot currently being resolved. Gameplay, not a
        // probe: it must compile in every build, not only editor/development ones.
        private ZombieWar.Skills.SkillRuntime.ShotPlan _shotPlan;
        private static readonly int[] ConeBuffer = new int[ZombieWar.Skills.TargetQuery.MaxConsidered];

        /// <summary>
        /// Shockwave Belt (LMG). Every Nth shot sweeps a cone in front of the player. Uses the shared
        /// P3 cone query and the same one-gather-per-effect rule as the autonomous powers.
        /// </summary>
        private void FireShockwave(Vector3 aimDirection)
        {
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (skills == null) return;

            Vector3 origin = transform.position;
            // M8: the wave is drawn whether or not it hits, across exactly the cone it checks.
            ZombieWar.Skills.SkillFxDirector.Instance?.ConeWave(origin, aimDirection, _shotPlan.shockwaveAngle, 12f,
                                                               new Color(1f, 0.78f, 0.35f, 0.9f));
            int found = ZombieWar.Skills.TargetQuery.Gather(origin, 12f, hitMask);
            if (found == 0) return;

            int hits = ZombieWar.Skills.TargetQuery.Cone(
                found, origin, aimDirection, _shotPlan.shockwaveAngle * 0.5f, ConeBuffer);

            for (int i = 0; i < hits; i++)
            {
                var col = ZombieWar.Skills.TargetQuery.Candidate(ConeBuffer[i]);
                var enemy = col != null ? col.GetComponentInParent<ZombieBase>() : null;
                if (enemy == null) continue;                 // never the player: enemies only
                enemy.TakeDamage(skills.PowerDamage(18f, ZombieWar.Skills.SkillCatalogDefs.LmgShockwave));
                enemy.ApplyPhysicalPush(1.5f);               // the "shockwave" part
            }
        }

        // PiercingLine — RaycastAll dọc 1 đường: damage TẤT CẢ zombie, dừng khi gặp tường (vật
        // không có IDamageable). pierceCount = -1 xuyên vô hạn (railgun). Docs/Reference/Design/WEAPON_DESIGN.md §3,§7.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Development-only ballistics probe, invoked in the SAME frame as the shot with the
        /// exact values the ray used. Exists so miss-rate can be measured from inside the fire path
        /// instead of reconstructed a frame later. Nothing in the game subscribes to it.</summary>
        public static System.Action<WeaponData, Vector3, Vector3, Vector3, int, bool> ShotProbe;

        /// <summary>One record per ACTUAL physics ray, emitted with the exact origin/direction handed
        /// to Physics.Raycast - post-spread, per pellet. M5.1.1's evidence reported the pre-spread
        /// base vector as "the ray", which cannot describe a 1.5°–14° spread weapon; this exists so
        /// that mistake cannot be repeated (M5.1.2 CP2).</summary>
        public struct ShotRay
        {
            public WeaponData Data;
            public int ShotId, RayIndex, RayCount;
            public Vector3 Origin, MuzzlePosition, MuzzleForward, AimDirection, TargetDirection, RayDirection, HitPoint;
            public bool TargetWasValid, HitSelectedTarget, Blocked;
            public Transform SelectedTarget, HitTransform;
            public int PierceHits;
        }
        public static System.Action<ShotRay> RayProbe;

        private static int _probeShotId;

        private int _probeRayIndex, _probeRayCount;
        private Vector3 _probeMuzzlePos, _probeMuzzleFwd, _probeAim, _probeTargetDir;
        private bool _probeTargetValid;
        private Transform _probeTarget;

        private void EmitRay(WeaponData data, Vector3 origin, Vector3 dir, Vector3 hitPoint,
                             Transform hitTransform, bool blocked, int pierceHits)
        {
            RayProbe?.Invoke(new ShotRay
            {
                Data = data,
                ShotId = _probeShotId,
                RayIndex = _probeRayIndex,
                RayCount = _probeRayCount,
                Origin = origin,
                MuzzlePosition = _probeMuzzlePos,
                MuzzleForward = _probeMuzzleFwd,
                AimDirection = _probeAim,
                TargetDirection = _probeTargetDir,
                RayDirection = dir,
                HitPoint = hitPoint,
                TargetWasValid = _probeTargetValid,
                SelectedTarget = _probeTarget,
                HitTransform = hitTransform,
                HitSelectedTarget = hitTransform != null && _probeTarget != null
                    && (hitTransform == _probeTarget || hitTransform.IsChildOf(_probeTarget)),
                Blocked = blocked,
                PierceHits = pierceHits,
            });
        }
#endif

        private static readonly RaycastHit[] _pierceBuf = new RaycastHit[64];

        // A pierce line can cross several colliders belonging to ONE enemy (body + child hitboxes).
        // Without this set each of those colliders resolved to the same IDamageable and took a full
        // hit, so a single enemy absorbed multiple pierce "slots" and multiple damage applications.
        // Reused per shot; cleared at the start of every line so it can never leak across shots.
        private readonly HashSet<IDamageable> _pierceDamaged = new HashSet<IDamageable>();
        private int _lastPierceHits;
        private bool _lastPierceBlocked;

        private void FireRayPiercing(WeaponData data, Vector3 rayOrigin, Vector3 muzzlePosition,
                                     float rayRange, Vector3 direction)
        {
            _lastPierceHits = 0; _lastPierceBlocked = false;
            int count = Physics.RaycastNonAlloc(rayOrigin, direction, _pierceBuf, rayRange, hitMask);
            Vector3 endPoint = muzzlePosition + direction * data.range;
            _pierceDamaged.Clear();

            if (count > 0)
            {
                // RaycastNonAlloc không sort => sort theo cự ly để falloff xuyên áp đúng thứ tự.
                Array.Sort(_pierceBuf, 0, count, RaycastDistanceComparer.Instance);

                // Breach Round adds pierce for this shot only. -1 (infinite pierce) stays infinite.
                int effectivePierce = data.pierceCount < 0
                    ? data.pierceCount
                    : data.pierceCount + Mathf.Max(0, _shotPlan.bonusPierce);
                int maxTargets = effectivePierce < 0 ? int.MaxValue : effectivePierce + 1;
                float dmgMult = 1f;
                int hitTargets = 0;

                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = _pierceBuf[i];
                    var dmg = hit.collider.GetComponentInParent<IDamageable>();
                    if (dmg == null)
                    {
                        // Tường/vật cản chặn đạn => tracer dừng tại đây.
                        endPoint = hit.point;
                        _lastPierceBlocked = true;
                        break;
                    }

                    // Second collider on an enemy already hit by this line: the shot passes through
                    // without spending a pierce slot or re-damaging it.
                    if (!_pierceDamaged.Add(dmg)) continue;

                    ApplyHit(data, dmg, hit, rayOrigin, dmgMult);
                    dmgMult *= data.pierceDamageFalloff;
                    hitTargets++;
                    _lastPierceHits = hitTargets;
                    if (hitTargets >= maxTargets)
                    {
                        endPoint = hit.point;
                        break;
                    }
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EmitRay(data, rayOrigin, direction, endPoint, null, _lastPierceBlocked, _lastPierceHits);
#endif

            SpawnTracer(data, muzzlePosition, endPoint);
            SpawnSmokeTrail(data, muzzlePosition, endPoint);
        }

        // So sánh RaycastHit theo cự ly cho Array.Sort (generic => không box struct).
        private sealed class RaycastDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        // Random direction inside a cone of full angle coneAngleDeg around forward (shotgun spread).
        private static Vector3 ScatterDirection(Vector3 forward, float coneAngleDeg)
        {
            if (coneAngleDeg <= 0f) return forward;
            float half = coneAngleDeg * 0.5f * Mathf.Deg2Rad;
            float z = UnityEngine.Random.Range(Mathf.Cos(half), 1f);
            float t = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            Vector3 local = new Vector3(r * Mathf.Cos(t), r * Mathf.Sin(t), z);
            return Quaternion.LookRotation(forward) * local;
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
        private void ApplyRecoil(WeaponData data)
        {
            if (recoilPivot == null) return;

            // Sample 1 cell / 1 phat ban: advance phase theo golden-ratio (low-discrepancy => phan bo
            // kieu blue-noise, cac phat cach deu khong cum) thay vi scroll theo Time.time (2 phat nhanh
            // se trung pixel). Neu chua gan recoilNoiseTexture, chinh phase da la chuoi tot => xai thang.
            _recoilNoisePhase = Mathf.Repeat(_recoilNoisePhase + 0.61803398f, 1f);
            Vector2 noise = NoiseTextureSampler.Sample(recoilNoiseTexture, _recoilNoisePhase, _recoilNoiseSeed);
            float side = Mathf.Abs(noise.x) > 0.0001f ? noise.x : (_recoilNoisePhase * 2f - 1f);

            float inv = 1f / Mathf.Max(0.01f, data.recoilKickDuration);

            // Giat lui theo -Z.
            _recoilPosVel += new Vector3(0f, 0f, -data.recoilKickDistance) * inv;
            // Hat nong LEN (thanh phan chinh) - bien do recoilAimKickAngle.
            _recoilPitchVel += data.recoilAimKickAngle * inv;
            // Lech TRAI/PHAI (yaw) - noise/blue-noise dieu khien, bien do rieng recoilSideKickAngle.
            _recoilYawVel += side * data.recoilSideKickAngle * inv;
        }
    }
}
