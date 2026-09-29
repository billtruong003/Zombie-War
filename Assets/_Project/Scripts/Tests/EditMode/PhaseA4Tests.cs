using NUnit.Framework;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>Phase A4: Cooldown, Area, Pickup Range, Regeneration, Luck.</summary>
    public class PhaseA4Tests
    {
        SkillRuntime _run;

        [SetUp]
        public void SetUp()
        {
            _run = new SkillRuntime { UnlockLevel = int.MaxValue };
            AutonomousPower.ResetGlobalBudget();
        }

        void Max(string id) { while (_run.Take(id)) { } }

        int ChainProcsIn(float from, float seconds)
        {
            int procs = 0;
            for (float t = from; t <= from + seconds; t += 0.05f)
                foreach (var p in _run.PollPowers(t, 1f))
                    if (p.skillId == SkillCatalogDefs.AutoChainLightning) procs++;
            return procs;
        }

        [Test]
        public void CooldownSpeedsUpProcPowers_InEitherPickOrder()
        {
            Max(SkillCatalogDefs.AutoChainLightning);            // 4 s
            Max(SkillCatalogDefs.StatCooldown);                  // -35% → 2.6 s, taken AFTER the power
            Assert.AreEqual(0.65f, _run.CooldownMultiplier, 1e-4f);
            // t = 100, 102.6, … 118.2: eight procs in 20 s (was six at 4 s).
            Assert.AreEqual(8, ChainProcsIn(100f, 20f));
        }

        [Test]
        public void AreaGrowsEveryPowerArea()
        {
            Max(SkillCatalogDefs.AutoOrbit);
            Max(SkillCatalogDefs.AutoFrostNova);
            float orbit = _run.OrbitRadius, frost = _run.FrostRadius;
            Max(SkillCatalogDefs.StatArea);
            Assert.AreEqual(orbit * 1.4f, _run.OrbitRadius, 1e-3f);
            Assert.AreEqual(frost * 1.4f, _run.FrostRadius, 1e-3f);
        }

        [Test]
        public void AreaGrowsProcRadii()
        {
            Max(SkillCatalogDefs.AutoOrdnance);
            float before = 0f;
            for (float t = 100f; t < 110f && before == 0f; t += 0.05f)
                foreach (var p in _run.PollPowers(t, 1f)) if (p.skillId == SkillCatalogDefs.AutoOrdnance) before = p.radius;
            Max(SkillCatalogDefs.StatArea);
            float after = 0f;
            for (float t = 200f; t < 210f && after == 0f; t += 0.05f)
                foreach (var p in _run.PollPowers(t, 1f)) if (p.skillId == SkillCatalogDefs.AutoOrdnance) after = p.radius;
            Assert.Greater(before, 0f);
            Assert.AreEqual(before * 1.4f, after, 1e-3f);
        }

        [Test]
        public void PickupRegenAndLuckReadTheirRanks()
        {
            Assert.AreEqual(1f, _run.PickupRangeMultiplier);
            Assert.AreEqual(0f, _run.RegenPerSecond);
            Assert.AreEqual(1f, _run.LuckMultiplier);
            Max(SkillCatalogDefs.StatPickup);
            Max(SkillCatalogDefs.StatRegen);
            Max(SkillCatalogDefs.StatLuck);
            Assert.AreEqual(2.25f, _run.PickupRangeMultiplier, 1e-4f);
            Assert.AreEqual(0.02f, _run.RegenPerSecond, 1e-5f);
            Assert.AreEqual(1.5f, _run.LuckMultiplier, 1e-4f);
        }

        [Test]
        public void TheFiveStatsUseStatSlotsAndUnlockOnTheRoad()
        {
            var expected = new System.Collections.Generic.Dictionary<string, int>
            {
                { SkillCatalogDefs.StatCooldown, 4 }, { SkillCatalogDefs.StatArea, 8 }, { SkillCatalogDefs.StatPickup, 13 },
                { SkillCatalogDefs.StatRegen, 21 }, { SkillCatalogDefs.StatLuck, 29 },
            };
            foreach (var kv in expected)
            {
                var d = SkillCatalogDefs.ById(kv.Key);
                Assert.AreEqual(SkillSlot.Stat, d.Slot, kv.Key);
                Assert.AreEqual(kv.Value, d.unlockLevel, kv.Key);
                Assert.AreNotEqual(d.displayName, SkillDescriptions.Describe(d, 1), $"{kv.Key} has no card text");
            }
        }
    }
}
