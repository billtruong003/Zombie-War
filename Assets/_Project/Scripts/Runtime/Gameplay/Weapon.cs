using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    public class Weapon : MonoBehaviour
    {
        [SerializeField] private List<WeaponData> weapons = new();
        [SerializeField] private Transform weaponSocket;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private Texture2D recoilNoiseTexture;

        [Tooltip("Screen-space trauma punched on every shot. Sells recoil in 3rd-person independent " +
                 "of the IK-grip feedback loop (which visually absorbs most of the gun-mount kick).")]
        [SerializeField] private float cameraShakeOnFire = 0.25f;
        private CameraFollow _cameraFollow;

        [Tooltip("Transform kicked on fire (spring). Default = weaponSocket. Because the IK hand " +
                 "targets ride the weapon grips, kicking the mount recoils the gun AND both hands together.")]
        [SerializeField] private Transform recoilPivot;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private float gizmoAimLength = 6f;

        // Two-Bone IK Constraint targets (set up in the Editor - see Docs/EDITOR_SETUP_CHECKLIST.md)
        // must be repointed whenever the equipped weapon instance changes.
        public event Action<WeaponGripPoints> OnWeaponEquipped;

        private int _currentIndex;
        private GameObject _currentInstance;
        private WeaponGripPoints _currentGripPoints;
        private Vector3 _weaponRestLocalPosition;
        private float _fireCooldown;
        private int _ammoInMag;
        private bool _reloading;
        private float _reloadTimer;

        private float _recoilNoiseSeed;
        private float _recoilNoisePhase; // advance 1 cell/phat ban (golden-ratio => blue-noise-ish)

        // Recoil spring (drives recoilPivot): back-kick (-Z) + muzzle climb (pitch up) + side sway (yaw).
        private Vector3 _pivotRestPos;
        private Quaternion _pivotRestRot;
        private Vector3 _recoilPos, _recoilPosVel;
        private float _recoilPitch, _recoilPitchVel;
        private float _recoilYaw, _recoilYawVel;

        public WeaponData Current => weapons.Count > 0 ? weapons[_currentIndex] : null;

        // Grips of the currently-equipped weapon instance (null until first Equip). Lets late-enabling
        // listeners (e.g. WeaponIKController) sync their state without waiting for the next equip event.
        public WeaponGripPoints CurrentGrips => _currentGripPoints;

        // HUD readouts.
        public int AmmoInMag => _ammoInMag;
        public int MagazineSize => Current != null ? Current.magazineSize : 0;
        public bool IsReloading => _reloading;
        public float ReloadProgress => _reloading && Current != null && Current.reloadDuration > 0f
            ? 1f - Mathf.Clamp01(_reloadTimer / Current.reloadDuration)
            : 1f;

#if UNITY_EDITOR
        // Editor-only hooks for the Grip Tuner inspector (WeaponEditor). The equipped gun model is a
        // child of recoilPivot; its LOCAL transform == gripLocalPosition/Euler (recoil kicks the pivot,
        // not the instance) so reading it back is a clean capture.
        public Transform EditorInstanceTransform => _currentInstance != null ? _currentInstance.transform : null;

        // Push tuned values onto the live instance so the user sees the change in Play Mode before saving.
        public void EditorApplyGrip(Vector3 localPos, Vector3 localEuler)
        {
            if (_currentInstance == null) return;
            _currentInstance.transform.localPosition = localPos;
            _currentInstance.transform.localRotation = Quaternion.Euler(localEuler);
            _weaponRestLocalPosition = localPos;
        }
#endif

        private void Awake()
        {
            _recoilNoiseSeed = UnityEngine.Random.value * 100f;
            EnsureRecoilPivot();
        }

        // Pivot ngoi GIUA weaponSocket va gun model. Sung + grip + muzzle deu la con cua no.
        // Rest = identity (0,0,0 / no rotation) nen spring luon keo ve DUNG rest, khong troi.
        // Neu chua gan trong editor thi tao runtime duoi weaponSocket.
        private void EnsureRecoilPivot()
        {
            if (recoilPivot == null && weaponSocket != null)
            {
                var go = new GameObject("RecoilPivot");
                recoilPivot = go.transform;
                recoilPivot.SetParent(weaponSocket, false);
                recoilPivot.localPosition = Vector3.zero;
                recoilPivot.localRotation = Quaternion.identity;
            }
            if (recoilPivot != null)
            {
                _pivotRestPos = recoilPivot.localPosition;
                _pivotRestRot = recoilPivot.localRotation;
            }
        }

        private void Start()
        {
            EquipWeapon(0);
        }

        private void Update()
        {
            if (_fireCooldown > 0f) _fireCooldown -= Time.deltaTime;

            TickReload();
            TryAutoFire();
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

        // No fire button - walking a zombie into weapon range is the only thing that triggers
        // shooting. TryFire's own cooldown gate makes this safe to call every frame.
        private void TryAutoFire()
        {
            var player = PlayerMovement.Instance;
            if (player == null || !player.HasTarget) return;
            if (player.AimTargetDistance > Current.range) return;

            TryFire(player.AimDirection);
        }

                // Auto-reload: no fire button on mobile, so an empty mag just refills after a beat.
        // The reload gap is the intended 'gai dan' rhythm - TryFire is gated out while reloading.
        private void TickReload()
        {
            if (!_reloading) return;
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                _reloading = false;
                _ammoInMag = Current != null ? Current.magazineSize : 0;
            }
        }

        private void StartReload()
        {
            if (Current == null || Current.magazineSize <= 0 || _reloading) return;
            _reloading = true;
            _reloadTimer = Current.reloadDuration;
            if (!string.IsNullOrEmpty(Current.reloadSfxKey)) Bill.Audio?.Play(Current.reloadSfxKey);
        }

public void SwitchWeapon()
        {
            EquipWeapon((_currentIndex + 1) % weapons.Count);
        }

        public void TryFire(Vector3 aimDirection)
        {
            if (_fireCooldown > 0f) return;
            if (_reloading) return;

            var data = Current;
            // Het dan -> nap dan, chan phat ban tao khoang khung.
            if (data.magazineSize > 0 && _ammoInMag <= 0)
            {
                StartReload();
                return;
            }

            _fireCooldown = 1f / data.fireRate;
            if (data.magazineSize > 0)
            {
                _ammoInMag--;
                if (_ammoInMag <= 0) StartReload();
            }

            // Ban theo TRUC NONG (MuzzlePoint.forward = +Z cua muzzle) => tracer luon thang ra tu
            // nong, khong "meo" so voi than sung. Auto-aim + Multi-Aim IK lo viec chia nong vao zombie.
            bool hasMuzzle = _currentGripPoints != null && _currentGripPoints.MuzzlePoint != null;
            Vector3 muzzlePosition = hasMuzzle
                ? _currentGripPoints.MuzzlePoint.position
                : _currentInstance.transform.position;
            Vector3 muzzleForward = hasMuzzle
                ? _currentGripPoints.MuzzlePoint.forward
                : aimDirection;

            int pellets = Mathf.Max(1, data.pelletCount);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = (pellets > 1 || data.spreadAngle > 0f)
                    ? ScatterDirection(muzzleForward, data.spreadAngle)
                    : muzzleForward;
                FireRay(data, muzzlePosition, dir);
            }

            SpawnMuzzleFlash(data, muzzlePosition, muzzleForward);

            Bill.Audio?.Play(data.fireSfxKey);
            ApplyRecoil(data);
            ShakeCamera(cameraShakeOnFire);
        }

        // Screen shake is what actually reads as "recoil" in 3rd-person; the gun-mount spring is
        // largely cancelled by the hand IK chasing the grips. Mirrors Bomb's camera lookup, cached.
        private void ShakeCamera(float amount)
        {
            if (amount <= 0f) return;
            if (_cameraFollow == null && Camera.main != null)
                Camera.main.TryGetComponent(out _cameraFollow);
            if (_cameraFollow != null) _cameraFollow.Shake(amount);
        }

        private void FireRay(WeaponData data, Vector3 muzzlePosition, Vector3 direction)
        {
            Vector3 hitPoint = muzzlePosition + direction * data.range;

            if (Physics.Raycast(muzzlePosition, direction, out RaycastHit hit, data.range, hitMask))
            {
                hitPoint = hit.point;
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(data.damage);
                if (data.impactPrefab != null)
                    FxPool.Play(data.impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }

            SpawnTracer(data, muzzlePosition, hitPoint);
            SpawnSmokeTrail(data, muzzlePosition, hitPoint);
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

        private void EquipWeapon(int index)
        {
            if (_currentInstance != null) Destroy(_currentInstance);

            _currentIndex = index;
            var data = weapons[_currentIndex];

            // Nap day bang khi doi sung - khong bat dau bang mot lan reload kho hieu.
            _ammoInMag = data.magazineSize;
            _reloading = false;
            _reloadTimer = 0f;

            // Sung + grip + muzzle deu la con cua recoilPivot => recoil kick pivot la ca cum theo.
            EnsureRecoilPivot();
            _currentInstance = Instantiate(data.weaponPrefab, recoilPivot != null ? recoilPivot : weaponSocket);
            _currentInstance.transform.localPosition = data.gripLocalPosition;
            _currentInstance.transform.localRotation = Quaternion.Euler(data.gripLocalEuler);
            _weaponRestLocalPosition = _currentInstance.transform.localPosition;
            _currentGripPoints = _currentInstance.GetComponent<WeaponGripPoints>();

            OnWeaponEquipped?.Invoke(_currentGripPoints);
        }

        private static void SpawnMuzzleFlash(WeaponData data, Vector3 position, Vector3 direction)
        {
            if (data.muzzleFlashPrefab == null) return;
            FxPool.Play(data.muzzleFlashPrefab, position, Quaternion.LookRotation(direction));
        }

        // Pooled one-shot mesh tracer (MeshTracer handles the stretch + fade animation itself).
        private static void SpawnTracer(WeaponData data, Vector3 from, Vector3 to)
        {
            if (data.tracerPrefab == null) return;
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
