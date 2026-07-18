using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // Grenade-style throwable: launched with an arc by BombThrower, tumbles + bounces via its
    // Rigidbody + bouncy PhysicMaterial, draws a flight trail, then detonates on the fuse.
    [RequireComponent(typeof(Rigidbody))]
    public class Bomb : PooledObject
    {
        [SerializeField] private float fuseTime = 1.5f;
        [SerializeField] private float explosionRadius = 4f;
        [SerializeField] private float damage = 80f;
        [SerializeField] private LayerMask damageMask = ~0;
        [SerializeField] private ParticleSystem explosionPrefab;
        [SerializeField] private string explosionSfxKey = "bomb_explode";
        [SerializeField] private float cameraShakeAmount = 0.6f;
        [Tooltip("Flight trail cleared on each spawn so pooled reuse doesn't streak across the map.")]
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private float tumbleSpin = 6f;

        private Rigidbody _rb;
        private float _fuseTimer;
        private bool _exploded;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (trail == null) trail = GetComponentInChildren<TrailRenderer>();
            _fuseTimer = fuseTime;
        }

        // Pooled reuse skips Awake, so reset fuse/physics/trail state every time we're spawned.
        public override void OnSpawnedFromPool()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _fuseTimer = fuseTime;
            _exploded = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            if (trail != null) trail.Clear();
        }

        // BombThrower calls this right after spawn to give the bomb its arc.
        public void Launch(Vector3 velocity)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = Random.insideUnitSphere * tumbleSpin;
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

            if (trail != null) trail.Clear();

            if (Bill.Pool != null) ReturnToPool();
            else Destroy(gameObject);
        }
    }
}
