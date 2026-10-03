#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BillGameCore.BillSceneSwitcher
{
    /// <summary>
    /// Glassmorphism-styled dropdown panel for the Scene Switcher (v2).
    /// Tab "Trong build": bootstrap, pinned scenes (also out-of-build ones), build settings, recent.
    /// Tab "Ngoài build": every other scene of the project, grouped by folder, filtered by chips.
    /// Search looks through the whole scene index; "Thêm scene" pins a scene or adds it to the build.
    /// </summary>
    public class BillSceneSwitcherDropdown : EditorWindow
    {
        string _search = "";
        string _addSearch = "";
        bool _addMode;
        Vector2 _scroll;
        BillSceneSwitcherData _data;
        double _openTime;
        int _selected;
        bool _menuOpen;
        bool _vendorExpanded;
        bool _scrollToSelected;

        static readonly Dictionary<string, bool> _groupOpen = new();

        // Toast
        string _toast;
        double _toastUntil;
        Action _toastUndo;

        // Drag to reorder pinned
        string _dragPath;
        Vector2 _dragStart;
        bool _dragging;
        float _dropY;

        List<Row> _rows;
        bool _dirty = true;

        // ═══════════════════════════════════════════
        // Glassmorphism Color Palette
        // ═══════════════════════════════════════════

        static bool IsDark => EditorGUIUtility.isProSkin;

        static Color PanelBg => IsDark
            ? new Color(0.11f, 0.11f, 0.14f, 0.97f)
            : new Color(0.94f, 0.94f, 0.96f, 0.97f);
        static Color PanelBorder => IsDark
            ? new Color(1f, 1f, 1f, 0.06f)
            : new Color(0f, 0f, 0f, 0.08f);
        static Color PanelInnerGlow => IsDark
            ? new Color(1f, 1f, 1f, 0.02f)
            : new Color(1f, 1f, 1f, 0.3f);

        static Color RowNormal => IsDark
            ? new Color(1f, 1f, 1f, 0.02f)
            : new Color(0f, 0f, 0f, 0.015f);
        static Color RowHover => IsDark
            ? new Color(1f, 1f, 1f, 0.07f)
            : new Color(0f, 0f, 0f, 0.05f);
        static Color RowActive => IsDark
            ? new Color(1f, 0.55f, 0.2f, 0.14f)
            : new Color(0.9f, 0.45f, 0.1f, 0.1f);
        static Color RowActiveBorder => IsDark
            ? new Color(1f, 0.6f, 0.25f, 0.3f)
            : new Color(0.9f, 0.5f, 0.15f, 0.25f);
        static Color RowSeparator => IsDark
            ? new Color(1f, 1f, 1f, 0.035f)
            : new Color(0f, 0f, 0f, 0.04f);
        static Color RowSelected => IsDark
            ? new Color(0.35f, 0.6f, 1f, 0.16f)
            : new Color(0.2f, 0.45f, 0.9f, 0.12f);

        static Color BootstrapAccent => IsDark
            ? new Color(1f, 0.8f, 0.2f, 0.35f)
            : new Color(0.9f, 0.7f, 0.1f, 0.25f);
        static Color BootstrapBorder => IsDark
            ? new Color(1f, 0.85f, 0.3f, 0.4f)
            : new Color(0.85f, 0.65f, 0.1f, 0.3f);
        static Color PinAccent => IsDark
            ? new Color(1f, 0.55f, 0.2f, 0.25f)
            : new Color(0.9f, 0.45f, 0.1f, 0.15f);
        static Color RecentAccent => IsDark
            ? new Color(0.55f, 0.55f, 0.6f, 0.5f)
            : new Color(0.4f, 0.4f, 0.45f, 0.4f);

        static Color TextPrimary => IsDark
            ? new Color(0.88f, 0.88f, 0.92f)
            : new Color(0.12f, 0.12f, 0.16f);
        static Color TextSecondary => IsDark
            ? new Color(0.5f, 0.5f, 0.56f)
            : new Color(0.45f, 0.45f, 0.5f);
        static Color TextPath => IsDark
            ? new Color(0.42f, 0.42f, 0.5f, 0.7f)
            : new Color(0.4f, 0.4f, 0.5f, 0.6f);
        static Color Accent => IsDark
            ? new Color(1f, 0.65f, 0.3f)
            : new Color(0.85f, 0.45f, 0.1f);

        static Color SectionHeader => IsDark
            ? new Color(0.55f, 0.55f, 0.6f)
            : new Color(0.4f, 0.4f, 0.45f);
        static Color SearchBg => IsDark
            ? new Color(0.08f, 0.08f, 0.1f, 0.7f)
            : new Color(1f, 1f, 1f, 0.5f);
        static Color SearchBorder => IsDark
            ? new Color(1f, 1f, 1f, 0.08f)
            : new Color(0f, 0f, 0f, 0.1f);

        static Color BtnBg => IsDark
            ? new Color(1f, 1f, 1f, 0.06f)
            : new Color(0f, 0f, 0f, 0.04f);
        static Color BtnHover => IsDark
            ? new Color(1f, 1f, 1f, 0.12f)
            : new Color(0f, 0f, 0f, 0.08f);
        static Color BtnActiveBg => IsDark
            ? new Color(1f, 0.55f, 0.15f, 0.25f)
            : new Color(0.9f, 0.45f, 0.1f, 0.2f);
        static Color BuildAccent => IsDark
            ? new Color(0.47f, 0.75f, 1f, 0.18f)
            : new Color(0.2f, 0.45f, 0.85f, 0.14f);
        static Color ChipBuild => IsDark ? new Color(0.56f, 0.78f, 1f) : new Color(0.15f, 0.4f, 0.75f);
        static Color ChipOut => IsDark ? new Color(0.66f, 0.66f, 0.71f) : new Color(0.4f, 0.4f, 0.45f);
        static Color ChipLoaded => IsDark ? new Color(0.56f, 0.88f, 0.6f) : new Color(0.15f, 0.55f, 0.2f);
        static Color ToastBg => IsDark ? new Color(0.16f, 0.24f, 0.17f, 0.97f) : new Color(0.86f, 0.95f, 0.87f, 0.97f);

        // ═══════════════════════════════════════════
        // GUIStyle cache
        // ═══════════════════════════════════════════

        static GUIStyle _nameStyle, _pathStyle, _sectionStyle, _searchStyle;
        static GUIStyle _indexStyle, _emptyStyle, _footerBtnStyle, _chipStyle, _tabStyle, _smallStyle, _titleStyle;

        static GUIStyle NameStyle => _nameStyle ??= new GUIStyle(EditorStyles.label)
            { fontSize = 12, alignment = TextAnchor.MiddleLeft, richText = true, clipping = TextClipping.Clip };
        static GUIStyle PathStyle => _pathStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 9, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        static GUIStyle SectionStyle => _sectionStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 9, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        static GUIStyle SearchStyle => _searchStyle ??= new GUIStyle(EditorStyles.toolbarSearchField)
            { fontSize = 12 };
        static GUIStyle IndexStyle => _indexStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 9, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        static GUIStyle EmptyStyle => _emptyStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            { fontSize = 11, wordWrap = true };
        static GUIStyle FooterBtnStyle => _footerBtnStyle ??= new GUIStyle(EditorStyles.miniButton)
            { fontSize = 11, fixedHeight = 24 };
        static GUIStyle ChipStyle => _chipStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 8, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(4, 4, 0, 0) };
        static GUIStyle TabStyle => _tabStyle ??= new GUIStyle(EditorStyles.label)
            { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = true };
        static GUIStyle SmallStyle => _smallStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 9, alignment = TextAnchor.MiddleRight, richText = true };
        static GUIStyle TitleStyle => _titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 13, alignment = TextAnchor.MiddleLeft };

        // ═══════════════════════════════════════════
        // Rows
        // ═══════════════════════════════════════════

        enum Kind { Section, Group, Scene, HiddenVendor, Empty }
        enum Chip { None, Build, Out, Loaded }

        sealed class Row
        {
            public Kind Kind;
            public string Text, Right, Folder;
            public Color Accent;
            public bool Open;
            // Scene
            public string Name, Path, Highlight;
            public int BuildIndex = -1;
            public bool Enabled = true, IsActive, IsLoaded, IsBootstrap, IsPinned, InBuild, InPinnedSection, AddRow;
            public Chip Chip;
            public float Y, H;
        }

        const float RowH = 42f, RowHCompact = 28f, SectionH = 22f, GroupH = 24f;
        float SceneRowH => BillSceneSwitcherPrefs.ShowScenePath ? RowH : RowHCompact;

        // ═══════════════════════════════════════════
        // Show / lifecycle
        // ═══════════════════════════════════════════

        public static void Show(Rect buttonScreenRect)
        {
            var window = CreateInstance<BillSceneSwitcherDropdown>();
            window.titleContent = new GUIContent("Scene Switcher");
            window.wantsMouseMove = true;
            window.ShowAsDropDown(buttonScreenRect, new Vector2(440, 560));
        }

        void OnEnable()
        {
            _data = BillSceneSwitcherData.Instance;
            _openTime = EditorApplication.timeSinceStartup;
            BillSceneIndex.Changed += MarkDirty;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        void OnDisable()
        {
            BillSceneIndex.Changed -= MarkDirty;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
        }

        void OnSceneOpened(UnityEngine.SceneManagement.Scene s, OpenSceneMode m) => MarkDirty();
        void OnSceneClosed(UnityEngine.SceneManagement.Scene s) => MarkDirty();

        void OnFocus() => _menuOpen = false;

        void OnLostFocus()
        {
            if (_menuOpen) return;
            // Delay close slightly to allow button clicks to register
            EditorApplication.delayCall += () =>
            {
                if (this != null && !_menuOpen) Close();
            };
        }

        void OnProjectChange() => MarkDirty();

        void MarkDirty()
        {
            _dirty = true;
            Repaint();
        }

        int Tab
        {
            get => Mathf.Clamp(BillSceneSwitcherPrefs.LastTab, 0, 1);
            set => BillSceneSwitcherPrefs.LastTab = value;
        }

        // ═══════════════════════════════════════════
        // Row building
        // ═══════════════════════════════════════════

        struct Status { public HashSet<string> Loaded; public string Active, Bootstrap; public Dictionary<string, int> BuildIndex; public Dictionary<string, bool> BuildEnabled; }

        Status ReadStatus()
        {
            var st = new Status
            {
                Loaded = new HashSet<string>(),
                Active = EditorSceneManager.GetActiveScene().path,
                BuildIndex = new Dictionary<string, int>(),
                BuildEnabled = new Dictionary<string, bool>(),
            };
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) st.Loaded.Add(s.path);
            }
            var build = EditorBuildSettings.scenes;
            for (int i = 0; i < build.Length; i++)
            {
                if (st.BuildIndex.ContainsKey(build[i].path)) continue;
                st.BuildIndex[build[i].path] = i;
                st.BuildEnabled[build[i].path] = build[i].enabled;
            }
            st.Bootstrap = _data?.BootstrapScenePath ?? "";
            if (string.IsNullOrEmpty(st.Bootstrap) && build.Length > 0) st.Bootstrap = build[0].path;
            return st;
        }

        Row SceneRow(string path, Status st, string highlight = null, bool chip = false)
        {
            st.BuildIndex.TryGetValue(path, out int idx);
            bool inBuild = st.BuildIndex.ContainsKey(path);
            bool loaded = st.Loaded.Contains(path);
            bool active = path == st.Active;
            var r = new Row
            {
                Kind = Kind.Scene,
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path,
                Highlight = highlight,
                BuildIndex = inBuild ? idx : -1,
                Enabled = !inBuild || st.BuildEnabled[path],
                IsActive = active,
                IsLoaded = loaded,
                IsBootstrap = path == st.Bootstrap,
                IsPinned = _data != null && _data.IsPinned(path),
                InBuild = inBuild,
            };
            if (loaded && !active) r.Chip = Chip.Loaded;
            else if (chip) r.Chip = inBuild ? Chip.Build : Chip.Out;
            return r;
        }

        static Row Section(string text, Color accent, string right = null) => new Row { Kind = Kind.Section, Text = text, Accent = accent, Right = right };

        void BuildRows()
        {
            _dirty = false;
            var st = ReadStatus();
            var rows = new List<Row>();

            if (_addMode) BuildAddRows(rows, st);
            else if (!string.IsNullOrEmpty(_search)) BuildSearchRows(rows, st, _search);
            else if (Tab == 0) BuildInBuildRows(rows, st);
            else BuildOutOfBuildRows(rows, st);

            float y = 4;
            foreach (var r in rows)
            {
                r.Y = y;
                r.H = r.Kind switch
                {
                    Kind.Scene => SceneRowH,
                    Kind.Group => GroupH,
                    Kind.Empty => 60,
                    _ => SectionH,
                };
                y += r.H + (r.Kind == Kind.Section ? 2 : 0);
            }
            _rows = rows;
            int scenes = rows.Count(r => r.Kind == Kind.Scene);
            _selected = scenes == 0 ? -1 : Mathf.Clamp(_selected, 0, scenes - 1);
        }

        void BuildInBuildRows(List<Row> rows, Status st)
        {
            var shown = new HashSet<string>();
            if (!string.IsNullOrEmpty(st.Bootstrap))
            {
                rows.Add(Section("BOOTSTRAP", BootstrapAccent));
                rows.Add(SceneRow(st.Bootstrap, st));
                shown.Add(st.Bootstrap);
            }

            var pinned = (_data?.GetPinnedScenePaths() ?? new List<string>()).Where(p => !shown.Contains(p)).ToList();
            if (pinned.Count > 0)
            {
                rows.Add(Section("ĐÃ GHIM", PinAccent, "kéo để đổi thứ tự"));
                foreach (var p in pinned)
                {
                    var r = SceneRow(p, st, chip: !st.BuildIndex.ContainsKey(p));
                    r.InPinnedSection = true;
                    rows.Add(r);
                    shown.Add(p);
                }
            }

            var build = EditorBuildSettings.scenes;
            int enabled = build.Count(s => s.enabled);
            var buildRows = build.Select(s => s.path).Where(p => !string.IsNullOrEmpty(p) && !shown.Contains(p) && File.Exists(p)).Distinct().ToList();
            rows.Add(Section($"BUILD SETTINGS ({build.Length})", SectionHeader, $"đang bật {enabled}/{build.Length}"));
            foreach (var p in buildRows)
            {
                rows.Add(SceneRow(p, st));
                shown.Add(p);
            }
            if (build.Length == 0) rows.Add(new Row { Kind = Kind.Empty, Text = "Chưa có scene nào trong Build Settings.\nThêm bằng ＋ Thêm scene hoặc tab Ngoài build." });

            var recent = BillSceneSwitcherData.GetRecentScenePaths().Where(p => !shown.Contains(p)).Take(Mathf.Max(0, BillSceneSwitcherPrefs.RecentCount)).ToList();
            if (recent.Count > 0)
            {
                rows.Add(Section("GẦN ĐÂY", RecentAccent));
                foreach (var p in recent) rows.Add(SceneRow(p, st, chip: !st.BuildIndex.ContainsKey(p)));
            }
        }

        void BuildOutOfBuildRows(List<Row> rows, Status st)
        {
            var list = BillSceneIndex.All.Where(s => !st.BuildIndex.ContainsKey(s.Path) && PassesChips(s, st)).ToList();
            if (list.Count == 0)
            {
                rows.Add(new Row { Kind = Kind.Empty, Text = "Không có scene nào khớp bộ lọc.\nBật thêm chip ở trên." });
                return;
            }
            foreach (var group in list.GroupBy(s => s.Folder))
            {
                bool project = group.First().IsProject;
                bool open = _groupOpen.TryGetValue(group.Key, out var o) ? o : project || BillSceneSwitcherPrefs.FilterLoaded;
                rows.Add(new Row { Kind = Kind.Group, Text = string.IsNullOrEmpty(group.Key) ? "Assets" : group.Key, Folder = group.Key, Open = open, Right = group.Count().ToString() });
                if (!open) continue;
                foreach (var s in group) rows.Add(SceneRow(s.Path, st));
            }
        }

        static bool PassesChips(BillSceneIndex.Scene s, Status st)
        {
            if (BillSceneSwitcherPrefs.FilterLoaded && !st.Loaded.Contains(s.Path)) return false;
            return s.IsProject ? BillSceneSwitcherPrefs.FilterProject : BillSceneSwitcherPrefs.FilterVendor;
        }

        void BuildSearchRows(List<Row> rows, Status st, string query)
        {
            var matches = BillSceneIndex.All.Where(s => Matches(s, query)).ToList();
            var own = matches.Where(s => s.IsProject).OrderBy(s => Rank(s, query)).ToList();
            var vendor = matches.Where(s => !s.IsProject).OrderBy(s => Rank(s, query)).ToList();
            foreach (var s in own) rows.Add(SceneRow(s.Path, st, query, chip: true));
            if (vendor.Count > 0)
            {
                if (_vendorExpanded || BillSceneSwitcherPrefs.FilterVendor)
                {
                    rows.Add(Section($"ASSET MUA · {vendor.Count}", RecentAccent));
                    foreach (var s in vendor) rows.Add(SceneRow(s.Path, st, query, chip: true));
                }
                else rows.Add(new Row { Kind = Kind.HiddenVendor, Text = $"ASSET MUA (ẨN) · {vendor.Count}", Accent = RecentAccent, Right = "bấm để hiện" });
            }
            if (rows.Count == 0) rows.Add(new Row { Kind = Kind.Empty, Text = "Không tìm thấy scene nào." });
        }

        void BuildAddRows(List<Row> rows, Status st)
        {
            var list = BillSceneIndex.All.Where(s => string.IsNullOrEmpty(_addSearch) ? s.IsProject : Matches(s, _addSearch))
                .OrderBy(s => s.IsProject ? 0 : 1).ThenBy(s => Rank(s, _addSearch ?? "")).ToList();
            foreach (var s in list)
            {
                var r = SceneRow(s.Path, st, _addSearch, chip: true);
                r.AddRow = true;
                rows.Add(r);
            }
            if (list.Count == 0) rows.Add(new Row { Kind = Kind.Empty, Text = "Không tìm thấy scene nào." });
        }

        static bool Matches(BillSceneIndex.Scene s, string q) =>
            s.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || s.Path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        static int Rank(BillSceneIndex.Scene s, string q)
        {
            if (string.IsNullOrEmpty(q)) return 0;
            if (s.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 0;
            return s.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 2;
        }

        // ═══════════════════════════════════════════
        // Main OnGUI
        // ═══════════════════════════════════════════

        void OnGUI()
        {
            _data = BillSceneSwitcherData.Instance;
            if (_dirty || _rows == null) BuildRows();
            HandleKeys();

            var rect = new Rect(0, 0, position.width, position.height);
            DrawGlassBackground(rect);
            DrawAccentLine(rect);
            DrawContent(new Rect(0, 2, rect.width, rect.height - 2));

            // Repaint for animations
            if (EditorApplication.timeSinceStartup - _openTime < 0.5f || _toast != null)
                Repaint();

            if (Event.current.type == EventType.MouseMove || _dragging)
                Repaint();
        }

        void HandleKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            var scenes = _rows.Where(r => r.Kind == Kind.Scene).ToList();
            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    if (scenes.Count > 0) { _selected = (_selected + 1) % scenes.Count; _scrollToSelected = true; }
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    if (scenes.Count > 0) { _selected = (_selected - 1 + scenes.Count) % scenes.Count; _scrollToSelected = true; }
                    e.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_selected >= 0 && _selected < scenes.Count && !_addMode) LoadScene(scenes[_selected].Path, e.shift);
                    e.Use();
                    break;
                case KeyCode.Escape:
                    if (_addMode) { _addMode = false; MarkDirty(); }
                    else Close();
                    e.Use();
                    break;
            }
        }

        // ═══════════════════════════════════════════
        // Glass background
        // ═══════════════════════════════════════════

        void DrawGlassBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), PanelInnerGlow);

            // Frosted depth gradient at top
            for (int i = 0; i < 8; i++)
            {
                float a = (1f - i / 8f) * 0.015f;
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + i, rect.width, 1),
                    IsDark ? new Color(1, 1, 1, a) : new Color(0, 0, 0, a * 0.3f));
            }

            DrawBorder(rect, PanelBorder);
        }

        void DrawAccentLine(Rect rect)
        {
            float t = (float)EditorApplication.timeSinceStartup;
            float lineH = 2f;
            int segments = Mathf.Max(1, (int)(rect.width / 3));
            float segW = rect.width / segments;

            for (int i = 0; i < segments; i++)
            {
                float ratio = (float)i / segments;
                float hue = Mathf.Lerp(0.06f, 0.1f, Mathf.PingPong(ratio * 2f + t * 0.06f, 1f));
                float sat = IsDark ? 0.5f : 0.45f;
                float val = IsDark ? 0.85f : 0.7f;
                var c = Color.HSVToRGB(hue, sat, val);
                c.a = IsDark ? 0.35f : 0.25f;
                EditorGUI.DrawRect(new Rect(rect.x + i * segW, rect.y, segW + 1, lineH), c);
            }
        }

        static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        // ═══════════════════════════════════════════
        // Content layout
        // ═══════════════════════════════════════════

        void DrawContent(Rect rect)
        {
            float y = rect.y + 8;
            const float pad = 10;

            if (_addMode)
            {
                GUI.Label(new Rect(pad, y, 120, 20), "Thêm scene", TitleStyle);
                var prev = GUI.color;
                GUI.color = TextSecondary;
                GUI.Label(new Rect(pad + 110, y, rect.width - pad * 2 - 140, 20), $"tìm trong {BillSceneIndex.All.Count} scene đã index", SmallStyle);
                GUI.color = prev;
                if (DrawIconButton(new Rect(rect.width - pad - 22, y - 1, 22, 22), "d_winbtn_win_close", "Đóng (Esc)", BtnBg)) { _addMode = false; MarkDirty(); }
                y += 26;
                string before = _addSearch;
                _addSearch = DrawSearchField(new Rect(pad, y, rect.width - pad * 2, 20), _addSearch, "AddSearch");
                if (before != _addSearch) { _selected = 0; MarkDirty(); }
                y += 28;
            }
            else
            {
                string before = _search;
                _search = DrawSearchField(new Rect(pad, y, rect.width - pad * 2 - 104, 20), _search, "SceneSwitcherSearch");
                if (before != _search) { _selected = 0; _vendorExpanded = false; MarkDirty(); }
                if (GUI.Button(new Rect(rect.width - pad - 96, y - 1, 96, 22), "＋ Thêm scene", FooterBtnStyle))
                {
                    _addMode = true;
                    _addSearch = _search;
                    _selected = 0;
                    _openTime = EditorApplication.timeSinceStartup;
                    MarkDirty();
                }
                y += 28;
                y = DrawTabs(new Rect(pad, y, rect.width - pad * 2, 24));
                if (string.IsNullOrEmpty(_search) && Tab == 1) y = DrawChips(new Rect(pad, y + 4, rect.width - pad * 2, 20));
                else if (!string.IsNullOrEmpty(_search))
                {
                    int n = _rows.Count(r => r.Kind == Kind.Scene);
                    var prev = GUI.color;
                    GUI.color = TextSecondary;
                    GUI.Label(new Rect(pad, y + 2, rect.width - pad * 2, 16),
                        $"Đang tìm trong <b>cả hai tab</b> · {n} kết quả · ↑↓ chọn · Enter mở · Shift+Enter additive", new GUIStyle(SmallStyle) { alignment = TextAnchor.MiddleLeft });
                    GUI.color = prev;
                    y += 20;
                }
                y += 4;
            }

            float footerH = 34;
            var listRect = new Rect(0, y, rect.width, rect.height - y - footerH - 2);
            DrawSceneList(listRect, pad);
            DrawToast(new Rect(pad, listRect.yMax - 30, rect.width - pad * 2, 26));
            DrawFooter(new Rect(0, rect.height - footerH, rect.width, footerH), pad);
        }

        string DrawSearchField(Rect rect, string text, string control)
        {
            // Glass search background
            var bgRect = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
            EditorGUI.DrawRect(bgRect, SearchBg);
            DrawBorder(bgRect, SearchBorder);

            GUI.SetNextControlName(control);
            text = EditorGUI.TextField(rect, text, SearchStyle);

            // Auto-focus search on open
            if (EditorApplication.timeSinceStartup - _openTime < 0.2f)
                EditorGUI.FocusTextInControl(control);
            return text;
        }

        float DrawTabs(Rect rect)
        {
            var status = ReadStatus();
            int outCount = BillSceneIndex.All.Count(s => !status.BuildIndex.ContainsKey(s.Path));
            int inCount = EditorBuildSettings.scenes.Length + (_data?.GetPinnedScenePaths().Count(p => !status.BuildIndex.ContainsKey(p)) ?? 0);
            bool searching = !string.IsNullOrEmpty(_search);

            float x = rect.x;
            x = DrawTab(new Rect(x, rect.y, 120, rect.height), "Trong build", inCount, Tab == 0 && !searching, () => Tab = 0);
            x = DrawTab(new Rect(x + 4, rect.y, 130, rect.height), "Ngoài build", outCount, Tab == 1 && !searching, () => Tab = 1);

            var prev = GUI.color;
            GUI.color = TextSecondary;
            GUI.Label(new Rect(x, rect.y, rect.xMax - x, rect.height),
                $"{BillSceneIndex.All.Count} scene · index {BillSceneIndex.LastBuildMs:0} ms", SmallStyle);
            GUI.color = prev;
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax, rect.width, 1), RowSeparator);
            return rect.yMax + 2;
        }

        float DrawTab(Rect rect, string label, int count, bool on, Action select)
        {
            var e = Event.current;
            bool hover = rect.Contains(e.mousePosition);
            var prev = GUI.color;
            GUI.color = on ? Accent : (hover ? TextPrimary : TextSecondary);
            var content = new GUIContent($"{label}  <size=9>{count}</size>");
            float w = TabStyle.CalcSize(content).x + 14;
            var r = new Rect(rect.x, rect.y, w, rect.height);
            GUI.Label(new Rect(r.x + 6, r.y, r.width, r.height), content, TabStyle);
            GUI.color = prev;
            if (on) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), Accent);
            if (r.Contains(e.mousePosition) && e.type == EventType.MouseDown && e.button == 0)
            {
                if (!string.IsNullOrEmpty(_search)) { _search = ""; GUI.FocusControl(null); }
                select();
                _scroll = Vector2.zero;
                _selected = 0;
                MarkDirty();
                e.Use();
            }
            return r.xMax;
        }

        float DrawChips(Rect rect)
        {
            var status = ReadStatus();
            var all = BillSceneIndex.All;
            int own = all.Count(s => s.IsProject && !status.BuildIndex.ContainsKey(s.Path));
            int vendor = all.Count(s => !s.IsProject && !status.BuildIndex.ContainsKey(s.Path));
            int loaded = status.Loaded.Count(p => !status.BuildIndex.ContainsKey(p));
            float x = rect.x;
            x = DrawChip(new Rect(x, rect.y, 0, rect.height), $"Của project {own}", BillSceneSwitcherPrefs.FilterProject, v => BillSceneSwitcherPrefs.FilterProject = v);
            x = DrawChip(new Rect(x + 5, rect.y, 0, rect.height), $"Asset mua {vendor}", BillSceneSwitcherPrefs.FilterVendor, v => BillSceneSwitcherPrefs.FilterVendor = v);
            DrawChip(new Rect(x + 5, rect.y, 0, rect.height), $"Đang mở {loaded}", BillSceneSwitcherPrefs.FilterLoaded, v => BillSceneSwitcherPrefs.FilterLoaded = v);
            return rect.yMax + 2;
        }

        float DrawChip(Rect rect, string label, bool on, Action<bool> set)
        {
            var e = Event.current;
            var content = new GUIContent(label);
            float w = EditorStyles.miniLabel.CalcSize(content).x + 16;
            var r = new Rect(rect.x, rect.y, w, rect.height);
            bool hover = r.Contains(e.mousePosition);
            EditorGUI.DrawRect(r, on ? BtnActiveBg : (hover ? BtnHover : BtnBg));
            DrawBorder(r, on ? RowActiveBorder : PanelBorder);
            var prev = GUI.color;
            GUI.color = on ? Accent : TextSecondary;
            GUI.Label(r, content, new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
            GUI.color = prev;
            if (hover && e.type == EventType.MouseDown && e.button == 0)
            {
                set(!on);
                MarkDirty();
                e.Use();
            }
            return r.xMax;
        }

        // ═══════════════════════════════════════════
        // Scene list rendering
        // ═══════════════════════════════════════════

        void DrawSceneList(Rect listRect, float pad)
        {
            if (_rows.Count == 0) return;
            float contentH = _rows[_rows.Count - 1].Y + _rows[_rows.Count - 1].H + 8;
            float innerW = listRect.width - 14;

            if (_scrollToSelected && Event.current.type == EventType.Repaint)
            {
                _scrollToSelected = false;
                var sel = SelectedRow();
                if (sel != null)
                {
                    if (sel.Y < _scroll.y) _scroll.y = sel.Y - 4;
                    else if (sel.Y + sel.H > _scroll.y + listRect.height) _scroll.y = sel.Y + sel.H - listRect.height + 4;
                }
            }

            var viewRect = new Rect(0, 0, innerW, contentH);
            _scroll = GUI.BeginScrollView(listRect, _scroll, viewRect);

            int sceneIdx = -1;
            foreach (var r in _rows)
            {
                if (r.Kind == Kind.Scene) sceneIdx++;
                if (r.Y + r.H < _scroll.y - 4 || r.Y > _scroll.y + listRect.height + 4) continue;   // off screen
                var rowRect = new Rect(pad, r.Y, innerW - pad * 2 + 14, r.H);
                switch (r.Kind)
                {
                    case Kind.Section: DrawSectionHeader(rowRect, r); break;
                    case Kind.Group: DrawGroup(rowRect, r); break;
                    case Kind.HiddenVendor: DrawHiddenVendor(rowRect, r); break;
                    case Kind.Empty: GUI.Label(rowRect, r.Text, EmptyStyle); break;
                    case Kind.Scene: DrawSceneRow(rowRect, r, sceneIdx == _selected); break;
                }
            }

            HandlePinDrag(innerW, pad);
            GUI.EndScrollView();
        }

        Row SelectedRow()
        {
            int i = -1;
            foreach (var r in _rows)
                if (r.Kind == Kind.Scene && ++i == _selected) return r;
            return null;
        }

        // ═══════════════════════════════════════════
        // Section header / group / hidden vendor
        // ═══════════════════════════════════════════

        void DrawSectionHeader(Rect rect, Row row)
        {
            rect.height = 18;
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + 6, 6, 6), row.Accent);

            var prev = GUI.color;
            GUI.color = new Color(SectionHeader.r, SectionHeader.g, SectionHeader.b, 1f);
            GUI.Label(new Rect(rect.x + 14, rect.y, rect.width - 14, rect.height), row.Text, SectionStyle);
            if (!string.IsNullOrEmpty(row.Right))
            {
                GUI.color = TextSecondary;
                GUI.Label(new Rect(rect.x, rect.y, rect.width - 4, rect.height), row.Right, SmallStyle);
            }
            GUI.color = prev;

            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax, rect.width, 1), RowSeparator);
        }

        void DrawGroup(Rect rect, Row row)
        {
            var e = Event.current;
            bool hover = rect.Contains(e.mousePosition);
            if (hover) EditorGUI.DrawRect(rect, RowHover);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), RowSeparator);
            var prev = GUI.color;
            GUI.color = TextSecondary;
            GUI.Label(new Rect(rect.x + 2, rect.y, 14, rect.height), row.Open ? "▾" : "▸", EditorStyles.miniLabel);
            GUI.color = TextPrimary;
            GUI.Label(new Rect(rect.x + 16, rect.y, rect.width - 60, rect.height), row.Text, EditorStyles.label);
            GUI.color = TextSecondary;
            GUI.Label(new Rect(rect.x, rect.y, rect.width - 4, rect.height), row.Right, SmallStyle);
            GUI.color = prev;
            if (hover && e.type == EventType.MouseDown && e.button == 0)
            {
                _groupOpen[row.Folder] = !row.Open;
                MarkDirty();
                e.Use();
            }
        }

        void DrawHiddenVendor(Rect rect, Row row)
        {
            var e = Event.current;
            bool hover = rect.Contains(e.mousePosition);
            if (hover) EditorGUI.DrawRect(rect, RowHover);
            DrawSectionHeader(rect, row);
            if (hover && e.type == EventType.MouseDown && e.button == 0)
            {
                _vendorExpanded = true;
                MarkDirty();
                e.Use();
            }
        }

        // ═══════════════════════════════════════════
        // Scene row rendering
        // ═══════════════════════════════════════════

        void DrawSceneRow(Rect rect, Row entry, bool selected)
        {
            var evt = Event.current;
            bool hovered = rect.Contains(evt.mousePosition);

            // Row background
            Color rowBg = entry.IsActive ? RowActive : (hovered ? RowHover : RowNormal);
            EditorGUI.DrawRect(rect, rowBg);
            if (selected) EditorGUI.DrawRect(rect, RowSelected);
            if (_dragging && _dragPath == entry.Path) EditorGUI.DrawRect(rect, BtnActiveBg);

            // Active scene accent border (left)
            if (entry.IsActive)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 3, rect.height - 4), RowActiveBorder);

            // Bootstrap accent border (left)
            if (entry.IsBootstrap && !entry.IsActive)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 3, rect.height - 4), BootstrapBorder);

            // Bottom separator
            EditorGUI.DrawRect(new Rect(rect.x + 4, rect.yMax - 1, rect.width - 8, 1), RowSeparator);

            float x = rect.x + 8;
            float midY = rect.y + (BillSceneSwitcherPrefs.ShowScenePath ? 4 : (rect.height - 16) / 2);

            // Build index badge (struck through when the scene is disabled in the build)
            if (entry.BuildIndex >= 0)
            {
                var idxRect = new Rect(x, midY, 18, 16);
                var badgeBg = entry.IsBootstrap
                    ? new Color(BootstrapAccent.r, BootstrapAccent.g, BootstrapAccent.b, 0.3f)
                    : new Color(BtnBg.r, BtnBg.g, BtnBg.b, 0.8f);
                EditorGUI.DrawRect(idxRect, badgeBg);

                var prev = GUI.color;
                GUI.color = entry.IsBootstrap ? BootstrapBorder : TextSecondary;
                if (!entry.Enabled) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.45f);
                GUI.Label(idxRect, entry.BuildIndex.ToString(), IndexStyle);
                if (!entry.Enabled) EditorGUI.DrawRect(new Rect(idxRect.x + 3, idxRect.center.y, idxRect.width - 6, 1), TextSecondary);
                GUI.color = prev;
            }
            x += 22;

            // Scene icon
            var icon = EditorGUIUtility.IconContent("SceneAsset Icon");
            if (icon.image != null)
            {
                GUI.DrawTexture(new Rect(x, midY, 16, 16), icon.image, ScaleMode.ScaleToFit);
                x += 20;
            }

            // Buttons + chip take the right side
            float btnAreaWidth = entry.AddRow ? 150 : CalculateButtonAreaWidth(entry);
            float chipW = entry.Chip == Chip.None ? 0 : 74;
            float nameWidth = rect.xMax - x - btnAreaWidth - chipW - 4;

            var nameRect = new Rect(x, rect.y + 2, nameWidth, BillSceneSwitcherPrefs.ShowScenePath ? 18 : rect.height - 4);
            var prevColor = GUI.color;
            GUI.color = entry.IsActive ? Accent : (entry.Enabled ? TextPrimary : TextSecondary);
            GUI.Label(nameRect, Highlight(entry.Name, entry.Highlight), NameStyle);
            GUI.color = prevColor;

            // Path
            if (BillSceneSwitcherPrefs.ShowScenePath)
            {
                prevColor = GUI.color;
                GUI.color = TextPath;
                GUI.Label(new Rect(x, rect.y + 20, nameWidth, 16), TrimPath(entry.Path), PathStyle);
                GUI.color = prevColor;
            }

            if (entry.Chip != Chip.None) DrawChipLabel(new Rect(rect.xMax - btnAreaWidth - chipW, rect.y + (rect.height - 14) / 2, chipW - 6, 14), entry.Chip);

            // Action buttons (right side)
            if (entry.AddRow) DrawAddButtons(rect, entry);
            else DrawRowButtons(rect, entry, btnAreaWidth);

            if (!hovered) return;

            // Right click: every action
            if (evt.type == EventType.ContextClick || (evt.type == EventType.MouseDown && evt.button == 1))
            {
                ShowContextMenu(entry);
                evt.Use();
                return;
            }

            // Double-click to load
            if (evt.type == EventType.MouseDown && evt.clickCount == 2 && evt.button == 0 && !entry.AddRow)
            {
                LoadScene(entry.Path, false);
                evt.Use();
                return;
            }

            // Press on a pinned row starts a possible drag
            if (evt.type == EventType.MouseDown && evt.button == 0 && entry.InPinnedSection && !_addMode && string.IsNullOrEmpty(_search))
            {
                _dragPath = entry.Path;
                _dragStart = evt.mousePosition;
                _dragging = false;
            }
        }

        static string Highlight(string name, string query)
        {
            if (string.IsNullOrEmpty(query)) return name;
            int i = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return name;
            return name.Substring(0, i) + "<color=#ffc28a><b>" + name.Substring(i, query.Length) + "</b></color>" + name.Substring(i + query.Length);
        }

        void DrawChipLabel(Rect rect, Chip chip)
        {
            var (text, col) = chip switch
            {
                Chip.Build => ("BUILD", ChipBuild),
                Chip.Loaded => ("ĐANG MỞ", ChipLoaded),
                _ => ("NGOÀI BUILD", ChipOut),
            };
            EditorGUI.DrawRect(rect, new Color(col.r, col.g, col.b, 0.14f));
            var prev = GUI.color;
            GUI.color = col;
            GUI.Label(rect, text, ChipStyle);
            GUI.color = prev;
        }

        float CalculateButtonAreaWidth(Row entry)
        {
            float w = 0;
            w += 26; // Load button
            if (BillSceneSwitcherPrefs.ShowAdditiveButton) w += 26; // Additive button
            if (entry.IsLoaded && !entry.IsActive) w += 26; // Unload button
            w += 26; // Pin button
            if (!entry.InBuild) w += 26; // Add to build
            else if (!entry.IsBootstrap) w += 26; // Bootstrap button
            return w + 4;
        }

        void DrawRowButtons(Rect rowRect, Row entry, float btnAreaWidth)
        {
            float btnSize = 22;
            float btnY = rowRect.y + (rowRect.height - btnSize) / 2;
            float x = rowRect.xMax - btnAreaWidth;

            // Load button
            if (DrawIconButton(new Rect(x, btnY, btnSize, btnSize), "d_PlayButton", "Mở scene (Enter)",
                    entry.IsActive ? BtnActiveBg : BtnBg))
            {
                LoadScene(entry.Path, false);
            }
            x += 26;

            // Additive load button
            if (BillSceneSwitcherPrefs.ShowAdditiveButton)
            {
                bool isLoaded = entry.IsLoaded && !entry.IsActive;
                if (DrawIconButton(new Rect(x, btnY, btnSize, btnSize),
                        "d_Toolbar Plus", isLoaded ? "Đã mở thêm (additive)" : "Mở thêm (Shift+Enter)",
                        isLoaded ? BtnActiveBg : BtnBg))
                {
                    if (!isLoaded)
                        LoadScene(entry.Path, true);
                }
                x += 26;
            }

            // Unload button (for additively loaded scenes)
            if (entry.IsLoaded && !entry.IsActive)
            {
                if (DrawIconButton(new Rect(x, btnY, btnSize, btnSize),
                        "d_Toolbar Minus", "Đóng scene", BtnBg))
                {
                    UnloadScene(entry.Path);
                }
                x += 26;
            }

            // Pin button
            if (DrawIconButton(new Rect(x, btnY, btnSize, btnSize),
                    entry.IsPinned ? "d_Favorite Icon" : "d_Favorite",
                    entry.IsPinned ? "Bỏ ghim" : "Ghim",
                    entry.IsPinned ? PinAccent : BtnBg))
            {
                TogglePin(entry);
            }
            x += 26;

            if (!entry.InBuild)
            {
                // Add to build settings
                if (DrawIconButton(new Rect(x, btnY, btnSize, btnSize), "d_CreateAddNew", "Thêm vào Build Settings", BuildAccent))
                    AddToBuild(entry);
            }
            else if (!entry.IsBootstrap)
            {
                // Set as bootstrap button
                if (DrawIconButton(new Rect(x, btnY, btnSize, btnSize),
                        "d_Animation.Record", "Đặt làm bootstrap (build index 0)", BtnBg))
                {
                    _data.SetBootstrapScene(entry.Path);
                    _data.Save();
                    MarkDirty();
                }
            }
        }

        void DrawAddButtons(Rect rowRect, Row entry)
        {
            float y = rowRect.y + (rowRect.height - 20) / 2;
            float x = rowRect.xMax - 150;
            using (new EditorGUI.DisabledScope(entry.IsPinned))
                if (GUI.Button(new Rect(x, y, 66, 20), entry.IsPinned ? "★ Đã ghim" : "☆ Ghim", EditorStyles.miniButton))
                    TogglePin(entry);
            using (new EditorGUI.DisabledScope(entry.InBuild))
                if (GUI.Button(new Rect(x + 70, y, 78, 20), entry.InBuild ? "Đã trong Build" : "⤒ Vào Build", EditorStyles.miniButton))
                    AddToBuild(entry);
        }

        bool DrawIconButton(Rect rect, string iconName, string tooltip, Color bg)
        {
            var evt = Event.current;
            bool hovered = rect.Contains(evt.mousePosition);
            Color drawBg = hovered ? BtnHover : bg;

            EditorGUI.DrawRect(rect, drawBg);

            // Border on hover
            if (hovered)
                DrawBorder(rect, new Color(PanelBorder.r, PanelBorder.g, PanelBorder.b, PanelBorder.a * 2f));

            var icon = EditorGUIUtility.IconContent(iconName);
            var content = icon?.image != null
                ? new GUIContent(icon.image, tooltip)
                : new GUIContent("?", tooltip);

            var prevColor = GUI.color;
            GUI.color = hovered ? Color.white : new Color(1, 1, 1, 0.7f);
            var iconRect = new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6);
            if (content.image != null)
                GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit);
            GUI.color = prevColor;

            // Invisible button for tooltip
            GUI.Label(rect, new GUIContent("", tooltip));

            if (hovered && evt.type == EventType.MouseDown && evt.button == 0)
            {
                evt.Use();
                return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════
        // Drag to reorder pinned scenes
        // ═══════════════════════════════════════════

        void HandlePinDrag(float innerW, float pad)
        {
            if (_dragPath == null) return;
            var e = Event.current;
            var pinned = _rows.Where(r => r.Kind == Kind.Scene && r.InPinnedSection).ToList();
            if (e.type == EventType.MouseDrag && !_dragging && Vector2.Distance(e.mousePosition, _dragStart) > 4) _dragging = true;

            if (_dragging)
            {
                // Insertion line between pinned rows
                _dropY = e.mousePosition.y;
                float lineY = pinned.Count > 0 ? pinned[0].Y : 0;
                foreach (var r in pinned) if (_dropY > r.Y + r.H / 2) lineY = r.Y + r.H;
                EditorGUI.DrawRect(new Rect(pad, lineY - 1, innerW - pad * 2 + 14, 2), Accent);
                if (e.type == EventType.MouseDrag) e.Use();
            }

            if (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp)
            {
                if (_dragging && pinned.Count > 0)
                {
                    int target = pinned.Count(r => _dropY > r.Y + r.H / 2);
                    var order = pinned.Select(r => r.Path).ToList();
                    int from = order.IndexOf(_dragPath);
                    if (from >= 0)
                    {
                        order.RemoveAt(from);
                        if (target > from) target--;
                        order.Insert(Mathf.Clamp(target, 0, order.Count), _dragPath);
                        _data.SetPinOrder(order);
                        _data.Save();
                        MarkDirty();
                    }
                    e.Use();
                }
                _dragPath = null;
                _dragging = false;
            }
        }

        // ═══════════════════════════════════════════
        // Context menu
        // ═══════════════════════════════════════════

        void ShowContextMenu(Row entry)
        {
            var menu = new GenericMenu();
            var path = entry.Path;
            menu.AddItem(new GUIContent("Mở\tEnter"), entry.IsActive, () => LoadScene(path, false));
            if (entry.IsLoaded && !entry.IsActive) menu.AddItem(new GUIContent("Đóng scene"), false, () => UnloadScene(path));
            else menu.AddItem(new GUIContent("Mở thêm (additive)\tShift+Enter"), false, () => LoadScene(path, true));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(entry.IsPinned ? "Bỏ ghim" : "Ghim"), entry.IsPinned, () => TogglePin(entry));
            if (entry.InBuild)
            {
                menu.AddItem(new GUIContent(entry.Enabled ? "Tắt trong Build" : "Bật trong Build"), false, () =>
                {
                    BillSceneSwitcherData.SetBuildEnabled(path, !entry.Enabled);
                    ShowToast($"Đã {(entry.Enabled ? "tắt" : "bật")} {entry.Name} trong Build Settings", BillSceneSwitcherData.UndoBuild);
                });
                menu.AddItem(new GUIContent("Bỏ khỏi Build Settings"), false, () =>
                {
                    BillSceneSwitcherData.RemoveFromBuild(path);
                    ShowToast($"Đã bỏ {entry.Name} khỏi Build Settings", BillSceneSwitcherData.UndoBuild);
                });
            }
            else menu.AddItem(new GUIContent("Thêm vào Build Settings"), false, () => AddToBuild(entry));
            if (entry.IsBootstrap) menu.AddDisabledItem(new GUIContent("Đang là Bootstrap"));
            else menu.AddItem(new GUIContent("Đặt làm Bootstrap"), false, () =>
            {
                _data.SetBootstrapScene(path);
                _data.Save();
                MarkDirty();
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Hiện trong Project"), false, () =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            });
            menu.AddItem(new GUIContent("Copy đường dẫn"), false, () => EditorGUIUtility.systemCopyBuffer = path);
            _menuOpen = true;
            menu.ShowAsContext();
        }

        // ═══════════════════════════════════════════
        // Toast
        // ═══════════════════════════════════════════

        void ShowToast(string message, Action undo)
        {
            _toast = message;
            _toastUndo = undo;
            _toastUntil = EditorApplication.timeSinceStartup + 5;
            MarkDirty();
        }

        void DrawToast(Rect rect)
        {
            if (_toast == null) return;
            if (EditorApplication.timeSinceStartup > _toastUntil) { _toast = null; _toastUndo = null; return; }
            EditorGUI.DrawRect(rect, ToastBg);
            DrawBorder(rect, new Color(ChipLoaded.r, ChipLoaded.g, ChipLoaded.b, 0.35f));
            var prev = GUI.color;
            GUI.color = IsDark ? new Color(0.81f, 0.93f, 0.83f) : new Color(0.1f, 0.3f, 0.12f);
            GUI.Label(new Rect(rect.x + 8, rect.y, rect.width - 90, rect.height), "✓ " + _toast, EditorStyles.label);
            GUI.color = prev;
            if (_toastUndo != null && GUI.Button(new Rect(rect.xMax - 78, rect.y + 3, 72, rect.height - 6), "Hoàn tác", EditorStyles.miniButton))
            {
                _toastUndo();
                _toast = null;
                _toastUndo = null;
                MarkDirty();
            }
        }

        // ═══════════════════════════════════════════
        // Footer
        // ═══════════════════════════════════════════

        void DrawFooter(Rect rect, float pad)
        {
            EditorGUI.DrawRect(new Rect(rect.x + pad, rect.y, rect.width - pad * 2, 1), RowSeparator);
            float btnY = rect.y + 6;

            if (_addMode)
            {
                var prev = GUI.color;
                GUI.color = TextSecondary;
                GUI.Label(new Rect(pad, btnY, rect.width - pad * 2, 22), "Ghim = chỉ hiện trong switcher · Vào Build = thêm vào Build Settings (bật)", new GUIStyle(SmallStyle) { alignment = TextAnchor.MiddleLeft });
                GUI.color = prev;
                return;
            }

            float w = rect.width - pad * 2;
            float refreshW = 70, gap = 6;
            float half = (w - refreshW - gap * 2) / 2f;
            if (GUI.Button(new Rect(pad, btnY, half, 22), "Build Settings", FooterBtnStyle))
                EditorWindow.GetWindow(Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUI.Button(new Rect(pad + half + gap, btnY, half, 22), "▶ Play từ Bootstrap", FooterBtnStyle))
                {
                    Close();
                    BillScenePlayFromBootstrap.Play();
                    GUIUtility.ExitGUI();
                }

            if (GUI.Button(new Rect(pad + half * 2 + gap * 2, btnY, refreshW, 22), "Refresh", FooterBtnStyle))
            {
                BillSceneIndex.Invalidate();
                MarkDirty();
            }
        }

        // ═══════════════════════════════════════════
        // Scene operations
        // ═══════════════════════════════════════════

        void TogglePin(Row entry)
        {
            bool was = entry.IsPinned;
            _data.TogglePin(entry.Path);
            _data.Save();
            var path = entry.Path;
            ShowToast(was ? $"Đã bỏ ghim {entry.Name}" : $"Đã ghim {entry.Name}", () => { _data.TogglePin(path); _data.Save(); });
        }

        void AddToBuild(Row entry)
        {
            int index = BillSceneSwitcherData.AddToBuild(entry.Path);
            ShowToast($"Đã thêm {entry.Name} vào Build Settings (index {index}, đang bật)", BillSceneSwitcherData.UndoBuild);
        }

        void LoadScene(string path, bool additive)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (EditorApplication.isPlaying)
            {
                // In play mode, use runtime scene loading
                var mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
                SceneManager.LoadScene(Path.GetFileNameWithoutExtension(path), mode);
            }
            else
            {
                if (!additive && BillSceneSwitcherPrefs.ConfirmSceneSwitch)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        return;
                }

                var mode = additive ? OpenSceneMode.Additive : OpenSceneMode.Single;
                EditorSceneManager.OpenScene(path, mode);
            }

            MarkDirty();

            if (!additive)
            {
                Close();
                // Only inside OnGUI: menu callbacks run outside it.
                if (Event.current != null) GUIUtility.ExitGUI();
            }
        }

        void UnloadScene(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var scene = SceneManager.GetSceneByPath(path);
            if (scene.isLoaded && SceneManager.sceneCount > 1)
            {
                if (EditorApplication.isPlaying)
                    SceneManager.UnloadSceneAsync(scene);
                else
                    EditorSceneManager.CloseScene(scene, true);

                MarkDirty();
            }
        }

        // ═══════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════

        static string TrimPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            if (path.StartsWith("Assets/")) path = path.Substring(7);
            return path;
        }
    }
}
#endif
