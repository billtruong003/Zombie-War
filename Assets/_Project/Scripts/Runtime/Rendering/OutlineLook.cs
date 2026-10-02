using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Runtime override of the outline's look for QA and look comparisons (2026-10-02): the QA panel's
    /// LOOK tab and the editor's LookShot set these, the outline feature uses them over its volume.
    /// All null = the volume's own look (the shipped state).
    /// </summary>
    public static class OutlineLook
    {
        public static Color? Colour;
        /// <summary>x = how much the line takes the object's own colour (0–1), y = its darkening.</summary>
        public static Vector2? Tint;
        public static int? Thickness;
        public static bool Hidden;

        /// <summary>The looks on trial (owner pick pending): code, label, line colour, object tint, darkening.</summary>
        public static readonly (string code, string label, Color colour, float tint, float darken)[] Presets =
        {
            ("L1", "Đen tuyền", new Color(0f, 0f, 0f), 0f, 0f),
            ("L2", "Nâu sô-cô-la", new Color(0.32f, 0.16f, 0.05f), 0f, 0f),
            ("L3", "Xanh navy", new Color(0.05f, 0.12f, 0.38f), 0f, 0f),
            ("L4", "Tím đậm", new Color(0.3f, 0.07f, 0.38f), 0f, 0f),
            ("L5", "Trắng kem", new Color(1f, 0.95f, 0.82f), 0f, 0f),
            ("L6", "Theo màu vật, tối đậm", new Color(0.05f, 0.04f, 0.06f), 1f, 0.3f),
            ("L7", "Theo màu vật, tối vừa", new Color(0.05f, 0.04f, 0.06f), 1f, 0.5f),
            ("L8", "Nửa đen, nửa màu vật", new Color(0.05f, 0.04f, 0.06f), 0.5f, 0.4f),
        };

        public static void Apply(int preset)
        {
            var p = Presets[preset];
            Colour = p.colour;
            Tint = new Vector2(p.tint, p.darken);
        }

        public static void Clear()
        {
            Colour = null; Tint = null; Thickness = null; Hidden = false;
        }

        // Domain reload is off in this project: statics survive Play Mode exit.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewSession() => Clear();
    }
}
