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
        ClearWave,
        FinishStage,
        CollectCoin,
        ChoosePerk,
        SwitchWeapon,
        FinishRun,
        DefeatStageBoss,
        ClearAllStages,
        FlawlessStage,
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
            new PassMission("daily.kill50",      "Tiêu diệt 50 quái",             MissionScope.Daily,  MissionMetric.KillAny,        50,  100, 200),
            new PassMission("daily.kill150",     "Tiêu diệt 150 quái",            MissionScope.Daily,  MissionMetric.KillAny,       150,  200, 400),
            new PassMission("daily.wave5",       "Vượt 5 đợt",                    MissionScope.Daily,  MissionMetric.ClearWave,       5,  100, 200),
            new PassMission("daily.stage1",      "Hoàn thành 1 màn chiến dịch",   MissionScope.Daily,  MissionMetric.FinishStage,     1,  150, 300),
            new PassMission("daily.coin250",     "Nhặt 250 Coin trong màn",       MissionScope.Daily,  MissionMetric.CollectCoin,   250,  100, 200),
            new PassMission("daily.perk3",       "Chọn 3 perk tạm thời",          MissionScope.Daily,  MissionMetric.ChoosePerk,      3,  100, 200),
            new PassMission("daily.runner20",    "Diệt 20 quái lao nhanh",        MissionScope.Daily,  MissionMetric.KillRunner,     20,  150, 250),
            new PassMission("daily.ranged10",    "Diệt 10 quái bắn xa",           MissionScope.Daily,  MissionMetric.KillRanged,     10,  150, 250),
            new PassMission("daily.burrow8",     "Diệt 8 quái đào đất",           MissionScope.Daily,  MissionMetric.KillBurrower,    8,  150, 250),
            new PassMission("daily.elite1",      "Hạ 1 quái tinh nhuệ hoặc boss", MissionScope.Daily,  MissionMetric.KillElite,       1,  200, 350),
            new PassMission("daily.recommended", "Qua màn với vũ khí gợi ý",      MissionScope.Daily,  MissionMetric.FinishStage,     1,  150, 300),
            new PassMission("daily.switch10",    "Đổi vũ khí 10 lần khi chiến",   MissionScope.Daily,  MissionMetric.SwitchWeapon,   10,  100, 200),

            // ---- Weekly / campaign pool ----------------------------------------------------
            new PassMission("weekly.kill1000",   "Tiêu diệt 1.000 quái",          MissionScope.Weekly, MissionMetric.KillAny,      1000,  600, 1500),
            new PassMission("weekly.wave25",     "Vượt 25 đợt",                   MissionScope.Weekly, MissionMetric.ClearWave,      25,  500, 1200),
            new PassMission("weekly.run10",      "Chơi xong 10 lượt chiến dịch",  MissionScope.Weekly, MissionMetric.FinishRun,      10,  500, 1200),
            new PassMission("weekly.hugo",       "Hạ boss cuối 1 lần",            MissionScope.Weekly, MissionMetric.DefeatStageBoss, 1,  600, 1500),
            new PassMission("weekly.allstages",  "Qua cả 5 màn ít nhất 1 lần",    MissionScope.Weekly, MissionMetric.ClearAllStages,  5,  800, 2000),
            new PassMission("weekly.coin5000",   "Kiếm 5.000 Coin từ các lượt",   MissionScope.Weekly, MissionMetric.CollectCoin,  5000,  600, 1500),
            new PassMission("weekly.eachboss",   "Hạ boss của từng màn",          MissionScope.Weekly, MissionMetric.DefeatStageBoss, 4,  700, 1800),
            new PassMission("weekly.flawless",   "Qua 1 màn không gục",           MissionScope.Weekly, MissionMetric.FlawlessStage,   1,  700, 1800),
        };

        public static PassMission Find(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].id == id) return All[i];
            return null;
        }

        /// <summary>Days since epoch in UTC. The daily reset key.</summary>
        public static int DayKey(DateTime utcNow) => (int)(utcNow.Date - new DateTime(1970, 1, 1)).TotalDays;

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
            result.AddRange(Rotate(MissionScope.Daily, DayKey(utcNow), DailyCount));
            result.AddRange(Rotate(MissionScope.Weekly, WeekKey(utcNow), WeeklyCount));
            return result;
        }

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
