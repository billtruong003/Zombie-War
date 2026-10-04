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

        public static MeshTracer Play(GameObject prefab, Vector3 start, Vector3 end) => Play(prefab, start, end, null, 1f);

        /// <summary>M8: a tinted, thinner or thicker tracer (the drone's rank colour).</summary>
        public static MeshTracer Play(GameObject prefab, Vector3 start, Vector3 end, Color? tint, float thicknessScale)
        {
            if (prefab == null) return null;

            var pool = Bill.Pool;
            if (pool == null)
            {
                var loose = Object.Instantiate(prefab, start, Quaternion.identity);
                var lt = loose.GetComponent<MeshTracer>();
                if (lt != null) Fire(lt, start, end, tint, thicknessScale);
                Object.Destroy(loose, FallbackLifetime);
                return lt;
            }

            string key = PoolKeys.For("tracer_", prefab);
            pool.Register(key, prefab);

            var go = pool.Spawn(key, start, Quaternion.identity);
            if (go == null) return null;

            var tracer = go.GetComponent<MeshTracer>();
            if (tracer == null)
            {
                pool.Return(go);
                return null;
            }

            Fire(tracer, start, end, tint, thicknessScale);
            return tracer;
        }

        static void Fire(MeshTracer t, Vector3 start, Vector3 end, Color? tint, float thicknessScale)
        {
            if (tint.HasValue) t.Play(start, end, tint.Value, thicknessScale);
            else t.Play(start, end);
        }
    }
}
