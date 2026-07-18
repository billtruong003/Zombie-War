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

        /// <summary>Pop a floating damage number at a world position.</summary>
        /// <param name="crit">Plumbed for a future crit system; pass false today.</param>
        public static void Spawn(float amount, Vector3 position, bool crit = false)
        {
            var prefab = ResolvePrefab();
            if (prefab == null) return;

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
            pool.Register(PoolKey, prefab.gameObject);

            var number = pool.Spawn<DamageNumber>(PoolKey, position, Quaternion.identity);
            if (number != null) number.Show(amount, crit);
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
