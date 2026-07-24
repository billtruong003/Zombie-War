using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Single authoring contract for generated arenas:
    /// only WalkableGround contributes to the NavMesh, while every placed obstacle owns a
    /// non-trigger convex collider. Keeping this in one place prevents the three map builders from
    /// silently producing different navigation rules.
    /// </summary>
    internal static class MapNavigationAuthoring
    {
        internal const string WalkableGroundLayerName = "WalkableGround";
        internal const string NavObstacleLayerName = "NavObstacle";
        const int ConvexMeshTriangleLimit = 255;

        /// <summary>
        /// NavMeshAgents ignore physics colliders entirely, so an obstacle that only has a collider
        /// is invisible to enemy pathing - agents walk straight through rocks. Static obstacles must
        /// therefore ALSO be part of the bake, on a dedicated layer, marked NotWalkable so their
        /// footprint is carved out of the mesh. The layer is created on demand in the first free
        /// TagManager slot.
        /// </summary>
        internal static int EnsureNavObstacleLayer()
        {
            int layer = LayerMask.NameToLayer(NavObstacleLayerName);
            if (layer >= 0) return layer;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                throw new System.InvalidOperationException("TagManager.asset not found.");
            var tagManager = new SerializedObject(assets[0]);
            var layersProp = tagManager.FindProperty("layers");
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var slot = layersProp.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = NavObstacleLayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return i;
            }
            throw new System.InvalidOperationException(
                $"No free layer slot available for '{NavObstacleLayerName}'.");
        }

        internal static void AssignWalkableGround(GameObject root)
        {
            int layer = LayerMask.NameToLayer(WalkableGroundLayerName);
            if (layer < 0)
                throw new System.InvalidOperationException(
                    $"Required layer '{WalkableGroundLayerName}' is missing from TagManager.");

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        internal static void BakeSandOnly(NavMeshSurface surface)
        {
            if (surface == null) return;

            int layer = LayerMask.NameToLayer(WalkableGroundLayerName);
            if (layer < 0)
                throw new System.InvalidOperationException(
                    $"Required layer '{WalkableGroundLayerName}' is missing from TagManager.");

            surface.collectObjects = CollectObjects.All;
            // Obstacles participate in the bake purely to carve NotWalkable holes (see
            // MarkStaticNavObstacle); the walkable surface still comes from WalkableGround only.
            int obstacleLayer = EnsureNavObstacleLayer();
            surface.layerMask = (1 << layer) | (1 << obstacleLayer);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        /// <summary>
        /// MeshCollider convex cooking is limited to 255 triangles. Complex meshes are represented
        /// by a conservative BoxCollider instead; primitive colliders already satisfy the convex
        /// obstacle requirement and are kept untouched.
        /// </summary>
        internal static void EnsureConvexObstacleColliders(GameObject root)
        {
            if (root == null) return;

            foreach (var meshCollider in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (meshCollider.sharedMesh != null &&
                    CountTriangles(meshCollider.sharedMesh) <= ConvexMeshTriangleLimit)
                {
                    meshCollider.convex = true;
                    meshCollider.isTrigger = false;
                    continue;
                }

                var owner = meshCollider.gameObject;
                var filter = owner.GetComponent<MeshFilter>();
                Object.DestroyImmediate(meshCollider);
                var box = owner.GetComponent<BoxCollider>();
                if (box == null) box = owner.AddComponent<BoxCollider>();
                if (box == null) continue;
                if (filter != null && filter.sharedMesh != null)
                {
                    box.center = filter.sharedMesh.bounds.center;
                    box.size = filter.sharedMesh.bounds.size;
                }
                box.isTrigger = false;
            }

            if (!root.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger))
            {
                if (!TryGetLocalRendererBounds(root, out var localBounds)) return;
                var fallback = root.AddComponent<BoxCollider>();
                fallback.center = localBounds.center;
                fallback.size = localBounds.size;
                fallback.isTrigger = false;
            }

            MarkStaticNavObstacle(root);
        }

        /// <summary>
        /// Puts a static obstacle's colliders on the NavObstacle layer and marks the whole object
        /// NotWalkable, so the next bake carves its footprint out of the mesh. Destructible props
        /// are skipped: they disappear at runtime, and a static carve would leave a permanent hole
        /// where a destroyed crate used to be.
        /// </summary>
        internal static void MarkStaticNavObstacle(GameObject root)
        {
            if (root == null || root.GetComponentInChildren<DestructibleProp>(true) != null) return;

            int obstacleLayer = EnsureNavObstacleLayer();
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                if (!collider.isTrigger)
                    collider.gameObject.layer = obstacleLayer;

            if (!root.TryGetComponent(out NavMeshModifier modifier))
                modifier = root.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
        }

        internal static float GetHorizontalFootprintRadius(GameObject root)
        {
            GetHorizontalFootprint(root, out _, out float radius);
            return radius;
        }

        internal static void GetHorizontalFootprint(
            GameObject root, out Vector3 worldCenter, out float radius)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true)
                .Where(c => !c.isTrigger && c.enabled)
                .ToArray();
            if (colliders.Length == 0)
            {
                worldCenter = root.transform.position;
                radius = 0.25f;
                return;
            }

            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            worldCenter = bounds.center;
            worldCenter.y = root.transform.position.y;
            radius = Mathf.Max(0.25f, new Vector2(bounds.extents.x, bounds.extents.z).magnitude);
        }

        [MenuItem("Tools/ZombieWar/Rebake Sand-only NavMeshes")]
        static void RebakeCampaignMaps()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int updated = 0;
            for (int i = 1; i <= 5; i++)
            {
                string path = $"Assets/_Project/Scenes/Map_Level{i}.unity";
                if (!System.IO.File.Exists(path)) continue;

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var ground = roots.FirstOrDefault(r => r.name == "Ground")
                             ?? roots.FirstOrDefault(r => r.name == "Environment")
                                 ?.transform.Find("Ground")?.gameObject;
                var navRoot = roots.FirstOrDefault(r => r.name == "NavMesh");
                var surface = navRoot != null ? navRoot.GetComponent<NavMeshSurface>() : null;
                if (ground == null || surface == null)
                {
                    Debug.LogWarning($"[MapNavigation] {path}: missing Ground or NavMeshSurface.");
                    continue;
                }

                AssignWalkableGround(ground);
                var environment = roots.FirstOrDefault(r => r.name == "Environment");
                if (environment != null)
                {
                    foreach (Transform child in environment.transform)
                        if (child.gameObject != ground)
                            EnsureConvexObstacleColliders(child.gameObject);
                }

                BakeSandOnly(surface);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                updated++;
            }

            Debug.Log($"[MapNavigation] Rebaked {updated} campaign maps from WalkableGround only.");
        }

        static int CountTriangles(Mesh mesh)
        {
            long indexCount = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                indexCount += (long)mesh.GetIndexCount(i);
            return (int)(indexCount / 3L);
        }

        static bool TryGetLocalRendererBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            var corners = new List<Vector3>(renderers.Length * 8);
            foreach (var renderer in renderers)
            {
                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    var corner = new Vector3(x == 0 ? min.x : max.x,
                                             y == 0 ? min.y : max.y,
                                             z == 0 ? min.z : max.z);
                    corners.Add(root.transform.InverseTransformPoint(corner));
                }
            }

            bounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < corners.Count; i++) bounds.Encapsulate(corners[i]);
            return true;
        }
    }
}
