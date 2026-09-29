using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // Straight-flying spit fired by ZombieRanged. In a top-down shooter the only thing a zombie's
    // projectile can hit is the player, so it just tracks the player's distance instead of pulling
    // in physics layers/collision matrices - simpler and no scene layer setup required.
    //
    // The pooled prefabs live in Resources/Pools (ZombieSpit, BoneBolt), built by
    // HordeCall/Enemies/Build Enemy Projectiles. Until 2026-09-29 no such prefab existed, so every
    // ranged enemy's shot failed to spawn and only logged a pool error.
    public sealed class ZombieSpitProjectile : PooledObject
    {
        [SerializeField] private float lifeTime = 4f;
        [SerializeField] private float hitRadius = 0.4f;
        [Tooltip("Burst where it hits the player (and, smaller, where it fizzles out).")]
        [SerializeField] private ParticleSystem impactFx;
        [SerializeField] private float impactScale = 0.6f;
        const float PlayerBodyRadius = 0.35f;

        private Vector3 _velocity;
        private float _damage;
        private float _life;
        private string _impactSfxKey;
        private ParticleSystem[] _systems;
        private TrailRenderer[] _trails;

        /// <param name="impactSfxKey">
        /// The shooter's own impact cue, carried on the projectile. A ranged enemy's shot is the one
        /// hit the player never sees coming, so it has to be audible - and the key travels with the
        /// projectile rather than being hardcoded here so a bone caster and a plant spitter do not
        /// land with the same sound.
        /// </param>
        public void Launch(Vector3 direction, float speed, float damage, string impactSfxKey = null)
        {
            _velocity = direction * speed;
            _damage = damage;
            _life = lifeTime;
            _impactSfxKey = impactSfxKey;
        }

        public override void OnSpawnedFromPool()
        {
            // A reused shot must not drag the previous flight's trail across the map.
            _systems ??= GetComponentsInChildren<ParticleSystem>(true);
            _trails ??= GetComponentsInChildren<TrailRenderer>(true);
            foreach (var t in _trails) t.Clear();
            foreach (var ps in _systems) { ps.Clear(true); ps.Play(true); }
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Burst(impactScale * 0.5f);
                ReturnToPool();
                return;
            }

            transform.position += _velocity * Time.deltaTime;

            var player = PlayerMovement.Instance;
            if (player == null) return;

            // Flat distance: the shot flies at chest height while the player's pivot is at the feet, so
            // a 3D check against the pivot could never come within the hit radius.
            Vector3 d = transform.position - player.transform.position; d.y = 0f;
            if (d.sqrMagnitude <= (hitRadius + PlayerBodyRadius) * (hitRadius + PlayerBodyRadius))
            {
                if (!string.IsNullOrEmpty(_impactSfxKey))
                    Bill.Audio?.PlayCue(_impactSfxKey, transform.position, SfxPriority.Medium, 0.66f);
                Burst(impactScale);
                player.GetComponentInParent<IDamageable>()?.TakeDamage(_damage);
                ReturnToPool();
            }
        }

        private void Burst(float scale)
        {
            if (impactFx != null) FxPool.Play(impactFx, transform.position, Quaternion.identity, scale);
        }
    }
}
