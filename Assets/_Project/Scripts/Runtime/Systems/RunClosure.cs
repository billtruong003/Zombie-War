namespace ZombieWar
{
    /// <summary>
    /// Everything that must happen exactly once when a run ends: freeze the summary, bank the Coin
    /// the ending allows, and record the survival time.
    ///
    /// Split out of <see cref="RunDirector"/> so the contract is testable. The director is a
    /// MonoBehaviour whose only job is to translate gameplay events into one call here and broadcast
    /// the result; none of the decisions below need a scene, a camera or Bill services.
    ///
    /// The single-shot guarantee comes from run state, not from a caller-side flag:
    /// <see cref="RunState.IsOver"/> is set by the first close and refuses every later one, so a
    /// death and a walk-away landing in the same frame produce exactly one result.
    /// </summary>
    public static class RunClosure
    {
        /// <summary>Owner, M8 (2026-09-27): every ending banks the whole run's Coin. The run is endless,
        /// so living longer already pays more; a cut on death only added friction.</summary>
        public const float DiedCoinFraction = 1f;

        /// <summary>Walking away also banks everything (same owner call).</summary>
        public const float AbandonedCoinFraction = 1f;

        public static float CoinFractionFor(RunOutcome outcome) =>
            outcome == RunOutcome.Died ? DiedCoinFraction : AbandonedCoinFraction;

        /// <summary>What a close actually did. <see cref="Closed"/> false means nothing happened and
        /// the caller must not broadcast a terminal event.</summary>
        public readonly struct Result
        {
            public readonly bool Closed;
            public readonly RunSummary Summary;
            public readonly long BankedCoin;
            public readonly bool NewSurvivalRecord;
            /// <summary>Account XP this run paid, and how many account levels it gained (M9).</summary>
            public readonly int AccountXpGained;
            public readonly int AccountLevelsGained;

            public Result(bool closed, RunSummary summary, long bankedCoin, bool newSurvivalRecord, int accountXpGained = 0, int accountLevelsGained = 0)
            {
                Closed = closed;
                Summary = summary;
                BankedCoin = bankedCoin;
                NewSurvivalRecord = newSurvivalRecord;
                AccountXpGained = accountXpGained;
                AccountLevelsGained = accountLevelsGained;
            }
        }

        /// <summary>
        /// Closes <paramref name="run"/> with <paramref name="outcome"/>. Safe to call more than
        /// once; only the first call does anything.
        /// </summary>
        public static Result Close(RunState run, RunOutcome outcome)
        {
            if (run == null || run.IsOver || outcome == RunOutcome.InProgress) return default;

            // Read the outcome back off the snapshot rather than trusting the argument: Finish is
            // first-call-wins, so the snapshot is the only honest record of what the run ended as.
            var summary = run.Finish(outcome);
            run.Payout(CoinFractionFor(summary.Outcome));

            bool record = PlayerProfile.RecordSurvival(summary.Duration);
            int xp = AccountProgress.XpForRun(summary.Duration, summary.Kills);
            int levels = PlayerProfile.AddAccountXp(xp);
            return new Result(true, summary, run.BankedCoin, record, xp, levels);
        }
    }
}
