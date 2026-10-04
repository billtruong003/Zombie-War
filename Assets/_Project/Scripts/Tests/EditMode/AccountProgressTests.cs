using NUnit.Framework;

namespace ZombieWar.Tests
{
    public class AccountProgressTests
    {
        [Test]
        public void NewAccount_IsLevelOne()
        {
            Assert.AreEqual(1, AccountProgress.LevelFor(0));
            Assert.AreEqual(0f, AccountProgress.Progress01(0), 1e-4f);
        }

        [Test]
        public void Curve_CostsGrowByTheStep()
        {
            Assert.AreEqual(40, AccountProgress.CostOf(1));
            Assert.AreEqual(70, AccountProgress.CostOf(2));
            Assert.AreEqual(0, AccountProgress.TotalFor(1));
            Assert.AreEqual(40, AccountProgress.TotalFor(2));
            Assert.AreEqual(110, AccountProgress.TotalFor(3));
            Assert.AreEqual(2, AccountProgress.LevelFor(40));
            Assert.AreEqual(1, AccountProgress.LevelFor(39));
            Assert.AreEqual(3, AccountProgress.LevelFor(110));
        }

        [Test]
        public void LevelFor_IsMonotonic()
        {
            int last = 1;
            for (int xp = 0; xp < 20000; xp += 37)
            {
                int l = AccountProgress.LevelFor(xp);
                Assert.GreaterOrEqual(l, last);
                last = l;
            }
        }

        [Test]
        public void RunXp_PaysForTimeAndKills()
        {
            Assert.AreEqual(21, AccountProgress.XpForRun(30f, 30));
            Assert.AreEqual(0, AccountProgress.XpForRun(-5f, -3));
            // A first 30 s run is not enough for LV2, two are.
            Assert.AreEqual(1, AccountProgress.LevelFor(AccountProgress.XpForRun(30f, 30)));
            Assert.AreEqual(2, AccountProgress.LevelFor(2 * AccountProgress.XpForRun(30f, 30)));
        }

        [Test]
        public void Gates_OpenAtTheMockupLevels()
        {
            Assert.IsFalse(AccountProgress.IsUnlocked(AccountProgress.Feature.Pass, 1));
            Assert.IsTrue(AccountProgress.IsUnlocked(AccountProgress.Feature.Pass, 2));
            Assert.IsFalse(AccountProgress.IsUnlocked(AccountProgress.Feature.Gacha, 2));
            Assert.IsTrue(AccountProgress.IsUnlocked(AccountProgress.Feature.Gacha, 3));
            Assert.IsFalse(AccountProgress.IsUnlocked(AccountProgress.Feature.GunStars, 4));
            Assert.IsTrue(AccountProgress.IsUnlocked(AccountProgress.Feature.GunStars, 5));
        }

        [Test]
        public void LockedMessage_NamesTheRealGate()
        {
            var gacha = AccountProgress.Feature.Gacha;
            Assert.AreEqual($"Unlocks at account level {AccountProgress.RequiredLevel(gacha)}",
                AccountProgress.LockedMessage(gacha, 1, firstRunPending: true), "a level gate wins over the first-run gate");
            Assert.AreEqual("Play your first run to unlock", AccountProgress.LockedMessage(null, 1, true));
            Assert.IsNull(AccountProgress.LockedMessage(null, 1, false));
            Assert.IsNull(AccountProgress.LockedMessage(gacha, AccountProgress.RequiredLevel(gacha), false));
        }
    }
}
