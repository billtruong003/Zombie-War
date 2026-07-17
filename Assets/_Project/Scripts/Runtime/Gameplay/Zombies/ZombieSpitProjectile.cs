using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // Straight-flying spit fired by ZombieRanged. In a top-down shooter the only thing a zombie's
    // projectile can hit is the player, so it just tracks the player's distance instead of pulling
    // in physics layers/collision matrices - simpler and no scene layer setup required.
    public sealed class ZombieSpitProjectile : PooledObject
    {
        [SerializeField] private float lifeTime = 4f;
        [SerializeField] private float hitRadius = 0.4f;

        private Vector3 _velocity;
        private float _damage;
        private float _life;

        public void Launch(Vector3 direction, float speed, float damage)
        {
            _velocity = direction * speed;
            _damage = damage;
            _life = lifeTime;
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                ReturnToPool();
                return;
            }

            transform.position += _velocity * Time.deltaTime;

            var player = PlayerMovement.Instance;
            if (player == null) return;

            if (Vector3.Distance(transform.position, player.transform.position) <= hitRadius)
            {
                player.GetComponentInParent<IDamageable>()?.TakeDamage(_damage);
                ReturnToPool();
            }
        }
    }
}
