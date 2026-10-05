using System;
using System.Collections.Generic;

namespace ZombieWar
{
    /// PlayerProfile, part: the Daily Ops chest and streak (backlog #19/#20, owner-approved 05/10).
    /// Finishing all four Daily Ops opens one chest a day: 40 gems, 1 gacha ticket and 15 shards of a
    /// gun the player owns; three days in a row doubles the shards, every seventh day adds a gun the
    /// player does not have yet (Rare or better).
    public static partial class PlayerProfile
    {
        public const int DailyChestGems = 40, DailyChestTickets = 1, DailyChestShards = 15;
        public const int StreakDoubleAt = 3, StreakGunEvery = 7;

        /// <summary>The daily chest opened (the radio celebrates a seventh day).</summary>
        public static event Action<DailyChestReward> DailyChestClaimed;

        public struct DailyChestReward
        {
            public int gems, tickets, shards, streak;
            public string shardGun, newGun;
        }

        /// <summary>Today's Daily Ops ids if they were dealt for <paramref name="day"/>, else null.</summary>
        public static IReadOnlyList<string> DailyOpsKept(int day) =>
            Data.dailyOpsDay == day && Data.dailyOpsIds.Count > 0 ? Data.dailyOpsIds : null;

        public static void KeepDailyOps(int day, List<string> ids)
        {
            Data.dailyOpsDay = day;
            Data.dailyOpsIds.Clear();
            Data.dailyOpsIds.AddRange(ids);
            MarkDirty();
        }

        /// <summary>Days in a row the chest was opened; 0 once a day was missed.</summary>
        public static int DailyStreakOn(int today) =>
            Data.dailyChestDay == today || Data.dailyChestDay == today - 1 ? Data.dailyStreak : 0;

        public static bool DailyChestOpenedOn(int today) => Data.dailyChestDay == today;

        /// <summary>All of today's Daily Ops claimed.</summary>
        public static bool DailyOpsAllClaimed(DateTime utcNow)
        {
            int n = 0;
            foreach (var m in PassMissions.ActiveFor(utcNow))
            {
                if (m.scope != MissionScope.Daily) continue;
                n++;
                if (!IsMissionClaimed(m.id)) return false;
            }
            return n > 0;
        }

        public static bool CanClaimDailyChest(DateTime utcNow) =>
            !DailyChestOpenedOn(PassMissions.DayKey(utcNow)) && DailyOpsAllClaimed(utcNow);

        /// <summary>
        /// Opens today's chest once. The day and streak are saved before anything is granted (an
        /// interruption costs a reward rather than repeating it). <paramref name="guns"/> is the gun
        /// catalog the seventh-day gun is drawn from; null skips that gun.
        /// </summary>
        public static bool TryClaimDailyChest(DateTime utcNow, IReadOnlyList<WeaponData> guns, out DailyChestReward reward)
        {
            reward = default;
            if (!CanClaimDailyChest(utcNow)) return false;
            int today = PassMissions.DayKey(utcNow);
            int streak = DailyStreakOn(today) + 1;
            Data.dailyChestDay = today;
            Data.dailyStreak = streak;
            SaveNow();

            var rng = new Random(today * 7919 + streak);
            reward.streak = streak;
            reward.gems = DailyChestGems;
            reward.tickets = DailyChestTickets;
            reward.shards = DailyChestShards * (streak >= StreakDoubleAt ? 2 : 1);
            var owned = Data.ownedWeaponIds;
            reward.shardGun = owned.Count > 0 ? owned[rng.Next(owned.Count)] : Data.weapon;

            Add(CurrencyKind.Gem, reward.gems);
            AddTickets(reward.tickets);
            AddWeaponShards(reward.shardGun, reward.shards);

            if (streak % StreakGunEvery == 0 && guns != null)
            {
                var pool = new List<WeaponData>();
                foreach (var g in guns)
                    if (g != null && g.tier >= WeaponTier.Rare && !IsWeaponOwned(g.WeaponId)) pool.Add(g);
                if (pool.Count > 0)
                {
                    var gun = pool[rng.Next(pool.Count)];
                    AddOwnedWeapon(gun.WeaponId);
                    MarkUnseen(gun.WeaponId);
                    reward.newGun = gun.WeaponId;
                }
            }
            Notify(Change.Missions);
            DailyChestClaimed?.Invoke(reward);
            return true;
        }
    }
}
