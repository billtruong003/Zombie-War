using UnityEngine;

namespace ZombieWar
{
    [CreateAssetMenu(menuName = "ZombieWar/Weapon Data", fileName = "WD_")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName = "Weapon";
        public GameObject weaponPrefab;

        [Header("Combat")]
        public float fireRate = 8f;
        public float damage = 12f;
        public float range = 30f;

        [Header("VFX / SFX")]
        public ParticleSystem muzzleFlashPrefab;
        public ParticleSystem smokeTrailPrefab;
        public ParticleSystem impactPrefab;
        public string fireSfxKey = "gun_fire";

        [Header("Recoil (noise-driven, see NoiseTextureSampler)")]
        public float recoilKickDistance = 0.05f;
        public float recoilKickDuration = 0.04f;
        public float recoilReturnDuration = 0.12f;
        public float recoilAimKickAngle = 1.5f;
    }
}
