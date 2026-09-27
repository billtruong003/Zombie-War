using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Colours one Graphic (Image, RawImage, TMP text) by a theme role, keeping the alpha it was
    /// built with, and re-colours it when the theme changes. The UI kit adds it for every colour it
    /// knows, so screens stay themeable without code of their own.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class ThemeTint : MonoBehaviour
    {
        public ThemeRole role;
        [Range(0, 1)] public float alpha = 1f;
        [Tooltip("Display text only: the outlined material for dark themes and the plain one for light themes.")]
        public Material outlined, plain;
        Graphic _g;

        void OnEnable()
        {
            ThemeService.Changed += Apply;
            Apply();
        }

        void OnDisable() => ThemeService.Changed -= Apply;

        public void Apply()
        {
            if (role == ThemeRole.None || ThemeService.Current == null) return;
            if (_g == null) _g = GetComponent<Graphic>();
            var c = ThemeService.Get(role);
            c.a *= alpha;
            _g.color = c;
            // Dark text on a light card reads cleaner without the black outline (mockup rule).
            if (outlined != null && plain != null && _g is TMPro.TMP_Text txt)
                txt.fontSharedMaterial = ThemeService.Current.dark ? outlined : plain;
        }

        /// <summary>Drops the theme role and uses a fixed colour (art that must not follow the theme).</summary>
        public static void Clear(Graphic g, Color c)
        {
            if (g == null) return;
            var t = g.GetComponent<ThemeTint>();
            if (t != null) t.role = ThemeRole.None;
            g.color = c;
        }

        /// <summary>Re-tags at runtime (a screen switching a tile between states) and applies.</summary>
        public static void Set(Graphic g, ThemeRole role, float alpha = 1f)
        {
            if (g == null) return;
            var t = g.GetComponent<ThemeTint>();
            if (t == null) t = g.gameObject.AddComponent<ThemeTint>();
            t.role = role; t.alpha = alpha;
            t.Apply();
        }
    }
}
