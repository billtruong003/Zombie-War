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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _targets.Clear();

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
            float maxSqr = maxRange * maxRange;
            float nearestScore = float.MaxValue;

            for (int i = 0; i < _targets.Count; i++)
            {
                var candidate = _targets[i];
                if (!candidate.IsTargetable) continue;

                float sqrDistance = (candidate.Transform.position - from).sqrMagnitude;
                if (sqrDistance > maxSqr) continue;
                float bias = candidate.AimBias;
                float score = bias > 0f ? Mathf.Max(0f, Mathf.Sqrt(sqrDistance) - bias) : Mathf.Sqrt(sqrDistance);
                if (score <= nearestScore)
                {
                    nearestScore = score;
                    nearest = candidate;
                }
            }

            return nearest;
        }

    }
}
