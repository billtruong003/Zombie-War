using System.Collections;
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

        [Header("Death sequence")]
        [Tooltip("Animator state crossfaded on death. Must exist on the controller's base layer.")]
        [SerializeField] private string deathStateName = "Death";
        [Tooltip("One-shot FX spawned at the player's feet when they die (FxPool).")]
        [SerializeField] private ParticleSystem deathFx;
        [Tooltip("Seconds between the death anim starting and the lose screen appearing.")]
        [SerializeField] private float loseScreenDelay = 2f;

        private Health _health;
        private Animator _animator;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _animator = GetComponentInChildren<Animator>(true);
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
            DisableControl();
            PlayDeathVisuals();

            // Fired immediately: zombies stop hunting, waves stop spawning, HP bar zeroes.
            Bill.Events?.Fire(new PlayerDiedEvent());

            // The lose screen only appears after the corpse has had its moment.
            StartCoroutine(GameOverAfterDelay());
        }

        private void DisableControl()
        {
            if (disableOnDeath != null)
            {
                foreach (var behaviour in disableOnDeath)
                    if (behaviour != null) behaviour.enabled = false;
            }

            // Safety net: runtime-built rigs often ship this list empty, which used to leave
            // the corpse fully playable (running around post-mortem). Kill the core inputs.
            foreach (var m in GetComponentsInChildren<PlayerMovement>()) m.enabled = false;
            foreach (var w in GetComponentsInChildren<Weapon>()) w.enabled = false;
            foreach (var b in GetComponentsInChildren<BombThrower>()) b.enabled = false;
        }

        private void PlayDeathVisuals()
        {
            if (_animator != null)
            {
                // Drop the aim/upper-body override layers so they can't fight the death pose.
                for (int i = 1; i < _animator.layerCount; i++)
                    _animator.SetLayerWeight(i, 0f);
                _animator.CrossFadeInFixedTime(deathStateName, 0.15f, 0);
            }

            if (deathFx != null)
                FxPool.Play(deathFx, transform.position, Quaternion.identity);
        }

        private IEnumerator GameOverAfterDelay()
        {
            yield return new WaitForSeconds(loseScreenDelay);

            Bill.Events?.Fire(new GameOverEvent());

            // Only transition if the bootstrap actually registered the state, so a scene
            // played in isolation (no bootstrap) still fires the event without log spam.
            if (Bill.State != null && Bill.State.GetState<GameOverState>() != null)
                Bill.State.GoTo<GameOverState>();
        }
    }
}
