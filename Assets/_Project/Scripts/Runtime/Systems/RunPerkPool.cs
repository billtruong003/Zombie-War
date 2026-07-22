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
            new RunPerk("perk.damage.s",    "Sát thương +15%", "Tăng 15% sát thương mọi vũ khí.",  RunPerkKind.Damage,     1.15f),
            new RunPerk("perk.damage.m",    "Sát thương +30%", "Tăng 30% sát thương mọi vũ khí.",  RunPerkKind.Damage,     1.30f),
            new RunPerk("perk.firerate.s",  "Tốc độ bắn +12%", "Bắn nhanh hơn 12%.",               RunPerkKind.FireRate,   1.12f),
            new RunPerk("perk.firerate.m",  "Tốc độ bắn +25%", "Bắn nhanh hơn 25%.",               RunPerkKind.FireRate,   1.25f),
            new RunPerk("perk.speed.s",     "Chạy nhanh +10%", "Di chuyển nhanh hơn 10%.",         RunPerkKind.MoveSpeed,  1.10f),
            new RunPerk("perk.health.s",    "Máu tối đa +20%", "Tăng 20% máu tối đa.",             RunPerkKind.MaxHealth,  1.20f),
            new RunPerk("perk.coin.s",      "Vàng rơi +25%",   "Nhận thêm 25% Coin trong màn.",    RunPerkKind.CoinGain,   1.25f),
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
