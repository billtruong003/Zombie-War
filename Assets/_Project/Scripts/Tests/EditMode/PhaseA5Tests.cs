using System.Collections.Generic;
using NUnit.Framework;
using ZombieWar.Skills;
using ZombieWar.Skills.Powers;

namespace ZombieWar.Tests
{
    /// <summary>Phase A5: Toxic Cloud, Gravity Well, Thorn Aura, Sentry Turret, Meteor.</summary>
    public class PhaseA5Tests
    {
        SkillRuntime _run;

        [SetUp]
        public void SetUp()
        {
            _run = new SkillRuntime { UnlockLevel = int.MaxValue };
            AutonomousPower.ResetGlobalBudget();
        }

        void Max(string id) { while (_run.Take(id)) { } }

        List<SkillRuntime.PowerProc> Poll(float from, float seconds)
        {
            var all = new List<SkillRuntime.PowerProc>();
            for (float t = from; t <= from + seconds; t += 0.05f) all.AddRange(_run.PollPowers(t, 1f));
            return all;
        }

        [Test]
        public void TheFourTimedPowersProcOnTheirCooldowns()
        {
            Max(SkillCatalogDefs.AutoToxic);    // 5 s
            Max(SkillCatalogDefs.AutoGravity);  // 7 s
            Max(SkillCatalogDefs.AutoTurret);   // 10 s
            Max(SkillCatalogDefs.AutoMeteor);   // 11 s
            var count = new Dictionary<string, int>();
            foreach (var p in Poll(100f, 21.9f)) count[p.skillId] = count.GetValueOrDefault(p.skillId) + 1;
            Assert.AreEqual(5, count[SkillCatalogDefs.AutoToxic]);
            Assert.AreEqual(4, count[SkillCatalogDefs.AutoGravity]);
            Assert.AreEqual(3, count[SkillCatalogDefs.AutoTurret]);
            Assert.AreEqual(2, count[SkillCatalogDefs.AutoMeteor]);
        }

        [Test]
        public void RankFiveTurretDropsTwo()
        {
            _run.Take(SkillCatalogDefs.AutoTurret);
            Assert.AreEqual(1, _run.TurretCount);
            Max(SkillCatalogDefs.AutoTurret);
            Assert.AreEqual(2, _run.TurretCount);
            foreach (var p in Poll(100f, 1f))
                if (p.skillId == SkillCatalogDefs.AutoTurret) Assert.AreEqual(2, p.targets);
        }

        [Test]
        public void ThornAuraIsContinuousAndGrowsWithArea()
        {
            Assert.AreEqual(0f, _run.ThornAuraRadius);
            Max(SkillCatalogDefs.AutoThorns);
            Assert.AreEqual(SkillRuntime.ThornRadius, _run.ThornAuraRadius, 1e-4f);
            foreach (var p in Poll(100f, 10f)) Assert.AreNotEqual(SkillCatalogDefs.AutoThorns, p.skillId, "the aura never procs");
            Max(SkillCatalogDefs.StatArea);
            Assert.AreEqual(SkillRuntime.ThornRadius * 1.4f, _run.ThornAuraRadius, 1e-4f);
        }

        [Test]
        public void EveryNewPowerHasAModuleTextAndItsUnlockLevel()
        {
            var claimed = new HashSet<string>();
            foreach (var t in typeof(PowerModule).Assembly.GetTypes())
                if (!t.IsAbstract && typeof(PowerModule).IsAssignableFrom(t))
                    foreach (var id in ((PowerModule)System.Activator.CreateInstance(t)).ProcIds) claimed.Add(id);
            var levels = new Dictionary<string, int>
            {
                { SkillCatalogDefs.AutoToxic, 1 }, { SkillCatalogDefs.AutoGravity, 2 }, { SkillCatalogDefs.AutoThorns, 3 },
                { SkillCatalogDefs.AutoTurret, 7 }, { SkillCatalogDefs.AutoMeteor, 9 },
            };
            foreach (var kv in levels)
            {
                var d = SkillCatalogDefs.ById(kv.Key);
                Assert.AreEqual(kv.Value, d.unlockLevel, kv.Key);
                Assert.AreEqual(SkillLayer.Autonomous, d.layer, kv.Key);
                for (int r = 1; r <= 5; r++) Assert.AreNotEqual(d.displayName, SkillDescriptions.Describe(d, r), kv.Key);
                if (kv.Key != SkillCatalogDefs.AutoThorns) Assert.IsTrue(claimed.Contains(kv.Key), $"no module handles {kv.Key}");
            }
        }
    }
}
