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

        // Two-Bone IK Constraint targets (set up in the Editor - see Docs/EDITOR_SETUP_CHECKLIST.md)
        // must be repointed whenever the equipped weapon instance changes.
        public event Action<WeaponGripPoints> OnWeaponEquipped;

        private int _currentIndex;
        private GameObject _currentInstance;
        private WeaponGripPoints _currentGripPoints;
        private Vector3 _weaponRestLocalPosition;
        private float _fireCooldown;
        private float _recoilNoiseSeed;

        public WeaponData Current => weapons[_currentIndex];

        private void Awake()
        {
            _recoilNoiseSeed = UnityEngine.Random.value * 100f;
        }

        private void Start()
        {
            EquipWeapon(0);
        }

        private void Update()
        {
            if (_fireCooldown > 0f) _fireCooldown -= Time.deltaTime;
        }

        public void SwitchWeapon()
        {
            EquipWeapon((_currentIndex + 1) % weapons.Count);
        }

        public void TryFire(Vector3 aimDirection)
        {
            if (_fireCooldown > 0f) return;

            var data = Current;
            _fireCooldown = 1f / data.fireRate;

            Vector3 muzzlePosition = _currentGripPoints != null && _currentGripPoints.MuzzlePoint != null
                ? _currentGripPoints.MuzzlePoint.position
                : _currentInstance.transform.position;

            Vector3 hitPoint = muzzlePosition + aimDirection * data.range;

            if (Physics.Raycast(muzzlePosition, aimDirection, out RaycastHit hit, data.range, hitMask))
            {
                hitPoint = hit.point;
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(data.damage);
                if (data.impactPrefab != null)
                    Instantiate(data.impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }

            SpawnMuzzleFlash(data, muzzlePosition, aimDirection);
            SpawnSmokeTrail(data, muzzlePosition, hitPoint);

            Bill.Audio?.Play(data.fireSfxKey);
            ApplyRecoil(data);
        }

        private void EquipWeapon(int index)
        {
            if (_currentInstance != null) Destroy(_currentInstance);

            _currentIndex = index;
            var data = weapons[_currentIndex];

            _currentInstance = Instantiate(data.weaponPrefab, weaponSocket);
            _currentInstance.transform.localPosition = Vector3.zero;
            _currentInstance.transform.localRotation = Quaternion.identity;
            _weaponRestLocalPosition = _currentInstance.transform.localPosition;
            _currentGripPoints = _currentInstance.GetComponent<WeaponGripPoints>();

            OnWeaponEquipped?.Invoke(_currentGripPoints);
        }

        private static void SpawnMuzzleFlash(WeaponData data, Vector3 position, Vector3 direction)
        {
            if (data.muzzleFlashPrefab == null) return;
            Instantiate(data.muzzleFlashPrefab, position, Quaternion.LookRotation(direction));
        }

        // A single stretched particle standing in for a bullet-trail smoke effect - cheaper than a
        // real trail renderer and good enough at hitscan speed (the "particle" the brief asks for).
        private static void SpawnSmokeTrail(WeaponData data, Vector3 from, Vector3 to)
        {
            if (data.smokeTrailPrefab == null) return;

            float length = Vector3.Distance(from, to);
            var trail = Instantiate(data.smokeTrailPrefab, from, Quaternion.LookRotation(to - from));
            trail.transform.localScale = new Vector3(trail.transform.localScale.x, trail.transform.localScale.y, length);
        }

        private void ApplyRecoil(WeaponData data)
        {
            Transform weaponTransform = _currentInstance.transform;
            Vector2 noise = NoiseTextureSampler.Sample(recoilNoiseTexture, Time.time, _recoilNoiseSeed);
            Vector3 kickPosition = _weaponRestLocalPosition + new Vector3(noise.x, noise.y, -1f) * data.recoilKickDistance;

            var kickX = BillTween.LocalMoveX(weaponTransform, kickPosition.x, data.recoilKickDuration).SetEase(EaseType.OutQuad);
            var kickY = BillTween.LocalMoveY(weaponTransform, kickPosition.y, data.recoilKickDuration).SetEase(EaseType.OutQuad);
            var kickZ = BillTween.LocalMoveZ(weaponTransform, kickPosition.z, data.recoilKickDuration).SetEase(EaseType.OutQuad);

            BillTween.Sequence()
                .Append(kickX).Join(kickY).Join(kickZ)
                .AppendCallback(() =>
                {
                    var returnX = BillTween.LocalMoveX(weaponTransform, _weaponRestLocalPosition.x, data.recoilReturnDuration).SetEase(EaseType.OutSine);
                    var returnY = BillTween.LocalMoveY(weaponTransform, _weaponRestLocalPosition.y, data.recoilReturnDuration).SetEase(EaseType.OutSine);
                    var returnZ = BillTween.LocalMoveZ(weaponTransform, _weaponRestLocalPosition.z, data.recoilReturnDuration).SetEase(EaseType.OutSine);
                    BillTween.Sequence().Append(returnX).Join(returnY).Join(returnZ);
                });
        }
    }
}
