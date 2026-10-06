using System.Collections.Generic;
using BillGameCore;
using UnityEngine;

namespace ZombieWar.Online
{
    /// <summary>
    /// Turns the game's existing signals into the analytics events of Docs/Plans/ANALYTICS_EVENTS.md,
    /// so gameplay code does not need to know about analytics. Events that have no signal to listen
    /// to are logged at their source (skill_pick in SkillRuntime, run_revive in ReviveRules,
    /// gacha_pull in GachaBanners, ad events in AdService/RewardedAds).
    /// No personal data goes out: ids of game content and numbers only.
    /// </summary>
    public static class AnalyticsHooks
    {
        static bool _installed, _snapshot;
        static long _coin, _gem, _tickets;

        /// <summary>Set while the whole profile is swapped (cloud restore), so the swap is not
        /// reported as currency earned and guns unlocked.</summary>
        public static bool Muted;
        static readonly HashSet<string> Owned = new();
        static readonly Dictionary<string, int> Levels = new();
        static readonly HashSet<string> Evolved = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _installed = false; _snapshot = false; Muted = false; Owned.Clear(); Levels.Clear(); Evolved.Clear(); }

        /// <summary>Called by BootstrapEntry once Bill is ready. Every handler is a named method removed
        /// before it is added, so a second Play session in the editor (domain reload off, some of these
        /// events are not reset) never subscribes twice.</summary>
        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            TakeSnapshot();

            RunScope.Register(OnRunStart);
            Bill.Events.Unsubscribe<RunFinishedEvent>(OnRunFinished); Bill.Events.Subscribe<RunFinishedEvent>(OnRunFinished);
            Bill.Events.Unsubscribe<FtueStepEvent>(OnFtueStep); Bill.Events.Subscribe<FtueStepEvent>(OnFtueStep);
            Bill.Events.Unsubscribe<Stations.StationCompletedEvent>(OnStation); Bill.Events.Subscribe<Stations.StationCompletedEvent>(OnStation);
            RunState.LevelsGained -= OnLevels; RunState.LevelsGained += OnLevels;
            Bosses.TitanBoss.Spawned -= OnBossSpawn; Bosses.TitanBoss.Spawned += OnBossSpawn;
            Bosses.TitanBoss.Died -= OnBossDied; Bosses.TitanBoss.Died += OnBossDied;
            Achievements.Unlocked -= OnAchievement; Achievements.Unlocked += OnAchievement;
            PlayerProfile.DailyChestClaimed -= OnDailyChest; PlayerProfile.DailyChestClaimed += OnDailyChest;
            PlayerProfile.MissionClaimed -= OnMission; PlayerProfile.MissionClaimed += OnMission;
            PlayerProfile.WalletChanged -= OnProfileChanged; PlayerProfile.WalletChanged += OnProfileChanged;
            PlayerProfile.LoadoutChanged -= OnProfileChanged; PlayerProfile.LoadoutChanged += OnProfileChanged;
            GraphicsTier.Changed -= OnTier; GraphicsTier.Changed += OnTier;
            GameAnalytics.SetUserProperty("graphics_tier", GraphicsTier.Current.ToString());
        }

        static void OnFtueStep(FtueStepEvent e) => GameAnalytics.Log("ftue_step", ("step", e.Step));
        static void OnStation(Stations.StationCompletedEvent e) => GameAnalytics.Log("station_complete", ("kind", e.Kind.ToString()), ("seconds", RunSeconds()));
        static void OnLevels(int _) => GameAnalytics.Log("level_up", ("level", RunState.Current?.Level ?? 0), ("seconds", RunSeconds()));
        static void OnBossSpawn(Bosses.TitanBoss _) => GameAnalytics.Log("boss_spawn", ("boss", "titan"), ("seconds", RunSeconds()));
        static void OnBossDied(Bosses.TitanBoss _) => GameAnalytics.Log("boss_kill", ("boss", "titan"), ("seconds", RunSeconds()));
        static void OnAchievement(Achievements.Def d) => GameAnalytics.Log("achievement_unlock", ("achievement", d.id));
        static void OnDailyChest(PlayerProfile.DailyChestReward r) => GameAnalytics.Log("daily_claim", ("streak", r.streak), ("gems", r.gems));
        static void OnMission(PassMission m) => GameAnalytics.Log("mission_claim", ("mission", Short(m.id)), ("scope", m.scope.ToString()));
        static void OnTier(GraphicsTier.Level l) => GameAnalytics.SetUserProperty("graphics_tier", l.ToString());

        static int RunSeconds() => Mathf.FloorToInt(RunState.Current?.Duration ?? 0f);

        // Firebase caps parameter values at 100 characters; mission ids carry their rules.
        static string Short(string id) => id == null ? "" : id.Length <= 100 ? id : id.Substring(0, 100);

        static void OnRunStart()
        {
            GameAnalytics.Log("run_start", ("map", World.MapTheme.CurrentId), ("weapon", PlayerProfile.EquippedWeaponId ?? ""),
                ("weapon_level", PlayerProfile.GetWeaponLevel(PlayerProfile.EquippedWeaponId)), ("run_index", PlayerProfile.RunsPlayed + 1));
        }

        static void OnRunFinished(RunFinishedEvent e)
        {
            var s = e.Summary;
            GameAnalytics.Log("run_end", ("map", World.MapTheme.CurrentId), ("weapon", PlayerProfile.EquippedWeaponId ?? ""),
                ("outcome", s.Outcome == RunOutcome.Died ? "died" : "left"), ("seconds", Mathf.FloorToInt(s.Duration)),
                ("kills", s.Kills), ("level", s.Level), ("threat_tier", s.PeakThreatTier), ("score", s.Score),
                ("coins", e.Result.BankedCoin), ("gems", s.Gem), ("revives", ReviveRules.Used));
            if (e.Result.NewScoreRecord) GameAnalytics.Log("new_best", ("kind", "score"), ("value", s.Score));
            if (e.Result.NewSurvivalRecord) GameAnalytics.Log("new_best", ("kind", "time"), ("value", Mathf.FloorToInt(s.Duration)));
            int runs = PlayerProfile.RunsPlayed;
            GameAnalytics.SetUserProperty("runs_bucket", runs <= 0 ? "0" : runs <= 5 ? "1-5" : runs <= 20 ? "6-20" : "21+");
        }

        public static void TakeSnapshot()
        {
            if (!PlayerProfile.HasProfile) return;
            _snapshot = true;
            _coin = PlayerProfile.Coin; _gem = PlayerProfile.Gem; _tickets = PlayerProfile.Tickets;
            Owned.Clear(); Levels.Clear(); Evolved.Clear();
            foreach (var id in PlayerProfile.OwnedWeaponIds)
            {
                Owned.Add(id);
                Levels[id] = PlayerProfile.GetWeaponLevel(id);
                if (PlayerProfile.IsGunEvolved(id)) Evolved.Add(id);
            }
        }

        /// <summary>Currency and gun changes, found by comparing with the last snapshot. Inside a run
        /// the snapshot just follows along: the run's earnings are reported once, by run_end.</summary>
        static void OnProfileChanged()
        {
            if (Muted) return;
            bool inRun = RunState.Current != null && !RunState.Current.IsOver;
            if (!inRun && _snapshot)
            {
                Currency("coin", PlayerProfile.Coin - _coin);
                Currency("gem", PlayerProfile.Gem - _gem);
                Currency("ticket", PlayerProfile.Tickets - _tickets);
                foreach (var id in PlayerProfile.OwnedWeaponIds)
                {
                    if (!Owned.Contains(id)) GameAnalytics.Log("gun_unlock", ("weapon", id));
                    else if (PlayerProfile.GetWeaponLevel(id) > (Levels.TryGetValue(id, out var l) ? l : 1))
                        GameAnalytics.Log("gun_upgrade", ("weapon", id), ("level", PlayerProfile.GetWeaponLevel(id)));
                    if (PlayerProfile.IsGunEvolved(id) && !Evolved.Contains(id)) GameAnalytics.Log("gun_evolve", ("weapon", id));
                }
            }
            TakeSnapshot();
        }

        static void Currency(string name, long delta)
        {
            if (delta > 0) GameAnalytics.Log("earn_virtual_currency", ("virtual_currency_name", name), ("value", delta));
            else if (delta < 0) GameAnalytics.Log("spend_virtual_currency", ("virtual_currency_name", name), ("value", -delta));
        }
    }
}
