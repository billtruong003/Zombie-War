using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    public class Bomb : PooledObject
    {
        [SerializeField] private float fuseTime = 1.5f;
        [SerializeField] private float explosionRadius = 4f;
        [SerializeField] private float damage = 80f;
        [SerializeField] private LayerMask damageMask = ~0;
        [SerializeField] private ParticleSystem explosionPrefab;
        [SerializeField] private string explosionSfxKey = "bomb_explode";
        [SerializeField] private float cameraShakeAmount = 0.6f;

        private float _fuseTimer;
        private bool _exploded;

        private void Awake()
        {
            _fuseTimer = fuseTime;
        }

        // Pooled reuse skips Awake, so reset fuse/exploded state every time we're spawned.
        public override void OnSpawnedFromPool()
        {
            _fuseTimer = fuseTime;
            _exploded = false;
        }

        private void Update()
        {
            if (_exploded) return;

            _fuseTimer -= Time.deltaTime;
            if (_fuseTimer <= 0f) Explode();
        }

        private void Explode()
        {
            _exploded = true;

            var hits = Physics.OverlapSphere(transform.position, explosionRadius, damageMask);
            foreach (var hit in hits)
                hit.GetComponentInParent<IDamageable>()?.TakeDamage(damage);

            if (explosionPrefab != null)
                FxPool.Play(explosionPrefab, transform.position, Quaternion.identity);

            Bill.Audio?.Play(explosionSfxKey, transform.position);

            if (Camera.main != null && Camera.main.TryGetComponent(out CameraFollow cameraFollow))
                cameraFollow.Shake(cameraShakeAmount);

            if (Bill.Pool != null) ReturnToPool();
            else Destroy(gameObject);
        }
    }
}
