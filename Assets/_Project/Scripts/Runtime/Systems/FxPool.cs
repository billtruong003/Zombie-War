using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// <summary>
    /// Spawns short-lived particle VFX (muzzle flashes, impacts, smoke trails, explosions)
    /// through the BillGameCore pool service so we never churn Instantiate/Destroy per shot.
    /// Prefabs are registered lazily by instance id, so designers keep wiring plain
    /// ParticleSystem references on the data assets - no manual pool keys required.
    /// Falls back to a self-destroying Instantiate when the pool service isn't up yet
    /// (e.g. a scene opened without the bootstrap), so FX still render in isolation.
    /// </summary>
    public static class FxPool
    {
        private const float FallbackLifetime = 2f;

        public static ParticleSystem Play(ParticleSystem prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            var pool = Bill.Pool;
            if (pool == null)
            {
                var loose = Object.Instantiate(prefab, position, rotation);
                float looseTtl = Lifetime(loose);
                Object.Destroy(loose.gameObject, looseTtl > 0f ? looseTtl : FallbackLifetime);
                return loose;
            }

            string key = "fx_" + prefab.GetInstanceID();
            pool.Register(key, prefab.gameObject);

            var go = pool.Spawn(key, position, rotation);
            if (go == null) return null;

            var ps = go.GetComponent<ParticleSystem>();
            float ttl = FallbackLifetime;
            if (ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
                float measured = Lifetime(ps);
                if (measured > 0f) ttl = measured;
            }

            pool.Return(go, ttl);
            return ps;
        }

        private static float Lifetime(ParticleSystem ps)
        {
            var main = ps.main;
            return main.duration + main.startLifetime.constantMax;
        }
    }
}
