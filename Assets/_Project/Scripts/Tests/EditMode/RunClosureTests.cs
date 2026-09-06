using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// The terminal-run contract. Every case here is one that previously either paid the player
    /// twice or ended a run silently - a defeat used to produce no RunFinishedEvent at all, so the
    /// result screen never appeared and the Battle Pass never counted the run.
    /// </summary>
    public class RunClosureTests
    {
        // Same in-memory storage the other profile tests use: these assert on real currency totals,
        // so they must not read or write the developer's actual save.
        private class InMemorySave : ISaveService
        {
            public readonly Dictionary<string, string> store = new();
            private int _slot;
            private string K(string key) => $"s{_slot}_{key}";

            public void Set(string key, string val) => store[K(key)] = val;
            public void Set(string key, int val) => store[K(key)] = val.ToString();
            public void Set(string key, float val) => store[K(key)] = val.ToString();
            public void Set(string key, bool val) => store[K(key)] = val ? "1" : "0";
            public void Set<T>(string key, T val) where T : class => store[K(key)] = JsonUtility.ToJson(val);
            public string GetString(string key, string fb = "") => store.TryGetValue(K(key), out var v) ? v : fb;
            public int GetInt(string key, int fb = 0) => store.TryGetValue(K(key), out var v) && int.TryParse(v, out var i) ? i : fb;
            public float GetFloat(string key, float fb = 0f) => store.TryGetValue(K(key), out var v) && float.TryParse(v, out var f) ? f : fb;
            public bool GetBool(string key, bool fb = false) => store.TryGetValue(K(key), out var v) ? v == "1" : fb;
            public T Get<T>(string key) where T : class
            {
                if (!store.TryGetValue(K(key), out var j) || string.IsNullOrEmpty(j)) return null;
                try { return JsonUtility.FromJson<T>(j); } catch { return null; }
            }
            public bool Has(string key) => store.ContainsKey(K(key));
            public void Delete(string key) => store.Remove(K(key));
            public void SetSlot(int slot) => _slot = Mathf.Max(0, slot);
            public void Flush() { }
        }

        private InMemorySave _save;
        private CampaignCatalog _campaign;
        private const string Level = "level.closure_test";
        private const string Unlisted = "level.not_in_catalog";

        static CampaignCatalog MakeCatalog(string levelId, int coin, int gold, int gem)
        {
            var catalog = ScriptableObject.CreateInstance<CampaignCatalog>();
            var so = new UnityEditor.SerializedObject(catalog);
            var levels = so.FindProperty("levels");
            levels.arraySize = 1;
            var e = levels.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("levelId").stringValue = levelId;
            e.FindPropertyRelative("firstClearCoin").intValue = coin;
            e.FindPropertyRelative("firstClearGold").intValue = gold;
            e.FindPropertyRelative("firstClearGem").intValue = gem;
            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        [SetUp]
        public void SetUp()
        {
            _save = new InMemorySave();
            PlayerProfile.StorageOverride = _save;
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();

            _campaign = MakeCatalog(Level, 500, 5, 1);
        }

        [TearDown]
        public void TearDown()
        {
            if (_campaign != null) Object.DestroyImmediate(_campaign);
            RunState.Abandon();

            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
        }

        static RunState BeginRunWorth(long coin, string levelId = Level)
        {
            var run = RunState.Begin(levelId);
            run.AddCurrency(PlayerProfile.CurrencyKind.Coin, coin);
            return run;
        }

        [Test]
        public void Victory_ClosesOnce_BanksRunCoin_CompletesStage_AndClaimsFirstClear()
        {
            long before = PlayerProfile.Coin;
            var run = BeginRunWorth(40);

            var result = RunClosure.Close(run, RunOutcome.Victory, _campaign);

            Assert.IsTrue(result.Closed, "victory must produce a terminal result");
            Assert.AreEqual(RunOutcome.Victory, result.Summary.Outcome);
            Assert.IsTrue(result.BankedPayout);
            Assert.IsTrue(result.MarkedComplete);
            Assert.IsTrue(result.ClaimedFirstClear);
            Assert.IsTrue(PlayerProfile.IsLevelCompleted(Level));
            Assert.AreEqual(before + 40 + 500, PlayerProfile.Coin, "run coin + first-clear coin");
        }

        [Test]
        public void Defeat_StillCloses_AndBanksTheDefeatFraction()
        {
            long before = PlayerProfile.Coin;
            var run = BeginRunWorth(100);
            run.SetWave(3);

            var result = RunClosure.Close(run, RunOutcome.Defeat, _campaign);

            Assert.IsTrue(result.Closed, "a defeat is a finished run - it must report");
            Assert.AreEqual(RunOutcome.Defeat, result.Summary.Outcome);
            Assert.AreEqual(3, result.Summary.WaveReached);
            Assert.AreEqual(100, result.Summary.Coin, "the summary reports what was EARNED");
            Assert.IsTrue(result.BankedPayout, "a lost run still banks its fraction");
            long expected = (long)(100 * RunClosure.DefeatCoinFraction);
            Assert.AreEqual(expected, result.BankedCoin, "the result reports what was KEPT");
            Assert.AreEqual(before + expected, PlayerProfile.Coin,
                "GDD closure rule: defeat banks only the defeat fraction of Coin");
        }

        [Test]
        public void Defeat_LosesRareCurrencyEntirely()
        {
            long goldBefore = PlayerProfile.Gold;
            long gemBefore = PlayerProfile.Gem;
            var run = RunState.Begin(Level);
            run.AddCurrency(PlayerProfile.CurrencyKind.Gold, 10);
            run.AddCurrency(PlayerProfile.CurrencyKind.Gem, 3);

            var result = RunClosure.Close(run, RunOutcome.Defeat, _campaign);

            Assert.AreEqual(0, result.BankedGold);
            Assert.AreEqual(0, result.BankedGem);
            Assert.AreEqual(goldBefore, PlayerProfile.Gold, "defeat loses unbanked Gold");
            Assert.AreEqual(gemBefore, PlayerProfile.Gem, "defeat loses unbanked Gem");
        }

        [Test]
        public void Defeat_DoesNotCompleteTheStageOrPayFirstClear()
        {
            var run = BeginRunWorth(0);

            var result = RunClosure.Close(run, RunOutcome.Defeat, _campaign);

            Assert.IsFalse(result.MarkedComplete);
            Assert.IsFalse(result.ClaimedFirstClear);
            Assert.IsFalse(PlayerProfile.IsLevelCompleted(Level));
        }

        [Test]
        public void DuplicateClose_IsRefused_AndCannotPayTwice()
        {
            long before = PlayerProfile.Coin;
            var run = BeginRunWorth(30);

            var first = RunClosure.Close(run, RunOutcome.Victory, _campaign);
            var second = RunClosure.Close(run, RunOutcome.Victory, _campaign);

            Assert.IsTrue(first.Closed);
            Assert.IsFalse(second.Closed, "the second close must produce no terminal event");
            Assert.AreEqual(before + 30 + 500, PlayerProfile.Coin, "paid exactly once");
        }

        [Test]
        public void ConflictingClose_KeepsTheFirstOutcome()
        {
            var run = BeginRunWorth(0);

            var victory = RunClosure.Close(run, RunOutcome.Victory, _campaign);
            var defeat = RunClosure.Close(run, RunOutcome.Defeat, _campaign);

            Assert.AreEqual(RunOutcome.Victory, victory.Summary.Outcome);
            Assert.IsFalse(defeat.Closed, "a late defeat cannot overwrite a win");
            Assert.AreEqual(RunOutcome.Victory, run.Outcome);
        }

        [Test]
        public void FirstClearRewardCannotBeClaimedTwiceAcrossRuns()
        {
            long before = PlayerProfile.Coin;

            var first = RunClosure.Close(BeginRunWorth(10), RunOutcome.Victory, _campaign);
            RunState.Abandon();
            var replay = RunClosure.Close(BeginRunWorth(10), RunOutcome.Victory, _campaign);

            Assert.IsTrue(first.ClaimedFirstClear);
            Assert.IsFalse(replay.ClaimedFirstClear, "first clear is once, ever");
            Assert.IsTrue(replay.MarkedComplete, "a replay still counts as a completion");
            Assert.AreEqual(before + 10 + 500 + 10, PlayerProfile.Coin,
                "both runs' coin, but only one first-clear bonus");
        }

        [Test]
        public void VictoryWithNoCatalogEntry_StillClosesAndStillCompletes()
        {
            var run = BeginRunWorth(15, Unlisted);

            var result = RunClosure.Close(run, RunOutcome.Victory, _campaign);

            Assert.IsTrue(result.Closed, "a missing campaign entry must not suppress the terminal event");
            Assert.IsTrue(result.MarkedComplete);
            Assert.IsFalse(result.ClaimedFirstClear, "no authored entry means no authored bonus");
            Assert.IsTrue(PlayerProfile.IsLevelCompleted(Unlisted));
        }

        [Test]
        public void VictoryWithNullCatalog_StillCloses()
        {
            var result = RunClosure.Close(BeginRunWorth(5), RunOutcome.Victory, null);

            Assert.IsTrue(result.Closed);
            Assert.IsTrue(result.MarkedComplete);
            Assert.IsFalse(result.ClaimedFirstClear);
        }

        [Test]
        public void VictoryWithEmptyLevelId_StillCloses_ButBanksNoProgression()
        {
            var result = RunClosure.Close(BeginRunWorth(20, ""), RunOutcome.Victory, _campaign);

            Assert.IsTrue(result.Closed, "an unidentified map is still a finished run");
            Assert.IsTrue(result.BankedPayout);
            Assert.IsFalse(result.MarkedComplete);
            Assert.IsFalse(result.ClaimedFirstClear);
        }

        [Test]
        public void CloseIsNullSafeAndRejectsInProgress()
        {
            Assert.IsFalse(RunClosure.Close(null, RunOutcome.Victory, _campaign).Closed);
            Assert.IsFalse(RunClosure.Close(BeginRunWorth(0), RunOutcome.InProgress, _campaign).Closed,
                "InProgress is not a terminal outcome");
        }
    }
}
