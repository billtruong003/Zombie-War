using System;
using System.Collections.Generic;
using BillGameCore;
using UnityEngine;
using ZombieWar.Stations;
using ZombieWar.Threat;
using Random = UnityEngine.Random;

namespace ZombieWar.Audio
{
    /// <summary>
    /// The agents' radio outside the FTUE (script v3, Review/Lore/HordeCall_Radio_VO_v3.md): run events,
    /// menu reactions, critiques, calm-moment chatter, two-agent conversations, sector briefings and a
    /// few easter eggs. It decides WHEN a line plays; <see cref="RadioVoice"/> plays it.
    /// Rules (script v3, "luật phát"):
    /// <list type="bullet">
    /// <item>one line at a time; in a run, at most one non-urgent line every <see cref="RunGap"/> s;</item>
    /// <item>chatter only when calm (no surge, no level-up, past the first 45 s), every 60–90 s, no repeat
    /// until the bag is empty;</item>
    /// <item>at most one conversation per run and one per menu visit; each plays once ever;</item>
    /// <item>critiques once a day each; easter eggs once ever (holidays once a year).</item>
    /// </list>
    /// Nothing here plays while the FTUE still has lines to say for the moment (FtueVoice goes first).
    /// </summary>
    public sealed class RadioDirector : MonoBehaviour
    {
        const float RunGap = 20f, PollEvery = 0.5f, CalmAfter = 45f, StillSeconds = 6f;

        static RadioDirector _instance;
        bool _subscribed;
        float _nextPoll, _lastLineAt = -999f, _nextChatAt, _lastHitAt, _surgeUntil;
        RunState _run;
        int _killMark, _relaysThisRun, _deathsToday;
        bool _dialogueThisRun, _surgeSeen, _bestCrossed, _longRunSaid, _noHitSaid, _revived, _tier10;
        float _revivedAt;
        Vector3 _lastPos; float _stillSince;
        readonly List<string> _chatBag = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("[ZombieWar.RadioDirector]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RadioDirector>();
        }

        // ------------------------------------------------------------ line tables
        static readonly string[] Chatter =
        {
            "vo_riley_chat_01", "vo_riley_chat_02", "vo_riley_chat_03", "vo_riley_chat_04", "vo_riley_chat_05", "vo_riley_chat_06",
            "vo_lukas_chat_01", "vo_lukas_chat_02", "vo_lukas_chat_03", "vo_lukas_chat_04", "vo_lukas_chat_05",
            "vo_chen_chat_01", "vo_chen_chat_02", "vo_chen_chat_03", "vo_chen_chat_04", "vo_chen_chat_05",
            "vo_kaito_chat_01", "vo_kaito_chat_02", "vo_kaito_chat_03", "vo_kaito_chat_04", "vo_kaito_chat_05",
            "vo_jiho_chat_01", "vo_jiho_chat_02", "vo_jiho_chat_03", "vo_jiho_chat_04", "vo_jiho_chat_05",
            "vo_mai_chat_01", "vo_mai_chat_02", "vo_mai_chat_03", "vo_mai_chat_04", "vo_mai_chat_05",
        };

        /// <summary>Conversation n (1-based) → its lines in order (script v3, dialogues 01–19).</summary>
        static readonly string[][] Dialogues =
        {
            new[] { "vo_dlg_01_1_kaito", "vo_dlg_01_2_jiho", "vo_dlg_01_3_kaito", "vo_dlg_01_4_jiho" },
            new[] { "vo_dlg_02_1_lukas", "vo_dlg_02_2_chen" },
            new[] { "vo_dlg_03_1_mai", "vo_dlg_03_2_riley", "vo_dlg_03_3_mai" },
            new[] { "vo_dlg_04_1_mai", "vo_dlg_04_2_jiho", "vo_dlg_04_3_mai", "vo_dlg_04_4_jiho" },
            new[] { "vo_dlg_05_1_kaito", "vo_dlg_05_2_lukas", "vo_dlg_05_3_kaito" },
            new[] { "vo_dlg_06_1_riley", "vo_dlg_06_2_chen", "vo_dlg_06_3_riley", "vo_dlg_06_4_chen" },
            new[] { "vo_dlg_07_1_mai", "vo_dlg_07_2_kaito", "vo_dlg_07_3_mai", "vo_dlg_07_4_kaito" },
            new[] { "vo_dlg_08_1_jiho", "vo_dlg_08_2_lukas", "vo_dlg_08_3_jiho", "vo_dlg_08_4_lukas" },
            new[] { "vo_dlg_09_1_kaito", "vo_dlg_09_2_riley", "vo_dlg_09_3_chen" },
            new[] { "vo_dlg_10_1_mai", "vo_dlg_10_2_chen", "vo_dlg_10_3_mai" },
            new[] { "vo_dlg_11_1_kaito", "vo_dlg_11_2_jiho", "vo_dlg_11_3_kaito" },
            new[] { "vo_dlg_12_1_mai", "vo_dlg_12_2_riley", "vo_dlg_12_3_mai", "vo_dlg_12_4_riley" },
            new[] { "vo_dlg_13_1_kaito", "vo_dlg_13_2_lukas", "vo_dlg_13_3_kaito", "vo_dlg_13_4_lukas" },
            new[] { "vo_dlg_14_1_mai", "vo_dlg_14_2_chen", "vo_dlg_14_3_mai", "vo_dlg_14_4_chen" },
            new[] { "vo_dlg_15_1_kaito", "vo_dlg_15_2_chen", "vo_dlg_15_3_kaito" },
            new[] { "vo_dlg_16_1_mai", "vo_dlg_16_2_lukas", "vo_dlg_16_3_mai", "vo_dlg_16_4_jiho" },
            new[] { "vo_dlg_17_1_kaito", "vo_dlg_17_2_riley", "vo_dlg_17_3_kaito", "vo_dlg_17_4_riley" },
            new[] { "vo_dlg_18_1_mai", "vo_dlg_18_2_chen", "vo_dlg_18_3_mai", "vo_dlg_18_4_chen" },
            new[] { "vo_dlg_19_1_kaito", "vo_dlg_19_2_lukas", "vo_dlg_19_3_chen", "vo_dlg_19_4_lukas" },
        };

        // ------------------------------------------------------------ helpers
        static bool FtueActive => !Ftue.Done(Ftue.Move) || PlayerProfile.RunsPlayed == 0;

        static string Pick(params string[] ids) => ids[Random.Range(0, ids.Length)];

        /// <summary>Plays a line unless the radio is busy or a line went out less than <paramref name="gap"/> s ago.</summary>
        bool Say(string id, float gap = 0f)
        {
            if (RadioVoice.Busy || Time.unscaledTime - _lastLineAt < gap) return false;
            RadioVoice.Say(id);
            _lastLineAt = Time.unscaledTime;
            return true;
        }

        bool SayOnce(string id, float gap = 0f)
        {
            if (RadioVoice.Said(id) || RadioVoice.Busy || Time.unscaledTime - _lastLineAt < gap) return false;
            RadioVoice.SayOnce(id);
            _lastLineAt = Time.unscaledTime;
            return true;
        }

        /// <summary>Once per calendar day (critiques, the daily-crate nag, the 3 a.m. egg).</summary>
        bool SayDaily(string id, float gap = 0f)
        {
            string key = "vo.day." + id;
            int today = DailyRewards.Today;
            if (PlayerPrefs.GetInt(key, -1) == today) return false;
            if (!Say(id, gap)) return false;
            PlayerPrefs.SetInt(key, today);
            return true;
        }

        /// <summary>Queue conversation n (1-based) once ever.</summary>
        bool Converse(int n)
        {
            string flag = $"dlg.{n:00}";
            if (PlayerProfile.HasVoFlag(flag) || RadioVoice.Busy) return false;
            PlayerProfile.MarkVoFlag(flag);
            RadioVoice.SayAll(Dialogues[n - 1]);
            _lastLineAt = Time.unscaledTime;
            return true;
        }

        // ------------------------------------------------------------ calls from screens (via FtueVoice or direct)
        /// <summary>Home after the first run: a greeting, a time-of-day conversation, or nothing.</summary>
        public static void HomeShown(Func<bool> stillOnHome)
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            int hour = DateTime.Now.Hour, runs = PlayerProfile.RunsPlayed;
            if (hour >= 2 && hour < 4 && d.SayDaily("vo_riley_egg_night3am")) return;
            if (d.CameBack()) return;
            if (d.Holiday()) return;
            if (runs == 3 && d.Converse(7)) return;
            if (runs >= 10 && d.Converse(17)) return;
            if (hour >= 22 && d.Converse(12)) return;
            if (hour >= 6 && hour < 10 && d.Converse(16)) return;
            if (DailyRewards.ClaimableCount(DailyRewards.Today) > 0 && d.SayDaily("vo_mai_meta_daily")) return;
            if (Random.value < 0.08f && d.Converse(18)) return;
            if (Random.value < 0.06f && (d.SayOnce("vo_riley_lore_why_us") || d.SayOnce("vo_riley_lore_nightfin"))) return;
            if (Random.value < 0.35f) d.Say(Pick("vo_riley_meta_home_01", "vo_riley_meta_home_02"), 30f);
            // Idle on Home: the mask conversation at 20 s, Ji-ho at 60 s, Kaito's egg at 2 min.
            RadioVoice.After("home.dlg04", 20f, stillOnHome, () => d.Converse(4));
            RadioVoice.Nudge("vo_jiho_sit_afk_home", 60f, stillOnHome);
            RadioVoice.Nudge("vo_kaito_egg_idle_2m", 120f, stillOnHome);
        }

        public static void ArsenalShown()
        {
            var d = _instance;
            if (d == null || FtueActive || !Ftue.Done(Ftue.Gun)) return;
            if (Random.value < 0.15f && d.Converse(5)) return;
            if (Random.value < 0.08f && d.Converse(19)) return;
            if (Random.value < 0.08f && d.SayOnce("vo_lukas_lore_raptor")) return;
            if (Random.value < 0.4f) d.Say(Pick("vo_lukas_meta_arsenal_01", "vo_lukas_meta_arsenal_02"), 30f);
        }

        /// <summary>A gun was bought outside the FTUE. <paramref name="owned"/> counts guns owned now.</summary>
        public static void GunBought(bool legendary, int owned)
        {
            var d = _instance;
            if (d == null || !Ftue.Done(Ftue.Gun)) return;
            if (legendary) { d.Say("vo_lukas_meta_gun_legend"); return; }
            if (owned == 3 && d.Converse(13)) return;
            d.Say("vo_lukas_meta_gun_buy");
        }

        public static void GunStarred()
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            d.Converse(8);
        }

        public static void MissionsShown()
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            if (Random.value < 0.08f && (d.SayOnce("vo_chen_lore_dragon") || d.SayOnce("vo_chen_lore_powers"))) return;
            // Every Daily Op done today: rest is training too.
            if (PlayerProfile.DailyOpsAllClaimed(GameClock.UtcNow) && d.SayDaily("vo_chen_meta_missions_empty")) return;
            if (Random.value < 0.35f) d.Say("vo_chen_meta_missions", 30f);
        }

        public static void GachaShown()
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            if (PlayerProfile.GetPity("gacha.weapon") >= PityNearAt && d.SayDaily("vo_mai_meta_gacha_pity")) return;
            if (Random.value < 0.35f) d.Say("vo_mai_meta_gacha", 30f);
        }

        public static void GachaResult(bool legendary)
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            if (legendary) d.Say("vo_mai_meta_gacha_legend");
        }

        public static void ShopShown()
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            if (Random.value < 0.08f && (d.SayOnce("vo_mai_lore_credits") || d.SayOnce("vo_mai_lore_tiger"))) return;
            if (Random.value < 0.35f) d.Say("vo_mai_meta_shop", 30f);
        }

        public static void SettingsShown()
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            if (Random.value < 0.1f && d.SayOnce("vo_jiho_lore_smog")) return;
            if (Random.value < 0.4f) d.Say("vo_jiho_meta_settings", 30f);
        }

        public static void StudioShown()
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            if (Random.value < 0.12f && d.SayOnce("vo_jiho_lore_the_mask")) return;
            if (Random.value < 0.4f) d.Say("vo_jiho_meta_studio", 30f);
        }

        public static void ProfileShown()
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            if (Random.value < 0.1f && d.SayOnce("vo_kaito_lore_shark")) return;
            if (Random.value < 0.4f) d.Say("vo_kaito_meta_profile", 30f);
        }

        /// <summary>A level-up card offer outside the FTUE.</summary>
        public static void CardOffered()
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (skills != null && skills.SlotsUsed(ZombieWar.Skills.SkillSlot.Stat) >= ZombieWar.Skills.SkillCatalogDefs.MaxStatSlots
                && skills.SlotsUsed(ZombieWar.Skills.SkillSlot.Skill) == 0 && d.SayOnce("vo_chen_sit_all_stats")) return;
            if (Random.value < 0.2f) d.Say("vo_chen_run_levelup", 90f);
        }

        /// <summary>The level-up timer ran out and picked a card for the player.</summary>
        public static void CardAutoPicked()
        {
            var d = _instance;
            if (d != null && !FtueActive) d.SayDaily("vo_chen_sit_reroll");
        }

        /// <summary>An achievement unlocked: its own line, once (backlog #22).</summary>
        public static void AchievementUnlocked(string voiceId)
        {
            var d = _instance;
            if (d != null && !string.IsNullOrEmpty(voiceId)) d.SayOnce(voiceId);
        }

        const int PityNearAt = 75;   // of 90: "it's getting close"

        /// <summary>A gun the player tried to buy costs more than they carry.</summary>
        public static void CantAfford()
        {
            var d = _instance;
            if (d != null && !FtueActive) d.SayDaily("vo_lukas_meta_poor");
        }

        /// <summary>A x10 pull with nothing Epic or better.</summary>
        public static void GachaDry()
        {
            var d = _instance;
            if (d != null && !FtueActive) d.SayDaily("vo_mai_egg_gacha_10x");
        }

        /// <summary>The player put a mask on (Ji-ho's whole look is the mask).</summary>
        public static void MaskWorn()
        {
            var d = _instance;
            if (d != null && !FtueActive) d.SayOnce("vo_jiho_meta_studio_smog");
        }

        /// <summary>The player renamed themselves; an agent's name gets Riley's egg.</summary>
        public static void NameSet(string name)
        {
            var d = _instance;
            if (d == null || string.IsNullOrEmpty(name)) return;
            string n = name.Trim().ToLowerInvariant();
            foreach (var agent in new[] { "riley", "lukas", "chen", "kaito", "ji-ho", "jiho", "mai", "nightfin", "raptor", "dragon", "shark", "smog", "tiger" })
                if (n == agent) { d.SayOnce("vo_riley_egg_name_agent"); return; }
        }

        int _passLevel = -1;

        void OnMissionClaimed(PassMission m)
        {
            if (FtueActive) return;
            int level = PassRewards.Level;
            bool up = _passLevel >= 0 && level > _passLevel;
            _passLevel = level;
            if (up) Say("vo_chen_meta_pass_up", 10f);
            else Say("vo_chen_meta_mission_done", 20f);
        }

        void OnDailyChest(PlayerProfile.DailyChestReward r)
        {
            if (r.streak > 0 && r.streak % PlayerProfile.StreakGunEvery == 0) Say("vo_mai_meta_streak7");
            else if (r.streak == PlayerProfile.StreakDoubleAt) Say("vo_mai_meta_streak3");
            else Say("vo_mai_meta_chest_open");
        }

        /// <summary>The run raised the carried gun's mastery: Chen notes it, louder at the family-bonus levels.</summary>
        public static void MasteryUp(int before, int after)
        {
            var d = _instance;
            if (d == null || FtueActive || after <= before) return;
            bool bonus = (before < GunMastery.BonusLevel1 && after >= GunMastery.BonusLevel1)
                      || (before < GunMastery.BonusLevel2 && after >= GunMastery.BonusLevel2);
            d.Say(bonus ? "vo_chen_meta_mastery_bonus" : "vo_chen_meta_mastery_up", 5f);
        }

        /// <summary>A Boss Beacon walked past without opting in (n-th this run): Kaito teases at 3.</summary>
        public static void BeaconPassed(int n)
        {
            var d = _instance;
            if (d != null && !FtueActive && n >= 3) d.SayOnce("vo_kaito_sit_skip_beacon", 20f);
        }

        /// <summary>A Supply Drop walked past unopened: Mai is a little hurt.</summary>
        public static void DropPassed(int n)
        {
            var d = _instance;
            if (d != null && !FtueActive && n >= 1) d.SayOnce("vo_mai_sit_skip_drop", 20f);
        }

        /// <summary>The player keeps poking a radio portrait.</summary>
        public static void PortraitTapped(int taps)
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            // ">=" so a tap that lands while the radio is busy is not lost: the next tap tries again.
            if (taps >= 10 && d.SayOnce("vo_mai_egg_tap10")) return;
            if (taps >= 5) d.SayOnce("vo_jiho_egg_tap5");
        }

        /// <summary>A gun evolved in the mastery screen (not the in-run skill evolution).</summary>
        public static void GunEvolved()
        {
            var d = _instance;
            if (d != null) d.Say("vo_lukas_meta_gun_evolve");
        }

        /// <summary>Home after three or more days away: Riley's welcome back (once per return).</summary>
        bool CameBack()
        {
            const string key = "vo.lastHomeDay";
            int today = DailyRewards.Today, last = PlayerPrefs.GetInt(key, today);
            PlayerPrefs.SetInt(key, today);
            return today - last >= 3 && Say("vo_riley_meta_comeback");
        }

        public static void EvolutionTaken()
        {
            var d = _instance;
            if (d != null && !FtueActive) d.Say("vo_chen_run_evolve");
        }

        /// <summary>A beacon boss spawned; <paramref name="dataName"/> is its ZombieData asset name.</summary>
        public static void BossSpawned(string dataName)
        {
            var d = _instance;
            if (d == null || FtueActive) return;
            string n = dataName ?? "";
            if (n.Contains("Cactus") && d.SayOnce("vo_kaito_lore_a_thorn")) return;
            if (n.Contains("MoleRat") && d.SayOnce("vo_kaito_lore_a_burrow")) return;
            if ((n.Contains("Bone") || n.Contains("Colossus")) && d.SayOnce("vo_riley_lore_a_bone")) return;
            d.Say("vo_kaito_run_alpha_spawn");
        }

        /// <summary>The result screen is up (any run after the first).</summary>
        public static void RunResult(RunClosure.Result result)
        {
            var d = _instance;
            if (d == null || PlayerProfile.RunsPlayed <= 1) return;
            var s = result.Summary;
            if (s.Kills == 404 && d.SayOnce("vo_jiho_egg_kills_404")) return;
            if (s.Kills == 1337 && d.SayOnce("vo_kaito_egg_kills_1337")) return;
            if (s.Duration >= 600f && d.Converse(15)) return;
            if (result.NewScoreRecord) { d.Say("vo_riley_meta_record"); return; }   // the record is the score (27/09)
            if (result.AccountLevelsGained > 0) { d.Say("vo_chen_meta_acc_up"); return; }
            d.Say(Pick("vo_riley_run_end_01", "vo_riley_run_end_02"));
        }

        // ------------------------------------------------------------ events
        void Update()
        {
            if (!_subscribed && Bill.IsReady) Subscribe();
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollEvery;
            Poll();
        }

        void Subscribe()
        {
            _subscribed = true;
            Bill.Events.Subscribe<ThreatTierChangedEvent>(OnTier);
            _passLevel = PassRewards.Level;
            PlayerProfile.MissionClaimed += OnMissionClaimed;
            PlayerProfile.DailyChestClaimed += OnDailyChest;
            Bill.Events.Subscribe<HordeSurgeEvent>(OnSurge);
            Bill.Events.Subscribe<ZombieKilledEvent>(OnKill);
            Bill.Events.Subscribe<StationCompletedEvent>(OnStation);
            Bill.Events.Subscribe<PlayerDamagedEvent>(OnDamaged);
            Bill.Events.Subscribe<PlayerDiedEvent>(OnDied);
            Bill.Events.Subscribe<PickupCollectedEvent>(OnPickup);
            Bill.Events.Subscribe<RunAbandonRequestedEvent>(OnAbandon);
        }

        void OnDestroy()
        {
            if (_subscribed && Bill.IsReady)
            {
                Bill.Events.Unsubscribe<ThreatTierChangedEvent>(OnTier);
                Bill.Events.Unsubscribe<HordeSurgeEvent>(OnSurge);
                Bill.Events.Unsubscribe<ZombieKilledEvent>(OnKill);
                Bill.Events.Unsubscribe<StationCompletedEvent>(OnStation);
                Bill.Events.Unsubscribe<PlayerDamagedEvent>(OnDamaged);
                Bill.Events.Unsubscribe<PlayerDiedEvent>(OnDied);
                Bill.Events.Unsubscribe<PickupCollectedEvent>(OnPickup);
                Bill.Events.Unsubscribe<RunAbandonRequestedEvent>(OnAbandon);
            }
            if (_instance == this) _instance = null;
        }

        void OnTier(ThreatTierChangedEvent e)
        {
            if (FtueActive || !e.Rising || e.Tier < 2) return;
            if (e.Tier >= 10 && !_tier10) { _tier10 = true; Say("vo_riley_run_tier_10"); return; }
            Say(Pick("vo_riley_run_tier_01", "vo_riley_run_tier_02"), 60f);
        }

        void OnSurge(HordeSurgeEvent e)
        {
            if (!e.Started) { _surgeUntil = Time.unscaledTime + 8f; return; }
            _surgeUntil = float.MaxValue;
            if (FtueActive) return;
            if (!_surgeSeen) { _surgeSeen = true; if (Random.value < 0.5f && Converse(14)) return; }
            Say(Pick("vo_kaito_run_surge_01", "vo_kaito_run_surge_02"), 45f);
        }

        void OnKill(ZombieKilledEvent e)
        {
            if (FtueActive || e.Data == null || !e.Data.isElite) return;
            if (Random.value < 0.5f) Say(Pick("vo_lukas_run_elite_01", "vo_lukas_run_elite_02"), 40f);
        }

        void OnStation(StationCompletedEvent e)
        {
            if (FtueActive) return;
            switch (e.Kind)
            {
                case StationKind.BossBeacon:
                    if (RadioVoice.Said("vo_riley_ftue_beacon_kill") && !Converse(9)) Say("vo_riley_run_alpha_kill");
                    break;
                case StationKind.SupplyDrop:
                    if (RadioVoice.Said("vo_mai_ftue_drop_done")) Converse(3);
                    break;
                case StationKind.SignalRelay:
                    if (++_relaysThisRun == 3 && Converse(11)) break;
                    if (Random.value < 0.4f) Say(Pick("vo_jiho_run_station_done", "vo_jiho_run_station_done_2"), RunGap);
                    break;
                case StationKind.SupplyCache:
                    if (Random.value < 0.4f) Say(Pick("vo_jiho_run_station_done", "vo_jiho_run_station_done_2"), RunGap);
                    break;
                case StationKind.HealZone:
                    var hp = PlayerMovement.Instance != null ? PlayerMovement.Instance.GetComponent<Health>() : null;
                    if (hp != null && hp.Current >= hp.Max - 0.01f) SayDaily("vo_jiho_sit_full_hp_heal");
                    break;
            }
        }

        void OnDamaged(PlayerDamagedEvent e)
        {
            _lastHitAt = Time.unscaledTime;
            if (FtueActive) return;
            if (e.Normalized < 0.25f) Say(Pick("vo_mai_run_lowhp_01", "vo_mai_run_lowhp_02"), 60f);
        }

        void OnDied(PlayerDiedEvent e)
        {
            if (FtueActive || !Ftue.Done(Ftue.Revive)) return;
            var run = RunState.Current;
            if (run != null && run.Kills <= 1 && SayOnce("vo_lukas_egg_die_at_kill1")) return;
            if (run != null && run.Duration < 60f)
            {
                _deathsToday++;
                if (_deathsToday >= 2 && SayDaily("vo_riley_sit_early_death_2")) return;
                if (SayDaily("vo_chen_sit_early_death")) return;
            }
            Say(Pick("vo_riley_run_death_01", "vo_riley_run_death_02"));
            _revived = true; _revivedAt = Time.unscaledTime;   // if a revive follows, the comeback clock runs
        }

        void OnPickup(PickupCollectedEvent e)
        {
            if (FtueActive || Random.value > 0.5f) return;
            if (e.Effect == PickupEffect.Magnet && RadioVoice.Said("vo_mai_ftue_magnet")) Say("vo_mai_run_magnet", 30f);
            else if (e.Effect == PickupEffect.Bomb && RadioVoice.Said("vo_lukas_ftue_bomb")) Say("vo_lukas_run_bomb", 30f);
            else if (e.Effect == PickupEffect.Freeze && RadioVoice.Said("vo_jiho_ftue_freeze")) Say("vo_jiho_run_freeze", 30f);
        }

        void OnAbandon(RunAbandonRequestedEvent e)
        {
            if (!FtueActive) Say("vo_riley_meta_quit");
        }

        // ------------------------------------------------------------ polled run state
        void Poll()
        {
            var run = RunState.Current;
            if (run != _run) StartRun(run);
            if (run == null || run.IsOver || FtueActive || Time.timeScale <= 0f) return;
            float now = Time.unscaledTime, t = run.Duration;

            // Kill milestones, best time, a long run.
            if (run.Kills >= 1000 && _killMark < 1000) { _killMark = 1000; Say("vo_kaito_run_kills_1000"); }
            else if (run.Kills >= 500 && _killMark < 500) { _killMark = 500; Say("vo_kaito_run_kills_500"); }
            else if (run.Kills >= 100 && _killMark < 100) { _killMark = 100; Say("vo_kaito_run_kills_100"); }
            float best = PlayerProfile.BestSurvivalSeconds;
            if (!_bestCrossed && best > 30f && t > best) { _bestCrossed = true; Say("vo_riley_run_best_time"); }
            if (!_longRunSaid && t >= 900f) { _longRunSaid = Say("vo_mai_sit_long_run"); }
            if (!_noHitSaid && t > 120f && now - _lastHitAt >= 120f) { _noHitSaid = Say("vo_chen_sit_no_hit_2m", RunGap); }
            if (_revived && now - _revivedAt >= 180f) { _revived = false; SayOnce("vo_riley_sit_comeback_win", RunGap); }

            // Standing still.
            var player = PlayerMovement.Instance;
            if (player != null)
            {
                var p = player.transform.position;
                if ((p - _lastPos).sqrMagnitude > 0.04f) { _lastPos = p; _stillSince = now; }
                else if (now - _stillSince > StillSeconds && t > CalmAfter) { SayDaily("vo_riley_sit_still", RunGap); _stillSince = now; }
            }

            // Calm moments: one conversation per run, otherwise chatter every 60–90 s.
            bool calm = t > CalmAfter && now > _surgeUntil && now - _lastLineAt > RunGap && !RadioVoice.Busy;
            if (!calm || now < _nextChatAt) return;
            _nextChatAt = now + Random.Range(60f, 90f);
            if (!_dialogueThisRun)
            {
                bool pistol = (PlayerProfile.EquippedWeaponId ?? "").IndexOf("pistol", StringComparison.OrdinalIgnoreCase) >= 0;
                if ((t > 180f && Converse(1)) || (pistol && Converse(2))) { _dialogueThisRun = true; return; }
            }
            if (_chatBag.Count == 0) { _chatBag.AddRange(Chatter); Shuffle(_chatBag); }
            var id = _chatBag[_chatBag.Count - 1];
            _chatBag.RemoveAt(_chatBag.Count - 1);
            Say(id);
        }

        void StartRun(RunState run)
        {
            _run = run;
            _killMark = 0; _relaysThisRun = 0;
            _dialogueThisRun = _surgeSeen = _bestCrossed = _longRunSaid = _noHitSaid = _revived = _tier10 = false;
            _lastHitAt = _stillSince = Time.unscaledTime;
            _nextChatAt = Time.unscaledTime + CalmAfter;
            _surgeUntil = 0f;
            if (run == null || FtueActive) return;
            // The first deployment to each sector gets its briefing; otherwise a deploy line.
            var theme = ZombieWar.World.BakedMapStreamer.Active != null ? ZombieWar.World.BakedMapStreamer.Active.Theme : null;
            if (theme != null && !string.IsNullOrEmpty(theme.briefingLine) && SayOnce(theme.briefingLine))
            {
                if (theme.briefingDialogue >= 0) Converse(theme.briefingDialogue);
                return;
            }
            // The hoarder and the pistol loyalist, once a day each.
            if (PlayerProfile.RunsPlayed >= 3 && PlayerProfile.Coin >= 2000 && SayDaily("vo_mai_sit_hoard")) return;
            if (PlayerProfile.RunsPlayed >= 10 && (PlayerProfile.EquippedWeaponId ?? "").IndexOf("pistol", StringComparison.OrdinalIgnoreCase) >= 0
                && SayDaily("vo_lukas_sit_same_gun")) return;
            Say(Pick("vo_riley_meta_deploy_01", "vo_riley_meta_deploy_02"));
        }

        /// <summary>Holiday eggs, once a year each (fixed-date ones; lunar dates use a small table).</summary>
        bool Holiday()
        {
            var n = DateTime.Now;
            string y = n.Year.ToString();
            bool On(string id, bool when) => when && !PlayerProfile.HasVoFlag("year." + y + "." + id)
                                             && Say(id) && Mark("year." + y + "." + id);
            bool Mark(string f) { PlayerProfile.MarkVoFlag(f); return true; }
            bool lunar = LunarNewYear.TryGetValue(n.Year, out var ly) && Math.Abs((n.Date - ly).TotalDays) <= 3;
            bool chuseok = Chuseok.TryGetValue(n.Year, out var cy) && Math.Abs((n.Date - cy).TotalDays) <= 2;
            return On("vo_mai_egg_tet", lunar) || On("vo_chen_egg_lunar", lunar)
                || On("vo_lukas_egg_oktober", (n.Month == 9 && n.Day >= 19) || (n.Month == 10 && n.Day <= 4))
                || On("vo_riley_egg_july4", n.Month == 7 && n.Day == 4)
                || On("vo_jiho_egg_chuseok", chuseok)
                || On("vo_kaito_egg_goldenweek", (n.Month == 4 && n.Day >= 29) || (n.Month == 5 && n.Day <= 5));
        }

        static readonly Dictionary<int, DateTime> LunarNewYear = new()
        {
            [2027] = new DateTime(2027, 2, 6), [2028] = new DateTime(2028, 1, 26), [2029] = new DateTime(2029, 2, 13),
            [2030] = new DateTime(2030, 2, 3), [2031] = new DateTime(2031, 1, 23),
        };

        static readonly Dictionary<int, DateTime> Chuseok = new()
        {
            [2026] = new DateTime(2026, 9, 25), [2027] = new DateTime(2027, 9, 15), [2028] = new DateTime(2028, 10, 3),
            [2029] = new DateTime(2029, 9, 22), [2030] = new DateTime(2030, 9, 12),
        };

        static void Shuffle(List<string> list)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
        }
    }
}
