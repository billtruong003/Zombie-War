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
        [Tooltip("05/10: run XP drops as blue orbs (the coin mesh on the blue atlas cell).")]
        [SerializeField] private string xpPoolKey = "pickup_xp";
        [SerializeField] private string healthPoolKey = "pickup_health";

        [Header("Magnet")]
        [Tooltip("How close the player must get before loot flies to them.")]
        [SerializeField] private float magnetRadius = 4.5f;
        [Tooltip("05/10 owner: XP orbs are walked to, not pulled from afar. Only this close (times " +
                 "Pickup Range) do they fly in; a Magnet item still sweeps them all.")]
        [SerializeField] private float xpPullRadius = 1.2f;

        [Header("Drops")]
        [Tooltip("Coins are split into at most this many physical pickups, so a boss worth 40 coin " +
                 "does not spawn 40 objects.")]
        [SerializeField] private int maxCoinDropsPerKill = 4;
        [Tooltip("Chance an elite/boss also drops a Gem. Normal enemies never roll for one.")]
        [Range(0f, 1f)] [SerializeField] private float eliteGemChance = 0.5f;
        [SerializeField] private int eliteGemAmount = 1;
        [SerializeField] private float dropScatterRadius = 0.6f;

        [Header("Crowd-scale drops (M8) — TUNING")]
        [Tooltip("Physical coin pickups a NORMAL enemy drops (its whole coin value rides on them). " +
                 "Measured 2026-09-26: at horde density, splitting every 2-4 coin reward into 2-4 " +
                 "objects carpeted the ground. Elites still burst into up to maxCoinDropsPerKill.")]
        [SerializeField] private int normalEnemyCoinDrops = 1;
        [Tooltip("Above this many live pickups, a new coin merges into the nearest resting coin " +
                 "within mergeRadius instead of adding another object.")]
        [SerializeField] private int mergeAboveLive = 60;
        [SerializeField] private float mergeRadius = 4f;

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
            if (!TryGetComponent<SupplyCrates>(out _)) gameObject.AddComponent<SupplyCrates>();
            if (!TryGetComponent<RewardLadder>(out _)) gameObject.AddComponent<RewardLadder>();
            if (!TryGetComponent<RunMoments>(out _)) gameObject.AddComponent<RunMoments>();
            if (!TryGetComponent<FootDust>(out _)) gameObject.AddComponent<FootDust>();
            Bill.Events?.Subscribe<ZombieKilledEvent>(OnZombieKilled);
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            Bill.Events?.Unsubscribe<ZombieKilledEvent>(OnZombieKilled);
        }

        private void Update()
        {
            TrackEvolutionReady();
            TickRescue();
            var player = PlayerMovement.Instance;
            if (player == null || Live.Count == 0) return;

            Vector3 playerPos = player.transform.position;
            float dt = Time.deltaTime;

            // Iterate a copy: collecting returns the pickup to the pool, which unregisters it and
            // would otherwise mutate the list mid-loop.
            // (Copied by hand: Mono's AddRange(list) allocates a temporary array every frame.)
            Scratch.Clear();
            for (int i = 0; i < Live.Count; i++) Scratch.Add(Live[i]);
            // A4 Pickup Range grows the pull radius.
            float range = ZombieWar.Skills.SkillRuntime.Active?.PickupRangeMultiplier ?? 1f;
            float pull = magnetRadius * range, xpPull = xpPullRadius * range;
            for (int i = 0; i < Scratch.Count; i++)
            {
                var p = Scratch[i];
                if (p != null) p.Tick(dt, playerPos, p.Effect == PickupEffect.Xp ? xpPull : pull, _magnetSweepUntil > Time.time);
            }
        }

        // ── magnet sweep ────────────────────────────────────────────────────────────────────
        // Run-scoped: registered with RunScope below so a sweep cannot survive into the next run.
        private static float _magnetSweepUntil;
        private static bool _magnetRegistered;

        [Header("Mechanic items (A8) — TUNING")]
        [Tooltip("Chance an elite kill drops a mechanic item (Magnet, Bomb or Freeze Clock), before Luck.")]
        [SerializeField, Range(0f, 1f)] private float mechanicDropChance = 0.15f;
        [SerializeField] private string magnetPoolKey = "pickup_magnet";
        [SerializeField] private string bombPoolKey = "pickup_bomb";
        [SerializeField] private string freezePoolKey = "pickup_freeze";

        /// <summary>A mechanic item is on the map (the one-at-a-time rule).</summary>
        public static bool MechanicOnMap
        {
            get
            {
                for (int i = 0; i < Live.Count; i++)
                    if (Live[i] != null && Live[i].IsMechanic && !Live[i].Collected) return true;
                return false;
            }
        }

        /// <summary>Drops a Magnet, Bomb or Freeze Clock, unless one is already on the map.</summary>
        public bool SpawnMechanic(PickupEffect item, Vector3 at)
        {
            if (!MechanicItems.IsMechanic(item) || MechanicOnMap) return false;
            string key = item == PickupEffect.Bomb ? bombPoolKey : item == PickupEffect.Freeze ? freezePoolKey : magnetPoolKey;
            at.y = 0f;
            Spawn(PlayerProfile.CurrencyKind.Coin, 0, key, at);
            return MechanicOnMap;
        }

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

        /// <summary>Adds the value to the nearest resting pickup of the same kind within
        /// <see cref="mergeRadius"/>. False when none is close enough, so the drop spawns normally.</summary>
        private bool TryMerge(PlayerProfile.CurrencyKind kind, int amount, string key, Vector3 origin)
        {
            Pickup best = null;
            float bestSqr = mergeRadius * mergeRadius;
            for (int i = 0; i < Live.Count; i++)
            {
                var p = Live[i];
                if (p == null) continue;
                float d = (p.transform.position - origin).sqrMagnitude;
                if (d < bestSqr && p.Kind == kind && p.PoolKey == key && p.Amount > 0 && !p.Collected) { best = p; bestSqr = d; }
            }
            return best != null && best.TryAbsorb(kind, amount);
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

            // 05/10: coin is banked on the kill (RecordKill); the floor carries XP and items.
            int xp = Mathf.Max(0, data.xpReward);
            if (xp > 0)
            {
                int drops = Mathf.Clamp(xp, 1, data.isElite ? maxCoinDropsPerKill : Mathf.Max(1, normalEnemyCoinDrops));
                int per = Mathf.Max(1, xp / drops);
                int remainder = xp - per * drops;
                for (int i = 0; i < drops; i++)
                    Spawn(PlayerProfile.CurrencyKind.Coin, per + (i == 0 ? remainder : 0), xpPoolKey, origin);
            }

            // Genre rule 9: what the player needs, rarely enough to feel like luck (NeedDrops).
            float luck = ZombieWar.Skills.SkillRuntime.Active?.LuckMultiplier ?? 1f;   // A4 Luck: items, not coins
            DropForNeed(origin, data.isElite, luck);

            // A7: elites can drop a chest; Luck raises the chance (the beacon boss drops its own).
            // A10 pity: an evolution waiting too long makes the next elite's chest certain.
            if (data.isElite && (ChestPity(_evoReadySince, Time.time, evolutionPitySeconds) || Random.value < eliteChestChance * luck))
            {
                SpawnChest(origin + new Vector3(0.8f, 0f, 0.4f));
                if (_evoReadySince >= 0f) _evoReadySince = Time.time;   // one chest per wait, not one per elite
            }

            // Gems stay rare and authored: elites and bosses only.
            if (data.isElite && Random.value < eliteGemChance * luck)
                Spawn(PlayerProfile.CurrencyKind.Gem, eliteGemAmount, gemPoolKey, origin);
        }

        // ── A7 chests ─────────────────────────────────────────────────────────────────
        [Header("Chest (A7: evolutions come from chests only)")]
        [Tooltip("Chance an elite (not a beacon boss) drops a chest, before Luck.")]
        [SerializeField, Range(0f, 1f)] private float eliteChestChance = 0.04f;
        [SerializeField] private string chestPoolKey = "pickup_chest";

        [Tooltip("A10: once an evolution has been ready this long, the next elite always drops a chest. " +
                 "Measured: a 14-minute pacing run that never met a beacon got no evolution at all.")]
        [SerializeField] private float evolutionPitySeconds = 45f;
        private float _evoReadySince = -1f, _evoCheckAt;

        /// <summary>The pity rule: an evolution ready since <paramref name="readySince"/> (negative:
        /// none ready) for at least <paramref name="pitySeconds"/>.</summary>
        public static bool ChestPity(float readySince, float now, float pitySeconds) =>
            readySince >= 0f && now - readySince >= pitySeconds;

        // ── Need drops (05/10, genre rules 5 and 9) ─────────────────────────────────────
        readonly NeedDrops _needs = new();
        static bool _needsRegistered;
        Health _playerHealth;

        Health PlayerHealth()
        {
            if (_playerHealth == null && PlayerMovement.Instance != null)
                _playerHealth = PlayerMovement.Instance.GetComponentInParent<Health>();
            return _playerHealth;
        }

        float HealthFraction()
        {
            var h = PlayerHealth();
            return h == null || h.Max <= 0f ? 1f : h.Current / h.Max;
        }

        void TickRescue()
        {
            if (!_needsRegistered) { _needsRegistered = true; RunScope.Register(() => { if (Instance != null) Instance._needs.Reset(); }); }
            var player = PlayerMovement.Instance;
            if (player == null || !_needs.Tick(HealthFraction(), Time.time)) return;
            // No kill brought it in time: a heal lands a few metres away, in view.
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(2.5f, 4f);
            SpawnAt(healthPoolKey, player.transform.position + new Vector3(dir.x, 0f, dir.y));
        }

        void DropForNeed(Vector3 origin, bool elite, float luck)
        {
            if (PlayerMovement.Instance == null) return;
            var c = GatherNeeds(elite);
            var r = _needs.OnKill(c, Time.time, Random.value / luck, Random.value / luck);
            if (r.heal) SpawnAt(healthPoolKey, origin + new Vector3(0.5f, 0f, -0.3f));
            if (r.item != PickupEffect.Currency) SpawnMechanic(r.item, origin + new Vector3(-0.5f, 0f, 0.3f));
        }

        NeedDrops.Context GatherNeeds(bool elite)
        {
            var c = new NeedDrops.Context { healthFraction = HealthFraction(), elite = elite };
            var player = PlayerMovement.Instance;
            if (player == null) return c;
            Vector3 p = player.transform.position;
            var alive = ZombieManager.Alive;
            int pouncers = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                var z = alive[i];
                if (z == null || z.IsDead) continue;
                Vector3 d = z.transform.position - p; d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr <= 16f) c.crowdNear++;
                if (sqr <= 64f && ((z.Data != null && z.Data.isElite) || z is ZombiePouncer && ++pouncers >= 3)) c.dangerNear = true;
            }
            for (int i = 0; i < Live.Count; i++)
                if (Live[i] != null && Live[i].Effect == PickupEffect.Xp && !Live[i].Collected) c.looseOrbs++;
            return c;
        }

        /// <summary>A broken supply crate: XP worth half a level in a ring, a heal when the player is
        /// hurt, and the item they need most.</summary>
        public void DropCrateLoot(Vector3 at)
        {
            int xp = Mathf.Max(6, (RunState.Current?.XpForNextLevel ?? 12) / 2);
            for (int i = 0; i < 4; i++)
            {
                var offset = new Vector3(Mathf.Cos(i * 1.57f), 0f, Mathf.Sin(i * 1.57f)) * 0.9f;
                Spawn(PlayerProfile.CurrencyKind.Coin, Mathf.Max(1, xp / 4), xpPoolKey, at + offset);
            }
            if (HealthFraction() < 0.7f) SpawnAt(healthPoolKey, at + new Vector3(0.6f, 0f, -0.6f));
            SpawnMechanic(NeedDrops.BestItem(GatherNeeds(true), out _), at + new Vector3(-0.6f, 0f, 0.6f));
        }

        void SpawnAt(string key, Vector3 at)
        {
            at.y = 0f;
            Spawn(PlayerProfile.CurrencyKind.Coin, 0, key, at);
        }

        private void TrackEvolutionReady()
        {
            if (Time.time < _evoCheckAt) return;
            _evoCheckAt = Time.time + 1f;
            bool ready = ZombieWar.Skills.SkillRuntime.Active?.AnyEvolutionReady ?? false;
            if (!ready) _evoReadySince = -1f;
            else if (_evoReadySince < 0f) _evoReadySince = Time.time;
        }

        /// <summary>A chest was walked over. RunOverlays opens it.</summary>
        public static event System.Action<Vector3> ChestCollected;
        public static void RaiseChestCollected(Vector3 at) => ChestCollected?.Invoke(at);

        /// <summary>Drops a chest (a beacon boss always does; a Supply Drop can).</summary>
        public void SpawnChest(Vector3 at)
        {
            at.y = 0f;
            Spawn(PlayerProfile.CurrencyKind.Coin, 0, chestPoolKey, at);   // its appear burst plays on enable
            Bill.Audio?.PlayCue("sfx.skill.evolve", at, SfxPriority.High, 0.7f);
        }

        /// <summary>A coin that is not a kill's authored reward (Drone Squadron's bonus).</summary>
        public static void SpawnBonusCoin(Vector3 at, int amount)
        {
            if (amount <= 0) return;
            RunState.Current?.AddCurrency(PlayerProfile.CurrencyKind.Coin, amount);   // 05/10: coin is never on the floor
        }

        /// <summary>A shower of XP orbs worth <paramref name="totalXp"/> (the golden zombie's prize).</summary>
        public void SpawnXpBurst(Vector3 at, int totalXp, int orbs)
        {
            orbs = Mathf.Clamp(orbs, 1, Mathf.Max(1, totalXp));
            int per = Mathf.Max(1, totalXp / orbs), rest = totalXp - per * orbs;
            for (int i = 0; i < orbs; i++) Spawn(PlayerProfile.CurrencyKind.Coin, per + (i == 0 ? rest : 0), xpPoolKey, at);
        }

        private void Spawn(PlayerProfile.CurrencyKind kind, int amount, string key, Vector3 origin)
        {
            if (string.IsNullOrEmpty(key) || Bill.Pool == null) return;

            if (amount > 0 && Live.Count >= mergeAboveLive && TryMerge(kind, amount, key, origin)) return;

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
            // 05/10: the coin is banked; the ring of loot is XP worth half a level.
            RunState.Current?.AddCurrency(PlayerProfile.CurrencyKind.Coin, coin);
            int xp = Mathf.Max(6, (RunState.Current?.XpForNextLevel ?? 12) / 2);
            for (int i = 0; i < 6; i++)
            {
                var offset = new Vector3(Mathf.Cos(i * 1.05f), 0f, Mathf.Sin(i * 1.05f)) * 1.6f;
                Spawn(PlayerProfile.CurrencyKind.Coin, Mathf.Max(1, xp / 6), xpPoolKey, at + offset);
            }
            if (gem > 0) Spawn(PlayerProfile.CurrencyKind.Gem, gem, gemPoolKey, at);
        }

        /// <summary>Gem visual scale from its value. Sub-linear so a 10-gem is noticeably bigger than
        /// a 1-gem without being ten times the size.</summary>
        public static float GemScaleFor(int amount) =>
            Mathf.Clamp(0.8f + Mathf.Log(Mathf.Max(1, amount) + 1f, 2f) * 0.35f, 0.8f, 2.5f);
    }
}
