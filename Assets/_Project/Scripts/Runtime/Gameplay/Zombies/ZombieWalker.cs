using UnityEngine;

namespace ZombieWar
{
    // The standard shambling melee zombie - the default enemy. All the interesting behaviour it
    // needs already lives in ZombieBase; it only has to say how it hits.
    public sealed class ZombieWalker : ZombieBase
    {
        protected override void PerformAttack(Transform target) => DealContactDamage(target);
    }
}
