namespace ZombieWar
{
    /// PlayerProfile, part: evolved guns (backlog #23).
    public static partial class PlayerProfile
    {
        public static bool IsGunEvolved(string weaponId) => !string.IsNullOrEmpty(weaponId) && Data.evolvedGuns.Contains(weaponId);

        public static GunEvolution.Block EvolveBlock(string weaponId) =>
            GunEvolution.Check(weaponId, GetWeaponLevel(weaponId), MasteryLevel(weaponId), AccountLevel,
                               IsWeaponOwned(weaponId), IsGunEvolved(weaponId));

        /// <summary>Evolves a gun once all conditions hold. Saved before anything shows it.</summary>
        public static bool TryEvolveGun(string weaponId)
        {
            if (EvolveBlock(weaponId) != GunEvolution.Block.None) return false;
            Data.evolvedGuns.Add(weaponId);
            SaveNow();
            Notify(Change.Loadout);
            return true;
        }
    }
}
