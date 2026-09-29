using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// Phase A0 (dev bench and blocking bugs): the per-source damage ledger and the coalesced save
    /// for gems picked up mid-run.
    public class PhaseA0Tests
    {
        private class CountingSave : ISaveService
        {
            public readonly Dictionary<string, string> store = new();
            public int flushCount;
            public void Set(string key, string val) => store[key] = val;
            public void Set(string key, int val) => store[key] = val.ToString();
            public void Set(string key, float val) => store[key] = val.ToString();
            public void Set(string key, bool val) => store[key] = val ? "1" : "0";
            public void Set<T>(string key, T val) where T : class => store[key] = JsonUtility.ToJson(val);
            public string GetString(string key, string fb = "") => store.TryGetValue(key, out var v) ? v : fb;
            public int GetInt(string key, int fb = 0) => store.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fb;
            public float GetFloat(string key, float fb = 0f) => store.TryGetValue(key, out var v) && float.TryParse(v, out var f) ? f : fb;
            public bool GetBool(string key, bool fb = false) => store.TryGetValue(key, out var v) ? v == "1" : fb;
            public T Get<T>(string key) where T : class =>
                store.TryGetValue(key, out var j) && !string.IsNullOrEmpty(j) ? JsonUtility.FromJson<T>(j) : null;
            public bool Has(string key) => store.ContainsKey(key);
            public void Delete(string key) => store.Remove(key);
            public void SetSlot(int slot) { }
            public void Flush() => flushCount++;
        }

        private CountingSave _save;

        [SetUp]
        public void SetUp()
        {
            _save = new CountingSave();
            PlayerProfile.StorageOverride = _save;
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
            DamageLedger.Reset();
            DamageLedger.Enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.StorageOverride = null;
            PlayerProfile.ResetCacheForTests();
            DamageLedger.Reset();
            DamageLedger.Enabled = false;
        }

        [Test]
        public void Ledger_WhenDisabled_RecordsNothing()
        {
            DamageLedger.Record(SkillCatalogDefs.AutoOrbit, 50f);
            var rows = new List<DamageLedger.Row>();
            DamageLedger.Snapshot(rows);
            Assert.AreEqual(0, rows.Count, "the ledger is off in normal play and must cost nothing");
        }

        [Test]
        public void Ledger_GroupsBySource_AndSortsBiggestFirst()
        {
            DamageLedger.Enabled = true;
            DamageLedger.Record(DamageLedger.Gun, 10f);
            DamageLedger.Record(SkillCatalogDefs.AutoOrbit, 30f);
            DamageLedger.Record(SkillCatalogDefs.AutoOrbit, 30f);
            DamageLedger.Record(SkillCatalogDefs.AutoDrone, 0f);        // zero damage is not a hit

            var rows = new List<DamageLedger.Row>();
            DamageLedger.Snapshot(rows);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(SkillCatalogDefs.AutoOrbit, rows[0].source);
            Assert.AreEqual(60f, rows[0].total, 1e-4f);
            Assert.AreEqual(2, rows[0].hits);
            Assert.AreEqual(10f, DamageLedger.TotalOf(DamageLedger.Gun), 1e-4f);
        }

        [Test]
        public void AddDeferred_ChangesTheBalanceAtOnce()
        {
            long before = PlayerProfile.Gem;
            PlayerProfile.AddDeferred(PlayerProfile.CurrencyKind.Gem, 3);
            Assert.AreEqual(before + 3, PlayerProfile.Gem, "a picked-up gem is in the profile immediately");
        }

        [Test]
        public void AddDeferred_WithoutATimer_SavesRightAway_AndFlushIfDirtyIsThenANoOp()
        {
            // No Bill services in EditMode: there is nothing to defer to, so it must not lose the write.
            _ = PlayerProfile.Gem;                  // first read creates (and saves) a fresh profile
            int before = _save.flushCount;
            PlayerProfile.AddDeferred(PlayerProfile.CurrencyKind.Gem, 1);
            Assert.AreEqual(before + 1, _save.flushCount);
            PlayerProfile.FlushIfDirty();
            Assert.AreEqual(before + 1, _save.flushCount, "nothing left to write");
        }

        [Test]
        public void AddDeferred_IgnoresZeroAndNegative()
        {
            long before = PlayerProfile.Coin;
            PlayerProfile.AddDeferred(PlayerProfile.CurrencyKind.Coin, 0);
            PlayerProfile.AddDeferred(PlayerProfile.CurrencyKind.Coin, -5);
            Assert.AreEqual(before, PlayerProfile.Coin);
        }
    }
}
