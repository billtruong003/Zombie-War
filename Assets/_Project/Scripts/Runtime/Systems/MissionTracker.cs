using System;
using System.Collections.Generic;
using BillGameCore;
using UnityEngine;
using ZombieWar.Stations;

namespace ZombieWar
{
    /// <summary>
    /// Feeds Battle Pass missions from typed gameplay events.
    ///
    /// Event-driven, never polled: each gameplay signal is translated into at most one increment per
    /// active mission of the matching metric. Missions not in today's rotation are ignored entirely,
    /// so a kill cannot quietly bank progress toward something the player cannot see or claim.
    /// </summary>
    public class MissionTracker : MonoBehaviour
    {
        private readonly List<PassMission> _active = new List<PassMission>();

        private void OnEnable()
        {
            PlayerProfile.RefreshMissionWindow(DateTime.UtcNow);
            _active.Clear();
            _active.AddRange(PassMissions.ActiveFor(DateTime.UtcNow));

            Bill.Events?.Subscribe<ZombieKilledEvent>(OnZombieKilled);
            Bill.Events?.Subscribe<StationCompletedEvent>(OnStationCompleted);
            Bill.Events?.Subscribe<RunFinishedEvent>(OnRunFinished);
        }

        private void OnDisable()
        {
            Bill.Events?.Unsubscribe<ZombieKilledEvent>(OnZombieKilled);
            Bill.Events?.Unsubscribe<StationCompletedEvent>(OnStationCompleted);
            Bill.Events?.Unsubscribe<RunFinishedEvent>(OnRunFinished);
        }

        private void OnZombieKilled(ZombieKilledEvent e)
        {
            var data = e.Data;
            if (data == null) return;

            Report(MissionMetric.KillAny, 1);

            switch (data.archetype)
            {
                case ZombieArchetype.Runner:   Report(MissionMetric.KillRunner, 1); break;
                case ZombieArchetype.Ranged:   Report(MissionMetric.KillRanged, 1); break;
                case ZombieArchetype.Burrower: Report(MissionMetric.KillBurrower, 1); break;
            }

            if (data.isElite) Report(MissionMetric.KillElite, 1);
        }

        private void OnStationCompleted(StationCompletedEvent e)
        {
            Report(MissionMetric.CompleteStation, 1);
            if (e.Kind == StationKind.BossBeacon) Report(MissionMetric.DefeatBoss, 1);
        }

        private void OnRunFinished(RunFinishedEvent e)
        {
            var s = e.Summary;
            Report(MissionMetric.FinishRun, 1);

            // Coin missions count what the run actually earned, matching the ledger the player saw.
            if (s.Coin > 0) Report(MissionMetric.CollectCoin, (int)Math.Min(int.MaxValue, s.Coin));

            // Records are states, not counters: a 6-minute run completes "survive 5 minutes" once,
            // it does not add six to it.
            RaiseTo(MissionMetric.SurviveMinutes, Mathf.FloorToInt(s.Duration / 60f));
            RaiseTo(MissionMetric.ReachThreatTier, s.PeakThreatTier);
        }

        /// <summary>Called by the level-up UI when the player takes a card.</summary>
        public static void ReportCardChosen() => ReportStatic(MissionMetric.ChooseCard, 1);

        private void Report(MissionMetric metric, int amount)
        {
            if (amount <= 0) return;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].metric == metric)
                    PlayerProfile.AddMissionProgress(_active[i].id, amount);
        }

        private void RaiseTo(MissionMetric metric, int value)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].metric != metric) continue;
                int current = PlayerProfile.GetMissionProgress(_active[i].id);
                if (value > current) PlayerProfile.AddMissionProgress(_active[i].id, value - current);
            }
        }

        /// <summary>Static path for callers that have no tracker reference. Resolves the active set
        /// on demand - slightly more work per call, but card picks are rare.</summary>
        private static void ReportStatic(MissionMetric metric, int amount)
        {
            if (amount <= 0) return;
            foreach (var m in PassMissions.ActiveFor(DateTime.UtcNow))
                if (m.metric == metric)
                    PlayerProfile.AddMissionProgress(m.id, amount);
        }
    }
}
