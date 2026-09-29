using System;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>How an endless run ended. There is no victory: the world always wins eventually,
    /// and the only other exit is the player walking away (GDD §20).</summary>
    public enum RunOutcome { InProgress, Died, Abandoned }

    /// <summary>What a run actually produced. Taken as a snapshot the moment the run ends, so the
    /// result screen and the payout can never disagree with each other or drift as the scene unloads.</summary>
    public readonly struct RunSummary
    {
        public readonly RunOutcome Outcome;
        public readonly int Kills, Level, Xp, PeakThreatTier;
        public readonly long Coin, Gem;
        public readonly float Duration;

        public RunSummary(RunOutcome outcome, int kills, int level, int xp, int peakThreatTier,
                          long coin, long gem, float duration)
        {
            Outcome = outcome; Kills = kills; Level = level; Xp = xp; PeakThreatTier = peakThreatTier;
            Coin = coin; Gem = gem; Duration = duration;
        }
    }

    /// <summary>
    /// The single in-memory authority for everything a run earns: kills, threat reached, currency,
    /// XP/level, elapsed time and the terminal result. The run's card build lives in SkillRuntime.
    ///
    /// Design rules this type exists to enforce:
    ///   * Coin earned in a run is banked HERE, never written straight into <see cref="PlayerProfile"/>.
    ///     The profile is touched exactly once, at <see cref="Payout"/>, so a run that is abandoned
    ///     mid-way cannot half-pay the player. Gem is the deliberate exception: GDD §11 secures it the
    ///     moment it is picked up, so it goes to the profile immediately and is only counted here.
    ///   * <see cref="Payout"/> is idempotent - calling it twice pays once. Scene unload, a replay tap
    ///     and a result screen all racing to finish the run cannot double-credit.
    /// </summary>
    public class RunState
    {
        /// <summary>The run currently in progress. Null between runs - callers must null-check,
        /// which also keeps menu-only scenes free of run state.</summary>
        public static RunState Current { get; private set; }

        public static event Action Changed;

        /// <summary>Raised when XP crosses one or more level thresholds, with the number of levels
        /// gained in that grant. This is the level-up UI's trigger.</summary>
        public static event Action<int> LevelsGained;

        private bool _paidOut;

        public int Kills { get; private set; }
        public int Level { get; private set; } = 1;
        public int PeakThreatTier { get; private set; }

        /// <summary>Seed the level-up offer builder draws from. Fresh per run so two runs never deal
        /// the same cards; tests pin it with <see cref="SetSeed"/> to reproduce an offer.</summary>
        public int Seed { get; private set; }

        public void SetSeed(int seed) => Seed = seed;
        public int Xp { get; private set; }
        public long Coin { get; private set; }
        public long Gem { get; private set; }
        public float Duration { get; private set; }
        public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;

        public bool IsOver => Outcome != RunOutcome.InProgress;

        /// <summary>XP needed to reach the next level. The gaps still widen, so an endless run is not a
        /// constant stream of pauses, but gently. M8 measurement (2026-09-26, starter pistol, circling
        /// bot, new powers): the old 25 + (L-1)^1.35 x 12 curve spaced cards 30-48 s apart from level 4
        /// on — six cards in a 3.5-minute run, far too few for a build to form. A first ease
        /// (18 + (L-1)^1.2 x 7) still left 25-30 s gaps after level 5 (level 11 at 5:00). This curve
        /// lands the first card in ~10 s and keeps later gaps around 15-25 s at horde kill rates.</summary>
        public int XpForNextLevel => XpForLevel(Level);

        public static int XpForLevel(int level) =>
            14 + Mathf.RoundToInt(Mathf.Pow(Mathf.Max(0, level - 1), 1.05f) * 5f);

        public static RunState Begin()
        {
            // Clear every run-scoped static BEFORE the new run exists.
            //
            // Reset happens at run START, not run end: a quit, a crash or an unexpected exit can skip
            // an end-of-run hook, but nothing can start a run without coming through here. This is
            // the single place run state is cleared — see RunScope. Do not scatter Clear() calls into
            // OnEnable handlers; that is how the skill build and the pickup registry both leaked.
            RunScope.ResetAll();

            Current = new RunState { Seed = Environment.TickCount };
            Changed?.Invoke();
            return Current;
        }

        /// <summary>Drops the active run without paying out. Only a safety net for leaving the scene
        /// without a closure; the normal walk-away path closes the run as
        /// <see cref="RunOutcome.Abandoned"/> first so the player sees what they forfeited.</summary>
        public static void Abandon()
        {
            Current = null;
            Changed?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (IsOver) return;
            Duration += deltaTime;
        }

        /// <summary>Records the threat tier the run has reached. Only moves forward - the result
        /// screen reports the peak, not wherever pressure happened to sit at the moment of death.</summary>
        public void ReportThreatTier(int tier)
        {
            if (IsOver || tier <= PeakThreatTier) return;
            PeakThreatTier = tier;
            Changed?.Invoke();
        }

        /// <summary>Banks one enemy kill. Callers must invoke this exactly once per death; ZombieBase
        /// guards its own death path so a pooled instance cannot report twice.</summary>
        /// <param name="bankCoin">
        /// False when physical pickups are handling the payout. The coin value is identical either
        /// way - it is only a question of whether it lands now or when the player walks over the
        /// drop. Banking in both places would pay twice for one kill.
        /// </param>
        public void RecordKill(ZombieData data, bool bankCoin = true)
        {
            if (IsOver || data == null) return;

            Kills++;
            if (bankCoin) Coin += ScaleCoin(Mathf.Max(0, data.coinReward));
            AddXp(Mathf.Max(0, data.xpReward));
            Changed?.Invoke();
        }

        /// <summary>
        /// Spends banked run Coin at an in-run sink (Supply Cache). Returns false when the player
        /// cannot afford it, so callers never go negative.
        /// </summary>
        public bool SpendCoin(long amount)
        {
            if (IsOver || amount <= 0 || Coin < amount) return false;
            Coin -= amount;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Credits a pickup. Coin waits for settlement; Gem is secured into the profile on the
        /// spot, so neither a death nor a walk-away can take it back.</summary>
        public void AddCurrency(PlayerProfile.CurrencyKind kind, long amount)
        {
            if (IsOver || amount <= 0) return;
            if (kind == PlayerProfile.CurrencyKind.Gem)
            {
                Gem += amount;
                PlayerProfile.AddDeferred(PlayerProfile.CurrencyKind.Gem, amount);
            }
            else Coin += ScaleCoin(amount);
            Changed?.Invoke();
        }

        // Coin Gain Up is consumed here - the single place Coin enters the ledger - so kill banking
        // and physical pickups scale identically and nothing can double-apply it.
        private static long ScaleCoin(long amount) =>
            (long)Math.Round(amount * (Skills.SkillRuntime.Active?.CoinMultiplier ?? 1f));

        /// <summary>Adds XP and levels up as many times as the XP covers. Returns how many levels were
        /// gained, so the caller can queue that many card choices.</summary>
        public int AddXp(int amount)
        {
            if (IsOver || amount <= 0) return 0;

            Xp += amount;
            int gained = 0;
            while (Xp >= XpForNextLevel)
            {
                Xp -= XpForNextLevel;
                Level++;
                gained++;
            }
            if (gained > 0)
            {
                Changed?.Invoke();
                LevelsGained?.Invoke(gained);
            }
            return gained;
        }

        /// <summary>Ends the run and freezes a snapshot. The first call wins: an abandon that lands in
        /// the same frame as a death cannot overwrite it.</summary>
        public RunSummary Finish(RunOutcome outcome)
        {
            if (!IsOver && outcome != RunOutcome.InProgress) Outcome = outcome;
            Changed?.Invoke();
            return Snapshot();
        }

        public RunSummary Snapshot() =>
            new RunSummary(Outcome, Kills, Level, Xp, PeakThreatTier, Coin, Gem, Duration);

        /// <summary>Coin the payout actually credited, frozen by the first <see cref="Payout"/> call.
        /// It differs from the earned total on every ending - the result screen must show this, not
        /// the raw run number, or it lies about what the player kept.</summary>
        public long BankedCoin { get; private set; }

        /// <summary>Banks this run's Coin into the persistent profile. Idempotent - the second and
        /// later calls are no-ops and return false, which is what makes replay/home/result-screen
        /// races safe. The outcome-dependent fraction is decided by <see cref="RunClosure"/>, not
        /// here - this method only applies it.</summary>
        public bool Payout(float coinFraction)
        {
            if (_paidOut) return false;
            _paidOut = true;

            BankedCoin = (long)(Coin * Mathf.Clamp01(coinFraction));
            if (BankedCoin > 0) PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, BankedCoin);
            return true;
        }

        public bool HasPaidOut => _paidOut;
    }
}
