using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Thư viện builder dùng chung cho mọi HubInstaller/*Installer.
    /// Mọi số liệu theo Docs/UI_REDESIGN_SPEC.md (ref 1080×1920). Không chế màu ngoài UITheme.
    /// Sprites structural do UISpriteFactory sinh (rounded/pill/glow/dashed) — không dùng stock UISprite.
    /// </summary>
    public static class UIKit
    {
        public enum Anch { TL, TC, TR, ML, C, MR, BL, BC, BR }

        // ---------- sprites (UISpriteFactory, fallback built-in nếu chưa generate) ----------
        static Sprite S(string name, ref Sprite cache)
        {
            if (cache == null)
            {
                UISpriteFactory.EnsureAll();
                cache = UISpriteFactory.Load(name);
                if (cache == null) Debug.LogWarning($"[UIKit] Thiếu sprite '{name}' — fallback UISprite.");
            }
            return cache != null ? cache : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        static Sprite _r24, _r32, _f24, _f32, _dash, _pill, _circle, _ring, _glow, _gold, _goldR, _vign, _diag;
        public static Sprite Rounded24 => S("rounded_24", ref _r24);
        public static Sprite Rounded32 => S("rounded_32", ref _r32);
        public static Sprite Frame24   => S("frame_24", ref _f24);
        public static Sprite Frame32   => S("frame_32", ref _f32);
        public static Sprite Dashed    => S("rounded_dashed", ref _dash);
        public static Sprite Pill      => S("pill", ref _pill);
        public static Sprite Circle    => S("circle", ref _circle);
        public static Sprite Ring      => S("ring_thin", ref _ring);
        public static Sprite Glow      => S("glow_soft", ref _glow);
        public static Sprite GradGold  => S("grad_gold_v", ref _gold);
        public static Sprite GradGoldRounded => S("grad_gold_rounded", ref _goldR);
        public static Sprite RedVignette => S("grad_red_vignette", ref _vign);
        public static Sprite DiagonalBg  => S("bg_diagonal", ref _diag);

        // alias cũ (code chưa migrate) — giờ trỏ về sprite thật
        public static Sprite Rounded => Rounded24;

        // ---------- Layer Lab GUI Pro-SuperCasual icons (reference trực tiếp, không copy/không sửa gốc) ----------
        const string IconDir = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Demo/Demo_Icon";
        static readonly System.Collections.Generic.Dictionary<string, Sprite> _iconCache = new();

        /// <summary>Icon semantic từ Layer Lab (vd "Icon_Coin", "Icon_Lock02"). Null nếu thiếu (caller tự fallback).</summary>
        public static Sprite Icon(string name)
        {
            if (_iconCache.TryGetValue(name, out var s) && s != null) return s;
            s = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{name}.png");
            if (s == null) Debug.LogWarning($"[UIKit] Thiếu Layer Lab icon '{name}'");
            _iconCache[name] = s;
            return s;
        }

        // ============================================================ layout
        public static RectTransform Rect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        static Vector2 AnchorVec(Anch a) => a switch
        {
            Anch.TL => new Vector2(0, 1),  Anch.TC => new Vector2(0.5f, 1),  Anch.TR => new Vector2(1, 1),
            Anch.ML => new Vector2(0, .5f),Anch.C  => new Vector2(0.5f, .5f),Anch.MR => new Vector2(1, .5f),
            Anch.BL => new Vector2(0, 0),  Anch.BC => new Vector2(0.5f, 0),  Anch.BR => new Vector2(1, 0),
            _ => new Vector2(0.5f, 0.5f),
        };

        /// <summary>Ghim rt theo anchor a, đặt pos + size (pivot = anchor).</summary>
        public static RectTransform Place(RectTransform rt, Anch a, Vector2 pos, Vector2 size)
        {
            var v = AnchorVec(a);
            rt.anchorMin = rt.anchorMax = rt.pivot = v;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Full(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Dải ngang bám mép trên: height, y (âm = xuống), padding trái/phải.</summary>
        public static RectTransform StretchTop(RectTransform rt, float height, float y, float padL = 0, float padR = 0)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(padL, 0); rt.offsetMax = new Vector2(-padR, 0);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(0, y);
            return rt;
        }

        public static RectTransform StretchBottom(RectTransform rt, float height, float y, float padL = 0, float padR = 0)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(padL, 0); rt.offsetMax = new Vector2(-padR, 0);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(0, y);
            return rt;
        }

        // ============================================================ primitives
        public static Image Image(RectTransform parent, string name, Sprite sprite, Color color, bool ray = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = ray;
            // 9-slice chỉ khi sprite có border; circle/gradient để Simple cho khỏi méo
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Text(RectTransform parent, string name, string text, float size,
            Color color, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style; t.alignment = align;
            t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Lock icon (Layer Lab Icon_Lock02; fallback shape tự vẽ nếu asset thiếu).</summary>
        public static RectTransform LockGlyph(RectTransform parent, float size = 56)
        {
            var root = Rect("LockGlyph", parent);
            Place(root, Anch.C, Vector2.zero, new Vector2(size, size));
            var sprite = Icon("Icon_Lock02");
            if (sprite != null)
            {
                var img = Image(root, "Lock", sprite, Color.white, false);
                Full(img.rectTransform);
                img.preserveAspect = true;
            }
            else
            {
                var shackle = Image(root, "Shackle", Ring, UITheme.TextDim, false);
                Place(shackle.rectTransform, Anch.TC, new Vector2(0, 4), new Vector2(size * 0.55f, size * 0.55f));
                var body = Image(root, "Body", Rounded24, UITheme.TextDim, false);
                Place(body.rectTransform, Anch.BC, Vector2.zero, new Vector2(size * 0.8f, size * 0.55f));
            }
            return root;
        }

        /// <summary>Image icon preserveAspect từ Layer Lab; fallback rounded surface2 nếu thiếu.</summary>
        public static Image IconImage(RectTransform parent, string name, string iconName, Anch a, Vector2 pos, Vector2 size, Color? tint = null)
        {
            var sprite = Icon(iconName);
            var img = Image(parent, name, sprite != null ? sprite : Rounded24,
                sprite != null ? (tint ?? Color.white) : UITheme.Surface2, false);
            Place(img.rectTransform, a, pos, size);
            img.preserveAspect = sprite != null;
            return img;
        }

        // ============================================================ components §3

        /// <summary>Nút bevel: đáy màu Lo hở 10px, mặt màu Hi (rounded). Trả Button (targetGraphic = mặt).</summary>
        public static Button Button(RectTransform parent, string name, string label, Vector2 size, Anch a, Vector2 pos,
            Color face, Color edge, Color textColor, float fontSize)
        {
            var root = Image(parent, name, Rounded24, edge);
            Place(root.rectTransform, a, pos, size);
            var f = Image(root.rectTransform, "Face", Rounded24, face, false);
            Full(f.rectTransform, 0, 0, 0, UITheme.ButtonEdge);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = f;
            PressColors(btn);
            root.gameObject.AddComponent<UIFxPress>();
            var lbl = Text(f.rectTransform, "Label", label, fontSize, textColor, FontStyles.Bold, TextAlignmentOptions.Center);
            Full(lbl.rectTransform);
            return btn;
        }

        static void PressColors(Button btn)
        {
            var c = btn.colors;
            c.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            c.highlightedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            c.disabledColor = new Color(0.45f, 0.47f, 0.52f);
            btn.colors = c;
        }

        public static Button BtnPrimary(RectTransform p, string n, string label, Vector2 size, Anch a, Vector2 pos)
        {
            var b = Button(p, n, label, size, a, pos, Color.white, UITheme.GoldLo, UITheme.OnGold, UITheme.FontTitle);
            var face = (Image)b.targetGraphic;
            face.sprite = GradGoldRounded;             // gradient dọc goldHi→goldLo bake sẵn trong sprite rounded
            face.type = UnityEngine.UI.Image.Type.Sliced;
            return b;
        }

        public static Button BtnGreen(RectTransform p, string n, string label, Vector2 size, Anch a, Vector2 pos)
            => Button(p, n, label, size, a, pos, UITheme.Green, UITheme.GreenLo, Color.white, UITheme.FontSub);
        public static Button BtnDanger(RectTransform p, string n, string label, Vector2 size, Anch a, Vector2 pos)
            => Button(p, n, label, size, a, pos, UITheme.Danger, new Color(0.6f,0.16f,0.18f), Color.white, UITheme.FontSub);

        public static Button BtnGhost(RectTransform p, string n, string label, Vector2 size, Anch a, Vector2 pos)
        {
            var root = Image(p, n, Frame24, UITheme.Hairline);
            Place(root.rectTransform, a, pos, size);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = root;
            PressColors(btn);
            root.gameObject.AddComponent<UIFxPress>();
            var lbl = Text(root.rectTransform, "Label", label, UITheme.FontBody, UITheme.TextDim, FontStyles.Bold);
            Full(lbl.rectTransform);
            return btn;
        }

        /// <summary>Card surface + glow(tắt) + border(tắt). Trả về root; glow/border qua out.</summary>
        public static RectTransform Card(RectTransform parent, string name, Anch a, Vector2 pos, Vector2 size,
            out Image glow, out Image border, out Image bg)
        {
            var root = Rect(name, parent);
            Place(root, a, pos, size);
            glow = Image(root, "Glow", Glow, UITheme.Alpha(UITheme.Gold, UITheme.GlowAlpha), false);
            Full(glow.rectTransform, -24, -24, -24, -24);
            glow.gameObject.SetActive(false);
            bg = Image(root, "Bg", Rounded32, UITheme.Surface);
            Full(bg.rectTransform);
            border = Image(root, "Border", Frame32, UITheme.Green, false);
            Full(border.rectTransform);
            border.gameObject.SetActive(false);
            return root;
        }

        /// <summary>RarityCard item (súng/costume). Trả root + Button (nếu clickable).</summary>
        public static RectTransform RarityCard(RectTransform parent, string name, Anch a, Vector2 pos, Vector2 size,
            int rarity, string label, string price, bool locked, bool newDot, out Button btn)
        {
            var col = UITheme.RarityColor(rarity);
            var root = Rect(name, parent);
            Place(root, a, pos, size);

            var glow = Image(root, "Glow", Glow, UITheme.Alpha(col, UITheme.GlowAlpha), false);
            Full(glow.rectTransform, -24, -24, -24, -24);

            var bg = Image(root, "Bg", Rounded24, UITheme.Surface);
            Full(bg.rectTransform);
            btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            PressColors(btn);
            bg.gameObject.AddComponent<UIFxPress>();

            var border = Image(root, "Border", Frame24, col, false);
            Full(border.rectTransform);

            bool hasText = !string.IsNullOrEmpty(label) || !string.IsNullOrEmpty(price);
            float iconYOffset = hasText ? size.y * 0.1f : 0f;
            var icon = Image(root, "Icon", Rounded24, UITheme.Surface2, false);
            Place(icon.rectTransform, Anch.C, new Vector2(0, iconYOffset), size * 0.52f);

            if (!string.IsNullOrEmpty(label))
            {
                var l = Text(root, "Name", label, UITheme.FontLabel, UITheme.TextMain, FontStyles.Bold);
                Place(l.rectTransform, Anch.BC, new Vector2(0, string.IsNullOrEmpty(price) ? 18 : 74), new Vector2(size.x - 24, 40));
            }
            if (!string.IsNullOrEmpty(price))
            {
                var chip = Image(root, "PriceChip", Pill, UITheme.Surface2, false);
                Place(chip.rectTransform, Anch.BC, new Vector2(0, 14), new Vector2(Mathf.Min(size.x * 0.62f, 200), 52));
                var coin = Image(chip.rectTransform, "Coin", Circle, UITheme.Gold, false);
                Place(coin.rectTransform, Anch.ML, new Vector2(12, 0), new Vector2(32, 32));
                var pv = Text(chip.rectTransform, "Val", price, UITheme.FontLabel, UITheme.Gold, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
                Full(pv.rectTransform, 48, 0, 16, 0);
            }
            if (newDot)
            {
                var d = Image(root, "Dot", Circle, UITheme.Danger, false);
                Place(d.rectTransform, Anch.TR, new Vector2(-6, -6), new Vector2(22, 22));
            }
            if (locked)
            {
                var mask = Image(root, "LockMask", Rounded24, new Color(0, 0, 0, 0.6f), false);
                Full(mask.rectTransform);
                LockGlyph(mask.rectTransform);
            }
            return root;
        }

        /// <summary>Pill tiền tệ glass. iconName (Layer Lab) → icon thật; null/thiếu → chấm màu. Trả TMP value label.</summary>
        public static TMP_Text CurrencyPill(RectTransform parent, string name, Color iconColor, Anch a, Vector2 pos, float width = 176, string iconName = null)
        {
            var pill = Image(parent, name, Pill, UITheme.Alpha(UITheme.Surface, 0.88f));
            Place(pill.rectTransform, a, pos, new Vector2(width, 72));
            var frame = Image(pill.rectTransform, "Hairline", Frame24, UITheme.Hairline, false);
            Full(frame.rectTransform);
            var iconSprite = iconName != null ? Icon(iconName) : null;
            var dot = Image(pill.rectTransform, "Icon",
                iconSprite != null ? iconSprite : Circle,
                iconSprite != null ? Color.white : iconColor, false);
            Place(dot.rectTransform, Anch.ML, new Vector2(14, 0), new Vector2(44, 44));
            dot.preserveAspect = iconSprite != null;
            var val = Text(pill.rectTransform, "Value", "0", 34, iconColor, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            Full(val.rectTransform, 66, 0, 24, 0);
            return val;
        }

        public static Toggle Toggle(RectTransform parent, string name, Anch a, Vector2 pos, bool on)
        {
            var track = Image(parent, name, Pill, on ? UITheme.Green : UITheme.Surface2);
            Place(track.rectTransform, a, pos, new Vector2(96, 52));
            var tg = track.gameObject.AddComponent<Toggle>();
            var knob = Image(track.rectTransform, "Knob", Circle, Color.white, false);
            // Knob anchor ML cố định — UIToggleVisual tween anchoredPosition.x giữa OFF=4 / ON=48
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0, 0.5f);
            knob.rectTransform.pivot = new Vector2(0, 0.5f);
            knob.rectTransform.sizeDelta = new Vector2(44, 44);
            knob.rectTransform.anchoredPosition = new Vector2(on ? 48 : 4, 0);
            tg.targetGraphic = track; tg.isOn = on;

            var visual = track.gameObject.AddComponent<UIToggleVisual>();
            visual.toggle = tg;
            visual.track = track;
            visual.knob = knob.rectTransform;
            visual.onColor = UITheme.Green;
            visual.offColor = UITheme.Surface2;
            return tg;
        }

        /// <summary>Tab phân đoạn (pill trong pill). Trả về Button[]; fills/labels để runtime đổi active state.</summary>
        public static Button[] SegmentedTabs(RectTransform parent, string name, Anch a, Vector2 pos, Vector2 size,
            string[] labels, Color activeColor, int active = 0)
            => SegmentedTabs(parent, name, a, pos, size, labels, activeColor, active, out _, out _);

        public static Button[] SegmentedTabs(RectTransform parent, string name, Anch a, Vector2 pos, Vector2 size,
            string[] labels, Color activeColor, int active, out Image[] fills, out TMP_Text[] tabLabels)
        {
            var bar = Image(parent, name, Pill, UITheme.Surface);
            Place(bar.rectTransform, a, pos, size);
            var btns = new Button[labels.Length];
            fills = new Image[labels.Length];
            tabLabels = new TMP_Text[labels.Length];
            float w = size.x / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                var seg = Image(bar.rectTransform, "Tab" + i, Pill, i == active ? activeColor : Color.clear, true);
                seg.rectTransform.anchorMin = seg.rectTransform.anchorMax = new Vector2(0, 0.5f);
                seg.rectTransform.pivot = new Vector2(0, 0.5f);
                seg.rectTransform.sizeDelta = new Vector2(w - 12, size.y - 12);
                seg.rectTransform.anchoredPosition = new Vector2(i * w + 6, 0);
                var b = seg.gameObject.AddComponent<Button>();
                b.targetGraphic = seg;
                var l = Text(seg.rectTransform, "L", labels[i], 28,
                    i == active ? Color.white : UITheme.TextDim, FontStyles.Bold);
                Full(l.rectTransform);
                btns[i] = b; fills[i] = seg; tabLabels[i] = l;
            }
            return btns;
        }

        /// <summary>RawImage hiển thị RenderTexture nhân vật phải giữ đúng ratio texture —
        /// fitter khoá width theo height, không cho stretch X/Y độc lập (UI handoff §RawImage).</summary>
        public static void RtAspect(GameObject rawGo, Texture tex)
        {
            var f = rawGo.GetComponent<AspectRatioFitter>();
            if (f == null) f = rawGo.AddComponent<AspectRatioFitter>();
            f.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            f.aspectRatio = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 1f;
        }

        public static Image ProgressBar(RectTransform parent, string name, Anch a, Vector2 pos, Vector2 size, float fill01, Color fill)
        {
            var track = Image(parent, name, Pill, UITheme.Surface2);
            Place(track.rectTransform, a, pos, size);
            var f = Image(track.rectTransform, "Fill", Pill, fill, false);
            f.rectTransform.anchorMin = new Vector2(0, 0); f.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1);
            f.rectTransform.offsetMin = Vector2.zero; f.rectTransform.offsetMax = Vector2.zero;
            return f;
        }

        public static void ProgressPips(RectTransform parent, string name, Anch a, Vector2 pos, int total, int filled)
        {
            var row = Rect(name, parent);
            Place(row, a, pos, new Vector2(total * 48, 16));
            for (int i = 0; i < total; i++)
            {
                var pip = Image(row, "Pip" + i, Rounded24, i < filled ? UITheme.Green : UITheme.Surface2, false);
                pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(0, 0.5f);
                pip.rectTransform.pivot = new Vector2(0, 0.5f);
                pip.rectTransform.sizeDelta = new Vector2(40, 16);
                pip.rectTransform.anchoredPosition = new Vector2(i * 48, 0);
            }
        }

        /// <summary>HeaderBack: nút ← + title. Trả về Button back.</summary>
        public static Button HeaderBack(RectTransform parent, string title, out TMP_Text titleLabel)
        {
            var back = Image(parent, "BackBtn", Rounded24, UITheme.Rarity[2]);
            Place(back.rectTransform, Anch.TL, new Vector2(32, -32), new Vector2(88, 88));
            var b = back.gameObject.AddComponent<Button>();
            b.targetGraphic = back;
            PressColors(b);
            back.gameObject.AddComponent<UIFxPress>();
            var arrow = Text(back.rectTransform, "Icon", "←", 44, Color.white, FontStyles.Bold);
            Full(arrow.rectTransform);
            titleLabel = Text(parent, "Title", title, UITheme.FontHeader, UITheme.TextMain,
                FontStyles.Bold | FontStyles.UpperCase);
            Place(titleLabel.rectTransform, Anch.TC, new Vector2(0, -48), new Vector2(700, 80));
            return b;
        }

        /// <summary>Modal: Dim + Panel giữa (rounded). Trả về (root, panel).</summary>
        public static RectTransform Modal(RectTransform parent, string name, float panelWidth, out RectTransform panel, out Button dim)
        {
            var root = Rect(name, parent);
            Full(root);
            var d = Image(root, "Dim", null, new Color(0, 0, 0, 0.7f));
            Full(d.rectTransform);
            dim = d.gameObject.AddComponent<Button>();
            dim.targetGraphic = d;
            panel = Image(root, "Panel", Rounded32, UITheme.Surface).rectTransform;
            Place(panel, Anch.C, Vector2.zero, new Vector2(panelWidth, 200));
            return root;
        }

        /// <summary>ScrollRect dọc, viewport = FULL, content top-anchored. Trả về content RectTransform.</summary>
        public static RectTransform VScroll(RectTransform parent, string name, RectTransform area, out ScrollRect scroll)
        {
            var root = Rect(name, parent);
            if (area == parent)
            {
                // area == parent nghĩa là "scroll chiếm trọn parent" — copy offset của parent
                // vào con sẽ áp inset 2 lần (BUG A Shop cũ), nên Full thay vì copy.
                Full(root);
            }
            else
            {
                root.anchorMin = area.anchorMin; root.anchorMax = area.anchorMax;
                root.pivot = area.pivot; root.anchoredPosition = area.anchoredPosition;
                root.sizeDelta = area.sizeDelta; root.offsetMin = area.offsetMin; root.offsetMax = area.offsetMax;
            }
            var mask = root.gameObject.AddComponent<RectMask2D>();
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            var content = Rect("Content", root);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1); content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0, 0);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20; vlg.padding = new RectOffset(32, 32, 0, 40);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        // ---------- wiring helpers ----------
        public static void Wire(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"[UIKit] Thiếu field '{prop}' trên {so.targetObject.GetType().Name}");
        }
    }
}
