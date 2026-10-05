using System.Collections.Generic;
using NUnit.Framework;
using ZombieWar.Skills;
using ZombieWar.Skills.Powers;

namespace ZombieWar.Tests
{
    /// <summary>Phase A6: the eight v3 powers.</summary>
    public class PhaseA6Tests
    {
        SkillRuntime _run;

        [SetUp]
        public void SetUp()
        {
            _run = new SkillRuntime { UnlockLevel = int.MaxValue };
            AutonomousPower.ResetGlobalBudget();
        }

        void Max(string id) { while (_run.Take(id)) { } }

        static readonly Dictionary<string, int> Levels = new()
        {
            { SkillCatalogDefs.AutoStormCloud, 6 }, { SkillCatalogDefs.AutoIceShards, 5 }, { SkillCatalogDefs.AutoFlameBurst, 7 },
            { SkillCatalogDefs.AutoLandmine, 10 }, { SkillCatalogDefs.AutoAxe, 11 }, { SkillCatalogDefs.AutoWarDog, 12 },
            { SkillCatalogDefs.AutoStomp, 8 }, { SkillCatalogDefs.AutoTimeWarp, 12 },
        };

        [Test]
        public void EveryV3PowerHasItsLevelTextAndAModule()
        {
            var modules = new List<PowerModule>();
            var claimed = new HashSet<string>();
            foreach (var t in typeof(PowerModule).Assembly.GetTypes())
                if (!t.IsAbstract && typeof(PowerModule).IsAssignableFrom(t))
                {
                    var m = (PowerModule)System.Activator.CreateInstance(t);
                    modules.Add(m);
                    foreach (var id in m.ProcIds) claimed.Add(id);
                }
            foreach (var kv in Levels)
            {
                var d = SkillCatalogDefs.ById(kv.Key);
                Assert.IsNotNull(d, kv.Key);
                Assert.AreEqual(kv.Value, d.unlockLevel, kv.Key);
                for (int r = 1; r <= 5; r++) Assert.AreNotEqual(d.displayName, SkillDescriptions.Describe(d, r), kv.Key);
                bool timed = d.HasTable("cd");
                Assert.AreEqual(timed, claimed.Contains(kv.Key), $"{kv.Key}: a timed power needs a module that owns its proc, a continuous one none");
            }
        }

        [Test]
        public void ProcPowersFireSizedByTheirRank()
        {
            Max(SkillCatalogDefs.AutoIceShards);
            Max(SkillCatalogDefs.AutoAxe);
            Max(SkillCatalogDefs.AutoStomp);
            Max(SkillCatalogDefs.AutoFlameBurst);
            Max(SkillCatalogDefs.AutoTimeWarp);
            var seen = new Dictionary<string, SkillRuntime.PowerProc>();
            for (float t = 100f; t < 102f; t += 0.05f)
                foreach (var p in _run.PollPowers(t, 1f)) seen[p.skillId] = p;
            Assert.AreEqual(10, seen[SkillCatalogDefs.AutoIceShards].targets);
            Assert.AreEqual(3, seen[SkillCatalogDefs.AutoAxe].targets);
            Assert.AreEqual(5f, seen[SkillCatalogDefs.AutoStomp].radius, 1e-4f);
            Assert.AreEqual(6.2f, seen[SkillCatalogDefs.AutoFlameBurst].radius, 1e-4f);
            Assert.IsTrue(seen.ContainsKey(SkillCatalogDefs.AutoTimeWarp));
            Assert.AreEqual(4f, _run.TimeWarpSeconds, 1e-4f);
        }

        [Test]
        public void ContinuousPowersReadTheirRanks()
        {
            Assert.AreEqual(0, _run.WarDogCount);
            _run.Take(SkillCatalogDefs.AutoWarDog);
            Assert.AreEqual(1, _run.WarDogCount);
            Max(SkillCatalogDefs.AutoWarDog);
            Assert.AreEqual(2, _run.WarDogCount);
            Assert.AreEqual(1.8f, _run.WarDogBitesPerSecond, 1e-4f);
            Max(SkillCatalogDefs.AutoStormCloud);
            Assert.AreEqual(0.6f, _run.StormCloudInterval, 1e-4f);
            Max(SkillCatalogDefs.AutoLandmine);
            Assert.AreEqual(2.2f, _run.LandmineSpacing, 1e-4f);
        }
    }
}
