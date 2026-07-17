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

        private void Awake()
        {
            _current = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            _current = Mathf.Max(0f, _current - amount);
            OnDamaged?.Invoke(amount);

            if (_current <= 0f) OnDeath?.Invoke();
        }

        public void ResetHealth()
        {
            _current = maxHealth;
        }

        // Lets a data-driven owner (e.g. a pooled zombie whose stats come from a ZombieData asset)
        // make that data the source of truth for max HP instead of the inspector value.
        public void Configure(float max)
        {
            maxHealth = max;
            _current = max;
        }
    }
}
