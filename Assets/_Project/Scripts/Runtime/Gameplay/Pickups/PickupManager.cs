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

        private void OnEnable()
        {
            Instance = this;
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
                if (p != null) p.Tick(dt, playerPos, magnetRadius, false);
            }
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

        /// <summary>Gem visual scale from its value. Sub-linear so a 10-gem is noticeably bigger than
        /// a 1-gem without being ten times the size.</summary>
        public static float GemScaleFor(int amount) =>
            Mathf.Clamp(0.8f + Mathf.Log(Mathf.Max(1, amount) + 1f, 2f) * 0.35f, 0.8f, 2.5f);

        /// <summary>Sweeps up everything still on the floor when a wave ends, so the player never
        /// has to walk the arena picking up stragglers before the result screen.</summary>
        private void OnWaveCleared(WaveClearedEvent e) => CollectAll();

        public static void CollectAll()
        {
            Scratch.Clear();
            Scratch.AddRange(Live);
            for (int i = 0; i < Scratch.Count; i++)
                if (Scratch[i] != null) Scratch[i].CollectImmediate();
        }
    }
}
