using NUnit.Framework;
using ZombieWar.Skills;
using ZombieWar.UI;

namespace ZombieWar.Tests
{
    /// <summary>Pause build card (mockup U3 05/10): the evolution line it shows.</summary>
    public class PauseBuildTests
    {
        SkillRuntime _run;

        [SetUp]
        public void SetUp()
        {
            _run = new SkillRuntime { UnlockLevel = int.MaxValue };
            AutonomousPower.ResetGlobalBudget();
        }

        void Max(string id) { while (_run.Take(id)) { } }

        [Test]
        public void NoPowerMeansNoEvolutionLine()
        {
            Assert.IsNull(PauseBuildView.EvolutionLine(_run).evo);
            _run.Take(SkillCatalogDefs.StatDamage);
            Assert.IsNull(PauseBuildView.EvolutionLine(_run).evo);
        }

        [Test]
        public void AnOwnedPowerShowsWhatItsEvolutionStillNeeds()
        {
            _run.Take(SkillCatalogDefs.AutoFrostNova);
            var line = PauseBuildView.EvolutionLine(_run);
            var evo = SkillCatalogDefs.EvolutionOf(SkillCatalogDefs.AutoFrostNova);
            Assert.AreEqual(evo, line.evo);
            Assert.AreEqual("NEXT EVOLUTION", line.tag);
            StringAssert.Contains("rank 1/", line.sub);
            StringAssert.Contains("needs", line.sub);
        }

        [Test]
        public void AMaxedPowerWithItsPartnerIsReady()
        {
            var evo = SkillCatalogDefs.EvolutionOf(SkillCatalogDefs.AutoFrostNova);
            Max(SkillCatalogDefs.AutoFrostNova);
            _run.Take(evo.partner);
            var line = PauseBuildView.EvolutionLine(_run);
            Assert.AreEqual(evo, line.evo);
            Assert.AreEqual("EVOLUTION READY", line.tag);
            Assert.AreEqual("Open a chest to evolve it.", line.sub);
        }

        [Test]
        public void CatalogOrderIsStable()
        {
            var a = SkillCatalogDefs.ById(SkillCatalogDefs.AutoOrbit);
            var b = SkillCatalogDefs.ById(SkillCatalogDefs.AutoFrostNova);
            Assert.AreEqual(-PauseBuildView.CatalogOrder(b, a), PauseBuildView.CatalogOrder(a, b));
            Assert.AreEqual(0, PauseBuildView.CatalogOrder(a, a));
        }
    }
}
