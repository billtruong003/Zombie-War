using NUnit.Framework;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>Phase A10: the power damage budget.</summary>
    public class PhaseA10Tests
    {
        [Test]
        public void AnythingOutsideTheBudgetKeepsItsDamage()
        {
            Assert.AreEqual(1f, PowerBudget.Of(null, false));
            Assert.AreEqual(1f, PowerBudget.Of("gun.whatever", true));
            Assert.AreEqual(1f, PowerBudget.Of(SkillCatalogDefs.LmgShockwave, false));
        }

        [Test]
        public void EveryBudgetedIdIsARealPower_AndEveryEvolutionFactorHasAnEvolution()
        {
            foreach (var id in PowerBudget.BudgetedPowers)
            {
                var d = SkillCatalogDefs.ById(id);
                Assert.IsNotNull(d, id);
                Assert.AreEqual(SkillLayer.Autonomous, d.layer, id);
            }
            foreach (var id in PowerBudget.BudgetedEvolutions)
                Assert.IsNotNull(SkillCatalogDefs.EvolutionOf(id), id + " has no evolution");
        }

        [Test]
        public void FactorsStayInASaneRange()
        {
            foreach (var d in SkillCatalogDefs.All)
            {
                if (d.layer != SkillLayer.Autonomous) continue;
                float p = PowerBudget.Of(d.id, false), e = PowerBudget.Of(d.id, true);
                Assert.That(p, Is.InRange(0.3f, 8f), d.id);
                Assert.That(e, Is.InRange(0.15f, 8f), d.id + " evolved");
            }
        }

        [Test]
        public void FxPoolCapsLiveCopiesOfOneEffect_AndFreesThemWhenTheyEnd()
        {
            var admit = typeof(FxPool).GetMethod("Admit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            bool Try(int id, float ttl, float now) => (bool)admit.Invoke(null, new object[] { id, ttl, now });
            FxPool.ResetBudget();
            try
            {
                for (int i = 0; i < FxPool.MaxLivePerPrefab; i++) Assert.IsTrue(Try(1, 1f, 10f), "copy " + i);
                Assert.IsFalse(Try(1, 1f, 10.5f), "the cap holds while they live");
                Assert.IsTrue(Try(2, 1f, 10.5f), "another effect has its own budget");
                Assert.IsTrue(Try(1, 1f, 11.01f), "a slot frees when the first copy ends");
                FxPool.ResetBudget();
                Assert.IsTrue(Try(1, 1f, 0f), "a reset (new run) clears the book");
            }
            finally { FxPool.ResetBudget(); }
        }

        [Test]
        public void AWaitingEvolutionMakesTheNextEliteChestCertain()
        {
            Assert.IsFalse(PickupManager.ChestPity(-1f, 100f, 45f), "nothing ready");
            Assert.IsFalse(PickupManager.ChestPity(80f, 100f, 45f), "ready, but not long enough");
            Assert.IsTrue(PickupManager.ChestPity(50f, 100f, 45f), "ready for 50 s");

            var run = new SkillRuntime { UnlockLevel = int.MaxValue };
            Assert.IsFalse(run.AnyEvolutionReady);
            while (run.Take(SkillCatalogDefs.AutoOrbit)) { }
            Assert.IsFalse(run.AnyEvolutionReady, "the partner is missing");
            run.Take(SkillCatalogDefs.StatMoveSpeed);
            Assert.IsTrue(run.AnyEvolutionReady, "Orbit maxed + Move Speed = Buzzsaw ready");
            run.OpenChest(1);
            Assert.IsFalse(run.AnyEvolutionReady, "taken");
        }

        [Test]
        public void TheBudgetReachesPowerDamage()
        {
            var run = new SkillRuntime();
            run.Take(SkillCatalogDefs.AutoDrone);
            float drone = run.PowerDamage(10f, SkillCatalogDefs.AutoDrone);
            run.Take(SkillCatalogDefs.AutoAirstrike);
            float strike = run.PowerDamage(10f, SkillCatalogDefs.AutoAirstrike);
            Assert.AreEqual(PowerBudget.Of(SkillCatalogDefs.AutoDrone, false) / PowerBudget.Of(SkillCatalogDefs.AutoAirstrike, false),
                            drone / strike, 1e-4f);
        }
    }
}
