using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// <summary>
    /// Owns the player's death flow. Zombies already route contact/projectile damage to the
    /// player's <see cref="Health"/> (an <see cref="IDamageable"/>); this glue turns Health's
    /// damage/death callbacks into decoupled Bill.Events + a GameStateMachine transition, and
    /// switches off the player's input behaviours on death. Nothing here talks to the HUD
    /// directly - the HUD subscribes to the events.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("Behaviours switched off when the player dies (movement, weapon auto-fire, bomb throw).")]
        [SerializeField] private Behaviour[] disableOnDeath;

        private Health _health;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.OnDamaged += HandleDamaged;
            _health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            _health.OnDamaged -= HandleDamaged;
            _health.OnDeath -= HandleDeath;
        }

        private void HandleDamaged(float amount)
        {
            Bill.Events?.Fire(new PlayerDamagedEvent(amount, _health.Current, _health.Max));
        }

        private void HandleDeath()
        {
            if (disableOnDeath != null)
            {
                foreach (var behaviour in disableOnDeath)
                    if (behaviour != null) behaviour.enabled = false;
            }

            Bill.Events?.Fire(new PlayerDiedEvent());

            // Only transition if the bootstrap actually registered the state, so a scene
            // played in isolation (no bootstrap) still fires the event without log spam.
            if (Bill.State != null && Bill.State.GetState<GameOverState>() != null)
                Bill.State.GoTo<GameOverState>();
        }
    }
}
