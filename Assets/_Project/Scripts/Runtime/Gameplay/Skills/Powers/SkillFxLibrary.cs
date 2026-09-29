using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>
    /// Every power's assets and tuning, one section per power module. Lives at
    /// Assets/_Project/Data/Skills/SkillFxLibrary.asset and is referenced by the player's
    /// <see cref="SkillArsenal"/>; builders write here, never into the player prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/Skills/Skill FX Library", fileName = "SkillFxLibrary")]
    public sealed class SkillFxLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Shared
        {
            [Tooltip("Broad-phase layers a power may consider. Narrowing this is an optimisation; the " +
                     "correctness guarantee is the ZombieBase filter in PowerKit.Hit.")]
            public LayerMask enemyMask = ~0;
            [Tooltip("Flat trails on blades and drones.")]
            public Material trailMaterial;
            [Tooltip("ZombieWar/FX/SkillDisc: bomb shadows, frost bands, the Kinetic charge ring.")]
            public Material discMaterial;
            [Tooltip("The falling bomb shared by Airstrike, Ordnance and Carpet Bomb.")]
            public ParticleSystem bombFx;
            [Tooltip("Bombs fall from this height, at an angle along the run's flight line.")]
            public float bombFallHeight = 14f;
            public float bombDrift = 5f;
            [Tooltip("0.3 s from 12 m read as a streak; a bomb the eye can follow needs ~0.55 s.")]
            public float bombFallSeconds = 0.55f;
            [Tooltip("A soul flying from a kill to the player (Soul Burst / Reaper).")]
            public ParticleSystem soulWispFx;
            [Tooltip("The moment an evolution is taken.")]
            public ParticleSystem evolveFx;
            [Tooltip("ZombieWar/FX/ToonErode on the ring channel: toon ground shockwaves that eat themselves away.")]
            public Material shockwaveMaterial;
        }

        public Shared shared = new();
        public OrbitPower.Assets orbit = new();
        public DronePower.Assets drone = new();
        public FrostNovaPower.Assets frost = new();
        public FireTrailPower.Assets fireTrail = new();
        public BoomerangPower.Assets boomerang = new();
        public AirstrikePower.Assets airstrike = new();
        public OrdnancePower.Assets ordnance = new();
        public ChainPower.Assets chain = new();
        public SelfBurstPower.Assets selfBurst = new();
        public KineticShieldPower.Assets shield = new();
        public PoisonPower.Assets poison = new();
        public GunModsPower.Assets gunMods = new();
        public LauncherPower.Assets launcher = new();
        public GuardianAngelPower.Assets guardian = new();
        public ToxicCloudPower.Assets toxic = new();
        public GravityWellPower.Assets gravity = new();
        public ThornAuraPower.Assets thorns = new();
        public SentryTurretPower.Assets turret = new();
        public MeteorPower.Assets meteor = new();
        public StormCloudPower.Assets stormCloud = new();
        public IceShardsPower.Assets iceShards = new();
        public FlameBurstPower.Assets flameBurst = new();
        public LandminePower.Assets landmine = new();
        public SpinningAxePower.Assets axe = new();
        public WarDogPower.Assets warDog = new();
        public GroundStompPower.Assets stomp = new();
        public TimeWarpPower.Assets timeWarp = new();
        [Tooltip("Phase A8: what the Magnet, Bomb and Freeze Clock items play when taken.")]
        public ZombieWar.MechanicItems.Assets items = new();
    }
}
