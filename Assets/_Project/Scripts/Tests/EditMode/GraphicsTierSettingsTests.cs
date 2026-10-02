using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// The graphics tier and the Settings screen's graphics option are one value (02/10).
    public class GraphicsTierSettingsTests
    {
        const string Key = "set.graphics";
        bool _had; int _saved;

        [SetUp] public void Save() { _had = PlayerPrefs.HasKey(Key); _saved = PlayerPrefs.GetInt(Key, 0); }
        [TearDown]
        public void Restore()
        {
            if (_had) GameSettings.Quality = (GameSettings.Graphics)_saved;
            else GameSettings.ResetQualityToDevice();
        }

        [Test]
        public void SettingTheTier_SetsTheSettingsOption_AndTellsListeners()
        {
            GraphicsTier.Level heard = GraphicsTier.Level.High;
            void Listen(GraphicsTier.Level l) => heard = l;
            GraphicsTier.Changed += Listen;
            try
            {
                GraphicsTier.Set(GraphicsTier.Level.Low);
                Assert.AreEqual(GameSettings.Graphics.Low, GameSettings.Quality);
                Assert.AreEqual(GraphicsTier.Level.Low, GraphicsTier.Current);
                Assert.AreEqual(GraphicsTier.Level.Low, heard);
            }
            finally { GraphicsTier.Changed -= Listen; }
        }

        [Test]
        public void TheSettingsOption_DrivesTheTier()
        {
            GameSettings.Quality = GameSettings.Graphics.Mid;
            Assert.AreEqual(GraphicsTier.Level.Mid, GraphicsTier.Current);
            Assert.AreEqual(8, GraphicsTier.PointLights);
        }

        [TestCase(8192, 4, 0, GameSettings.Graphics.Low)]    // four cores: low whatever the memory
        [TestCase(8192, 8, 512, GameSettings.Graphics.Low)]  // small GPU memory
        [TestCase(6144, 8, 0, GameSettings.Graphics.High)]
        [TestCase(6144, 6, 0, GameSettings.Graphics.Mid)]
        public void DeviceGuess_UsesMemoryCoresAndGpuMemory(int mb, int cores, int vram, GameSettings.Graphics expected)
        {
            Assert.AreEqual(expected, GameSettings.DeviceDefault(mb, cores, vram));
        }
    }
}
