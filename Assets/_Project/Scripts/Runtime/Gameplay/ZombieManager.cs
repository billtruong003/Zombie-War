using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    // Central authority for the 3-tier distance gating (see GAMEPLAY_DESIGN.md mục 4) - zombies
    // never decide their own tier or run their own cheap-movement Update(); this single component
    // re-evaluates everyone on a throttled interval instead of N zombies checking distance every frame.
    public class ZombieManager : MonoBehaviour
    {
        [SerializeField] private float fullTierRadius = 20f;
        [SerializeField] private float cheapTierRadius = 60f;
        [SerializeField] private float tierReevaluateInterval = 0.25f;

        private static readonly List<ZombieAI> _zombies = new();
        private float _reevaluateTimer;

        public static void Register(ZombieAI zombie)
        {
            if (!_zombies.Contains(zombie)) _zombies.Add(zombie);
        }

        public static void Unregister(ZombieAI zombie)
        {
            _zombies.Remove(zombie);
        }

        private void Update()
        {
            var player = PlayerMovement.Instance;
            if (player == null) return;

            Vector3 playerPosition = player.transform.position;
            TickCheapMovement(playerPosition);

            _reevaluateTimer -= Time.deltaTime;
            if (_reevaluateTimer > 0f) return;
            _reevaluateTimer = tierReevaluateInterval;

            ReevaluateTiers(playerPosition);
        }

        private void TickCheapMovement(Vector3 playerPosition)
        {
            for (int i = 0; i < _zombies.Count; i++)
            {
                var zombie = _zombies[i];
                if (zombie.Tier == ZombieTier.Cheap) zombie.CheapTick(playerPosition);
            }
        }

        private void ReevaluateTiers(Vector3 playerPosition)
        {
            for (int i = 0; i < _zombies.Count; i++)
            {
                var zombie = _zombies[i];
                float distance = Vector3.Distance(zombie.transform.position, playerPosition);

                if (distance <= fullTierRadius) zombie.SetTier(ZombieTier.Full);
                else if (distance <= cheapTierRadius) zombie.SetTier(ZombieTier.Cheap);
                else zombie.SetTier(ZombieTier.Inactive);
            }
        }
    }
}
