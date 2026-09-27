using NUnit.Framework;

namespace ZombieWar.Tests
{
    public class GameSettingsPresetTests
    {
        [Test]
        public void Presets_GetHeavierFromLowToHigh()
        {
            var low = GameSettings.PresetFor(GameSettings.Graphics.Low);
            var mid = GameSettings.PresetFor(GameSettings.Graphics.Mid);
            var high = GameSettings.PresetFor(GameSettings.Graphics.High);
            Assert.Less(low.renderScale, mid.renderScale);
            Assert.Less(mid.renderScale, high.renderScale);
            Assert.AreEqual(0f, low.shadowDistance, "Low has no shadows");
            Assert.Less(mid.shadowDistance, high.shadowDistance);
            Assert.IsFalse(low.postFx);
            Assert.IsTrue(high.hdr);
            Assert.AreEqual(1f, high.renderScale);
        }

        [TestCase(2048, GameSettings.Graphics.Low)]
        [TestCase(4096, GameSettings.Graphics.Mid)]
        [TestCase(8192, GameSettings.Graphics.High)]
        public void DeviceDefault_FollowsMemory(int mb, GameSettings.Graphics expected)
        {
            Assert.AreEqual(expected, GameSettings.DeviceDefault(mb));
        }
    }
}

