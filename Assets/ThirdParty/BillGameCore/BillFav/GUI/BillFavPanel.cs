#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Object = UnityEngine.Object;

namespace BillGameCore.BillFav
{
    /// <summary>
    /// Glassmorphism-styled panel renderer for BillFav.
    /// Used by both BillFavOverlay (hack mode) and BillFavWindow (standalone mode).
    /// </summary>
    public class BillFavPanel
    {
        readonly AnimationDriver _anim = new();
        readonly DragDropHandler _drag = new();

        // Mouse state
        bool _mouseDown;
        Vector2 _mousePos;
        Vector2 _mouseDownPos;
        bool _doubleClick;

        // Hover
        int _hoveredRow = -1;
        float _hoverAlpha;
        double _lastHoverTime;

        BillFavData _data;
        float _rowHeight;

        static bool IsDark => EditorGUIUtility.isProSkin;

        // ═══════════════════════════════════════════
        // Glassmorphism Color Palette
        // ═══════════════════════════════════════════

        static Color PanelBg => IsDark
            ? new Color(0.11f, 0.11f, 0.14f, 0.94f)
            : new Color(0.94f, 0.94f, 0.96f, 0.92f);
        static Color PanelBorder => IsDark
            ? new Color(1f, 1f, 1f, 0.06f)
            : new Color(0f, 0f, 0f, 0.08f);
        static Color PanelInnerGlow => IsDark
            ? new Color(1f, 1f, 1f, 0.02f)
            : new Color(1f, 1f, 1f, 0.3f);

        static Color RowNormal => IsDark
            ? new Color(1f, 1f, 1f, 0.025f)
            : new Color(0f, 0f, 0f, 0.02f);
        static Color RowHover => IsDark
            ? new Color(1f, 1f, 1f, 0.07f)
            : new Color(0f, 0f, 0f, 0.05f);
        static Color RowSelected => IsDark
            ? new Color(1f, 0.55f, 0.2f, 0.14f)
            : new Color(0.9f, 0.45f, 0.1f, 0.1f);
        static Color RowSelectedBorder => IsDark
            ? new Color(1f, 0.6f, 0.25f, 0.3f)
            : new Color(0.9f, 0.5f, 0.15f, 0.25f);
        static Color RowSeparator => IsDark
            ? new Color(1f, 1f, 1f, 0.035f)
            : new Color(0f, 0f, 0f, 0.04f);

        static Color TextPrimary => IsDark
            ? new Color(0.88f, 0.88f, 0.92f)
            : new Color(0.12f, 0.12f, 0.16f);
        static Color TextSecondary => IsDark
            ? new Color(0.5f, 0.5f, 0.56f)
            : new Color(0.45f, 0.45f, 0.5f);
        static Color TextPath => IsDark
            ? new Color(0.42f, 0.42f, 0.5f, 0.7f)
            : new Color(0.4f, 0.4f, 0.5f, 0.6f);

        static Color PillBg => IsDark
            ? new Color(0.08f, 0.08f, 0.12f, 0.85f)
            : new Color(1f, 1f, 1f, 0.7f);
        static Color PillBorder => IsDark
            ? new Color(1f, 1f, 1f, 0.08f)
            : new Color(0f, 0f, 0f, 0.1f);
        static Color BadgeBg => IsDark
            ? new Color(1f, 0.55f, 0.15f, 0.4f)
            : new Color(0.9f, 0.45f, 0.1f, 0.3f);

        static Color DragGlow => IsDark
            ? new Color(1f, 0.6f, 0.2f, 0.08f)
            : new Color(0.9f, 0.5f, 0.15f, 0.06f);

        // ═══════════════════════════════════════════
        // Type-based accent colors
        // ═══════════════════════════════════════════

        static Color AccentScript => new(0.35f, 0.6f, 1f, 0.6f);       // blue
        static Color AccentPrefab => new(0.6f, 0.4f, 0.9f, 0.6f);      // purple
        static Color AccentMaterial => new(0.3f, 0.8f, 0.5f, 0.6f);     // green
        static Color AccentTexture => new(0.9f, 0.6f, 0.2f, 0.6f);      // orange
        static Color AccentAudio => new(0.9f, 0.4f, 0.55f, 0.6f);       // pink
        static Color AccentScene => new(0.5f, 0.8f, 0.9f, 0.6f);        // cyan
        static Color AccentFolder => new(0.85f, 0.75f, 0.35f, 0.6f);    // gold
        static Color AccentDefault => new(0.5f, 0.5f, 0.55f, 0.4f);     // gray

        static Color GetTypeAccent(BillFavItem item, Object obj)
        {
            if (item.IsFolder) return AccentFolder;
            if (obj == null) return AccentDefault;

            var type = obj.GetType();
            if (type == typeof(MonoScript)) return AccentScript;
            if (type.Name.Contains("Material")) return AccentMaterial;
            if (type == typeof(GameObject)) return AccentPrefab;
            if (type == typeof(Texture2D) || type == typeof(Sprite) || type == typeof(Texture)) return AccentTexture;
            if (type == typeof(AudioClip)) return AccentAudio;
            if (type == typeof(SceneAsset)) return AccentScene;

            var path = item.AssetPath;
            if (path.EndsWith(".cs")) return AccentScript;
            if (path.EndsWith(".prefab")) return AccentPrefab;
            if (path.EndsWith(".mat")) return AccentMaterial;
            if (path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".tga")) return AccentTexture;
            if (path.EndsWith(".wav") || path.EndsWith(".mp3") || path.EndsWith(".ogg")) return AccentAudio;
            if (path.EndsWith(".unity")) return AccentScene;

            return AccentDefault;
        }

        // ═══════════════════════════════════════════
        // GUIStyle cache
        // ═══════════════════════════════════════════

        static GUIStyle _nameStyle, _nameSelectedStyle, _statusStyle, _pathStyle;
        static GUIStyle _brandingStyle, _pillStyle, _badgeStyle, _emptyHintStyle;

        static GUIStyle NameStyle => _nameStyle ??= new GUIStyle(EditorStyles.label)
            { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
        static GUIStyle NameSelectedStyle => _nameSelectedStyle ??= new GUIStyle(EditorStyles.boldLabel)
            { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
        static GUIStyle StatusStyle => _statusStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 9, fontStyle = FontStyle.Italic };
        static GUIStyle PathStyle => _pathStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { fontSize = 9, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        static GUIStyle BrandingStyle => _brandingStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 10, fontStyle = FontStyle.Bold };
        static GUIStyle PillStyle => _pillStyle ??= new GUIStyle(EditorStyles.boldLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        static GUIStyle BadgeStyle => _badgeStyle ??= new GUIStyle(EditorStyles.miniLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 8, fontStyle = FontStyle.Bold };
        static GUIStyle EmptyHintStyle => _emptyHintStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            { fontSize = 11 };

        // ───────────────────────────────────────────
        // Main entry points
        // ───────────────────────────────────────────

        public void UpdateDrag(Rect rect)
        {
            _data = BillFavData.Instance;
            if (_data == null) return;
            _rowHeight = 44f * _data.RowScale;
            var evt = Event.current;
            UpdateMouseState(evt, rect);
            _drag.Update(evt, rect, _data, _rowHeight, _mousePos, _mouseDownPos,
                         _data.ActivePage.ScrollPos, MouseDragDistance);
        }

        public bool IsDragging => _drag.IsDragging;

        public void Draw(Rect rect, float opacity = 1f)
        {
            _data = BillFavData.Instance;
            if (_data == null) return;
            _rowHeight = 44f * _data.RowScale;

            var evt = Event.current;
            UpdateMouseState(evt, rect);

            _anim.Tick(opacity, _data.ActivePageIndex, _data,
                       _drag.IsDragging, _drag.InsertIndex(MouseRowsPos), _rowHeight);
            _drag.Update(evt, rect, _data, _rowHeight, _mousePos, _mouseDownPos,
                         _data.ActivePage.ScrollPos, MouseDragDistance);

            DrawGlassBackground(rect);
            DrawAccentLine(rect);
            DrawPage(rect, _data.ActivePage);
            DrawBottomBar(rect);

            HandleKeys(evt, rect);
            HandlePageScroll(evt);
            TickHover();

            if (_anim.IsAnimating || _drag.IsDragging || _hoverAlpha > 0.01f)
                HandleUtility.Repaint();
        }

        // ───────────────────────────────────────────
        // Glass background
        // ───────────────────────────────────────────

        void DrawGlassBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), PanelInnerGlow);

            // Top gradient (frosted depth)
            for (int i = 0; i < 8; i++)
            {
                float a = (1f - i / 8f) * 0.015f;
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + i, rect.width, 1),
                    IsDark ? new Color(1, 1, 1, a) : new Color(0, 0, 0, a * 0.3f));
            }

            DrawBorder(rect, PanelBorder);
        }

        // ───────────────────────────────────────────
        // Animated accent line (top — breathing gradient)
        // ───────────────────────────────────────────

        void DrawAccentLine(Rect rect)
        {
            float t = (float)EditorApplication.timeSinceStartup;
            float lineH = 2f;
            var lineRect = new Rect(rect.x, rect.y, rect.width, lineH);

            int segments = Mathf.Max(1, (int)(rect.width / 3));
            float segW = rect.width / segments;

            for (int i = 0; i < segments; i++)
            {
                float ratio = (float)i / segments;
                // Warm shifting hue — orange/amber breathing
                float hue = Mathf.Lerp(0.06f, 0.12f, Mathf.PingPong(ratio * 2f + t * 0.06f, 1f));
                float sat = IsDark ? 0.7f : 0.6f;
                float val = IsDark ? 0.85f : 0.7f;
                var c = Color.HSVToRGB(hue, sat, val);
                c.a = IsDark ? 0.35f : 0.25f;

                EditorGUI.DrawRect(new Rect(rect.x + i * segW, rect.y, segW + 1, lineH), c);
            }
        }

        // ───────────────────────────────────────────
        // Page drawing
        // ───────────────────────────────────────────

        void DrawPage(Rect pageRect, BillFavData.Page page)
        {
            var items = page.Items;

            GUILayout.BeginArea(pageRect);
            page.ScrollPos = EditorGUILayout.BeginScrollView(
                new Vector2(0, page.ScrollPos), GUIStyle.none, GUIStyle.none).y;

            GUILayout.Space(6);

            for (int i = 0; i < items.Count; i++)
            {
                float gap = _anim.GetRowGap(i);
                if (gap > 0.5f) GUILayout.Space(gap);

                var rowRect = GUILayoutUtility.GetRect(0, _rowHeight, GUILayout.ExpandWidth(true));

                if (_drag.IsDragging && _drag.DraggedItem != null &&
                    items[i].Equals(_drag.DraggedItem)) continue;

                DrawRow(rowRect, items[i], i);
            }

            float trailGap = _anim.GetRowGap(items.Count);
            if (trailGap > 0.5f) GUILayout.Space(trailGap);
            GUILayout.Space(70);

            if (_drag.IsDragging && _drag.DraggedItem != null)
            {
                float y = _drag.DraggedItemY(_mousePos - pageRect.position);
                var dragRect = new Rect(0, y, pageRect.width, _rowHeight);
                DrawRow(dragRect, _drag.DraggedItem, -1, isBeingDragged: true);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            if (!items.Any() && !_drag.IsDragging)
                DrawEmptyHint(pageRect);

            DrawScrollFade(pageRect, page.ScrollPos);
        }

        // ───────────────────────────────────────────
        // Row drawing — glassmorphism + type accent
        // ───────────────────────────────────────────

        void DrawRow(Rect rect, BillFavItem item, int index, bool isBeingDragged = false)
        {
            var evt = Event.current;
            bool hovered = rect.Contains(evt.mousePosition) && !_drag.IsDragging;
            if (hovered && index >= 0) _hoveredRow = index;

            var obj = item.Obj;
            if (obj == null)
            {
                var path = item.AssetPath;
                if (!string.IsNullOrEmpty(path))
                    obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            }

            bool selected = !_drag.IsDragging && Selection.activeObject != null &&
                            obj != null && Selection.activeObject == obj;

            float pad = 6f;
            var inner = new Rect(rect.x + pad, rect.y + 1, rect.width - pad * 2, rect.height - 2);
            var accent = GetTypeAccent(item, obj);

            // ── Shadow for drag ──
            if (isBeingDragged)
            {
                for (int s = 4; s >= 1; s--)
                {
                    var sr = new Rect(inner.x - s, inner.y - s + 2, inner.width + s * 2, inner.height + s * 2);
                    EditorGUI.DrawRect(sr, new Color(0, 0, 0, 0.05f * s));
                }
            }

            // ── Row background ──
            Color rowBg = isBeingDragged
                ? Color.Lerp(RowHover, DragGlow, 0.5f)
                : selected ? RowSelected
                : hovered ? Color.Lerp(RowNormal, RowHover, _hoverAlpha)
                : RowNormal;

            EditorGUI.DrawRect(inner, rowBg);

            // ── Type accent bar (left edge) ──
            float accentW = selected ? 3f : 2f;
            float accentAlpha = selected ? 1f : (hovered ? 0.7f : 0.4f);
            var accentColor = new Color(accent.r, accent.g, accent.b, accent.a * accentAlpha);
            EditorGUI.DrawRect(new Rect(inner.x, inner.y + 4, accentW, inner.height - 8), accentColor);

            // ── Bottom separator ──
            if (!isBeingDragged)
                EditorGUI.DrawRect(new Rect(inner.x + 8, inner.yMax - 1, inner.width - 16, 1), RowSeparator);

            // ── Icon ──
            float iconPad = accentW + 10f;
            float iconSize = 26f * Mathf.Min(1f, _data.RowScale);
            var iconRect = new Rect(inner.x + iconPad, inner.center.y - iconSize / 2, iconSize, iconSize);
            DrawItemIcon(iconRect, item, obj);

            // ── Name + path ──
            float nameX = iconRect.xMax + 8;
            float rightPad = 28f;

            bool showPath = hovered && !string.IsNullOrEmpty(item.AssetPath);
            float nameH = showPath ? inner.height * 0.55f : inner.height;
            var nameRect = new Rect(nameX, inner.y, inner.xMax - nameX - rightPad, nameH);

            var prevColor = GUI.color;
            if (selected)
            {
                GUI.color = IsDark ? new Color(1f, 0.78f, 0.5f) : new Color(0.7f, 0.35f, 0.05f);
                GUI.Label(nameRect, item.Name, NameSelectedStyle);
            }
            else
            {
                GUI.color = TextPrimary;
                GUI.Label(nameRect, item.Name, NameStyle);
            }
            GUI.color = prevColor;

            // Asset path on hover (second line)
            if (showPath)
            {
                var pathRect = new Rect(nameX, inner.y + nameH - 2, inner.xMax - nameX - rightPad, inner.height - nameH + 2);
                prevColor = GUI.color;
                GUI.color = TextPath;
                GUI.Label(pathRect, item.AssetPath, PathStyle);
                GUI.color = prevColor;
            }

            // ── Status label ──
            if (obj == null && !showPath)
            {
                float nameW = NameStyle.CalcSize(new GUIContent(item.Name)).x;
                var statusRect = new Rect(nameX + nameW + 6, inner.y + 2, 70, inner.height);
                prevColor = GUI.color;
                GUI.color = TextSecondary;
                GUI.Label(statusRect, item.IsDeleted ? "deleted" : "not loaded", StatusStyle);
                GUI.color = prevColor;
            }

            // ── Cross button ──
            if (hovered && !_drag.IsDragging && index >= 0)
            {
                float crossSize = 14f;
                var crossRect = new Rect(inner.xMax - 8 - crossSize, inner.center.y - crossSize / 2, crossSize, crossSize);
                bool crossHover = crossRect.Contains(evt.mousePosition);

                prevColor = GUI.color;
                GUI.color = crossHover
                    ? (IsDark ? new Color(1f, 0.4f, 0.4f, 0.9f) : new Color(0.8f, 0.2f, 0.2f, 0.8f))
                    : (IsDark ? new Color(1, 1, 1, 0.25f) : new Color(0, 0, 0, 0.25f));
                GUI.Label(crossRect, EditorGUIUtility.IconContent("CrossIcon"));
                GUI.color = prevColor;

                if (evt.type == EventType.MouseUp && crossHover)
                {
                    Undo.RecordObject(_data, "BillFav Remove Item");
                    _data.RemoveItem(index);
                    evt.Use();
                }
            }

            // ── Click to select ──
            if (hovered && evt.type == EventType.MouseUp && !_drag.IsDragging)
            {
                evt.Use();
                if (MouseDragDistance <= 2 && obj != null)
                    SelectItem(item, obj);
            }

            // ── Double-click to open ──
            if (hovered && evt.type == EventType.MouseDown && evt.clickCount == 2)
            {
                if (obj != null) AssetDatabase.OpenAsset(obj);
                evt.Use();
            }
        }

        // ───────────────────────────────────────────
        // Hover animation
        // ───────────────────────────────────────────

        void TickHover()
        {
            float target = (_hoveredRow >= 0) ? 1f : 0f;
            float dt = Mathf.Clamp((float)(EditorApplication.timeSinceStartup - _lastHoverTime), 0.001f, 0.05f);
            _lastHoverTime = EditorApplication.timeSinceStartup;
            _hoverAlpha = Mathf.MoveTowards(_hoverAlpha, target, 12f * dt);
            _hoveredRow = -1;
        }

        // ───────────────────────────────────────────
        // Icon (with subtle shadow)
        // ───────────────────────────────────────────

        void DrawItemIcon(Rect rect, BillFavItem item, Object obj)
        {
            Texture icon;
            if (item.IsFolder)
                icon = EditorGUIUtility.IconContent("Folder Icon").image;
            else if (obj != null)
                icon = AssetPreview.GetAssetPreview(obj) ?? AssetPreview.GetMiniThumbnail(obj);
            else
                icon = AssetPreview.GetMiniTypeThumbnail(item.ResolvedType);

            if (icon == null) return;

            var prev = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.12f);
            GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), icon, ScaleMode.ScaleToFit);
            GUI.color = prev;
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
        }

        // ───────────────────────────────────────────
        // Bottom bar: branding + page widget + badge
        // ───────────────────────────────────────────

        void DrawBottomBar(Rect totalRect)
        {
            // ── "BillFavorites" shimmer branding ──
            float brandingH = 18f;
            var brandingRect = new Rect(totalRect.x, totalRect.yMax - brandingH - 2, totalRect.width, brandingH);

            float t = (float)EditorApplication.timeSinceStartup;
            // Shimmer: a highlight that sweeps across the text
            float shimmerPos = Mathf.Repeat(t * 0.15f, 1.4f) - 0.2f; // sweeps every ~9 seconds
            string brandText = "BillFavorites";
            float textW = BrandingStyle.CalcSize(new GUIContent(brandText)).x;
            float textStartX = brandingRect.center.x - textW / 2;

            // Base text
            var prevColor = GUI.color;
            GUI.color = IsDark
                ? new Color(0.3f, 0.3f, 0.4f, 0.45f)
                : new Color(0.5f, 0.5f, 0.6f, 0.4f);
            GUI.Label(brandingRect, brandText, BrandingStyle);

            // Shimmer overlay — brighter text at shimmer position
            float shimmerAlpha = 0.35f;
            float shimmerWidth = textW * 0.3f;
            float shimmerX = textStartX + shimmerPos * (textW + shimmerWidth) - shimmerWidth;
            var shimmerRect = new Rect(
                Mathf.Max(shimmerX, textStartX),
                brandingRect.y,
                Mathf.Min(shimmerWidth, textStartX + textW - Mathf.Max(shimmerX, textStartX)),
                brandingRect.height);

            if (shimmerRect.width > 0)
            {
                GUI.BeginClip(shimmerRect);
                var offsetRect = new Rect(brandingRect.x - shimmerRect.x, 0, brandingRect.width, brandingRect.height);
                GUI.color = IsDark
                    ? new Color(1f, 0.7f, 0.35f, shimmerAlpha)
                    : new Color(0.8f, 0.45f, 0.1f, shimmerAlpha);
                GUI.Label(offsetRect, brandText, BrandingStyle);
                GUI.EndClip();
            }

            GUI.color = prevColor;

            // ── Page widget pill ──
            DrawPageWidget(totalRect, brandingH + 5);
        }

        void DrawPageWidget(Rect totalRect, float bottomOffset)
        {
            var page = _data.ActivePage;
            float height = 24f;
            int itemCount = page.Items.Count;

            float nameWidth = PillStyle.CalcSize(new GUIContent(page.Name)).x;
            float widgetWidth = nameWidth + 56;
            var widgetRect = new Rect(
                totalRect.center.x - widgetWidth / 2,
                totalRect.yMax - height - bottomOffset,
                widgetWidth, height);

            // Glass pill
            EditorGUI.DrawRect(widgetRect, PillBg);
            DrawBorder(widgetRect, PillBorder);

            // Page name
            var prevColor = GUI.color;
            GUI.color = TextSecondary;
            GUI.Label(widgetRect, page.Name, PillStyle);
            GUI.color = prevColor;

            // ── Item count badge ──
            if (itemCount > 0)
            {
                string countText = itemCount.ToString();
                float badgeW = Mathf.Max(16, BadgeStyle.CalcSize(new GUIContent(countText)).x + 8);
                float badgeH = 14f;
                var badgeRect = new Rect(
                    widgetRect.xMax - badgeW / 2 - 2,
                    widgetRect.y - badgeH / 2 + 2,
                    badgeW, badgeH);

                EditorGUI.DrawRect(badgeRect, BadgeBg);
                prevColor = GUI.color;
                GUI.color = IsDark ? new Color(0.9f, 0.92f, 1f) : new Color(1, 1, 1, 0.95f);
                GUI.Label(badgeRect, countText, BadgeStyle);
                GUI.color = prevColor;
            }

            // Chevrons
            float chevronSize = 14f;
            float chevronPad = 6f;

            if (_data.ActivePageIndex > 0)
            {
                var leftIcon = new Rect(widgetRect.x + chevronPad, widgetRect.center.y - chevronSize / 2, chevronSize, chevronSize);
                prevColor = GUI.color;
                GUI.color = new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b,
                    0.6f * _anim.PrevPageBrightness);
                GUI.DrawTexture(leftIcon, EditorGUIUtility.IconContent("d_tab_prev").image);
                GUI.color = prevColor;

                var leftClick = new Rect(widgetRect.x, widgetRect.y, chevronSize + chevronPad * 2, widgetRect.height);
                if (Event.current.type == EventType.MouseUp && leftClick.Contains(Event.current.mousePosition))
                {
                    _data.ActivePageIndex--;
                    _anim.FlashPrevPage();
                    Event.current.Use();
                }
            }

            {
                var rightIcon = new Rect(widgetRect.xMax - chevronPad - chevronSize, widgetRect.center.y - chevronSize / 2, chevronSize, chevronSize);
                prevColor = GUI.color;
                GUI.color = new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b,
                    0.6f * _anim.NextPageBrightness);
                GUI.DrawTexture(rightIcon, EditorGUIUtility.IconContent("d_tab_next").image);
                GUI.color = prevColor;

                var rightClick = new Rect(widgetRect.xMax - chevronSize - chevronPad * 2, widgetRect.y, chevronSize + chevronPad * 2, widgetRect.height);
                if (Event.current.type == EventType.MouseUp && rightClick.Contains(Event.current.mousePosition))
                {
                    _data.ActivePageIndex++;
                    _anim.FlashNextPage();
                    Event.current.Use();
                }
            }
        }

        // ───────────────────────────────────────────
        // Selection — mirrors vFav
        // ───────────────────────────────────────────

        static readonly System.Type t_ProjectBrowser =
            typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");

        void SelectItem(BillFavItem item, Object obj)
        {
            if (item.IsSceneObject) { Selection.activeObject = obj; return; }
            if (item.IsFolder) { OpenFolderInBrowser(item.AssetPath); return; }

            var browser = GetProjectBrowser();
            if (browser != null && GetBrowserViewMode(browser) == 1)
            {
                var parentPath = System.IO.Path.GetDirectoryName(item.AssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parentPath))
                    OpenFolderInBrowser(parentPath, browser);
            }
            Selection.activeObject = obj;
        }

        static void OpenFolderInBrowser(string folderPath, EditorWindow browser = null)
        {
            var folderAsset = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
            if (folderAsset == null) return;
            browser ??= GetProjectBrowser();
            if (browser == null) { Selection.activeObject = folderAsset; return; }

            if (GetBrowserViewMode(browser) == 1)
            {
                try
                {
                    var mi = t_ProjectBrowser.GetMethod("SetFolderSelection",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
#if UNITY_6000_3_OR_NEWER
                    mi?.Invoke(browser, new object[] { new[] { (EntityId)folderAsset.GetInstanceID() }, false });
#else
                    mi?.Invoke(browser, new object[] { new[] { folderAsset.GetInstanceID() }, false });
#endif
                }
                catch { Selection.activeObject = folderAsset; }
            }
            else
            {
                Selection.activeObject = folderAsset;
                try
                {
                    var tree = t_ProjectBrowser.GetField("m_AssetTree", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(browser);
                    var data = tree?.GetType().GetProperty("data")?.GetValue(tree);
#if UNITY_6000_3_OR_NEWER
                    data?.GetType().GetMethod("SetExpanded", new[] { typeof(EntityId), typeof(bool) })
                        ?.Invoke(data, new object[] { (EntityId)folderAsset.GetInstanceID(), true });
#else
                    data?.GetType().GetMethod("SetExpanded", new[] { typeof(int), typeof(bool) })
                        ?.Invoke(data, new object[] { folderAsset.GetInstanceID(), true });
#endif
                }
                catch { }
            }
        }

        static EditorWindow GetProjectBrowser()
        {
            if (t_ProjectBrowser == null) return null;
            var all = Resources.FindObjectsOfTypeAll(t_ProjectBrowser);
            return all.Length > 0 ? all[0] as EditorWindow : null;
        }

        static int GetBrowserViewMode(EditorWindow browser)
        {
            try
            {
                var fi = t_ProjectBrowser.GetField("m_ViewMode", BindingFlags.Instance | BindingFlags.NonPublic);
                return fi != null ? (int)fi.GetValue(browser) : 0;
            }
            catch { return 0; }
        }

        // ───────────────────────────────────────────
        // Keyboard & scroll
        // ───────────────────────────────────────────

        void HandleKeys(Event evt, Rect rect)
        {
            if (!rect.Contains(evt.mousePosition)) return;
            if (evt.type != EventType.KeyDown) return;

            if (BillFavPrefs.ArrowKeysEnabled)
            {
                if (evt.keyCode == KeyCode.LeftArrow && _data.ActivePageIndex > 0)
                { _data.ActivePageIndex--; _anim.FlashPrevPage(); evt.Use(); }
                if (evt.keyCode == KeyCode.RightArrow)
                { _data.ActivePageIndex++; _anim.FlashNextPage(); evt.Use(); }
            }
            if (BillFavPrefs.NumberKeysEnabled && !EditorGUIUtility.editingTextField)
            {
                int num = (int)evt.keyCode - 48;
                if (num == 0) num = 10;
                if (num >= 1 && num <= 10)
                { _data.ActivePageIndex = num - 1; evt.Use(); }
            }
        }

        void HandlePageScroll(Event evt)
        {
            if (!BillFavPrefs.PageScrollEnabled) return;
            if (evt.type != EventType.ScrollWheel) return;
            float delta = evt.delta.y;
            if (delta == 0 && evt.shift) delta = evt.delta.x;
            if (delta < 0 && _data.ActivePageIndex > 0)
            { _data.ActivePageIndex--; _anim.FlashPrevPage(); evt.Use(); }
            if (delta > 0)
            { _data.ActivePageIndex++; _anim.FlashNextPage(); evt.Use(); }
        }

        // ───────────────────────────────────────────
        // Mouse state
        // ───────────────────────────────────────────

        void UpdateMouseState(Event evt, Rect rect)
        {
            _mousePos = evt.mousePosition;
            if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
            {
                _mouseDown = true;
                _mouseDownPos = evt.mousePosition;
                _doubleClick = evt.clickCount == 2;
            }
            if (evt.type == EventType.MouseUp)
            {
                _mouseDown = false;
                _doubleClick = false;
            }
        }

        Vector2 MouseRowsPos => new(_mousePos.x, _mousePos.y + (_data?.ActivePage?.ScrollPos ?? 0));
        float MouseDragDistance => (_mousePos - _mouseDownPos).magnitude;

        // ───────────────────────────────────────────
        // Draw helpers
        // ───────────────────────────────────────────

        void DrawEmptyHint(Rect rect)
        {
            var prev = GUI.color;
            GUI.color = TextSecondary;
            GUI.Label(new Rect(rect.x, rect.center.y - 26, rect.width, 20),
                "Drop folders, assets", EmptyHintStyle);
            GUI.Label(new Rect(rect.x, rect.center.y - 8, rect.width, 20),
                "or GameObjects here", EmptyHintStyle);
            GUI.color = prev;
        }

        void DrawScrollFade(Rect rect, float scrollPos)
        {
            float fadeH = 16f;
            float topAlpha = Mathf.Clamp01(scrollPos / 20f);
            if (topAlpha > 0.01f)
            {
                for (int i = 0; i < (int)fadeH; i++)
                {
                    float a = (1f - i / fadeH) * topAlpha * PanelBg.a;
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y + i, rect.width, 1),
                        new Color(PanelBg.r, PanelBg.g, PanelBg.b, a));
                }
            }

            float bottomH = 40f;
            for (int i = 0; i < (int)bottomH; i++)
            {
                float a = (i / bottomH) * PanelBg.a;
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - bottomH + i, rect.width, 1),
                    new Color(PanelBg.r, PanelBg.g, PanelBg.b, a));
            }
        }

        static void DrawBorder(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
        }
    }
}
#endif
