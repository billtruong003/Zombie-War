using System.Collections.Generic;
using NUnit.Framework;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>Phase A3: ten gun modifiers / universals and the Launcher's two signature cards.</summary>
    public class PhaseA3Tests
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
        public void TheTenUniversalsUnlockOnTheirRoad()
        {
            var expected = new Dictionary<string, int>
            {
                { SkillCatalogDefs.UniPierce, 1 }, { SkillCatalogDefs.UniCrit, 1 }, { SkillCatalogDefs.UniSplit, 1 },
                { SkillCatalogDefs.UniRicochet, 2 }, { SkillCatalogDefs.UniSiphon, 4 }, { SkillCatalogDefs.UniAcid, 6 },
                { SkillCatalogDefs.UniExplosive, 8 }, { SkillCatalogDefs.UniDoubleTap, 10 },
                { SkillCatalogDefs.UniGuardian, 11 }, { SkillCatalogDefs.UniGreed, 13 },
            };
            foreach (var kv in expected)
            {
                var d = SkillCatalogDefs.ById(kv.Key);
                Assert.IsNotNull(d, kv.Key);
                Assert.AreEqual(SkillLayer.Universal, d.layer, kv.Key);
                Assert.AreEqual(kv.Value, d.unlockLevel, kv.Key);
                Assert.AreEqual(5, d.maxRank, kv.Key);
            }
        }

        [Test]
        // 05/10 owner: the old road (one card a level up to 34) felt stingy. Now 12 skills open at
        // level 1, then a few cards a level, and everything by level 13.
        public void EachAccountLevelUnlocksAFewCards_AndAllAreOpenByThirteen()
        {
            var at = new List<SkillDef>();
            for (int lv = 2; lv <= 40; lv++)
            {
                SkillCatalogDefs.UnlockedAt(lv, at);
                Assert.LessOrEqual(at.Count, 3, $"level {lv} unlocks {at.Count} cards");
                if (lv > 13) Assert.AreEqual(0, at.Count, $"level {lv} still unlocks cards");
            }
        }

        [Test]
        public void PiercingRoundsPierceEveryShot()
        {
            Assert.AreEqual(0, _run.OnShotFired().bonusPierce);
            Max(SkillCatalogDefs.UniPierce);
            var plan = _run.OnShotFired();
            Assert.AreEqual(3, plan.bonusPierce);
            Assert.AreEqual(0.95f, plan.pierceFalloff, 1e-4f);
            Assert.IsFalse(plan.breach, "a universal pierce is not a Breach Round (its tracer must not say so)");
        }

        [Test]
        public void SplitShotFansEveryNthShot()
        {
            Max(SkillCatalogDefs.UniSplit);   // every 3 shots, 4 extra bullets
            int fans = 0, extra = 0;
            for (int i = 0; i < 30; i++)
            {
                var p = _run.OnShotFired();
                if (p.splitBullets > 0) { fans++; extra = p.splitBullets; }
            }
            Assert.AreEqual(10, fans);
            Assert.AreEqual(4, extra);
        }

        [Test]
        public void CritAndDoubleTapHitTheirRates()
        {
            Max(SkillCatalogDefs.UniCrit);       // 20%
            Max(SkillCatalogDefs.UniDoubleTap);  // 30%
            int crits = 0, taps = 0;
            const int n = 20000;
            for (int i = 0; i < n; i++)
            {
                if (_run.RollCrit()) crits++;
                if (_run.OnShotFired().doubleTap) taps++;
            }
            Assert.AreEqual(0.20f, crits / (float)n, 0.02f);
            Assert.AreEqual(0.30f, taps / (float)n, 0.02f);
        }

        [Test]
        public void NothingRollsWithoutTheCard()
        {
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(_run.RollCrit());
                Assert.IsFalse(_run.RollExplosive());
                var p = _run.OnShotFired();
                Assert.IsFalse(p.doubleTap);
                Assert.AreEqual(0, p.splitBullets);
            }
            Assert.AreEqual(0, _run.RicochetBounces);
            Assert.AreEqual(0f, _run.AcidDps);
            Assert.AreEqual(1f, _run.EnemyHealthMultiplier);
        }

        [Test]
        public void BloodSiphonHealsEveryNKills()
        {
            Max(SkillCatalogDefs.UniSiphon);   // every 12 kills
            for (int i = 0; i < 11; i++) _run.OnKill();
            Assert.AreEqual(0f, _run.PendingHealFraction);
            _run.OnKill();
            Assert.AreEqual(SkillRuntime.SiphonHealFraction, _run.PendingHealFraction, 1e-5f);
            Assert.IsTrue(_run.ConsumeSiphonTrigger());
            Assert.IsFalse(_run.ConsumeSiphonTrigger(), "the trigger is read once");
        }

        [Test]
        public void GuardianAngelSavesOncePerRun()
        {
            Assert.IsFalse(_run.TryGuardianAngel(out _));
            Max(SkillCatalogDefs.UniGuardian);
            Assert.IsTrue(_run.TryGuardianAngel(out float heal));
            Assert.AreEqual(0.7f, heal, 1e-5f);
            Assert.IsFalse(_run.TryGuardianAngel(out _), "once per run");
            _run.Reset();
            Max(SkillCatalogDefs.UniGuardian);
            Assert.IsTrue(_run.TryGuardianAngel(out _), "a new run re-arms it");
        }

        [Test]
        public void GreedPaysCoinAndCostsEnemyHealth()
        {
            float before = _run.CoinMultiplier;
            Max(SkillCatalogDefs.UniGreed);
            Assert.AreEqual(before * 1.6f, _run.CoinMultiplier, 1e-4f);
            Assert.AreEqual(1.3f, _run.EnemyHealthMultiplier, 1e-4f);
        }

        [Test]
        public void LauncherCardsNeedTheLauncherInHand()
        {
            _run.EquippedFamily = WeaponClass.Rocket;
            Max(SkillCatalogDefs.RocketCluster);
            Max(SkillCatalogDefs.RocketNapalm);
            Assert.AreEqual(4, _run.ClusterBomblets);
            Assert.AreEqual(4f, _run.NapalmSeconds, 1e-4f);
            _run.EquippedFamily = WeaponClass.AssaultRifle;
            Assert.AreEqual(0, _run.ClusterBomblets);
            Assert.AreEqual(0f, _run.NapalmSeconds);
        }

        [Test]
        public void LauncherSignaturesAreOfferedOnlyToTheLauncher()
        {
            var cluster = SkillCatalogDefs.ById(SkillCatalogDefs.RocketCluster);
            Assert.IsTrue(cluster.IsCompatibleWith(WeaponClass.Rocket));
            Assert.IsFalse(cluster.IsCompatibleWith(WeaponClass.Shotgun));
        }

        [Test]
        public void EveryNewCardHasText()
        {
            foreach (var id in new[] { SkillCatalogDefs.UniPierce, SkillCatalogDefs.UniRicochet, SkillCatalogDefs.UniSplit,
                                       SkillCatalogDefs.UniCrit, SkillCatalogDefs.UniSiphon, SkillCatalogDefs.UniAcid,
                                       SkillCatalogDefs.UniExplosive, SkillCatalogDefs.UniDoubleTap, SkillCatalogDefs.UniGuardian,
                                       SkillCatalogDefs.UniGreed, SkillCatalogDefs.RocketCluster, SkillCatalogDefs.RocketNapalm })
            {
                var d = SkillCatalogDefs.ById(id);
                for (int r = 1; r <= d.maxRank; r++)
                    Assert.AreNotEqual(d.displayName, SkillDescriptions.Describe(d, r), $"{id} rank {r} has no card text");
            }
        }
    }
}
