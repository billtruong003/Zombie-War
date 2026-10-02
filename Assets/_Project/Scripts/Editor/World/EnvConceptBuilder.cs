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
    /// Concept zones for the two new maps (owner feedback 02/10), in their own scene so the main env
    /// sandbox and its baked maps are not rebuilt: three desert grounds (plain sand, dunes, sand with
    /// clay paths, each with an oasis) and three street layouts (crossroads, avenue with a plaza,
    /// overgrown street) built from MegaCity road tiles, buildings and cars.
    /// Scene: Scenes/Dev/EnvConcepts.unity. Shots: Review/M8/concepts/.
    /// </summary>
    public static partial class EnvSandboxBuilder
    {
        const string ConceptScenePath = "Assets/_Project/Scenes/Dev/EnvConcepts.unity";
        const string ConceptArt = "Assets/_Project/Art/EnvSandbox/Concepts/";

        static readonly string[] DesertMid = { "TT/Cactus_01", "TT/Cactus_02", "TT/Rock_01", "TT/Rock_02", "KH/skull", "KK/Containers_Crate_Large", "KK/Fuel_A_Barrel_Dirty", "KK/Fuel_B_Jerrycan", "KD/rubble_half", "TT/Cactus_01" };
        static readonly string[] DesertOuter = { "TT/Cactus_03", "TT/Cactus_02", "TT/CliffCorner_01", "TT/CliffCorner_02", "TT/Cliff_01", "KD/pillar", "KD/rubble_large", "SN/DeadTree_2", "SN/DeadTree_4", "TT/Rock_04" };
        static readonly string[] DesertScatter = { "SN/Grass_Wispy_Short", "SN/Pebble_Round_1", "SN/Pebble_Square_3", "KH/bone_A", "SN/Grass_Wispy_Short" };

        static Zone DesertConcept(string id, string name, int col, Color[] layers, Vector2[][] paths)
        {
            string sand = C + "SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png";
            return new Zone
            {
                id = id, name = name, col = col, row = 0,
                tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", sand, sand, sand },
                tint = new[] { Col(1f, 0.92f, 0.82f), Color.white, Col(1.04f, 1f, 0.92f), Col(0.96f, 0.9f, 0.82f) },
                layerColor = layers, primary = 2, secondary = 1, path = 0, outerCh = 3,
                mid = DesertMid, outer = DesertOuter, landmark = new[] { "TT/Tower1" }, scatter = DesertScatter, scatterCount = 40,
                midPts = new[] { V(-8, -8), V(9, 9), V(10, -9) }, outerPts = new[] { V(-17, -15), V(16, -16), V(17, 2), V(15, 16) },
                landmarkPts = new[] { V(14, 15) }, paths = paths, gridStep = 6.5f,
                pools = new[] { new Vector3(-9f, 8f, 5f) }, fluid = "water", bankChannel = 2, basinDepth = 0.8f,
            };
        }

        static List<Zone> DesertConcepts()
        {
            var sand = new[] { Col(0.86f, 0.62f, 0.42f), Col(0.95f, 0.82f, 0.57f), Col(0.97f, 0.86f, 0.62f), Col(0.9f, 0.75f, 0.52f) };
            return new List<Zone>
            {
                DesertConcept("desert_a", "Sa mạc A · cát trơn", 0, sand, new Vector2[0][]),
                DesertConcept("desert_b", "Sa mạc B · đụn cát", 1, sand, new Vector2[0][]),
                DesertConcept("desert_c", "Sa mạc C · cát + lối đất sét", 2, new[] { Col(0.76f, 0.5f, 0.34f), sand[1], sand[2], sand[3] },
                              new[] { new[] { V(-20, -4), V(-2, -1), V(20, 5) }, new[] { V(-1, -20), V(-2, -1) } }),
            };
        }

        static Zone CityGround(string id, string name, int col, bool overgrown) => new Zone
        {
            id = id, name = name, col = col, row = 1,
            tex = new[] { C + "DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png", C + "GRASS/GRASS_Dense/GRASS_Dense_Tint_01/Textures/Grass_Dense_Tint_01_Base_Basecolor_A.png",
                          C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_B_Basecolor_A.png", C + "ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_B_Basecolor_A.png" },
            tint = new[] { Col(0.8f, 0.76f, 0.7f), Col(0.85f, 0.95f, 0.75f), Col(0.8f, 0.8f, 0.82f), Col(0.7f, 0.7f, 0.74f) },
            layerColor = new[] { Col(0.42f, 0.38f, 0.34f), Col(0.36f, 0.5f, 0.26f), Col(0.6f, 0.6f, 0.62f), Col(0.5f, 0.5f, 0.53f) },
            primary = overgrown ? 1 : 2, secondary = overgrown ? 2 : 3, path = 0,
            mid = new string[0], outer = new string[0], landmark = new string[0], scatter = new[] { "SN/Grass_Wispy_Short" }, scatterCount = 0,
            midPts = new Vector2[0], outerPts = new Vector2[0], landmarkPts = new Vector2[0],
        };

        [MenuItem("HordeCall/World/Build Env Concepts (desert, city)")]
        public static string BuildConcepts()
        {
            Directory.CreateDirectory(ConceptArt + "Materials");
            var prevActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var log = new System.Text.StringBuilder();
            try
            {
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.262f, 0.413f, 0.631f);
                RenderSettings.ambientEquatorColor = new Color(0.529f, 0.592f, 0.665f);
                RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.2f);
                RenderSettings.fog = false;
                var root = new GameObject("EnvConcepts");
                var rig = new GameObject("ToonLightRig");
                rig.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                rig.AddComponent<ToonLightRig>();

                foreach (var z in DesertConcepts())
                {
                    log.Append(BuildZone(z, root.transform)).Append("; ");
                    var zr = root.transform.Find("Zone_" + z.id);
                    var g = zr.Find("Ground").GetComponent<MeshRenderer>().sharedMaterial;
                    g.SetFloat("_CrackStrength", 0f); g.SetFloat("_CrackGlowReach", 0f);
                    g.SetFloat("_TextureStrength", 0.3f);
                    if (z.id == "desert_b") { g.SetFloat("_MacroScale", 16f); g.SetFloat("_MacroStrength", 0.34f); }
                    else { g.SetFloat("_MacroScale", 40f); g.SetFloat("_MacroStrength", 0.12f); }
                    Oasis(zr, new Vector2(-9f, 8f), 5f);
                    ApplyConceptLook(zr, "desert");
                }

                string[] cityNames = { "Phố A · ngã tư", "Phố B · đại lộ + quảng trường", "Phố C · phố hoang cỏ mọc" };
                for (int i = 0; i < 3; i++)
                {
                    var z = CityGround("city_" + (char)('a' + i), cityNames[i], i, overgrown: i == 2);
                    log.Append(BuildZone(z, root.transform)).Append("; ");
                    var zr = root.transform.Find("Zone_" + z.id);
                    var g = zr.Find("Ground").GetComponent<MeshRenderer>().sharedMaterial;
                    g.SetFloat("_CrackStrength", i == 2 ? 0.25f : 0.12f); g.SetFloat("_CrackGlowReach", 0f);
                    g.SetFloat("_TextureStrength", 0.2f); g.SetFloat("_MacroStrength", 0.1f);
                    if (i == 1) CityAvenue(zr); else CityCrossroads(zr, overgrown: i == 2);
                    ToonifyMegaCity(zr);
                    ApplyConceptLook(zr, "city");
                }

                EditorSceneManager.SaveScene(scene, ConceptScenePath);
            }
            finally
            {
                if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            return log.ToString();
        }

        // ── desert ─────────────────────────────────────────────────────────────────────

        static void Oasis(Transform zr, Vector2 c, float r)
        {
            var rng = new System.Random(17);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 0.8f + (float)rng.NextDouble());
                Place(i % 3 == 0 ? "SN/Bush_Common_Flowers" : "SN/Grass_Common_Tall", zr, p, (float)rng.NextDouble() * 360f, 1f);
            }
            Place("SN/CommonTree_2", zr, c + new Vector2(r + 2f, 1.5f), 40f, 0.8f);
        }

        // ── city ───────────────────────────────────────────────────────────────────────

        static GameObject Put(string key, Transform zr, float x, float z, float yaw, float scale = 1f, float lift = 0f)
        {
            var go = PlaceObject(key, zr, new Vector2(x, z), yaw, scale);
            if (go != null && lift != 0f) go.transform.localPosition += Vector3.up * lift;
            return go;
        }

        static void Road(Transform zr, string tile, float x, float z, float yaw) => Put("MC/" + tile, zr, x, z, yaw, 1f, 0.03f);

        static void CityCrossroads(Transform zr, bool overgrown)
        {
            var rng = new System.Random(overgrown ? 31 : 13);
            // Roads: a four-way crossing with straight tiles out to the zone edge (some lost to the
            // grass on the overgrown street).
            Road(zr, "SM_Floor_Road_03", 0, 0, 0);
            foreach (var d in new[] { 10f, 20f, -10f, -20f })
            {
                if (!(overgrown && d == -20f)) Road(zr, "SM_Floor_Road_01_M", 0, d, 0);
                if (!(overgrown && d == 20f)) Road(zr, "SM_Floor_Road_01_M", d, 0, 90);
            }
            // Corner blocks: shops facing the crossing, set back to the zone edge.
            string[] shops = { "SM_Buildings_Commercial_02", "SM_Buildings_Commercial_07", "SM_Buildings_Commercial_11", "SM_Buildings_Commercial_14" };
            float[][] corners = { new[] { 14f, 14f, 225f }, new[] { -14f, 14f, 135f }, new[] { -14f, -14f, 45f }, new[] { 14f, -14f, 315f } };
            for (int i = 0; i < 4; i++) Put("MC/" + shops[i], zr, corners[i][0], corners[i][1], corners[i][2]);
            // Street furniture along the kerbs.
            foreach (var (x, z) in new[] { (6.4f, 9f), (-6.4f, -9f), (9f, -6.4f), (-9f, 6.4f), (6.4f, -16f), (-16f, 6.4f) })
                Put("MC/SM_FloorProps_Light_01", zr, x, z, 0f);
            foreach (var (x, z, y) in new[] { (5.8f, 5.8f, 225f), (-5.8f, -5.8f, 45f) })
                Put("MC/SM_FloorProps_TrafficLight_01", zr, x, z, y);
            Put("MC/SM_FloorProps_BusStation_01", zr, 7f, -12.5f, 90f);
            Put("MC/SM_Props_Bench_01", zr, -7.2f, 12.5f, 90f);
            // Abandoned cars, one crashed into the crossing.
            Put("MC/SM_Vehicles_03_V1", zr, 2.4f, 9.5f, 8f);
            Put("MC/SM_Vehicles_Taxi_01", zr, -12f, -2.3f, 96f);
            Put("MC/SM_Vehicles_Bus_V1", zr, -2.4f, -14f, 192f);
            Put("MC/SM_Vehicles_Police_02", zr, 12.5f, 2.4f, 74f);
            Put("MC/SM_Vehicles_07_V1", zr, 1.5f, -2.5f, 38f);
            // Barricades, cones, rubble and trash.
            foreach (var (k, x, z, y) in new[] { ("MC/SM_FloorProps_03", -3f, 6.5f, 0f), ("MC/SM_FloorProps_04", 3.5f, -7f, 20f), ("KD/barrier", -7f, -3f, 90f),
                                                  ("MC/SM_FloorProps_01", 4.2f, 5f, 0f), ("MC/SM_FloorProps_01", -4f, -5.5f, 0f), ("MC/SM_FloorProps_02", 5f, 4f, 0f),
                                                  ("KD/rubble_half", 10.5f, 9f, 30f), ("KD/rubble_large", -11f, -10f, 200f), ("KK/Containers_Box_Large_Dirty", -9f, 10f, 15f),
                                                  ("MC/SM_IndustrialProps_Barrel_01", 9.5f, -9.5f, 0f), ("KK/Pallet_Wood_Covered_B", 11f, -8f, 40f) })
                Put(k, zr, x, z, y);
            if (overgrown)
            {
                // Nature takes the street back: trees in the blocks, grass and bushes through the cracks.
                foreach (var (k, x, z) in new[] { ("SN/CommonTree_1", 9f, 15f), ("SN/CommonTree_3", -15f, 9f), ("SN/TwistedTree_1", -9f, -16f), ("SN/CommonTree_4", 16f, -10f), ("SN/Bush_Common_Flowers", -8f, 8f), ("SN/Bush_Common_Flowers", 8f, -8f) })
                    Put(k, zr, x, z, (float)rng.NextDouble() * 360f);
                for (int i = 0; i < 160; i++)
                {
                    var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 19f, (float)(rng.NextDouble() * 2 - 1) * 19f);
                    if (p.magnitude < 3f) continue;
                    string[] g = { "SN/Grass_Wispy_Short", "SN/Grass_Common_Short", "SN/Grass_Wispy_Tall", "SN/Clover_1", "SN/Plant_7" };
                    Put(g[rng.Next(g.Length)], zr, p.x, p.y, (float)rng.NextDouble() * 360f, 1f, 0.05f);
                }
            }
            else
            {
                // A few weeds in the cracks.
                for (int i = 0; i < 30; i++)
                {
                    var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 19f, (float)(rng.NextDouble() * 2 - 1) * 19f);
                    Put("SN/Grass_Wispy_Short", zr, p.x, p.y, (float)rng.NextDouble() * 360f, 0.8f, 0.05f);
                }
            }
        }

        static void CityAvenue(Transform zr)
        {
            var rng = new System.Random(23);
            // A wide avenue across the zone, shops on its north side, a plaza with a fountain south of it.
            foreach (float x in new[] { -22.5f, -7.5f, 7.5f, 22.5f }) Road(zr, "SM_Floor_Road_14_L", x, 7f, 90);
            string[] shops = { "SM_Buildings_Commercial_05", "SM_Buildings_Commercial_12", "SM_Buildings_Commercial_15", "SM_Buildings_Commercial_19" };
            for (int i = 0; i < 4; i++) Put("MC/" + shops[i], zr, -15f + i * 10f, 20f, 180f);
            Put("MC/SM_Props_Fountain_01", zr, 0f, -9f, 0f);
            foreach (var (x, z, y) in new[] { (-4.5f, -9f, 90f), (4.5f, -9f, 270f), (0f, -13.5f, 0f), (0f, -4.5f, 180f) }) Put("MC/SM_Props_Bench_01", zr, x, z, y);
            foreach (var (x, z) in new[] { (-9f, -5f), (9f, -5f), (-9f, -14f), (9f, -14f) }) { Put("SN/CommonTree_2", zr, x, z, (float)rng.NextDouble() * 360f, 0.75f); }
            foreach (var x in new[] { -14f, -2f, 10f }) Put("MC/SM_FloorProps_Light_01", zr, x, -0.8f, 0f);
            foreach (var x in new[] { -8f, 4f, 16f }) Put("MC/SM_FloorProps_Light_01", zr, x, 14.8f, 180f);
            Put("MC/SM_Floor_Garden_06", zr, -13f, -15f, 0f, 1f, 0.03f);
            Put("MC/SM_Floor_Garden_06", zr, 13f, -15f, 0f, 1f, 0.03f);
            Put("MC/SM_Vehicles_05_V1", zr, -12f, 4.5f, 90f);
            Put("MC/SM_Vehicles_Truck_02_V1", zr, 6f, 9.5f, 262f);
            Put("MC/SM_Vehicles_Ambulance", zr, 15f, 4f, 115f);
            Put("MC/SM_Vehicles_10_V1", zr, -2f, 6f, 80f);
            Put("MC/SM_FloorProps_BusStation_01", zr, -5f, 15.2f, 180f);
            foreach (var (k, x, z, y) in new[] { ("MC/SM_FloorProps_03", -17f, 2f, 0f), ("KD/barrier", 12f, 1f, 0f), ("MC/SM_FloorProps_01", 2f, 1.5f, 0f),
                                                  ("KD/rubble_half", -16f, -8f, 30f), ("KK/Containers_Box_Large_Dirty", 15f, -3f, 15f), ("MC/SM_IndustrialProps_Barrel_02", 16.5f, -2f, 0f) })
                Put(k, zr, x, z, y);
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 19f, (float)(rng.NextDouble() * 2 - 1) * 19f);
                Put("SN/Grass_Wispy_Short", zr, p.x, p.y, (float)rng.NextDouble() * 360f, 0.8f, 0.05f);
            }
        }

        // ── looks ──────────────────────────────────────────────────────────────────────

        static readonly Dictionary<Material, Material> ToonCache = new();

        /// MegaCity ships Lit materials over one colour atlas; in the game's toon light they read flat
        /// and dark. For the concepts each gets a toon copy (the decoration shader) of the same atlas.
        static void ToonifyMegaCity(Transform zr)
        {
            var shader = Shader.Find("HordeCall/Env/Solid");
            foreach (var r in zr.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !AssetDatabase.GetAssetPath(m).Contains("JC_LP_MegaCity")) continue;
                    if (!ToonCache.TryGetValue(m, out var toon))
                    {
                        string path = ConceptArt + "Materials/" + m.name + "_Toon.mat";
                        toon = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (toon == null) { toon = new Material(shader); AssetDatabase.CreateAsset(toon, path); }
                        toon.shader = shader;
                        var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                        if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                        toon.SetTexture("_BaseMap", tex);
                        var col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                        toon.SetColor("_BaseColor", col);
                        EditorUtility.SetDirty(toon);
                        ToonCache[m] = toon;
                    }
                    mats[i] = toon; changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// The theme's restyled palette and foliage materials on every decoration piece of a zone.
        static void ApplyConceptLook(Transform zr, string id)
        {
            var (solid, foliage) = ThemeMaterials(id);
            if (solid == null) return;
            var baseSolid = AssetDatabase.LoadAssetAtPath<Material>(Pal + "M_EnvSolid.mat");
            var baseFoliage = AssetDatabase.LoadAssetAtPath<Material>(Pal + "M_EnvFoliage.mat");
            foreach (var r in zr.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == baseSolid) { mats[i] = solid; changed = true; }
                    else if (mats[i] == baseFoliage) { mats[i] = foliage; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // ── shots ──────────────────────────────────────────────────────────────────────

        /// Renders every concept zone: a wide view and the game camera's view of its middle.
        public static string RenderConcepts()
        {
            var scene = EditorSceneManager.OpenScene(ConceptScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory("Review/M8/concepts");
            var go = new GameObject("TmpShotCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.68f, 0.82f);
            cam.farClipPlane = 400f;
            var rt = new RenderTexture(1080, 1920, 24);
            var rtWide = new RenderTexture(1600, 1200, 24);
            int n = 0;
            foreach (Transform zr in GameObject.Find("EnvConcepts").transform)
            {
                string id = zr.name.Replace("Zone_", "");
                var c = zr.position;
                Shoot(cam, rtWide, c + new Vector3(0f, 38f, -30f), c, 40f, $"Review/M8/concepts/{id}_wide.png");
                Shoot(cam, rt, c + new Vector3(0f, 12f, -8f), c + new Vector3(0f, 0f, 0f), 60f, $"Review/M8/concepts/{id}_game.png");
                n++;
            }
            Object.DestroyImmediate(go); rt.Release(); rtWide.Release();
            return "rendered " + n;
        }

        static void Shoot(Camera cam, RenderTexture rt, Vector3 pos, Vector3 look, float fov, string path)
        {
            cam.targetTexture = rt; cam.fieldOfView = fov;
            cam.transform.position = pos; cam.transform.LookAt(look);
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
