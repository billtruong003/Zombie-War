using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Env Sandbox (2026-09-30): one dev scene with a 40 × 40 m zone per environment concept
    /// (Review/M8/env_concepts), built from the vendor packs already in the project, so each theme is
    /// judged — and its shaders tried — before anything reaches a game scene. Zones follow the
    /// concept layout: an open fight area in the middle, low props on the middle ring, tall props on
    /// the outer ring, one landmark, the theme's hazard at the edge. The ground uses the game's own
    /// world ground shader with the theme's four layers. No gameplay scene is opened or touched.
    /// </summary>
    public static class EnvSandboxBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dev/EnvSandbox.unity";
        const string Art = "Assets/_Project/Art/EnvSandbox/";
        const string C = "Assets/Cartoon_Texture_Pack/";
        const float Spacing = 60f, Half = 20f;

        static readonly Dictionary<string, string> PackDir = new()
        {
            ["KK"] = "Assets/KayKit/Packs/Bits", ["TT"] = "Assets/Tiny Teacup Studio", ["PG"] = "Assets/Playground Low Poly",
            ["LX"] = "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios", ["SG"] = "Assets/Synty/PolygonGeneric",
            ["DF"] = "Assets/Synty/PolygonDarkFantasy", ["KJ"] = "Assets/Synty/PolygonKaiju", ["MC"] = "Assets/JC_LP_MegaCity/Prefabs",
        };
        // Kaiju is modelled at diorama scale (a car is 0.1 m tall).
        static float PackScale(string pack) => pack == "KJ" ? 14f : 1f;

        // Vendor pieces modelled far bigger than a 1.8 m hero reads in this game.
        static float PieceScale(string key)
        {
            if (key.StartsWith("DF/SM_Env_Cliff_Basalt")) return 0.25f;
            if (key.StartsWith("DF/SM_Env_Basalt")) return 0.3f;
            if (key.StartsWith("DF/SM_Env_Rock_Cliff")) return 0.35f;
            if (key.StartsWith("SG/SM_Gen_Env_Dirt_Cliff")) return 0.45f;
            if (key.StartsWith("MC/SM_Nature_Tree")) return 0.6f;
            if (key.StartsWith("SG/SM_Gen_Env_Tree")) return 0.8f;
            if (key.StartsWith("TT/Cliff")) return 0.8f;
            return 1f;
        }

        sealed class Zone
        {
            public string id, name;
            public int col, row;
            public string[] tex;          // dry, grass, sand, rock
            public Color[] tint;
            public Color[] layerColor;    // the ground shader's per-layer palette (78% of its colour); null = the meadow's
            public float[] tiling = { 6f, 5f, 8f, 7f };
            public int primary, secondary, path, outerCh = 3;
            public string[] mid, outer, landmark, scatter;
            public Vector2[] midPts, outerPts, landmarkPts;
            public Vector2[][] paths = new Vector2[0][], hazards = new Vector2[0][];
            public Vector2[] water = new Vector2[0];
            public Color waterColor;
            public bool snow;
            public int scatterCount = 40;
        }

        static Vector2 V(float x, float y) => new(x, y);
        static Color Col(float r, float g, float b) => new(r, g, b, 1f);

        static List<Zone> Zones() => new()
        {
            new Zone { id = "meadow", name = "Đồng cỏ nông trại", col = 0, row = 0,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_01/Textures/Grass_Dense_Tint_01_Base_Basecolor_A.png",
                              C + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png", C + "ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_B_Basecolor.png" },
                tint = new[] { Color.white, Color.white, Color.white, Color.white }, primary = 1, secondary = 0, path = 0, outerCh = 1,
                mid = new[] { "KK/Food_Crate_Large_Apples", "KK/Food_Barrel_Empty", "KK/Food_Basket_A_Berries", "KK/Containers_Box_Large", "SG/SM_Gen_Env_Log_01", "SG/SM_Gen_Env_Stump_03", "SG/SM_Gen_Env_Rock_03" },
                outer = new[] { "MC/SM_Nature_Tree_09", "MC/SM_Nature_Tree_10", "MC/SM_Nature_Tree_03", "MC/SM_Nature_Tree_02", "SG/SM_Gen_Env_Tree_01", "SG/SM_Gen_Env_Tree_02" },
                landmark = new[] { "KK/Food_Pile_Large" }, scatter = new[] { "LX/S_Grass_01A", "LX/S_Flowers_E", "LX/S_Flowers_C", "SG/SM_Gen_Env_Grass_01", "SG/SM_Gen_Env_Flowers_04" },
                midPts = new[] { V(-9, 8), V(-12, -6), V(8, -9), V(11, 5) }, outerPts = new[] { V(-16, 14), V(-17, -13), V(15, 15), V(17, -12), V(0, 18) }, landmarkPts = new[] { V(13, 13) },
                paths = new[] { new[] { V(-20, -4), V(20, 6) } } },

            new Zone { id = "desert", layerColor = new[] { Col(0.78f, 0.6f, 0.4f), Col(0.93f, 0.8f, 0.55f), Col(0.97f, 0.87f, 0.64f), Col(0.78f, 0.42f, 0.27f) }, name = "Tiền đồn sa mạc", col = 1, row = 0,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png",
                              C + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_A_Basecolor_A.png" },
                tint = new[] { Col(1.1f, 0.9f, 0.72f), Col(1.08f, 0.96f, 0.8f), Col(1.15f, 1.02f, 0.85f), Col(1.2f, 0.72f, 0.5f) }, primary = 1, secondary = 2, path = 0,
                mid = new[] { "TT/Cactus_02", "TT/Cactus_01", "TT/Rock_01", "TT/Rock_02", "TT/Rock_05", "TT/Barrel2", "TT/Barrel4", "KK/Fuel_B_Jerrycan", "KK/Fuel_A_Barrel_Dirty" },
                outer = new[] { "TT/CliffCorner_01", "TT/CliffCorner_02", "TT/Cliff_01", "TT/Rock_04", "TT/Cactus_03", "TT/Container1", "TT/Container4", "TT/Block2" },
                landmark = new[] { "TT/Tower1" }, scatter = new[] { "TT/Rock_05", "LX/S_Grass_02A" }, scatterCount = 18,
                midPts = new[] { V(-8, 7), V(9, 9), V(-10, -7), V(7, -10) }, outerPts = new[] { V(-17, 16), V(-18, -2), V(-15, -16), V(16, -15), V(18, 2) }, landmarkPts = new[] { V(14, 15) },
                paths = new[] { new[] { V(-20, -12), V(20, -16) } } },

            new Zone { id = "tundra", layerColor = new[] { Col(0.82f, 0.87f, 0.93f), Col(0.95f, 0.97f, 1f), Col(0.72f, 0.85f, 0.95f), Col(0.5f, 0.56f, 0.64f) }, name = "Băng nguyên", col = 2, row = 0, snow = true,
                tex = new[] { Art + "Textures/T_Snow_Packed.png", Art + "Textures/T_Snow_Fresh.png", Art + "Textures/T_Snow_Ice.png", Art + "Textures/T_Snow_ColdRock.png" },
                tint = new[] { Col(1.12f, 1.12f, 1.12f), Col(1.16f, 1.13f, 1.1f), Col(1f, 1.08f, 1.15f), Col(1.02f, 1.04f, 1.08f) }, primary = 1, secondary = 0, path = 0,
                mid = new[] { "SG/SM_Gen_Env_Rock_01", "SG/SM_Gen_Env_Rock_03", "SG/SM_Gen_Env_Rock_07", "SG/SM_Gen_Env_Stump_01", "SG/SM_Gen_Env_Rock_Pebbles_02", "KK/Containers_Crate_Large", "KK/Fuel_A_Barrels", "KK/Containers_Box_Large_Dirty" },
                outer = new[] { "SG/SM_Gen_Env_Tree_Dead_01", "SG/SM_Gen_Env_Tree_Dead_02", "SG/SM_Gen_Env_Tree_Dead_03", "SG/SM_Gen_Env_Tree_Pine_01", "SG/SM_Gen_Env_Tree_Pine_02", "SG/SM_Gen_Env_Tree_Pine_03", "DF/SM_Env_Rock_Cliff_05", "DF/SM_Env_Rock_02" },
                landmark = new[] { "DF/SM_Env_Rock_Cliff_Arch_01" }, scatter = new[] { "SG/SM_Gen_Env_Rock_Pebbles_03", "SG/SM_Gen_Env_Twig_01", "SG/SM_Gen_Env_Twig_03" }, scatterCount = 22,
                midPts = new[] { V(-9, 7), V(-11, -8), V(10, 9) }, outerPts = new[] { V(-17, 15), V(-18, -12), V(16, 16), V(0, -18), V(18, 0) }, landmarkPts = new[] { V(-14, -15) },
                water = new[] { V(9, -8) }, waterColor = Col(0.72f, 0.86f, 0.96f) },

            new Zone { id = "volcano", layerColor = new[] { Col(0.25f, 0.24f, 0.27f), Col(0.36f, 0.27f, 0.27f), Col(0.32f, 0.22f, 0.18f), Col(0.2f, 0.2f, 0.23f) }, name = "Núi lửa", col = 3, row = 0,
                tex = new[] { C + "ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_A_Basecolor.png", C + "ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_C_Basecolor.png",
                              C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_A_Basecolor.png" },
                tint = new[] { Col(0.78f, 0.7f, 0.68f), Col(0.7f, 0.64f, 0.63f), Col(0.55f, 0.42f, 0.36f), Col(0.55f, 0.5f, 0.5f) }, primary = 1, secondary = 0, path = 2,
                mid = new[] { "DF/SM_Env_Rocks_Small_01", "DF/SM_Env_Rocks_Small_02", "DF/SM_Env_Rock_01", "DF/SM_Env_Rock_02", "DF/SM_Env_Rock_03", "DF/SM_Env_Basalt_03", "KK/Fuel_A_Barrel_Dirty" },
                outer = new[] { "DF/SM_Env_Basalt_01", "DF/SM_Env_Basalt_02", "DF/SM_Env_Basalt_04", "DF/SM_Env_Basalt_05", "DF/SM_Env_Cliff_Basalt_02", "DF/SM_Env_Tree_Dead_01", "DF/SM_Env_Tree_Dead_03" },
                landmark = new[] { "DF/SM_Env_Cliff_Basalt_03" }, scatter = new[] { "DF/SM_Env_Grunge_05", "DF/SM_Env_Rocks_Small_02" }, scatterCount = 20,
                midPts = new[] { V(-9, 8), V(9, 8), V(8, -14) }, outerPts = new[] { V(-17, 16), V(15, 17), V(-17, -16), V(18, -2) }, landmarkPts = new[] { V(0, 17) },
                hazards = new[] { new[] { V(-20, -6), V(-4, -2), V(20, -9) } } },

            new Zone { id = "forest", layerColor = new[] { Col(0.42f, 0.3f, 0.2f), Col(0.26f, 0.45f, 0.22f), Col(0.36f, 0.52f, 0.25f), Col(0.4f, 0.44f, 0.38f) }, name = "Rừng sâu", col = 0, row = 1,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_02/Textures/Grass_Dense_Tint_02_Base_Basecolor_A.png",
                              C + "GRASS/GRASS_Flower/GRASS_Flower_Tint_02/Texture/Grass_Flower_Tint_02_Base_Basecolor.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_B_Basecolor_A.png" },
                tint = new[] { Col(0.85f, 0.8f, 0.72f), Col(0.85f, 0.95f, 0.8f), Color.white, Col(0.8f, 0.9f, 0.8f) }, primary = 1, secondary = 2, path = 0, outerCh = 0,
                mid = new[] { "SG/SM_Gen_Env_Mushroom_01", "SG/SM_Gen_Env_Mushroom_03", "SG/SM_Gen_Env_Leaves_Pile_01", "SG/SM_Gen_Env_Log_02", "SG/SM_Gen_Env_Stump_02", "SG/SM_Gen_Env_Fern_01", "LX/S_Fern_B", "LX/S_Fern_D" },
                outer = new[] { "MC/SM_Nature_Tree_07", "MC/SM_Nature_Tree_08", "MC/SM_Nature_Tree_10", "SG/SM_Gen_Env_Tree_Pine_01", "SG/SM_Gen_Env_Tree_Pine_03", "SG/SM_Gen_Env_Tree_03", "SG/SM_Gen_Env_Dirt_Cliff_05" },
                landmark = new[] { "SG/SM_Gen_Env_Dirt_Cliff_07" }, scatter = new[] { "SG/SM_Gen_Env_Leaves_01", "SG/SM_Gen_Env_Leaves_02", "SG/SM_Gen_Env_Grass_05", "LX/S_Clovers_A" },
                midPts = new[] { V(-8, 8), V(9, 6), V(-9, -9), V(8, -9) }, outerPts = new[] { V(-17, 15), V(-18, 0), V(-16, -15), V(16, -16), V(18, 1), V(15, 16), V(0, 18), V(0, -18) }, landmarkPts = new Vector2[0],
                paths = new[] { new[] { V(-20, 0), V(20, -2) } } },

            new Zone { id = "swamp", layerColor = new[] { Col(0.3f, 0.33f, 0.2f), Col(0.42f, 0.5f, 0.22f), Col(0.4f, 0.45f, 0.32f), Col(0.32f, 0.36f, 0.3f) }, name = "Đầm độc", col = 1, row = 1,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_02/Textures/Grass_Dense_Tint_02_Base_Basecolor_A.png",
                              C + "SAND/SAND_Underwater/Textures/Sand_Underwater_Base_Basecolor.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_B_Basecolor_A.png" },
                tint = new[] { Col(0.62f, 0.72f, 0.45f), Col(0.8f, 0.9f, 0.55f), Col(0.7f, 0.8f, 0.6f), Col(0.6f, 0.7f, 0.6f) }, primary = 0, secondary = 1, path = 2,
                mid = new[] { "LX/S_Cattail_A", "LX/S_Cattail_B", "LX/S_Clovers_A", "SG/SM_Gen_Env_Mushroom_02", "SG/SM_Gen_Env_Root_01", "SG/SM_Gen_Env_Root_02", "KK/Fuel_C_Barrel_Dirty" },
                outer = new[] { "SG/SM_Gen_Env_Tree_Dead_01", "SG/SM_Gen_Env_Tree_Dead_02", "SG/SM_Gen_Env_Tree_Dead_03", "DF/SM_Env_Tree_Dead_02", "DF/SM_Env_Tree_Dead_04", "SG/SM_Gen_Env_Dirt_Cliff_02" },
                landmark = new[] { "KK/Fuel_C_Barrels" }, scatter = new[] { "SG/SM_Gen_Env_Grass_Tall_02", "LX/S_Clovers_B", "SG/SM_Gen_Env_Lilypads_02" },
                midPts = new[] { V(8, 9), V(-10, -9) }, outerPts = new[] { V(-17, 15), V(16, 16), V(-17, -15), V(17, -3) }, landmarkPts = new[] { V(15, -15) },
                water = new[] { V(-9, 6), V(10, -8) }, waterColor = Col(0.45f, 0.78f, 0.22f) },

            new Zone { id = "city", layerColor = new[] { Col(0.42f, 0.4f, 0.38f), Col(0.4f, 0.48f, 0.3f), Col(0.62f, 0.63f, 0.66f), Col(0.3f, 0.31f, 0.34f) }, name = "Phố đổ nát", col = 2, row = 1,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_01/Textures/Grass_Dense_Tint_01_Base_Basecolor_A.png",
                              C + "WALLS/WALL_Stone/Textures/Wall_Stone_A_Basecolor.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_B_Basecolor_A.png" },
                tint = new[] { Col(0.72f, 0.72f, 0.74f), Col(0.7f, 0.78f, 0.6f), Col(0.72f, 0.75f, 0.82f), Col(0.62f, 0.64f, 0.68f) }, primary = 2, secondary = 0, path = 3,
                mid = new[] { "MC/SM_FloorProps_01", "MC/SM_FloorProps_02", "MC/SM_FloorProps_03", "MC/SM_FloorProps_04", "MC/SM_FloorProps_05", "KJ/SM_Prop_Rubble_01", "KJ/SM_Prop_Rubble_02", "KK/Containers_Box_Large_Dirty" },
                outer = new[] { "MC/SM_FloorProps_BusStation_01", "MC/SM_FloorProps_Light_01", "KJ/SM_Prop_Bridge_Rubble_02", "KJ/SM_Prop_Bridge_Rubble_03", "KJ/SM_Prop_Car_01", "KJ/SM_Prop_Taxi_01", "KJ/SM_Prop_Bus_01" },
                landmark = new[] { "KJ/SM_Prop_Crane_01" }, scatter = new[] { "SG/SM_Gen_Prop_Papers_04", "MC/SM_FloorProps_06" }, scatterCount = 16,
                midPts = new[] { V(-7, 6), V(7, -6), V(6, 7) }, outerPts = new[] { V(-16, 15), V(15, -15), V(-16, -15), V(16, 15) }, landmarkPts = new[] { V(15, 15) },
                paths = new[] { new[] { V(-20, 0), V(20, 0) }, new[] { V(0, -20), V(0, 20) } } },

            new Zone { id = "toy", layerColor = new[] { Col(0.72f, 0.55f, 0.38f), Col(0.45f, 0.72f, 0.35f), Col(0.95f, 0.86f, 0.62f), Col(0.6f, 0.6f, 0.62f) }, name = "Sân chơi đồ chơi", col = 3, row = 1,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_01/Textures/Grass_Dense_Tint_01_Base_Basecolor_A.png",
                              C + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_A_Basecolor_A.png" },
                tint = new[] { Col(1.05f, 0.95f, 0.85f), Col(1f, 1.1f, 0.9f), Col(1.1f, 1.05f, 0.9f), Color.white }, primary = 1, secondary = 2, path = 0, outerCh = 1,
                mid = new[] { "PG/SM_Toy_01", "PG/SM_Toy_04", "PG/SM_Toy_07", "PG/SM_Ball_01", "PG/SM_Bench_02", "PG/SM_Flowers_03", "PG/SM_Stone_01" },
                outer = new[] { "PG/SM_Playground_01", "PG/SM_Playground_02", "PG/SM_Playground_05", "PG/SM_Swings_03", "PG/SM_Game_Piramid_01", "PG/SM_Tree_01", "PG/SM_Tree_02", "PG/SM_Tree_03", "PG/SM_Alcove_01" },
                landmark = new[] { "PG/SM_Archway_01" }, scatter = new[] { "PG/SM_Grass_02", "PG/SM_Flowers_01" },
                midPts = new[] { V(-8, 7), V(8, 8), V(-7, -8), V(9, -8) }, outerPts = new[] { V(-16, 15), V(16, 15), V(-16, -15), V(16, -15) }, landmarkPts = new[] { V(0, 16) },
                paths = new[] { new[] { V(-20, -2), V(20, -2) } } },
        };

        [MenuItem("HordeCall/World/Build Env Sandbox")]
        public static string Build()
        {
            Directory.CreateDirectory(Art + "Materials/Snow");
            Directory.CreateDirectory(Art + "Textures");
            EnsureSnowTextures();

            var prevActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var log = new System.Text.StringBuilder();
            try
            {
                // No skybox: the game camera never shows the sky. Flat three-colour ambient.
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.262f, 0.413f, 0.631f);
                RenderSettings.ambientEquatorColor = new Color(0.529f, 0.592f, 0.665f);
                RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.2f);
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.fog = false;

                var root = new GameObject("EnvSandbox");
                var rig = new GameObject("ToonLightRig");
                rig.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                rig.AddComponent<ToonLightRig>();

                foreach (var z in Zones()) log.Append(BuildZone(z, root.transform)).Append("; ");

                var overview = new GameObject("Cam_Overview").AddComponent<Camera>();
                overview.enabled = false;
                overview.transform.SetPositionAndRotation(new Vector3(Spacing * 1.5f, 120f, Spacing * 0.5f - 95f), Quaternion.Euler(52f, 0f, 0f));
                overview.fieldOfView = 55f;
                overview.clearFlags = CameraClearFlags.SolidColor;
                overview.backgroundColor = new Color(0.2f, 0.25f, 0.32f);
                overview.farClipPlane = 600f;

                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            return log.ToString();
        }

        static string BuildZone(Zone z, Transform root)
        {
            var origin = new Vector3(z.col * Spacing, 0f, z.row * Spacing);
            var zr = new GameObject("Zone_" + z.id).transform;
            zr.SetParent(root, false);
            zr.position = origin;
            var rng = new System.Random(z.id.GetHashCode());

            // Ground: the world ground shader, the theme's four layers, weights painted by the layout.
            var ground = new GameObject("Ground");
            ground.transform.SetParent(zr, false);
            ground.AddComponent<MeshFilter>().sharedMesh = GroundMesh(z, origin);
            ground.AddComponent<MeshRenderer>().sharedMaterial = GroundMaterial(z);

            // The game camera frames about 12 × 20 m, so every screen needs its own dressing: low
            // clusters on a jittered 7 m grid across the whole zone (the player's own spot stays
            // clear), tall pieces along the border and a few inside, the landmark within one screen.
            int placed = 0;
            for (float gx = -17f; gx <= 17.1f; gx += 7f)
                for (float gz = -17f; gz <= 17.1f; gz += 7f)
                {
                    var c = new Vector2(gx + (float)(rng.NextDouble() * 4 - 2), gz + (float)(rng.NextDouble() * 4 - 2));
                    if (c.magnitude < 4.5f || OnLine(c, z)) continue;
                    bool border = Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y)) > 15f;
                    if (border || rng.NextDouble() < 0.18) placed += Scatter(z, zr, rng, new[] { c }, z.outer, 1, 1.2f);
                    else placed += Scatter(z, zr, rng, new[] { c }, z.mid, 1 + rng.Next(3), 1.8f);
                }
            placed += Scatter(z, zr, rng, new[] { new Vector2(5f, 9f) }, z.landmark, 1, 0f);
            // Hero for scale: 1.8 m, where the player stands.
            var hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(hero.GetComponent<Collider>());
            hero.name = "Hero_1.8m";
            hero.transform.SetParent(zr, false);
            hero.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            hero.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            hero.GetComponent<MeshRenderer>().sharedMaterial = Flat("M_HeroScale", new Color(0.24f, 0.56f, 0.94f));
            // Loose ground detail over the whole zone, kept out of the middle of the fight area.
            for (int i = 0; i < z.scatterCount; i++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * Half * 0.95f, (float)(rng.NextDouble() * 2 - 1) * Half * 0.95f);
                if (p.magnitude < 5f) continue;
                if (Place(z.scatter[rng.Next(z.scatter.Length)], zr, p, (float)rng.NextDouble() * 360f, 1f)) placed++;
            }

            foreach (var w in z.water) Water(zr, w, z.waterColor, z.id);
            foreach (var h in z.hazards) Lava(zr, h);

            if (z.snow)
            {
                SnowCover(zr);
                var fall = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Epic Toon FX/Prefabs/Environment/Weather/Snow/SnowLight.prefab");
                if (fall != null)
                {
                    var f = (GameObject)PrefabUtility.InstantiatePrefab(fall, zr);
                    f.name = "Snowfall_WebParticles";
                    f.transform.localPosition = new Vector3(0f, 14f, 0f);
                }
            }

            var label = new GameObject("Label").AddComponent<TMPro.TextMeshPro>();
            label.transform.SetParent(zr, false);
            label.transform.localPosition = new Vector3(0f, 0.2f, -Half - 1.5f);
            label.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            label.text = z.name;
            label.fontSize = 18f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(40f, 5f);
            label.color = Color.white;

            // The game's camera: offset (0, 12, -8) looking down at 60°, FOV 60, portrait.
            var cam = new GameObject("Cam_" + z.id).AddComponent<Camera>();
            cam.transform.SetParent(zr, false);
            cam.enabled = false;
            cam.transform.localPosition = new Vector3(0f, 12f, -8f);
            cam.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.25f, 0.32f);
            var wide = new GameObject("Cam_" + z.id + "_Hero").AddComponent<Camera>();
            var heroCam = wide;
            heroCam.transform.SetParent(zr, false);
            heroCam.enabled = false;
            heroCam.transform.localPosition = new Vector3(18f, 26f, -38f);
            heroCam.transform.LookAt(origin + new Vector3(0f, 0f, 1f));
            heroCam.fieldOfView = 42f;
            heroCam.clearFlags = CameraClearFlags.SolidColor;
            heroCam.backgroundColor = new Color(0.2f, 0.25f, 0.32f);
            return z.id + " " + placed;
        }

        static bool OnLine(Vector2 p, Zone z)
        {
            foreach (var l in z.paths) if (DistToPolyline(p, l) < 2.5f) return true;
            foreach (var l in z.hazards) if (DistToPolyline(p, l) < 3f) return true;
            foreach (var w in z.water) if (Vector2.Distance(p, w) < 6f) return true;
            return false;
        }

        static int Scatter(Zone z, Transform zr, System.Random rng, Vector2[] pts, string[] kit, int perPoint, float radius)
        {
            int n = 0;
            if (pts == null || kit == null || kit.Length == 0) return 0;
            foreach (var c in pts)
                for (int i = 0; i < perPoint; i++)
                {
                    var off = new Vector2((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1)) * radius;
                    if (Place(kit[rng.Next(kit.Length)], zr, c + off, (float)rng.NextDouble() * 360f, 0.9f + (float)rng.NextDouble() * 0.2f)) n++;
                }
            return n;
        }

        static readonly Dictionary<string, GameObject> PrefabCache = new();

        static bool Place(string key, Transform parent, Vector2 at, float yaw, float scale)
        {
            var pack = key.Substring(0, 2);
            var name = key.Substring(3);
            if (!PrefabCache.TryGetValue(key, out var prefab))
            {
                prefab = null;
                foreach (var g in AssetDatabase.FindAssets(name + " t:Prefab", new[] { PackDir[pack] }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (Path.GetFileNameWithoutExtension(p) == name) { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p); break; }
                }
                PrefabCache[key] = prefab;
                if (prefab == null) Debug.LogWarning("[EnvSandbox] missing prefab " + key);
            }
            if (prefab == null) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = new Vector3(at.x, 0f, at.y);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            go.transform.localScale *= scale * PackScale(pack) * PieceScale(key);
            return true;
        }

        // ── ground

        static Mesh GroundMesh(Zone z, Vector3 origin)
        {
            const int N = 40;
            var verts = new Vector3[(N + 1) * (N + 1)];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                {
                    int k = j * (N + 1) + i;
                    var p = new Vector2(-Half + i * (2 * Half / N), -Half + j * (2 * Half / N));
                    verts[k] = new Vector3(p.x, 0f, p.y);
                    uvs[k] = new Vector2(i / (float)N, j / (float)N);
                    cols[k] = Weights(z, p, origin);
                }
            var tris = new int[N * N * 6];
            int t = 0;
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int a = j * (N + 1) + i, b = a + N + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            var m = new Mesh { name = "EnvGround_" + z.id, vertices = verts, colors = cols, uv = uvs, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            string path = Art + "Materials/EnvGround_" + z.id + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Color Weights(Zone z, Vector2 p, Vector3 origin)
        {
            var w = new float[4];
            w[z.primary] = 1f;
            float n = Mathf.PerlinNoise((p.x + origin.x) * 0.08f + 3.1f, (p.y + origin.z) * 0.08f + 7.7f);
            w[z.secondary] += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.7f, n));
            float edge = Mathf.InverseLerp(14f, 19f, Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)));
            w[z.outerCh] += edge * 0.8f;
            foreach (var path in z.paths)
            {
                float d = DistToPolyline(p, path);
                w[z.path] += Mathf.InverseLerp(3f, 1.2f, d) * 2f;
            }
            float sum = w[0] + w[1] + w[2] + w[3];
            return new Color(w[0] / sum, w[1] / sum, w[2] / sum, w[3] / sum);
        }

        static float DistToPolyline(Vector2 p, Vector2[] line)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                var a = line[i]; var b = line[i + 1];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static Material GroundMaterial(Zone z)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_WorldStreamingGround.mat");
            string path = Art + "Materials/M_Ground_" + z.id + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            string[] texProps = { "_DryTex", "_GrassTex", "_SandTex", "_RockTex" };
            string[] tintProps = { "_DryTint", "_GrassTint", "_SandTint", "_RockTint" };
            string[] tileProps = { "_DryTiling", "_GrassTiling", "_SandTiling", "_RockTiling" };
            string[] paletteProps = { "_DryDebugColor", "_GrassDebugColor", "_SandDebugColor", "_RockDebugColor" };
            for (int i = 0; i < 4; i++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(z.tex[i]);
                if (tex == null) Debug.LogWarning("[EnvSandbox] missing texture " + z.tex[i]);
                m.SetTexture(texProps[i], tex);
                m.SetColor(tintProps[i], z.tint[i]);
                m.SetFloat(tileProps[i], z.tiling[i]);
                if (z.layerColor != null) m.SetColor(paletteProps[i], z.layerColor[i]);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ── hazards and water (flat placeholders until their shaders are made here)

        static void Water(Transform zr, Vector2 at, Color color, string id)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Water";
            go.transform.SetParent(zr, false);
            go.transform.localPosition = new Vector3(at.x, 0.02f, at.y);
            go.transform.localScale = new Vector3(11f, 0.01f, 7.6f);
            go.GetComponent<MeshRenderer>().sharedMaterial = Flat("M_Water_" + id, color);
        }

        static void Lava(Transform zr, Vector2[] line)
        {
            for (int i = 0; i + 1 < line.Length; i++)
            {
                var a = line[i]; var b = line[i + 1];
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "Lava";
                go.transform.SetParent(zr, false);
                var mid = (a + b) * 0.5f;
                go.transform.localPosition = new Vector3(mid.x, 0.02f, mid.y);
                go.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, 0f);
                go.transform.localScale = new Vector3(Vector2.Distance(a, b) + 2f, 0.02f, 3f);
                go.GetComponent<MeshRenderer>().sharedMaterial = Flat("M_Lava_Placeholder", new Color(1f, 0.45f, 0.1f));
            }
        }

        static Material Flat(string name, Color color)
        {
            string path = Art + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ── snow: every prop of the zone gets a Snow Cover copy of its material

        static void SnowCover(Transform zr)
        {
            var shader = Shader.Find("HordeCall/EnvSandbox/Snow Cover");
            var cache = new Dictionary<Material, Material>();
            foreach (var r in zr.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name == "Ground" || r.name == "Water" || r.name == "Hero_1.8m") continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (!cache.TryGetValue(src, out var snow))
                    {
                        string path = Art + "Materials/Snow/" + src.name.Replace("/", "_") + "_Snow.mat";
                        snow = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (snow == null) { snow = new Material(shader); AssetDatabase.CreateAsset(snow, path); }
                        snow.shader = shader;
                        // URP (_BaseMap), legacy (_MainTex) and Synty's own shaders (_Albedo_Map / _Albedo_Tint).
                        Texture tex = null;
                        foreach (var t in new[] { "_BaseMap", "_MainTex", "_Albedo_Map" })
                            if (tex == null && src.HasProperty(t)) tex = src.GetTexture(t);
                        var col = Color.white;
                        foreach (var c in new[] { "_BaseColor", "_Color", "_Albedo_Tint" })
                            if (src.HasProperty(c)) { col = src.GetColor(c); break; }
                        snow.SetTexture("_BaseMap", tex);
                        snow.SetColor("_BaseColor", col);
                        // Only faces within ~60° of straight up hold snow; sides and trunks stay dark,
                        // so props keep their shape against a white ground.
                        snow.SetFloat("_SnowAmount", 0.26f);
                        snow.SetFloat("_SnowHeight", 0.06f);
                        EditorUtility.SetDirty(snow);
                        cache[src] = snow;
                    }
                    mats[i] = snow;
                }
                r.sharedMaterials = mats;
            }
        }

        // ── snow ground layers: the meadow's own Cartoon Texture Pack layers re-graded

        static void EnsureSnowTextures()
        {
            Derive(C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", "T_Snow_Packed", (c, l) => Cool(0.70f + 0.22f * l, 0.88f, 0.92f, 0.98f));
            Derive(C + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png", "T_Snow_Fresh", (c, l) => Cool(0.86f + 0.14f * l, 0.93f, 0.96f, 1f));
            Derive(C + "SAND/SAND_Underwater/Textures/Sand_Underwater_Base_Basecolor.png", "T_Snow_Ice", (c, l) => Cool(0.62f + 0.3f * l, 0.8f, 0.9f, 1f));
            Derive(C + "ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_B_Basecolor.png", "T_Snow_ColdRock", (c, l) => Cool(0.34f + 0.42f * l, 0.8f, 0.86f, 0.95f));
        }

        static Color Cool(float v, float r, float g, float b) => new(v * r, v * g, v * b, 1f);

        static void Derive(string srcPath, string name, System.Func<Color, float, Color> grade)
        {
            string outPath = Art + "Textures/" + name + ".png";
            if (File.Exists(outPath)) return;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(srcPath));
            var rt = RenderTexture.GetTemporary(1024, 1024, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var small = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = small.GetPixels();
            float lo = 1f, hi = 0f;
            foreach (var c in px) { lo = Mathf.Min(lo, c.grayscale); hi = Mathf.Max(hi, c.grayscale); }
            float span = Mathf.Max(0.05f, hi - lo);
            for (int i = 0; i < px.Length; i++) px[i] = grade(px[i], (px[i].grayscale - lo) / span);
            small.SetPixels(px); small.Apply();
            File.WriteAllBytes(outPath, small.EncodeToPNG());
            Object.DestroyImmediate(tex); Object.DestroyImmediate(small);
            AssetDatabase.ImportAsset(outPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(outPath);
            imp.maxTextureSize = 1024; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }

        // ── captures: each zone from the game camera and a hero angle, plus an overview

        [MenuItem("HordeCall/World/Capture Env Sandbox")]
        public static string Capture()
        {
            const string outDir = "Review/M8/env_sandbox/";
            Directory.CreateDirectory(outDir);
            var prevActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            int n = 0;
            try
            {
                foreach (var rig in Object.FindObjectsByType<ToonLightRig>(FindObjectsSortMode.None))
                    if (rig.gameObject.scene == scene) { rig.enabled = false; rig.enabled = true; }
                foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (cam.gameObject.scene != scene) continue;
                    bool portrait = !cam.name.EndsWith("_Hero") && cam.name != "Cam_Overview";
                    int w = portrait ? 540 : 1280, h = portrait ? 960 : 720;
                    var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
                    cam.targetTexture = rt;
                    cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                    RenderTexture.active = prev;
                    cam.targetTexture = null;
                    File.WriteAllBytes(outDir + cam.name.Replace("Cam_", "") + ".png", tex.EncodeToPNG());
                    Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                    n++;
                }
            }
            finally
            {
                if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            return n + " captures";
        }
    }
}
