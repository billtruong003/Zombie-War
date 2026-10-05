using NUnit.Framework;
using ZombieWar.Threat;

namespace ZombieWar.Tests
{
    /// <summary>Horde Call telegraph (backlog #10, mockup U2 05/10).</summary>
    public class HordeCallTests
    {
        const float Opening = 30f, Every = 75f, Length = 10f;

        [Test]
        public void TheIndexNamesTheRunningOrNextSurge()
        {
            Assert.AreEqual(1, ThreatDirector.SurgeIndexAt(0f, Opening, Every, Length));
            Assert.AreEqual(1, ThreatDirector.SurgeIndexAt(Opening + Every - 5f, Opening, Every, Length), "warning for the first");
            Assert.AreEqual(1, ThreatDirector.SurgeIndexAt(Opening + Every + 2f, Opening, Every, Length), "first running");
            Assert.AreEqual(2, ThreatDirector.SurgeIndexAt(Opening + Every + Length + 1f, Opening, Every, Length), "after the first");
            Assert.AreEqual(2, ThreatDirector.SurgeIndexAt(Opening + 2f * Every + 1f, Opening, Every, Length), "second running");
            Assert.AreEqual(0, ThreatDirector.SurgeIndexAt(100f, Opening, 0f, Length), "surges off");
        }

        [Test]
        public void TheSideNeverRepeatsTwiceInARow()
        {
            for (int seed = 0; seed < 50; seed++)
                for (int i = 1; i < 20; i++)
                {
                    int a = ThreatDirector.SideFor(i, seed), b = ThreatDirector.SideFor(i + 1, seed);
                    Assert.That(a, Is.InRange(0, 3));
                    Assert.AreNotEqual(a, b, $"seed {seed} index {i}");
                }
        }

        [Test]
        public void SidesHaveCompassNames()
        {
            Assert.AreEqual("north", ThreatDirector.SideName(0));
            Assert.AreEqual("east", ThreatDirector.SideName(1));
            Assert.AreEqual("south", ThreatDirector.SideName(2));
            Assert.AreEqual("west", ThreatDirector.SideName(3));
        }
    }
}
