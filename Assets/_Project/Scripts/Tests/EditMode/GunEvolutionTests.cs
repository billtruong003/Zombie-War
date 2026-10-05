using NUnit.Framework;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>Gun evolution (backlog #23).</summary>
    public class GunEvolutionTests
    {
        [Test]
        public void EvolvingNeedsStarsMasteryAndLevel8()
        {
            Assert.AreEqual(GunEvolution.Block.NotOwned, GunEvolution.Check("g", 3, 10, 8, false, false));
            Assert.AreEqual(GunEvolution.Block.AccountLevel, GunEvolution.Check("g", 3, 10, 7, true, false));
            Assert.AreEqual(GunEvolution.Block.Stars, GunEvolution.Check("g", 2, 10, 8, true, false));
            Assert.AreEqual(GunEvolution.Block.Mastery, GunEvolution.Check("g", 3, 9, 8, true, false));
            Assert.AreEqual(GunEvolution.Block.AlreadyEvolved, GunEvolution.Check("g", 3, 10, 8, true, true));
            Assert.AreEqual(GunEvolution.Block.None, GunEvolution.Check("g", 3, 10, 8, true, false));
        }

        [Test]
        public void EveryFamilyHasATraitCard()
        {
            foreach (WeaponClass f in System.Enum.GetValues(typeof(WeaponClass)))
            {
                var (card, rank) = GunEvolution.TraitOf(f);
                var def = SkillCatalogDefs.ById(card);
                Assert.IsNotNull(def, $"{f}: {card}");
                Assert.That(rank, Is.InRange(1, def.maxRank), f.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(GunEvolution.TraitText(f)));
            }
        }

        [Test]
        public void TheTrait_CountsAsRanks_WithoutTakingASlot()
        {
            var run = new SkillRuntime { UnlockLevel = int.MaxValue };
            var (card, rank) = GunEvolution.TraitOf(WeaponClass.Marksman);
            int slots = run.SlotsUsed(SkillSlot.Skill);
            run.SetInnate(card, rank);
            Assert.AreEqual(rank, run.RankOf(card));
            Assert.IsTrue(run.Has(card));
            Assert.AreEqual(slots, run.SlotsUsed(SkillSlot.Skill), "a built-in card takes no skill slot");
            run.Reset();
            Assert.AreEqual(0, run.RankOf(card), "the next run starts clean until the gun is equipped");
        }
    }
}
