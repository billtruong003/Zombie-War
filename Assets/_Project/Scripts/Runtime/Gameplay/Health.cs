using System;
using UnityEngine;

namespace ZombieWar
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;

        private float _current;

        public float Current => _current;
        public float Max => maxHealth;
        public bool IsDead => _current <= 0f;

        public event Action<float> OnDamaged;
        public event Action OnDeath;
        /// Raised when Kinetic Shield ate an incoming hit, so the HUD can show it.
        public event Action OnDamageAbsorbed;

        // Only the player's Health may spend a Kinetic Shield charge. Resolved once in Awake rather
        // than per hit.
        private bool _isPlayer;

        private void Awake()
        {
            _current = maxHealth;
            _isPlayer = GetComponent<PlayerMovement>() != null;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            // M7.2b — Kinetic Shield. PLAYER ONLY: an enemy must never spend the player's charge, so
            // this is gated on the owner actually being the player rather than on "any Health".
            if (_isPlayer)
            {
                var skills = ZombieWar.Skills.SkillRuntime.Active;
                if (skills != null && skills.TryAbsorbDamage())
                {
                    // Make it legible. A hit that silently vanishes reads as a bug, not as a card.
                    ZombieWar.Skills.SkillCombatDriver.Instance?.PlayShieldBreak();
                    OnDamageAbsorbed?.Invoke();
                    return;
                }
            }

            _current = Mathf.Max(0f, _current - amount);
            OnDamaged?.Invoke(amount);

            if (_current <= 0f) OnDeath?.Invoke();
        }

        public void ResetHealth()
        {
            _current = maxHealth;
        }

        /// <summary>Restores health, clamped at max. Refuses to revive something already dead -
        /// a health pickup must not undo a death that has already resolved.</summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            _current = Mathf.Min(maxHealth, _current + amount);
            OnHealed?.Invoke(amount);
        }

        public event Action<float> OnHealed;

        // Lets a data-driven owner (e.g. a pooled zombie whose stats come from a ZombieData asset)
        // make that data the source of truth for max HP instead of the inspector value.
        public void Configure(float max)
        {
            maxHealth = max;
            _current = max;
        }

        /// <summary>Raises max health by a multiplier and grants the added headroom as current
        /// health, so picking a Max Health perk is felt immediately instead of only mattering after
        /// the next full heal. Run-scoped: pooled/respawned owners re-Configure and wipe it.</summary>
        public void IncreaseMax(float multiplier)
        {
            if (IsDead || multiplier <= 1f) return;
            float added = maxHealth * (multiplier - 1f);
            maxHealth += added;
            _current += added;
        }
    }
}
