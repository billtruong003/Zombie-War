namespace ZombieWar
{
    /// <summary>
    /// Everything that must happen exactly once when a run ends: freeze the summary, bank the
    /// currency, and - on a win - record the stage clear and its first-clear reward.
    ///
    /// Split out of <see cref="RunDirector"/> so the contract is testable. The director is a
    /// MonoBehaviour whose only remaining job is to translate gameplay events into one call here and
    /// broadcast the result; none of the decisions below need a scene, a camera or Bill services.
    ///
    /// The single-shot guarantee comes from run state, not from a caller-side flag:
    /// <see cref="RunState.IsOver"/> is set by the first close and refuses every later one. That is
    /// what makes a Victory and a Defeat arriving in the same frame safe - the first wins, the second
    /// is a no-op, and no second <see cref="RunFinishedEvent"/> can be produced.
    /// </summary>
    public static class RunClosure
    {
        /// <summary>GDD §11 closure rule: defeat banks a quarter of the common currency and loses the
        /// rare reward entirely. Provisional until income simulation, but the risk has to exist.</summary>
        public const float DefeatCoinFraction = 0.25f;

        /// <summary>What a close actually did. <see cref="Closed"/> false means nothing happened and
        /// the caller must not broadcast a terminal event. Banked and first-clear amounts are carried
        /// here so the result screen can show what the player actually kept - on a defeat that is NOT
        /// the run's earned totals.</summary>
        public readonly struct Result
        {
            public readonly bool Closed;
            public readonly RunSummary Summary;
            public readonly bool BankedPayout;
            public readonly bool MarkedComplete;
            public readonly bool ClaimedFirstClear;
            public readonly long BankedCoin, BankedGold, BankedGem;
            public readonly long FirstClearCoin, FirstClearGold, FirstClearGem;

            public Result(bool closed, RunSummary summary, bool bankedPayout,
                          bool markedComplete, bool claimedFirstClear,
                          long bankedCoin = 0, long bankedGold = 0, long bankedGem = 0,
                          long firstClearCoin = 0, long firstClearGold = 0, long firstClearGem = 0)
            {
                Closed = closed;
                Summary = summary;
                BankedPayout = bankedPayout;
                MarkedComplete = markedComplete;
                ClaimedFirstClear = claimedFirstClear;
                BankedCoin = bankedCoin;
                BankedGold = bankedGold;
                BankedGem = bankedGem;
                FirstClearCoin = firstClearCoin;
                FirstClearGold = firstClearGold;
                FirstClearGem = firstClearGem;
            }
        }

        /// <summary>
        /// Closes <paramref name="run"/> with <paramref name="outcome"/>. Safe to call more than
        /// once; only the first call does anything.
        /// </summary>
        /// <param name="campaign">
        /// May be null, and may not contain the run's level. Neither suppresses the close - a stage
        /// missing from the catalog still completes and still reports; it just pays no first-clear
        /// bonus, because there is no authored bonus to pay.
        /// </param>
        public static Result Close(RunState run, RunOutcome outcome, CampaignCatalog campaign)
        {
            if (run == null || run.IsOver || outcome == RunOutcome.InProgress) return default;

            var summary = run.Finish(outcome);

            // Read the outcome back off the snapshot rather than trusting the argument: Finish is
            // first-call-wins, so the snapshot is the only honest record of what the run ended as.
            bool won = summary.Outcome == RunOutcome.Victory;
            bool banked = won
                ? run.Payout()
                : run.Payout(DefeatCoinFraction, includeRare: false);

            if (!won)
                return new Result(true, summary, banked, false, false,
                                  run.BankedCoin, run.BankedGold, run.BankedGem);

            return AwardStage(run, campaign, summary, banked);
        }

        // Both profile writes are idempotent by themselves, so a replayed stage re-enters here and
        // correctly banks nothing new.
        private static Result AwardStage(RunState run, CampaignCatalog campaign,
                                         RunSummary summary, bool banked)
        {
            if (string.IsNullOrEmpty(run.LevelId))
                return new Result(true, summary, banked, false, false,
                                  run.BankedCoin, run.BankedGold, run.BankedGem);

            PlayerProfile.MarkLevelCompleted(run.LevelId);

            var level = campaign != null ? campaign.Find(run.LevelId) : null;
            if (level == null)
                return new Result(true, summary, banked, true, false,
                                  run.BankedCoin, run.BankedGold, run.BankedGem);

            bool claimed = PlayerProfile.TryClaimFirstClear(
                run.LevelId, level.firstClearCoin, level.firstClearGold, level.firstClearGem);

            return new Result(true, summary, banked, true, claimed,
                              run.BankedCoin, run.BankedGold, run.BankedGem,
                              claimed ? level.firstClearCoin : 0,
                              claimed ? level.firstClearGold : 0,
                              claimed ? level.firstClearGem : 0);
        }
    }
}
