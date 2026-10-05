using System.Collections.Generic;
using BillGameCore;
using UnityEngine;
using ZombieWar.Stations;
using ZombieWar.Threat;
using ZombieWar.World;

namespace ZombieWar
{
    /// <summary>
    /// Watches the game for the ten achievements (backlog #22): run events for the in-run ones,
    /// profile changes for guns owned and coins held. Lives for the whole session; an unlock pays its
    /// gems (<see cref="Achievements.Unlock"/>), says its radio line and shows a toast.
    /// </summary>
    public sealed class AchievementTracker : MonoBehaviour
    {
        static AchievementTracker _instance;
        bool _subscribed;
        RunState _run;
        float _lastHitAt, _nextPoll;
        readonly HashSet<StationKind> _stations = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("[ZombieWar.Achievements]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AchievementTracker>();
        }

        void OnEnable()
        {
            Achievements.Unlocked += OnUnlocked;
            PlayerProfile.WalletChanged += Achievements.CheckProfile;
            PlayerProfile.LoadoutChanged += Achievements.CheckProfile;
        }

        void OnDisable()
        {
            Achievements.Unlocked -= OnUnlocked;
            PlayerProfile.WalletChanged -= Achievements.CheckProfile;
            PlayerProfile.LoadoutChanged -= Achievements.CheckProfile;
            if (!_subscribed || !Bill.IsReady) return;
            Bill.Events.Unsubscribe<ZombieKilledEvent>(OnKill);
            Bill.Events.Unsubscribe<ThreatTierChangedEvent>(OnTier);
            Bill.Events.Unsubscribe<StationCompletedEvent>(OnStation);
            Bill.Events.Unsubscribe<PlayerDamagedEvent>(OnDamaged);
            _subscribed = false;
        }

        void Update()
        {
            if (!_subscribed && Bill.IsReady)
            {
                _subscribed = true;
                Bill.Events.Subscribe<ZombieKilledEvent>(OnKill);
                Bill.Events.Subscribe<ThreatTierChangedEvent>(OnTier);
                Bill.Events.Subscribe<StationCompletedEvent>(OnStation);
                Bill.Events.Subscribe<PlayerDamagedEvent>(OnDamaged);
                Achievements.CheckProfile();
            }
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.5f;

            var run = RunState.Current;
            if (run != _run)
            {
                _run = run;
                _stations.Clear();
                _lastHitAt = 0f;
                if (run != null && MapTheme.CurrentId == "tundra") Achievements.Unlock(Achievements.Whiteout);
            }
            if (run == null || run.IsOver) return;
            if (run.Duration >= 600f) Achievements.Unlock(Achievements.Survive10);
            if (run.Duration - _lastHitAt >= 180f) Achievements.Unlock(Achievements.NoHit3);
        }

        void OnKill(ZombieKilledEvent e)
        {
            if (e.Data != null && e.Data.isElite) Achievements.Unlock(Achievements.FirstAlpha);
            var run = RunState.Current;
            if (run != null && run.Kills >= 1000) Achievements.Unlock(Achievements.Run1000);
        }

        void OnTier(ThreatTierChangedEvent e)
        {
            if (e.Tier >= 10) Achievements.Unlock(Achievements.Threat10);
        }

        void OnStation(StationCompletedEvent e)
        {
            _stations.Add(e.Kind);
            if (_stations.Count >= Achievements.StationKindCount) Achievements.Unlock(Achievements.AllStations);
        }

        void OnDamaged(PlayerDamagedEvent e)
        {
            var run = RunState.Current;
            if (run != null && e.Amount > 0f) _lastHitAt = run.Duration;
        }

        static void OnUnlocked(Achievements.Def def)
        {
            ZombieWar.UI.Toast.Show($"ACHIEVEMENT · {def.title} · +{def.gems} GEMS", 2.6f);
            ZombieWar.Audio.RadioDirector.AchievementUnlocked(def.voice);
        }
    }
}
