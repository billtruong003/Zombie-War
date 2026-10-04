using System;
using UnityEngine;

namespace ZombieWar
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;

        // Player only (05/10, genre rule 6: damage is attrition, not burst; rule 5: mistakes heal).
        [Header("Player protection")]
        [Tooltip("Seconds after a hit during which further hits are ignored.")]
        [SerializeField] private float hitGraceSeconds = 0.4f;
        [Tooltip("Most of max health the player can lose in any one second, however many hit.")]
        [SerializeField, Range(0.05f, 1f)] private float maxLossPerSecond = 0.25f;
        [Tooltip("Health per second regained after regenDelay seconds without a hit.")]
        [SerializeField] private float regenPerSecond = 2f;
        [SerializeField] private float regenDelay = 3f;
        float _windowStart = -999f, _windowLoss, _lastHitAt = -999f;

        private float _current;

        public float Current => _current;
        public float Max => maxHealth;
        public bool IsDead => _current <= 0f;

        public event Action<float> OnDamaged;
        /// Every change of current or max health, whatever caused it (hit, heal, revive, max-up,
        /// reset). The one signal a display should follow; OnDamaged/OnHealed are for reactions.
        public event Action<float, float> OnChanged;
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
            enabled = _isPlayer;   // only the player regenerates: no Update on every pooled enemy
        }

        /// <summary>
        /// M10 revive: asked once when a hit would kill the player. Returning true holds the player
        /// at the edge of death (the revive offer is on screen); the offer then calls
        /// <see cref="Revive"/> or <see cref="ConfirmDeath"/>. Null or false = die as before.
        /// </summary>
        public Func<bool> ReviveGate;
        public bool IsHeld { get; private set; }
        float _invulnerableUntil;

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || IsHeld) return;
            if (_isPlayer && Time.time < _invulnerableUntil) return;

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

            if (_isPlayer)
            {
                amount = CapLoss(amount, Time.time);
                if (amount <= 0f) return;
                _invulnerableUntil = Time.time + hitGraceSeconds;
                _lastHitAt = Time.time;
            }

            SetCurrent(Mathf.Max(0f, _current - amount));

            // A3 Guardian Angel: once per run a fatal hit heals instead, before any revive offer.
            if (_current <= 0f && _isPlayer)
            {
                var skills = ZombieWar.Skills.SkillRuntime.Active;
                if (skills != null && skills.TryGuardianAngel(out float heal))
                {
                    SetCurrent(Mathf.Max(1f, maxHealth * heal));
                    _invulnerableUntil = Time.time + 1f;
                    ZombieWar.Skills.SkillCombatDriver.Instance?.PlayGuardianAngel();
                    OnHealed?.Invoke(_current);
                    return;
                }
            }

            if (_current <= 0f && _isPlayer && ReviveGate != null && ReviveGate())
            {
                SetCurrent(0.01f);   // alive but held; nothing hits during the offer (time is frozen)
                IsHeld = true;
                OnDamaged?.Invoke(amount);
                return;
            }
            OnDamaged?.Invoke(amount);

            if (_current <= 0f) OnDeath?.Invoke();
        }

        /// <summary>Ends a held death by getting back up: full health and a short grace period.</summary>
        public void Revive(float graceSeconds = 2.5f)
        {
            if (!IsHeld) return;
            IsHeld = false;
            float healed = maxHealth - _current;
            SetCurrent(maxHealth);
            _invulnerableUntil = Time.time + graceSeconds;
            OnHealed?.Invoke(healed);
        }

        /// <summary>Ends a held death by dying (offer declined or timed out).</summary>
        public void ConfirmDeath()
        {
            if (!IsHeld) return;
            IsHeld = false;
            SetCurrent(0f);
            OnDeath?.Invoke();
        }

        public void ResetHealth() => SetCurrent(maxHealth);

        /// <summary>What is left of a hit once the one-second loss budget is applied.</summary>
        float CapLoss(float amount, float now)
        {
            if (now - _windowStart >= 1f) { _windowStart = now; _windowLoss = 0f; }
            float allowed = Mathf.Max(0f, maxHealth * maxLossPerSecond - _windowLoss);
            amount = Mathf.Min(amount, allowed);
            _windowLoss += amount;
            return amount;
        }

        private void Update()
        {
            if (!_isPlayer || IsDead || IsHeld || regenPerSecond <= 0f || _current >= maxHealth) return;
            if (Time.time - _lastHitAt < regenDelay) return;
            SetCurrent(Mathf.Min(maxHealth, _current + regenPerSecond * Time.deltaTime));
        }

        /// <summary>Restores health, clamped at max. Refuses to revive something already dead -
        /// a health pickup must not undo a death that has already resolved.</summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            float before = _current;
            SetCurrent(Mathf.Min(maxHealth, _current + amount));
            OnHealed?.Invoke(_current - before);
        }

        public event Action<float> OnHealed;

        // Lets a data-driven owner (e.g. a pooled zombie whose stats come from a ZombieData asset)
        // make that data the source of truth for max HP instead of the inspector value.
        public void Configure(float max)
        {
            maxHealth = max;
            SetCurrent(max);
        }

        /// <summary>Raises max health by a multiplier and grants the added headroom as current
        /// health, so picking a Max Health perk is felt immediately instead of only mattering after
        /// the next full heal. Run-scoped: pooled/respawned owners re-Configure and wipe it.</summary>
        public void IncreaseMax(float multiplier)
        {
            if (IsDead || multiplier <= 1f) return;
            float added = maxHealth * (multiplier - 1f);
            maxHealth += added;
            SetCurrent(_current + added);
        }

        // The single write path for current health, so OnChanged can never be skipped.
        private void SetCurrent(float value)
        {
            _current = value;
            OnChanged?.Invoke(_current, maxHealth);
        }
    }
}
