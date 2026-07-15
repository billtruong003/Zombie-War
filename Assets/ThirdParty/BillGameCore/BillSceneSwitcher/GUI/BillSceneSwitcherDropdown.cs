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
    /// Glassmorphism-styled dropdown panel for the Scene Switcher.
    /// Shows bootstrap scene, pinned scenes, and build settings scenes.
    /// Supports single load, additive load, pin, and set-as-bootstrap actions.
    /// </summary>
    public class BillSceneSwitcherDropdown : EditorWindow
    {
        string _search = "";
        Vector2 _scroll;
        BillSceneSwitcherData _data;
        List<SceneEntry> _cachedEntries;
        double _openTime;

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

        static Color BootstrapAccent => IsDark
            ? new Color(1f, 0.8f, 0.2f, 0.35f)
            : new Color(0.9f, 0.7f, 0.1f, 0.25f);
        static Color BootstrapBorder => IsDark
            ? new Color(1f, 0.85f, 0.3f, 0.4f)
            : new Color(0.85f, 0.65f, 0.1f, 0.3f);
        static Color PinAccent => IsDark
            ? new Color(1f, 0.55f, 0.2f, 0.25f)
            : new Color(0.9f, 0.45f, 0.1f, 0.15f);

        static Color TextPrimary => IsDark
            ? new Color(0.88f, 0.88f, 0.92f)
            : new Color(0.12f, 0.12f, 0.16f);
        static Color TextSecondary => IsDark
            ? new Color(0.5f, 0.5f, 0.56f)
            : new Color(0.45f, 0.45f, 0.5f);
        static Color TextPath => IsDark
            ? new Color(0.42f, 0.42f, 0.5f, 0.7f)
            : new Color(0.4f, 0.4f, 0.5f, 0.6f);

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
        static Color AdditiveAccent => IsDark
            ? new Color(1f, 0.6f, 0.2f, 0.6f)
            : new Color(0.9f, 0.5f, 0.15f, 0.5f);

        // ═══════════════════════════════════════════
        // GUIStyle cache
        // ═══════════════════════════════════════════

        static GUIStyle _nameStyle, _pathStyle, _sectionStyle, _searchStyle;
        static GUIStyle _btnStyle, _btnActiveStyle, _indexStyle, _emptyStyle;
        static GUIStyle _footerBtnStyle;

        static GUIStyle NameStyle => _nameStyle ??= new GUIStyle(EditorStyles.label)
            { fontSize = 12, alignment = TextAnchor.MiddleLeft };
        static GUIStyle PathStyle => _pathStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 9, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        static GUIStyle SectionStyle => _sectionStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 9, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        static GUIStyle SearchStyle => _searchStyle ??= new GUIStyle(EditorStyles.toolbarSearchField)
            { fontSize = 12 };
        static GUIStyle BtnStyle => _btnStyle ??= new GUIStyle(EditorStyles.miniButton)
            { fontSize = 10, padding = new RectOffset(4, 4, 2, 2), fixedHeight = 0 };
        static GUIStyle BtnActiveStyle => _btnActiveStyle ??= new GUIStyle(EditorStyles.miniButton)
            { fontSize = 10, fontStyle = FontStyle.Bold, padding = new RectOffset(4, 4, 2, 2), fixedHeight = 0 };
        static GUIStyle IndexStyle => _indexStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 9, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        static GUIStyle EmptyStyle => _emptyStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            { fontSize = 11 };
        static GUIStyle FooterBtnStyle => _footerBtnStyle ??= new GUIStyle(EditorStyles.miniButton)
            { fontSize = 11, fixedHeight = 24 };

        // ═══════════════════════════════════════════
        // Scene entry
        // ═══════════════════════════════════════════

        struct SceneEntry
        {
            public string Name;
            public string Path;
            public int BuildIndex;
            public bool IsActive;
            public bool IsLoaded;
            public bool IsBootstrap;
            public bool IsPinned;
            public bool InBuildSettings;
            public bool Enabled;
        }

        // ═══════════════════════════════════════════
        // Show / lifecycle
        // ═══════════════════════════════════════════

        public static void Show(Rect buttonScreenRect)
        {
            var window = CreateInstance<BillSceneSwitcherDropdown>();
            window.titleContent = new GUIContent("Scene Switcher");
            window.wantsMouseMove = true;
            var size = new Vector2(360, 480);
            window.ShowAsDropDown(buttonScreenRect, size);
        }

        void OnEnable()
        {
            _data = BillSceneSwitcherData.Instance;
            _openTime = EditorApplication.timeSinceStartup;
            RefreshSceneEntries();
        }

        void OnLostFocus()
        {
            // Delay close slightly to allow button clicks to register
            EditorApplication.delayCall += () =>
            {
                if (this != null) Close();
            };
        }

        void OnProjectChange() => RefreshSceneEntries();

        // ═══════════════════════════════════════════
        // Scene data gathering
        // ═══════════════════════════════════════════

        void RefreshSceneEntries()
        {
            _cachedEntries = new List<SceneEntry>();
            var buildScenes = EditorBuildSettings.scenes;
            var activeScenePath = EditorSceneManager.GetActiveScene().path;
            var bootstrapPath = _data?.BootstrapScenePath ?? "";

            // Collect loaded scene paths
            var loadedPaths = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) loadedPaths.Add(s.path);
            }

            for (int i = 0; i < buildScenes.Length; i++)
            {
                var bs = buildScenes[i];
                var name = Path.GetFileNameWithoutExtension(bs.path);
                if (string.IsNullOrEmpty(name)) continue;

                _cachedEntries.Add(new SceneEntry
                {
                    Name = name,
                    Path = bs.path,
                    BuildIndex = i,
                    IsActive = bs.path == activeScenePath,
                    IsLoaded = loadedPaths.Contains(bs.path),
                    IsBootstrap = bs.path == bootstrapPath || (string.IsNullOrEmpty(bootstrapPath) && i == 0),
                    IsPinned = _data != null && _data.IsPinned(bs.path),
                    InBuildSettings = true,
                    Enabled = bs.enabled
                });
            }

            // Add pinned scenes not in build settings
            if (_data != null)
            {
                var pinnedPaths = _data.GetPinnedScenePaths();
                foreach (var pp in pinnedPaths)
                {
                    if (_cachedEntries.Any(e => e.Path == pp)) continue;
                    var name = Path.GetFileNameWithoutExtension(pp);
                    if (string.IsNullOrEmpty(name)) continue;
                    _cachedEntries.Add(new SceneEntry
                    {
                        Name = name,
                        Path = pp,
                        BuildIndex = -1,
                        IsActive = pp == activeScenePath,
                        IsLoaded = loadedPaths.Contains(pp),
                        IsBootstrap = false,
                        IsPinned = true,
                        InBuildSettings = false,
                        Enabled = true
                    });
                }
            }
        }

        // ═══════════════════════════════════════════
        // Main OnGUI
        // ═══════════════════════════════════════════

        void OnGUI()
        {
            _data = BillSceneSwitcherData.Instance;
            var rect = new Rect(0, 0, position.width, position.height);

            DrawGlassBackground(rect);
            DrawAccentLine(rect);

            var contentRect = new Rect(0, 2, rect.width, rect.height - 2);
            DrawContent(contentRect);

            // Repaint for animations
            if (EditorApplication.timeSinceStartup - _openTime < 0.5f)
                Repaint();

            if (Event.current.type == EventType.MouseMove)
                Repaint();
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
            float pad = 10;

            // Search bar
            var searchRect = new Rect(pad, y, rect.width - pad * 2, 20);
            DrawSearchField(searchRect);
            y += 28;

            // Separator
            EditorGUI.DrawRect(new Rect(pad, y, rect.width - pad * 2, 1), RowSeparator);
            y += 6;

            // Scrollable scene list
            var listRect = new Rect(0, y, rect.width, rect.height - y - 38);
            DrawSceneList(listRect, pad);

            // Footer
            var footerRect = new Rect(0, rect.height - 34, rect.width, 34);
            DrawFooter(footerRect, pad);
        }

        void DrawSearchField(Rect rect)
        {
            // Glass search background
            var bgRect = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
            EditorGUI.DrawRect(bgRect, SearchBg);
            DrawBorder(bgRect, SearchBorder);

            GUI.SetNextControlName("SceneSwitcherSearch");
            _search = EditorGUI.TextField(rect, _search, SearchStyle);

            // Auto-focus search on open
            if (EditorApplication.timeSinceStartup - _openTime < 0.2f)
                EditorGUI.FocusTextInControl("SceneSwitcherSearch");
        }

        // ═══════════════════════════════════════════
        // Scene list rendering
        // ═══════════════════════════════════════════

        void DrawSceneList(Rect listRect, float pad)
        {
            var filtered = GetFilteredEntries();

            if (filtered.Count == 0)
            {
                GUI.Label(listRect, string.IsNullOrEmpty(_search)
                    ? "No scenes in Build Settings.\nAdd scenes via File > Build Settings."
                    : "No matching scenes found.", EmptyStyle);
                return;
            }

            // Calculate content height
            float rowH = BillSceneSwitcherPrefs.ShowScenePath ? 42f : 28f;
            float sectionH = 22f;
            float contentH = 0;

            bool hasBootstrap = filtered.Any(e => e.IsBootstrap);
            bool hasPinned = filtered.Any(e => e.IsPinned && !e.IsBootstrap);
            bool hasBuild = filtered.Any(e => !e.IsPinned || e.IsBootstrap);

            if (hasBootstrap) contentH += sectionH + filtered.Count(e => e.IsBootstrap) * rowH + 4;
            if (hasPinned) contentH += sectionH + filtered.Count(e => e.IsPinned && !e.IsBootstrap) * rowH + 4;
            if (hasBuild) contentH += sectionH + filtered.Count(e => e.InBuildSettings) * rowH + 4;

            // Scroll view
            var viewRect = new Rect(0, 0, listRect.width - 14, contentH + 8);
            _scroll = GUI.BeginScrollView(listRect, _scroll, viewRect);

            float y = 4;

            // Bootstrap section
            if (hasBootstrap)
            {
                y = DrawSectionHeader(pad, y, listRect.width - 14, "BOOTSTRAP", BootstrapAccent);
                foreach (var entry in filtered.Where(e => e.IsBootstrap))
                {
                    DrawSceneRow(new Rect(pad, y, listRect.width - pad * 2 - 14, rowH), entry, true);
                    y += rowH;
                }
                y += 4;
            }

            // Pinned section
            if (hasPinned)
            {
                y = DrawSectionHeader(pad, y, listRect.width - 14, "PINNED", PinAccent);
                foreach (var entry in filtered.Where(e => e.IsPinned && !e.IsBootstrap))
                {
                    DrawSceneRow(new Rect(pad, y, listRect.width - pad * 2 - 14, rowH), entry, false);
                    y += rowH;
                }
                y += 4;
            }

            // Build settings section
            if (hasBuild)
            {
                int count = filtered.Count(e => e.InBuildSettings);
                y = DrawSectionHeader(pad, y, listRect.width - 14,
                    $"BUILD SETTINGS ({count})", SectionHeader);
                foreach (var entry in filtered.Where(e => e.InBuildSettings).OrderBy(e => e.BuildIndex))
                {
                    DrawSceneRow(new Rect(pad, y, listRect.width - pad * 2 - 14, rowH), entry, false);
                    y += rowH;
                }
                y += 4;
            }

            GUI.EndScrollView();
        }

        List<SceneEntry> GetFilteredEntries()
        {
            if (_cachedEntries == null) RefreshSceneEntries();
            if (string.IsNullOrEmpty(_search)) return _cachedEntries;

            var lower = _search.ToLowerInvariant();
            return _cachedEntries.Where(e =>
                e.Name.ToLowerInvariant().Contains(lower) ||
                e.Path.ToLowerInvariant().Contains(lower)).ToList();
        }

        // ═══════════════════════════════════════════
        // Section header
        // ═══════════════════════════════════════════

        float DrawSectionHeader(float x, float y, float width, string text, Color accent)
        {
            var rect = new Rect(x, y, width - x * 2, 18);

            // Accent dot
            var dotRect = new Rect(rect.x + 2, rect.y + 6, 6, 6);
            EditorGUI.DrawRect(dotRect, accent);

            // Label
            var prev = GUI.color;
            GUI.color = new Color(SectionHeader.r, SectionHeader.g, SectionHeader.b, 1f);
            GUI.Label(new Rect(rect.x + 14, rect.y, rect.width - 14, rect.height), text, SectionStyle);
            GUI.color = prev;

            // Separator line
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax, rect.width, 1), RowSeparator);

            return y + 20;
        }

        // ═══════════════════════════════════════════
        // Scene row rendering
        // ═══════════════════════════════════════════

        void DrawSceneRow(Rect rect, SceneEntry entry, bool isBootstrapSection)
        {
            var evt = Event.current;
            bool hovered = rect.Contains(evt.mousePosition);

            // Row background
            Color rowBg = entry.IsActive ? RowActive : (hovered ? RowHover : RowNormal);
            EditorGUI.DrawRect(rect, rowBg);

            // Active scene accent border (left)
            if (entry.IsActive)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 3, rect.height - 4), RowActiveBorder);

            // Bootstrap accent border (left)
            if (entry.IsBootstrap && !entry.IsActive)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, 3, rect.height - 4), BootstrapBorder);

            // Bottom separator
            EditorGUI.DrawRect(new Rect(rect.x + 4, rect.yMax - 1, rect.width - 8, 1), RowSeparator);

            float x = rect.x + 8;

            // Build index badge
            if (entry.BuildIndex >= 0)
            {
                var idxRect = new Rect(x, rect.y + 4, 18, 16);
                var badgeBg = entry.IsBootstrap
                    ? new Color(BootstrapAccent.r, BootstrapAccent.g, BootstrapAccent.b, 0.3f)
                    : new Color(BtnBg.r, BtnBg.g, BtnBg.b, 0.8f);
                EditorGUI.DrawRect(idxRect, badgeBg);

                var prev = GUI.color;
                GUI.color = entry.IsBootstrap ? BootstrapBorder : TextSecondary;
                GUI.Label(idxRect, entry.BuildIndex.ToString(), IndexStyle);
                GUI.color = prev;

                x += 22;
            }

            // Scene icon
            var icon = EditorGUIUtility.IconContent("SceneAsset Icon");
            if (icon.image != null)
            {
                GUI.DrawTexture(new Rect(x, rect.y + 4, 16, 16), icon.image, ScaleMode.ScaleToFit);
                x += 20;
            }

            // Scene name
            float btnAreaWidth = CalculateButtonAreaWidth(entry);
            float nameWidth = rect.xMax - x - btnAreaWidth - 4;

            var nameRect = new Rect(x, rect.y + 2, nameWidth, BillSceneSwitcherPrefs.ShowScenePath ? 18 : rect.height - 4);
            var prevColor = GUI.color;
            GUI.color = entry.IsActive ? new Color(1f, 0.65f, 0.3f) : (entry.Enabled ? TextPrimary : TextSecondary);
            GUI.Label(nameRect, entry.Name, NameStyle);
            GUI.color = prevColor;

            // Path
            if (BillSceneSwitcherPrefs.ShowScenePath)
            {
                var pathRect = new Rect(x, rect.y + 20, nameWidth, 16);
                prevColor = GUI.color;
                GUI.color = TextPath;
                GUI.Label(pathRect, TrimPath(entry.Path), PathStyle);
                GUI.color = prevColor;
            }

            // Action buttons (right side)
            DrawRowButtons(rect, entry, btnAreaWidth, isBootstrapSection);

            // Double-click to load
            if (hovered && evt.type == EventType.MouseDown && evt.clickCount == 2 && evt.button == 0)
            {
                LoadScene(entry.Path, false);
                evt.Use();
            }
        }

        float CalculateButtonAreaWidth(SceneEntry entry)
        {
            float w = 0;
            w += 26; // Load button
            if (BillSceneSwitcherPrefs.ShowAdditiveButton) w += 26; // Additive button
            w += 26; // Pin button
            if (!entry.IsBootstrap) w += 26; // Bootstrap button
            if (entry.IsLoaded && !entry.IsActive) w += 26; // Unload button
            return w + 4;
        }

        void DrawRowButtons(Rect rowRect, SceneEntry entry, float btnAreaWidth, bool isBootstrapSection)
        {
            float btnSize = 22;
            float btnY = rowRect.y + (BillSceneSwitcherPrefs.ShowScenePath ? 8 : (rowRect.height - btnSize) / 2);
            float x = rowRect.xMax - btnAreaWidth;

            // Load button
            if (DrawIconButton(new Rect(x, btnY, btnSize, btnSize), "d_PlayButton", "Load scene",
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
                        "d_Toolbar Plus", isLoaded ? "Already loaded (additive)" : "Load additive",
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
                        "d_Toolbar Minus", "Unload scene", BtnBg))
                {
                    UnloadScene(entry.Path);
                }
                x += 26;
            }

            // Pin button
            {
                Color pinBg = entry.IsPinned ? PinAccent : BtnBg;
                if (DrawIconButton(new Rect(x, btnY, btnSize, btnSize),
                        entry.IsPinned ? "d_Favorite Icon" : "d_Favorite",
                        entry.IsPinned ? "Unpin" : "Pin scene",
                        pinBg))
                {
                    _data.TogglePin(entry.Path);
                    _data.Save();
                    RefreshSceneEntries();
                }
                x += 26;
            }

            // Set as bootstrap button
            if (!entry.IsBootstrap)
            {
                if (DrawIconButton(new Rect(x, btnY, btnSize, btnSize),
                        "d_Animation.Record", "Set as bootstrap scene (build index 0)",
                        BtnBg))
                {
                    _data.SetBootstrapScene(entry.Path);
                    _data.Save();
                    RefreshSceneEntries();
                }
            }
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
        // Footer
        // ═══════════════════════════════════════════

        void DrawFooter(Rect rect, float pad)
        {
            EditorGUI.DrawRect(new Rect(rect.x + pad, rect.y, rect.width - pad * 2, 1), RowSeparator);

            float btnW = (rect.width - pad * 2 - 6) / 2f;
            float btnY = rect.y + 6;

            if (GUI.Button(new Rect(pad, btnY, btnW, 22), "Build Settings", FooterBtnStyle))
                EditorWindow.GetWindow(Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));

            if (GUI.Button(new Rect(pad + btnW + 6, btnY, btnW, 22), "Refresh", FooterBtnStyle))
                RefreshSceneEntries();
        }

        // ═══════════════════════════════════════════
        // Scene operations
        // ═══════════════════════════════════════════

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

            RefreshSceneEntries();

            if (!additive)
                Close();
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

                RefreshSceneEntries();
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
