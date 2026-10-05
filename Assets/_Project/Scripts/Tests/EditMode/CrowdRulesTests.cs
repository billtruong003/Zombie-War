using NUnit.Framework;
using UnityEngine;
using ZombieWar.Threat;

namespace ZombieWar.Tests
{
    /// <summary>05/10 genre rules 2 and 3: the crowd is fodder, ranged enemies and pounces are a
    /// capped share that opens up with time.</summary>
    public class CrowdRulesTests
    {
        [Test]
        public void NoRangedBeforeMinuteTwo_ThenTwo_GrowingToFifteenPercent()
        {
            Assert.AreEqual(0, ThreatDirector.RangedCapAt(119f, 60));
            Assert.AreEqual(2, ThreatDirector.RangedCapAt(120f, 60));
            Assert.AreEqual(3, ThreatDirector.RangedCapAt(165f, 60));
            Assert.AreEqual(9, ThreatDirector.RangedCapAt(1200f, 60), "15% of 60");
        }

        [Test]
        public void NoElitesBeforeTwoThirty_ThenOneMoreEvery75s_AtMostSix()
        {
            Assert.AreEqual(0, ThreatDirector.EliteCapAt(149f));
            Assert.AreEqual(1, ThreatDirector.EliteCapAt(150f));
            Assert.AreEqual(2, ThreatDirector.EliteCapAt(225f));
            Assert.AreEqual(ThreatDirector.EliteMax, ThreatDirector.EliteCapAt(3600f));
        }

        [Test]
        public void FodderDominatesTheFirstMinutes()
        {
            Assert.AreEqual(3, ThreatDirector.FodderWeightAt(30f));
            Assert.AreEqual(2, ThreatDirector.FodderWeightAt(180f));
            Assert.AreEqual(1, ThreatDirector.FodderWeightAt(300f));
        }

        [Test]
        public void PounceSlotsOpenWithTheRun()
        {
            Assert.AreEqual(0, ZombiePouncer.PounceSlotsAt(30f));
            Assert.AreEqual(1, ZombiePouncer.PounceSlotsAt(90f));
            Assert.AreEqual(2, ZombiePouncer.PounceSlotsAt(300f));
        }

        [Test]
        public void EnemiesGrowWithTime_SlowThenCompounding()
        {
            Assert.AreEqual(1f, ThreatDirector.TimeHealthMultiplier(170f), 1e-4f);
            Assert.AreEqual(1.7f, ThreatDirector.TimeHealthMultiplier(600f), 1e-3f);
            Assert.AreEqual(1.7f * Mathf.Pow(1.12f, 10f), ThreatDirector.TimeHealthMultiplier(1200f), 1e-2f);
            Assert.AreEqual(1.35f, ThreatDirector.TimeDamageMultiplier(600f), 1e-3f, "half the linear part");
            Assert.AreEqual(1.35f * Mathf.Pow(1.12f, 10f), ThreatDirector.TimeDamageMultiplier(1200f), 1e-2f, "full compound rate after 10:00");
        }

        [Test]
        public void SpawnBurstsGrowAfterMinuteTen()
        {
            Assert.AreEqual(1, ThreatDirector.BurstAt(599f, 1));
            Assert.AreEqual(2, ThreatDirector.BurstAt(600f, 1));
            Assert.AreEqual(4, ThreatDirector.BurstAt(840f, 1));
            Assert.AreEqual(ThreatDirector.BurstMax, ThreatDirector.BurstAt(3600f, 1));
        }

        [Test]
        public void KillCoin_FullForFiveMinutes_ThenFlattens()
        {
            Assert.AreEqual(1f, RunState.KillCoinFactor(299f), 1e-4f);
            Assert.AreEqual(0.5f, RunState.KillCoinFactor(600f), 1e-4f);
            Assert.AreEqual(0.25f, RunState.KillCoinFactor(1200f), 1e-4f);
        }

        [Test]
        public void OnScreenEnemiesMoveAtTheirOwnSpeed()
        {
            Assert.AreEqual(1f, ZombieManager.PursuitMultiplierAt(12f, 14f, 30f, 1.8f), 1e-4f);
            Assert.AreEqual(1.8f, ZombieManager.PursuitMultiplierAt(40f, 14f, 30f, 1.8f), 1e-4f);
        }
    }
}
