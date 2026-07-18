using UnityEngine;

namespace ZombieWar
{
    // Vai trò tactical + routing cho HUD icon / shop card / build archetype. Xem Docs/WEAPON_DESIGN.md §2.
    public enum WeaponClass
    {
        Sidearm, SMG, AssaultRifle, Shotgun, LMG,
        Marksman, Railgun, Flamethrower, Tesla, Laser, Rocket
    }

    // Cách đạn tương tác thế giới. Xem Docs/WEAPON_DESIGN.md §3.
    public enum FireMode
    {
        SingleHitscan,       // 1 ray, dừng ở target đầu (pistol/AR/LMG)
        MultiPelletHitscan,  // N ray trong cone, mỗi cái dừng ở target đầu (shotgun)
        PiercingLine,        // RaycastAll, damage tất cả trên đường (sniper/railgun)
        ContinuousBeam,      // tick damage liên tục, ramp khi giữ (laser/flamethrower)
        Projectile,          // spawn vật thể bay, nổ AoE (rocket/GL)
        ChainLightning       // hit đầu → nhảy sang target gần (tesla)
    }

    // Độ hiếm → màu shop/HUD + hệ số giá. Xem Docs/WEAPON_DESIGN.md §5.
    public enum WeaponTier { Common, Uncommon, Rare, Epic, Legendary }

    // Loại đạn dùng chung cho kinh tế shop (mua 1 thùng Heavy → nhiều súng dùng). §4.
    public enum AmmoType { None, Light, Heavy, Shell, Energy, Rocket }

    // Mô hình tài nguyên bắn. §4.
    public enum ResourceModel { Magazine, Heat, Charge, Free }

    [CreateAssetMenu(menuName = "ZombieWar/Weapon Data", fileName = "WD_")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName = "Weapon";
        public GameObject weaponPrefab;

        // ─────────────────────────────────────────────────────────────────
        // IDENTITY / SHOP (Docs/WEAPON_DESIGN.md §2,§5,§6,§9)
        // ─────────────────────────────────────────────────────────────────
        [Header("Identity / Shop")]
        public WeaponClass weaponClass = WeaponClass.AssaultRifle;
        public WeaponTier tier = WeaponTier.Common;
        [Tooltip("Role tag hiển thị trên shop card, đọc trong 1 giây. VD 'Railgun', 'Bloodletter'.")]
        public string roleTag = "";
        [Tooltip("Build hint 1 dòng: chọn súng này = lối chơi gì. VD 'Pierce — farm hàng dọc'.")]
        [TextArea(1, 2)] public string buildHint = "";
        [Tooltip("Tag để hệ upgrade (level-up in-run) nghiêng perk pool. VD 'crit','pierce','melt'. §6.")]
        public string buildTag = "generalist";
        [Tooltip("Giá mua trong shop (in-run cash). 0 = súng khởi đầu / không bán.")]
        public int price = 0;
        [Tooltip("Giá unlock vĩnh viễn (meta currency). 0 = mở sẵn.")]
        public int unlockCost = 0;

        [Header("Handling")]
        [Tooltip("Two-handed = support (left) hand IK also grips the weapon. Off for one-handed (SMG/pistol).")]
        public bool twoHanded = true;

        // ─────────────────────────────────────────────────────────────────
        // FIRE MODEL (Docs/WEAPON_DESIGN.md §3)
        // ─────────────────────────────────────────────────────────────────
        [Header("Fire model")]
        public FireMode fireMode = FireMode.SingleHitscan;

        [Header("Combat")]
        public float fireRate = 8f;
        public float damage = 12f;
        public float range = 30f;
        [Tooltip("Hold-to-fire (AR). If false, one shot per press (pistol/shotgun).")]
        public bool automatic = false;
        [Tooltip("Knockback đẩy zombie mỗi hit (0 = không). §8.")]
        public float knockback = 0f;
        [Tooltip("Damage theo cự ly chuẩn hoá 0..1 (0=nòng, 1=range max). Rỗng = phẳng 1.0. §3.")]
        public AnimationCurve damageFalloffCurve = null;

        // ── Piercing (FireMode.PiercingLine) — súng bắn tỉa/railgun xuyên hàng. §3,§7
        [Header("Piercing (sniper / railgun)")]
        [Tooltip("Số zombie 1 phát xuyên qua. 0 = dừng ở con đầu (súng thường), -1 = xuyên VÔ HẠN (railgun).")]
        public int pierceCount = 0;
        [Tooltip("Nhân damage sau mỗi lần xuyên (1 = không giảm, 0.85 = giảm 15%/con). §3.")]
        [Range(0f, 1f)] public float pierceDamageFalloff = 1f;

        // ── Beam (FireMode.ContinuousBeam) — laser/flamethrower. §3
        [Header("Beam (laser / flamethrower)")]
        [Tooltip("Số tick damage/giây khi giữ tia.")]
        public float beamTickRate = 20f;
        [Tooltip("Damage cộng thêm mỗi giây dí tia trên CÙNG 1 target (melt ramp). 0 = không ramp.")]
        public float beamRampPerSecond = 0f;
        [Tooltip("Trần ramp (nhân damage tối đa). VD 3 = tối đa x3 sau khi dí đủ lâu.")]
        public float beamMaxRamp = 1f;
        [Tooltip("Bề rộng tia (m). 0 = tia mảnh. >0 = quét trúng nhiều con (flamethrower cone).")]
        public float beamWidth = 0f;

        // ── Projectile (FireMode.Projectile) — rocket/GL. §3
        [Header("Projectile (rocket / GL)")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 30f;
        [Tooltip("Bán kính nổ AoE (m).")]
        public float explosionRadius = 0f;
        [Tooltip("Nhân damage ở rìa vụ nổ (1 = full khắp bán kính, 0 = tâm full → rìa 0).")]
        [Range(0f, 1f)] public float explosionEdgeFalloff = 0.3f;

        // ── Chain (FireMode.ChainLightning) — tesla. §3
        [Header("Chain (tesla)")]
        [Tooltip("Số lần sét nhảy sang target kế.")]
        public int chainCount = 0;
        [Tooltip("Bán kính tìm target kế để nhảy (m).")]
        public float chainRange = 4f;
        [Tooltip("Nhân damage mỗi lần nhảy.")]
        [Range(0f, 1f)] public float chainDamageFalloff = 0.8f;

        // ─────────────────────────────────────────────────────────────────
        // RESOURCE MODEL (Docs/WEAPON_DESIGN.md §4)
        // ─────────────────────────────────────────────────────────────────
        [Header("Resource model")]
        public ResourceModel resourceModel = ResourceModel.Magazine;
        [Tooltip("Loại đạn dùng chung cho kinh tế shop. §4.")]
        public AmmoType ammoType = AmmoType.Light;

        [Header("Magazine / Reload")]
        [Tooltip("So dan moi bang. 0 = vo han, khong can nap.")]
        public int magazineSize = 12;
        [Tooltip("Thoi gian nap dan (giay) khi het bang - tao khoang khung 'gai dan'.")]
        public float reloadDuration = 1.2f;
        public string reloadSfxKey = "gun_reload";

        [Header("Heat (laser / tesla / flamethrower)")]
        [Tooltip("Nhiệt cộng mỗi shot/tick. 0 = không dùng heat.")]
        public float heatPerShot = 0f;
        [Tooltip("Trần nhiệt trước khi overheat khoá nòng.")]
        public float heatCapacity = 100f;
        [Tooltip("Nhiệt giảm/giây khi nhả cò.")]
        public float coolRate = 40f;
        [Tooltip("Thời gian khoá nòng (giây) khi overheat chạm trần.")]
        public float overheatLockTime = 1.5f;

        [Header("Charge (railgun / sniper)")]
        [Tooltip("Thời gian sạc đầy (giây) để đạt full damage/pierce. 0 = bắn tức thì.")]
        public float chargeTime = 0f;
        [Tooltip("Cho giữ sạc (true) hay tự bắn khi đầy (false).")]
        public bool chargeHold = true;

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
        [Tooltip("LineRenderer prefab cho beam liên tục (laser). Dùng thay tracer khi ContinuousBeam.")]
        public GameObject beamPrefab;
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

        // ─────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Nhân damage theo cự ly chuẩn hoá 0..1. Rỗng curve = phẳng 1.0.</summary>
        public float RangeFalloff(float distance01)
        {
            if (damageFalloffCurve == null || damageFalloffCurve.length == 0) return 1f;
            return Mathf.Max(0f, damageFalloffCurve.Evaluate(Mathf.Clamp01(distance01)));
        }

        /// <summary>Màu tier cho shop/HUD (Docs/WEAPON_DESIGN.md §5).</summary>
        public Color TierColor => tier switch
        {
            WeaponTier.Common     => new Color(0.72f, 0.76f, 0.80f), // #B8C2CC
            WeaponTier.Uncommon   => new Color(0.25f, 0.73f, 0.31f), // #3FB950
            WeaponTier.Rare       => new Color(0.23f, 0.51f, 0.96f), // #3B82F6
            WeaponTier.Epic       => new Color(0.66f, 0.33f, 0.97f), // #A855F7
            WeaponTier.Legendary  => new Color(0.96f, 0.65f, 0.14f), // #F5A623
            _                     => Color.white
        };

        /// <summary>Súng dùng heat thay vì magazine?</summary>
        public bool UsesHeat => resourceModel == ResourceModel.Heat;
        /// <summary>Súng cần sạc trước khi bắn?</summary>
        public bool UsesCharge => resourceModel == ResourceModel.Charge || chargeTime > 0f;
    }
}
