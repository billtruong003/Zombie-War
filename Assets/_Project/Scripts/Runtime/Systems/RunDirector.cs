using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Bridges the existing gameplay events onto the run ledger and the campaign save.
    ///
    /// Deliberately a listener rather than edits inside WaveDirector/PlayerController: those already
    /// broadcast everything needed, so the run/campaign concern stays in one file instead of being
    /// smeared across the wave and player systems.
    ///
    /// Terminal handling is the delicate part. Victory and Defeat both route through
    /// <see cref="Finish"/>, which is guarded so the run is banked exactly once - RunState.Payout is
    /// itself idempotent, and completion/first-clear writes are idempotent in PlayerProfile, so even
    /// a doubled event cannot double-pay.
    /// </summary>
    public class RunDirector : MonoBehaviour
    {
        [Tooltip("Campaign definition, used to resolve the stage's first-clear reward on victory.")]
        [SerializeField] private CampaignCatalog campaign;

        private bool _finished;

        private void OnEnable()
        {
            Bill.Events?.Subscribe<WaveStartedEvent>(OnWaveStarted);
            Bill.Events?.Subscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            Bill.Events?.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnDisable()
        {
            Bill.Events?.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            Bill.Events?.Unsubscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            Bill.Events?.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        private void Update() => RunState.Current?.Tick(Time.deltaTime);

        private void OnWaveStarted(WaveStartedEvent e) => RunState.Current?.SetWave(e.WaveNumber);

        private void OnAllWavesCleared(AllWavesClearedEvent e) => Finish(RunOutcome.Victory);

        private void OnGameOver(GameOverEvent e) => Finish(RunOutcome.Defeat);

        private void Finish(RunOutcome outcome)
        {
            var run = RunState.Current;
            if (run == null || _finished) return;
            _finished = true;

            var summary = run.Finish(outcome);
            run.Payout();   // idempotent: the run's earned currency is banked exactly once

            if (outcome != RunOutcome.Victory) return;

            // Clearing a stage unlocks the next one and pays its first-clear bonus once, ever.
            // Replays still reach here, but both writes below refuse to repeat themselves.
            string levelId = run.LevelId;
            if (string.IsNullOrEmpty(levelId)) return;

            PlayerProfile.MarkLevelCompleted(levelId);

            var level = campaign != null ? campaign.Find(levelId) : null;
            if (level != null)
                PlayerProfile.TryClaimFirstClear(levelId,
                    level.firstClearCoin, level.firstClearGold, level.firstClearGem);

            Bill.Events?.Fire(new RunFinishedEvent(summary));
        }
    }

    /// <summary>Fired once a run has ended and been banked. The result screen reads the snapshot
    /// from here rather than querying RunState, which may already have been cleared.</summary>
    public readonly struct RunFinishedEvent : IEvent
    {
        public readonly RunSummary Summary;
        public RunFinishedEvent(RunSummary summary) { Summary = summary; }
    }
}
