using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    // Vai trò tactical + routing cho HUD icon / shop card / build archetype. Xem Docs/Reference/Design/WEAPON_DESIGN.md §2.
    public enum WeaponClass
    {
        Sidearm, SMG, AssaultRifle, Shotgun, LMG,
        Marksman, Railgun, Flamethrower, Tesla, Laser, Rocket
    }

    // How a shot meets the world. Only the modes the Weapon actually implements exist; beam,
    // projectile and chain were enum values with no code behind them and are gone.
    public enum FireMode
    {
        SingleHitscan = 0,       // 1 ray, stops at the first target (pistol/AR/LMG)
        MultiPelletHitscan = 1,  // N rays in a cone (shotgun; pelletCount drives it)
        PiercingLine = 2,        // passes through targets (sniper)
    }

    // Độ hiếm → màu shop/HUD + hệ số giá. Xem Docs/Reference/Design/WEAPON_DESIGN.md §5.
    public enum WeaponTier { Common, Uncommon, Rare, Epic, Legendary }

    [CreateAssetMenu(menuName = "ZombieWar/Weapon Data", fileName = "WD_")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName = "Weapon";
        public GameObject weaponPrefab;

        // ─────────────────────────────────────────────────────────────────
        // STABLE IDENTITY (roster migration, see Docs/WeaponRosterMapping.json)
        // weaponId is the PERSISTENT save/equip key — immutable once shipped, independent of
        // asset path/filename/display name. catalogOrder is presentation/debug order ONLY
        // (shop grid, HUD roster, Weapon.EquipIndex) — never persisted as equipment identity.
        // ─────────────────────────────────────────────────────────────────
        [Header("Stable Identity (do not hand-edit after ship — see WeaponRosterMigration)")]
        [Tooltip("Immutable save/equip key. Format: weapon.<family>.<model>, lowercase ascii dot notation.")]
        [SerializeField] private string weaponId = "";
        [Tooltip("Presentation/debug order only (shop grid, HUD roster). NOT a persistent identity.")]
        [SerializeField] private int catalogOrder = -1;
        [Tooltip("Previous asset names this WeaponData was known as, oldest first. Renaming the asset " +
            "does NOT change these — LoadoutState.Resolve falls back to them so pre-migration saves " +
            "(which stored the old asset name as the equip id) keep resolving after a rename.")]
        [SerializeField] private string[] legacyAliases = Array.Empty<string>();

        public string WeaponId => weaponId;
        public int CatalogOrder => catalogOrder;
        public IReadOnlyList<string> LegacyAliases => legacyAliases;

        // ─────────────────────────────────────────────────────────────────
        // AUTHORING GATE (M7.1)
        // The owner hand-authors every grip/muzzle anchor. Nothing infers them. A weapon onboarded
        // from a vendor pack therefore arrives UNPLAYABLE and stays that way until the owner has
        // placed its anchors and signed it off, so a half-authored gun can never reach the Hub.
        // Default is Ready so all 25 previously shipped weapons keep their behaviour untouched.
        // ─────────────────────────────────────────────────────────────────
        public enum AuthoringStatus
        {
            /// Anchors authored and signed off by the owner. Equippable.
            Ready = 0,
            /// Onboarded as data only. Grip/muzzle NOT placed. Must never be equippable.
            PendingOwnerAuthoring = 1,
            /// Needs a runtime capability that does not exist yet (e.g. FireMode.Projectile).
            BlockedNeedsProjectileFireMode = 2,
            /// Geometry is far outside the arsenal's poly envelope. Held out of the playable pool
            /// even after anchors are authored, until the owner decides keep / decimate / cut.
            BlockedNeedsDecimation = 3,
        }

        [Header("Authoring gate (M7.1)")]
        [Tooltip("PendingOwnerAuthoring = onboarded as data, anchors not yet hand-authored by the " +
                 "owner. Such a weapon must never be equippable or reach Hub/shop/loadout.")]
        [SerializeField] private AuthoringStatus authoringStatus = AuthoringStatus.Ready;

        public AuthoringStatus Authoring => authoringStatus;

        /// <summary>True only when the owner has authored anchors and no capability is missing.</summary>
        public bool IsPlayable => authoringStatus == AuthoringStatus.Ready;

        // ─────────────────────────────────────────────────────────────────
        // IDENTITY / SHOP (Docs/Reference/Design/WEAPON_DESIGN.md §2,§5,§6,§9)
        // ─────────────────────────────────────────────────────────────────
        [Header("Identity / Shop")]
        public WeaponClass weaponClass = WeaponClass.AssaultRifle;
        public WeaponTier tier = WeaponTier.Common;
        [Tooltip("Giá mua trong shop (in-run cash). 0 = súng khởi đầu / không bán.")]
        public int price = 0;
        [Tooltip("Giá unlock vĩnh viễn (meta currency). 0 = mở sẵn.")]
        public int unlockCost = 0;

        [Header("Handling")]
        [Tooltip("Two-handed = support (left) hand IK also grips the weapon. Off for one-handed (SMG/pistol).")]
        public bool twoHanded = true;

        // ─────────────────────────────────────────────────────────────────
        // FIRE MODEL (Docs/Reference/Design/WEAPON_DESIGN.md §3)
        // ─────────────────────────────────────────────────────────────────
        [Header("Fire model")]
        public FireMode fireMode = FireMode.SingleHitscan;

        [Header("Combat")]
        public float fireRate = 8f;
        public float damage = 12f;
        public float range = 30f;
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

        // ── Splash (launcher): the shot blows up where it lands and hurts everyone around it.
        [Header("Splash (launcher)")]
        [Tooltip("Blast radius (m) where the shot lands. 0 = no blast (every gun but the launcher).")]
        public float splashRadius = 0f;
        [Tooltip("Share of the shot's damage dealt to every OTHER enemy in the blast.")]
        [Range(0f, 1f)] public float splashDamageFraction = 0.6f;
        public ParticleSystem splashFx;
        public string splashSfxKey = "sfx.skill.airstrike.blast";

        // M4: `magazineSize` and `reloadDuration` are GONE. Weapons no longer have a magazine and
        // never reload - while a valid target is in range the weapon fires continuously at its
        // effective fire rate, and weapon identity comes from cadence and per-weapon behaviour
        // instead of from magazine downtime. Unity drops the two retired keys from the 25 weapon
        // assets on their next save; nothing at runtime reads them any more.
        //
        // `reloadSfxKey` deliberately SURVIVES. Gameplay no longer plays it, but the addressable
        // audio catalog tooling reads and writes this field, and that work is in flight and not
        // owned by this milestone. Removing the field would break that tooling for no gameplay gain.
        [Tooltip("KHÔNG còn dùng trong gameplay từ M4 (súng không nạp đạn nữa). " +
                 "Giữ lại vì công cụ catalog audio addressable đang đọc/ghi trường này.")]
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

        [Header("Hold offset (local to GunMount/RecoilPivot inside WeaponRig, tuned in aim pose)")]
        // Model TRS under the rig's RecoilPivot. The hands IK toward the grips this transform
        // places (see WeaponIKController) - the hands never carry the weapon.
        public Vector3 gripLocalPosition = new Vector3(0.00428f, -0.06307f, 0.00193f);
        public Vector3 gripLocalEuler = new Vector3(15.425f, 110.259f, 176.855f);
        [Tooltip("Uniform-friendly per-axis scale applied to the instantiated weapon root.")]
        public Vector3 gripLocalScale = Vector3.one;

        [Header("Authored hand grips (local to instantiated weapon root)")]
        [Tooltip("When enabled, Weapon restores the captured grip positions before notifying the IK rig. " +
                 "When disabled, the grip markers authored in the weapon prefab remain the fallback.")]
        public bool useAuthoredGripPositions;
        [Tooltip("Captured RightHandTarget position expressed in weapon-root local space.")]
        public Vector3 rightHandGripRootPosition;
        [Tooltip("Captured LeftHandTarget position expressed in weapon-root local space. Used by two-handed weapons.")]
        public Vector3 leftHandGripRootPosition;

        // ─────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Nhân damage theo cự ly chuẩn hoá 0..1. Rỗng curve = phẳng 1.0.</summary>
        public float RangeFalloff(float distance01)
        {
            if (damageFalloffCurve == null || damageFalloffCurve.length == 0) return 1f;
            return Mathf.Max(0f, damageFalloffCurve.Evaluate(Mathf.Clamp01(distance01)));
        }

        /// <summary>Tint for the toon "clay" icon (M8): the rarity colour lifted toward white, so a
        /// light icon reads on its rarity tile without turning into a flat colour blob.</summary>
        public Color IconTint => Color.Lerp(TierColor, Color.white, 0.55f);

        /// <summary>M8 icon tile: the rarity colour, a quarter of the way to the UI ground so white
        /// text and the dark icon outline both read on it.</summary>
        public Color TileColor => Color.Lerp(TierColor, new Color(0.118f, 0.125f, 0.172f), 0.25f);

        /// <summary>Màu tier cho shop/HUD (Docs/Reference/Design/WEAPON_DESIGN.md §5).</summary>
        public Color TierColor => tier switch
        {
            WeaponTier.Common     => new Color(0.72f, 0.76f, 0.80f), // #B8C2CC
            WeaponTier.Uncommon   => new Color(0.25f, 0.73f, 0.31f), // #3FB950
            WeaponTier.Rare       => new Color(0.23f, 0.51f, 0.96f), // #3B82F6
            WeaponTier.Epic       => new Color(0.66f, 0.33f, 0.97f), // #A855F7
            WeaponTier.Legendary  => new Color(0.96f, 0.65f, 0.14f), // #F5A623
            _                     => Color.white
        };
    }
}
