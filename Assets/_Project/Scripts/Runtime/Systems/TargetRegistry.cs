using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    // Decouples the player's auto-aim from the zombie implementation - anything targetable
    // (zombies, later maybe destructible props) registers itself here instead of being
    // discovered via tag/layer lookups or per-frame Physics.OverlapSphere allocations.
    public static class TargetRegistry
    {
        private static readonly List<ITargetable> _targets = new();

        public static void Register(ITargetable target)
        {
            if (!_targets.Contains(target)) _targets.Add(target);
        }

        public static void Unregister(ITargetable target)
        {
            _targets.Remove(target);
        }

        public static ITargetable FindNearest(Vector3 from, float maxRange)
        {
            ITargetable nearest = null;
            float nearestSqrDistance = maxRange * maxRange;

            for (int i = 0; i < _targets.Count; i++)
            {
                var candidate = _targets[i];
                if (!candidate.IsTargetable) continue;

                float sqrDistance = (candidate.Transform.position - from).sqrMagnitude;
                if (sqrDistance <= nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
