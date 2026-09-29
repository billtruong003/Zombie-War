using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// Phase A1 (owner 2026-09-29): five ranks, 6 skill + 4 stat slots, overflow cards, and cards
    /// unlocked by account level.
    public class PhaseA1Tests
    {
        static readonly string[] SixSkills =
        {
            SkillCatalogDefs.AutoChainLightning, SkillCatalogDefs.AutoOrbit, SkillCatalogDefs.AutoDrone,
            SkillCatalogDefs.AutoFrostNova, SkillCatalogDefs.AutoBoomerang, SkillCatalogDefs.UniExecution,
        };

        static readonly string[] FourStats =
        {
            SkillCatalogDefs.StatDamage, SkillCatalogDefs.StatFireRate, SkillCatalogDefs.StatMoveSpeed, SkillCatalogDefs.StatMaxHealth,
        };

        static SkillRuntime FullBuild(bool maxed)
        {
            var run = new SkillRuntime();
            foreach (var id in SixSkills.Concat(FourStats))
            {
                run.Take(id);
                if (maxed) while (run.Take(id)) { }
            }
            return run;
        }

        [Test]
        public void SkillSlots_HoldSix_AndStatSlots_HoldFour()
        {
            var run = FullBuild(maxed: false);
            Assert.AreEqual(6, run.SlotsUsed(SkillSlot.Skill));
            Assert.AreEqual(4, run.SlotsUsed(SkillSlot.Stat));
            Assert.IsFalse(run.Take(SkillCatalogDefs.AutoAirstrike), "a 7th skill must not fit");
            Assert.IsFalse(run.Take(SkillCatalogDefs.StatCoinGain), "a 5th stat must not fit");
            Assert.IsTrue(run.Take(SkillCatalogDefs.AutoOrbit), "an owned card still ranks up");
            Assert.IsTrue(run.Take(SkillCatalogDefs.StatDamage));
        }

        [Test]
        public void StatsNeverTakeASkillSlot()
        {
            var run = new SkillRuntime();
            foreach (var id in FourStats) run.Take(id);
            Assert.AreEqual(0, run.SlotsUsed(SkillSlot.Skill));
            foreach (var id in SixSkills) Assert.IsTrue(run.Take(id), id);
        }

        [Test]
        public void AnEvolutionTakesNoExtraSlot()
        {
            var run = FullBuild(maxed: true);
            Assert.IsTrue(run.Take(SkillCatalogDefs.EvoThunderstorm), "chain maxed + fire rate owned, slots full");
            Assert.AreEqual(6, run.SlotsUsed(SkillSlot.Skill));
        }

        [Test]
        public void WithSlotsFull_TheOfferNeverShowsANewCard()
        {
            var run = FullBuild(maxed: false);
            for (int seed = 0; seed < 60; seed++)
                foreach (var d in SkillOfferBuilder.Build(run, WeaponClass.Sidearm, seed, 12))
                    Assert.IsTrue(d.IsOverflow || run.Has(d.id) || d.IsEvolution, $"seed {seed}: {d.id} is new with no room");
        }

        [Test]
        public void AFullMaxedBuild_StillGetsThreeChoices_AllOverflow()
        {
            var run = FullBuild(maxed: true);
            // Take every evolution the build allows so nothing but overflow is left.
            foreach (var d in SkillCatalogDefs.All.Where(x => x.IsEvolution)) run.Take(d.id);
            for (int seed = 0; seed < 40; seed++)
            {
                var offer = SkillOfferBuilder.Build(run, WeaponClass.Sidearm, seed, 40);
                Assert.AreEqual(3, offer.Count, $"seed {seed}");
                Assert.IsTrue(offer.All(d => d.IsOverflow), $"seed {seed}");
                Assert.AreEqual(3, offer.Distinct().Count(), "three different overflow cards");
            }
        }

        [Test]
        public void Offers_AreAlwaysThree_WhileTheBuildCanGrow()
        {
            for (int seed = 0; seed < 60; seed++)
                for (int level = 1; level <= 12; level++)
                    Assert.AreEqual(3, SkillOfferBuilder.Build(new SkillRuntime(), WeaponClass.SMG, seed, level).Count);
        }

        [Test]
        public void OverflowCards_AreQueuedForTheDriver_AndMightStacks()
        {
            var run = new SkillRuntime();
            float baseMult = run.DamageMultiplier;
            run.Take(SkillCatalogDefs.OverHeal);
            run.Take(SkillCatalogDefs.OverCoin);
            run.Take(SkillCatalogDefs.OverMagnet);
            run.Take(SkillCatalogDefs.OverMight);
            run.Take(SkillCatalogDefs.OverMight);

            Assert.AreEqual(0.25f, run.ConsumeHealFraction(), 1e-4f);
            Assert.AreEqual(0f, run.ConsumeHealFraction(), 1e-4f, "consumed once");
            Assert.AreEqual(60, run.ConsumeCoin());
            Assert.IsTrue(run.ConsumeMagnet());
            Assert.IsFalse(run.ConsumeMagnet());
            Assert.AreEqual(2, run.MightStacks);
            Assert.AreEqual(baseMult * (1f + 2 * SkillRuntime.MightPerStack), run.DamageMultiplier, 1e-4f);

            run.Reset();
            Assert.AreEqual(0, run.MightStacks, "overflow is per run");
        }

        [Test]
        public void AtAccountLevel1_OnlyStarterCardsAreOffered()
        {
            var run = new SkillRuntime { UnlockLevel = 1 };
            for (int seed = 0; seed < 80; seed++)
                for (int level = 1; level <= 8; level++)
                    foreach (var d in SkillOfferBuilder.Build(run, WeaponClass.AssaultRifle, seed, level))
                        Assert.IsTrue(d.IsOverflow || d.layer == SkillLayer.Signature || d.unlockLevel <= 1,
                            $"{d.id} (unlocks at {d.unlockLevel}) offered at account level 1");
        }

        [Test]
        public void ACardAppearsFromItsUnlockLevel()
        {
            var ordnance = SkillCatalogDefs.ById(SkillCatalogDefs.AutoOrdnance);
            Assert.IsFalse(SkillCatalogDefs.IsUnlocked(ordnance, ordnance.unlockLevel - 1));
            Assert.IsTrue(SkillCatalogDefs.IsUnlocked(ordnance, ordnance.unlockLevel));

            var carpet = SkillCatalogDefs.ById(SkillCatalogDefs.EvoCarpetBomb);
            Assert.IsFalse(SkillCatalogDefs.IsUnlocked(carpet, ordnance.unlockLevel - 1), "an evolution waits for its power");
            Assert.IsTrue(SkillCatalogDefs.IsUnlocked(carpet, ordnance.unlockLevel));

            var list = new List<SkillDef>();
            SkillCatalogDefs.UnlockedAt(ordnance.unlockLevel, list);
            CollectionAssert.Contains(list, ordnance, "the unlock popup lists it at its level");
        }

        [Test]
        public void TheStarterSetFillsBothSlotGroups()
        {
            int skills = SkillCatalogDefs.All.Count(d => d.Slot == SkillSlot.Skill && d.layer != SkillLayer.Signature && d.unlockLevel <= 1);
            int stats = SkillCatalogDefs.All.Count(d => d.Slot == SkillSlot.Stat && d.unlockLevel <= 1);
            Assert.GreaterOrEqual(skills + 2, SkillCatalogDefs.MaxSkillSlots, "starter skills + the gun's 2 signatures fill 6 slots");
            Assert.GreaterOrEqual(stats, SkillCatalogDefs.MaxStatSlots);
        }

        [Test]
        public void RankFive_MatchesTheOldRankThree()
        {
            var run = new SkillRuntime();
            while (run.Take(SkillCatalogDefs.AutoOrbit)) { }
            while (run.Take(SkillCatalogDefs.AutoChainLightning)) { }
            while (run.Take(SkillCatalogDefs.AutoDrone)) { }
            while (run.Take(SkillCatalogDefs.AutoFrostNova)) { }
            Assert.AreEqual(4, run.OrbitBladeCount);
            Assert.AreEqual(5, run.ChainTargets);
            Assert.AreEqual(4f, run.DroneShotsPerSecond, 1e-4f);
            Assert.AreEqual(0.55f, run.FrostSlow, 1e-4f);
            Assert.AreEqual(4f, SkillRuntime.CooldownAt(SkillCatalogDefs.AutoChainLightning, 5, false), 1e-4f);
            Assert.AreEqual(1.6f, run.PowerDamage(1f, SkillCatalogDefs.AutoOrbit) / run.DamageMultiplier
                                   / Threat.ThreatDirector.EnemyStatMultiplier, 1e-4f);
        }
    }
}
