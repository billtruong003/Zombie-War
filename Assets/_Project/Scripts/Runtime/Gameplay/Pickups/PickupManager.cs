using System.Collections.Generic;
using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Drops loot when enemies die and drives every live pickup from one loop.
    ///
    /// One manager Update instead of one per coin: a cleared wave can leave a hundred pickups on the
    /// floor, and a hundred MonoBehaviour.Update calls (plus a hundred trigger colliders) is exactly
    /// the kind of per-frame cost this project avoids elsewhere.
    ///
    /// Drops are authored per enemy through <see cref="ZombieData.coinReward"/>, with Gems kept rare
    /// and explicit - there is no routine Gem farming, so a Gem only appears on an elite/boss roll.
    /// </summary>
    public class PickupManager : MonoBehaviour
    {
        public static PickupManager Instance { get; private set; }

        [Header("Pool keys (Resources/Pools/<key>)")]
        [SerializeField] private string coinPoolKey = "pickup_coin";
        [SerializeField] private string gemPoolKey = "pickup_gem";

        [Header("Magnet")]
        [Tooltip("How close the player must get before loot flies to them.")]
        [SerializeField] private float magnetRadius = 3.5f;

        [Header("Drops")]
        [Tooltip("Coins are split into at most this many physical pickups, so a boss worth 40 coin " +
                 "does not spawn 40 objects.")]
        [SerializeField] private int maxCoinDropsPerKill = 4;
        [Tooltip("Chance an elite/boss also drops a Gem. Normal enemies never roll for one.")]
        [Range(0f, 1f)] [SerializeField] private float eliteGemChance = 0.5f;
        [SerializeField] private int eliteGemAmount = 1;
        [SerializeField] private float dropScatterRadius = 0.6f;

        private static readonly List<Pickup> Live = new List<Pickup>(128);
        private static readonly List<Pickup> Scratch = new List<Pickup>(128);

        public static void Register(Pickup p) { if (!Live.Contains(p)) Live.Add(p); }
        public static void Unregister(Pickup p) => Live.Remove(p);

        /// <summary>Live pickups currently registered. Exposed so a run boundary can be asserted.</summary>
        public static int LiveCount => Live.Count;

        /// <summary>
        /// M7.2c — `Live` is a STATIC list, so it survived scene unloads and accumulated entries from
        /// every previous run (including destroyed ones). Cleared through RunScope at run start; the
        /// pickup objects themselves belong to the pool and are released with the scene.
        /// </summary>
        public static void ClearRegistry()
        {
            Live.Clear();
            Scratch.Clear();
        }

        private void OnEnable()
        {
            Instance = this;
            EnsureMagnetRegistered();
            Bill.Events?.Subscribe<ZombieKilledEvent>(OnZombieKilled);
            Bill.Events?.Subscribe<WaveClearedEvent>(OnWaveCleared);
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            Bill.Events?.Unsubscribe<ZombieKilledEvent>(OnZombieKilled);
            Bill.Events?.Unsubscribe<WaveClearedEvent>(OnWaveCleared);
        }

        private void Update()
        {
            var player = PlayerMovement.Instance;
            if (player == null || Live.Count == 0) return;

            Vector3 playerPos = player.transform.position;
            float dt = Time.deltaTime;

            // Iterate a copy: collecting returns the pickup to the pool, which unregisters it and
            // would otherwise mutate the list mid-loop.
            Scratch.Clear();
            Scratch.AddRange(Live);
            for (int i = 0; i < Scratch.Count; i++)
            {
                var p = Scratch[i];
                if (p != null) p.Tick(dt, playerPos, magnetRadius, _magnetSweepUntil > Time.time);
            }
        }

        // ── magnet sweep ────────────────────────────────────────────────────────────────────
        // Run-scoped: registered with RunScope below so a sweep cannot survive into the next run.
        private static float _magnetSweepUntil;
        private static bool _magnetRegistered;

        [Header("Magnet pickup (TUNING)")]
        [Tooltip("How long the sweep keeps pulling. Long enough for distant coins to arrive.")]
        [SerializeField] private float magnetSweepSeconds = 2.5f;
        [Tooltip("Chance an elite/boss kill drops a magnet.")]
        [SerializeField, Range(0f, 1f)] private float magnetDropChance = 0.12f;
        [SerializeField] private string magnetPoolKey = "pickup_magnet";

        /// <summary>
        /// Pulls EVERY pickup on the ground to the player for a short window. Global rather than a
        /// radius: a magnet that only grabbed nearby coins would be indistinguishable from walking.
        /// </summary>
        public static void BeginMagnetSweep(float seconds = 2.5f)
        {
            _magnetSweepUntil = Time.time + seconds;
        }

        public static bool MagnetSweepActive => _magnetSweepUntil > Time.time;

        /// <summary>Run-scoped reset: a sweep in progress must not carry into a new run.</summary>
        public static void ResetMagnet() => _magnetSweepUntil = 0f;

        void EnsureMagnetRegistered()
        {
            if (_magnetRegistered) return;
            _magnetRegistered = true;
            RunScope.Register(ResetMagnet);
        }

        /// <summary>
        /// Drops this kill's loot.
        ///
        /// The COIN AMOUNT still comes from ZombieData and is unchanged - the pickup is only the
        /// physical delivery of a reward the ledger already knew about. That is why ZombieBase no
        /// longer banks coin directly when pickups are enabled: otherwise the player would be paid
        /// twice for one kill.
        /// </summary>
        private void OnZombieKilled(ZombieKilledEvent e)
        {
            var data = e.Data;
            if (data == null) return;

            Vector3 origin = e.Position;

            int coin = Mathf.Max(0, data.coinReward);
            if (coin > 0)
            {
                int drops = Mathf.Clamp(coin, 1, maxCoinDropsPerKill);
                int per = Mathf.Max(1, coin / drops);
                int remainder = coin - per * drops;

                for (int i = 0; i < drops; i++)
                {
                    int amount = per + (i == 0 ? remainder : 0);
                    Spawn(PlayerProfile.CurrencyKind.Coin, amount, coinPoolKey, origin);
                }
            }

            // M7.3b — the magnet drop. Elites and bosses only, so it stays an event rather than
            // background noise, and so the player associates it with a fight they chose.
            //
            // FALLBACK, and it matters more than the magnet: a player who never sees one loses
            // nothing. Coins sit on the ground indefinitely and are collected by walking over them,
            // exactly as before. The magnet is a convenience reward, never the only route to loot.
            if (data.isElite && Random.value < magnetDropChance)
                Spawn(PlayerProfile.CurrencyKind.Coin, 0, magnetPoolKey, origin);

            // Gems stay rare and authored: elites and bosses only.
            if (data.isElite && Random.value < eliteGemChance)
                Spawn(PlayerProfile.CurrencyKind.Gem, eliteGemAmount, gemPoolKey, origin);
        }

        private void Spawn(PlayerProfile.CurrencyKind kind, int amount, string key, Vector3 origin)
        {
            if (string.IsNullOrEmpty(key) || Bill.Pool == null) return;

            Vector2 scatter = Random.insideUnitCircle * dropScatterRadius;
            Vector3 pos = origin + new Vector3(scatter.x, 0.25f, scatter.y);

            var go = Bill.Pool.Spawn(key, pos, Quaternion.identity);
            if (go == null) return;

            var pickup = go.GetComponent<Pickup>();
            if (pickup == null)
            {
                Debug.LogWarning($"[PickupManager] Pool '{key}' has no Pickup component.", go);
                return;
            }
            pickup.Init(kind, amount, key, pos);

            // Gem size communicates how much it is worth, per the design: bigger gem = more.
            if (kind == PlayerProfile.CurrencyKind.Gem)
                go.transform.localScale = Vector3.one * GemScaleFor(amount);
        }

        /// <summary>
        /// M7.3 — a station reward, scattered on the floor. Deliberately NOT auto-credited: with
        /// wave-clear auto-collect removed, walking to loot is the point.
        /// </summary>
        public void DropReward(Vector3 at, int coin = 60, int gem = 1)
        {
            for (int i = 0; i < 6; i++)
            {
                var offset = new Vector3(Mathf.Cos(i * 1.05f), 0f, Mathf.Sin(i * 1.05f)) * 1.6f;
                Spawn(PlayerProfile.CurrencyKind.Coin, Mathf.Max(1, coin / 6), coinPoolKey, at + offset);
            }
            if (gem > 0) Spawn(PlayerProfile.CurrencyKind.Gem, gem, gemPoolKey, at);
        }

        /// <summary>Gem visual scale from its value. Sub-linear so a 10-gem is noticeably bigger than
        /// a 1-gem without being ten times the size.</summary>
        public static float GemScaleFor(int amount) =>
            Mathf.Clamp(0.8f + Mathf.Log(Mathf.Max(1, amount) + 1f, 2f) * 0.35f, 0.8f, 2.5f);

        /// <summary>
        /// M7.3 — auto-collect on wave clear is REMOVED.
        ///
        /// An endless world has no reliable wave boundary, so the trigger was unreliable to begin
        /// with; worse, sweeping the floor removed the reason to move toward loot at all. Loot is now
        /// walked to, which is what makes the magnet pickup and station rewards mean anything.
        ///
        /// `CollectAll()` is kept as public API — the result/settlement path may still want it — but
        /// nothing subscribes it to WaveClearedEvent any more.
        /// </summary>
        private void OnWaveCleared(WaveClearedEvent e) { /* intentionally empty — see summary */ }

        public static void CollectAll()
        {
            Scratch.Clear();
            Scratch.AddRange(Live);
            for (int i = 0; i < Scratch.Count; i++)
                if (Scratch[i] != null) Scratch[i].CollectImmediate();
        }
    }
}
