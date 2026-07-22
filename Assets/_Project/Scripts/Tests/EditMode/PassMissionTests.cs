using System;
using System.Linq;
using NUnit.Framework;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>Battle Pass mission catalog, deterministic rotation, progress and claim safety.</summary>
    public class PassMissionTests
    {
        [SetUp]
        public void SetUp() => PlayerProfile.ClearMissionProgressForTests();

        [TearDown]
        public void TearDown() => PlayerProfile.ClearMissionProgressForTests();

        [Test]
        public void Catalog_HasTwentyMissionsWithUniqueIds()
        {
            Assert.AreEqual(20, PassMissions.All.Count);
            CollectionAssert.AllItemsAreUnique(PassMissions.All.Select(m => m.id).ToList());
        }

        [Test]
        public void EveryMission_HasSaneTargetAndReward()
        {
            foreach (var m in PassMissions.All)
            {
                Assert.Greater(m.target, 0, $"{m.id} has no target");
                Assert.Greater(m.passXp, 0, $"{m.id} grants no pass xp");
                Assert.IsFalse(string.IsNullOrEmpty(m.title), $"{m.id} has no title");
            }
        }

        [Test]
        public void Catalog_CoversBothScopes()
        {
            Assert.GreaterOrEqual(PassMissions.All.Count(m => m.scope == MissionScope.Daily), PassMissions.DailyCount);
            Assert.GreaterOrEqual(PassMissions.All.Count(m => m.scope == MissionScope.Weekly), PassMissions.WeeklyCount);
        }

        [Test]
        public void ActiveSet_IsDeterministicForTheSameDay()
        {
            var a = PassMissions.ActiveFor(new DateTime(2026, 7, 21, 3, 0, 0, DateTimeKind.Utc));
            var b = PassMissions.ActiveFor(new DateTime(2026, 7, 21, 22, 0, 0, DateTimeKind.Utc));

            CollectionAssert.AreEqual(a.Select(m => m.id).ToList(), b.Select(m => m.id).ToList(),
                "the same UTC day must always yield the same mission set");
        }

        [Test]
        public void ActiveSet_ChangesAcrossDays()
        {
            var day1 = PassMissions.ActiveFor(new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc))
                .Where(m => m.scope == MissionScope.Daily).Select(m => m.id).ToList();
            var day2 = PassMissions.ActiveFor(new DateTime(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc))
                .Where(m => m.scope == MissionScope.Daily).Select(m => m.id).ToList();

            CollectionAssert.AreNotEqual(day1, day2, "dailies must rotate day to day");
        }

        [Test]
        public void ActiveSet_HasNoDuplicates()
        {
            var set = PassMissions.ActiveFor(new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc));
            CollectionAssert.AllItemsAreUnique(set.Select(m => m.id).ToList());
            Assert.AreEqual(PassMissions.DailyCount + PassMissions.WeeklyCount, set.Count);
        }

        [Test]
        public void DayAndWeekKeys_AdvanceCorrectly()
        {
            var d1 = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(PassMissions.DayKey(d1) + 1, PassMissions.DayKey(d1.AddDays(1)));
            Assert.AreEqual(PassMissions.WeekKey(d1) + 1, PassMissions.WeekKey(d1.AddDays(7)));
            Assert.AreEqual(PassMissions.WeekKey(d1), PassMissions.WeekKey(d1.AddDays(1)),
                "one day must not roll the weekly window");
        }

        [Test]
        public void Progress_AccumulatesAndClampsAtTarget()
        {
            var m = PassMissions.Find("daily.kill50");

            PlayerProfile.AddMissionProgress(m.id, 20);
            Assert.AreEqual(20, PlayerProfile.GetMissionProgress(m.id));

            PlayerProfile.AddMissionProgress(m.id, 20);
            Assert.AreEqual(40, PlayerProfile.GetMissionProgress(m.id));

            PlayerProfile.AddMissionProgress(m.id, 999);
            Assert.AreEqual(m.target, PlayerProfile.GetMissionProgress(m.id),
                "progress must clamp at the target, not overshoot");
        }

        [Test]
        public void Progress_IgnoresUnknownMissionAndNonPositiveAmounts()
        {
            PlayerProfile.AddMissionProgress("nope.not.a.mission", 10);
            Assert.AreEqual(0, PlayerProfile.GetMissionProgress("nope.not.a.mission"));

            var m = PassMissions.Find("daily.kill50");
            PlayerProfile.AddMissionProgress(m.id, 0);
            PlayerProfile.AddMissionProgress(m.id, -5);
            Assert.AreEqual(0, PlayerProfile.GetMissionProgress(m.id));
        }

        [Test]
        public void Claim_RequiresCompletion()
        {
            var m = PassMissions.Find("daily.kill50");
            PlayerProfile.AddMissionProgress(m.id, 10);

            Assert.IsFalse(PlayerProfile.TryClaimMission(m.id), "an incomplete mission must not pay out");
            Assert.IsFalse(PlayerProfile.IsMissionClaimed(m.id));
        }

        [Test]
        public void Claim_PaysExactlyOnce()
        {
            var m = PassMissions.Find("daily.kill50");
            long coinBefore = PlayerProfile.Coin;
            int xpBefore = PlayerProfile.PassXp;

            PlayerProfile.AddMissionProgress(m.id, m.target);

            Assert.IsTrue(PlayerProfile.TryClaimMission(m.id));
            Assert.IsFalse(PlayerProfile.TryClaimMission(m.id));
            Assert.IsFalse(PlayerProfile.TryClaimMission(m.id));

            Assert.AreEqual(coinBefore + m.coinReward, PlayerProfile.Coin, "coin must be granted once");
            Assert.AreEqual(xpBefore + m.passXp, PlayerProfile.PassXp, "pass xp must be granted once");
        }

        [Test]
        public void DailyRollover_ClearsDailiesButKeepsWeeklies()
        {
            var daily = PassMissions.Find("daily.kill50");
            var weekly = PassMissions.Find("weekly.kill1000");
            var day = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc);

            PlayerProfile.RefreshMissionWindow(day);
            PlayerProfile.AddMissionProgress(daily.id, 30);
            PlayerProfile.AddMissionProgress(weekly.id, 300);

            PlayerProfile.RefreshMissionWindow(day.AddDays(1));

            Assert.AreEqual(0, PlayerProfile.GetMissionProgress(daily.id), "a new day must reset dailies");
            Assert.AreEqual(300, PlayerProfile.GetMissionProgress(weekly.id),
                "a new day must NOT reset weekly progress");
        }

        [Test]
        public void WeeklyRollover_ClearsWeeklies()
        {
            var weekly = PassMissions.Find("weekly.kill1000");
            var day = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc);

            PlayerProfile.RefreshMissionWindow(day);
            PlayerProfile.AddMissionProgress(weekly.id, 300);

            PlayerProfile.RefreshMissionWindow(day.AddDays(8));
            Assert.AreEqual(0, PlayerProfile.GetMissionProgress(weekly.id));
        }

        [Test]
        public void Rollover_MakesTheMissionClaimableAgain()
        {
            var daily = PassMissions.Find("daily.kill50");
            var day = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc);

            PlayerProfile.RefreshMissionWindow(day);
            PlayerProfile.AddMissionProgress(daily.id, daily.target);
            Assert.IsTrue(PlayerProfile.TryClaimMission(daily.id));

            PlayerProfile.RefreshMissionWindow(day.AddDays(1));
            Assert.IsFalse(PlayerProfile.IsMissionClaimed(daily.id),
                "yesterday's claim must not block today's copy of the mission");
        }

        [Test]
        public void ProgressAndClaims_SurviveReload()
        {
            var m = PassMissions.Find("daily.kill50");
            PlayerProfile.AddMissionProgress(m.id, m.target);
            PlayerProfile.TryClaimMission(m.id);

            // Drop the in-memory cache so the next read comes back off storage.
            PlayerProfile.ResetCacheForTests();

            Assert.AreEqual(m.target, PlayerProfile.GetMissionProgress(m.id), "progress must persist");
            Assert.IsTrue(PlayerProfile.IsMissionClaimed(m.id), "claims must persist");
        }
    }
}
