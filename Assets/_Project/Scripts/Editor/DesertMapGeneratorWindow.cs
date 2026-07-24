using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Authoring window for desert arenas: pick the size, densities and seed, generate, and read back
    /// what was produced (including whether any spawn point got walled off).
    ///
    /// Replaces the previous single menu command, which had every value hardcoded and gave no way to
    /// iterate on a layout without editing the script.
    ///
    /// Boundary geometry is placed from MEASURED prefab bounds, not assumed pivots:
    ///   Cliff_01        x -2.50..2.50, z -2.79..0.37   (5 m wide, body along -Z, 0.37 lip along +Z)
    ///   CliffCorner_01  yaw 0   -> x -5.00..2.80, z -2.80..5.00   (mass toward -X,+Z)
    ///                   yaw 90  -> x -2.80..5.00, z -2.80..5.00   (mass toward +X,+Z)
    ///                   yaw 180 -> x -2.80..5.00, z -5.00..2.80   (mass toward +X,-Z)
    ///                   yaw 270 -> x -5.00..2.80, z -5.00..2.80   (mass toward -X,-Z)
    /// A corner must push its mass OUTWARD, which is what fixes the rotation being 90 degrees off.
    ///
    /// Menu: Tools/ZombieWar/Desert Map Generator.
    /// </summary>
    public class DesertMapGeneratorWindow : EditorWindow
    {
        const string Pack = "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/";
        const string PropDir = "Assets/_Project/Prefabs/Props/";
        const string SourceScene = "Assets/_Project/Scenes/Map_Level1.unity";

        // Measured constants - see the class comment.
        const float TileSize = 5f;
        const float WallBodyDepth = 2.79f;  // Cliff_01: how far the rock body extends outward of its pivot
        const float CornerLip = 2.80f;      // CliffCorner_01: extent opposite the mass direction

        [Header("Output")]
        [SerializeField] string outScene = "Assets/_Project/Scenes/Map_GenTest.unity";

        [SerializeField] float arenaSize = 50f;
        [SerializeField] int seed = 12345;

        [SerializeField] float playerClearRadius = 8f;
        [SerializeField] float spawnClearRadius = 4f;
        [SerializeField] int spawnPointCount = 12;

        [SerializeField] int scatterAttempts = 900;
        [SerializeField] float scatterDensity = 1f;

        [SerializeField] int crateCount = 18;
        [SerializeField] int barrelClusters = 5;

        [SerializeField] bool markStatic = true;
        [SerializeField] bool disableShadows = true;

        string _lastReport = "";
        Vector2 _scroll;

        [MenuItem("Tools/ZombieWar/Desert Map Generator")]
        public static void Open() => GetWindow<DesertMapGeneratorWindow>("Desert Map");

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            outScene = EditorGUILayout.TextField("Scene path", outScene);
            EditorGUILayout.HelpBox($"Cloned from {System.IO.Path.GetFileName(SourceScene)} so the HUD, " +
                                    "camera, spawner and RunSystems wiring comes with it.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Arena", EditorStyles.boldLabel);
            // Snapped to tile multiples: at any other size the wall run cannot end exactly where a
            // corner arm begins, and the rounding leaves either a gap or a wall piece jammed into
            // the corner - the "one extra chunk" artefact.
            arenaSize = Mathf.Round(EditorGUILayout.Slider("Size (m)", arenaSize, 25f, 100f) / TileSize) * TileSize;
            seed = EditorGUILayout.IntField("Seed", seed);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Random seed")) seed = Random.Range(1, 999999);
                EditorGUILayout.LabelField($"{Mathf.CeilToInt(arenaSize / TileSize)} x " +
                                           $"{Mathf.CeilToInt(arenaSize / TileSize)} tiles", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clearance", EditorStyles.boldLabel);
            playerClearRadius = EditorGUILayout.Slider("Player clear (m)", playerClearRadius, 2f, 20f);
            spawnClearRadius = EditorGUILayout.Slider("Spawn clear (m)", spawnClearRadius, 1f, 12f);
            spawnPointCount = EditorGUILayout.IntSlider("Spawn points", spawnPointCount, 4, 24);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scatter", EditorStyles.boldLabel);
            scatterDensity = EditorGUILayout.Slider("Rock/cactus density", scatterDensity, 0f, 3f);
            scatterAttempts = EditorGUILayout.IntSlider("Placement attempts", scatterAttempts, 100, 4000);
            crateCount = EditorGUILayout.IntSlider("Loot crates", crateCount, 0, 60);
            barrelClusters = EditorGUILayout.IntSlider("Barrel clusters", barrelClusters, 0, 20);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Batching", EditorStyles.boldLabel);
            markStatic = EditorGUILayout.Toggle("Mark Batching Static", markStatic);
            disableShadows = EditorGUILayout.Toggle("Disable shadow casting", disableShadows);
            EditorGUILayout.HelpBox(
                "These renderers use no MaterialPropertyBlock, so the SRP Batcher claims them and GPU " +
                "instancing never runs. Static batching is what actually reduces the draw call COUNT; " +
                "disabling shadow casting removes their second pass.", MessageType.Info);

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate", GUILayout.Height(32))) _lastReport = Generate();

            if (!string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(_lastReport,
                    _lastReport.Contains("BLOCKED") ? MessageType.Error : MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Generate desert environment vào CẢ 5 map campaign, mỗi map seed riêng, GIỮ nguyên wiring
        /// per-map (WaveDirector + WaveData, RunSystems, spawner) vì generate tại chỗ chứ không clone.
        /// Đồng thời đảm bảo mỗi map có đúng một ToonLightRig (mặc định 50/-30/0).
        /// </summary>
        [MenuItem("Tools/ZombieWar/Generate All Campaign Maps")]
        public static void GenerateAllCampaignMaps()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var win = CreateInstance<DesertMapGeneratorWindow>();
            var report = new System.Text.StringBuilder();
            for (int i = 1; i <= 5; i++)
            {
                string path = $"Assets/_Project/Scenes/Map_Level{i}.unity";
                win.seed = 100 + i;                       // deterministic, khác nhau mỗi màn
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                report.AppendLine(win.GenerateCore(scene));
            }
            DestroyImmediate(win);
            Debug.Log("[MapGen] ALL CAMPAIGN MAPS:\n" + report);
        }

        string Generate()
        {
            AssetDatabase.DeleteAsset(outScene);
            if (!AssetDatabase.CopyAsset(SourceScene, outScene)) return "Could not copy the source scene.";

            var scene = EditorSceneManager.OpenScene(outScene, OpenSceneMode.Single);
            return GenerateCore(scene);
        }

        string GenerateCore(UnityEngine.SceneManagement.Scene scene)
        {
            var rng = new System.Random(seed);

            var roots = scene.GetRootGameObjects();

            // Re-run trên map đã generate: dọn Environment cũ trước (idempotent).
            var oldEnv = roots.FirstOrDefault(r => r.name == "Environment");
            if (oldEnv != null) Object.DestroyImmediate(oldEnv);
            roots = scene.GetRootGameObjects();
            var playerSpawn = roots.FirstOrDefault(r => r.name == "PlayerSpawnPoint");
            Vector3 playerPos = playerSpawn != null ? playerSpawn.transform.position : Vector3.zero;

            var oldGround = roots.FirstOrDefault(r => r.name == "Ground");
            if (oldGround != null) Object.DestroyImmediate(oldGround);
            roots = scene.GetRootGameObjects();   // the cached array still holds the destroyed Ground

            var env = new GameObject("Environment");
            SceneManager.MoveGameObjectToScene(env, scene);

            // ONE occupancy record shared by every scatter pass. The previous version gave rocks
            // and crates/barrels separate lists, so a barrel could not know a rock existed and
            // spawned inside it.
            var occupancy = new Occupancy();
            occupancy.Reserve(playerPos, playerClearRadius);

            var spawnPositions = BuildSpawnRing(scene, roots.FirstOrDefault(r => r.name == "SpawnPoints"), occupancy);

            int tiles = BuildGround(env.transform, rng);
            int cliffs = BuildBoundary(env.transform, occupancy);
            // Gameplay objects claim their ground FIRST; decorative rocks fill in around them.
            // The other way round, a dense scatter leaves no 6 m clearing anywhere and every
            // barrel cluster silently fails to place.
            int interactive = ScatterInteractive(env.transform, rng, occupancy);
            int props = ScatterProps(env.transform, rng, occupancy);

            ApplyBatchingFlags(env);
            EnsureToonLightRig(scene);
            return FinishGeneration(scene, spawnPositions, playerPos, tiles, cliffs, props, interactive);
        }

        /// Mỗi map đúng một ToonLightRig; tạo mới với hướng mặc định (50,-30,0) nếu thiếu,
        /// đã có thì giữ nguyên hướng designer chỉnh.
        static void EnsureToonLightRig(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<ZombieWar.ToonLightRig>(true) != null) return;

            var go = new GameObject("ToonLightRig", typeof(ZombieWar.ToonLightRig));
            go.transform.rotation = Quaternion.Euler(ZombieWar.ToonLightRig.DefaultEuler);
            go.transform.position = new Vector3(0f, 10f, 0f); // vị trí chỉ để gizmo dễ thấy
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        string FinishGeneration(UnityEngine.SceneManagement.Scene scene,
            System.Collections.Generic.List<Vector3> spawnPositions, Vector3 playerPos,
            int tiles, int cliffs, int props, int interactive)
        {

            var navRoot = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "NavMesh");
            var surface = navRoot != null ? navRoot.GetComponent<NavMeshSurface>() : null;
            if (surface != null) MapNavigationAuthoring.BakeSandOnly(surface);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            int ok = VerifyPaths(spawnPositions, playerPos, out int blocked);

            string report =
                $"{System.IO.Path.GetFileNameWithoutExtension(scene.path)}  (seed {seed}, {arenaSize:F0} m)\n" +
                $"{tiles} tiles, {cliffs} cliff pieces, {props} rocks/cacti, {interactive} crates+barrels\n" +
                $"spawn paths: {ok}/{ok + blocked}" + (blocked > 0 ? "  << BLOCKED" : "  all reachable");
            Debug.Log("[MapGen] " + report.Replace("\n", "  |  "));
            return report;
        }

        /// <summary>
        /// Everything already standing on the arena floor, with the spacing it demands.
        ///
        /// Single source of truth for "is this spot free": every placement pass reserves through
        /// here and queries through here, so no pass can overlap another's output. Reserved radii
        /// are kept per entry - a spawn zone pushes props 4 m away while a pebble only needs 1 m.
        /// </summary>
        class Occupancy
        {
            readonly List<(Vector3 pos, float radius)> _entries = new();

            public void Reserve(Vector3 pos, float radius) => _entries.Add((pos, radius));

            /// <summary>True when a new footprint fits without intersecting any reserved disc.</summary>
            public bool IsFree(Vector3 pos, float radius)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    float required = _entries[i].radius + radius;
                    if ((_entries[i].pos - pos).sqrMagnitude < required * required) return false;
                }
                return true;
            }
        }

        static GameObject Place(Transform parent, string prefabPath, Vector3 pos, float yaw,
                                float scale = 1f, bool obstacle = true)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            if (obstacle) MapNavigationAuthoring.EnsureConvexObstacleColliders(go);
            return go;
        }

        static bool TryPlaceOccupied(Transform parent, string prefabPath, Vector3 pos, float yaw,
                                     float scale, float edgePadding, Occupancy occupancy)
        {
            var go = Place(parent, prefabPath, pos, yaw, scale);
            if (go == null) return false;

            MapNavigationAuthoring.GetHorizontalFootprint(go, out Vector3 footprintCenter,
                                                         out float footprintRadius);
            float radius = footprintRadius + Mathf.Max(0f, edgePadding) * 0.5f;
            if (!occupancy.IsFree(footprintCenter, radius))
            {
                Object.DestroyImmediate(go);
                return false;
            }

            occupancy.Reserve(footprintCenter, radius);
            return true;
        }

        int BuildGround(Transform parent, System.Random rng)
        {
            var holder = new GameObject("Ground").transform;
            holder.SetParent(parent, false);

            int half = Mathf.CeilToInt(arenaSize / TileSize / 2f);
            int count = 0;
            for (int x = -half; x <= half; x++)
            for (int z = -half; z <= half; z++)
                if (Place(holder, Pack + "Ground_01.prefab",
                          new Vector3(x * TileSize, 0f, z * TileSize),
                          rng.Next(0, 4) * 90f, 1f, false) != null) count++;
            MapNavigationAuthoring.AssignWalkableGround(holder.gameObject);
            return count;
        }

        /// <summary>
        /// Cliff ring. Walls sit at +/-wallEdge with their body pushed outward; corners sit further
        /// out so their INNER faces line up with the wall inner faces instead of stepping into the
        /// arena. Corner yaw is chosen so the L's mass points outward at each corner.
        /// </summary>
        int BuildBoundary(Transform parent, Occupancy occupancy)
        {
            var holder = new GameObject("Boundary").transform;
            holder.SetParent(parent, false);

            float wallEdge = arenaSize * 0.5f + TileSize * 0.5f;

            // Align the corner's OUTER face with the wall's outer face:
            //   wall outer  = wallEdge + WallBodyDepth (2.79)
            //   corner outer= pivot    + CornerLip     (2.80)
            // -> pivot = wallEdge - 0.01, i.e. corners sit at (+/-wallEdge, +/-wallEdge): the
            // symmetric "27.5 / 27.5" placement. Each L-arm then runs 5 m INWARD along its wall.
            float cornerEdge = wallEdge + WallBodyDepth - CornerLip;

            // The arms cover the last 5 m of each side, so the straight run only needs to reach
            // cornerEdge - 5. Sizing it larger is what created overlap; smaller, gaps.
            // Round, not ceil: with the arena snapped to tile multiples the division is exact, and
            // ceil() was what jammed one extra wall piece into the corner whenever it landed on .996.
            float wallReach = cornerEdge - 5f;
            int perSide = Mathf.Max(1, Mathf.RoundToInt(wallReach * 2f / 5f));
            int count = 0;

            foreach (var outward in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
            {
                var along = new Vector3(outward.z, 0f, -outward.x);
                // Body runs along local -Z, so aim local +Z at -outward to push it outside the arena.
                var rot = Quaternion.LookRotation(-outward, Vector3.up);

                for (int i = 0; i < perSide; i++)
                {
                    float t = (i - (perSide - 1) * 0.5f) * 5f;
                    var go = Place(holder, Pack + "Cliff_01.prefab", along * t + outward * wallEdge, 0f);
                    if (go == null) continue;
                    go.transform.rotation = rot;
                    ReserveObstacle(go, occupancy);
                    count++;
                }
            }

            // The L's arms must run INWARD along the two walls they join - i.e. the mass points
            // back toward the arena centre, not outward. Previous version had this inverted, which
            // is why every corner read as rotated 90 degrees and stuck out of the ring.
            // Mass per yaw (measured): 0 -> (-X,+Z)  90 -> (+X,+Z)  180 -> (+X,-Z)  270 -> (-X,-Z)
            var corners = new (float x, float z, float yaw)[]
            {
                ( cornerEdge,  cornerEdge, 270f),   // needs mass -X,-Z
                (-cornerEdge,  cornerEdge, 180f),   // needs mass +X,-Z
                (-cornerEdge, -cornerEdge,  90f),   // needs mass +X,+Z
                ( cornerEdge, -cornerEdge,   0f),   // needs mass -X,+Z
            };
            foreach (var (x, z, yaw) in corners)
            {
                var go = Place(holder, Pack + "CliffCorner_01.prefab",
                               new Vector3(x, 0f, z), yaw);
                if (go == null) continue;
                ReserveObstacle(go, occupancy);
                count++;
            }

            return count;
        }

        static void ReserveObstacle(GameObject go, Occupancy occupancy)
        {
            MapNavigationAuthoring.GetHorizontalFootprint(go, out Vector3 center, out float radius);
            occupancy.Reserve(center, radius);
        }

        int ScatterProps(Transform parent, System.Random rng, Occupancy occupancy)
        {
            var holder = new GameObject("Props").transform;
            holder.SetParent(parent, false);

            var table = new (string prefab, float weight, float edgePadding)[]
            {
                ("Rock_04",   0.22f, 0.15f), ("Rock_05",   0.20f, 0.15f),
                ("Rock_01",   0.14f, 0.20f), ("Rock_02",   0.10f, 0.25f),
                ("Rock_03",   0.08f, 0.25f), ("Cactus_03", 0.12f, 0.20f),
                ("Cactus_01", 0.08f, 0.25f), ("Tree_01",   0.06f, 0.35f),
            };
            float totalWeight = table.Sum(t => t.weight);

            float halfArena = arenaSize * 0.5f - 2f;
            int attempts = Mathf.RoundToInt(scatterAttempts * scatterDensity);
            int count = 0;

            for (int a = 0; a < attempts; a++)
            {
                var pos = RandomPoint(rng, halfArena);

                double roll = rng.NextDouble() * totalWeight;
                var entry = table[0];
                foreach (var t in table) { roll -= t.weight; if (roll <= 0) { entry = t; break; } }

                float scale = 0.85f + (float)rng.NextDouble() * 0.5f;
                if (!TryPlaceOccupied(holder, Pack + entry.prefab + ".prefab", pos,
                                      (float)rng.NextDouble() * 360f, scale,
                                      entry.edgePadding, occupancy)) continue;
                count++;
            }
            return count;
        }

        int ScatterInteractive(Transform parent, System.Random rng, Occupancy occupancy)
        {
            var holder = new GameObject("Interactive").transform;
            holder.SetParent(parent, false);

            var crates = new[] { "PROP_Crate_Loot", "PROP_Crate_Small" };
            var barrels = new[] { "PROP_Barrel_Fuel", "PROP_Barrel_Fuel_B" };

            // Footprints. Crates keep a wide berth so loot stays reachable; a barrel inside its own
            // cluster only needs body clearance - the cluster look IS barrels standing close.
            const float CrateGap = 0.6f;
            const float BarrelGap = 0.1f;
            const float ClusterGap = 6f;

            float half = arenaSize * 0.5f - 4f;
            int count = 0;

            for (int a = 0; a < crateCount * 14 && count < crateCount; a++)
            {
                var pos = RandomPoint(rng, half);
                if (!TryPlaceOccupied(holder, PropDir + crates[rng.Next(crates.Length)] + ".prefab",
                                      pos, (float)rng.NextDouble() * 360f, 1f,
                                      CrateGap, occupancy)) continue;
                count++;
            }

            // Clusters of 2-4 so the chain reaction has something to chain to.
            for (int cluster = 0; cluster < barrelClusters; cluster++)
            {
                Vector3 centre = Vector3.zero;
                bool found = false;
                for (int a = 0; a < 60 && !found; a++)
                {
                    centre = RandomPoint(rng, half);
                    // Extra clearance for the whole cluster, and never near the player start -
                    // a chain detonation reaching spawn would be a death the player never saw coming.
                    if (occupancy.IsFree(centre, ClusterGap) && centre.magnitude > 12f) found = true;
                }
                if (!found) continue;

                // Bounded attempts rather than a retry-with-index-rewind: guaranteed to terminate,
                // and a cramped cluster simply ends up with fewer barrels instead of looping.
                int wanted = 2 + rng.Next(3);
                int placedInCluster = 0;
                for (int a = 0; a < wanted * 6 && placedInCluster < wanted; a++)
                {
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                    float r = 0.9f + (float)rng.NextDouble() * 0.8f;
                    var pos = centre + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                    if (!TryPlaceOccupied(holder,
                                          PropDir + barrels[rng.Next(barrels.Length)] + ".prefab",
                                          pos, (float)rng.NextDouble() * 360f, 1f,
                                          BarrelGap, occupancy)) continue;
                    placedInCluster++;
                    count++;
                }

                // Reserved AFTER the barrels are down: reserving the centre first would make every
                // barrel fail its own IsFree check against the cluster's reservation.
                if (placedInCluster > 0) occupancy.Reserve(centre, ClusterGap * 0.5f);
            }
            return count;
        }

        static Vector3 RandomPoint(System.Random rng, float halfExtent) =>
            new Vector3((float)(rng.NextDouble() * 2 - 1) * halfExtent, 0f,
                        (float)(rng.NextDouble() * 2 - 1) * halfExtent);

        List<Vector3> BuildSpawnRing(Scene scene, GameObject existing, Occupancy occupancy)
        {
            if (existing != null) Object.DestroyImmediate(existing);

            var holder = new GameObject("SpawnPoints");
            SceneManager.MoveGameObjectToScene(holder, scene);

            var positions = new List<Vector3>();
            var transforms = new List<Transform>();
            float radius = arenaSize * 0.42f;

            for (int i = 0; i < spawnPointCount; i++)
            {
                float angle = i / (float)spawnPointCount * Mathf.PI * 2f;
                var p = new GameObject($"Spawn_{i:00}");
                p.transform.SetParent(holder.transform, false);
                p.transform.position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                transforms.Add(p.transform);
                positions.Add(p.transform.position);
                occupancy.Reserve(p.transform.position, spawnClearRadius);
            }

            var spawnerGo = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "WaveDirector");
            var spawner = spawnerGo != null ? spawnerGo.GetComponent<ZombieSpawner>() : null;
            if (spawner == null) return positions;

            var so = new SerializedObject(spawner);
            var prop = so.FindProperty("spawnPoints");
            if (prop != null)
            {
                prop.arraySize = transforms.Count;
                for (int i = 0; i < transforms.Count; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return positions;
        }

        void ApplyBatchingFlags(GameObject env)
        {
            // Interactive props are excluded: they get disabled when destroyed, so they must not be
            // merged into a static batch.
            var interactive = env.transform.Find("Interactive");

            foreach (var r in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool isInteractive = interactive != null && r.transform.IsChildOf(interactive);

                if (disableShadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;

                if (markStatic && !isInteractive)
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            }
        }

        static int VerifyPaths(List<Vector3> spawns, Vector3 playerPos, out int blocked)
        {
            int ok = 0; blocked = 0;
            foreach (var s in spawns)
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(s, playerPos, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete) ok++;
                else blocked++;
            }
            return ok;
        }
    }
}
