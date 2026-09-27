using NUnit.Framework;

namespace ZombieWar.Tests
{
    /// M10 revive price: 60% of the carried coins, doubling, with a floor.
    public class ReviveRulesTests
    {
        [Test]
        public void CoinCost_StartsAt60Percent_AndDoubles()
        {
            Assert.AreEqual(744, ReviveRules.CoinCost(1240, 0));
            Assert.AreEqual(1488, ReviveRules.CoinCost(1240, 1), "the mockup's revive 2 of 3");
            Assert.AreEqual(2976, ReviveRules.CoinCost(1240, 2));
        }

        [Test]
        public void TwoCoinRevives_CostMoreThanTheRunCarries()
        {
            foreach (long carried in new long[] { 200, 1240, 9000 })
                Assert.Greater(ReviveRules.CoinCost(carried, 0) + ReviveRules.CoinCost(carried, 1), carried);
        }

        [Test]
        public void CoinCost_HasAFloor()
        {
            Assert.AreEqual(ReviveRules.MinCoinCost, ReviveRules.CoinCost(0, 0));
        }
    }
}
