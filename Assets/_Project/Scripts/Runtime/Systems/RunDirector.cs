using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Bridges gameplay events onto the run ledger.
    ///
    /// Deliberately a listener rather than edits inside PlayerController or the pause menu: those
    /// already broadcast what they know, so the run concern stays in one file. Terminal handling lives
    /// in <see cref="RunClosure"/> so it can be tested without a scene; this component only
    /// translates events into one close call and broadcasts the result.
    /// </summary>
    public class RunDirector : MonoBehaviour
    {
        private bool _finished;

        private void OnEnable()
        {
            Bill.Events?.Subscribe<GameOverEvent>(OnGameOver);
            Bill.Events?.Subscribe<RunAbandonRequestedEvent>(OnAbandonRequested);
        }

        private void OnDisable()
        {
            Bill.Events?.Unsubscribe<GameOverEvent>(OnGameOver);
            Bill.Events?.Unsubscribe<RunAbandonRequestedEvent>(OnAbandonRequested);
        }

        private void Update()
        {
            var run = RunState.Current;
            if (run == null) return;
            run.Tick(Time.deltaTime);

            var threat = Threat.ThreatDirector.Instance;
            if (threat != null) run.ReportThreatTier(threat.CurrentTier);
        }

        private void OnGameOver(GameOverEvent e) => Finish(RunOutcome.Died);

        private void OnAbandonRequested(RunAbandonRequestedEvent e) => Finish(RunOutcome.Abandoned);

        private void Finish(RunOutcome outcome)
        {
            if (_finished) return;

            var result = RunClosure.Close(RunState.Current, outcome);
            if (!result.Closed) return;   // no run, or something already closed this one
            _finished = true;

            // Fired for EVERY terminal outcome, exactly once, so the result screen and mission
            // tracking see a death and a walk-away the same way.
            Bill.Events?.Fire(new RunFinishedEvent(result));
        }
    }

    /// <summary>The player confirmed "end run" from the pause menu. The run closes as
    /// <see cref="RunOutcome.Abandoned"/> and the result screen shows what was forfeited.</summary>
    public readonly struct RunAbandonRequestedEvent : IEvent { }

    /// <summary>Gameplay earned the player an extra 1-of-3 card offer (a Signal Relay station). The
    /// run overlay presents it through the level-up path; gameplay never reaches into the UI.</summary>
    public readonly struct CardOfferRequestedEvent : IEvent { }

    /// <summary>Fired once a run has ended and been banked. The result screen reads the snapshot
    /// from here rather than querying RunState, which may already have been cleared.</summary>
    public readonly struct RunFinishedEvent : IEvent
    {
        public readonly RunClosure.Result Result;
        public RunSummary Summary => Result.Summary;
        public RunFinishedEvent(RunClosure.Result result) { Result = result; }
    }
}
