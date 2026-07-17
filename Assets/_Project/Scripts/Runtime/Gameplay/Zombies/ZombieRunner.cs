using UnityEngine;

namespace ZombieWar
{
    // A fast zombie that pounces: once inside lunge range it sprints, so it closes the last few
    // metres far quicker than it wanders the distance. Still a melee attacker.
    public sealed class ZombieRunner : ZombieBase
    {
        [SerializeField] private float lungeRange = 6f;
        [SerializeField] private float lungeSpeedMultiplier = 2.2f;

        protected override void Chase(Transform target)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            Agent.speed = distance <= lungeRange
                ? Data.moveSpeed * lungeSpeedMultiplier
                : Data.moveSpeed;
            base.Chase(target);
        }

        protected override void PerformAttack(Transform target) => DealContactDamage(target);
    }
}
