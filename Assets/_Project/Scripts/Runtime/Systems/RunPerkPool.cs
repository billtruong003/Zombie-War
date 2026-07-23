using System.Collections.Generic;

namespace ZombieWar
{
    /// <summary>
    /// The authored pool of temporary run perks and the 1-of-3 draw shown on level-up.
    ///
    /// Kept as code rather than a ScriptableObject on purpose: the list is short, every entry is a
    /// pure number, and having it here means the perk IDs used by tests and by Pass missions cannot
    /// silently diverge from an asset someone edited. Promote it to an asset when designers need to
    /// tune it without a recompile.
    /// </summary>
    public static class RunPerkPool
    {
        public static readonly IReadOnlyList<RunPerk> All = new List<RunPerk>
        {
            new RunPerk("perk.damage.s",    "Damage +15%",     "+15% damage on all weapons.",      RunPerkKind.Damage,     1.15f),
            new RunPerk("perk.damage.m",    "Damage +30%",     "+30% damage on all weapons.",      RunPerkKind.Damage,     1.30f),
            new RunPerk("perk.firerate.s",  "Fire Rate +12%",  "Shoot 12% faster.",                RunPerkKind.FireRate,   1.12f),
            new RunPerk("perk.firerate.m",  "Fire Rate +25%",  "Shoot 25% faster.",                RunPerkKind.FireRate,   1.25f),
            new RunPerk("perk.speed.s",     "Move Speed +10%", "Move 10% faster.",                 RunPerkKind.MoveSpeed,  1.10f),
            new RunPerk("perk.health.s",    "Max Health +20%", "+20% maximum health.",             RunPerkKind.MaxHealth,  1.20f),
            new RunPerk("perk.coin.s",      "Coin Drops +25%", "+25% Coin earned in a stage.",     RunPerkKind.CoinGain,   1.25f),
        };

        /// <summary>Draws <paramref name="count"/> distinct perks. Uses UnityEngine.Random so a test
        /// can seed it and get a deterministic offer.</summary>
        public static List<RunPerk> Draw(int count = 3)
        {
            var pool = new List<RunPerk>(All);
            var picked = new List<RunPerk>(count);
            while (picked.Count < count && pool.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, pool.Count);
                picked.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picked;
        }
    }
}
