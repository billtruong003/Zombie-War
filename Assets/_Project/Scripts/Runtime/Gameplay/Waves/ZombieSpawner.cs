using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using BillGameCore;

namespace ZombieWar
{
    // Turns a ZombieData into a live, pooled zombie placed on a valid NavMesh position. Owns the
    // pool-key convention (mirrors BombThrower: "zombie_" + prefab instance id) and the placement
    // policy (fixed spawn points if authored, otherwise a ring around the player). The spawned
    // prefab already carries its own ZombieData, so it self-configures (Health.Configure + register)
    // on OnEnable - the spawner only decides WHERE, never WHAT stats.
    public class ZombieSpawner : MonoBehaviour
    {
        [Header("Ring spawn around player (used when no fixed spawn points)")]
        [SerializeField] private float minSpawnRadius = 12f;
        [SerializeField] private float maxSpawnRadius = 22f;
        [SerializeField, Min(0.1f)] private float navSampleMaxDistance = 1f;
        [SerializeField, Min(1)] private int maxPlacementAttempts = 32;
        [SerializeField, Min(0f)] private float obstacleClearance = 0.12f;

        [Header("Fixed spawn points (overrides ring spawn when non-empty)")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Pool warmup")]
        [SerializeField] private int warmCountPerType = 8;

        private readonly HashSet<string> _registered = new();

        public static string KeyFor(ZombieData data) => "zombie_" + data.prefab.GetInstanceID();

        // Idempotent - safe to call every wave. Registers + warms the pool the first time only.
        public void EnsureRegistered(ZombieData data)
        {
            if (data == null || data.prefab == null) return;
            string key = KeyFor(data);
            if (_registered.Contains(key)) return;

            var pool = Bill.Pool;
            if (pool == null) return;

            pool.Register(key, data.prefab, warmCountPerType);
            _registered.Add(key);
        }

        public ZombieBase Spawn(ZombieData data)
        {
            if (data == null || data.prefab == null) return null;

            var pool = Bill.Pool;
            if (pool == null)
            {
                Debug.LogWarning("[ZombieSpawner] Bill.Pool not ready - skipping spawn.");
                return null;
            }

            EnsureRegistered(data);

            if (!TryGetSpawnPosition(data, out Vector3 pos))
            {
                Debug.LogWarning($"[ZombieSpawner] No clear, reachable spawn found for {data.name}.");
                return null;
            }

            var go = pool.Spawn(KeyFor(data), pos, Quaternion.identity);
            return go != null ? go.GetComponent<ZombieBase>() : null;
        }

        private bool TryGetSpawnPosition(ZombieData data, out Vector3 result)
        {
            GetAgentDimensions(data, out float radius, out float height);
            var player = PlayerMovement.Instance;
            Vector3 target = player != null ? player.transform.position : transform.position;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                for (int i = 0; i < maxPlacementAttempts; i++)
                {
                    var t = spawnPoints[Random.Range(0, spawnPoints.Length)];
                    if (t != null && IsClearAndReachable(t.position, target, radius, height, out result))
                        return true;
                }
                result = default;
                return false;
            }

            Vector3 center = target;

            for (int i = 0; i < maxPlacementAttempts; i++)
            {
                Vector2 flat = Random.insideUnitCircle.normalized;
                if (flat == Vector2.zero) flat = Vector2.right;
                float r = Random.Range(minSpawnRadius, maxSpawnRadius);
                Vector3 candidate = center + new Vector3(flat.x, 0f, flat.y) * r;

                if (IsClearAndReachable(candidate, target, radius, height, out result))
                    return true;
            }

            result = default;
            return false;
        }

        private bool IsClearAndReachable(
            Vector3 candidate, Vector3 target, float radius, float height, out Vector3 result)
        {
            result = default;
            if (!NavMesh.SamplePosition(candidate, out var spawnHit, navSampleMaxDistance, NavMesh.AllAreas))
                return false;

            float checkRadius = radius + obstacleClearance;
            float upperY = Mathf.Max(checkRadius, height - checkRadius);
            Vector3 lower = spawnHit.position + Vector3.up * checkRadius;
            Vector3 upper = spawnHit.position + Vector3.up * upperY;

            int groundLayer = LayerMask.NameToLayer("WalkableGround");
            int obstacleMask = groundLayer >= 0 ? ~(1 << groundLayer) : Physics.AllLayers;
            if (Physics.CheckCapsule(lower, upper, checkRadius, obstacleMask,
                                     QueryTriggerInteraction.Ignore))
                return false;

            if (!NavMesh.SamplePosition(target, out var targetHit, navSampleMaxDistance,
                                        NavMesh.AllAreas))
                return false;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(spawnHit.position, targetHit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                return false;

            result = spawnHit.position;
            return true;
        }

        private static void GetAgentDimensions(ZombieData data, out float radius, out float height)
        {
            var agent = data.prefab.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                radius = 0.4f;
                height = 1.8f;
                return;
            }

            Vector3 scale = data.prefab.transform.lossyScale;
            radius = Mathf.Max(0.1f, agent.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            height = Mathf.Max(radius * 2f, agent.height * Mathf.Abs(scale.y));
        }
    }
}
