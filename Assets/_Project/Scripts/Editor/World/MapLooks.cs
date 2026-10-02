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
                    // Owner tuning in the fluid lab (02/10).
                    m.SetColor("_ShallowColor", C(0.045f, 0f, 1f)); m.SetColor("_DeepColor", C(0f, 0.522f, 1f));
                    m.SetFloat("_DepthRange", 0.57f); m.SetFloat("_Clarity", 1.18f); m.SetFloat("_MinAlpha", 0.41f);
                    m.SetFloat("_Caustics", 0f); m.SetFloat("_CausticScale", 0.75f);
                    m.SetFloat("_FoamWidth", 0.078f); m.SetFloat("_FoamSoft", 0.219f); m.SetFloat("_FoamWobble", 0f);
                    m.SetFloat("_StreakScale", 3.22f); m.SetFloat("_StreakCut", 0.372f); m.SetFloat("_StreakAlpha", 0.132f);
                    m.SetFloat("_WaveHeight", 0.014f);
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
                    // Owner tuning in the fluid lab (02/10).
                    m.SetColor("_ShallowColor", C(0f, 1f, 0.646f)); m.SetColor("_DeepColor", C(0f, 1f, 0.379f));
                    m.SetFloat("_DepthRange", 1.7f); m.SetFloat("_Clarity", 3f); m.SetFloat("_MinAlpha", 0f);
                    m.SetColor("_StreakColor", C(0.082f, 0.585f, 0f)); m.SetFloat("_StreakCut", 0.485f); m.SetFloat("_StreakAlpha", 1f);
                    m.SetFloat("_WaveHeight", 0.1f);
                    m.SetFloat("_FoamNoise", 1f); m.SetFloat("_FoamNoiseScale", 0.85f); m.SetFloat("_FoamNoiseDist", 0.33f);
                    m.SetFloat("_ScumAmount", 0.064f); m.SetFloat("_ScumScale", 0.45f);
                    m.SetColor("_ScumColor", C(0.671f, 1f, 0f)); m.SetColor("_ScumColor2", C(0f, 1f, 0f));
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
                    // Owner tuning in the fluid lab (02/10).
                    m.SetFloat("_Clarity", 1.05f); m.SetFloat("_MinAlpha", 0.026f);
                    m.SetFloat("_FoamWidth", 0.12f); m.SetFloat("_FoamSoft", 0.214f); m.SetFloat("_FoamWobble", 0.061f);
                    m.SetColor("_StreakColor", C(0.611f, 0.796f, 0.317f)); m.SetFloat("_StreakAlpha", 0.448f);
                    m.SetFloat("_BubbleScale", 0.79f); m.SetFloat("_BubbleRate", 0.1f);
                    m.SetFloat("_FoamNoise", 0.462f); m.SetFloat("_FoamNoiseScale", 5.39f); m.SetFloat("_FoamNoiseDist", 0.59f);
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
                    // Owner tuning in the fluid lab (02/10).
                    m.SetFloat("_Clarity", 0.53f); m.SetFloat("_MinAlpha", 0.447f);
                    m.SetColor("_FoamColor", C(1f, 1f, 1f));
                    m.SetColor("_StreakColor", C(0f, 0.057f, 0.094f)); m.SetFloat("_StreakCut", 0.56f); m.SetFloat("_StreakAlpha", 0.2f);
                    m.SetVector("_Flow", new Vector4(0.22f, 0.12f, 0.25f, 0f));
                    m.SetFloat("_CrackTiling", 5.79f); m.SetFloat("_CrackAmount", 1f);
                    m.SetFloat("_FoamNoise", 1f); m.SetFloat("_FoamNoiseDist", 0.24f);
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
                    // MinionsArt's glowing lava by default (owner 02/10: the black crust type sank into
                    // the charcoal ground); the crust type is a trial look.
                    var noise = m.GetTexture("_NoiseTex");
                    Use(m, "HordeCall/Map/Volcano Lava");
                    m.SetTexture("_NoiseTex", noise);
                    LavaMolten(m);
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

        /// <summary>MinionsArt's lava: a glowing body, a network of white-hot veins drifting with the
        /// flow, brighter up the bank, small bubbles. Values tuned by the owner in the editor (02/10).</summary>
        public static void LavaMolten(Material m)
        {
            m.SetFloat("_Lava", 0f); m.EnableKeyword("_LAVA_MOLTEN"); m.DisableKeyword("_LAVA_CRUST");
            m.SetColor("_CrustColor", C(1f, 0f, 0f));
            m.SetColor("_RimColor", C(1f, 0.248f, 0f));
            m.SetColor("_HotColor", C(1f, 1f, 0f));
            m.SetColor("_CoreColor", new Color(5.211f, 4.971f, 0.383f, 1f));
            m.SetColor("_EdgeColor", new Color(1.848f, 1.497f, 0f, 1f));
            m.SetFloat("_Scale", 6f); m.SetFloat("_DistortScale", 3f); m.SetFloat("_Distortion", 1f);
            m.SetVector("_Flow", new Vector4(0.5f, 0.2f, 0.025f, 0f));
            m.SetFloat("_CrustCut", 0.643f); m.SetFloat("_CoreCut", 0.425f);
            m.SetFloat("_RimWidth", 0.069f); m.SetFloat("_EdgeWidth", 0.056f); m.SetFloat("_Pulse", 0f);
            m.SetFloat("_VeinScale", 1.3f); m.SetFloat("_VeinWidth", 0.09f); m.SetFloat("_BankGlow", 0.34f);
            m.SetFloat("_BubbleAmount", 0.293f); m.SetFloat("_BubbleScale", 0.5f); m.SetFloat("_BubbleRate", 0.5f);
        }

        /// <summary>Skinned-over lava: black plates, white-hot seams, boiling vents.</summary>
        public static void LavaCrust(Material m)
        {
            m.SetFloat("_Lava", 1f); m.EnableKeyword("_LAVA_CRUST"); m.DisableKeyword("_LAVA_MOLTEN");
            m.SetColor("_HotColor", C(1f, 0.3f, 0.03f));
            m.SetColor("_CoreColor", new Color(1.6f, 1.05f, 0.3f, 1f));
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
                    // The zone palette (EnvSandboxBuilder, forest) at 82 %: set, never multiplied, so
                    // applying twice gives the same ground.
                    Palette(m, C(0.295f, 0.213f, 0.148f), C(0.164f, 0.303f, 0.164f), C(0.23f, 0.353f, 0.18f), C(0.279f, 0.312f, 0.271f));
                    return true;
                case "swamp":
                    Use(m, "HordeCall/Map/Swamp Ground");
                    Bed(m, C(0.3f, 0.32f, 0.16f), C(0.22f, 0.25f, 0.13f), 0.8f);
                    m.SetFloat("_LitterAmount", 0.25f); m.SetFloat("_LitterScale", 0.55f);
                    m.SetColor("_LitterColor", C(0.32f, 0.25f, 0.12f)); m.SetColor("_LitterColor2", C(0.26f, 0.3f, 0.12f));
                    m.SetFloat("_MossAmount", 0.5f); m.SetColor("_MossColor", C(0.2f, 0.27f, 0.1f));
                    return true;
                case "volcano":
                    // Owner pick 02/10: plain charcoal, no cracks, a rough normal.
                    Use(m, "HordeCall/Map/Volcano Ground");
                    CharcoalRough(m);
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
                    m.SetFloat("_SnowTrailWobble", 0.6f); m.SetFloat("_SnowTrailBreakup", 0.6f);
                    m.SetFloat("_SparkleAmount", 2.5f); m.SetFloat("_SparkleScale", 0.35f);
                    return true;
            }
            return false;
        }

        static void Bed(Material m, Color a, Color b, float strength)
        {
            m.SetColor("_BedColor", a); m.SetColor("_BedColor2", b); m.SetFloat("_BedStrength", strength);
        }

        static void Palette(Material m, Color dry, Color grass, Color sand, Color rock)
        {
            m.SetColor("_DryDebugColor", dry); m.SetColor("_GrassDebugColor", grass);
            m.SetColor("_SandDebugColor", sand); m.SetColor("_RockDebugColor", rock);
        }

        /// <summary>Deep charcoal, no grey; thin hard cracks, lava glowing through near the basins and
        /// faintly everywhere.</summary>
        public static void Charcoal(Material m)
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
            m.SetFloat("_BumpStrength", 0f);
        }

        /// <summary>Trial (owner 02/10): charcoal without cracks, a rough normal instead.</summary>
        public static void CharcoalRough(Material m)
        {
            Charcoal(m);
            m.SetColor("_DryDebugColor", C(0.11f, 0.1f, 0.1f)); m.SetColor("_GrassDebugColor", C(0.09f, 0.085f, 0.085f));
            m.SetColor("_SandDebugColor", C(0.12f, 0.11f, 0.105f)); m.SetColor("_RockDebugColor", C(0.08f, 0.075f, 0.075f));
            m.SetFloat("_CrackStrength", 0f);
            m.SetFloat("_CrackGlowBase", 0f); m.SetFloat("_CrackGlowReach", 0f);
            m.SetFloat("_BumpStrength", 1.2f); m.SetFloat("_BumpScale", 2.5f);
        }

        /// <summary>The trial looks the QA panel switches between: every grade of the map, and on the
        /// volcano the other lava type and the charcoal without cracks.</summary>
        static void WriteDevLooks(System.Text.StringBuilder log)
        {
            const string optDir = "Assets/Settings/MapPost/Options/";
            System.IO.Directory.CreateDirectory(optDir);
            foreach (var id in MapIds)
            {
                var theme = AssetDatabase.LoadAssetAtPath<ZombieWar.World.MapTheme>("Assets/Resources/MapThemes/MapTheme_" + id + ".asset");
                if (theme == null) continue;
                var looks = new System.Collections.Generic.List<ZombieWar.World.MapTheme.LookOption>();
                foreach (var (name, label, grade) in LookShot.GradeOptions(id))
                {
                    string path = optDir + "Post_" + id + "_" + name + ".asset";
                    var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
                    if (profile == null) { profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
                    LookShot.WriteGrade(profile, grade);
                    foreach (var c in profile.components) if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, profile);
                    EditorUtility.SetDirty(profile);
                    looks.Add(new ZombieWar.World.MapTheme.LookOption { name = label, post = profile });
                }
                if (id == "volcano")
                {
                    string dir = Art + "Maps/volcano/Options/";
                    System.IO.Directory.CreateDirectory(dir);
                    var crust = Variant(Art + "Materials/M_Fluid_volcano.mat", dir + "M_Fluid_volcano_crust.mat", LavaCrust);
                    var cracked = Variant(Art + "Maps/volcano/M_MapGround_volcano.mat", dir + "M_MapGround_volcano_cracked.mat", Charcoal);
                    looks.Add(new ZombieWar.World.MapTheme.LookOption { name = "Lava kiểu 2 · vỏ nguội", fluid = crust });
                    looks.Add(new ZombieWar.World.MapTheme.LookOption { name = "Đất than nứt sáng (bản cũ)", ground = cracked });
                }
                theme.devLooks = looks.ToArray();
                EditorUtility.SetDirty(theme);
                log.Append("looks_").Append(id).Append('(').Append(looks.Count).Append(") ");
            }
        }

        static Material Variant(string srcPath, string path, System.Action<Material> look)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>(srcPath);
            if (src == null) return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
            look(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Snow settled on the tundra's props (owner 02/10): painted from the top down, no
        /// vertex push (it tore low-poly hard edges apart), a broken-up edge and a cold grey lip.</summary>
        public static void SnowCover(Material m)
        {
            m.SetFloat("_SnowAmount", 0.3f); m.SetFloat("_SnowSoftness", 0.04f);
            m.SetFloat("_SnowNoise", 0.35f); m.SetFloat("_SnowNoiseScale", 0.6f);
            m.SetColor("_SnowEdgeColor", C(0.62f, 0.68f, 0.8f)); m.SetFloat("_SnowEdgeWidth", 0.22f);
            m.SetFloat("_SnowHeight", 0f);
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
            WriteDevLooks(log);

            // Each map's colour grade (the owner's pick, or the first option until one is picked).
            const string postDir = "Assets/Settings/MapPost/";
            System.IO.Directory.CreateDirectory(postDir);
            foreach (var id in MapIds)
            {
                var theme = AssetDatabase.LoadAssetAtPath<ZombieWar.World.MapTheme>("Assets/Resources/MapThemes/MapTheme_" + id + ".asset");
                if (theme == null) continue;
                var grade = LookShot.PickedGrade(id);
                if (grade == null) { theme.post = null; EditorUtility.SetDirty(theme); continue; }
                string path = postDir + "Post_" + id + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
                if (profile == null) { profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
                LookShot.WriteGrade(profile, grade.Value);
                foreach (var c in profile.components) if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, profile);
                EditorUtility.SetDirty(profile);
                theme.post = profile;
                EditorUtility.SetDirty(theme);
                log.Append("Post_").Append(id).Append(' ');
            }

            // The snow on the tundra's props.
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { Art + "Materials/Snow" }))
            {
                var snow = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (snow == null || snow.shader == null || snow.shader.name != "HordeCall/EnvSandbox/Snow Cover") continue;
                SnowCover(snow);
                EditorUtility.SetDirty(snow);
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
