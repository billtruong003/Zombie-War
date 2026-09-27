using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Design tokens — nguồn chuẩn, khớp 100% Docs/Reference/UI/UI_REDESIGN_SPEC.md §2.
    /// Palette v2 POLISH. Đừng chế màu mới ngoài bảng này.
    /// </summary>
    public static class UITheme
    {
        // ---- Reference resolution (portrait 9:16, ref 1080×1920, match 0.5) ----
        public const float RefWidth = 1080f;
        public const float RefHeight = 1920f;

        // ---- Spacing grid ----
        public const float Space1 = 8f;
        public const float Space2 = 16f;
        public const float Space3 = 24f;
        public const float Space4 = 32f;
        public const float Grid = 48f;   // HUD snap

        // ---- Core palette (§2.1) ----
        public static readonly Color Bg        = Hex("#141821");
        public static readonly Color Surface   = Hex("#1B2130");
        public static readonly Color Surface2  = Hex("#232B3A");
        public static readonly Color Hairline  = Hex("#2A3244");
        public static readonly Color TextMain  = Hex("#F4F6F8");
        public static readonly Color TextDim   = Hex("#9AA3B2");

        public static readonly Color Gold   = Hex("#F5B841");
        public static readonly Color GoldHi = Hex("#FFCF66");
        public static readonly Color GoldLo = Hex("#E89A2B");
        public static readonly Color OnGold = Hex("#14181F");

        public static readonly Color Cyan   = Hex("#5BD9E8");   // KC/gem
        public static readonly Color Green  = Hex("#4CAF6E");   // primary positive
        public static readonly Color GreenLo= Hex("#37814F");
        public static readonly Color Danger = Hex("#E5484D");

        // ---- Rarity 0..4 : Xám → Xanh lá → Xanh biển → Tím → Cam ----
        public static readonly Color[] Rarity =
        {
            Hex("#9AA3B2"), Hex("#4CAF6E"), Hex("#4FA3F7"), Hex("#8B7BD8"), Hex("#F2994A"),
        };
        public static Color RarityColor(int r) => Rarity[Mathf.Clamp(r, 0, 4)];

        // ---- M8 mockup palette (owner-approved 2026-09-26; M8UiPolish applies it) ----
        public static readonly Color M8Ground   = Hex("#262A36");   // screen ground
        public static readonly Color M8Card     = Hex("#2F3544");   // cards, panels, secondary buttons
        public static readonly Color M8Deep     = Hex("#1F2330");   // wells: bar tracks, empty slots
        public static readonly Color M8Edge     = Hex("#3A4152");   // hairlines
        public static readonly Color M8Ink      = Hex("#F4F1EA");
        public static readonly Color M8InkDim   = Hex("#9AA1B0");
        public static readonly Color M8Scrim    = Hex("#0C0E14D6");  // behind modals
        public static readonly Color M8Yellow   = Hex("#FFC93C");
        public static readonly Color M8YellowLip= Hex("#E0A21C");
        public static readonly Color M8OnYellow = Hex("#2A1D00");
        public static readonly Color M8Green    = Hex("#5BD68A");
        public static readonly Color M8GreenLip = Hex("#34A865");
        public static readonly Color M8OnGreen  = Hex("#10331D");
        public static readonly Color M8Blue     = Hex("#4FA3FF");
        public static readonly Color M8BlueLip  = Hex("#2F78C9");
        public static readonly Color M8Red      = Hex("#E5484D");
        public static readonly Color M8RedLip   = Hex("#9E2A2E");
        public static readonly Color M8CardLip  = Hex("#1F2330");

        // ---- M8 corner radii (canvas units). A shape inside another is never rounder than its
        //      parent allows: inner radius = outer radius - inset. UiAudit checks nested pairs. ----
        public const float M8RadiusPanel = 32f;
        public const float M8RadiusCard = 24f;
        public const float M8RadiusTile = 14f;   // tiles and chips inside a card
        public const float M8RadiusButton = 24f;

        /// <summary>Corner radius (sprite pixels) drawn inside each of the UI sprites.</summary>
        public static float SpriteCornerPx(string spriteName) => spriteName switch
        {
            "rounded_24" or "frame_24" => 24f,
            "rounded_32" or "frame_32" or "pill" => 31f,
            _ => 0f,
        };

        /// <summary>pixelsPerUnitMultiplier that draws <paramref name="spriteName"/> with a corner radius of <paramref name="radius"/>.</summary>
        public static float MultiplierFor(string spriteName, float radius) =>
            radius <= 0f ? 1f : Mathf.Max(0.05f, SpriteCornerPx(spriteName) / radius);

        // ---- Currency accents ----
        public static readonly Color Coin = Gold;   // vàng UI
        public static readonly Color Gem  = Cyan;

        // ================= BACK-COMPAT ALIASES (code cũ đang dùng) =================
        public static readonly Color Panel      = Surface;
        public static readonly Color PanelLight = Surface2;
        public static readonly Color Success    = Green;
        public static readonly Color SuccessEdge= GreenLo;
        public static readonly Color Warning    = Gold;
        public static readonly Color Arcane     = Rarity[3];
        public static readonly Color TextMainC  = TextMain;

        // ---- Typography (canvas 1080×1920; §2.2) ----
        public const float FontHero   = 76f;   // Header 76/800
        public const float FontHeader = 76f;
        public const float FontSub    = 52f;   // Subheader 52/600
        public const float FontTitle  = 64f;   // CTA lớn
        public const float FontBody   = 38f;   // Body 38/500
        public const float FontLabel  = 30f;   // Label 30/700 uppercase
        public const float FontSmall  = 30f;   // alias cũ

        // ---- Shape (§2.3) ----
        public const float RadiusCard   = 32f;
        public const float RadiusButton = 24f;
        public const float RadiusPanel  = 32f;   // alias
        public const float ButtonEdge   = 10f;   // bevel đáy
        public const float GlowAlpha    = 0.35f;
        public const float HairlineW    = 2f;

        // ---- Transition timing (§6.1) ----
        public const float FadeTime    = 0.15f;
        public const float SlidePixels = 40f;
        public const float TEnter      = 0.30f;
        public const float TExit       = 0.15f;
        public const float TStandard   = 0.20f;
        public const float TBounce     = 0.40f;

        static Color Hex(string h) =>
            ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

        static Color A(Color c, float a) { c.a = a; return c; }
        public static Color Alpha(Color c, float a) => A(c, a);
    }
}
