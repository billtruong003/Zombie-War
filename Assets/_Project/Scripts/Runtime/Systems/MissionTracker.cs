using System;
using System.Collections.Generic;
using BillGameCore;
using UnityEngine;

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
            Bill.Events?.Subscribe<WaveClearedEvent>(OnWaveCleared);
            Bill.Events?.Subscribe<RunFinishedEvent>(OnRunFinished);
        }

        private void OnDisable()
        {
            Bill.Events?.Unsubscribe<ZombieKilledEvent>(OnZombieKilled);
            Bill.Events?.Unsubscribe<WaveClearedEvent>(OnWaveCleared);
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

        private void OnWaveCleared(WaveClearedEvent e) => Report(MissionMetric.ClearWave, 1);

        private void OnRunFinished(RunFinishedEvent e)
        {
            var s = e.Summary;
            Report(MissionMetric.FinishRun, 1);

            // Coin missions count what the run actually earned, matching the ledger the player saw.
            if (s.Coin > 0) Report(MissionMetric.CollectCoin, (int)Math.Min(int.MaxValue, s.Coin));

            if (s.Outcome != RunOutcome.Victory) return;

            Report(MissionMetric.FinishStage, 1);
            Report(MissionMetric.ClearAllStages, 0);   // recomputed below from real progress

            // "Clear all 5 stages" is a state, not a counter - read the actual completed set so it
            // can never drift from what the campaign screen shows.
            int cleared = PlayerProfile.CompletedLevelIds.Count;
            foreach (var m in _active)
                if (m.metric == MissionMetric.ClearAllStages)
                    SetAtLeast(m, cleared);
        }

        /// <summary>Called by the level-up UI when the player takes a perk.</summary>
        public static void ReportPerkChosen() => ReportStatic(MissionMetric.ChoosePerk, 1);

        /// <summary>Called by the weapon switcher during combat.</summary>
        public static void ReportWeaponSwitched() => ReportStatic(MissionMetric.SwitchWeapon, 1);

        /// <summary>Called when a stage boss dies.</summary>
        public static void ReportBossDefeated() => ReportStatic(MissionMetric.DefeatStageBoss, 1);

        private void Report(MissionMetric metric, int amount)
        {
            if (amount <= 0) return;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].metric == metric)
                    PlayerProfile.AddMissionProgress(_active[i].id, amount);
        }

        private static void SetAtLeast(PassMission mission, int value)
        {
            int current = PlayerProfile.GetMissionProgress(mission.id);
            if (value > current) PlayerProfile.AddMissionProgress(mission.id, value - current);
        }

        /// <summary>Static path for callers that have no tracker reference. Resolves the active set
        /// on demand - slightly more work per call, but these fire rarely (perk picks, weapon swaps).</summary>
        private static void ReportStatic(MissionMetric metric, int amount)
        {
            if (amount <= 0) return;
            foreach (var m in PassMissions.ActiveFor(DateTime.UtcNow))
                if (m.metric == metric)
                    PlayerProfile.AddMissionProgress(m.id, amount);
        }
    }
}
