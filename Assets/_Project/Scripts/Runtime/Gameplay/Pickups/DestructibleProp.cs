using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>What a prop does when it is destroyed.</summary>
    public enum PropKind
    {
        /// <summary>Breaks open and scatters loot.</summary>
        LootCrate,
        /// <summary>Detonates, damaging everything nearby - including the player.</summary>
        ExplosiveBarrel,
    }

    /// <summary>
    /// A shootable piece of scenery: a crate that pops loot, or a fuel barrel that explodes.
    ///
    /// Implements <see cref="IDamageable"/> so it takes bullets through exactly the same path enemies
    /// do - no special-case weapon code, and it works with pierce/explosion/chain damage for free.
    ///
    /// The explosion deliberately damages the PLAYER too. A barrel that only ever helps is scenery;
    /// one that can kill you is a decision, and it gives the arena something to play around.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DestructibleProp : MonoBehaviour, IDamageable
    {
        [SerializeField] private PropKind kind = PropKind.LootCrate;
        [SerializeField] private float maxHealth = 30f;

        [Header("Loot crate")]
        [Tooltip("How many pickups burst out. Contents are rolled per drop from the table below.")]
        [SerializeField] private int dropCount = 3;
        [Range(0f, 1f)] [SerializeField] private float coinChance = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float gemChance = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float healthChance = 0.18f;
        [Range(0f, 1f)] [SerializeField] private float bombChance = 0.10f;
        [SerializeField] private int coinPerDrop = 3;

        [Header("Explosive barrel")]
        [SerializeField] private float explosionRadius = 4f;
        [SerializeField] private float explosionDamage = 60f;
        [Tooltip("Barrels chain-detonate each other, which is most of the fun of them.")]
        [SerializeField] private float chainDelay = 0.12f;
        [SerializeField] private string explosionVfxKey = "";

        [Header("Pool keys")]
        [SerializeField] private string coinPoolKey = "pickup_coin";
        [SerializeField] private string gemPoolKey = "pickup_gem";
        [SerializeField] private string healthPoolKey = "pickup_health";
        [SerializeField] private string bombPoolKey = "pickup_bomb";

        private float _health;
        private bool _destroyed;

        public PropKind Kind => kind;

        private void OnEnable()
        {
            _health = maxHealth;
            _destroyed = false;
        }

        public void TakeDamage(float amount)
        {
            if (_destroyed || amount <= 0f) return;

            _health -= amount;
            if (_health > 0f) return;

            Break();
        }

        private void Break()
        {
            if (_destroyed) return;
            _destroyed = true;

            if (kind == PropKind.ExplosiveBarrel) Explode();
            else DropLoot();

            // Scenery is authored into the scene, not pooled, so it is simply switched off. Leaving
            // the GameObject alive keeps any chain-reaction coroutine on a sibling valid.
            gameObject.SetActive(false);
        }

        private void DropLoot()
        {
            for (int i = 0; i < dropCount; i++)
            {
                float roll = Random.value;
                if (roll < gemChance)
                    SpawnPickup(gemPoolKey, PlayerProfile.CurrencyKind.Gem, 1);
                else if (roll < gemChance + bombChance)
                    SpawnPickup(bombPoolKey, PlayerProfile.CurrencyKind.Coin, 0);
                else if (roll < gemChance + bombChance + healthChance)
                    SpawnPickup(healthPoolKey, PlayerProfile.CurrencyKind.Coin, 0);
                else if (roll < gemChance + bombChance + healthChance + coinChance)
                    SpawnPickup(coinPoolKey, PlayerProfile.CurrencyKind.Coin, coinPerDrop);
            }
        }

        private void SpawnPickup(string key, PlayerProfile.CurrencyKind kind, int amount)
        {
            if (string.IsNullOrEmpty(key) || Bill.Pool == null) return;

            Vector2 scatter = Random.insideUnitCircle * 0.7f;
            Vector3 pos = transform.position + new Vector3(scatter.x, 0.4f, scatter.y);

            var go = Bill.Pool.Spawn(key, pos, Quaternion.identity);
            var pickup = go != null ? go.GetComponent<Pickup>() : null;
            pickup?.Init(kind, Mathf.Max(1, amount), key, pos);
        }

        private void Explode()
        {
            if (!string.IsNullOrEmpty(explosionVfxKey))
                Bill.Pool?.Spawn(explosionVfxKey, transform.position, Quaternion.identity);

            // OverlapSphere rather than a distance sweep over registries: this has to catch enemies,
            // the player AND other barrels, which live in different systems but share colliders.
            var hits = Physics.OverlapSphere(transform.position, explosionRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                var other = hits[i];
                if (other.transform == transform) continue;

                // Chain-detonate neighbouring barrels on a short delay so it reads as a chain
                // rather than one simultaneous blast.
                var prop = other.GetComponentInParent<DestructibleProp>();
                if (prop != null && prop != this)
                {
                    if (prop.kind == PropKind.ExplosiveBarrel && !prop._destroyed)
                        prop.Invoke(nameof(Break), chainDelay);
                    continue;
                }

                var damageable = other.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                // Linear falloff so hugging the barrel is meaningfully worse than clipping its edge.
                float distance = Vector3.Distance(transform.position, other.transform.position);
                float falloff = Mathf.Clamp01(1f - distance / explosionRadius);
                damageable.TakeDamage(explosionDamage * falloff);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (kind != PropKind.ExplosiveBarrel) return;
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
