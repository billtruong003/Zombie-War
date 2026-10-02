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
    public static partial class EnvSandboxBuilder
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
            ["EP"] = "Assets/_Project/Art/EnvPalette/Grass",
            // 2026-10-02 (owner): props from the five KayKit packs; every tree, bush, grass and flower from
            // the Stylized Nature MegaKit only (KayKit's leafy trees are solid low poly and clash with
            // its alpha-cut leaves; its bare trunks are fine); KayKit Forest gives rocks; no Synty piece on any map.
            ["KF"] = "Assets/KayKit/Packs/ForestNature", ["KH"] = "Assets/KayKit/Packs/HalloweenBits",
            ["KX"] = "Assets/KayKit/Packs/HolidayBits", ["KD"] = "Assets/KayKit/Packs/Dungeon",
            ["SN"] = "Assets/ThirdParty/StylizedNatureMegaKit",
        };
        // Kaiju is modelled at diorama scale (a car is 0.1 m tall).
        // KayKit Dungeon and Halloween are modelled for a ~2.5 m character, Holiday a little smaller.
        static float PackScale(string pack) => pack switch { "KJ" => 14f, "KD" => 0.6f, "KH" => 0.65f, "KX" => 0.75f, _ => 1f };

        // Vendor pieces modelled far bigger than a 1.8 m hero reads in this game.
        static float PieceScale(string key)
        {
            // Stylized Nature MegaKit is modelled big (a grass tuft 1.3–1.9 m, a twisted tree 16–19 m):
            // trees land at 5–8 m like the Lux trees they replace, cover under the hero's knee.
            // CommonTree_5, TwistedTree_2 and Flower_4 are left out: their leaf cards sit outside the
            // texture (UV -1..2), which keeps their texture out of the foliage atlas for every model.
            if (key.StartsWith("SN/TwistedTree")) return 0.4f;
            if (key.StartsWith("SN/DeadTree")) return 0.45f;
            if (key.StartsWith("SN/CommonTree") || key.StartsWith("SN/Pine")) return 0.75f;
            if (key.StartsWith("SN/Bush")) return 0.85f;
            // Seen from the top-down camera, thin upright blades read as specks: cover is kept near
            // knee height but wide, and leans on the flat leafy pieces (clover, Plant_7, fern).
            if (key.StartsWith("SN/Grass_Common_Tall") || key.StartsWith("SN/Grass_Wispy_Tall")) return 0.7f;
            if (key.StartsWith("SN/Grass")) return 0.85f;
            if (key.StartsWith("SN/Flower")) return 0.55f;
            if (key.StartsWith("SN/Clover")) return 0.75f;
            if (key.StartsWith("SN/Fern")) return 0.45f;
            if (key.StartsWith("SN/Plant_7")) return 1.2f;
            if (key.StartsWith("SN/Petal")) return 1.4f;
            if (key.StartsWith("SN/Plant_1_Big")) return 0.4f;
            if (key.StartsWith("SN/Plant_1")) return 0.7f;
            if (key.StartsWith("SN/Rock_Medium")) return 0.9f;
            if (key.StartsWith("KF/Tree_Bare")) return 0.9f;   // bare trunks have no leaves to clash, so KayKit is fine
            if (key.StartsWith("KD/rubble") || key.StartsWith("KD/pillar")) return 1.15f;   // back to ~2.5 m after the pack scale
            if (key.StartsWith("DF/SM_Env_Cliff_Basalt")) return 0.25f;
            if (key.StartsWith("DF/SM_Env_Basalt")) return 0.3f;
            if (key.StartsWith("DF/SM_Env_Rock_Cliff")) return 0.35f;
            if (key.StartsWith("SG/SM_Gen_Env_Dirt_Cliff")) return 0.45f;
            if (key.StartsWith("MC/SM_Nature_Tree")) return 0.6f;
            if (key.StartsWith("SG/SM_Gen_Env_Tree")) return 0.8f;
            if (key.StartsWith("TT/Cliff")) return 0.8f;
            if (key.StartsWith("TT/Cactus")) return 2.2f;   // Tiny Teacup cacti are knee-high as shipped
            if (key.StartsWith("LX/S_Tree")) return 0.7f;
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
            public Vector2[][] paths = new Vector2[0][];
            public bool snow;
            public int scatterCount = 40;
            public float gridStep = 7f;
            // Basins (01/10): pools (x, z, radius) and rivers sink the ground; the fluid fills them.
            public Vector3[] pools = new Vector3[0];
            public Vector2[][] rivers = new Vector2[0][];
            public float riverWidth = 1.8f, basinDepth = 1f;
            public string fluid;           // water, toxic, lava, ice
            public int bankChannel = 3;
            public float crack, crackGlow; // ground crack darkening, and glow reach near basins
        }

        static Vector2 V(float x, float y) => new(x, y);
        static Color Col(float r, float g, float b) => new(r, g, b, 1f);

        static List<Zone> Zones() => new()
        {
            new Zone { id = "meadow", name = "Đồng cỏ nông trại", col = 0, row = 0,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_01/Textures/Grass_Dense_Tint_01_Base_Basecolor_A.png",
                              C + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png", C + "ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_B_Basecolor.png" },
                tint = new[] { Color.white, Color.white, Color.white, Color.white }, primary = 1, secondary = 0, path = 0, outerCh = 1,
                mid = new[] { "KK/Food_Crate_Large_Apples", "KK/Food_Barrel_Empty", "KK/Food_Basket_A_Berries", "KK/Containers_Box_Large", "KK/Wood_Log_A", "KK/Wood_Log_Stack", "KK/Pallet_Wood_Covered_A", "KF/Rock_1_E_Color1", "KF/Rock_2_C_Color1", "SN/Rock_Medium_2", "SN/Bush_Common_Flowers", "KH/pumpkin_orange", "SN/Clover_2" },
                outer = new[] { "SN/CommonTree_1", "SN/CommonTree_2", "SN/CommonTree_3", "SN/CommonTree_4", "SN/CommonTree_4", "SN/CommonTree_1", "SN/Bush_Common_Flowers", "KH/fence_seperate" },
                landmark = new[] { "KK/Food_Pile_Large" }, scatter = new[] { "SN/Grass_Wispy_Short", "SN/Grass_Common_Short", "SN/Grass_Common_Short", "SN/Clover_1", "SN/Clover_2", "SN/Plant_7", "SN/Flower_3_Group", "SN/Flower_3_Group", "SN/Flower_3_Single", "SN/Petal_1", "SN/Plant_7", "SN/Pebble_Round_2" },
                midPts = new[] { V(-9, 8), V(-12, -6), V(8, -9), V(11, 5) }, outerPts = new[] { V(-16, 14), V(-17, -13), V(15, 15), V(17, -12), V(0, 18) }, landmarkPts = new[] { V(13, 13) },
                paths = new[] { new[] { V(-20, -4), V(20, 6) } }, gridStep = 4.5f, scatterCount = 170,
                pools = new[] { new Vector3(9f, -10f, 5f) }, rivers = new[] { new[] { V(-20, 13), V(-8, 7), V(2, -3), V(9, -10) } }, riverWidth = 1.7f,
                fluid = "water", bankChannel = 0, basinDepth = 0.9f },

            new Zone { id = "desert", layerColor = new[] { Col(0.78f, 0.6f, 0.4f), Col(0.93f, 0.8f, 0.55f), Col(0.97f, 0.87f, 0.64f), Col(0.78f, 0.42f, 0.27f) }, name = "Tiền đồn sa mạc", col = 1, row = 0,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png",
                              C + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_A_Basecolor_A.png" },
                tint = new[] { Col(1.1f, 0.9f, 0.72f), Col(1.08f, 0.96f, 0.8f), Col(1.15f, 1.02f, 0.85f), Col(1.2f, 0.72f, 0.5f) }, primary = 1, secondary = 2, path = 0,
                mid = new[] { "TT/Cactus_02", "TT/Cactus_01", "TT/Rock_01", "TT/Rock_02", "TT/Rock_05", "TT/Barrel2", "TT/Barrel4", "KK/Fuel_B_Jerrycan", "KK/Fuel_A_Barrel_Dirty" },
                outer = new[] { "TT/CliffCorner_01", "TT/CliffCorner_02", "TT/Cliff_01", "TT/Rock_04", "TT/Cactus_03", "TT/Container1", "TT/Container4", "TT/Block2" },
                landmark = new[] { "TT/Tower1" }, scatter = new[] { "TT/Rock_05", "SN/Grass_Wispy_Short" }, scatterCount = 18, crack = 0.35f,
                midPts = new[] { V(-8, 7), V(9, 9), V(-10, -7), V(7, -10) }, outerPts = new[] { V(-17, 16), V(-18, -2), V(-15, -16), V(16, -15), V(18, 2) }, landmarkPts = new[] { V(14, 15) },
                paths = new[] { new[] { V(-20, -12), V(20, -16) } } },

            new Zone { id = "tundra", layerColor = new[] { Col(0.56f, 0.62f, 0.71f), Col(0.66f, 0.72f, 0.81f), Col(0.46f, 0.6f, 0.74f), Col(0.33f, 0.38f, 0.47f) }, name = "Băng nguyên", col = 2, row = 0, snow = true,
                tex = new[] { Art + "Textures/T_Snow_Packed.png", Art + "Textures/T_Snow_Fresh.png", Art + "Textures/T_Snow_Ice.png", Art + "Textures/T_Snow_ColdRock.png" },
                tint = new[] { Col(1f, 1f, 1.02f), Col(1.02f, 1.01f, 1f), Col(0.95f, 1f, 1.05f), Col(1f, 1.02f, 1.05f) }, primary = 1, secondary = 0, path = 0,
                mid = new[] { "KF/Rock_1_E_Color1", "KF/Rock_1_H_Color1", "KF/Rock_2_C_Color1", "KF/Rock_3_C_Color1", "KX/snowman_A", "KX/snowball_pile", "KK/Containers_Crate_Large", "KK/Fuel_A_Barrels", "KK/Containers_Box_Large_Dirty", "KK/Wood_Log_B", "KD/barrel_large" },
                outer = new[] { "SN/Pine_1", "SN/Pine_2", "SN/Pine_3", "SN/Pine_4", "SN/Pine_5", "SN/Pine_4", "SN/DeadTree_1", "KF/Rock_1_K_Color1", "KF/Rock_2_F_Color1", "KF/Tree_Bare_1_C_Color1", "KF/Tree_Bare_2_B_Color1" },
                landmark = new[] { "KF/Rock_1_M_Color1" }, scatter = new[] { "SN/Pebble_Round_1", "SN/Pebble_Square_3", "KF/Rock_2_B_Color1", "KX/snowball" }, scatterCount = 22,
                midPts = new[] { V(-9, 7), V(-11, -8), V(10, 9) }, outerPts = new[] { V(-17, 15), V(-18, -12), V(16, 16), V(0, -18), V(18, 0) }, landmarkPts = new[] { V(-14, -15) },
                pools = new[] { new Vector3(9f, -8f, 6.5f), new Vector3(-7f, 12f, 3.2f) }, fluid = "ice", bankChannel = 3, basinDepth = 0.6f },

            new Zone { id = "volcano", layerColor = new[] { Col(0.25f, 0.24f, 0.27f), Col(0.36f, 0.27f, 0.27f), Col(0.32f, 0.22f, 0.18f), Col(0.2f, 0.2f, 0.23f) }, name = "Núi lửa", col = 3, row = 0,
                tex = new[] { C + "ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_A_Basecolor.png", C + "ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_C_Basecolor.png",
                              C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_A_Basecolor.png" },
                tint = new[] { Col(0.78f, 0.7f, 0.68f), Col(0.7f, 0.64f, 0.63f), Col(0.55f, 0.42f, 0.36f), Col(0.55f, 0.5f, 0.5f) }, primary = 1, secondary = 0, path = 2,
                mid = new[] { "KF/Rock_1_D_Color1", "KF/Rock_1_G_Color1", "KF/Rock_3_D_Color1", "KF/Rock_3_H_Color1", "KF/Rock_2_D_Color1", "KH/skull", "KH/gravestone", "KK/Fuel_A_Barrel_Dirty", "KD/rubble_half" },
                outer = new[] { "KF/Rock_1_J_Color1", "KF/Rock_1_L_Color1", "KF/Rock_2_E_Color1", "KF/Rock_2_G_Color1", "SN/DeadTree_4", "SN/DeadTree_3", "SN/DeadTree_5", "SN/DeadTree_2", "KF/Tree_Bare_2_B_Color1", "KH/tree_dead_large", "KH/tree_dead_medium" },
                landmark = new[] { "KF/Rock_1_O_Color1", "KF/Rock_1_N_Color1" }, scatter = new[] { "SN/Pebble_Square_1", "SN/Pebble_Square_4", "KH/bone_A", "KF/Rock_2_B_Color1" }, scatterCount = 20,
                midPts = new[] { V(-9, 8), V(9, 8), V(8, -14) }, outerPts = new[] { V(-17, 16), V(15, 17), V(-17, -16), V(18, -2) }, landmarkPts = new[] { V(0, 17) },
                rivers = new[] { new[] { V(-20, -6), V(-4, -2), V(20, -9) } }, riverWidth = 2.4f,
                pools = new[] { new Vector3(-10f, 10f, 4.5f), new Vector3(11f, -15f, 3.5f), new Vector3(12f, 6f, 2.6f) },
                fluid = "lava", bankChannel = 3, basinDepth = 1.2f, crack = 0.85f, crackGlow = 0.45f },

            new Zone { id = "forest", layerColor = new[] { Col(0.36f, 0.26f, 0.18f), Col(0.2f, 0.37f, 0.2f), Col(0.28f, 0.43f, 0.22f), Col(0.34f, 0.38f, 0.33f) }, name = "Rừng sâu", col = 0, row = 1,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_02/Textures/Grass_Dense_Tint_02_Base_Basecolor_A.png",
                              C + "GRASS/GRASS_Flower/GRASS_Flower_Tint_02/Texture/Grass_Flower_Tint_02_Base_Basecolor.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_B_Basecolor_A.png" },
                tint = new[] { Col(0.85f, 0.8f, 0.72f), Col(0.85f, 0.95f, 0.8f), Color.white, Col(0.8f, 0.9f, 0.8f) }, primary = 1, secondary = 2, path = 0, outerCh = 0,
                mid = new[] { "SN/Mushroom_Common", "SN/Mushroom_Laetiporus", "SN/Fern_1", "SN/Plant_1_Big", "SN/Bush_Common_Flowers", "KF/Rock_1_F_Color1", "KF/Rock_3_E_Color1", "SN/Plant_1", "SN/Fern_1", "KK/Wood_Log_A" },
                outer = new[] { "SN/CommonTree_1", "SN/CommonTree_3", "SN/CommonTree_4", "SN/Pine_1", "SN/Pine_3", "SN/Pine_5", "SN/TwistedTree_1", "SN/TwistedTree_3", "SN/Pine_1", "SN/CommonTree_3", "SN/Pine_4", "SN/CommonTree_2", "SN/Pine_2", "SN/Pine_4", "SN/TwistedTree_4", "SN/CommonTree_2", "KF/Rock_1_K_Color1" },
                landmark = new[] { "SN/TwistedTree_4", "KF/Rock_1_P_Color1" }, scatter = new[] { "SN/Grass_Wispy_Short", "SN/Grass_Wispy_Tall", "SN/Clover_2", "SN/Clover_1", "SN/Plant_7", "SN/Plant_7_Big", "SN/Fern_1", "SN/Petal_2", "SN/Petal_3", "SN/Mushroom_Common", "SN/Flower_3_Single", "SN/Plant_7" },
                midPts = new[] { V(-8, 8), V(9, 6), V(-9, -9), V(8, -9) }, outerPts = new[] { V(-17, 15), V(-18, 0), V(-16, -15), V(16, -16), V(18, 1), V(15, 16), V(0, 18), V(0, -18) }, landmarkPts = new Vector2[0],
                paths = new[] { new[] { V(-20, 0), V(20, -2) } }, gridStep = 4f, scatterCount = 210,
                pools = new[] { new Vector3(-10f, 11f, 4f) }, rivers = new[] { new[] { V(-20, -14), V(-6, -9), V(5, -5), V(20, 12) } }, riverWidth = 1.6f,
                fluid = "water", bankChannel = 0, basinDepth = 0.9f },

            new Zone { id = "swamp", layerColor = new[] { Col(0.3f, 0.33f, 0.2f), Col(0.42f, 0.5f, 0.22f), Col(0.4f, 0.45f, 0.32f), Col(0.32f, 0.36f, 0.3f) }, name = "Đầm độc", col = 1, row = 1,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_02/Textures/Grass_Dense_Tint_02_Base_Basecolor_A.png",
                              C + "SAND/SAND_Underwater/Textures/Sand_Underwater_Base_Basecolor.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_B_Basecolor_A.png" },
                tint = new[] { Col(0.62f, 0.72f, 0.45f), Col(0.8f, 0.9f, 0.55f), Col(0.7f, 0.8f, 0.6f), Col(0.6f, 0.7f, 0.6f) }, primary = 0, secondary = 1, path = 2,
                mid = new[] { "SN/Plant_1", "SN/Plant_1_Big", "SN/Clover_2", "SN/Mushroom_Common", "KF/Rock_2_C_Color1", "KH/gravemarker_A", "KK/Fuel_C_Barrel_Dirty", "KD/barrel_small" },
                outer = new[] { "SN/DeadTree_1", "SN/DeadTree_3", "SN/DeadTree_4", "SN/TwistedTree_5", "SN/DeadTree_2", "SN/DeadTree_5", "KF/Rock_2_H_Color1", "KF/Tree_Bare_1_B_Color1", "KF/Tree_Bare_2_C_Color1", "KH/tree_dead_large" },
                landmark = new[] { "KK/Fuel_C_Barrels" }, scatter = new[] { "SN/Grass_Wispy_Tall", "SN/Grass_Wispy_Short", "SN/Clover_1", "SN/Plant_7_Big", "SN/Plant_7", "SN/Grass_Wispy_Tall" },
                midPts = new[] { V(8, 9), V(-10, -9) }, outerPts = new[] { V(-17, 15), V(16, 16), V(-17, -15), V(17, -3) }, landmarkPts = new[] { V(15, -15) },
                pools = new[] { new Vector3(-9f, 6f, 5.5f), new Vector3(10f, -8f, 6f), new Vector3(3f, 14f, 3.5f) },
                rivers = new[] { new[] { V(-9, 6), V(0, -1), V(10, -8) } }, riverWidth = 1.5f,
                fluid = "toxic", bankChannel = 2, basinDepth = 0.8f },

            new Zone { id = "city", layerColor = new[] { Col(0.42f, 0.4f, 0.38f), Col(0.4f, 0.48f, 0.3f), Col(0.62f, 0.63f, 0.66f), Col(0.3f, 0.31f, 0.34f) }, name = "Phố đổ nát", col = 2, row = 1,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_01/Textures/Grass_Dense_Tint_01_Base_Basecolor_A.png",
                              C + "WALLS/WALL_Stone/Textures/Wall_Stone_A_Basecolor.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_B_Basecolor_A.png" },
                tint = new[] { Col(0.72f, 0.72f, 0.74f), Col(0.7f, 0.78f, 0.6f), Col(0.72f, 0.75f, 0.82f), Col(0.62f, 0.64f, 0.68f) }, primary = 2, secondary = 0, path = 3,
                mid = new[] { "MC/SM_FloorProps_01", "MC/SM_FloorProps_02", "MC/SM_FloorProps_03", "MC/SM_FloorProps_04", "MC/SM_FloorProps_05", "KD/rubble_half", "KD/barrier", "KK/Containers_Box_Large_Dirty" },
                outer = new[] { "MC/SM_FloorProps_BusStation_01", "MC/SM_FloorProps_Light_01", "KD/rubble_large", "KD/barrier_corner", "KD/pillar", "KK/Pallet_Wood_Covered_B", "KK/Fuel_B_Barrels" },
                landmark = new[] { "KD/pillar_decorated" }, scatter = new[] { "KK/Textiles_A", "MC/SM_FloorProps_06" }, scatterCount = 16,
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
                log.Append(BuildMaps(root.transform));

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
            for (float gx = -17f; gx <= 17.1f; gx += z.gridStep)
                for (float gz = -17f; gz <= 17.1f; gz += z.gridStep)
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
                if (p.magnitude < 5f || BasinAt(z, p) > 0f) continue;
                if (Place(z.scatter[rng.Next(z.scatter.Length)], zr, p, (float)rng.NextDouble() * 360f, 1f)) placed++;
            }

            if (!string.IsNullOrEmpty(z.fluid)) Fluid(z, zr, origin);

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
            foreach (var c in new[] { cam, heroCam })
                c.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().requiresDepthOption =
                    UnityEngine.Rendering.Universal.CameraOverrideOption.On;   // water, lava and toxic read scene depth
            return z.id + " " + placed;
        }

        static bool OnLine(Vector2 p, Zone z)
        {
            foreach (var l in z.paths) if (DistToPolyline(p, l) < 2.5f) return true;
            // Keep a margin around basins: a prop on the bank would hang over the fluid.
            foreach (var o in new[] { Vector2.zero, new Vector2(1.5f, 0f), new Vector2(-1.5f, 0f), new Vector2(0f, 1.5f), new Vector2(0f, -1.5f) })
                if (BasinAt(z, p + o) > 0f) return true;
            return false;
        }

        // ── basins: a black-and-white mask (white = deep) from pools and rivers, warped by noise

        static float BasinAt(Zone z, Vector2 p)
        {
            float v = 0f;
            float warp = Mathf.PerlinNoise(p.x * 0.17f + 11.3f, p.y * 0.17f + 5.1f) - 0.5f;
            foreach (var c in z.pools)
            {
                float d = Vector2.Distance(p, new Vector2(c.x, c.y)) + warp * c.z * 0.7f;
                v = Mathf.Max(v, 1f - d / c.z);
            }
            foreach (var r in z.rivers)
            {
                float d = DistToPolyline(p, r) + warp * z.riverWidth * 0.8f;
                v = Mathf.Max(v, (1f - d / z.riverWidth) * 0.85f);
            }
            return Mathf.Clamp01(v);
        }

        /// The ground's height from the mask: flat at 0, sinking to -basinDepth where the mask is white.
        static float Sink(Zone z, float basin) => -z.basinDepth * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.6f, basin));

        static Texture2D BasinMask(Zone z, Vector3 origin)
        {
            const int R = 256;
            string path = Art + "Textures/T_Basin_" + z.id + ".png";
            var tex = new Texture2D(R, R, TextureFormat.RGBA32, false, true);
            var px = new Color[R * R];
            for (int j = 0; j < R; j++)
                for (int i = 0; i < R; i++)
                {
                    float v = BasinAt(z, new Vector2(-Half + (i + 0.5f) * 2f * Half / R, -Half + (j + 0.5f) * 2f * Half / R));
                    px[j * R + i] = new Color(v, v, v, 1f);
                }
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = false; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// A 512² tiling crack mask: the edges of a Worley (cellular) noise, white = crack.
        static Texture2D CrackTexture()
        {
            string path = Art + "Textures/T_Cracks.png";
            if (!File.Exists(path))
            {
                const int R = 512, Cells = 7;
                var rng = new System.Random(7);
                var pts = new Vector2[Cells * Cells];
                for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2((i % Cells + (float)rng.NextDouble()) / Cells, (i / Cells + (float)rng.NextDouble()) / Cells);
                var tex = new Texture2D(R, R, TextureFormat.RGBA32, false, true);
                var px = new Color[R * R];
                for (int y = 0; y < R; y++)
                    for (int x = 0; x < R; x++)
                    {
                        var uv = new Vector2((x + 0.5f) / R, (y + 0.5f) / R);
                        float f1 = 9f, f2 = 9f;
                        foreach (var c in pts)
                            for (int oy = -1; oy <= 1; oy++)
                                for (int ox = -1; ox <= 1; ox++)
                                {
                                    float d = Vector2.Distance(uv, c + new Vector2(ox, oy));
                                    if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                                }
                        float wob = Mathf.PerlinNoise(uv.x * 23f, uv.y * 23f) * 0.012f;
                        float e = Mathf.InverseLerp(0.004f, 0.016f, f2 - f1 - wob);
                        float line = 1f - e * e * (3f - 2f * e);   // shader-style smoothstep
                        px[y * R + x] = new Color(line, line, line, 1f);
                    }
                tex.SetPixels(px); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.sRGBTexture = false; imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// The fluid surface: a plane below ground level that only shows where the ground sank.
        static void Fluid(Zone z, Transform zr, Vector3 origin)
        {
            const int N = 40;
            float level = z.basinDepth * -0.35f;
            var verts = new Vector3[(N + 1) * (N + 1)];
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                    // Inset 1 m: a plane as wide as the ground peeks out under the zone's near edge.
                    verts[j * (N + 1) + i] = new Vector3(-Half + 1f + i * 2f * (Half - 1f) / N, level, -Half + 1f + j * 2f * (Half - 1f) / N);
            var tris = new int[N * N * 6];
            int t = 0;
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int a = j * (N + 1) + i, b = a + N + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            var m = new Mesh { name = "EnvFluid_" + z.id, vertices = verts, triangles = tris };
            m.RecalculateNormals(); m.RecalculateBounds();
            string mp = Art + "Materials/EnvFluid_" + z.id + ".asset";
            AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(m, mp);

            var go = new GameObject("Fluid_" + z.fluid);
            go.transform.SetParent(zr, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = FluidMaterial(z, origin);
        }

        static Material FluidMaterial(Zone z, Vector3 origin)
        {
            bool lava = z.fluid == "lava";
            var shader = Shader.Find(lava ? "HordeCall/EnvSandbox/Stylized Lava" : "HordeCall/EnvSandbox/Stylized Water");
            string path = Art + "Materials/M_Fluid_" + z.id + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetTexture("_BasinMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/T_Basin_" + z.id + ".png"));
            m.SetVector("_ZoneRect", new Vector4(origin.x, origin.z, 2f * Half, 0f));
            m.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Noise/Noise_Perlin_01.png"));
            // Depth comes from the camera depth texture, in metres along the view ray: the game camera
            // looks down at an angle, so a basin reads deeper than it sinks.
            if (lava)
            {
                m.SetFloat("_EdgeWidth", 0.38f);
            }
            else
            {
                m.SetFloat("_FoamWidth", z.fluid == "ice" ? 0.15f : 0.2f);
                m.SetFloat("_DepthRange", z.basinDepth * 1.35f);
                switch (z.fluid)
                {
                    case "toxic":
                        m.SetColor("_ShallowColor", new Color(0.55f, 0.85f, 0.2f)); m.SetColor("_DeepColor", new Color(0.18f, 0.42f, 0.1f));
                        m.SetColor("_FoamColor", new Color(0.78f, 0.95f, 0.45f)); m.SetColor("_StreakColor", new Color(0.7f, 0.95f, 0.3f));
                        m.SetColor("_Emission", new Color(0.12f, 0.3f, 0.02f)); m.SetFloat("_BubbleAmount", 1f);
                        m.SetVector("_Flow", new Vector4(0.2f, 0.1f, 0.02f, 0f)); m.SetFloat("_WaveHeight", 0.02f);
                        break;
                    case "ice":
                        m.SetColor("_ShallowColor", new Color(0.8f, 0.9f, 0.97f)); m.SetColor("_DeepColor", new Color(0.45f, 0.65f, 0.85f));
                        m.SetColor("_FoamColor", new Color(0.97f, 0.98f, 1f)); m.SetColor("_StreakColor", new Color(0.92f, 0.97f, 1f));
                        m.SetVector("_Flow", Vector4.zero); m.SetFloat("_WaveHeight", 0f); m.SetFloat("_StreakCut", 0.66f);
                        m.SetTexture("_CrackTex", CrackTexture()); m.SetFloat("_CrackAmount", 0.7f); m.SetFloat("_CrackTiling", 5f);
                        break;
                    default:
                        m.SetColor("_ShallowColor", new Color(0.38f, 0.82f, 0.88f)); m.SetColor("_DeepColor", new Color(0.12f, 0.42f, 0.66f));
                        m.SetColor("_FoamColor", new Color(0.95f, 0.98f, 1f)); m.SetColor("_StreakColor", new Color(0.72f, 0.94f, 1f));
                        m.SetVector("_Flow", new Vector4(0.6f, 0.25f, 0.06f, 0f));
                        break;
                }
            }
            EditorUtility.SetDirty(m);
            return m;
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

        static bool Place(string key, Transform parent, Vector2 at, float yaw, float scale) => PlaceObject(key, parent, at, yaw, scale) != null;

        static GameObject PlaceObject(string key, Transform parent, Vector2 at, float yaw, float scale)
        {
            var pack = key.Substring(0, 2);
            var name = key.Substring(3);
            if (!PrefabCache.TryGetValue(key, out var prefab))
            {
                prefab = FindVendorPrefab(key);
                PrefabCache[key] = prefab;
                if (prefab == null) Debug.LogWarning("[EnvSandbox] missing prefab " + key);
            }
            if (prefab == null) return null;
            // The palette kit (one gradient texture for every solid piece) replaces the vendor prefab
            // once it has been built.
            var converted = ConvertedPrefab(key);
            if (converted != null) prefab = converted;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = new Vector3(at.x, 0f, at.y);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            go.transform.localScale *= scale * PackScale(pack) * PieceScale(key);
            return go;
        }

        // ── ground

        static Mesh GroundMesh(Zone z, Vector3 origin)
        {
            const int N = 100;
            var verts = new Vector3[(N + 1) * (N + 1)];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                {
                    int k = j * (N + 1) + i;
                    var p = new Vector2(-Half + i * (2 * Half / N), -Half + j * (2 * Half / N));
                    verts[k] = new Vector3(p.x, Sink(z, BasinAt(z, p)), p.y);
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
            w[z.bankChannel] += Mathf.InverseLerp(0.0f, 0.3f, BasinAt(z, p)) * 3f;
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
            m.shader = Shader.Find("HordeCall/EnvSandbox/Ground");
            m.CopyPropertiesFromMaterial(src);
            var origin = new Vector3(z.col * Spacing, 0f, z.row * Spacing);
            m.SetTexture("_BasinMask", BasinMask(z, origin));
            m.SetVector("_ZoneRect", new Vector4(origin.x, origin.z, 2f * Half, 0f));
            m.SetTexture("_CrackTex", CrackTexture());
            m.SetFloat("_CrackStrength", z.crack);
            m.SetFloat("_CrackGlowReach", z.crackGlow);
            m.SetFloat("_BankDarken", z.fluid == "lava" ? 0.55f : 0.3f);
            // Broad light and dark patches so open ground does not read as one flat colour; snow needs
            // more of them, plus cold blue ice fissures, or it washes out to white under the toon light.
            m.SetFloat("_MacroScale", 45f);
            m.SetFloat("_MacroStrength", z.snow ? 0.3f : 0.16f);
            if (z.snow)
            {
                m.SetFloat("_CrackStrength", 0.15f);
                m.SetFloat("_CrackTiling", 24f);
                m.SetColor("_CrackColor", new Color(0.36f, 0.5f, 0.66f));
            }
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
                if (r.name == "Ground" || r.name.StartsWith("Fluid") || r.name == "Hero_1.8m" || r.name == "Block") continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null || src.shader.name.Contains("Foliage")) continue;   // alpha-cut leaves keep their shape
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
