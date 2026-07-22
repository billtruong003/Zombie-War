using System.Collections;
using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// A dropped Coin or Gem. Sits where the enemy died, then flies to the player once they come
    /// within magnet range and banks itself into the run ledger.
    ///
    /// Two rules this type exists to guarantee:
    ///   * collect-once - <see cref="_collected"/> latches, so a pickup cannot pay twice even if the
    ///     magnet and an auto-collect sweep both reach it on the same frame;
    ///   * the profile is never touched. Everything goes into <see cref="RunState"/>, which pays out
    ///     once at the end of the run. A pickup that is never collected is simply lost, which is what
    ///     keeps abandoning a run from banking loose change.
    ///
    /// No collider or trigger: the magnet is a distance check driven by <see cref="PickupManager"/>
    /// on a throttled sweep. With hundreds of coins on the floor that is far cheaper than hundreds
    /// of trigger volumes, and it cannot miss due to fast movement tunnelling.
    /// </summary>
    /// <summary>What collecting this pickup actually does. Authored on the prefab, because the
    /// prefab already knows what it is - the spawner should not have to say.</summary>
    public enum PickupEffect { Currency, Health, Bomb }

    public class Pickup : MonoBehaviour
    {
        [Tooltip("Set per prefab: a health apple is always a health apple.")]
        [SerializeField] private PickupEffect effect = PickupEffect.Currency;
        [Tooltip("Health restored, for the Health effect.")]
        [SerializeField] private float healAmount = 25f;
        [SerializeField] private float magnetSpeed = 9f;
        [SerializeField] private float magnetAcceleration = 22f;
        [Tooltip("Distance at which the pickup is considered banked.")]
        [SerializeField] private float collectDistance = 0.45f;
        [Tooltip("Seconds of bobbing before the pickup can be magneted, so it reads as a drop first.")]
        [SerializeField] private float settleTime = 0.25f;
        [SerializeField] private float spinSpeed = 120f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 3f;

        private PlayerProfile.CurrencyKind _kind;
        private int _amount;
        private bool _collected;
        private bool _flying;
        private float _speed;
        private float _age;
        private Vector3 _groundPos;
        private string _poolKey;

        public PlayerProfile.CurrencyKind Kind => _kind;
        public bool Collected => _collected;

        /// <summary>Called by the spawner right after the pool hands this instance over.</summary>
        public void Init(PlayerProfile.CurrencyKind kind, int amount, string poolKey, Vector3 position)
        {
            _kind = kind;
            _amount = Mathf.Max(1, amount);
            _poolKey = poolKey;
            _collected = false;
            _flying = false;
            _speed = 0f;
            _age = 0f;
            _groundPos = position;
            transform.position = position;
        }

        private void OnEnable() => PickupManager.Register(this);
        private void OnDisable() => PickupManager.Unregister(this);

        /// <summary>Driven by PickupManager, not Update - one manager loop beats N MonoBehaviour
        /// Updates once a wave's worth of coins is on the floor.</summary>
        public void Tick(float dt, Vector3 playerPos, float magnetRadius, bool forceCollect)
        {
            if (_collected) return;

            _age += dt;

            if (!_flying)
            {
                // Idle: bob and spin so it reads as loot rather than scenery.
                float y = _groundPos.y + Mathf.Abs(Mathf.Sin(_age * bobSpeed)) * bobHeight;
                transform.position = new Vector3(_groundPos.x, y, _groundPos.z);
                transform.Rotate(Vector3.up, spinSpeed * dt, Space.World);

                if (_age < settleTime) return;
                float sqr = (playerPos - transform.position).sqrMagnitude;
                if (!forceCollect && sqr > magnetRadius * magnetRadius) return;

                _flying = true;
                _speed = magnetSpeed * 0.35f;
            }

            // Flying: accelerate toward the player so late-arriving coins catch up rather than
            // trailing forever behind a moving target.
            _speed += magnetAcceleration * dt;
            Vector3 target = playerPos + Vector3.up * 0.6f;
            transform.position = Vector3.MoveTowards(transform.position, target, _speed * dt);

            if ((target - transform.position).sqrMagnitude <= collectDistance * collectDistance)
                Collect();
        }

        private void Collect()
        {
            if (_collected) return;
            _collected = true;

            switch (effect)
            {
                case PickupEffect.Health:
                    // Heal through the player's own Health component so it clamps at max and the
                    // HUD's existing damage/heal wiring picks it up.
                    var player = PlayerMovement.Instance;
                    var health = player != null ? player.GetComponentInParent<Health>() : null;
                    health?.Heal(healAmount);
                    break;

                case PickupEffect.Bomb:
                    Bill.Events?.Fire(new BombPickedUpEvent());
                    break;

                default:
                    RunState.Current?.AddCurrency(_kind, _amount);
                    break;
            }

            Bill.Events?.Fire(new PickupCollectedEvent(_kind, _amount));

            if (!string.IsNullOrEmpty(_poolKey) && Bill.Pool != null) Bill.Pool.Return(gameObject);
            else gameObject.SetActive(false);
        }

        /// <summary>Banked without the fly-in. Used when a wave ends and leftovers are swept up.</summary>
        public void CollectImmediate() => Collect();
    }

    /// <summary>The player picked up a spare bomb. BombThrower listens and adds a charge.</summary>
    public readonly struct BombPickedUpEvent : IEvent { }

    public readonly struct PickupCollectedEvent : IEvent
    {
        public readonly PlayerProfile.CurrencyKind Kind;
        public readonly int Amount;
        public PickupCollectedEvent(PlayerProfile.CurrencyKind kind, int amount)
        {
            Kind = kind; Amount = amount;
        }
    }
}
