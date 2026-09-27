using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M9 UI kit v2: the owner-approved Meta v2 design system as editor building blocks. Screen
    /// builders compose these, so every screen shares the same colours by role, radii, lip buttons
    /// and text rules. Sizes are given in mockup pixels (390-wide artboards) and scaled by
    /// <see cref="K"/> to the 1080x1920 canvas, so a builder reads like the mockup it came from.
    ///
    /// Rules baked in:
    /// - Display text (outline font) is always light. Dark text only ever uses the body font.
    /// - A shape inside another is never rounder than its parent (radius tokens below).
    /// - Colour has a role: yellow = main action / coin, gem purple = spend gems, green = claim or
    ///   owned, blue = secondary navigation, red = sale badge or alert only.
    /// </summary>
    public static class UIKitV2
    {
        public const float K = 1080f / 390f;

        // Radius tokens in mockup px (panel 16, card 14, tile 8, button 12).
        public const float RPanel = 16f, RCard = 14f, RTile = 8f, RButton = 12f, RTag = 5f;

        public enum Role { Primary, Gem, Claim, Info, Quiet, Danger }

        const string SpriteDir = "Assets/_Project/UI/Sprites/";
        const string FontDir = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Fonts/";
        public const string IconDir = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/Icon_ItemIcons/128/";

        public static readonly Color Ground = Hex("262a36"), Card = Hex("2f3544"), Deep = Hex("1f2330"), Edge = Hex("3a4152"),
            Ink = Hex("f4f1ea"), Dim = Hex("9aa1b0"), OutlineInk = Hex("1b1e27"),
            Yellow = Hex("ffc93c"), YellowLip = Hex("e0a21c"), OnYellow = Hex("2a1d00"),
            Gem = Hex("b77cff"), GemLip = Hex("8f52e6"), OnGem = Hex("1f1030"),
            Green = Hex("5bd68a"), GreenLip = Hex("34a865"), OnGreen = Hex("10331d"),
            Blue = Hex("4fa3ff"), BlueLip = Hex("2f78c9"), Red = Hex("e5484d"), RedLip = Hex("9e2a2e"),
            Gold3A = Hex("3a331f"), GoldText = Hex("ffd98a");

        public static readonly Color[] Rarity = { Hex("9aa3b2"), Hex("4caf6e"), Hex("4fa3f7"), Hex("8b7bd8"), Hex("f2994a") };

        static TMP_FontAsset _display, _body;
        const string DisplayPlainPath = "Assets/_Project/UI/Theme/DisplayPlain.mat";
        static Material _plain;
        /// <summary>The display font without outline or drop line, for dark text on light cards.</summary>
        public static Material DisplayPlain
        {
            get
            {
                if (_plain != null) return _plain;
                _plain = AssetDatabase.LoadAssetAtPath<Material>(DisplayPlainPath);
                if (_plain != null) return _plain;
                _plain = new Material(DisplayFont.material) { name = "Cairo Display Plain" };
                _plain.DisableKeyword("OUTLINE_ON"); _plain.DisableKeyword("UNDERLAY_ON");
                _plain.SetFloat("_OutlineWidth", 0f); _plain.SetFloat("_FaceDilate", 0.12f);
                System.IO.Directory.CreateDirectory("Assets/_Project/UI/Theme");
                AssetDatabase.CreateAsset(_plain, DisplayPlainPath);
                return _plain;
            }
        }
        public static TMP_FontAsset DisplayFont => _display != null ? _display : (_display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "Cairo_Line_Black SDF_Light.asset"));
        public static TMP_FontAsset BodyFont => _body != null ? _body : (_body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "Cairo SDF.asset"));

        public static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.magenta;
        public static float Px(float mockupPx) => mockupPx * K;

        public static Sprite Spr(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");
        public const string PictoDir = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/Icon_PictoIcons/128/";

        /// <summary>White line glyph (lock, gear, arrow, hand...), tinted by the caller.</summary>
        public static Sprite PictoSprite(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(PictoDir + "PictoIcon_" + name + ".Png");
            return s != null ? s : AssetDatabase.LoadAssetAtPath<Sprite>(PictoDir + "PictoIcon_" + name + ".png");
        }

        public static Image Picto(RectTransform rt, string name, Color? tint = null)
        {
            var img = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.sprite = PictoSprite(name); img.preserveAspect = true; img.raycastTarget = false;
            img.color = tint ?? Ink;
            // Line icons follow text rules: ink on light surfaces, as built elsewhere.
            if (!NoTheme && Key(img.color) == Key(Ink))
            {
                var back = Backdrop(rt);
                var bt = back != null ? back.GetComponent<ThemeTint>() : null;
                if (bt != null && SurfaceRoles.Contains(bt.role)) Theme(img, img.color, ThemeRole.TextOnSurface);
            }
            else if (!NoTheme && Key(img.color) == Key(Dim)) Theme(img, img.color);
            return img;
        }

        public static Sprite Icon(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "ItemIcon_" + name + ".Png");
            return s != null ? s : AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "ItemIcon_" + name + ".png");
        }

        // ------------------------------------------------------------ layout
        /// <summary>A screen root at the reference size, so bands and insets have real widths while
        /// the builder runs (a default 100 px root gives negative widths and empty text).</summary>
        public static RectTransform ScreenRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1080f, 1920f);
            return rt;
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Stretch to the parent with insets in mockup px.</summary>
        public static RectTransform Fill(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(Px(l), Px(b)); rt.offsetMax = new Vector2(-Px(r), -Px(t));
            return rt;
        }

        /// <summary>A full-width band from the top: y and height in mockup px, side insets.</summary>
        public static RectTransform TopBand(RectTransform rt, float y, float h, float l = 0, float r = 0)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(Px(l), -Px(y + h)); rt.offsetMax = new Vector2(-Px(r), -Px(y));
            return rt;
        }

        /// <summary>A full-width band from the bottom.</summary>
        public static RectTransform BottomBand(RectTransform rt, float y, float h, float l = 0, float r = 0)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(Px(l), Px(y)); rt.offsetMax = new Vector2(-Px(r), Px(y + h));
            return rt;
        }

        /// <summary>Anchored box: anchor point (0..1), pivot, position and size in mockup px.</summary>
        public static RectTransform Box(RectTransform rt, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(Px(x), Px(y)); rt.sizeDelta = new Vector2(Px(w), Px(h));
            return rt;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float gap, TextAnchor align = TextAnchor.MiddleLeft, bool expandW = false, float padL = 0, float padR = 0)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = Px(gap); h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = expandW; h.childForceExpandHeight = true;
            h.padding = new RectOffset(Mathf.RoundToInt(Px(padL)), Mathf.RoundToInt(Px(padR)), 0, 0);
            return h;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, float gap, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = Px(gap); v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        public static LayoutElement Size(RectTransform rt, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = Px(w); le.minWidth = Px(w); }
            if (h >= 0) { le.preferredHeight = Px(h); le.minHeight = Px(h); }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static GridLayoutGroup Grid(RectTransform rt, int columns, float cellH, float gap)
        {
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = columns;
            g.spacing = new Vector2(Px(gap), Px(gap));
            g.cellSize = new Vector2(0, Px(cellH));   // width set by GridFit
            rt.gameObject.AddComponent<GridFit>().Columns = columns;
            return g;
        }

        // ------------------------------------------------------------ theme
        /// <summary>
        /// Every kit colour has a theme role (owner 2026-09-27: Light Sky default, Dark, Candy,
        /// Meadow). Graphics built with one of these colours get a <see cref="ThemeTint"/>, so a
        /// theme change recolours the whole UI at runtime. Colours outside the map (banner art,
        /// runtime rarity) stay as built.
        /// </summary>
        static Dictionary<string, ThemeRole> _roles;
        static Dictionary<string, ThemeRole> Roles => _roles ??= BuildRoles();
        /// <summary>Set while building something that must stay dark in every theme (revive overlay).</summary>
        public static bool NoTheme;

        static string Key(Color c) => ColorUtility.ToHtmlStringRGB(c);

        static Dictionary<string, ThemeRole> BuildRoles()
        {
            var m = new Dictionary<string, ThemeRole>();
            void A(Color c, ThemeRole r) { var k = Key(c); if (!m.ContainsKey(k)) m[k] = r; }
            A(Ground, ThemeRole.Ground); A(Card, ThemeRole.Card); A(Deep, ThemeRole.Deep); A(Edge, ThemeRole.Edge);
            A(Ink, ThemeRole.Ink); A(Dim, ThemeRole.Dim); A(OutlineInk, ThemeRole.Outline);
            A(Yellow, ThemeRole.Primary); A(YellowLip, ThemeRole.PrimaryLip); A(OnYellow, ThemeRole.PrimaryOn);
            A(Gem, ThemeRole.Gem); A(GemLip, ThemeRole.GemLip); A(OnGem, ThemeRole.GemOn);
            A(Green, ThemeRole.Claim); A(GreenLip, ThemeRole.ClaimLip); A(OnGreen, ThemeRole.ClaimOn);
            A(Blue, ThemeRole.Info); A(BlueLip, ThemeRole.InfoLip);
            A(Red, ThemeRole.Danger); A(RedLip, ThemeRole.DangerLip);
            A(Gold3A, ThemeRole.PrimaryTint); A(GoldText, ThemeRole.Gold);
            for (int i = 0; i < Rarity.Length; i++) A(Rarity[i], ThemePalette.Rarity(i));
            // Dark tints used by builders for tiles and cards.
            foreach (var h in new[] { "263a2e", "1f3a26", "2c5236" }) A(Hex(h), ThemeRole.ClaimTint);
            foreach (var h in new[] { "3a2a55", "262040" }) A(Hex(h), ThemeRole.GemTint);
            foreach (var h in new[] { "1f3150", "1f3a52" }) A(Hex(h), ThemeRole.InfoTint);
            foreach (var h in new[] { "4a1f22" }) A(Hex(h), ThemeRole.DangerTint);
            foreach (var h in new[] { "4a2a0c" }) A(Hex(h), ThemeRole.LegendTint);
            foreach (var h in new[] { "b9bfcc", "6c7384", "5a6275" }) A(Hex(h), ThemeRole.Dim);
            return m;
        }

        static readonly HashSet<ThemeRole> SurfaceRoles = new()
        {
            ThemeRole.Card, ThemeRole.Deep, ThemeRole.Edge, ThemeRole.Nav, ThemeRole.PrimaryTint, ThemeRole.GemTint,
            ThemeRole.ClaimTint, ThemeRole.InfoTint, ThemeRole.DangerTint,
        };

        /// <summary>Tags a graphic with the role of the colour it was built with.</summary>
        public static void Theme(Graphic g, Color c, ThemeRole force = ThemeRole.None)
        {
            if (NoTheme || g == null) return;
            var role = force != ThemeRole.None ? force : Roles.TryGetValue(Key(c), out var r) ? r : ThemeRole.None;
            if (role == ThemeRole.None) return;
            var t = g.GetComponent<ThemeTint>() ?? g.gameObject.AddComponent<ThemeTint>();
            t.role = role; t.alpha = c.a;
        }

        /// <summary>Nearest ancestor graphic that draws a background (skips clear hit areas).</summary>
        static Graphic Backdrop(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                var g = p.GetComponent<Image>() as Graphic ?? p.GetComponent<RawImage>();
                if (g != null && g.color.a > 0.3f && g.enabled) return g;
            }
            return null;
        }

        /// <summary>Text: follows the theme when it sits on a themed surface (or the screen), keeps
        /// its colour when it sits on fixed art (banners, rarity tiles).</summary>
        static void ThemeText(TMP_Text t, Color c, bool display)
        {
            if (NoTheme) return;
            var back = Backdrop(t.transform);
            var tint = back != null ? back.GetComponent<ThemeTint>() : null;
            bool onSurface = tint != null && SurfaceRoles.Contains(tint.role);
            bool onTheme = back == null || tint != null || back.GetComponent<MenuBackgroundView>() != null;
            if (display)
            {
                // Outline font: white on colour, ink on light surfaces (without the outline there).
                if (onSurface && Key(c) == Key(Ink))
                {
                    Theme(t, c, ThemeRole.TextOnSurface);
                    var tt = t.GetComponent<ThemeTint>();
                    if (tt != null) { tt.outlined = DisplayFont.material; tt.plain = DisplayPlain; }
                }
                return;
            }
            if (!onTheme) return;
            var k = Key(c);
            if (tint != null && tint.role == ThemeRole.QuietFace && k != Key(Ink)) { Theme(t, c, ThemeRole.OnQuiet); return; }
            if (k == Key(Ink)) Theme(t, c, onSurface ? ThemeRole.TextOnSurface : ThemeRole.Ink);
            else if (k == Key(Yellow) || k == Key(GoldText)) Theme(t, c, ThemeRole.PrimaryText);
            else if (k == Key(Gem)) Theme(t, c, ThemeRole.GemText);
            else if (k == Key(Green)) Theme(t, c, ThemeRole.ClaimText);
            else if (k == Key(Blue)) Theme(t, c, ThemeRole.InfoText);
            else if (k == Key(Red)) Theme(t, c, ThemeRole.DangerText);
            else if (k == Key(Hex("c9bfe0")) || k == Key(Hex("ffd2e8"))) Theme(t, c, ThemeRole.Dim);
            else if (k == Key(Hex("ffb27a")) || k == Key(Hex("ffd98a"))) Theme(t, c, ThemeRole.PrimaryText);
            else if (k == Key(Hex("9fd0f5"))) Theme(t, c, ThemeRole.InfoText);
            else if (k == Key(Hex("7fe3a6"))) Theme(t, c, ThemeRole.ClaimText);
            else if (k == Key(Hex("c9a7ff"))) Theme(t, c, ThemeRole.GemText);
            else Theme(t, c);
        }

        /// <summary>Screen background: the animated MenuBackground shader behind everything.</summary>
        public static void ScreenBackground(RectTransform root)
        {
            Flat(root, Ground, true);
            var bg = Fill(Node(root, "Background"), 0, 0, 0, 0);
            bg.SetAsFirstSibling();
            var raw = bg.gameObject.AddComponent<RawImage>(); raw.raycastTarget = false;
            bg.gameObject.AddComponent<MenuBackgroundView>();
        }

        // ------------------------------------------------------------ surfaces
        /// <summary>A rounded surface: radius in mockup px, 9-slice scaled so corners stay round.</summary>
        public static Image Surface(RectTransform rt, Color color, float radius, bool ray = false)
        {
            var img = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.sprite = Spr("rounded_24");
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = ray;
            // rounded_24 draws a 24 px corner at multiplier 1 on the 1080 canvas.
            float mult = UITheme.MultiplierFor("rounded_24", Px(radius));
            img.pixelsPerUnitMultiplier = mult;
            var fit = rt.GetComponent<UISliceFit>() ?? rt.gameObject.AddComponent<UISliceFit>();
            fit.BaseMultiplier = mult;
            Theme(img, color);
            return img;
        }

        public static Image Flat(RectTransform rt, Color color, bool ray = false)
        {
            var img = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.sprite = null; img.color = color; img.raycastTarget = ray;
            Theme(img, color);
            return img;
        }

        public static Image IconImage(RectTransform rt, string icon, Color? tint = null)
        {
            var img = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.sprite = Icon(icon); img.preserveAspect = true; img.raycastTarget = false;
            img.color = tint ?? Color.white;
            return img;
        }

        // ------------------------------------------------------------ text
        /// <summary>Display text: outline font, always light (the outline is black).</summary>
        public static TextMeshProUGUI Title(RectTransform rt, string text, float sizePx, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var t = rt.GetComponent<TextMeshProUGUI>() ?? rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = DisplayFont; t.fontSharedMaterial = DisplayFont.material;
            var c = color ?? Ink;
            if (Luma(c) < 0.45f) c = Ink;   // never a dark fill on the black outline
            t.text = text; t.fontSize = Px(sizePx); t.color = c; t.alignment = align;
            // Overflow, not Ellipsis: Cairo's line height is ~1.5x the size, and Ellipsis drops the
            // whole line when that is taller than the box, leaving an empty label.
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
            ThemeText(t, c, true);
            return t;
        }

        /// <summary>Body text: plain font, any colour with enough contrast.</summary>
        public static TextMeshProUGUI Body(RectTransform rt, string text, float sizePx, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool bold = true)
        {
            var t = rt.GetComponent<TextMeshProUGUI>() ?? rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = BodyFont; t.fontSharedMaterial = BodyFont.material;
            t.text = text; t.fontSize = Px(sizePx); t.color = color ?? Ink; t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            // Overflow, not Ellipsis: Cairo's line height is ~1.5x the size, and Ellipsis drops the
            // whole line when that is taller than the box, leaving an empty label.
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
            ThemeText(t, t.color, false);
            return t;
        }

        /// <summary>Lets a label shrink (down to <paramref name="minFactor"/> of its size) instead of
        /// spilling out of its box when runtime text is longer than the mockup's.</summary>
        public static TextMeshProUGUI Shrink(TextMeshProUGUI t, float minFactor = 0.6f)
        {
            float max = t.fontSize;
            t.enableAutoSizing = true; t.fontSizeMax = max; t.fontSizeMin = Mathf.Max(8f, max * minFactor);
            // TMP only measures overflow against the box when it may wrap: without wrapping a long
            // single line never triggers the shrink.
            t.enableWordWrapping = true;
            return t;
        }

        /// <summary>Small caps label (dim, letter-spaced).</summary>
        public static TextMeshProUGUI Label(RectTransform rt, string text, Color? color = null, float sizePx = 11f)
        {
            var t = Body(rt, text.ToUpperInvariant(), sizePx, color ?? Dim);
            t.characterSpacing = 6f;
            return t;
        }

        public static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        // ------------------------------------------------------------ components
        public static (Color face, Color lip) RoleColors(Role r) => r switch
        {
            Role.Primary => (Yellow, YellowLip),
            Role.Gem => (Gem, GemLip),
            Role.Claim => (Green, GreenLip),
            Role.Info => (Blue, BlueLip),
            Role.Danger => (Red, RedLip),
            _ => (Card, Deep),
        };

        /// <summary>
        /// Lip button: the button's own image is the darker lip, the "Face" child sits 4 mockup px
        /// higher and carries the label. Returns the Button; the label is at Face/Label.
        /// </summary>
        public static Button Button(RectTransform rt, string label, Role role, float labelPx = 17f)
        {
            var (face, lip) = RoleColors(role);
            var lipImg = Surface(rt, lip, RButton, true);
            var faceRt = Fill(Node(rt, "Face"), 0, 0, 0, 0);
            faceRt.offsetMin = new Vector2(0, Px(4));
            var faceImg = Surface(faceRt, face, RButton);
            if (role == Role.Quiet) { Theme(lipImg, lip, ThemeRole.QuietLip); Theme(faceImg, face, ThemeRole.QuietFace); }
            var lbl = Title(Fill(Node(faceRt, "Label"), 6, 0, 6, 0), label, labelPx, Ink, TextAlignmentOptions.Center);
            Shrink(lbl);   // runtime labels ("COME BACK TOMORROW", prices) must never leave the button
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = lipImg; b.transition = Selectable.Transition.None;
            rt.gameObject.AddComponent<UIPressFeel>();
            return b;
        }

        /// <summary>Small rounded tag: body font, explicit colours.</summary>
        public static RectTransform Tag(RectTransform parent, string name, string text, Color bg, Color fg, float h = 18f, float textPx = 10f)
        {
            var rt = Node(parent, name);
            Surface(rt, bg, RTag);
            Body(Fill(Node(rt, "Text"), 6, 0, 6, 0), text.ToUpperInvariant(), textPx, fg, TextAlignmentOptions.Center);
            // Width follows the text through the parent layout group (no fitter: a fitter inside a
            // layout group fights it).
            var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset((int)Px(6), (int)Px(6), 0, 0); hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandHeight = true; hl.childForceExpandWidth = false;
            Size(rt, -1, h);
            return rt;
        }

        /// <summary>Red count dot for the top-right corner of a button.</summary>
        public static TextMeshProUGUI Badge(RectTransform parent, string text)
        {
            var rt = Box(Node(parent, "Badge"), new Vector2(1, 1), new Vector2(0.5f, 0.5f), -2, -2, 16, 16);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Spr("circle"); img.color = Red; img.raycastTarget = false;
            return Body(Fill(Node(rt, "Count"), 0, 0, 0, 0), text, 10f, Color.white, TextAlignmentOptions.Center);
        }

        /// <summary>Currency pill: icon, value text, optional green "+" button.</summary>
        public static TextMeshProUGUI Pill(RectTransform rt, string icon, string value, out Button plus)
        {
            Surface(rt, Deep, 10f);
            var row = Row(rt, 5, TextAnchor.MiddleLeft, false, 5, 5);
            row.childForceExpandHeight = false;
            var ic = Node(rt, "Icon"); IconImage(ic, icon); Size(ic, 18, 18);
            var tx = Node(rt, "Value"); var t = Shrink(Body(tx, value, 13f, Ink), 0.7f); Size(tx, -1, 22, 1);
            var pb = Node(rt, "Plus"); Size(pb, 18, 18);
            Surface(pb, Green, 5f, true);
            Body(Fill(Node(pb, "T"), 0, 0, 0, 0), "+", 14f, OnGreen, TextAlignmentOptions.Center);
            plus = pb.gameObject.AddComponent<Button>();
            return t;
        }

        const string BannerMat = "Assets/_Project/Art/Materials/UI/BannerFx.mat";

        /// <summary>
        /// Premium banner background (BannerFx shader) on a rounded surface: fixed colours in every
        /// theme (the ThemeTint is removed so a theme never dyes it).
        /// </summary>
        public static BannerFx Banner(Graphic g, Color a, Color b, Color glow, Vector2 rayCenter, float rim, float sparkle = 0.8f, float sheen = 0.6f)
        {
            var tint = g.GetComponent<ThemeTint>(); if (tint != null) Object.DestroyImmediate(tint);
            g.color = Color.white;
            var fx = g.GetComponent<BannerFx>() ?? g.gameObject.AddComponent<BannerFx>();
            var m = AssetDatabase.LoadAssetAtPath<Material>(BannerMat);
            if (m == null)
            {
                m = new Material(Shader.Find("ZombieWar/UI/BannerFx")) { name = "BannerFx" };
                AssetDatabase.CreateAsset(m, BannerMat);
            }
            fx.BaseMaterial = m;
            fx.Configure(a, b, glow, rayCenter, rim, sparkle, sheen);
            return fx;
        }

        /// <summary>Progress bar: deep track + clipped fill (UIBarClip) so the ends stay round.</summary>
        public static UIBarClip Bar(RectTransform rt, Color fill, float value01)
        {
            Surface(rt, Deep, 4f);
            var clip = Fill(Node(rt, "Fill"), 0, 0, 0, 0);
            clip.gameObject.AddComponent<RectMask2D>();
            var bar = clip.gameObject.AddComponent<UIBarClip>();
            var g = Fill(Node(clip, "Graphic"), 0, 0, 0, 0);
            Surface(g, fill, 4f);
            bar.Graphic = g;
            UIBarClip.Set(clip, value01);
            return bar;
        }

        /// <summary>Bottom tab bar (HOME ARSENAL SHOP GACHA PASS). Returns the five buttons.</summary>
        public static Button[] NavBar(RectTransform root, int active, out Image[] dots)
        {
            var bar = BottomBand(Node(root, "Nav"), 0, 64);
            Theme(Flat(bar, Deep, true), Deep, ThemeRole.Nav);
            var line = TopBand(Node(bar, "Line"), 0, 1); Flat(line, Edge);
            var row = Fill(Node(bar, "Tabs"), 0, 1, 0, 0);
            Row(row, 0, TextAnchor.MiddleCenter, true);
            string[] names = { "HOME", "ARSENAL", "SHOP", "GACHA", "PASS" };
            string[] icons = { "Badge_Crown", "Battle", "Shop", "Ticket_Gold", "Star_Gold" };
            var buttons = new Button[5];
            dots = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var tab = Node(row, names[i]);
                var img = tab.gameObject.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0);
                buttons[i] = tab.gameObject.AddComponent<Button>();
                var ic = Box(Node(tab, "Icon"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 8, 28, 26);
                IconImage(ic, icons[i], i == active ? Color.white : new Color(1, 1, 1, 0.55f));
                var lb = Box(Node(tab, "Label"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -16, 76, 14);
                Body(lb, names[i], 10f, i == active ? Yellow : Dim, TextAlignmentOptions.Center);
                var dot = Box(Node(tab, "Dot"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 15, 18, 9, 9);
                dots[i] = dot.gameObject.AddComponent<Image>(); dots[i].sprite = Spr("circle"); dots[i].color = Red; dots[i].raycastTarget = false;
                dot.gameObject.SetActive(false);
            }
            return buttons;
        }

        /// <summary>Back button used by pushed screens.</summary>
        public static Button BackButton(RectTransform parent)
        {
            var rt = Box(Node(parent, "Back"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 38, 38);
            Surface(rt, Card, 11f, true);
            Picto(Fill(Node(rt, "Arrow"), 9, 9, 9, 9), "Arrow_Left_1");
            var b = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<UIPressFeel>();
            return b;
        }

        /// <summary>Pushed-screen header (mockup: padding 14, back 38, title 24). Returns the back
        /// button; the right side ("Right") is a right-aligned row for pills.</summary>
        public static Button Header(RectTransform safe, string title, out RectTransform right)
        {
            var bar = TopBand(Node(safe, "Header"), 14, 38, 14, 14);
            var back = BackButton(bar);
            Title(Box(Node(bar, "Title"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 46, 0, 200, 38), title, 24f);
            right = Box(Node(bar, "Right"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 190, 30);
            var row = Row(right, 6, TextAnchor.MiddleRight); row.childForceExpandHeight = true;
            return back;
        }

        /// <summary>Vertical page body under a header: children stack with their preferred
        /// heights, so rows hidden at runtime close their gap.</summary>
        /// Scrolls vertically: the mockups are 390x844 (19.5:9) and a 16:9 phone is shorter, so a
        /// tall page must scroll rather than clip. Returns the content column.
        public static RectTransform Page(RectTransform safe, float top, float bottom = 0)
        {
            var view = Fill(Node(safe, "Page"), 0, top, 0, bottom);
            Flat(view, new Color(0, 0, 0, 0), true);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Node(view, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var col = Column(content, 0);
            col.childForceExpandWidth = true;
            int side = Mathf.RoundToInt(Px(14));
            col.padding = new RectOffset(side, side, 0, side);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = view; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30f;
            return content;
        }

        /// <summary>Dim caps section label with the mockup's 12 px above and 6 px below.</summary>
        public static TextMeshProUGUI SectionLabel(RectTransform page, string text, string name = null)
        {
            var rt = Node(page, name ?? ("Label_" + text));
            Size(rt, -1, 33);
            return Label(Fill(Node(rt, "T"), 0, 12, 0, 6), text);
        }

        /// <summary>Card surface that sizes to its stacked children (optional padding in px).</summary>
        public static RectTransform CardColumn(RectTransform page, string name, float pad = 0, float gap = 0)
        {
            var rt = Node(page, name);
            Surface(rt, Card, RCard);
            var col = Column(rt, gap);
            int p = Mathf.RoundToInt(Px(pad));
            col.padding = new RectOffset(p, p, p, p);
            return rt;
        }

        /// <summary>Wires a serialized field on a component by name.</summary>
        public static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[UIKitV2] {target.GetType().Name} has no field '{field}'"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireArray<T>(Object target, string field, IList<T> values) where T : Object
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[UIKitV2] {target.GetType().Name} has no field '{field}'"); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
