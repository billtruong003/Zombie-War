using NUnit.Framework;
using UnityEngine;
using ZombieWar.Online;

namespace ZombieWar.Tests
{
    public class RemoteConfigTests
    {
        const string CacheKey = "hc.config";
        string _saved;

        [SetUp] public void Keep() => _saved = PlayerPrefs.GetString(CacheKey, null);

        [TearDown]
        public void Restore()
        {
            if (_saved == null) PlayerPrefs.DeleteKey(CacheKey); else PlayerPrefs.SetString(CacheKey, _saved);
            RemoteConfig.Apply(_saved ?? "{}");
            if (_saved == null) PlayerPrefs.DeleteKey(CacheKey);
        }

        [Test]
        public void VersionsCompareAsNumbers()
        {
            Assert.IsTrue(RemoteConfig.Older("1.0.9", "1.0.10"));
            Assert.IsFalse(RemoteConfig.Older("1.1.0", "1.0.10"));
            Assert.IsFalse(RemoteConfig.Older("1.0.0", "1.0.0"));
            Assert.IsFalse(RemoteConfig.Older("dev", "1.0.0"), "an unreadable version never forces an update");
        }

        [Test]
        public void MissingFields_MeanEverythingOn()
        {
            RemoteConfig.Apply("{\"version\":2}");
            Assert.IsTrue(RemoteConfig.AdsOn);
            Assert.IsTrue(RemoteConfig.InterstitialOn);
            Assert.IsTrue(RemoteConfig.GachaOn);
            Assert.IsTrue(RemoteConfig.CloudSaveOn);
            Assert.IsFalse(RemoteConfig.Maintenance);
            Assert.IsFalse(RemoteConfig.UpdateRequired);
        }

        [Test]
        public void SwitchesAndMaintenance_AreRead()
        {
            RemoteConfig.Apply("{\"maintenance\":{\"on\":true,\"message\":\"back at 9\"},\"features\":{\"ads\":true,\"interstitial\":false,\"gacha\":false,\"cloudSave\":true},\"minVersion\":\"999.0.0\"}");
            Assert.IsFalse(RemoteConfig.InterstitialOn);
            Assert.IsFalse(RemoteConfig.GachaOn);
            Assert.IsTrue(RemoteConfig.AdsOn);
            Assert.IsTrue(RemoteConfig.Maintenance);
            Assert.AreEqual("back at 9", RemoteConfig.MaintenanceMessage);
            Assert.IsTrue(RemoteConfig.UpdateRequired);
        }

        [Test]
        public void ABrokenAnswer_KeepsTheLastGoodOne()
        {
            RemoteConfig.Apply("{\"features\":{\"gacha\":false}}");
            RemoteConfig.Apply("not json");
            Assert.IsFalse(RemoteConfig.GachaOn);
        }
    }
}
