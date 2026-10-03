#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace BillGameCore.BillSceneSwitcher
{
    /// <summary>
    /// Every scene in the project, found once (FindAssets t:Scene) and cached for the editor session.
    /// The cache is dropped only when a .unity file is imported, deleted or moved, so opening the
    /// switcher never scans the project again.
    /// </summary>
    [InitializeOnLoad]
    public static class BillSceneIndex
    {
        // Scan once right after the editor (or a script reload) settles, so the first open is instant.
        static BillSceneIndex() => EditorApplication.delayCall += () => { if (_scenes == null) Build(); };

        public sealed class Scene
        {
            public string Path, Name, Guid, Folder;
            public bool IsProject;
        }

        static List<Scene> _scenes;
        static double _lastBuildMs;

        public static event Action Changed;

        public static IReadOnlyList<Scene> All
        {
            get
            {
                if (_scenes == null) Build();
                return _scenes;
            }
        }

        public static double LastBuildMs => _lastBuildMs;

        public static void Invalidate()
        {
            _scenes = null;
            Changed?.Invoke();
        }

        /// <summary>Re-reads the "project folders" pref without rescanning the asset database.</summary>
        public static void RefreshOwnership()
        {
            if (_scenes == null) return;
            var roots = BillSceneSwitcherPrefs.ProjectFolderList;
            foreach (var s in _scenes) s.IsProject = IsUnder(s.Path, roots);
            Changed?.Invoke();
        }

        static void Build()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var roots = BillSceneSwitcherPrefs.ProjectFolderList;
            var excluded = BillSceneSwitcherPrefs.ExcludedFolderList;
            var list = new List<Scene>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsUnder(path, excluded)) continue;
                list.Add(new Scene
                {
                    Path = path,
                    Guid = guid,
                    Name = System.IO.Path.GetFileNameWithoutExtension(path),
                    Folder = TrimAssets(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') ?? ""),
                    IsProject = IsUnder(path, roots),
                });
            }
            list.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
            _scenes = list;
            _lastBuildMs = watch.Elapsed.TotalMilliseconds;
        }

        static bool IsUnder(string path, IReadOnlyList<string> roots)
        {
            foreach (var r in roots)
                if (path.StartsWith(r.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string TrimAssets(string folder) => folder.StartsWith("Assets/") ? folder.Substring(7) : folder == "Assets" ? "" : folder;

        /// <summary>Drops the cache when scenes appear, disappear or move.</summary>
        sealed class Watcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (_scenes == null) return;
                if (AnyScene(imported) || AnyScene(deleted) || AnyScene(moved) || AnyScene(movedFrom)) Invalidate();
            }

            static bool AnyScene(string[] paths)
            {
                foreach (var p in paths)
                    if (p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }
    }
}
#endif
