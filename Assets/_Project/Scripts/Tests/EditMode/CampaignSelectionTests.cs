using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// Campaign selector navigation and fallback rules (Phase 1).
    ///
    /// These cover the cases a player can actually reach and the ones a corrupted or migrated profile
    /// can produce: a saved stage that no longer exists, a saved stage that is no longer reachable, an
    /// empty catalog, a single-stage catalog, and both list boundaries. Progression state is faked
    /// through the profile's in-memory save seam so nothing touches the real player file.
    /// </summary>
    public class CampaignSelectionTests
    {
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

        private CampaignCatalog _catalog;

        /// <param name="recommended">Per-stage recommended power, so the advisory path can be driven.</param>
        static CampaignCatalog MakeCatalog(int stages, int recommended = 0)
        {
            var catalog = ScriptableObject.CreateInstance<CampaignCatalog>();
            var so = new UnityEditor.SerializedObject(catalog);
            var levels = so.FindProperty("levels");
            levels.arraySize = stages;
            for (int i = 0; i < stages; i++)
            {
                var e = levels.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("levelId").stringValue = $"level.{i + 1}";
                e.FindPropertyRelative("displayName").stringValue = $"Stage {i + 1}";
                e.FindPropertyRelative("sceneName").stringValue = $"Map_Level{i + 1}";
                e.FindPropertyRelative("recommendedPower").intValue = recommended;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        [SetUp]
        public void SetUp()
        {
            PlayerProfile.StorageOverride = new InMemorySave();
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
            _catalog = MakeCatalog(5);
        }

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null) Object.DestroyImmediate(_catalog);
            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
        }

        static void Clear(params int[] stages)
        {
            foreach (int s in stages) PlayerProfile.MarkLevelCompleted($"level.{s}");
        }

        // ---- Fresh profile -----------------------------------------------------------------

        [Test]
        public void FreshProfile_OpensOnStageOne()
        {
            Assert.AreEqual(0, CampaignSelection.ResolveInitialIndex(_catalog, "", 0));
            Assert.AreEqual(0, CampaignSelection.HighestPlayableIndex(_catalog, 0));
        }

        [Test]
        public void FreshProfile_StageTwoIsLocked()
        {
            Assert.AreEqual(CampaignSelection.DotState.Selected,
                CampaignSelection.StateFor(_catalog, 0, 0, 0));
            Assert.AreEqual(CampaignSelection.DotState.Locked,
                CampaignSelection.StateFor(_catalog, 1, 0, 0));
        }

        // ---- Navigation --------------------------------------------------------------------

        [Test]
        public void RightArrow_RefusesToEnterALockedStage()
        {
            Assert.AreEqual(0, CampaignSelection.Step(_catalog, 0, 1, 0),
                "stage 2 is locked on a fresh profile - the press must do nothing");
            Assert.IsFalse(CampaignSelection.CanStep(_catalog, 0, 1, 0));
        }

        [Test]
        public void ArrowsMoveExactlyOneIndex()
        {
            Clear(1, 2, 3);
            Assert.AreEqual(1, CampaignSelection.Step(_catalog, 0, 1, 0));
            Assert.AreEqual(2, CampaignSelection.Step(_catalog, 1, 1, 0));
            Assert.AreEqual(1, CampaignSelection.Step(_catalog, 2, -1, 0));
        }

        [Test]
        public void NoWrapAtEitherBoundary()
        {
            Clear(1, 2, 3, 4, 5);
            Assert.AreEqual(0, CampaignSelection.Step(_catalog, 0, -1, 0), "left of stage 1 must not wrap");
            Assert.AreEqual(4, CampaignSelection.Step(_catalog, 4, 1, 0), "right of the last stage must not wrap");
            Assert.IsFalse(CampaignSelection.CanStep(_catalog, 0, -1, 0));
            Assert.IsFalse(CampaignSelection.CanStep(_catalog, 4, 1, 0));
        }

        [Test]
        public void ClearingStageN_UnlocksExactlyStageNPlusOne()
        {
            Clear(1);
            Assert.AreEqual(1, CampaignSelection.HighestPlayableIndex(_catalog, 0));
            Assert.AreEqual(CampaignSelection.DotState.Locked,
                CampaignSelection.StateFor(_catalog, 2, 0, 0), "stage 3 must stay locked");
        }

        [Test]
        public void CompletedStageReadsAsCompleted_WhenNotSelected()
        {
            Clear(1);
            Assert.AreEqual(CampaignSelection.DotState.Completed,
                CampaignSelection.StateFor(_catalog, 0, selectedIndex: 1, playerPower: 0));
        }

        // ---- Saved-selection fallback ------------------------------------------------------

        [Test]
        public void ValidSavedSelection_IsRestored()
        {
            Clear(1, 2);
            Assert.AreEqual(2, CampaignSelection.ResolveInitialIndex(_catalog, "level.3", 0));
        }

        [Test]
        public void SavedSelectionThatIsNowLocked_FallsBackToFurthestEarned()
        {
            // Profile wiped, save still names stage 4.
            Assert.AreEqual(0, CampaignSelection.ResolveInitialIndex(_catalog, "level.4", 0));
        }

        [Test]
        public void SavedIdThatNoLongerExists_FallsBackDeterministically()
        {
            Clear(1);
            Assert.AreEqual(1, CampaignSelection.ResolveInitialIndex(_catalog, "level.removed", 0),
                "an unknown id must fall back to the furthest earned stage, not to nothing");
        }

        [Test]
        public void EmptyOrNullSavedId_FallsBackToStageOne()
        {
            Assert.AreEqual(0, CampaignSelection.ResolveInitialIndex(_catalog, "", 0));
            Assert.AreEqual(0, CampaignSelection.ResolveInitialIndex(_catalog, null, 0));
        }

        // ---- Degenerate catalogs -----------------------------------------------------------

        [Test]
        public void NullCatalog_ReportsNoSelectionWithoutThrowing()
        {
            Assert.AreEqual(CampaignSelection.NoSelection, CampaignSelection.ResolveInitialIndex(null, "level.1", 0));
            Assert.AreEqual(CampaignSelection.NoSelection, CampaignSelection.Step(null, 0, 1, 0));
            Assert.AreEqual(CampaignSelection.NoSelection, CampaignSelection.HighestPlayableIndex(null, 0));
            Assert.AreEqual(CampaignSelection.NoSelection, CampaignSelection.Revalidate(null, 0, 0));
        }

        [Test]
        public void EmptyCatalog_ReportsNoSelection()
        {
            var empty = MakeCatalog(0);
            try
            {
                Assert.AreEqual(CampaignSelection.NoSelection, CampaignSelection.ResolveInitialIndex(empty, "level.1", 0));
                Assert.AreEqual(CampaignSelection.NoSelection, CampaignSelection.HighestPlayableIndex(empty, 0));
            }
            finally { Object.DestroyImmediate(empty); }
        }

        [Test]
        public void SingleStageCatalog_HasNoUsableArrows()
        {
            var one = MakeCatalog(1);
            try
            {
                Assert.AreEqual(0, CampaignSelection.ResolveInitialIndex(one, "", 0));
                Assert.IsFalse(CampaignSelection.CanStep(one, 0, 1, 0));
                Assert.IsFalse(CampaignSelection.CanStep(one, 0, -1, 0));
            }
            finally { Object.DestroyImmediate(one); }
        }

        [Test]
        public void Revalidate_ClampsASelectionThatBecameInvalid()
        {
            Clear(1, 2);
            Assert.AreEqual(2, CampaignSelection.Revalidate(_catalog, 2, 0), "still valid, keep it");
            Assert.AreEqual(2, CampaignSelection.Revalidate(_catalog, 4, 0), "index 4 is locked - clamp back");
            Assert.AreEqual(2, CampaignSelection.Revalidate(_catalog, 99, 0), "out of range - clamp back");
        }

        // ---- Power is advisory -------------------------------------------------------------

        [Test]
        public void UnderRecommendedPower_StillSelectableAndPlayable()
        {
            var demanding = MakeCatalog(2, recommended: 5000);
            try
            {
                PlayerProfile.MarkLevelCompleted("level.1");
                var gate = demanding.Evaluate(1, 10);

                Assert.IsTrue(gate.CanPlay, "power must not block a completed path");
                Assert.IsTrue(gate.HasWarning, "but it must warn");
                Assert.AreEqual(1, CampaignSelection.Step(demanding, 0, 1, 10),
                    "an under-recommended stage is still reachable by the arrow");
                Assert.AreEqual(1, CampaignSelection.HighestPlayableIndex(demanding, 10));
            }
            finally { Object.DestroyImmediate(demanding); }
        }

        [Test]
        public void ZeroRecommendedPower_NeverWarns()
        {
            Clear(1);
            var gate = _catalog.Evaluate(1, 0);
            Assert.IsTrue(gate.CanPlay);
            Assert.IsFalse(gate.HasWarning, "an unauthored recommendation must not produce noise");
        }
    }
}
