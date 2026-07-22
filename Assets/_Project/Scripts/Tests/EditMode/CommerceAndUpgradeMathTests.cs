using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    public class CommerceAndUpgradeMathTests
    {
        [Test]
        public void CostumeItem_UsesExplicitOfferBeforeRarityFallback()
        {
            var economy = ScriptableObject.CreateInstance<EconomyConfig>();
            economy.costumeBands = new List<EconomyConfig.RarityBand>
            {
                new() { rarity = WeaponTier.Epic, currency = WalletCurrency.Coin, price = 999 }
            };
            economy.costumeItems = new List<EconomyConfig.CostumeEntry>
            {
                new() { itemId = "eye.happy", rarity = WeaponTier.Epic,
                    source = AcquireSource.Shop, currency = WalletCurrency.Gem, price = 20 }
            };
            economy.RebuildLookups();

            Assert.IsTrue(economy.TryGetCostumePrice("eye.happy", out var currency, out long price));
            Assert.AreEqual(WalletCurrency.Gem, currency);
            Assert.AreEqual(20, price);
            Object.DestroyImmediate(economy);
        }

        [Test]
        public void CostumeSet_SupportsCoinAndLegacyGemOffers()
        {
            var economy = ScriptableObject.CreateInstance<EconomyConfig>();
            var coin = new EconomyConfig.CostumeSetEntry { currency = WalletCurrency.Coin, price = 3500 };
            var legacy = new EconomyConfig.CostumeSetEntry { gemPrice = 60 };

            Assert.IsTrue(economy.TryGetCostumeSetPrice(coin, out var coinCurrency, out long coinPrice));
            Assert.AreEqual(WalletCurrency.Coin, coinCurrency);
            Assert.AreEqual(3500, coinPrice);
            Assert.IsTrue(economy.TryGetCostumeSetPrice(legacy, out var gemCurrency, out long gemPrice));
            Assert.AreEqual(WalletCurrency.Gem, gemCurrency);
            Assert.AreEqual(60, gemPrice);
            Object.DestroyImmediate(economy);
        }

        [Test]
        public void WeaponStars_IncreaseTheStatsShownAndUsedByCombat()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.damage = 20f;
            weapon.fireRate = 10f;

            Assert.AreEqual(20f, WeaponUpgradeMath.EffectiveDamage(weapon, 1), 0.001f);
            Assert.AreEqual(23f, WeaponUpgradeMath.EffectiveDamage(weapon, 2), 0.001f);
            Assert.AreEqual(27f, WeaponUpgradeMath.EffectiveDamage(weapon, 3), 0.001f);
            Assert.AreEqual(10f, WeaponUpgradeMath.EffectiveFireRate(weapon, 1), 0.001f);
            Assert.AreEqual(10.5f, WeaponUpgradeMath.EffectiveFireRate(weapon, 2), 0.001f);
            Assert.AreEqual(11.2f, WeaponUpgradeMath.EffectiveFireRate(weapon, 3), 0.001f);
            Object.DestroyImmediate(weapon);
        }
    }
}
