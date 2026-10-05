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
            /// <summary>The run beat the best score (the record that counts, owner 27/09).</summary>
            public readonly bool NewScoreRecord;
            /// <summary>Mastery the carried gun earned (backlog #21) and its level before/after.</summary>
            public readonly int MasteryXp, MasteryBefore, MasteryAfter;

            public Result(bool closed, RunSummary summary, long bankedCoin, bool newSurvivalRecord, int accountXpGained = 0, int accountLevelsGained = 0, bool newScoreRecord = false,
                          int masteryXp = 0, int masteryBefore = 0, int masteryAfter = 0)
            {
                MasteryXp = masteryXp; MasteryBefore = masteryBefore; MasteryAfter = masteryAfter;
                NewScoreRecord = newScoreRecord;
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

            // One write for the whole closing (payout, record, stats, XP and the run's pending
            // mission/voice flags), not four, and the change events after it.
            bool record = false, scoreRecord = false;
            int masteryXp = 0, masteryBefore = 0, masteryAfter = 0;
            int xp = AccountProgress.XpForRun(summary.Duration, summary.Kills), levels = 0;
            PlayerProfile.Batch(() =>
            {
                run.Payout(CoinFractionFor(summary.Outcome));
                record = PlayerProfile.RecordSurvival(summary.Duration);
                scoreRecord = PlayerProfile.RecordScore(summary.Score);
                PlayerProfile.RecordRunStats(summary.Kills, summary.PeakThreatTier, summary.Duration);
                PlayerProfile.RecordGunRun(PlayerProfile.EquippedWeaponId, summary.Kills, summary.Duration, summary.Score);
                var family = PassMissions.CarriedFamily();
                masteryXp = GunMastery.XpForRun(summary.Kills, summary.Duration, family ?? WeaponClass.AssaultRifle,
                                                family.HasValue && DailyOpsAsk(family.Value));
                (masteryBefore, masteryAfter) = PlayerProfile.AddMasteryXp(PlayerProfile.EquippedWeaponId, masteryXp);
                levels = PlayerProfile.AddAccountXp(xp);
            });
            PlayerProfile.FlushIfDirty();
            return new Result(true, summary, run.BankedCoin, record, xp, levels, scoreRecord, masteryXp, masteryBefore, masteryAfter);
        }

        /// <summary>Today's Daily Ops ask for this family: mastery XP doubles (mockup U7).</summary>
        static bool DailyOpsAsk(WeaponClass family)
        {
            foreach (var m in PassMissions.ActiveFor(GameClock.UtcNow))
            {
                var gun = DailyOps.WeaponOf(m);
                if (gun != null ? gun == PlayerProfile.EquippedWeaponId : DailyOps.FamilyOf(m) == family) return true;
            }
            return false;
        }
    }
}
