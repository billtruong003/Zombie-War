using System;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Tests
{
    /// PlayMode test: CurrencyClusterWidget khong duplicate subscription qua nhieu vong
    /// enable/disable (OnEnable subscribe / OnDisable unsubscribe phai doi xung).
    public class CurrencyWidgetTests
    {
        private sealed class CountingProvider : ICurrencyProvider
        {
            public int Subscribers;
            private Action _changed;
            public long Coin => 42;
            public long Gold => 0;
            public long Gem => 0;
            public event Action Changed
            {
                add { _changed += value; Subscribers++; }
                remove { _changed -= value; Subscribers--; }
            }
        }

        [Test]
        public void EnableDisableCycles_DoNotDuplicateSubscriptions()
        {
            var go = new GameObject("CurrencyWidgetTest");
            go.SetActive(false); // AddComponent khong chay OnEnable khi GO inactive
            var widget = go.AddComponent<CurrencyClusterWidget>();
            var provider = new CountingProvider();
            widget.Bind(provider); // inactive -> chi set provider, chua subscribe

            for (int i = 0; i < 3; i++)
            {
                go.SetActive(true);
                Assert.AreEqual(1, provider.Subscribers, $"Vong {i}: enable phai co dung 1 listener.");
                go.SetActive(false);
                Assert.AreEqual(0, provider.Subscribers, $"Vong {i}: disable phai go het listener.");
            }

            UnityEngine.Object.Destroy(go);
        }
    }
}
