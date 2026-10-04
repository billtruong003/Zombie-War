using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// G5 (04/10): the profile write path — batched transactions, the last-good backup, the
    /// newer-schema guard and the v3 move of radio flags out of the FTUE steps.
    public class ProfileStoreTests
    {
        private TestSaveStore _save;
        private int _walletEvents;

        private void OnWallet() => _walletEvents++;

        [SetUp]
        public void SetUp()
        {
            _save = new TestSaveStore();
            PlayerProfile.StorageOverride = _save;
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
            _walletEvents = 0;
            PlayerProfile.WalletChanged += OnWallet;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.WalletChanged -= OnWallet;
            PlayerProfile.StorageOverride = null;
            PlayerProfile.ResetCacheForTests();
        }

        [Test]
        public void Batch_WritesOnceAndRaisesEachEventOnce()
        {
            _ = PlayerProfile.Coin;                 // load (and write the fresh profile) first
            int flushesBefore = _save.flushCount;
            _walletEvents = 0;

            bool ok = PlayerProfile.Batch(() =>
            {
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 10);
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 20);
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 5);
            });

            Assert.IsTrue(ok);
            Assert.AreEqual(30, PlayerProfile.Coin);
            Assert.AreEqual(1, _save.flushCount - flushesBefore, "a batch is one disk write");
            Assert.AreEqual(1, _walletEvents, "events fire once, after the commit");
        }

        [Test]
        public void Batch_RollsBackEverythingWhenTheWriteFails()
        {
            LogAssert.ignoreFailingMessages = true;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 100);
            _walletEvents = 0;
            _save.throwOnSet = true;

            bool ok = PlayerProfile.Batch(() =>
            {
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 50);
                PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Coin, 30);
            });

            Assert.IsFalse(ok);
            Assert.AreEqual(100, PlayerProfile.Coin, "nothing inside a failed batch survives");
            Assert.AreEqual(0, _walletEvents, "a rolled-back batch raises no event");
        }

        [Test]
        public void Batch_RollsBackWhenTheBodyThrows()
        {
            LogAssert.ignoreFailingMessages = true;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 40);
            bool ok = PlayerProfile.Batch(() =>
            {
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 60);
                throw new System.InvalidOperationException("boom");
            });
            Assert.IsFalse(ok);
            Assert.AreEqual(40, PlayerProfile.Coin);
        }

        [Test]
        public void UnreadableSave_RestoresTheLastGoodBackup_AndKeepsTheDamagedCopy()
        {
            LogAssert.ignoreFailingMessages = true;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 500);
            PlayerProfile.ResetCacheForTests();
            _ = PlayerProfile.Coin;                 // a clean load writes the backup

            _save.store["s0_" + PlayerProfile.SaveKey] = "{ this is not json";
            PlayerProfile.ResetCacheForTests();

            Assert.AreEqual(500, PlayerProfile.Coin, "the backup is used instead of an empty profile");
            Assert.AreEqual("{ this is not json", _save.GetString(PlayerProfile.CorruptKey));
        }

        [Test]
        public void NewerSchemaSave_IsReadButNeverWrittenBack()
        {
            LogAssert.ignoreFailingMessages = true;
            var raw = "{\"version\":99,\"coin\":7,\"playerId\":\"12345678\"}";
            _save.store["s0_" + PlayerProfile.SaveKey] = raw;
            PlayerProfile.ResetCacheForTests();

            Assert.AreEqual(7, PlayerProfile.Coin);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 1);
            Assert.AreEqual(raw, _save.store["s0_" + PlayerProfile.SaveKey], "unknown fields are not dropped by a rewrite");
        }

        [Test]
        public void V2Save_MovesRadioFlagsOutOfFtueSteps()
        {
            _save.store["s0_" + PlayerProfile.SaveKey] =
                "{\"version\":2,\"playerId\":\"12345678\",\"ftueSteps\":[\"move\",\"vo.vo_riley_ftue_home\",\"vo.dlg.01\"]}";
            PlayerProfile.ResetCacheForTests();

            Assert.IsTrue(PlayerProfile.HasFtueStep("move"));
            Assert.IsFalse(PlayerProfile.HasFtueStep("vo.dlg.01"));
            Assert.IsTrue(PlayerProfile.HasVoFlag("vo_riley_ftue_home"));
            Assert.IsTrue(PlayerProfile.HasVoFlag("dlg.01"));
        }

        [Test]
        public void ClearFtueSteps_AlsoForgetsRadioFlags()
        {
            PlayerProfile.MarkFtueStep("move");
            PlayerProfile.MarkVoFlag("vo_riley_ftue_home");
            PlayerProfile.ClearFtueSteps();
            Assert.IsFalse(PlayerProfile.HasFtueStep("move"));
            Assert.IsFalse(PlayerProfile.HasVoFlag("vo_riley_ftue_home"));
        }
    }
}
