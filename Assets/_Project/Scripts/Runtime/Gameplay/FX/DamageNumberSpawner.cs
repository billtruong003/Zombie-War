using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// <summary>
    /// Stateless entry point for spawning pooled damage numbers. Mirrors <see cref="TracerPool"/>
    /// and <see cref="FxPool"/>: no singleton, no per-caller wiring. The DamageNumber prefab lives
    /// in a Resources folder and is loaded + registered with Bill.Pool lazily on first use, so any
    /// scene works without a bootstrap and designers never touch pool keys.
    ///
    /// If the prefab is missing this no-ops; if the pool service isn't up (a scene opened without a
    /// bootstrap) we fall back to a self-destroying Instantiate - so gameplay never breaks over a
    /// missing bit of juice.
    /// </summary>
    public static class DamageNumberSpawner
    {
        /// <summary>Resources path (no extension) the DamageNumber prefab loads from.</summary>
        public const string ResourcePath = "FX/DamageNumber";

        /// <summary>Pool key the loaded prefab registers under.</summary>
        public const string PoolKey = "damage_number";

        private const float FallbackLifetime = 1f;

        private static DamageNumber _prefab;
        private static bool _loadAttempted;

        // At most this many new numbers per frame (crits always show): a damage-over-time tick over a
        // crowd used to pop a hundred numbers in one frame, each its own text mesh.
        // And at most this many on screen (07/10 audit: a horde kept 300-580 alive, each its own
        // renderer and Update). Past it the crowd reads as a blur anyway.
        public const int MaxPerFrame = 6;
        public const int MaxLive = 40;
        public const int WarmCount = 32;
        private static int _frame = -1, _spawnedThisFrame;

        /// <summary>Pop a floating damage number at a world position.</summary>
        /// <param name="crit">Plumbed for a future crit system; pass false today.</param>
        public static void Spawn(float amount, Vector3 position, bool crit = false)
        {
            if (_frame != Time.frameCount) { _frame = Time.frameCount; _spawnedThisFrame = 0; }
            if (!crit && (_spawnedThisFrame >= MaxPerFrame || DamageNumber.Live >= MaxLive)) return;
            var prefab = ResolvePrefab();
            if (prefab == null) return;
            _spawnedThisFrame++;

            var pool = Bill.Pool;
            if (pool == null)
            {
                // Isolation / no bootstrap: a self-destroying instance still shows the juice.
                var loose = Object.Instantiate(prefab, position, Quaternion.identity);
                loose.Show(amount, crit);
                Object.Destroy(loose.gameObject, FallbackLifetime);
                return;
            }

            // Idempotent register-by-key (mirrors TracerPool) then pool-spawn.
            pool.Register(PoolKey, prefab.gameObject, WarmCount);

            var number = pool.Spawn<DamageNumber>(PoolKey, position, Quaternion.identity);
            if (number != null) number.Show(amount, crit);
        }

        /// <summary>A pickup label above the player (05/10: nothing said what was picked up).
        /// Never throttled - pickups are rare compared with hits.</summary>
        public static void SpawnLabel(string label, Color color, Vector3 position)
        {
            var prefab = ResolvePrefab();
            if (prefab == null || Bill.Pool == null) return;
            Bill.Pool.Register(PoolKey, prefab.gameObject, WarmCount);
            var number = Bill.Pool.Spawn<DamageNumber>(PoolKey, position, Quaternion.identity);
            if (number != null) number.ShowLabel(label, color);
        }

        private static DamageNumber ResolvePrefab()
        {
            if (_loadAttempted) return _prefab;

            _loadAttempted = true;
            _prefab = Resources.Load<DamageNumber>(ResourcePath);
            if (_prefab == null)
                Debug.LogWarning($"[DamageNumberSpawner] Prefab not found at Resources/{ResourcePath}; damage numbers disabled.");

            return _prefab;
        }
    }
}
