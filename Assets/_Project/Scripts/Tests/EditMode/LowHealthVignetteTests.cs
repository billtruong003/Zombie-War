using NUnit.Framework;
using ZombieWar.UI;

namespace ZombieWar.Tests
{
    /// <summary>Backlog #9: the low-health heartbeat.</summary>
    public class LowHealthVignetteTests
    {
        [Test]
        public void TheHeartBeatsFasterNearDeath()
        {
            Assert.AreEqual(70f, LowHealthVignette.Bpm(LowHealthVignette.Threshold), 0.01f);
            Assert.AreEqual(130f, LowHealthVignette.Bpm(0f), 0.01f);
            Assert.Greater(LowHealthVignette.Bpm(0.05f), LowHealthVignette.Bpm(0.2f));
        }

        [Test]
        public void TheBeatPulsesBetweenRestAndThump()
        {
            float min = 1f, max = 0f;
            for (int i = 0; i < 200; i++)
            {
                float b = LowHealthVignette.Beat(i / 200f * (60f / 70f), LowHealthVignette.Threshold);
                min = UnityEngine.Mathf.Min(min, b); max = UnityEngine.Mathf.Max(max, b);
            }
            Assert.AreEqual(0.35f, min, 0.02f, "rest");
            Assert.Greater(max, 0.9f, "thump");
        }
    }
}
