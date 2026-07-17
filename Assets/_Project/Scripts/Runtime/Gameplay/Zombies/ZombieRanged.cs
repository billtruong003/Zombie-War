using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // A spitter: holds its distance and lobs a pooled projectile instead of closing to melee.
    // It engages from far out (EngageRange) and simply stands and fires - body-blocking is left to
    // the melee horde around it, which keeps the behaviour readable and dependency-light.
    public sealed class ZombieRanged : ZombieBase
    {
        [SerializeField] private float fireRange = 12f;
        [SerializeField] private string projectilePoolKey = "ZombieSpit";
        [SerializeField] private Transform muzzle;
        [SerializeField] private float projectileSpeed = 14f;

        protected override float EngageRange => fireRange;

        protected override void PerformAttack(Transform target)
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            Vector3 direction = FlattenY(target.position - origin).normalized;

            var projectile = Bill.Pool?.Spawn<ZombieSpitProjectile>(
                projectilePoolKey, origin, Quaternion.LookRotation(direction));
            projectile?.Launch(direction, projectileSpeed, Data.damage);
        }
    }
}
