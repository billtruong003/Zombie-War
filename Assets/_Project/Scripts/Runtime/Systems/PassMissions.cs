using System;
using System.Collections.Generic;

namespace ZombieWar
{
    public enum MissionScope { Daily, Weekly }

    /// <summary>What a mission counts. Progress is pushed by typed gameplay events, never polled.</summary>
    public enum MissionMetric
    {
        KillAny,
        KillRunner,
        KillRanged,
        KillBurrower,
        KillElite,
        CollectCoin,
        ChooseCard,
        FinishRun,
        DefeatBoss,
        CompleteStation,
        /// <summary>Best single-run survival, in whole minutes. A state, not a counter.</summary>
        SurviveMinutes,
        /// <summary>Highest threat tier reached in one run. A state, not a counter.</summary>
        ReachThreatTier,
    }

    /// <summary>One authored mission. Pure data - no behaviour, so it is trivially testable.</summary>
    public class PassMission
    {
        public readonly string id;
        public readonly string title;
        public readonly MissionScope scope;
        public readonly MissionMetric metric;
        public readonly int target;
        public readonly int passXp;
        public readonly int coinReward;

        public PassMission(string id, string title, MissionScope scope, MissionMetric metric,
                           int target, int passXp, int coinReward)
        {
            this.id = id; this.title = title; this.scope = scope; this.metric = metric;
            this.target = target; this.passXp = passXp; this.coinReward = coinReward;
        }
    }

    /// <summary>
    /// The authored Battle Pass mission catalog and the daily/weekly rotation.
    ///
    /// Rotation is DETERMINISTIC from the UTC date rather than random: the same day always yields the
    /// same missions, so a player who reinstalls, or two devices on one account, never disagree about
    /// what today's set is - and a test can assert it without mocking a clock.
    /// </summary>
    public static class PassMissions
    {
        public const int DailyCount = 4;
        public const int WeeklyCount = 4;

        public static readonly IReadOnlyList<PassMission> All = new List<PassMission>
        {
            // ---- Daily pool ---------------------------------------------------------------
            new PassMission("daily.kill50",    "Kill 50 monsters",                  MissionScope.Daily,  MissionMetric.KillAny,          50, 100, 200),
            new PassMission("daily.kill150",   "Kill 150 monsters",                 MissionScope.Daily,  MissionMetric.KillAny,         150, 200, 400),
            new PassMission("daily.survive5",  "Survive 5 minutes in one run",      MissionScope.Daily,  MissionMetric.SurviveMinutes,    5, 150, 300),
            new PassMission("daily.coin250",   "Collect 250 Coins",                 MissionScope.Daily,  MissionMetric.CollectCoin,     250, 100, 200),
            new PassMission("daily.card3",     "Choose 3 level-up cards",           MissionScope.Daily,  MissionMetric.ChooseCard,        3, 100, 200),
            new PassMission("daily.runner20",  "Kill 20 sprinting monsters",        MissionScope.Daily,  MissionMetric.KillRunner,       20, 150, 250),
            new PassMission("daily.ranged10",  "Kill 10 ranged monsters",           MissionScope.Daily,  MissionMetric.KillRanged,       10, 150, 250),
            new PassMission("daily.burrow8",   "Kill 8 burrowing monsters",         MissionScope.Daily,  MissionMetric.KillBurrower,      8, 150, 250),
            new PassMission("daily.elite1",    "Defeat 1 elite or boss",            MissionScope.Daily,  MissionMetric.KillElite,         1, 200, 350),
            new PassMission("daily.station2",  "Activate 2 stations",               MissionScope.Daily,  MissionMetric.CompleteStation,   2, 150, 300),
            new PassMission("daily.run3",      "Play 3 runs",                       MissionScope.Daily,  MissionMetric.FinishRun,         3, 100, 200),

            // ---- Weekly pool --------------------------------------------------------------
            new PassMission("weekly.kill1000", "Kill 1,000 monsters",               MissionScope.Weekly, MissionMetric.KillAny,        1000, 600, 1500),
            new PassMission("weekly.run10",    "Play 10 runs",                      MissionScope.Weekly, MissionMetric.FinishRun,        10, 500, 1200),
            new PassMission("weekly.boss1",    "Defeat a Boss Beacon boss",         MissionScope.Weekly, MissionMetric.DefeatBoss,        1, 600, 1500),
            new PassMission("weekly.coin5000", "Collect 5,000 Coins",               MissionScope.Weekly, MissionMetric.CollectCoin,    5000, 600, 1500),
            new PassMission("weekly.survive10","Survive 10 minutes in one run",     MissionScope.Weekly, MissionMetric.SurviveMinutes,   10, 700, 1800),
            new PassMission("weekly.station15","Activate 15 stations",              MissionScope.Weekly, MissionMetric.CompleteStation,  15, 600, 1500),
            new PassMission("weekly.threat4",  "Reach threat tier 4 in one run",    MissionScope.Weekly, MissionMetric.ReachThreatTier,   4, 700, 1800),
        };

        public static PassMission Find(string id)
        {
            var op = DailyOps.Decode(id);   // Daily Ops ids describe themselves
            if (op != null) return op;
            for (int i = 0; i < All.Count; i++)
                if (All[i].id == id) return All[i];
            return null;
        }

        /// <summary>The game day (GameClock's reset hour), so missions roll over with every other daily.</summary>
        public static int DayKey(DateTime utcNow) => GameClock.DayIndex(utcNow);

        /// <summary>Weeks since epoch in UTC. The weekly reset key.</summary>
        public static int WeekKey(DateTime utcNow) => DayKey(utcNow) / 7;

        /// <summary>
        /// Today's active mission set: a deterministic rotation through each pool keyed off the
        /// date. Using a rotating offset rather than a hash keeps it fair - every mission comes up
        /// on a predictable cycle instead of some never appearing.
        /// </summary>
        public static List<PassMission> ActiveFor(DateTime utcNow)
        {
            var result = new List<PassMission>(DailyCount + WeeklyCount);
            // Daily Ops (05/10) replace the old daily rotation: gun missions for owned families.
            result.AddRange(DailyOps.For(DayKey(utcNow), DailyOps.SeedFor(PlayerProfile.PlayerId),
                                         OwnedFamilies(), PlayerProfile.AccountLevel));
            result.AddRange(Rotate(MissionScope.Weekly, WeekKey(utcNow), WeeklyCount));
            return result;
        }

        /// <summary>
        /// The order every screen lists missions in (Home card, Pass, run result): ready to claim,
        /// then in progress, then claimed; daily before weekly. Home and Pass used to sort
        /// differently, so the Home card showed weekly goals while Pass led with the dailies.
        /// </summary>
        public static List<PassMission> Listed(DateTime utcNow)
        {
            var list = ActiveFor(utcNow);
            list.Sort((a, b) =>
            {
                int c = Rank(a).CompareTo(Rank(b));
                return c != 0 ? c : a.scope.CompareTo(b.scope);
            });
            return list;
        }

        /// <summary>Gun families the player owns (catalog lookup); tests can replace it.</summary>
        public static Func<IReadOnlyList<WeaponClass>> OwnedFamiliesProvider;

        static IReadOnlyList<WeaponClass> OwnedFamilies()
        {
            if (OwnedFamiliesProvider != null) return OwnedFamiliesProvider();
            var list = new List<WeaponClass>();
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            if (all == null) return list;
            foreach (var id in PlayerProfile.OwnedWeaponIds)
            {
                var w = LoadoutState.Resolve(id, all);
                if (w != null && !list.Contains(w.weaponClass)) list.Add(w.weaponClass);
            }
            var equipped = LoadoutState.Resolve(PlayerProfile.EquippedWeaponId, all);
            if (equipped != null && !list.Contains(equipped.weaponClass)) list.Add(equipped.weaponClass);
            return list;
        }

        /// <summary>The family of the gun the player carries now, or null when unknown.</summary>
        public static WeaponClass? CarriedFamily()
        {
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            var w = all != null ? LoadoutState.Resolve(PlayerProfile.EquippedWeaponId, all) : null;
            return w != null ? w.weaponClass : (WeaponClass?)null;
        }

        /// <summary>Whether progress made with <paramref name="carried"/> counts for this mission.</summary>
        public static bool CountsWith(PassMission m, WeaponClass? carried)
        {
            var need = DailyOps.FamilyOf(m);
            return !need.HasValue || (carried.HasValue && carried.Value == need.Value);
        }

        static int Rank(PassMission m) =>
            PlayerProfile.IsMissionClaimed(m.id) ? 2 : PlayerProfile.IsMissionComplete(m) ? 0 : 1;

        /// <summary>"12/50": the progress count shown next to every mission bar.</summary>
        public static string ProgressText(PassMission m) =>
            $"{Math.Min(PlayerProfile.GetMissionProgress(m.id), m.target):N0}/{m.target:N0}";

        static IEnumerable<PassMission> Rotate(MissionScope scope, int key, int count)
        {
            var pool = new List<PassMission>();
            for (int i = 0; i < All.Count; i++)
                if (All[i].scope == scope) pool.Add(All[i]);

            if (pool.Count == 0) yield break;
            int take = Math.Min(count, pool.Count);
            // Non-negative modulo: key can be large but never negative for real dates.
            int offset = ((key % pool.Count) + pool.Count) % pool.Count;
            for (int i = 0; i < take; i++)
                yield return pool[(offset + i) % pool.Count];
        }
    }
}
