using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Design tokens — nguồn chuẩn, khớp 100% Docs/UI_REDESIGN_SPEC.md §2.
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
