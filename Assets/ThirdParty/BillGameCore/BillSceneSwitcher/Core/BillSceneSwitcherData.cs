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
}
#endif
