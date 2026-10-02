using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// The locked look of each map's ground and fluid (owner round 2, 2026-10-02), written into the
    /// real materials: each map gets its own cut-down shader (EnvSandbox/Shaders/Maps/) and the values
    /// picked from the comparison page. The builders call this after setting up a material, so a
    /// rebake keeps the look; the menu item applies it to the materials already baked.
    /// </summary>
    public static class MapLooks
    {
        const string Art = "Assets/_Project/Art/EnvSandbox/";

        static Color C(float r, float g, float b) => new(r, g, b, 1f);

        // ── fluids ──────────────────────────────────────────────────────────────────────

        /// <summary>Gives a map's fluid material its own shader and locked values. Returns false for
        /// zones that are not maps yet (concept zones keep the sandbox shader).</summary>
        public static bool Fluid(Material m, string zone)
        {
            switch (zone)
            {
                case "meadow":
                    // Turquoise-green clear shallows over sand, sea-blue deep water, light lines on the
                    // floor, strong streak noise, foam broken up by noise at the banks.
                    Use(m, "HordeCall/Map/Meadow Water");
                    m.SetColor("_ShallowColor", C(0.1f, 0.78f, 0.64f));
                    m.SetColor("_DeepColor", C(0.06f, 0.5f, 0.8f));
                    m.SetFloat("_DepthRange", 1.1f);
                    m.SetFloat("_Clarity", 1.2f); m.SetFloat("_MinAlpha", 0.45f);
                    m.SetFloat("_Caustics", 0.6f); m.SetFloat("_CausticScale", 1.4f);
                    m.SetColor("_CausticColor", C(0.85f, 1f, 0.95f));
                    m.SetColor("_StreakColor", C(0.78f, 0.97f, 1f));
                    m.SetFloat("_StreakScale", 4.5f); m.SetFloat("_StreakCut", 0.64f); m.SetFloat("_StreakAlpha", 0.6f);
                    m.SetVector("_Flow", new Vector4(0.6f, 0.25f, 0.06f, 0f));
                    m.SetFloat("_WaveHeight", 0.04f);
                    Foam(m, C(0.96f, 0.99f, 1f), line: 0.03f, noise: 1f, scale: 1.5f, reach: 1.0f);
                    return true;
                case "forest":
                    // Green and murky: the floor goes a hand deep, duckweed and leaves drift in patches.
                    Use(m, "HordeCall/Map/Forest Water");
                    m.SetColor("_ShallowColor", C(0.36f, 0.52f, 0.22f));
                    m.SetColor("_DeepColor", C(0.12f, 0.27f, 0.14f));
                    m.SetFloat("_DepthRange", 0.8f);
                    m.SetFloat("_Clarity", 0.3f); m.SetFloat("_MinAlpha", 0.4f);
                    m.SetColor("_StreakColor", C(0.52f, 0.7f, 0.36f));
                    m.SetFloat("_StreakScale", 5f); m.SetFloat("_StreakCut", 0.7f); m.SetFloat("_StreakAlpha", 0.3f);
                    m.SetVector("_Flow", new Vector4(0.3f, 0.15f, 0.03f, 0f));
                    m.SetFloat("_WaveHeight", 0.02f);
                    m.SetFloat("_ScumAmount", 0.45f); m.SetFloat("_ScumScale", 0.28f);
                    m.SetColor("_ScumColor", C(0.36f, 0.55f, 0.14f)); m.SetColor("_ScumColor2", C(0.27f, 0.44f, 0.1f));
                    Foam(m, C(0.8f, 0.86f, 0.66f), line: 0.04f, noise: 0.75f, scale: 1f, reach: 0.3f);
                    return true;
                case "swamp":
                    // Owner pick S1: see-through toxic green, small bubbles, glow.
                    Use(m, "HordeCall/Map/Swamp Toxic");
                    m.SetColor("_ShallowColor", C(0.55f, 0.85f, 0.2f)); m.SetColor("_DeepColor", C(0.18f, 0.42f, 0.1f));
                    m.SetColor("_StreakColor", C(0.7f, 0.95f, 0.3f)); m.SetFloat("_StreakAlpha", 0.35f);
                    m.SetColor("_Emission", C(0.12f, 0.3f, 0.02f));
                    m.SetFloat("_Clarity", 0.45f); m.SetFloat("_MinAlpha", 0.12f);
                    m.SetFloat("_BubbleAmount", 0.55f); m.SetFloat("_BubbleScale", 1.4f); m.SetFloat("_BubbleRate", 0.5f);
                    m.SetVector("_Flow", new Vector4(0.2f, 0.1f, 0.02f, 0f)); m.SetFloat("_WaveHeight", 0.02f);
                    Foam(m, C(0.78f, 0.95f, 0.45f), line: 0.05f, noise: 0.6f, scale: 1.1f, reach: 0.3f);
                    m.SetFloat("_FoamSoft", 0.12f);
                    return true;
                case "tundra":
                    // Clear ice over a blue bed, cracks, frost at the banks; still.
                    Use(m, "HordeCall/Map/Tundra Ice");
                    m.SetColor("_ShallowColor", C(0.8f, 0.9f, 0.97f)); m.SetColor("_DeepColor", C(0.45f, 0.65f, 0.85f));
                    m.SetColor("_StreakColor", C(0.92f, 0.97f, 1f)); m.SetFloat("_StreakCut", 0.66f); m.SetFloat("_StreakAlpha", 0.35f);
                    m.SetFloat("_Clarity", 0.8f); m.SetFloat("_MinAlpha", 0.35f);
                    m.SetVector("_Flow", Vector4.zero); m.SetFloat("_WaveHeight", 0f);
                    m.SetFloat("_CrackAmount", 0.7f); m.SetFloat("_CrackTiling", 5f);
                    Foam(m, C(0.97f, 0.98f, 1f), line: 0.05f, noise: 0.7f, scale: 0.8f, reach: 0.3f);
                    return true;
                case "desert":
                    // Oasis pools: the meadow's clear turquoise water, a touch greener.
                    Use(m, "HordeCall/Map/Meadow Water");
                    m.SetColor("_ShallowColor", C(0.14f, 0.8f, 0.6f));
                    m.SetColor("_DeepColor", C(0.06f, 0.48f, 0.72f));
                    m.SetFloat("_DepthRange", 1f);
                    m.SetFloat("_Clarity", 1.1f); m.SetFloat("_MinAlpha", 0.45f);
                    m.SetFloat("_Caustics", 0.6f); m.SetFloat("_CausticScale", 1.4f);
                    m.SetColor("_CausticColor", C(0.85f, 1f, 0.95f));
                    m.SetColor("_StreakColor", C(0.78f, 0.97f, 1f));
                    m.SetFloat("_StreakScale", 4.5f); m.SetFloat("_StreakCut", 0.66f); m.SetFloat("_StreakAlpha", 0.5f);
                    m.SetVector("_Flow", new Vector4(0.3f, 0.15f, 0.03f, 0f));
                    m.SetFloat("_WaveHeight", 0.03f);
                    Foam(m, C(0.98f, 0.97f, 0.92f), line: 0.03f, noise: 1f, scale: 1.4f, reach: 0.8f);
                    return true;
                case "volcano":
                    // Crust type by default (black plates, white-hot seams, boiling vents); the molten
                    // type is the other option.
                    var noise = m.GetTexture("_NoiseTex");
                    Use(m, "HordeCall/Map/Volcano Lava");
                    m.SetTexture("_NoiseTex", noise);
                    LavaCrust(m);
                    return true;
            }
            return false;
        }

        static void Foam(Material m, Color colour, float line, float noise, float scale, float reach)
        {
            m.SetColor("_FoamColor", colour);
            m.SetFloat("_FoamWidth", line); m.SetFloat("_FoamSoft", 0f); m.SetFloat("_FoamWobble", 0.03f);
            m.SetFloat("_FoamNoise", noise); m.SetFloat("_FoamNoiseScale", scale); m.SetFloat("_FoamNoiseDist", reach);
        }

        /// <summary>Liquid lava: crust slabs with a red rim float on orange, yellow veins, boiling.</summary>
        public static void LavaMolten(Material m)
        {
            m.SetFloat("_Lava", 0f); m.EnableKeyword("_LAVA_MOLTEN"); m.DisableKeyword("_LAVA_CRUST");
            m.SetColor("_CrustColor", C(0.035f, 0.025f, 0.025f));
            m.SetColor("_RimColor", C(0.45f, 0.05f, 0.02f));
            m.SetColor("_HotColor", C(1f, 0.3f, 0.03f));
            m.SetColor("_CoreColor", new Color(1.6f, 1.05f, 0.3f, 1f));
            m.SetFloat("_CrustCut", 0.4f); m.SetFloat("_CoreCut", 0.6f); m.SetFloat("_Scale", 4f);
            m.SetFloat("_EdgeWidth", 0.04f); m.SetFloat("_RimWidth", 0.03f);
            m.SetColor("_EdgeColor", new Color(1.6f, 0.62f, 0.08f, 1f));
            m.SetFloat("_BubbleAmount", 0.45f); m.SetFloat("_BubbleScale", 1.3f);
        }

        /// <summary>Skinned-over lava: black plates, white-hot seams, boiling vents.</summary>
        public static void LavaCrust(Material m)
        {
            m.SetFloat("_Lava", 1f); m.EnableKeyword("_LAVA_CRUST"); m.DisableKeyword("_LAVA_MOLTEN");
            m.SetColor("_CrustColor", C(0.04f, 0.03f, 0.03f));
            m.SetColor("_RimColor", C(0.4f, 0.04f, 0.02f));
            m.SetFloat("_PlateScale", 1.5f); m.SetFloat("_SeamWidth", 0.08f); m.SetFloat("_VentCut", 0.66f);
            m.SetFloat("_EdgeWidth", 0.04f); m.SetFloat("_RimWidth", 0.03f);
            m.SetColor("_EdgeColor", new Color(1.6f, 0.62f, 0.08f, 1f));
            m.SetFloat("_BubbleAmount", 0.6f); m.SetFloat("_BubbleScale", 1.1f);
        }

        // ── ground ──────────────────────────────────────────────────────────────────────

        /// <summary>Gives a map's ground material its own shader and locked values.</summary>
        public static bool Ground(Material m, string zone)
        {
            switch (zone)
            {
                case "meadow":
                    Use(m, "HordeCall/Map/Meadow Ground");
                    Bed(m, C(0.78f, 0.7f, 0.5f), C(0.68f, 0.64f, 0.56f), 1f);
                    return true;
                case "forest":
                    // Not the meadow: darker loam, moss, drifts of fallen leaves.
                    Use(m, "HordeCall/Map/Forest Ground");
                    Bed(m, C(0.3f, 0.26f, 0.16f), C(0.24f, 0.22f, 0.14f), 0.85f);
                    m.SetFloat("_LitterAmount", 0.35f); m.SetFloat("_LitterScale", 0.42f);
                    m.SetColor("_LitterColor", C(0.5f, 0.3f, 0.12f)); m.SetColor("_LitterColor2", C(0.4f, 0.37f, 0.15f));
                    m.SetFloat("_MossAmount", 0.6f); m.SetColor("_MossColor", C(0.2f, 0.34f, 0.12f));
                    Darken(m, 0.82f);
                    return true;
                case "swamp":
                    Use(m, "HordeCall/Map/Swamp Ground");
                    Bed(m, C(0.3f, 0.32f, 0.16f), C(0.22f, 0.25f, 0.13f), 0.8f);
                    m.SetFloat("_LitterAmount", 0.25f); m.SetFloat("_LitterScale", 0.55f);
                    m.SetColor("_LitterColor", C(0.32f, 0.25f, 0.12f)); m.SetColor("_LitterColor2", C(0.26f, 0.3f, 0.12f));
                    m.SetFloat("_MossAmount", 0.5f); m.SetColor("_MossColor", C(0.2f, 0.27f, 0.1f));
                    return true;
                case "volcano":
                    Use(m, "HordeCall/Map/Volcano Ground");
                    Charcoal(m);
                    return true;
                case "desert":
                    // Owner pick D3: plain sand (no cracks), clay paths, a sandy bed under the oases.
                    Use(m, "HordeCall/Map/Desert Ground");
                    Bed(m, C(0.86f, 0.74f, 0.52f), C(0.74f, 0.64f, 0.48f), 1f);
                    m.SetFloat("_CrackStrength", 0f);
                    m.SetFloat("_MacroStrength", 0.12f); m.SetFloat("_MacroScale", 26f);
                    return true;
                case "tundra":
                    // MinionsArt's snow: trails pressed by every walker, sparkles; the painted fissures
                    // are kept faint (strong, they read as paving).
                    Use(m, "HordeCall/Map/Tundra Ground");
                    Bed(m, C(0.42f, 0.55f, 0.66f), C(0.3f, 0.42f, 0.55f), 0.8f);
                    m.SetFloat("_CrackStrength", 0.05f);
                    m.SetFloat("_MacroStrength", 0.22f); m.SetFloat("_MacroScale", 30f);
                    m.SetColor("_SnowTrailColor", C(0.74f, 0.82f, 0.98f)); m.SetFloat("_SnowTrailDepth", 3.5f);
                    m.SetFloat("_SparkleAmount", 2.5f); m.SetFloat("_SparkleScale", 0.35f);
                    return true;
            }
            return false;
        }

        static void Bed(Material m, Color a, Color b, float strength)
        {
            m.SetColor("_BedColor", a); m.SetColor("_BedColor2", b); m.SetFloat("_BedStrength", strength);
        }

        static void Darken(Material m, float k)
        {
            foreach (var p in new[] { "_DryDebugColor", "_GrassDebugColor", "_SandDebugColor", "_RockDebugColor" })
            {
                var c = m.GetColor(p);
                m.SetColor(p, new Color(c.r * k, c.g * k, c.b * k, 1f));
            }
        }

        /// <summary>Deep charcoal, no grey; thin hard cracks, lava glowing through near the basins and
        /// faintly everywhere.</summary>
        static void Charcoal(Material m)
        {
            Color a = C(0.07f, 0.06f, 0.06f), b = C(0.055f, 0.05f, 0.05f);
            m.SetColor("_DryDebugColor", a); m.SetColor("_GrassDebugColor", b);
            m.SetColor("_SandDebugColor", a * 1.15f); m.SetColor("_RockDebugColor", b * 0.9f);
            foreach (var p in new[] { "_DryTint", "_GrassTint", "_SandTint", "_RockTint" }) m.SetColor(p, C(0.3f, 0.27f, 0.27f));
            m.SetFloat("_TextureStrength", 0.2f);
            m.SetFloat("_MacroStrength", 0.12f);
            m.SetFloat("_BankDarken", 0.15f);
            m.SetColor("_CrackColor", C(0.012f, 0.008f, 0.008f));
            m.SetFloat("_CrackStrength", 1f);
            m.SetFloat("_CrackCell", 3.4f);
            m.SetFloat("_CrackWidth", 0.03f);
            m.SetFloat("_CrackFine", 0.55f);
            m.SetFloat("_CrackGlowBase", 0.55f);
            m.SetFloat("_CrackGlowReach", 0.55f);
            m.SetFloat("_CrackGlowFlicker", 0.35f);
            m.SetColor("_CrackGlow", C(1f, 0.18f, 0.02f));
            m.SetColor("_CrackGlowCore", new Color(1.6f, 0.55f, 0.06f, 1f));
        }

        static void Use(Material m, string shader)
        {
            var s = Shader.Find(shader);
            if (s == null) { Debug.LogError("[MapLooks] missing shader " + shader); return; }
            m.shader = s;
        }

        // ── apply to what is baked ──────────────────────────────────────────────────────

        static readonly string[] MapIds = { "meadow", "forest", "volcano", "swamp", "tundra", "desert" };

        [MenuItem("HordeCall/World/Apply Map Looks (ground + fluid)")]
        public static string ApplyAll()
        {
            var log = new System.Text.StringBuilder();
            foreach (var id in MapIds)
            {
                foreach (var path in new[] { Art + "Maps/" + id + "/M_MapGround_" + id + ".mat", Art + "Materials/M_Ground_" + id + ".mat" })
                {
                    var g = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (g != null && Ground(g, id)) { EditorUtility.SetDirty(g); log.Append(g.name).Append(' '); }
                }
                var f = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/M_Fluid_" + id + ".mat");
                if (f != null && Fluid(f, id)) { EditorUtility.SetDirty(f); log.Append(f.name).Append(' '); }
            }
            // Snow maps press trails (SnowTrails reads the shader from the theme so it ships in builds).
            var tundra = AssetDatabase.LoadAssetAtPath<ZombieWar.World.MapTheme>("Assets/Resources/MapThemes/MapTheme_tundra.asset");
            if (tundra != null) { tundra.snowTrails = Shader.Find("Hidden/HordeCall/SnowTrail"); EditorUtility.SetDirty(tundra); }
            AssetDatabase.SaveAssets();
            Debug.Log("[MapLooks] applied: " + log);
            return log.ToString();
        }
    }
}
