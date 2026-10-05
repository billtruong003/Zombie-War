namespace ZombieWar
{
    /// <summary>
    /// Gun evolution (backlog #23, owner-approved 05/10): a gun at 3 stars and mastery 10 (account
    /// level 8 and up) can evolve once. An evolved gun carries its family's trademark effect into every
    /// run as a free, built-in card rank that takes no skill slot (pistol shots bounce, rifle kills
    /// burst, shotgun blasts stun, SMG fire ramps, sniper rounds pierce everything...).
    /// </summary>
    public static class GunEvolution
    {
        public const int StarsNeeded = 3, AccountLevelNeeded = 8;

        public enum Block { None, NotOwned, Stars, Mastery, AccountLevel, AlreadyEvolved }

        /// <summary>Why this gun cannot evolve yet, or None when it can.</summary>
        public static Block Check(string weaponId, int stars, int mastery, int accountLevel, bool owned, bool evolved)
        {
            if (!owned) return Block.NotOwned;
            if (evolved) return Block.AlreadyEvolved;
            if (accountLevel < AccountLevelNeeded) return Block.AccountLevel;
            if (stars < StarsNeeded) return Block.Stars;
            if (mastery < GunMastery.MaxLevel) return Block.Mastery;
            return Block.None;
        }

        public static string BlockText(Block b) => b switch
        {
            Block.NotOwned => "Own this gun first",
            Block.Stars => $"Needs {StarsNeeded} stars",
            Block.Mastery => $"Needs mastery {GunMastery.MaxLevel}",
            Block.AccountLevel => $"Unlocks at level {AccountLevelNeeded}",
            Block.AlreadyEvolved => "Evolved",
            _ => "Ready to evolve",
        };

        /// <summary>The built-in card an evolved gun of this family carries, and its rank.</summary>
        public static (string card, int rank) TraitOf(WeaponClass f) => f switch
        {
            WeaponClass.Sidearm => (Skills.SkillCatalogDefs.UniRicochet, 3),
            WeaponClass.SMG => (Skills.SkillCatalogDefs.SmgBulletHose, 3),
            WeaponClass.AssaultRifle => (Skills.SkillCatalogDefs.UniExplosive, 2),
            WeaponClass.Shotgun => (Skills.SkillCatalogDefs.ShotgunConcussion, 3),
            WeaponClass.LMG => (Skills.SkillCatalogDefs.LmgShockwave, 3),
            WeaponClass.Marksman => (Skills.SkillCatalogDefs.UniPierce, 5),
            WeaponClass.Railgun => (Skills.SkillCatalogDefs.UniPierce, 5),
            WeaponClass.Flamethrower => (Skills.SkillCatalogDefs.UniAcid, 3),
            WeaponClass.Tesla => (Skills.SkillCatalogDefs.UniRicochet, 3),
            WeaponClass.Laser => (Skills.SkillCatalogDefs.UniCrit, 3),
            _ => (Skills.SkillCatalogDefs.UniExplosive, 3),
        };

        /// <summary>"Every shot bounces to nearby enemies" — the line the evolve screen shows.</summary>
        public static string TraitText(WeaponClass f) => f switch
        {
            WeaponClass.Sidearm or WeaponClass.Tesla => "Shots bounce to nearby enemies",
            WeaponClass.SMG => "Fire rate climbs while you keep shooting",
            WeaponClass.AssaultRifle or WeaponClass.Rocket => "Kills burst in a small blast",
            WeaponClass.Shotgun => "Blasts knock back and stun",
            WeaponClass.LMG => "Every few shots send a shockwave",
            WeaponClass.Marksman or WeaponClass.Railgun => "Rounds pierce every enemy in the line",
            WeaponClass.Flamethrower => "Hits leave burning acid",
            _ => "More critical hits",
        };
    }
}
