using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Authors the Stage 2-5 gameplay scenes as clones of the Map_Level1 contract.
    ///
    /// Map_Level1 is the reference, not a template asset: it is opened, duplicated through Unity's
    /// own scene APIs, then retuned per stage. That keeps every wired reference the working scene
    /// already has (HUD, camera follow, spawner, EventSystem) instead of trying to reconstruct them,
    /// which is where hand-rolled scene builders usually go wrong.
    ///
    /// Idempotent: re-running overwrites the generated stages in place and never touches
    /// Map_Level1 itself. Stage 1 remains the owner's existing, verified arena.
    ///
    /// Menu: Tools/ZombieWar/Build Campaign Stages.
    /// </summary>
    public static class CampaignStageBuilder
    {
        const string SceneDir = "Assets/_Project/Scenes";
        const string SourceScene = SceneDir + "/Map_Level1.unity";
        const string MatDir = "Assets/_Project/Art/Materials/Stages";

        /// <summary>Per-stage arena identity. Ground size is deliberately modest and uniform-ish:
        /// these are placeholder arenas, and the owner's environment pass comes later.</summary>
        struct StageDef
        {
            public int index;            // 2..5
            public string sceneName;
            public string displayName;
            public float groundSize;     // metres across
            public Color tint;           // so screenshots can never be confused between stages
            public int spawnRings;
        }

        static readonly StageDef[] Stages =
        {
            new StageDef { index = 2, sceneName = "Map_Level2", displayName = "Thorn Fields",
                           groundSize = 75f, tint = new Color(0.45f, 0.55f, 0.32f), spawnRings = 10 },
            new StageDef { index = 3, sceneName = "Map_Level3", displayName = "Bone Yard",
                           groundSize = 80f, tint = new Color(0.52f, 0.50f, 0.46f), spawnRings = 12 },
            new StageDef { index = 4, sceneName = "Map_Level4", displayName = "Wild Pack",
                           groundSize = 85f, tint = new Color(0.38f, 0.44f, 0.52f), spawnRings = 12 },
            new StageDef { index = 5, sceneName = "Map_Level5", displayName = "Titan Siege",
                           groundSize = 90f, tint = new Color(0.50f, 0.34f, 0.36f), spawnRings = 14 },
        };

        [MenuItem("Tools/ZombieWar/Build Campaign Stages")]
        public static void BuildAll()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            {
                Debug.LogError($"[StageBuilder] Source scene missing: {SourceScene}");
                return;
            }

            EnsureFolder(MatDir);
            var built = new List<string>();

            foreach (var stage in Stages)
            {
                string path = $"{SceneDir}/{stage.sceneName}.unity";

                // Copy on disk first, then open the copy. Duplicating the file (rather than building
                // a scene from scratch) is what preserves every existing wired reference.
                AssetDatabase.DeleteAsset(path);
                if (!AssetDatabase.CopyAsset(SourceScene, path))
                {
                    Debug.LogError($"[StageBuilder] Failed to copy {SourceScene} -> {path}");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    Retune(scene, stage);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    built.Add(stage.sceneName);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RegisterInBuildSettings();

            Debug.Log($"[StageBuilder] Built {built.Count} stages: {string.Join(", ", built)}. " +
                      "Map_Level1 untouched (it remains Stage 1).");
        }

        static void Retune(Scene scene, StageDef stage)
        {
            var roots = scene.GetRootGameObjects();

            // ---- ground: resize + unique tint ------------------------------------------------
            var ground = roots.FirstOrDefault(r => r.name == "Ground");
            if (ground != null)
            {
                // Unity's built-in Plane is 10 m across at scale 1.
                ground.transform.localScale = Vector3.one * (stage.groundSize / 10f);
                ground.transform.position = Vector3.zero;
                MapNavigationAuthoring.AssignWalkableGround(ground);

                var mr = ground.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = StageMaterial(stage);
            }

            // ---- spawn ring: authored points around the perimeter ----------------------------
            BuildSpawnRing(scene, stage);

            // ---- navmesh: rebake for the new ground size -------------------------------------
            var navRoot = roots.FirstOrDefault(r => r.name == "NavMesh");
            var surface = navRoot != null ? navRoot.GetComponent<NavMeshSurface>() : null;
            if (surface != null) MapNavigationAuthoring.BakeSandOnly(surface);
            else Debug.LogWarning($"[StageBuilder] {stage.sceneName}: no NavMeshSurface found to bake.");
        }

        /// <summary>
        /// Spawn points on a ring just inside the arena edge. Kept at the perimeter and away from the
        /// centre so enemies never appear on top of the player or inside the starting camera view -
        /// the player always sees them come in.
        /// </summary>
        static void BuildSpawnRing(Scene scene, StageDef stage)
        {
            var roots = scene.GetRootGameObjects();
            var spawnerGo = roots.FirstOrDefault(r => r.name == "WaveDirector");
            if (spawnerGo == null) return;

            // Rebuild from scratch so re-running cannot accumulate duplicate points.
            var existing = roots.FirstOrDefault(r => r.name == "SpawnPoints");
            if (existing != null) Object.DestroyImmediate(existing);

            var holder = new GameObject("SpawnPoints");
            SceneManager.MoveGameObjectToScene(holder, scene);

            float radius = stage.groundSize * 0.42f;   // inside the edge, clear of the boundary
            var points = new List<Transform>(stage.spawnRings);
            for (int i = 0; i < stage.spawnRings; i++)
            {
                float angle = i / (float)stage.spawnRings * Mathf.PI * 2f;
                var p = new GameObject($"Spawn_{i:00}");
                p.transform.SetParent(holder.transform, false);
                p.transform.position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                points.Add(p.transform);
            }

            // Wire them into the existing ZombieSpawner rather than adding a second spawn system.
            var spawner = spawnerGo.GetComponent<ZombieSpawner>();
            if (spawner == null) return;

            var so = new SerializedObject(spawner);
            var prop = FindArrayProperty(so, "spawnPoints", "points", "spawnPositions");
            if (prop == null)
            {
                Debug.LogWarning($"[StageBuilder] {stage.sceneName}: ZombieSpawner has no recognised " +
                                 "spawn-point array; ring created but not wired.");
                return;
            }

            prop.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static SerializedProperty FindArrayProperty(SerializedObject so, params string[] candidates)
        {
            foreach (var name in candidates)
            {
                var p = so.FindProperty(name);
                if (p != null && p.isArray) return p;
            }
            return null;
        }

        /// <summary>A distinct flat material per stage so a screenshot of Stage 3 can never be
        /// mistaken for Stage 4. Created under _Project, never a vendor material.</summary>
        static Material StageMaterial(StageDef stage)
        {
            string path = $"{MatDir}/Ground_Stage{stage.index}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", stage.tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", stage.tint);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Adds every campaign scene to Build Settings in stage order, without disturbing
        /// Bootstrap/Menu or duplicating entries.</summary>
        static void RegisterInBuildSettings()
        {
            var wanted = new List<string> { $"{SceneDir}/Bootstrap.unity", $"{SceneDir}/Menu.unity", SourceScene };
            wanted.AddRange(Stages.Select(s => $"{SceneDir}/{s.sceneName}.unity"));

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in wanted)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                if (scenes.Any(s => s.path == path)) continue;
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
