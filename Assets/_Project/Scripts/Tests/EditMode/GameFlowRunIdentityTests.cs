using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// Which campaign stage a run is banked against. A direct start used to begin with an empty level
    /// id, so playing Stage 1 from the editor (or from Hub before any selection) completed no stage
    /// and paid no first-clear reward.
    ///
    /// These exercise <see cref="GameFlow.LevelIdForScene"/> rather than the load coroutines: the
    /// scene plumbing needs Bill services, but the identity decision is what actually regressed and
    /// it is pure.
    /// </summary>
    public class GameFlowRunIdentityTests
    {
        // SelectLevel persists LastSelectedLevelId, which without this seam would fall back to a real
        // SaveService and write into the developer's actual profile.
        private class InMemorySave : ISaveService
        {
            private readonly Dictionary<string, string> _store = new();
            private int _slot;
            private string K(string key) => $"s{_slot}_{key}";

            public void Set(string key, string val) => _store[K(key)] = val;
            public void Set(string key, int val) => _store[K(key)] = val.ToString();
            public void Set(string key, float val) => _store[K(key)] = val.ToString();
            public void Set(string key, bool val) => _store[K(key)] = val ? "1" : "0";
            public void Set<T>(string key, T val) where T : class => _store[K(key)] = JsonUtility.ToJson(val);
            public string GetString(string key, string fb = "") => _store.TryGetValue(K(key), out var v) ? v : fb;
            public int GetInt(string key, int fb = 0) => _store.TryGetValue(K(key), out var v) && int.TryParse(v, out var i) ? i : fb;
            public float GetFloat(string key, float fb = 0f) => _store.TryGetValue(K(key), out var v) && float.TryParse(v, out var f) ? f : fb;
            public bool GetBool(string key, bool fb = false) => _store.TryGetValue(K(key), out var v) ? v == "1" : fb;
            public T Get<T>(string key) where T : class
            {
                if (!_store.TryGetValue(K(key), out var j) || string.IsNullOrEmpty(j)) return null;
                try { return JsonUtility.FromJson<T>(j); } catch { return null; }
            }
            public bool Has(string key) => _store.ContainsKey(K(key));
            public void Delete(string key) => _store.Remove(K(key));
            public void SetSlot(int slot) => _slot = Mathf.Max(0, slot);
            public void Flush() { }
        }

        static CampaignLevel Level(string id, string scene) =>
            new CampaignLevel { levelId = id, sceneName = scene };

        [SetUp]
        public void SetUp()
        {
            PlayerProfile.StorageOverride = new InMemorySave();
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
            GameFlow.SelectLevel(null);
        }

        [TearDown]
        public void TearDown()
        {
            GameFlow.SelectLevel(null);
            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
        }

        [Test]
        public void NoSelection_DefaultsToStageOne()
        {
            Assert.AreEqual(GameFlow.DefaultGameplayScene, GameFlow.PendingGameplayScene);
            Assert.AreEqual("level.1", GameFlow.PendingLevelId);
            Assert.AreEqual("level.1", GameFlow.LevelIdForScene(GameFlow.DefaultGameplayScene));
        }

        [Test]
        public void SelectedStage_KeepsItsOwnIdentity()
        {
            GameFlow.SelectLevel(Level("level.2", "Map_Level2"));
            Assert.AreEqual("Map_Level2", GameFlow.PendingGameplayScene);
            Assert.AreEqual("level.2", GameFlow.PendingLevelId);

            GameFlow.SelectLevel(Level("level.3", "Map_Level3"));
            Assert.AreEqual("Map_Level3", GameFlow.PendingGameplayScene);
            Assert.AreEqual("level.3", GameFlow.PendingLevelId);
        }

        [Test]
        public void RestartResolvesFromTheSceneBeingReloaded_NotFromTheSelection()
        {
            // Restart reloads ActiveGameplayScene. If the selection has drifted away from what is
            // actually on screen, the id must follow the scene - otherwise a restart could bank a
            // clear against a stage the player is not playing.
            GameFlow.SelectLevel(Level("level.3", "Map_Level3"));

            Assert.AreEqual("level.3", GameFlow.LevelIdForScene("Map_Level3"), "restarting Stage 3");
            Assert.AreEqual("level.1", GameFlow.LevelIdForScene(GameFlow.DefaultGameplayScene),
                "the Stage 1 map is Stage 1 even while Stage 3 is selected");
        }

        [Test]
        public void ReturningHomeAndStartingDefaultDoesNotInheritTheOldStage()
        {
            GameFlow.SelectLevel(Level("level.3", "Map_Level3"));
            GameFlow.SelectLevel(null);   // Home clears the selection

            Assert.AreEqual(GameFlow.DefaultGameplayScene, GameFlow.PendingGameplayScene);
            Assert.AreEqual("level.1", GameFlow.PendingLevelId);
        }

        [Test]
        public void SceneAndLevelIdNeverDisagree()
        {
            // The invariant the fix exists to guarantee: whatever scene the flow decides to load,
            // the id it begins the run with belongs to that same scene.
            GameFlow.SelectLevel(Level("level.4", "Map_Level4"));
            Assert.AreEqual(GameFlow.LevelIdForScene(GameFlow.PendingGameplayScene), GameFlow.PendingLevelId);

            GameFlow.SelectLevel(null);
            Assert.AreEqual(GameFlow.LevelIdForScene(GameFlow.PendingGameplayScene), GameFlow.PendingLevelId);
        }

        [Test]
        public void UnlistedMapYieldsNoStageIdentity()
        {
            // A test map is a legitimate run: it simply completes no campaign stage. RunClosure still
            // closes it - see RunClosureTests.VictoryWithEmptyLevelId_StillCloses.
            Assert.AreEqual("", GameFlow.LevelIdForScene("Map_GenTest"));
        }

        [Test]
        public void SelectionWithBlankIdFallsBackRatherThanBankingAnEmptyStage()
        {
            GameFlow.SelectLevel(Level("", GameFlow.DefaultGameplayScene));
            Assert.AreEqual("level.1", GameFlow.LevelIdForScene(GameFlow.DefaultGameplayScene));
        }
    }
}
