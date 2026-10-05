using System;
using System.Collections.Generic;

namespace ZombieWar
{
    /// <summary>
    /// Gun mastery (backlog #21, owner-approved 05/10). Each gun has 10 levels, earned only while it is
    /// carried: one XP per kill and twenty per minute survived, pistols half again faster (the cheap
    /// way to the crit bonus is the reason to go back to the pistol), double on a day whose Daily Ops
    /// ask for that family. Level 5 and level 10 of a FAMILY pay an account-wide bonus that every gun
    /// gets; the other levels pay shards, coins, gems or tickets. Level 10 unlocks the gun's evolution.
    /// </summary>
    public static class GunMastery
    {
        public const int MaxLevel = 10;
        public const int BonusLevel1 = 5, BonusLevel2 = 10;
        public const int XpPerMinute = 20;
        public const float SidearmSpeed = 1.5f, DailyOpsBoost = 2f;

        /// <summary>Total XP needed to reach level 1..10.</summary>
        static readonly int[] Thresholds = { 100, 250, 450, 700, 1000, 1400, 1900, 2500, 3200, 4000 };

        public static int XpToReach(int level) => level <= 0 ? 0 : Thresholds[Math.Min(level, MaxLevel) - 1];

        public static int LevelFor(int xp)
        {
            int l = 0;
            while (l < MaxLevel && xp >= Thresholds[l]) l++;
            return l;
        }

        /// <summary>0..1 progress inside the current level (1 at max).</summary>
        public static float Progress(int xp)
        {
            int l = LevelFor(xp);
            if (l >= MaxLevel) return 1f;
            int from = XpToReach(l), to = XpToReach(l + 1);
            return (xp - from) / (float)Math.Max(1, to - from);
        }

        /// <summary>The mastery XP one run pays its gun.</summary>
        public static int XpForRun(int kills, float seconds, WeaponClass family, bool dailyOpsFamily)
        {
            float xp = Math.Max(0, kills) + XpPerMinute * Math.Max(0f, seconds) / 60f;
            if (family == WeaponClass.Sidearm) xp *= SidearmSpeed;
            if (dailyOpsFamily) xp *= DailyOpsBoost;
            return (int)Math.Round(xp);
        }

        // ------------------------------------------------------------------ level rewards
        public enum RewardKind { Shards, Coin, Gem, Ticket }

        public readonly struct LevelReward
        {
            public readonly RewardKind kind; public readonly int amount; public readonly bool familyBonus, unlocksEvolution;
            public LevelReward(RewardKind k, int a, bool bonus = false, bool evo = false) { kind = k; amount = a; familyBonus = bonus; unlocksEvolution = evo; }
            public string Label => kind switch
            {
                RewardKind.Shards => $"+{amount} shards",
                RewardKind.Coin => $"+{amount:N0} coins",
                RewardKind.Gem => $"+{amount} gems",
                _ => amount == 1 ? "+1 ticket" : $"+{amount} tickets",
            };
        }

        static readonly LevelReward[] Rewards =
        {
            new(RewardKind.Shards, 10), new(RewardKind.Coin, 500), new(RewardKind.Shards, 15), new(RewardKind.Coin, 1000),
            new(RewardKind.Gem, 30, bonus: true),
            new(RewardKind.Shards, 20), new(RewardKind.Ticket, 1), new(RewardKind.Gem, 40), new(RewardKind.Shards, 25),
            new(RewardKind.Ticket, 2, bonus: true, evo: true),
        };

        public static LevelReward RewardFor(int level) => Rewards[Math.Min(Math.Max(level, 1), MaxLevel) - 1];

        // ------------------------------------------------------------------ family bonuses
        public enum Stat { Crit, MoveSpeed, Damage, MaxHealth, FireRate, PickupRange, Area, Cooldown }

        /// <summary>The stat a family's mastery raises for every gun.</summary>
        public static Stat StatOf(WeaponClass f) => f switch
        {
            WeaponClass.Sidearm => Stat.Crit,
            WeaponClass.SMG => Stat.MoveSpeed,
            WeaponClass.AssaultRifle => Stat.Damage,
            WeaponClass.Shotgun => Stat.MaxHealth,
            WeaponClass.LMG => Stat.FireRate,
            WeaponClass.Marksman => Stat.PickupRange,
            WeaponClass.Railgun => Stat.Damage,
            WeaponClass.Flamethrower => Stat.Area,
            WeaponClass.Tesla => Stat.Cooldown,
            WeaponClass.Laser => Stat.FireRate,
            _ => Stat.Area,
        };

        /// <summary>Bonus per tier (level 5, then level 10 again): pickup range moves in bigger steps.</summary>
        public static float StepOf(Stat s) => s == Stat.PickupRange ? 0.10f : 0.05f;

        public static int TiersAt(int level) => (level >= BonusLevel1 ? 1 : 0) + (level >= BonusLevel2 ? 1 : 0);

        /// <summary>"+5% crit chance for every gun" — the line the mastery screen and toasts use.</summary>
        public static string BonusText(WeaponClass f, int tiers)
        {
            var s = StatOf(f);
            float pct = StepOf(s) * Math.Max(1, tiers) * 100f;
            string what = s switch
            {
                Stat.Crit => "crit chance", Stat.MoveSpeed => "move speed", Stat.Damage => "damage",
                Stat.MaxHealth => "max health", Stat.FireRate => "fire rate", Stat.PickupRange => "pickup range",
                Stat.Area => "power area", _ => "faster power cooldowns",
            };
            return $"+{pct:0}% {what} for every gun";
        }

        /// <summary>Account-wide bonuses, from the best-mastered gun of each family.</summary>
        public struct Bonuses
        {
            public float crit, moveSpeed, damage, maxHealth, fireRate, pickupRange, area, cooldown;

            public void Add(Stat s, float v)
            {
                switch (s)
                {
                    case Stat.Crit: crit += v; break;
                    case Stat.MoveSpeed: moveSpeed += v; break;
                    case Stat.Damage: damage += v; break;
                    case Stat.MaxHealth: maxHealth += v; break;
                    case Stat.FireRate: fireRate += v; break;
                    case Stat.PickupRange: pickupRange += v; break;
                    case Stat.Area: area += v; break;
                    default: cooldown += v; break;
                }
            }
        }

        /// <summary>Bonuses for families mastered to the given levels (family → best level).</summary>
        public static Bonuses BonusesFor(IReadOnlyDictionary<WeaponClass, int> bestLevelByFamily)
        {
            var b = new Bonuses();
            if (bestLevelByFamily == null) return b;
            foreach (var kv in bestLevelByFamily)
            {
                int tiers = TiersAt(kv.Value);
                if (tiers > 0) { var s = StatOf(kv.Key); b.Add(s, StepOf(s) * tiers); }
            }
            return b;
        }

        /// <summary>The player's bonuses now (catalog lookup for each gun's family).</summary>
        public static Bonuses Current()
        {
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            var best = new Dictionary<WeaponClass, int>();
            if (all == null) return default;
            foreach (var e in PlayerProfile.GunStats)
            {
                var w = LoadoutState.Resolve(e.weaponId, all);
                if (w == null) continue;
                int l = LevelFor(e.masteryXp);
                if (!best.TryGetValue(w.weaponClass, out int had) || l > had) best[w.weaponClass] = l;
            }
            return BonusesFor(best);
        }
    }
}
