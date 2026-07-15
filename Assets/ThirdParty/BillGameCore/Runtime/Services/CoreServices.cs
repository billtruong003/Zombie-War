using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BillGameCore
{
    // -------------------------------------------------------
    // SaveService
    // -------------------------------------------------------

    public class SaveService : ISaveService, IInitializable, IDisposableService
    {
        private int _slot;
        string K(string key) => $"s{_slot}_{key}";

        public void Initialize() { }

        public void Set(string key, string val) => PlayerPrefs.SetString(K(key), val);
        public void Set(string key, int val) => PlayerPrefs.SetString(K(key), val.ToString());
        public void Set(string key, float val) => PlayerPrefs.SetString(K(key), val.ToString());
        public void Set(string key, bool val) => PlayerPrefs.SetString(K(key), val ? "1" : "0");
        public void Set<T>(string key, T val) where T : class => PlayerPrefs.SetString(K(key), JsonUtility.ToJson(val));

        public string GetString(string key, string fb = "") => PlayerPrefs.GetString(K(key), fb);
        public int GetInt(string key, int fb = 0) { var s = PlayerPrefs.GetString(K(key), null); return s != null && int.TryParse(s, out var v) ? v : fb; }
        public float GetFloat(string key, float fb = 0f) { var s = PlayerPrefs.GetString(K(key), null); return s != null && float.TryParse(s, out var v) ? v : fb; }
        public bool GetBool(string key, bool fb = false) { var s = PlayerPrefs.GetString(K(key), null); return s != null ? s == "1" : fb; }
        public T Get<T>(string key) where T : class { var j = PlayerPrefs.GetString(K(key), null); if (string.IsNullOrEmpty(j)) return null; try { return JsonUtility.FromJson<T>(j); } catch { return null; } }
        public bool Has(string key) => PlayerPrefs.HasKey(K(key));
        public void Delete(string key) => PlayerPrefs.DeleteKey(K(key));
        public void SetSlot(int slot) => _slot = Mathf.Max(0, slot);
        public void Flush() => PlayerPrefs.Save();
        public void Cleanup() => Flush();
    }

    // -------------------------------------------------------
    // ConfigService
    // -------------------------------------------------------

    public class ConfigService : IConfigService, IInitializable
    {
        private readonly Dictionary<string, string> _local = new(32);
        private readonly Dictionary<string, string> _remote = new(32);

        public void Initialize()
        {
            foreach (var cfg in Resources.LoadAll<GameConfigAsset>("Configs"))
                if (cfg.entries != null)
                    foreach (var e in cfg.entries)
                        if (!string.IsNullOrEmpty(e.key)) _local[e.key] = e.value;
        }

        public string Get(string key, string fb = "")
        {
            if (_remote.TryGetValue(key, out var r)) return r;
            if (_local.TryGetValue(key, out var l)) return l;
            return fb;
        }

        public int GetInt(string key, int fb = 0) { var s = Get(key, null); return s != null && int.TryParse(s, out var v) ? v : fb; }
        public float GetFloat(string key, float fb = 0f) { var s = Get(key, null); return s != null && float.TryParse(s, out var v) ? v : fb; }
        public bool GetBool(string key, bool fb = false) { var s = Get(key, null); return s == null ? fb : s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase); }
        public void Set(string key, string val) => _local[key] = val;
        public bool Has(string key) => _remote.ContainsKey(key) || _local.ContainsKey(key);

        public void ApplyRemote(Dictionary<string, string> data)
        {
            _remote.Clear();
            if (data != null) foreach (var kv in data) _remote[kv.Key] = kv.Value;
            Bill.Events?.Fire<ConfigRefreshedEvent>();
        }
    }

    // -------------------------------------------------------
    // UIService + BasePanel
    // -------------------------------------------------------

    public abstract class BasePanel
    {
        public VisualElement Root { get; private set; }
        public bool IsVisible => Root?.style.display == DisplayStyle.Flex;
        protected abstract void Build(VisualElement root);
        public virtual void OnOpened() { }
        public virtual void OnClosed() { }

        internal void Init()
        {
            Root = new VisualElement();
            Root.style.position = Position.Absolute;
            Root.style.left = Root.style.right = Root.style.top = Root.style.bottom = 0;
            Build(Root);
        }

        internal void Show() { Root.style.display = DisplayStyle.Flex; OnOpened(); }
        internal void Hide() { Root.style.display = DisplayStyle.None; OnClosed(); }
    }

    public class UIService : IUIService, IInitializable, IDisposableService
    {
        private UIDocument _doc;
        private VisualElement _uiRoot;
        private readonly Dictionary<Type, BasePanel> _panels = new(8);

        public void Initialize()
        {
            var go = new GameObject("[Bill.UI]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _doc = go.AddComponent<UIDocument>();
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            _doc.panelSettings = ps;
            _uiRoot = _doc.rootVisualElement;
            _uiRoot.style.position = Position.Absolute;
            _uiRoot.style.left = _uiRoot.style.right = _uiRoot.style.top = _uiRoot.style.bottom = 0;
            _uiRoot.pickingMode = PickingMode.Ignore;
        }

        public T Open<T>() where T : BasePanel, new() { var p = GetOrCreate<T>(); p.Show(); return p; }
        public T Open<T>(Action<T> setup) where T : BasePanel, new() { var p = GetOrCreate<T>(); setup?.Invoke(p); p.Show(); return p; }
        public void Close<T>() where T : BasePanel { if (_panels.TryGetValue(typeof(T), out var p) && p.IsVisible) p.Hide(); }
        public void CloseAll() { foreach (var p in _panels.Values) if (p.IsVisible) p.Hide(); }
        public void Toggle<T>() where T : BasePanel, new() { if (IsOpen<T>()) Close<T>(); else Open<T>(); }
        public bool IsOpen<T>() where T : BasePanel => _panels.TryGetValue(typeof(T), out var p) && p.IsVisible;
        public bool AnyOpen() { foreach (var p in _panels.Values) if (p.IsVisible) return true; return false; }

        T GetOrCreate<T>() where T : BasePanel, new()
        {
            if (_panels.TryGetValue(typeof(T), out var ex)) return (T)ex;
            var panel = new T();
            panel.Init();
            panel.Root.style.display = DisplayStyle.None;
            _uiRoot.Add(panel.Root);
            _panels[typeof(T)] = panel;
            return panel;
        }

        public void Cleanup() { CloseAll(); _panels.Clear(); if (_doc) UnityEngine.Object.Destroy(_doc.gameObject); }
    }
}
