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
    /// Terminal handling is the delicate part, and it lives in <see cref="RunClosure"/> rather than
    /// here so it can be tested without a scene. This component only translates gameplay events into
    /// one close call and broadcasts the result.
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
            if (_finished) return;

            var result = RunClosure.Close(RunState.Current, outcome, campaign);
            if (!result.Closed) return;   // no run, or something already closed this one
            _finished = true;

            // Fired for EVERY terminal outcome, exactly once. This used to sit behind a
            // victory-only early-return and a level-id early-return, so a defeat - or a win on a map
            // with no campaign entry - ended the run silently: no result screen, and MissionTracker
            // never counted the run as finished.
            Bill.Events?.Fire(new RunFinishedEvent(result));
        }
    }

    /// <summary>Fired once a run has ended and been banked. The result screen reads the snapshot
    /// from here rather than querying RunState, which may already have been cleared. Carries the
    /// full close result so the screen can show banked amounts, which on a defeat are NOT the
    /// earned totals.</summary>
    public readonly struct RunFinishedEvent : IEvent
    {
        public readonly RunClosure.Result Result;
        public RunSummary Summary => Result.Summary;
        public RunFinishedEvent(RunClosure.Result result) { Result = result; }
    }
}
