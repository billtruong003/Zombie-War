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
    /// <summary>
    /// M7.3b — <c>Magnet</c> added. Collecting one sweeps every pickup currently on the ground to the
    /// player. Blanket auto-collect on wave clear stays removed: loot is walked to by default, and the
    /// magnet is the reward that makes a big sweep feel earned rather than automatic.
    /// </summary>
    // Values are serialized in pickup prefabs: never renumber (2 was the retired bomb pickup;
    // the A8 Bomb item is a new value on purpose, so an old prefab can never turn into it).
    // 7 = Xp (05/10): run XP dropped as blue orbs, collected like coins used to be.
    public enum PickupEffect { Currency = 0, Health = 1, Magnet = 3, Chest = 4, Bomb = 5, Freeze = 6, Xp = 7 }

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
        [Tooltip("Seconds before an uncollected pickup leaves (0 = never). Mechanic items expire so " +
                 "one the player ignores does not block the next (only one is ever on the map).")]
        [SerializeField] private float lifetime = 0f;
        [Tooltip("It blinks for this long before it leaves.")]
        [SerializeField] private float blinkSeconds = 5f;

        private Renderer[] _renderers;
        private bool _hidden;

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
        public int Amount => _amount;
        public PickupEffect Effect => effect;
        public string PoolKey => _poolKey;
        /// <summary>Currency and XP orbs carry a value; items do not.</summary>
        bool CarriesValue => effect == PickupEffect.Currency || effect == PickupEffect.Xp;
        /// <summary>Magnet, Bomb or Freeze Clock (A8): one on the map at a time, never swept by a magnet.</summary>
        public bool IsMechanic => MechanicItems.IsMechanic(effect);

        /// <summary>
        /// Folds another drop's value into this one instead of spawning a new object. Only a resting
        /// currency pickup of the same kind can absorb: one already flying to the player has been
        /// counted as collected-in-flight and must not change value mid-air.
        /// </summary>
        public bool TryAbsorb(PlayerProfile.CurrencyKind kind, int amount)
        {
            if (!CarriesValue || _collected || _flying || kind != _kind || amount <= 0)
                return false;
            _amount += amount;
            return true;
        }

        /// <summary>Called by the spawner right after the pool hands this instance over.</summary>
        public void Init(PlayerProfile.CurrencyKind kind, int amount, string poolKey, Vector3 position)
        {
            _kind = kind;
            // Only currency carries a value. A heal or a magnet reports 0 rather than a phantom coin.
            _amount = CarriesValue ? Mathf.Max(1, amount) : 0;
            _poolKey = poolKey;
            _collected = false;
            _flying = false;
            _speed = 0f;
            _age = 0f;
            _groundPos = position;
            transform.position = position;
            EnsureBeam();
            SetHidden(false);
        }

        // A fake light ray over everything but plain coins (owner 02/10): items read from afar
        // without a real light. One quad per pickup, made the first time it spawns, coloured by
        // what it is; it blinks and hides with the pickup.
        static Mesh _beamMesh;
        static Material _beamMaterial;
        static bool _beamLoaded;
        static MaterialPropertyBlock _beamBlock;
        static readonly int BeamColorId = Shader.PropertyToID("_BeamColor");
        Renderer _beam;

        void EnsureBeam()
        {
            bool wants = (effect != PickupEffect.Currency && effect != PickupEffect.Xp) || _kind == PlayerProfile.CurrencyKind.Gem;
            if (!wants) { if (_beam != null) _beam.gameObject.SetActive(false); return; }
            if (!_beamLoaded) { _beamMaterial = Resources.Load<Material>("FX/M_PickupBeam"); _beamLoaded = true; }
            if (_beamMaterial == null) return;
            if (_beam == null)
            {
                if (_beamMesh == null)
                {
                    _beamMesh = new Mesh { name = "PickupBeam" };
                    _beamMesh.SetVertices(new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f) });
                    _beamMesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                    _beamMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                    _beamMesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 1.5f, 1.5f));
                }
                var go = new GameObject("beam");
                go.transform.SetParent(transform, false);
                // 1.1 m wide, 3.2 m tall in the world whatever the pickup's own scale.
                var s = transform.lossyScale;
                go.transform.localScale = new Vector3(1.1f / Mathf.Max(0.01f, s.x), 3.2f / Mathf.Max(0.01f, s.y), 1f);
                go.AddComponent<MeshFilter>().sharedMesh = _beamMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _beamMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _beam = mr;
                _renderers = null;   // re-gathered with the beam, so it blinks and hides too
            }
            _beamBlock ??= new MaterialPropertyBlock();
            _beamBlock.SetColor(BeamColorId, BeamColour());
            _beam.SetPropertyBlock(_beamBlock);
            _beam.gameObject.SetActive(true);
        }

        Color BeamColour() => effect switch
        {
            PickupEffect.Chest => new Color(1.6f, 1.1f, 0.3f, 1f),
            PickupEffect.Health => new Color(0.4f, 1.4f, 0.5f, 1f),
            PickupEffect.Magnet => new Color(0.4f, 0.75f, 1.6f, 1f),
            PickupEffect.Bomb => new Color(1.6f, 0.35f, 0.2f, 1f),
            PickupEffect.Freeze => new Color(0.5f, 1.3f, 1.6f, 1f),
            _ => new Color(1.4f, 0.4f, 1.2f, 1f),   // gem
        };

        private void SetHidden(bool hidden)
        {
            if (_hidden == hidden && _renderers != null) return;
            _hidden = hidden;
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers) if (r != null) r.enabled = !hidden;
        }

        /// <summary>Leaves without paying anything (an ignored item timing out).</summary>
        private void Expire()
        {
            _collected = true;
            SetHidden(false);   // the pool hands it out visible
            if (!string.IsNullOrEmpty(_poolKey) && Bill.Pool != null) Bill.Pool.Return(gameObject);
            else gameObject.SetActive(false);
        }

        const float ChestReach = 1.4f;

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

                if (lifetime > 0f)
                {
                    if (_age >= lifetime) { Expire(); return; }
                    float left = lifetime - _age;
                    // Blink faster as it runs out, so "it is leaving" reads without a timer.
                    SetHidden(left < blinkSeconds && Mathf.Repeat(_age, left < 2f ? 0.16f : 0.3f) < (left < 2f ? 0.07f : 0.1f));
                }

                if (_age < settleTime) return;
                float sqr = (playerPos - transform.position).sqrMagnitude;
                // A7: a chest is walked to, never pulled — no magnet, no sweep. Opening it is a choice
                // moment, and it must not fire the instant a magnet lands in the middle of a fight.
                if (effect == PickupEffect.Chest)
                {
                    Vector3 d = playerPos - _groundPos; d.y = 0f;
                    if (d.sqrMagnitude <= ChestReach * ChestReach) Collect();
                    return;
                }
                // A8: a magnet sweep brings loot, never a mechanic item (a bomb must go off where the
                // player chose to walk, and a magnet must not collect the next magnet).
                bool swept = forceCollect && !IsMechanic;
                if (!swept && sqr > magnetRadius * magnetRadius) return;

                _flying = true;
                SetHidden(false);
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

        /// <summary>05/10 owner: the map is chaos, so every item says what it was over the player's
        /// head. XP orbs and coins stay silent (their sound is enough).</summary>
        void AnnounceCollected()
        {
            string label = effect switch
            {
                PickupEffect.Health => $"+{Mathf.RoundToInt(healAmount)} HP",
                PickupEffect.Magnet => "MAGNET!",
                PickupEffect.Bomb => "BOMB!",
                PickupEffect.Freeze => "FREEZE!",
                PickupEffect.Chest => "CHEST!",
                _ => _kind == PlayerProfile.CurrencyKind.Gem ? $"+{_amount} GEM" : null,
            };
            var player = PlayerMovement.Instance;
            if (label == null || player == null) return;
            DamageNumberSpawner.SpawnLabel(label, BeamColour() * 0.75f + Color.white * 0.25f, player.transform.position + Vector3.up * 2.6f);
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

                case PickupEffect.Magnet:
                    // A travelling pull, not an instant credit: the coins visibly fly in, which is
                    // the entire point of the pickup.
                    MechanicItems.Magnet(transform.position);
                    break;

                case PickupEffect.Bomb:
                    MechanicItems.Bomb(transform.position);
                    break;

                case PickupEffect.Freeze:
                    MechanicItems.Freeze(transform.position);
                    break;

                case PickupEffect.Chest:
                    // The run overlays open it (an evolution, a rank-up or a bonus card).
                    PickupManager.RaiseChestCollected(transform.position);
                    break;

                case PickupEffect.Xp:
                    RunState.Current?.AddXp(_amount);
                    break;

                default:
                    RunState.Current?.AddCurrency(_kind, _amount);
                    break;
            }

            Bill.Events?.Fire(new PickupCollectedEvent(_kind, _amount, effect));
            AnnounceCollected();

            if (!string.IsNullOrEmpty(_poolKey) && Bill.Pool != null) Bill.Pool.Return(gameObject);
            else gameObject.SetActive(false);
        }
    }

    public readonly struct PickupCollectedEvent : IEvent
    {
        public readonly PlayerProfile.CurrencyKind Kind;
        public readonly int Amount;
        public readonly PickupEffect Effect;
        public PickupCollectedEvent(PlayerProfile.CurrencyKind kind, int amount)
            : this(kind, amount, PickupEffect.Currency) { }

        public PickupCollectedEvent(PlayerProfile.CurrencyKind kind, int amount, PickupEffect effect)
        {
            Kind = kind; Amount = amount; Effect = effect;
        }
    }
}
