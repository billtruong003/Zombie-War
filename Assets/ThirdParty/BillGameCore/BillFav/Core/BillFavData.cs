#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace BillGameCore.BillFav
{
    /// <summary>
    /// Persistent storage for all BillFav data. Lives as an asset in the project.
    /// Each project gets one instance, auto-created on first use.
    /// </summary>
    public class BillFavData : ScriptableObject
    {
        [SerializeField] List<Page> _pages = new();
        [SerializeField] float _rowScale = 1f;
        [SerializeField] int _activePageIndex;

        // ───────────────────────────────────────────
        // Page access
        // ───────────────────────────────────────────

        public List<Page> Pages => _pages;

        public int ActivePageIndex
        {
            get => Mathf.Clamp(_activePageIndex, 0, Mathf.Max(0, _pages.Count - 1));
            set => _activePageIndex = value;
        }

        public Page ActivePage
        {
            get
            {
                EnsurePageExists(ActivePageIndex);
                return _pages[ActivePageIndex];
            }
        }

        public float RowScale
        {
            get => _rowScale;
            set => _rowScale = Mathf.Clamp(value, 0.5f, 2f);
        }

        // ───────────────────────────────────────────
        // Page management
        // ───────────────────────────────────────────

        public Page CreatePage(string name = null)
        {
            var page = new Page(name ?? $"Page {_pages.Count + 1}");
            _pages.Add(page);
            SetDirty();
            return page;
        }

        public void DeletePage(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            if (_pages.Count <= 1) return; // keep at least 1

            _pages.RemoveAt(index);
            if (_activePageIndex >= _pages.Count)
                _activePageIndex = _pages.Count - 1;
            SetDirty();
        }

        public void ReorderPage(int from, int to)
        {
            if (from < 0 || from >= _pages.Count) return;
            if (to < 0 || to >= _pages.Count) return;

            var page = _pages[from];
            _pages.RemoveAt(from);
            _pages.Insert(to, page);

            if (_activePageIndex == from) _activePageIndex = to;
            SetDirty();
        }

        // ───────────────────────────────────────────
        // Item operations (on active page)
        // ───────────────────────────────────────────

        public void AddItem(UnityEngine.Object obj)
        {
            if (obj == null) return;
            var item = new BillFavItem(obj);
            if (ActivePage.Items.Any(i => i.Equals(item))) return; // no duplicates
            ActivePage.Items.Add(item);
            SetDirty();
        }

        public void RemoveItem(int index)
        {
            if (index < 0 || index >= ActivePage.Items.Count) return;
            ActivePage.Items.RemoveAt(index);
            SetDirty();
        }

        public void MoveItem(int from, int to)
        {
            var items = ActivePage.Items;
            if (from < 0 || from >= items.Count) return;
            to = Mathf.Clamp(to, 0, items.Count);

            var item = items[from];
            items.RemoveAt(from);
            if (to > from) to--;
            items.Insert(Mathf.Clamp(to, 0, items.Count), item);
            SetDirty();
        }

        // ───────────────────────────────────────────
        // Persistence
        // ───────────────────────────────────────────

        public void SetDirty()
        {
            EditorUtility.SetDirty(this);
        }

        public void Save()
        {
            SetDirty();
            AssetDatabase.SaveAssetIfDirty(this);
        }

        void EnsurePageExists(int index)
        {
            while (_pages.Count <= index)
                _pages.Add(new Page($"Page {_pages.Count + 1}"));
        }

        // ───────────────────────────────────────────
        // Singleton loader
        // ───────────────────────────────────────────

        const string DefaultPath = "Assets/BillGameCore/BillFavData.asset";

        static BillFavData _instance;

        public static BillFavData Instance
        {
            get
            {
                if (_instance != null) return _instance;

                // Try last known path
                var lastPath = EditorPrefs.GetString("BillFav_DataPath", "");
                if (!string.IsNullOrEmpty(lastPath))
                    _instance = AssetDatabase.LoadAssetAtPath<BillFavData>(lastPath);

                // Search project
                if (_instance == null)
                {
                    var guids = AssetDatabase.FindAssets("t:BillFavData");
                    if (guids.Length > 0)
                        _instance = AssetDatabase.LoadAssetAtPath<BillFavData>(
                            AssetDatabase.GUIDToAssetPath(guids[0]));
                }

                // Create new
                if (_instance == null)
                {
                    _instance = CreateInstance<BillFavData>();
                    _instance._pages.Add(new Page("Favorites"));

                    var dir = System.IO.Path.GetDirectoryName(DefaultPath);
                    if (!System.IO.Directory.Exists(dir))
                        System.IO.Directory.CreateDirectory(dir);

                    AssetDatabase.CreateAsset(_instance, DefaultPath);
                    AssetDatabase.SaveAssets();
                }

                if (_instance != null)
                    EditorPrefs.SetString("BillFav_DataPath", AssetDatabase.GetAssetPath(_instance));

                return _instance;
            }
        }

        // ───────────────────────────────────────────
        // Page class
        // ───────────────────────────────────────────

        [Serializable]
        public class Page
        {
            [SerializeField] string _name;
            [SerializeField] List<BillFavItem> _items = new();
            [SerializeField] int _id;

            public string Name { get => _name; set => _name = value; }
            public List<BillFavItem> Items => _items;
            public int Id { get { if (_id == 0) _id = Guid.NewGuid().GetHashCode(); return _id; } }

            // Transient UI state (not serialized)
            [NonSerialized] public float ScrollPos;

            public Page(string name) { _name = name; }
        }
    }
}
#endif
