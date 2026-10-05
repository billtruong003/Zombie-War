#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using ZombieWar.Skills;
using ZombieWar.Threat;

namespace ZombieWar.Dev
{
    /// <summary>
    /// Bot lab (backlog #28, owner 05/10): plays real runs on the real map without god mode, at a
    /// raised time scale, and measures what the genre rules ask (GAME_DESIGN §4): when the first
    /// card comes, whether the player outgrows the early crowd, how long a new player lives, whether
    /// a strong build still dies. The bot kites away from the crowd, walks over XP, heals when hurt,
    /// takes the first card offered, claims chests and declines revives.
    ///
    /// One CSV per run (a row per second) and one summary line per run in
    /// Review/QA/botlab/summary.jsonl. Start a batch with <see cref="Batch"/>.
    /// </summary>
    public sealed class BotLab : MonoBehaviour
    {
        public enum Profile { New, Mid, Strong }

        public static BotLab Instance { get; private set; }
        public static bool Running => Instance != null && Instance._running;
        public string LastSummary { get; private set; }

        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

        bool _running;
        Vector2 _input;
        float _wanderAngle;
        Vector3 _lastPos;
        float _stuckCheckAt, _unstickUntil;
        Vector2 _unstickDir;

        // ------------------------------------------------------------------ batch

        /// <summary>Runs <paramref name="runs"/> bot runs back to back with the given meta profile.</summary>
        public static void Batch(Profile profile, int runs, float timeScale = 4f, float maxMinutes = 25f, string label = null)
        {
            if (Instance == null)
            {
                var go = new GameObject("[BotLab]");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<BotLab>();
            }
            Instance.StartCoroutine(Instance.RunBatch(profile, runs, timeScale, maxMinutes, label ?? profile.ToString().ToLowerInvariant()));
        }

        IEnumerator RunBatch(Profile profile, int runs, float timeScale, float maxMinutes, string label)
        {
            _running = true;
            SetupProfile(profile);
            var all = new StringBuilder();
            for (int i = 0; i < runs; i++)
            {
                if (i == 0 && !GameFlow.InGameplay) GameFlow.StartGameplay();
                else GameFlow.RestartGameplay();
                yield return WaitForRun();
                yield return PlayOne(label, i, timeScale, maxMinutes, all);
            }
            Time.timeScale = 1f;
            PlayerMovement.BotInput = null;
            LastSummary = all.ToString();
            _running = false;
            Debug.Log("[BotLab] batch done\n" + LastSummary);
        }

        static IEnumerator WaitForRun()
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f)
            {
                var run = RunState.Current;
                if (run != null && !run.IsOver && PlayerMovement.Instance != null && run.Duration < 2f) yield break;
                yield return null;
            }
        }

        /// <summary>Labs and captures: answer level-ups and chests at once (the bot's card pick) without
        /// steering, until <see cref="StopAnswering"/>.</summary>
        public static void AnswerOverlays()
        {
            if (Instance == null)
            {
                var go = new GameObject("[BotLab]");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<BotLab>();
            }
            Instance.StopCoroutine(nameof(AnswerLoop));
            Instance.StartCoroutine(nameof(AnswerLoop));
        }

        public static void StopAnswering() { if (Instance != null) Instance.StopCoroutine(nameof(AnswerLoop)); }

        IEnumerator AnswerLoop()
        {
            var oType = typeof(RunOverlays);
            var pick = oType.GetMethod("PickPerk", F);
            var claim = oType.GetMethod("ClaimChest", F);
            var offerField = oType.GetField("_skillOffer", F);
            var chestOpen = oType.GetProperty("ChestOpen", F);
            var levelRoot = oType.GetField("levelUpRoot", F);
            while (true)
            {
                var overlays = FindFirstObjectByType<RunOverlays>(FindObjectsInactive.Include);
                if (overlays != null)
                {
                    var root = levelRoot?.GetValue(overlays) as GameObject;
                    if (root != null && root.activeSelf && offerField?.GetValue(overlays) is List<SkillDef> offer && offer.Count > 0)
                        pick?.Invoke(overlays, new object[] { ChooseCard(offer) });
                    if (chestOpen != null && (bool)chestOpen.GetValue(overlays)) claim?.Invoke(overlays, null);
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ meta profile

        static void SetupProfile(Profile profile)
        {
            // Every FTUE step done, so no card waits for a tap and no tutorial modal stops the run.
            foreach (var f in typeof(Ftue).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                if (f.IsLiteral && f.FieldType == typeof(string)) Ftue.Complete((string)f.GetValue(null));
            var data = typeof(PlayerProfile).GetProperty("Data", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(null);
            data?.GetType().GetField("runsPlayed")?.SetValue(data, Mathf.Max(3, PlayerProfile.RunsPlayed));
            if (profile == Profile.New) return;

            var cheat = BillGameCore.Bill.Cheat;
            cheat?.Execute("zw.unlock.weapons");
            cheat?.Execute("zw.acclevel 10");
            var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
            if (guns == null) return;
            WeaponData pick = null;
            foreach (var g in guns)
            {
                if (g == null) continue;
                var want = profile == Profile.Strong ? WeaponTier.Legendary : WeaponTier.Rare;
                if (g.tier == want && (pick == null || CombatPower.WeaponPower(g, 1) > CombatPower.WeaponPower(pick, 1))) pick = g;
            }
            if (pick == null) return;
            PlayerProfile.SetEquippedWeapon(pick.WeaponId);
            var econ = Resources.FindObjectsOfTypeAll<EconomyConfig>();
            int stars = profile == Profile.Strong ? 3 : 2;
            for (int s = PlayerProfile.GetWeaponLevel(pick.WeaponId); s < stars && econ.Length > 0; s++)
            {
                PlayerProfile.AddWeaponShards(pick.WeaponId, 500);
                cheat?.Execute("zw.coin");
                PlayerProfile.TryUpgradeWeapon(pick, econ[0]);
            }
            if (profile == Profile.Strong)
            {
                PlayerProfile.AddMasteryXp(pick.WeaponId, 50000);
                PlayerProfile.TryEvolveGun(pick.WeaponId);
            }
        }

        // ------------------------------------------------------------------ one run

        IEnumerator PlayOne(string label, int index, float timeScale, float maxMinutes, StringBuilder all)
        {
            var run = RunState.Current;
            var overlays = FindFirstObjectByType<RunOverlays>(FindObjectsInactive.Include);
            var runEnd = FindFirstObjectByType<ZombieWar.UI.RunEndV2>(FindObjectsInactive.Include);
            var hp = PlayerMovement.Instance.GetComponentInParent<Health>();
            var oType = typeof(RunOverlays);
            var pick = oType.GetMethod("PickPerk", F);
            var claim = oType.GetMethod("ClaimChest", F);
            var offerField = oType.GetField("_skillOffer", F);
            var chestOpen = oType.GetProperty("ChestOpen", F);
            var levelRoot = oType.GetField("levelUpRoot", F);
            var reviveRoot = typeof(ZombieWar.UI.RunEndV2).GetField("reviveRoot", F);
            var giveUp = typeof(ZombieWar.UI.RunEndV2).GetMethod("GiveUp", F);

            PlayerMovement.BotInput = () => _input;
            _wanderAngle = Random.value * Mathf.PI * 2f;
            _lastPos = PlayerMovement.Instance.transform.position;
            _stuckCheckAt = Time.time + 2f;

            var csv = new StringBuilder("t,hp,level,kills,alive,ranged,elites,tier,coin,skills,stats,surging,danger,xp_gained,xp_dropped,orbs_floor\n");
            int xpDropped = 0;
            System.Action<ZombieKilledEvent> onKill = e => { if (e.Data != null) xpDropped += Mathf.Max(0, e.Data.xpReward); };
            BillGameCore.Bill.Events?.Subscribe(onKill);
            float peakOrbs = 0f;
            float nextRow = 0f, firstCardAt = -1f, lowHpSeconds = 0f, peakAlive = 0f, peakRangedShare = 0f;
            int lastLevel = run.Level, chests = 0, cardsBy2 = 0, revivesDeclined = 0;
            var levelTimes = new List<string>();
            float hpBefore = hp != null ? hp.Current : 100f, damageTaken = 0f;
            bool wasChest = false;

            while (run != null && !run.IsOver && run.Duration < maxMinutes * 60f)
            {
                // answer every overlay at once, as a player who never hesitates would
                if (overlays != null)
                {
                    var root = levelRoot?.GetValue(overlays) as GameObject;
                    if (root != null && root.activeSelf && offerField?.GetValue(overlays) is List<SkillDef> offer && offer.Count > 0)
                        pick?.Invoke(overlays, new object[] { ChooseCard(offer) });
                    bool open = chestOpen != null && (bool)chestOpen.GetValue(overlays);
                    if (open && !wasChest) chests++;
                    wasChest = open;
                    if (open) claim?.Invoke(overlays, null);
                }
                if (runEnd != null && reviveRoot?.GetValue(runEnd) is GameObject rr && rr.activeSelf)
                {
                    revivesDeclined++;
                    giveUp?.Invoke(runEnd, null);
                }
                if (run.IsOver) break;
                Time.timeScale = timeScale;

                Steer(hp, out float danger);

                if (run.Level != lastLevel)
                {
                    if (firstCardAt < 0f) firstCardAt = run.Duration;
                    if (run.Duration <= 120f) cardsBy2 += run.Level - lastLevel;
                    lastLevel = run.Level;
                    levelTimes.Add(((int)run.Duration).ToString());
                }
                if (hp != null)
                {
                    if (hp.Current < hpBefore) damageTaken += hpBefore - hp.Current;
                    hpBefore = hp.Current;
                    if (hp.Current < hp.Max * 0.25f) lowHpSeconds += Time.deltaTime;
                }

                if (run.Duration >= nextRow)
                {
                    nextRow = Mathf.Floor(run.Duration) + 1f;
                    int alive = ZombieManager.AliveCount, ranged = 0, elites = 0;
                    var zs = ZombieManager.Alive;
                    for (int i = 0; i < zs.Count; i++)
                    {
                        var z = zs[i]; if (z == null || z.Data == null) continue;
                        if (z.Data.archetype == ZombieArchetype.Ranged) ranged++;
                        if (z.Data.isElite) elites++;
                    }
                    peakAlive = Mathf.Max(peakAlive, alive);
                    if (alive >= 10) peakRangedShare = Mathf.Max(peakRangedShare, ranged / (float)alive);
                    var sk = SkillRuntime.Active;
                    var td = ThreatDirector.Instance;
                    csv.Append(Inv($"{(int)run.Duration},{(hp != null ? hp.Current : 0):0},{run.Level},{run.Kills},{alive},{ranged},{elites},{(td != null ? td.CurrentTier : 0)},{run.Coin},{sk?.SlotsUsed(SkillSlot.Skill) ?? 0},{sk?.SlotsUsed(SkillSlot.Stat) ?? 0},{(td != null && td.Surging ? 1 : 0)},{danger:0.00},{XpGained(run)},{xpDropped},{FloorOrbs()}\n"));
                    peakOrbs = Mathf.Max(peakOrbs, FloorOrbs());
                }
                yield return null;
            }

            BillGameCore.Bill.Events?.Unsubscribe(onKill);
            Time.timeScale = 1f;
            PlayerMovement.BotInput = null;
            _input = Vector2.zero;
            bool died = run != null && run.Outcome != RunOutcome.InProgress;
            float seconds = run != null ? run.Duration : 0f;
            var sb = new StringBuilder("{");
            sb.Append(Inv($"\"label\":\"{label}\",\"run\":{index},\"died\":{(died ? "true" : "false")},\"seconds\":{seconds:0},"));
            sb.Append(Inv($"\"level\":{run?.Level ?? 0},\"kills\":{run?.Kills ?? 0},\"coin\":{run?.Coin ?? 0},\"score\":{run?.Score ?? 0},"));
            sb.Append(Inv($"\"first_card\":{firstCardAt:0},\"cards_by_2min\":{cardsBy2},\"chests\":{chests},\"peak_alive\":{peakAlive:0},"));
            sb.Append(Inv($"\"peak_ranged_share\":{peakRangedShare:0.00},\"low_hp_seconds\":{lowHpSeconds:0},\"damage_taken\":{damageTaken:0},"));
            sb.Append(Inv($"\"xp_gained\":{(run != null ? XpGained(run) : 0)},\"xp_dropped\":{xpDropped},\"peak_orbs_floor\":{peakOrbs:0},"));
            sb.Append(Inv($"\"revives_declined\":{revivesDeclined},\"peak_tier\":{run?.PeakThreatTier ?? 0},\"gun\":\"{PlayerProfile.EquippedWeaponId}\","));
            sb.Append($"\"level_times\":[{string.Join(",", levelTimes)}]}}");
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Review", "QA", "botlab"));
            System.IO.Directory.CreateDirectory(dir);
            string stamp = System.DateTime.Now.ToString("MMdd_HHmmss");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"{stamp}_{label}_{index}.csv"), csv.ToString());
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "summary.jsonl"), sb + "\n");
            all.AppendLine(sb.ToString());
            // let the result screen settle before the next restart
            yield return new WaitForSecondsRealtime(1.5f);
        }

        static string Inv(System.FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

        /// <summary>A casual player's pick: an evolution always; with slots free, a new card 70% of
        /// the time (players like new toys); otherwise the first card.</summary>
        static int ChooseCard(List<SkillDef> offer)
        {
            var sk = SkillRuntime.Active;
            for (int i = 0; i < offer.Count; i++) if (offer[i] != null && offer[i].IsEvolution) return i;
            if (sk != null && Random.value < 0.7f)
            {
                var fresh = new List<int>(3);
                for (int i = 0; i < offer.Count; i++)
                {
                    var d = offer[i]; if (d == null || sk.RankOf(d.id) > 0) continue;
                    int cap = d.Slot == SkillSlot.Skill ? SkillCatalogDefs.MaxSkillSlots : d.Slot == SkillSlot.Stat ? SkillCatalogDefs.MaxStatSlots : 99;
                    if (d.Slot == SkillSlot.None || sk.SlotsUsed(d.Slot) < cap) fresh.Add(i);
                }
                if (fresh.Count > 0) return fresh[Random.Range(0, fresh.Count)];
            }
            return 0;
        }

        static int XpGained(RunState run)
        {
            int total = run.Xp;
            for (int l = 1; l < run.Level; l++) total += RunState.XpForLevel(l);
            return total;
        }

        static int FloorOrbs()
        {
            if (_liveField == null) _liveField = typeof(PickupManager).GetField("Live", SF);
            if (!(_liveField?.GetValue(null) is List<Pickup> live)) return 0;
            int n = 0;
            for (int i = 0; i < live.Count; i++) if (live[i] != null && live[i].isActiveAndEnabled && live[i].Effect == PickupEffect.Xp) n++;
            return n;
        }

        // ------------------------------------------------------------------ steering

        static readonly List<Pickup> PickupScratch = new(128);
        readonly List<Vector3> _beacons = new(4);
        bool _prizeTarget;
        float _beaconScanAt;
        static System.Reflection.FieldInfo _liveField;

        void Steer(Health hp, out float danger)
        {
            var me = PlayerMovement.Instance;
            danger = 0f;
            if (me == null) { _input = Vector2.zero; return; }
            Vector3 p = me.transform.position;

            // 1. away from the crowd: inverse-square push from everything within 9 m (ranged and
            //    elites count more), so the bot kites instead of standing in the pack.
            Vector3 flee = Vector3.zero;
            var zs = ZombieManager.Alive;
            for (int i = 0; i < zs.Count; i++)
            {
                var z = zs[i]; if (z == null) continue;
                Vector3 d = p - z.transform.position; d.y = 0f;
                float m = d.magnitude;
                if (m > 9f || m < 0.01f) continue;
                float w = 1f / (m * m);
                if (z.Data != null && (z.Data.isElite || z.Data.archetype == ZombieArchetype.Ranged)) w *= 2f;
                flee += d / m * w;
            }
            danger = flee.magnitude;

            // 1b. keep out of Boss Beacon rings: a new player reads the red ring and walks round it.
            if (Time.time >= _beaconScanAt)
            {
                _beaconScanAt = Time.time + 0.5f;
                _beacons.Clear();
                foreach (var s in FindObjectsByType<ZombieWar.Stations.Station>(FindObjectsSortMode.None))
                    if (s != null && !s.Finished && s.Anchor.kind == ZombieWar.Stations.StationKind.BossBeacon) _beacons.Add(s.transform.position);
            }
            for (int i = 0; i < _beacons.Count; i++)
            {
                Vector3 d = p - _beacons[i]; d.y = 0f;
                float m = d.magnitude;
                if (m < 7f && m > 0.01f) flee += d / m * (2f / Mathf.Max(1f, m));
            }

            // 2. toward the most useful pickup: health when hurt, then items and chests, then XP.
            float hpFrac = hp != null && hp.Max > 0f ? hp.Current / hp.Max : 1f;
            Vector3 toPickup = Vector3.zero; float best = float.MaxValue;
            _prizeTarget = false;
            if (_liveField == null) _liveField = typeof(PickupManager).GetField("Live", SF);
            if (_liveField?.GetValue(null) is List<Pickup> live)
            {
                PickupScratch.Clear(); PickupScratch.AddRange(live);
                for (int i = 0; i < PickupScratch.Count; i++)
                {
                    var pk = PickupScratch[i]; if (pk == null || !pk.isActiveAndEnabled) continue;
                    Vector3 d = pk.transform.position - p; d.y = 0f;
                    float m = d.magnitude; if (m > 14f) continue;
                    float cost = m;
                    bool prize = pk.Effect == PickupEffect.Chest || (pk.Effect == PickupEffect.Health && hpFrac < 0.7f);
                    if (pk.Effect == PickupEffect.Health) cost *= hpFrac < 0.7f ? 0.2f : 3f;
                    else if (pk.Effect == PickupEffect.Chest) cost *= 0.2f;
                    else if (pk.Effect == PickupEffect.Bomb || pk.Effect == PickupEffect.Freeze || pk.Effect == PickupEffect.Magnet) cost *= 0.5f;
                    if (cost < best) { best = cost; toPickup = d / Mathf.Max(m, 0.01f); _prizeTarget = prize; }
                }
            }

            // 3. wander: a slow drift so the run explores and passes stations.
            _wanderAngle += Time.deltaTime * 0.15f * (Mathf.PerlinNoise(Time.time * 0.05f, 0.3f) - 0.5f) * 4f;
            Vector3 wander = new Vector3(Mathf.Cos(_wanderAngle), 0f, Mathf.Sin(_wanderAngle));

            // An average casual: enemies within ~3 m dominate, XP is collected while the crowd is a few
            // metres off, and a chest or a needed heal is worth pushing through for.
            float fleeW = Mathf.Clamp01(danger * 3f) * (hpFrac < 0.4f ? 1.5f : 1f);
            float greed = _prizeTarget ? 1.2f : 1f - Mathf.Clamp01(danger * 1.5f);
            Vector3 dir = flee.normalized * fleeW * 2f + toPickup * greed * 1.2f + wander * 0.35f;

            // 4. unstick against walls and props
            if (Time.time >= _stuckCheckAt)
            {
                if ((p - _lastPos).sqrMagnitude < 0.25f && _input.sqrMagnitude > 0.2f)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    _unstickDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    _unstickUntil = Time.time + 1.2f;
                    _wanderAngle = a;
                }
                _lastPos = p;
                _stuckCheckAt = Time.time + 1.5f;
            }
            if (Time.time < _unstickUntil) { _input = _unstickDir; return; }

            var v = new Vector2(dir.x, dir.z);
            _input = v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.zero;
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; PlayerMovement.BotInput = null; }
        }
    }
}
#endif
