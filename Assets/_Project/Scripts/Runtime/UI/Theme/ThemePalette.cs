using System;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>What a colour is for. The kit tags every graphic with one, so a theme change is a
    /// lookup, not a rebuild. Values are built in OKLCH by Tools/themes.json (owner 2026-09-27).</summary>
    public enum ThemeRole
    {
        None = 0,
        Ground, Card, Deep, Edge, Ink, Dim, Outline, Nav, Gold,
        Primary, PrimaryLip, PrimaryOn, PrimaryTint,
        Gem, GemLip, GemOn, GemTint,
        Claim, ClaimLip, ClaimOn, ClaimTint,
        Info, InfoLip, InfoOn, InfoTint,
        Danger, DangerLip, DangerOn, DangerTint,
        Rarity0, Rarity1, Rarity2, Rarity3, Rarity4, LegendTint,
        /// Display text on a surface: dark ink on light cards, white on dark ones.
        TextOnSurface,
        /// Secondary buttons: theme blue on light themes, slate on dark (a white button on white
        /// cards reads poorly).
        QuietFace, QuietLip,
        /// Text on an Ink-coloured chip (selected segment): dark on the light chip of Dark mode,
        /// white on the navy chip of light themes.
        OnInk,
        /// Accent-coloured text: the accent itself on dark themes, its darker lip on light ones
        /// (bright yellow or teal text on a light card is unreadable).
        PrimaryText, GemText, ClaimText, InfoText, DangerText,
        /// Secondary text on a quiet button: dim on the slate button, soft white on the blue one.
        OnQuiet,
    }

    /// <summary>One theme: a colour per role plus the menu background settings.</summary>
    [CreateAssetMenu(menuName = "HordeCall/UI Theme Palette")]
    public sealed class ThemePalette : ScriptableObject
    {
        public string id = "sky";
        public string displayName = "Sky";
        public bool dark;
        [SerializeField] private Color[] colors = new Color[Enum.GetValues(typeof(ThemeRole)).Length];

        [Header("Menu background (MenuBackground shader)")]
        public Color bgTop = Color.white, bgBottom = Color.blue, bgShape = Color.white;
        [Range(0, 1)] public float bgShapeAlpha = 0.22f, bgRays = 0.18f, bgVignette = 0.22f;

        public Color Get(ThemeRole role)
        {
            if (role == ThemeRole.TextOnSurface) return dark ? Color.white : Get(ThemeRole.Ink);
            if (role == ThemeRole.QuietFace) return Get(dark ? ThemeRole.Card : ThemeRole.Info);
            if (role == ThemeRole.QuietLip) return Get(dark ? ThemeRole.Deep : ThemeRole.InfoLip);
            if (role == ThemeRole.OnInk) return dark ? Get(ThemeRole.Ground) : Color.white;
            if (role == ThemeRole.OnQuiet) return dark ? Get(ThemeRole.Dim) : new Color(1f, 1f, 1f, 0.85f);
            if (role == ThemeRole.PrimaryText) return Get(dark ? ThemeRole.Primary : ThemeRole.Gold);
            if (role == ThemeRole.GemText) return Get(dark ? ThemeRole.Gem : ThemeRole.GemLip);
            if (role == ThemeRole.ClaimText) return Get(dark ? ThemeRole.Claim : ThemeRole.ClaimLip);
            if (role == ThemeRole.InfoText) return Get(dark ? ThemeRole.Info : ThemeRole.InfoLip);
            if (role == ThemeRole.DangerText) return Get(dark ? ThemeRole.Danger : ThemeRole.DangerLip);
            int i = (int)role;
            return colors != null && i >= 0 && i < colors.Length ? colors[i] : Color.magenta;
        }

        public void Set(ThemeRole role, Color c)
        {
            int n = Enum.GetValues(typeof(ThemeRole)).Length;
            if (colors == null || colors.Length != n) Array.Resize(ref colors, n);
            colors[(int)role] = c;
        }

        public static ThemeRole Rarity(int tier) => ThemeRole.Rarity0 + Mathf.Clamp(tier, 0, 4);
    }
}
