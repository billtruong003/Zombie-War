using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

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

        private static readonly List<ZombieBase> _zombies = new();
        private float _reevaluateTimer;

        // Single source of truth for "how many zombies are alive right now". A zombie leaves this
        // list the moment it is returned to the pool (OnDisable -> Unregister), so the wave director
        // can poll this to know when a wave is cleared - no separate bookkeeping needed.
        public static int AliveCount => _zombies.Count;

        public static void Register(ZombieBase zombie)
        {
            if (!_zombies.Contains(zombie)) _zombies.Add(zombie);
        }

        public static void Unregister(ZombieBase zombie)
        {
            _zombies.Remove(zombie);
        }

        private void OnEnable()
        {
            var bus = Bill.Events;
            if (bus == null) return;
            bus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            bus.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnDisable()
        {
            var bus = Bill.Events;
            if (bus == null) return;
            bus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            bus.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        // The corpse is not a target: everyone drops to Idle the moment the player dies.
        // (PlayerMovement disables itself on death -> Instance goes null -> Update() above
        // stops ticking cheap movement/tiers too.)
        private void OnPlayerDied(PlayerDiedEvent e)
        {
            for (int i = 0; i < _zombies.Count; i++)
                _zombies[i].OnPlayerLost();
        }

        // Once the lose screen takes over, the field is cleared - every zombie goes straight
        // back to the pool (Return -> OnDisable -> Unregister prunes the list as we walk it).
        private void OnGameOver(GameOverEvent e)
        {
            for (int i = _zombies.Count - 1; i >= 0; i--)
            {
                var zombie = _zombies[i];
                if (zombie != null) Bill.Pool?.Return(zombie.gameObject);
            }
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
