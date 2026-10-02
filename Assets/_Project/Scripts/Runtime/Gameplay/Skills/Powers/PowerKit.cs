using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Small helpers every power shares.</summary>
    public static class PowerKit
    {
        public static ZombieBase EnemyOf(Collider c) => c != null ? c.GetComponentInParent<ZombieBase>() : null;

        /// <summary>
        /// Damages an ENEMY only, and books it in the ledger under <paramref name="source"/>.
        ///
        /// Resolves <see cref="ZombieBase"/>, never any IDamageable: powers are centred on the player,
        /// the broad-phase sweep returns the player's own collider, and the first play-test of the
        /// driver killed the player with their own Chain Lightning. The type filter cannot be
        /// re-broken by a layer-mask edit.
        /// </summary>
        public static void Hit(ZombieBase enemy, float damage, float push, string source)
        {
            if (enemy == null || enemy.IsDead) return;
            enemy.TakeDamage(damage);
            DamageLedger.Record(source, damage);
            if (push > 0f && !enemy.IsDead) enemy.ApplyPhysicalPush(push);
        }

        public static void Hit(Collider col, float damage, float push, string source) =>
            Hit(col != null ? col.GetComponentInParent<ZombieBase>() : null, damage, push, source);

        public static Vector3 Chest(ZombieBase e) => e.transform.position + Vector3.up * 0.9f;

        /// <summary>
        /// The rotation the effect was authored with. Many Epic Toon FX prefabs are built lying flat
        /// (root at -90° X); playing them with Quaternion.identity stood them upright, so ground rings
        /// became half-domes cut by the floor and decals stood on their edge.
        /// </summary>
        public static Quaternion Flat(ParticleSystem prefab) =>
            prefab != null ? prefab.transform.localRotation : Quaternion.identity;

        /// <summary>Plays an effect scaled so its measured native radius matches <paramref name="radius"/>.</summary>
        public static void PlaySized(ParticleSystem fx, Vector3 at, float radius, float nativeRadius, float cap = float.MaxValue)
        {
            if (fx == null) return;
            FxPool.Play(fx, at, Flat(fx), Mathf.Min(radius / Mathf.Max(0.1f, nativeRadius), cap));
            // The blast lights its surroundings for a moment, in the effect's own colour.
            Color c = fx.main.startColor.Evaluate(0.5f);
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak > 0.01f) c /= peak;
            ToonPointLights.Flash(at + Vector3.up * 0.8f, c, Mathf.Max(3f, radius * 1.6f), 1.6f, 0.35f);
        }
    }
}
