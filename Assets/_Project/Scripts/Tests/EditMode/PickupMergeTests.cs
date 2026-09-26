using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M8: at horde density, coins merge instead of carpeting the ground. The value must never be
    /// lost or double-counted by a merge.
    /// </summary>
    public class PickupMergeTests
    {
        static Pickup MakeCoin(int amount)
        {
            var go = new GameObject("coin");
            var p = go.AddComponent<Pickup>();
            p.Init(PlayerProfile.CurrencyKind.Coin, amount, "pickup_coin", Vector3.zero);
            return p;
        }

        [Test]
        public void ARestingCoinAbsorbsAnotherDropsValue()
        {
            var coin = MakeCoin(2);
            Assert.IsTrue(coin.TryAbsorb(PlayerProfile.CurrencyKind.Coin, 3));
            Assert.AreEqual(5, coin.Amount, "the merged coin carries both values");
            Object.DestroyImmediate(coin.gameObject);
        }

        [Test]
        public void ACoinNeverAbsorbsAnotherCurrency()
        {
            var coin = MakeCoin(2);
            Assert.IsFalse(coin.TryAbsorb(PlayerProfile.CurrencyKind.Gem, 1), "a gem must stay a gem");
            Assert.AreEqual(2, coin.Amount);
            Object.DestroyImmediate(coin.gameObject);
        }

        [Test]
        public void NothingIsAbsorbedForZero()
        {
            var coin = MakeCoin(2);
            Assert.IsFalse(coin.TryAbsorb(PlayerProfile.CurrencyKind.Coin, 0));
            Assert.AreEqual(2, coin.Amount);
            Object.DestroyImmediate(coin.gameObject);
        }
    }
}
