#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace BillGameCore.BillSceneSwitcher
{
    /// <summary>
    /// Persistent storage for Scene Switcher data.
    /// Stores pinned scenes, bootstrap scene, and recent history.
    /// </summary>
    public class BillSceneSwitcherData : ScriptableObject
    {
        [SerializeField] string _bootstrapSceneGUID = "";
        [SerializeField] List<string> _pinnedSceneGUIDs = new();

        // ───────────────────────────────────────────
        // Bootstrap
        // ───────────────────────────────────────────

        public string BootstrapSceneGUID
        {
            get => _bootstrapSceneGUID;
            set { _bootstrapSceneGUID = value; SetDirty(); }
        }

        public string BootstrapScenePath
        {
            get => string.IsNullOrEmpty(_bootstrapSceneGUID)
                ? "" : AssetDatabase.GUIDToAssetPath(_bootstrapSceneGUID);
        }

        public void SetBootstrapScene(string path)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return;
            _bootstrapSceneGUID = guid;
            ApplyBootstrapToBuildSettings(path);
            SetDirty();
        }

        /// <summary>
        /// Moves the bootstrap scene to build index 0.
        /// </summary>
        void ApplyBootstrapToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FindIndex(s => s.path == path);

            if (existing < 0)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            }
            else if (existing > 0)
            {
                var scene = scenes[existing];
                scenes.RemoveAt(existing);
                scenes.Insert(0, scene);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ───────────────────────────────────────────
        // Pinned scenes
        // ───────────────────────────────────────────

        public List<string> PinnedSceneGUIDs => _pinnedSceneGUIDs;

        public bool IsPinned(string path)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid) && _pinnedSceneGUIDs.Contains(guid);
        }

        public void TogglePin(string path)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return;

            if (_pinnedSceneGUIDs.Contains(guid))
                _pinnedSceneGUIDs.Remove(guid);
            else
                _pinnedSceneGUIDs.Add(guid);
            SetDirty();
        }

        public List<string> GetPinnedScenePaths()
        {
            var paths = new List<string>();
            for (int i = _pinnedSceneGUIDs.Count - 1; i >= 0; i--)
            {
                var path = AssetDatabase.GUIDToAssetPath(_pinnedSceneGUIDs[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity"))
                    _pinnedSceneGUIDs.RemoveAt(i);
                else
                    paths.Insert(0, path);
            }
            return paths;
        }

        /// <summary>Reorders pinned scenes (drag to reorder). Pins missing from <paramref name="paths"/>
        /// (e.g. a pinned bootstrap scene, which the list does not show) keep their place at the end.</summary>
        public void SetPinOrder(IList<string> paths)
        {
            var ordered = paths.Select(AssetDatabase.AssetPathToGUID).Where(g => _pinnedSceneGUIDs.Contains(g)).ToList();
            ordered.AddRange(_pinnedSceneGUIDs.Where(g => !ordered.Contains(g)));
            _pinnedSceneGUIDs.Clear();
            _pinnedSceneGUIDs.AddRange(ordered);
            SetDirty();
        }

        // ───────────────────────────────────────────
        // Recent scenes
        // ───────────────────────────────────────────

        // Recent scenes are per person and per machine, so they live in EditorPrefs (keyed by
        // project), not in this shared asset: opening a scene must not dirty a tracked file.
        const int RecentKept = 12;
        static string RecentKey => "BillSceneSwitcher.Recent." + Application.dataPath.GetHashCode();

        public static void AddRecent(string path)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return;
            var list = EditorPrefs.GetString(RecentKey, "").Split(';').Where(g => g.Length > 0 && g != guid).ToList();
            list.Insert(0, guid);
            EditorPrefs.SetString(RecentKey, string.Join(";", list.Take(RecentKept)));
        }

        public static List<string> GetRecentScenePaths()
        {
            var paths = new List<string>();
            foreach (var guid in EditorPrefs.GetString(RecentKey, "").Split(';'))
            {
                if (guid.Length == 0) continue;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity")) paths.Add(path);
            }
            return paths;
        }

        // ───────────────────────────────────────────
        // Build settings (with one level of undo)
        // ───────────────────────────────────────────

        static EditorBuildSettingsScene[] _undoBuild;
        public static bool CanUndoBuild => _undoBuild != null;

        static void Snapshot() => _undoBuild = EditorBuildSettings.scenes.Select(s => new EditorBuildSettingsScene(s.path, s.enabled)).ToArray();

        public static int AddToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int i = scenes.FindIndex(s => s.path == path);
            if (i >= 0) return i;
            Snapshot();
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            return scenes.Count - 1;
        }

        public static void RemoveFromBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == path)) return;
            Snapshot();
            EditorBuildSettings.scenes = scenes.Where(s => s.path != path).ToArray();
        }

        public static void SetBuildEnabled(string path, bool enabled)
        {
            var scenes = EditorBuildSettings.scenes;
            int i = Array.FindIndex(scenes, s => s.path == path);
            if (i < 0 || scenes[i].enabled == enabled) return;
            Snapshot();
            scenes[i].enabled = enabled;
            EditorBuildSettings.scenes = scenes;
        }

        public static void UndoBuild()
        {
            if (_undoBuild == null) return;
            EditorBuildSettings.scenes = _undoBuild;
            _undoBuild = null;
        }

        // ───────────────────────────────────────────
        // Persistence
        // ───────────────────────────────────────────

        public void SetDirty() => EditorUtility.SetDirty(this);

        public void Save()
        {
            SetDirty();
            AssetDatabase.SaveAssetIfDirty(this);
        }

        // ───────────────────────────────────────────
        // Singleton loader
        // ───────────────────────────────────────────

        const string DefaultPath = "Assets/BillGameCore/BillSceneSwitcherData.asset";
        static BillSceneSwitcherData _instance;

        public static BillSceneSwitcherData Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var lastPath = EditorPrefs.GetString("BillSceneSwitcher_DataPath", "");
                if (!string.IsNullOrEmpty(lastPath))
                    _instance = AssetDatabase.LoadAssetAtPath<BillSceneSwitcherData>(lastPath);

                if (_instance == null)
                {
                    var guids = AssetDatabase.FindAssets("t:BillSceneSwitcherData");
                    if (guids.Length > 0)
                        _instance = AssetDatabase.LoadAssetAtPath<BillSceneSwitcherData>(
                            AssetDatabase.GUIDToAssetPath(guids[0]));
                }

                if (_instance == null)
                {
                    _instance = CreateInstance<BillSceneSwitcherData>();
                    var dir = System.IO.Path.GetDirectoryName(DefaultPath);
                    if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                        System.IO.Directory.CreateDirectory(dir);
                    AssetDatabase.CreateAsset(_instance, DefaultPath);
                    AssetDatabase.SaveAssets();
                }

                if (_instance != null)
                    EditorPrefs.SetString("BillSceneSwitcher_DataPath",
                        AssetDatabase.GetAssetPath(_instance));

                return _instance;
            }
        }
    }

    /// <summary>Feeds the "recent" list: every scene opened in the editor, newest first.</summary>
    [InitializeOnLoad]
    static class BillSceneRecentTracker
    {
        static BillSceneRecentTracker()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, _) =>
            {
                if (!string.IsNullOrEmpty(scene.path)) BillSceneSwitcherData.AddRecent(scene.path);
            };
        }
    }
}
#endif
