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
        [SerializeField] private float navSampleMaxDistance = 4f;
        [SerializeField] private int maxPlacementAttempts = 12;

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

            if (!TryGetSpawnPosition(out Vector3 pos)) return null;

            var go = pool.Spawn(KeyFor(data), pos, Quaternion.identity);
            return go != null ? go.GetComponent<ZombieBase>() : null;
        }

        private bool TryGetSpawnPosition(out Vector3 result)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                for (int i = 0; i < maxPlacementAttempts; i++)
                {
                    var t = spawnPoints[Random.Range(0, spawnPoints.Length)];
                    if (t != null &&
                        NavMesh.SamplePosition(t.position, out var hit, navSampleMaxDistance, NavMesh.AllAreas))
                    {
                        result = hit.position;
                        return true;
                    }
                }
                result = default;
                return false;
            }

            var player = PlayerMovement.Instance;
            Vector3 center = player != null ? player.transform.position : transform.position;

            for (int i = 0; i < maxPlacementAttempts; i++)
            {
                Vector2 flat = Random.insideUnitCircle.normalized;
                if (flat == Vector2.zero) flat = Vector2.right;
                float r = Random.Range(minSpawnRadius, maxSpawnRadius);
                Vector3 candidate = center + new Vector3(flat.x, 0f, flat.y) * r;

                if (NavMesh.SamplePosition(candidate, out var hit, navSampleMaxDistance, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
