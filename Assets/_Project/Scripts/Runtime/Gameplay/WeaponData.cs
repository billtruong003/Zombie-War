using UnityEngine;

namespace ZombieWar
{
    [CreateAssetMenu(menuName = "ZombieWar/Weapon Data", fileName = "WD_")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName = "Weapon";
        public GameObject weaponPrefab;

        [Header("Handling")]
        [Tooltip("Two-handed = support (left) hand IK also grips the weapon. Off for one-handed (SMG/pistol).")]
        public bool twoHanded = true;

        [Header("Combat")]
        public float fireRate = 8f;
        public float damage = 12f;
        public float range = 30f;
        [Tooltip("Hold-to-fire (AR). If false, one shot per press (pistol/shotgun).")]
        public bool automatic = false;

        [Header("Magazine / Reload")]
        [Tooltip("So dan moi bang. 0 = vo han, khong can nap.")]
        public int magazineSize = 12;
        [Tooltip("Thoi gian nap dan (giay) khi het bang - tao khoang khung 'gai dan'.")]
        public float reloadDuration = 1.2f;
        public string reloadSfxKey = "gun_reload";


        [Header("Spread (shotgun = many pellets)")]
        [Tooltip("Raycasts per trigger pull. 1 = single shot.")]
        public int pelletCount = 1;
        [Tooltip("Full cone angle (deg) pellets scatter within.")]
        public float spreadAngle = 0f;

        [Header("VFX / SFX")]
        public ParticleSystem muzzleFlashPrefab;
        public ParticleSystem smokeTrailPrefab;
        public ParticleSystem impactPrefab;
        [Tooltip("Mesh tracer prefab (MeshTracer). One spawned per pellet.")]
        public GameObject tracerPrefab;
        public string fireSfxKey = "gun_fire";

        [Header("Recoil (noise-driven, see NoiseTextureSampler)")]
        public float recoilKickDistance = 0.09f;
        public float recoilKickDuration = 0.04f;
        public float recoilReturnDuration = 0.12f;

        [Tooltip("Bien do HAT LEN (pitch, do) moi phat - thanh phan chinh cua muzzle climb.")]
        public float recoilAimKickAngle = 5f;

        [Tooltip("Bien do LECH NGANG (yaw, do) moi phat - noise/blue-noise dieu khien dau (trai/phai). " +
                 "= 0 => khong lech ngang.")]
        public float recoilSideKickAngle = 2f;

        [Header("Hold offset (local to hand WeaponSocket, tuned in aim pose)")]
        // Aligns the prefab's RightHandGrip into the palm and the barrel (+Z) with the aim
        // direction. Defaults are tuned for the Low-Poly Pistol on the QuickRig hand.
        public Vector3 gripLocalPosition = new Vector3(0.00428f, -0.06307f, 0.00193f);
        public Vector3 gripLocalEuler = new Vector3(15.425f, 110.259f, 176.855f);
    }
}
