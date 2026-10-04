using System;
using NUnit.Framework;

namespace ZombieWar.Tests
{
    /// G6 (04/10): one game day for every daily system, and a clock that never runs backwards.
    public class GameClockTests
    {
        private TestSaveStore _save;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _save = new TestSaveStore();
            PlayerProfile.StorageOverride = _save;
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
            _now = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);
            GameClock.SourceOverride = () => _now;
        }

        [TearDown]
        public void TearDown()
        {
            GameClock.SourceOverride = null;
            PlayerProfile.StorageOverride = null;
            PlayerProfile.ResetCacheForTests();
        }

        [Test]
        public void DayChangesAtTheResetHour_NotAtMidnight()
        {
            var before = new DateTime(2026, 10, 4, GameClock.ResetHourUtc - 1, 59, 0, DateTimeKind.Utc);
            var after = before.AddMinutes(2);
            Assert.AreEqual(GameClock.DayIndex(before) + 1, GameClock.DayIndex(after));
            Assert.AreEqual(GameClock.DayIndex(after), GameClock.DayIndex(after.AddHours(23)));
        }

        [Test]
        public void SettingTheClockBack_DoesNotReopenADay()
        {
            int today = GameClock.Today;
            _now = _now.AddDays(-3);
            Assert.AreEqual(today, GameClock.Today, "the clock holds at the latest time seen");
        }

        [Test]
        public void ClockBack_CannotReclaimTheWelcomeReward()
        {
            Assert.IsTrue(DailyRewards.ClaimWelcome(GameClock.Today, out _));
            _now = _now.AddDays(-1);
            Assert.IsFalse(DailyRewards.CanClaimWelcome(GameClock.Today));
        }

        [Test]
        public void MissionDayKey_RollsWithTheGameDay()
        {
            Assert.AreEqual(GameClock.DayIndex(_now), PassMissions.DayKey(_now));
        }

        [Test]
        public void PremiumBelongsToItsSeason()
        {
            PassRewards.EnsureSeason(GameClock.Today);
            PassRewards.UnlockPremium();
            Assert.IsTrue(PassRewards.IsPremium);
            PassRewards.EnsureSeason(GameClock.Today + PassRewards.SeasonDays);
            Assert.IsFalse(PassRewards.IsPremium, "a new season starts without premium");
        }

        [Test]
        public void HomeBadge_DoesNotWriteTheSave()
        {
            _ = PlayerProfile.Coin;
            int later = GameClock.Today + 40;                     // a stamp cycle that has run out
            int writes = _save.setCount;
            DailyRewards.ClaimableCount(later);
            Assert.AreEqual(writes, _save.setCount);
        }
    }
}
