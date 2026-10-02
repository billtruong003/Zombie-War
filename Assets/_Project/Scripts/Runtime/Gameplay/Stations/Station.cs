using TMPro;
using UnityEngine;

namespace ZombieWar.Stations
{
    /// <summary>
    /// M7.3 — one live station. The object is disposable; the FACTS live in
    /// <see cref="StationRegistry"/> keyed by anchor id, so recycling a chunk cannot reset an
    /// objective, duplicate loot or respawn a destroyed station.
    ///
    /// Contracts, per type:
    ///
    /// <b>Signal Relay</b> — hold a compact zone while pressure continues. Leaving pauses and BLEEDS
    /// progress rather than zeroing it, so a player forced out by a horde is not punished twice.
    /// Reward: a 1-of-3 card offer. Completed once; never repeats.
    ///
    /// <b>Supply Cache</b> — stand in the ring to confirm, pay Coin, get a 1-of-3 card offer. The price
    /// shows above the cache and rises with every purchase this run, so it is a real choice: power now
    /// versus Coin that a death would bank at only 25 %. Too poor to pay, the ring turns red.
    /// Repeatable on a cooldown, so a route can be re-run.
    ///
    /// <b>Boss Beacon</b> — opt in by entering. Spawns one existing VAT boss that chases. Claims the
    /// single "major encounter" slot, so two bosses can never run at once. No free chest: the reward
    /// only pays when the boss dies.
    /// </summary>
    [DisallowMultipleComponent]
    public class Station : MonoBehaviour
    {
        [Header("Timings and prices (TUNING)")]
        [SerializeField] private float relayHoldSeconds = 12f;
        [Tooltip("Seconds standing in the cache ring before it charges - a confirmation, so walking " +
                 "through a cache never spends Coin by accident.")]
        [SerializeField] private float cacheConfirmSeconds = 1.5f;
        [SerializeField] private int cachePriceCoin = 60;
        [Tooltip("Price increase per cache bought this run.")]
        [SerializeField] private int cachePriceStep = 40;
        [SerializeField] private float cacheCooldownSeconds = 90f;
        [Tooltip("Progress lost per second while the player is outside the ring. Lower than the gain " +
                 "rate on purpose: stepping out to survive must not erase the attempt.")]
        [SerializeField] private float relayBleedPerSecond = 0.35f;
        [Tooltip("Visible ramp before a Boss Beacon commits, so opting in is a readable decision.")]
        [SerializeField] private float beaconArmSeconds = 2.5f;
        [Tooltip("A9: seconds standing at a Supply Drop to open it (free, one-shot).")]
        [SerializeField] private float supplyOpenSeconds = 1.5f;
        [Tooltip("A9: seconds standing in a Heal Zone before it switches on.")]
        [SerializeField] private float healChargeSeconds = 1.5f;
        [Tooltip("A9: how long the heal field runs once on.")]
        [SerializeField] private float healSeconds = 8f;
        [Tooltip("A9: share of max health healed per second while standing in the field.")]
        [SerializeField] private float healPerSecond = 0.05f;
        [SerializeField] private float healCooldownSeconds = 75f;

        public StationAnchors.Anchor Anchor { get; private set; }
        public WorldSignal Signal { get; private set; }

        float _progressSeconds;
        bool _finished;
        bool _bossAttempted;
        float _bossRetryAt;

        TextMeshPro _priceLabel;
        float _healUntil, _healTickAt;
        bool _healing;

        public bool Finished => _finished;

        public int CachePrice => cachePriceCoin + cachePriceStep * StationDirector.CachePurchasesThisRun;
        public float Progress01 => Signal != null ? Signal.Progress01 : 0f;

        public void Bind(StationAnchors.Anchor anchor, WorldSignal signal)
        {
            Anchor = anchor;
            Signal = signal;
            _progressSeconds = 0f;
            _finished = false;

            float radius = RadiusFor(anchor.kind);
            signal.Configure(anchor.kind, radius);
            if (!TryGetComponent(out ToonPointLight glow)) glow = gameObject.AddComponent<ToonPointLight>();
            glow.colour = LightColour(anchor.kind);
            glow.range = Mathf.Max(5f, radius * 1.4f);
            glow.intensity = 0.9f;
            glow.flicker = 0.08f;
            glow.priority = 1f;   // the station the player is going for always gets its light
            glow.offset = Vector3.up * 1.2f;
            if (anchor.kind == StationKind.SupplyCache) EnsurePriceLabel();

            // Rebuild in the state the LEDGER remembers, not as a fresh station.
            var status = StationRegistry.StatusOf(anchor.id, Time.time);
            switch (status)
            {
                case StationRegistry.Status.Active:
                    // A beacon whose boss is already out. Walking back in must say "this one is
                    // spent" rather than silently doing nothing, which read as broken.
                    signal.SetState(SignalState.Completed); signal.SetProgress(1f); _finished = true; break;
                case StationRegistry.Status.Completed:
                case StationRegistry.Status.Destroyed:
                    signal.SetState(SignalState.Completed); signal.SetProgress(1f); _finished = true; break;
                case StationRegistry.Status.Cooldown:
                    signal.SetState(SignalState.Cooldown); _finished = true; break;
                default:
                    signal.SetState(SignalState.Idle); break;
            }
        }

        /// <summary>The footprint per type: a beacon's arena is wide, a supply crate is opened up close.</summary>
        static Color LightColour(StationKind kind) => kind switch
        {
            StationKind.SignalRelay => new Color(0.35f, 0.85f, 1f),
            StationKind.SupplyCache => new Color(1f, 0.8f, 0.35f),
            StationKind.BossBeacon => new Color(1f, 0.3f, 0.25f),
            StationKind.SupplyDrop => new Color(0.55f, 1f, 0.5f),
            StationKind.HealZone => new Color(0.45f, 1f, 0.6f),
            _ => Color.white,
        };

        public static float RadiusFor(StationKind kind) => kind switch
        {
            // A9b: the zone is the model's own lit boundary — the hex pad's rim seam, the pod's
            // ground ring, the plinth's inner ring — so standing "on it" is exactly inside it.
            StationKind.SupplyDrop => 2.45f,
            StationKind.HealZone => 2.2f,
            _ => 2.9f,
        };

        /// <summary>The body has dissolved away; the director drops the object.</summary>
        public bool Gone => Signal != null && Signal.Visual != null && Signal.Visual.Gone;

        /// <summary>Paid out: the whole station dissolves and leaves the map (owner, 2026-09-30).</summary>
        void Leave() => Signal?.Visual?.Vanish();

        public bool Healing => _healing;

        /// <summary>Driven by the director, not by an Update per station.</summary>
        public void Tick(float dt, Vector3 playerPosition)
        {
            if (Signal == null) return;
            // A9: a repeatable station (Supply Cache, Heal Zone) re-arms in place when its cooldown
            // ends; it used to wait for the player to leave and stream it back in.
            if (_finished && Signal.State == SignalState.Cooldown &&
                StationRegistry.StatusOf(Anchor.id, Time.time) == StationRegistry.Status.Untouched)
            {
                _finished = false;
                _progressSeconds = 0f;
                Signal.SetState(SignalState.Idle);
                Signal.SetProgress(0f);
            }
            bool inside = Signal.Contains(playerPosition);
            Signal.SetPlayerInside(inside);
            if (_finished) return;

            switch (Anchor.kind)
            {
                case StationKind.SignalRelay: TickHold(dt, inside, relayHoldSeconds, true); break;
                case StationKind.SupplyCache: TickCache(dt, inside); break;
                case StationKind.BossBeacon: TickBeacon(inside); break;
                case StationKind.SupplyDrop: TickConfirm(dt, inside, supplyOpenSeconds); break;
                case StationKind.HealZone:
                    if (_healing) TickHealing(dt, inside);
                    else TickConfirm(dt, inside, healChargeSeconds);
                    break;
            }
        }

        /// A short stand-still confirmation that resets when the player steps out (Supply Drop, and
        /// the Heal Zone's switch-on), so walking past never triggers it by accident.
        void TickConfirm(float dt, bool inside, float required)
        {
            if (!inside)
            {
                _progressSeconds = 0f;
                Signal.SetProgress(0f);
                Signal.SetState(SignalState.Idle);
                return;
            }
            _progressSeconds += dt;
            Signal.SetState(SignalState.Active);
            Signal.SetProgress(_progressSeconds / required);
            if (_progressSeconds >= required) Complete();
        }

        /// The Heal Zone while it runs: heals whoever stands in it, the ring drains as time runs out,
        /// then the zone goes on cooldown.
        void TickHealing(float dt, bool inside)
        {
            float now = Time.time;
            Signal.SetProgress((_healUntil - now) / Mathf.Max(0.01f, healSeconds));
            if (inside && now >= _healTickAt)
            {
                _healTickAt = now + 0.5f;
                var hp = PlayerMovement.Instance != null ? PlayerMovement.Instance.GetComponentInParent<Health>() : null;
                if (hp != null && hp.Current < hp.Max)
                {
                    hp.Heal(hp.Max * healPerSecond * 0.5f);
                    StationDirector.Instance?.PlayHealTick(PlayerMovement.Instance.transform.position);
                }
            }
            if (now < _healUntil) return;
            _healing = false;
            _finished = true;
            StationRegistry.SetStatus(Anchor.id, StationRegistry.Status.Cooldown, now, healCooldownSeconds);
            Signal.SetState(SignalState.Cooldown);
            Leave();
        }

        void TickHold(float dt, bool inside, float required, bool bleeds)
        {
            if (inside)
            {
                _progressSeconds += dt;
                Signal.SetState(SignalState.Active);
            }
            else if (bleeds && _progressSeconds > 0f)
            {
                // Bleed, do not zero. Being chased off is part of the fight, not a reset.
                _progressSeconds = Mathf.Max(0f, _progressSeconds - relayBleedPerSecond * dt);
                Signal.SetState(SignalState.Idle);
            }

            Signal.SetProgress(_progressSeconds / required);
            if (_progressSeconds >= required) Complete();
        }

        void TickCache(float dt, bool inside)
        {
            int price = CachePrice;
            var run = RunState.Current;
            bool affordable = run != null && run.Coin >= price;
            UpdatePriceLabel(price, affordable);

            if (!inside)
            {
                _progressSeconds = 0f;
                Signal.SetProgress(0f);
                Signal.SetState(SignalState.Idle);
                return;
            }

            if (!affordable)
            {
                // Legible refusal: red ring, no progress. Never a silent "nothing happens".
                _progressSeconds = 0f;
                Signal.SetProgress(0f);
                Signal.SetState(SignalState.Failed);
                return;
            }

            _progressSeconds += dt;
            Signal.SetState(SignalState.Active);
            Signal.SetProgress(_progressSeconds / cacheConfirmSeconds);
            if (_progressSeconds < cacheConfirmSeconds) return;

            run.SpendCoin(price);
            StationDirector.CachePurchasesThisRun++;
            Complete();
        }

        void EnsurePriceLabel()
        {
            if (_priceLabel != null) return;
            var go = new GameObject("Price");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.4f, 0f);   // just above the pad, not up the screen
            go.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);   // faces the top-down camera
            _priceLabel = go.AddComponent<TextMeshPro>();
            _priceLabel.alignment = TextAlignmentOptions.Center;
            _priceLabel.fontSize = 4.5f;
            _priceLabel.fontStyle = FontStyles.Bold;
            _priceLabel.outlineWidth = 0.25f;
            _priceLabel.outlineColor = Color.black;
        }

        int _shownPrice = -1;
        bool _shownAffordable;

        void UpdatePriceLabel(int price, bool affordable)
        {
            if (_priceLabel == null) return;
            if (!_priceLabel.gameObject.activeSelf) _priceLabel.gameObject.SetActive(true);
            if (price == _shownPrice && affordable == _shownAffordable) return;
            _shownPrice = price;
            _shownAffordable = affordable;
            _priceLabel.text = $"{price} COIN";
            _priceLabel.color = affordable ? new Color(1f, 0.82f, 0.25f) : new Color(0.95f, 0.3f, 0.3f);
        }

        void TickBeacon(bool inside)
        {
            if (!inside)
            {
                _progressSeconds = 0f;
                Signal.SetProgress(0f);
                Signal.SetState(SignalState.Idle);
                return;
            }

            // A failed attempt re-arms after a pause instead of spamming the spawner.
            if (_bossAttempted && Time.time >= _bossRetryAt) _bossAttempted = false;
            if (_bossAttempted) return;

            // A short visible arming ramp. Without it the beacon read as "nothing is happening",
            // which is exactly how the owner described it.
            _progressSeconds += Time.deltaTime;
            Signal.SetState(SignalState.Active);
            Signal.SetProgress(_progressSeconds / beaconArmSeconds);
            if (_progressSeconds < beaconArmSeconds) return;

            // One major encounter at a time. A second beacon simply will not arm.
            if (!StationRegistry.TryClaimEncounter(Anchor.id))
            {
                Signal.SetState(SignalState.Idle);
                return;
            }

            // Arm ONLY if the boss actually spawns. Measured in play: the beacon used to claim the
            // encounter, mark itself finished and show Active while the spawn silently failed —
            // the player stood in the ring and nothing whatsoever happened. A station that cannot
            // deliver its event must not consume the single encounter slot.
            // ONE attempt, then back off. Retrying every frame produced ~50 placement failures per
            // two seconds in the console and hammered the spawner for no benefit.
            if (_bossAttempted) return;
            _bossAttempted = true;

            bool spawned = StationDirector.Instance != null &&
                           StationDirector.Instance.SpawnBossFor(this);
            if (!spawned)
            {
                // Legible failure: the beacon shows Failed and re-arms after a pause, rather than
                // silently pretending nothing happened.
                StationRegistry.ReleaseEncounter(Anchor.id);
                Signal.SetState(SignalState.Failed);
                _progressSeconds = 0f;
                _bossRetryAt = Time.time + 6f;
                return;
            }

            Signal.SetState(SignalState.Active);
            StationRegistry.SetStatus(Anchor.id, StationRegistry.Status.Active, Time.time);
            _finished = true;                       // the station's job is done; the BOSS is the event
            Leave();                                // the beacon has called its boss and goes
        }

        void Complete()
        {
            StationDirector.Instance?.PlayCompleteFx(Anchor.kind, transform.position);
            if (Anchor.kind == StationKind.HealZone)
            {
                // Switched on: the field runs for a while before the zone is spent (TickHealing).
                _healing = true;
                _healUntil = Time.time + healSeconds;
                _healTickAt = 0f;
                Signal.SetState(SignalState.Active);
                StationDirector.Instance?.PlayHealField(transform.position, Signal.RingRadius, healSeconds);
                StationDirector.ReportCompleted(Anchor.kind);
                return;
            }

            _finished = true;
            Signal.SetProgress(1f);
            Signal.SetState(SignalState.Completed);

            switch (Anchor.kind)
            {
                case StationKind.SupplyDrop:
                    StationRegistry.SetStatus(Anchor.id, StationRegistry.Status.Completed, Time.time);
                    StationDirector.Instance?.GrantSupplyDrop(transform.position);
                    break;

                case StationKind.SignalRelay:
                    StationRegistry.SetStatus(Anchor.id, StationRegistry.Status.Completed, Time.time);
                    StationDirector.Instance?.GrantCardOffer();
                    break;

                case StationKind.SupplyCache:
                    // Repeatable: a route can be re-run, but not farmed on the spot.
                    StationRegistry.SetStatus(Anchor.id, StationRegistry.Status.Cooldown,
                                              Time.time, cacheCooldownSeconds);
                    if (_priceLabel != null) _priceLabel.gameObject.SetActive(false);
                    StationDirector.Instance?.GrantCardOffer();
                    break;
            }

            StationDirector.ReportCompleted(Anchor.kind);
            Leave();
        }

        /// <summary>Called when the director gives up on this station (player left the ring).</summary>
        public void Release()
        {
            if (Anchor.kind == StationKind.BossBeacon && !StationRegistry.IsBossAlive(Anchor.id))
                StationRegistry.ReleaseEncounter(Anchor.id);
        }
    }
}
