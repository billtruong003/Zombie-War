using System.Linq;
using NUnit.Framework;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>Phase A7: evolutions come from chests only; thirteen new evolutions.</summary>
    public class PhaseA7Tests
    {
        void Max(SkillRuntime run, string id) { while (run.Take(id)) { } }

        static readonly (string evo, string power, string partner)[] NewEvos =
        {
            (SkillCatalogDefs.EvoPlague, SkillCatalogDefs.AutoToxic, SkillCatalogDefs.StatArea),
            (SkillCatalogDefs.EvoSingularity, SkillCatalogDefs.AutoGravity, SkillCatalogDefs.StatCooldown),
            (SkillCatalogDefs.EvoFortress, SkillCatalogDefs.AutoTurret, SkillCatalogDefs.StatMaxHealth),
            (SkillCatalogDefs.EvoMeteorStorm, SkillCatalogDefs.AutoMeteor, SkillCatalogDefs.StatDamage),
            (SkillCatalogDefs.EvoIronMaiden, SkillCatalogDefs.AutoThorns, SkillCatalogDefs.UniKinetic),
            (SkillCatalogDefs.EvoSupercell, SkillCatalogDefs.AutoStormCloud, SkillCatalogDefs.UniCrit),
            (SkillCatalogDefs.EvoBlizzard, SkillCatalogDefs.AutoIceShards, SkillCatalogDefs.StatArea),
            (SkillCatalogDefs.EvoDragonBreath, SkillCatalogDefs.AutoFlameBurst, SkillCatalogDefs.StatFireRate),
            (SkillCatalogDefs.EvoMinefield, SkillCatalogDefs.AutoLandmine, SkillCatalogDefs.StatMoveSpeed),
            (SkillCatalogDefs.EvoAxeStorm, SkillCatalogDefs.AutoAxe, SkillCatalogDefs.StatLuck),
            (SkillCatalogDefs.EvoAlphaPack, SkillCatalogDefs.AutoWarDog, SkillCatalogDefs.StatRegen),
            (SkillCatalogDefs.EvoEarthquake, SkillCatalogDefs.AutoStomp, SkillCatalogDefs.StatMaxHealth),
            (SkillCatalogDefs.EvoTimeStop, SkillCatalogDefs.AutoTimeWarp, SkillCatalogDefs.StatCooldown),
        };

        [Test]
        public void NineteenEvolutions_AtMostOnePerPower_EveryNewPowerHasOne()
        {
            // Approved design: 19 evolutions; Airstrike, Fire Trail, Boomerang and Emergency have none.
            Assert.AreEqual(19, SkillCatalogDefs.All.Count(d => d.IsEvolution));
            foreach (var p in SkillCatalogDefs.All.Where(d => d.layer == SkillLayer.Autonomous))
                Assert.LessOrEqual(SkillCatalogDefs.All.Count(d => d.IsEvolution && d.evolvesFrom == p.id), 1, p.id);
            foreach (var (_, power, _) in NewEvos)
                Assert.IsNotNull(SkillCatalogDefs.EvolutionOf(power), power);
        }

        [Test]
        public void TheThirteenNewEvolutionsHaveTheirRecipesAndText()
        {
            foreach (var (evo, power, partner) in NewEvos)
            {
                var d = SkillCatalogDefs.ById(evo);
                Assert.IsNotNull(d, evo);
                Assert.IsTrue(d.IsEvolution, evo);
                Assert.AreEqual(power, d.evolvesFrom, evo);
                Assert.AreEqual(partner, d.partner, evo);
                Assert.AreNotEqual(d.displayName, SkillDescriptions.Describe(d, 1), $"{evo} has no card text");
                // Unlocked exactly when both of its parts are.
                int level = System.Math.Max(SkillCatalogDefs.ById(power).unlockLevel, SkillCatalogDefs.ById(partner).unlockLevel);
                Assert.IsFalse(SkillCatalogDefs.IsUnlocked(d, level - 1), evo);
                Assert.IsTrue(SkillCatalogDefs.IsUnlocked(d, level), evo);
            }
        }

        [Test]
        public void ALevelUpNeverOffersAnEvolution_EvenWhenOneIsReady()
        {
            foreach (var (evo, power, partner) in NewEvos)
            {
                var run = new SkillRuntime { UnlockLevel = int.MaxValue };
                Max(run, power); run.Take(partner);
                Assert.IsTrue(run.CanEvolve(SkillCatalogDefs.ById(evo)), evo);
                for (int seed = 0; seed < 30; seed++)
                    Assert.IsFalse(SkillOfferBuilder.Build(run, WeaponClass.AssaultRifle, seed, 12).Any(d => d.IsEvolution), $"{evo} seed {seed}");
            }
        }

        [Test]
        public void AChestGivesTheReadyEvolution()
        {
            foreach (var (evo, power, partner) in NewEvos)
            {
                var run = new SkillRuntime { UnlockLevel = int.MaxValue };
                Max(run, power); run.Take(partner);
                var r = run.OpenChest(7);
                Assert.AreEqual(SkillRuntime.ChestKind.Evolution, r.kind, evo);
                Assert.AreEqual(evo, r.card.id);
                Assert.IsTrue(run.IsEvolved(power), evo);
            }
        }

        [Test]
        public void WithoutAReadyEvolutionAChestRanksUpAnOwnedCard()
        {
            var run = new SkillRuntime { UnlockLevel = int.MaxValue };
            run.Take(SkillCatalogDefs.AutoOrbit);
            run.Take(SkillCatalogDefs.StatDamage);
            var r = run.OpenChest(3);
            Assert.AreEqual(SkillRuntime.ChestKind.RankUp, r.kind);
            Assert.IsTrue(r.card.id == SkillCatalogDefs.AutoOrbit || r.card.id == SkillCatalogDefs.StatDamage);
            Assert.AreEqual(2, run.RankOf(r.card.id));
            Assert.AreEqual(2, r.rank);
        }

        [Test]
        public void AFullMaxedBuildGetsABonusFromAChest()
        {
            var run = new SkillRuntime { UnlockLevel = int.MaxValue };
            foreach (var id in new[] { SkillCatalogDefs.AutoOrbit, SkillCatalogDefs.AutoDrone, SkillCatalogDefs.AutoFrostNova,
                                       SkillCatalogDefs.AutoBoomerang, SkillCatalogDefs.UniExecution, SkillCatalogDefs.UniPierce,
                                       SkillCatalogDefs.StatArea, SkillCatalogDefs.StatLuck, SkillCatalogDefs.StatRegen, SkillCatalogDefs.StatCooldown })
                Max(run, id);
            var r = run.OpenChest(11);
            Assert.AreEqual(SkillRuntime.ChestKind.Bonus, r.kind);
            Assert.IsTrue(r.card.IsOverflow);
        }

        [Test]
        public void AChestIsDeterministicForItsSeed()
        {
            SkillRuntime Build()
            {
                var run = new SkillRuntime { UnlockLevel = int.MaxValue };
                foreach (var id in new[] { SkillCatalogDefs.AutoOrbit, SkillCatalogDefs.AutoDrone, SkillCatalogDefs.StatDamage, SkillCatalogDefs.UniCrit })
                    run.Take(id);
                return run;
            }
            for (int seed = 0; seed < 20; seed++)
                Assert.AreEqual(Build().OpenChest(seed).card.id, Build().OpenChest(seed).card.id, $"seed {seed}");
        }

        [Test]
        public void AlphaPackRunsThreeDogs()
        {
            var run = new SkillRuntime { UnlockLevel = int.MaxValue };
            Max(run, SkillCatalogDefs.AutoWarDog); run.Take(SkillCatalogDefs.StatRegen);
            Assert.AreEqual(2, run.WarDogCount);
            run.OpenChest(1);
            Assert.AreEqual(3, run.WarDogCount);
        }
    }
}
