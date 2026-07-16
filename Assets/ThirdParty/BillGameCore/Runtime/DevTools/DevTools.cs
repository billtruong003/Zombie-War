#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace BillGameCore
{
    // -------------------------------------------------------
    // DebugOverlay
    // -------------------------------------------------------

    public class DebugOverlay : IService, IInitializable, ITickable, IDisposableService
    {
        private UIDocument _doc;
        private VisualElement _panel;
        private Button _tab;
        private bool _open;
        private Label _fps, _frameMs, _gcAlloc, _totalRam, _texRam, _meshRam, _drawCalls, _tris, _batches;
        private ProfilerRecorder _rGC, _rMem, _rTex, _rMesh, _rDC, _rTri, _rBatch;
        private int _fCount; private float _fTimer, _curFps, _curMs, _uTimer;

        public void Initialize()
        {
            var go = new GameObject("[Bill.DebugOverlay]"); UnityEngine.Object.DontDestroyOnLoad(go);
            _doc = go.AddComponent<UIDocument>();
            var ps = ScriptableObject.CreateInstance<PanelSettings>(); ps.scaleMode = PanelScaleMode.ConstantPixelSize; ps.sortingOrder = 9999;
            _doc.panelSettings = ps;
            var root = _doc.rootVisualElement; root.pickingMode = PickingMode.Ignore;

            _tab = new Button(() => SetOpen(!_open)) { text = "> Stats" };
            Style(_tab, 11, new Color(0.08f, 0.08f, 0.08f, 0.85f), new Color(0.4f, 0.9f, 0.4f));
            _tab.style.position = Position.Absolute; _tab.style.left = 6; _tab.style.top = 6;
            root.Add(_tab);

            _panel = new VisualElement();
            _panel.style.position = Position.Absolute; _panel.style.left = 6; _panel.style.top = 32; _panel.style.width = 230;
            _panel.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.92f);
            _panel.style.borderTopLeftRadius = _panel.style.borderTopRightRadius = _panel.style.borderBottomLeftRadius = _panel.style.borderBottomRightRadius = 6;
            _panel.style.paddingTop = 6; _panel.style.paddingBottom = 8; _panel.style.paddingLeft = _panel.style.paddingRight = 10;
            _panel.style.display = DisplayStyle.None;
            root.Add(_panel);

            Header("PERFORMANCE");
            _fps = Row("FPS"); _frameMs = Row("Frame");
            Header("MEMORY");
            _gcAlloc = Row("GC Alloc"); _totalRam = Row("Total RAM"); _texRam = Row("Textures"); _meshRam = Row("Meshes");
            Header("RENDERING");
            _drawCalls = Row("Draw Calls"); _batches = Row("Batches"); _tris = Row("Triangles");

            _rGC = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _rMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            _rTex = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
            _rMesh = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory");
            _rDC = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _rTri = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _rBatch = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");

            if (BillBootstrapConfig.Instance?.showOverlayOnStartup == true) SetOpen(true);
        }

        void Header(string t) { var l = new Label(t); l.style.fontSize = 9; l.style.color = new Color(.5f,.5f,.5f); l.style.marginTop = 6; l.style.letterSpacing = 1.5f; _panel.Add(l); }
        Label Row(string name)
        {
            var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; r.style.justifyContent = Justify.SpaceBetween;
            var n = new Label(name); n.style.fontSize = 11; n.style.color = new Color(.7f,.7f,.7f);
            var v = new Label("-"); v.style.fontSize = 11; v.style.color = Color.white; v.style.unityFontStyleAndWeight = FontStyle.Bold; v.style.unityTextAlign = TextAnchor.MiddleRight; v.style.minWidth = 70;
            r.Add(n); r.Add(v); _panel.Add(r); return v;
        }

        void Style(VisualElement e, int fs, Color bg, Color fg)
        {
            e.style.backgroundColor = bg; e.style.color = fg; e.style.fontSize = fs;
            e.style.paddingLeft = e.style.paddingRight = 8; e.style.paddingTop = e.style.paddingBottom = 3;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 4;
        }

        public void SetOpen(bool v) { _open = v; _panel.style.display = v ? DisplayStyle.Flex : DisplayStyle.None; }
        public void Toggle() => SetOpen(!_open);
        public bool IsOpen => _open;

        static string Bytes(long b) => b < 1024 ? $"{b}B" : b < 1048576 ? $"{b / 1024f:F1}KB" : $"{b / 1048576f:F1}MB";

        public void Tick(float dt)
        {
            _fCount++; _fTimer += Time.unscaledDeltaTime;
            if (_fTimer >= 0.5f) { _curFps = _fCount / _fTimer; _curMs = _fTimer / _fCount * 1000f; _fCount = 0; _fTimer = 0; _tab.text = _open ? $"< {_curFps:F0}fps" : $"> {_curFps:F0}fps"; }
            if (!_open) return;
            _uTimer += Time.unscaledDeltaTime; if (_uTimer < 0.25f) return; _uTimer = 0;

            _fps.text = $"{_curFps:F0}"; _fps.style.color = _curFps >= 55 ? new Color(.4f,1,.4f) : _curFps >= 30 ? new Color(1,.9f,.3f) : new Color(1,.3f,.3f);
            _frameMs.text = $"{_curMs:F1}ms";
            _gcAlloc.text = Bytes(_rGC.LastValue); _gcAlloc.style.color = _rGC.LastValue > 0 ? new Color(1,.6f,.3f) : Color.white;
            _totalRam.text = Bytes(_rMem.LastValue); _texRam.text = Bytes(_rTex.LastValue); _meshRam.text = Bytes(_rMesh.LastValue);
            _drawCalls.text = _rDC.Valid ? $"{_rDC.LastValue}" : "-"; _batches.text = _rBatch.Valid ? $"{_rBatch.LastValue}" : "-"; _tris.text = _rTri.Valid ? $"{_rTri.LastValue:N0}" : "-";
        }

        public void Cleanup() { _rGC.Dispose(); _rMem.Dispose(); _rTex.Dispose(); _rMesh.Dispose(); _rDC.Dispose(); _rTri.Dispose(); _rBatch.Dispose(); if (_doc) UnityEngine.Object.Destroy(_doc.gameObject); }
    }

    // -------------------------------------------------------
    // CheatConsole
    // -------------------------------------------------------

    public class CheatConsole : IService, IInitializable, ITickable, IDisposableService
    {
        private readonly Dictionary<string, CheatCmd> _cmds = new(32);
        private UIDocument _doc; private VisualElement _panel; private TextField _input; private Label _output; private ScrollView _scroll;
        private bool _visible;
        public bool IsVisible => _visible;

        public void Initialize() { RegisterDefaults(); BuildUI(); SetVisible(false); }

        public void Register(string name, Action action, string help = "") => _cmds[name.ToLower()] = new CheatCmd(name, _ => action(), help);
        public void Register<T>(string name, Action<T> action, string help = "") => _cmds[name.ToLower()] = new CheatCmd(name, a => { if (a.Length < 1) return; action(Parse<T>(a[0])); }, help, typeof(T));
        public void Register<T1, T2>(string name, Action<T1, T2> action, string help = "") => _cmds[name.ToLower()] = new CheatCmd(name, a => { if (a.Length < 2) return; action(Parse<T1>(a[0]), Parse<T2>(a[1])); }, help, typeof(T1), typeof(T2));

        public void Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            Log($"> {input}");
            var p = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!_cmds.TryGetValue(p[0].ToLower(), out var cmd)) { Log($"Unknown: {p[0]}. Type 'help'."); return; }
            try { cmd.Fn(p.Length > 1 ? p[1..] : Array.Empty<string>()); Log($"OK: {p[0]}"); }
            catch (Exception e) { Log($"Error: {e.Message}"); }
        }

        void RegisterDefaults()
        {
            Register("help", () => { var sb = new StringBuilder(); foreach (var c in _cmds.Values.OrderBy(x => x.Name)) sb.AppendLine($"  {c.Name} {string.Join(" ", c.Params.Select(t => $"<{t.Name}>"))}  {c.Help}"); Log(sb.ToString()); }, "List commands");
            Register("clear", () => { if (_output != null) _output.text = ""; }, "Clear output");
            Register<float>("timescale", s => Time.timeScale = Mathf.Clamp(s, 0, 10), "Set time scale");
            Register<int>("fps", t => Application.targetFrameRate = t, "Set target FPS");
            Register("gc", () => GC.Collect(), "Force GC");
            Register<string>("scene", n => Bill.Scene.Load(n), "Load scene");
            Register("reload", () => Bill.Scene.Reload(), "Reload scene");
            Register("pool", () => Debug.Log(Bill.Pool.GetStats()), "Pool stats");
            Register("services", () => Debug.Log(ServiceLocator.GetDependencyReport()), "Service info");
            Register("quit", () => { Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }, "Quit");
        }

        void BuildUI()
        {
            var go = new GameObject("[Bill.CheatConsole]"); UnityEngine.Object.DontDestroyOnLoad(go);
            _doc = go.AddComponent<UIDocument>();
            var ps = ScriptableObject.CreateInstance<PanelSettings>(); ps.scaleMode = PanelScaleMode.ConstantPixelSize; ps.sortingOrder = 9998;
            _doc.panelSettings = ps;
            _doc.rootVisualElement.pickingMode = PickingMode.Ignore;

            _panel = new VisualElement();
            _panel.style.position = Position.Absolute; _panel.style.left = _panel.style.right = _panel.style.bottom = 0; _panel.style.height = 200;
            _panel.style.backgroundColor = new Color(0.03f, 0.03f, 0.05f, 0.95f);
            _panel.style.borderTopWidth = 1; _panel.style.borderTopColor = new Color(.3f,.5f,.3f,.5f);
            _panel.pickingMode = PickingMode.Position;
            _doc.rootVisualElement.Add(_panel);

            _scroll = new ScrollView(ScrollViewMode.Vertical); _scroll.style.flexGrow = 1; _scroll.style.paddingLeft = _scroll.style.paddingRight = 8; _scroll.style.paddingTop = 4;
            _output = new Label(""); _output.style.fontSize = 11; _output.style.color = new Color(.85f,.85f,.85f); _output.style.whiteSpace = WhiteSpace.Normal; _output.enableRichText = true;
            _scroll.Add(_output); _panel.Add(_scroll);

            _input = new TextField(); _input.style.marginLeft = _input.style.marginRight = 8; _input.style.marginBottom = 6; _input.style.marginTop = 4; _input.style.fontSize = 12;
            _input.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
                string cmd = _input.value;
                e.StopPropagation();
                // Clearing the field IN the KeyDown dispatch leaves the runtime text editor's selection indices
                // pointing into the old string — the next typed character then hits DeleteSelection →
                // Substring(startIndex > length) → ArgumentOutOfRangeException. Defer the reset until after the
                // event fully dispatches and normalize the caret explicitly.
                _input.schedule.Execute(() =>
                {
                    _input.SetValueWithoutNotify(string.Empty);
                    _input.SelectAll();   // on an empty string this collapses caret+selection to index 0
                    _input.Focus();
                });
                Execute(cmd);
            });
            _panel.Add(_input);
        }

        void Log(string msg) { if (_output == null) return; _output.text += msg + "\n"; _scroll.schedule.Execute(() => _scroll.scrollOffset = new Vector2(0, float.MaxValue)); }
        public void SetVisible(bool v) { _visible = v; _panel.style.display = v ? DisplayStyle.Flex : DisplayStyle.None; if (v) { _input.Focus(); _input.value = ""; } }
        public void Tick(float dt) { if (Input.GetKeyDown(KeyCode.BackQuote)) SetVisible(!_visible); }

        static T Parse<T>(string s)
        {
            if (typeof(T) == typeof(string)) return (T)(object)s;
            if (typeof(T) == typeof(int)) return (T)(object)int.Parse(s);
            if (typeof(T) == typeof(float)) return (T)(object)float.Parse(s, CultureInfo.InvariantCulture);
            if (typeof(T) == typeof(bool)) return (T)(object)(s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase));
            return (T)Convert.ChangeType(s, typeof(T), CultureInfo.InvariantCulture);
        }

        public void Cleanup() { if (_doc) UnityEngine.Object.Destroy(_doc.gameObject); }

        class CheatCmd { public string Name, Help; public Type[] Params; public Action<string[]> Fn;
            public CheatCmd(string n, Action<string[]> fn, string h, params Type[] p) { Name = n; Fn = fn; Help = h; Params = p; } }
    }

    // -------------------------------------------------------
    // AnalyticsTracker
    // -------------------------------------------------------

    public class AnalyticsTracker : IService, IInitializable, ITickable, IDisposableService
    {
        private readonly List<Snap> _snaps = new(3600);
        private ProfilerRecorder _rMem, _rGC, _rDC, _rTri;
        private float _interval = 1f, _timer, _fTimer, _lastFps; private int _fCount;
        public bool IsRecording { get; set; } = true;
        public int Count => _snaps.Count;

        public void Initialize()
        {
            _rMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            _rGC = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _rDC = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _rTri = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        public void Tick(float dt)
        {
            _fCount++; _fTimer += Time.unscaledDeltaTime;
            if (_fTimer >= 0.5f) { _lastFps = _fCount / _fTimer; _fCount = 0; _fTimer = 0; }
            if (!IsRecording) return;
            _timer += Time.unscaledDeltaTime; if (_timer < _interval) return; _timer -= _interval;
            _snaps.Add(new Snap { T = Time.realtimeSinceStartup, FPS = _lastFps, MemMB = _rMem.Valid ? _rMem.LastValue / 1048576f : 0, GCKB = _rGC.Valid ? _rGC.LastValue / 1024f : 0, DC = _rDC.Valid ? (int)_rDC.LastValue : 0, Tri = _rTri.Valid ? (int)_rTri.LastValue : 0 });
        }

        public string ExportCSV(string name = null)
        {
            if (_snaps.Count == 0) return null;
            name ??= $"perf_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var path = Path.Combine(Application.persistentDataPath, name);
            var sb = new StringBuilder(); sb.AppendLine("Time,FPS,MemMB,GCKB,DrawCalls,Tris");
            foreach (var s in _snaps) sb.AppendLine($"{s.T:F2},{s.FPS:F1},{s.MemMB:F1},{s.GCKB:F1},{s.DC},{s.Tri}");
            File.WriteAllText(path, sb.ToString()); Debug.Log($"[Bill.Analytics] Exported to {path}"); return path;
        }

        public string Summary()
        {
            if (_snaps.Count == 0) return "No data.";
            float min = float.MaxValue, max = 0, avg = 0, maxMem = 0;
            foreach (var s in _snaps) { if (s.FPS < min) min = s.FPS; if (s.FPS > max) max = s.FPS; avg += s.FPS; if (s.MemMB > maxMem) maxMem = s.MemMB; }
            avg /= _snaps.Count;
            return $"Samples: {_snaps.Count} | FPS: {min:F0}/{avg:F0}/{max:F0} | Peak RAM: {maxMem:F0}MB";
        }

        public void Clear() => _snaps.Clear();
        public void SetInterval(float s) => _interval = Mathf.Max(0.1f, s);
        public void Cleanup() { _rMem.Dispose(); _rGC.Dispose(); _rDC.Dispose(); _rTri.Dispose(); }

        [Serializable] struct Snap { public float T, FPS, MemMB, GCKB; public int DC, Tri; }
    }
}
#endif
