using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// <summary>
    /// Spawns pooled <see cref="MeshTracer"/> instances through the BillGameCore pool
    /// service, mirroring <see cref="FxPool"/> but for GameObject-based mesh tracers.
    /// Prefabs are registered lazily by instance id so designers just wire a tracer
    /// prefab reference on the WeaponData - no manual pool keys. The tracer returns
    /// itself to the pool when its one-shot animation finishes. Falls back to a
    /// self-destroying Instantiate when the pool service isn't up yet (isolation).
    /// </summary>
    public static class TracerPool
    {
        private const float FallbackLifetime = 1f;

        public static MeshTracer Play(GameObject prefab, Vector3 start, Vector3 end)
        {
            if (prefab == null) return null;

            var pool = Bill.Pool;
            if (pool == null)
            {
                var loose = Object.Instantiate(prefab, start, Quaternion.identity);
                var lt = loose.GetComponent<MeshTracer>();
                if (lt != null) lt.Play(start, end);
                Object.Destroy(loose, FallbackLifetime);
                return lt;
            }

            string key = "tracer_" + prefab.GetInstanceID();
            pool.Register(key, prefab);

            var go = pool.Spawn(key, start, Quaternion.identity);
            if (go == null) return null;

            var tracer = go.GetComponent<MeshTracer>();
            if (tracer == null)
            {
                pool.Return(go);
                return null;
            }

            tracer.Play(start, end);
            return tracer;
        }
    }
}
