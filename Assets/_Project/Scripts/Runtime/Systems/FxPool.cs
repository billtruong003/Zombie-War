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
    ///
    /// M8: <paramref name="scale"/> lets an effect match the area it represents. A 1 m explosion on a
    /// 3.5 m blast reads as "the skill missed half the crowd", which is exactly the mismatch that made
    /// powers feel wrong. Pooled instances are always re-scaled, so a scaled play never leaks its
    /// size into the next unscaled one.
    /// </summary>
    public static class FxPool
    {
        private const float FallbackLifetime = 2f;

        public static ParticleSystem Play(ParticleSystem prefab, Vector3 position, Quaternion rotation)
            => Play(prefab, position, rotation, 1f);

        public static ParticleSystem Play(ParticleSystem prefab, Vector3 position, Quaternion rotation, float scale)
        {
            var ps = Spawn(prefab, position, rotation, scale, out var go);
            if (go == null) return ps;

            float ttl = FallbackLifetime;
            if (ps != null)
            {
                float measured = Lifetime(ps);
                if (measured > 0f) ttl = measured;
            }
            Bill.Pool?.Return(go, ttl);
            return ps;
        }

        /// <summary>
        /// Plays a (usually looping) effect for <paramref name="seconds"/>, then stops emitting and
        /// returns it once its last particles have died. For burning ground and similar zones whose
        /// lifetime is gameplay, not the prefab's.
        /// </summary>
        public static ParticleSystem PlayFor(ParticleSystem prefab, Vector3 position, Quaternion rotation,
                                             float scale, float seconds)
        {
            var ps = Spawn(prefab, position, rotation, scale, out var go);
            if (go == null || ps == null) return ps;

            float tail = ps.main.startLifetime.constantMax;
            var pool = Bill.Pool;
            if (pool == null) return ps;
            Bill.Timer?.Delay(seconds, () => { if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); });
            pool.Return(go, seconds + tail);
            return ps;
        }

        private static ParticleSystem Spawn(ParticleSystem prefab, Vector3 position, Quaternion rotation,
                                            float scale, out GameObject go)
        {
            go = null;
            if (prefab == null) return null;

            var pool = Bill.Pool;
            if (pool == null)
            {
                var loose = Object.Instantiate(prefab, position, rotation);
                loose.transform.localScale = Vector3.one * scale;
                float looseTtl = Lifetime(loose);
                Object.Destroy(loose.gameObject, looseTtl > 0f ? looseTtl : FallbackLifetime);
                return loose;
            }

            string key = "fx_" + prefab.GetInstanceID();
            pool.Register(key, prefab.gameObject);

            go = pool.Spawn(key, position, rotation);
            if (go == null) return null;
            go.transform.localScale = Vector3.one * scale;

            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
            }
            return ps;
        }

        private static float Lifetime(ParticleSystem ps)
        {
            var main = ps.main;
            return main.duration + main.startLifetime.constantMax;
        }
    }
}
