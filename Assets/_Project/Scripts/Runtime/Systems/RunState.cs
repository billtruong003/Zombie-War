using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    public enum RunOutcome { InProgress, Victory, Defeat }

    /// <summary>What a run actually produced. Taken as a snapshot the moment the run ends, so the
    /// result screen and the payout can never disagree with each other or drift as the scene unloads.</summary>
    public readonly struct RunSummary
    {
        public readonly RunOutcome Outcome;
        public readonly int Kills, WaveReached, Level, Xp;
        public readonly long Coin, Gold, Gem;
        public readonly float Duration;

        public RunSummary(RunOutcome outcome, int kills, int waveReached, int level, int xp,
                          long coin, long gold, long gem, float duration)
        {
            Outcome = outcome; Kills = kills; WaveReached = waveReached; Level = level; Xp = xp;
            Coin = coin; Gold = gold; Gem = gem; Duration = duration;
        }
    }

    /// <summary>
    /// The single in-memory authority for everything a run earns: kills, wave, currency, XP/level,
    /// temporary perks, elapsed time and the terminal result.
    ///
    /// Design rules this type exists to enforce:
    ///   * Currency earned in a run is banked HERE, never written straight into <see cref="PlayerProfile"/>.
    ///     The profile is touched exactly once, at <see cref="Payout"/>, so a run that is abandoned
    ///     mid-way cannot half-pay the player.
    ///   * <see cref="Payout"/> is idempotent - calling it twice pays once. Scene unload, a replay tap
    ///     and a result screen all racing to finish the run cannot double-credit.
    ///   * Perks are temporary: they live and die with the run and are applied as multipliers on top
    ///     of the permanent weapon-star scaling in <see cref="WeaponUpgradeMath"/>, never merged into it.
    /// </summary>
    public class RunState
    {
        /// <summary>The run currently in progress. Null between runs - callers must null-check,
        /// which also keeps menu-only scenes free of run state.</summary>
        public static RunState Current { get; private set; }

        public static event Action Changed;

        /// <summary>Raised when XP crosses one or more level thresholds, with the number of levels
        /// gained in that grant. This is the level-up UI's trigger - without a listener the level-up
        /// is silent, which the M5 audit flagged as scaffolding (S6).</summary>
        public static event Action<int> LevelsGained;

        private readonly List<RunPerk> _perks = new List<RunPerk>();
        private bool _paidOut;

        public int Kills { get; private set; }
        public int WaveReached { get; private set; }
        public int Level { get; private set; } = 1;

        /// M7.2 — the deterministic seed the level-up offer builder draws from. Set once per run so
        /// the same run reproduces the same offers; additive, and nothing existing reads it.
        public int Seed { get; private set; } = 20260815;

        public void SetSeed(int seed) => Seed = seed;
        public int Xp { get; private set; }
        public long Coin { get; private set; }
        public long Gold { get; private set; }
        public long Gem { get; private set; }
        public float Duration { get; private set; }
        public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;
        public string LevelId { get; private set; }

        public IReadOnlyList<RunPerk> Perks => _perks;
        public bool IsOver => Outcome != RunOutcome.InProgress;

        /// <summary>XP needed to reach the next level. Deliberately a simple growing curve rather than
        /// an authored table - it is tuned by one number and is trivial to reason about in tests.</summary>
        public int XpForNextLevel => 10 + (Level - 1) * 8;

        public static RunState Begin(string levelId)
        {
            // M7.2c — clear every run-scoped static BEFORE the new run exists.
            //
            // Reset happens at run START, not run end: a quit, a crash or an unexpected exit can skip
            // an end-of-run hook, but nothing can start a run without coming through here. This is
            // the single place run state is cleared — see RunScope. Do not scatter Clear() calls into
            // OnEnable handlers; that is how the skill build and the pickup registry both leaked.
            RunScope.ResetAll();

            Current = new RunState { LevelId = levelId };
            Changed?.Invoke();
            return Current;
        }

        /// <summary>Drops the active run without paying out. Used by "Home" - abandoning a run
        /// must never bank its currency.</summary>
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

        public void SetWave(int waveNumber)
        {
            if (waveNumber > WaveReached) WaveReached = waveNumber;
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
        /// M7.3 — spends banked run Coin. This is the first IN-RUN sink Coin has ever had: before
        /// Supply Cache, Coin was earned all run and only mattered at settlement. Returns false when
        /// the player cannot afford it, so callers never go negative.
        /// </summary>
        public bool SpendCoin(long amount)
        {
            if (IsOver || amount <= 0 || Coin < amount) return false;
            Coin -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddCurrency(PlayerProfile.CurrencyKind kind, long amount)
        {
            if (IsOver || amount <= 0) return;
            if (kind == PlayerProfile.CurrencyKind.Coin) Coin += ScaleCoin(amount);
            else if (kind == PlayerProfile.CurrencyKind.Gold) Gold += amount;
            else Gem += amount;
            Changed?.Invoke();
        }

        // The CoinGain perk is consumed here - the single place Coin enters the ledger - so kill
        // banking and physical pickups scale identically and nothing can double-apply it.
        private long ScaleCoin(long amount) =>
            (long)Math.Round(amount * Multiplier(RunPerkKind.CoinGain));

        /// <summary>Adds XP and levels up as many times as the XP covers. Returns how many levels were
        /// gained, so the caller can queue that many perk choices.</summary>
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

        public void AddPerk(RunPerk perk)
        {
            if (IsOver || perk == null) return;
            _perks.Add(perk);
            Changed?.Invoke();
        }

        /// <summary>Product of every stacked perk of this kind. 1 means "no perk of this kind",
        /// so callers can multiply unconditionally.</summary>
        public float Multiplier(RunPerkKind kind)
        {
            float m = 1f;
            for (int i = 0; i < _perks.Count; i++)
                if (_perks[i].kind == kind) m *= _perks[i].multiplier;
            return m;
        }

        /// <summary>Ends the run and freezes a snapshot. The first call wins: a Victory that lands in
        /// the same frame as a Defeat cannot overwrite it.</summary>
        public RunSummary Finish(RunOutcome outcome)
        {
            if (!IsOver && outcome != RunOutcome.InProgress) Outcome = outcome;
            Changed?.Invoke();
            return Snapshot();
        }

        public RunSummary Snapshot() =>
            new RunSummary(Outcome, Kills, WaveReached, Level, Xp, Coin, Gold, Gem, Duration);

        /// <summary>What the payout actually credited, frozen by the first <see cref="Payout"/> call.
        /// On a defeat this differs from the earned totals - the result screen must show these, not
        /// the raw run numbers, or it lies about what the player kept.</summary>
        public long BankedCoin { get; private set; }
        public long BankedGold { get; private set; }
        public long BankedGem { get; private set; }

        /// <summary>Banks this run's currency into the persistent profile. Idempotent - the second and
        /// later calls are no-ops and return false, which is what makes replay/home/result-screen
        /// races safe. The outcome-dependent fractions are decided by <see cref="RunClosure"/>, not
        /// here - this method only applies them.</summary>
        /// <param name="coinFraction">Portion of earned Coin to keep (GDD §11: defeat keeps 25%).</param>
        /// <param name="includeRare">False drops Gold and Gem entirely (defeat loses rare reward).</param>
        public bool Payout(float coinFraction = 1f, bool includeRare = true)
        {
            if (_paidOut) return false;
            _paidOut = true;

            BankedCoin = (long)(Coin * Mathf.Clamp01(coinFraction));
            BankedGold = includeRare ? Gold : 0;
            BankedGem = includeRare ? Gem : 0;

            if (BankedCoin > 0) PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, BankedCoin);
            if (BankedGold > 0) PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, BankedGold);
            if (BankedGem > 0) PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, BankedGem);
            return true;
        }

        public bool HasPaidOut => _paidOut;
    }

    public enum RunPerkKind
    {
        Damage,
        FireRate,
        MoveSpeed,
        MaxHealth,
        CoinGain
    }

    /// <summary>One temporary run-scoped upgrade. Authored as plain data so the level-up UI can show
    /// three of them without knowing anything about weapons.</summary>
    [Serializable]
    public class RunPerk
    {
        public string id;
        public string title;
        public string description;
        public RunPerkKind kind;
        public float multiplier = 1.1f;

        public RunPerk() { }

        public RunPerk(string id, string title, string description, RunPerkKind kind, float multiplier)
        {
            this.id = id; this.title = title; this.description = description;
            this.kind = kind; this.multiplier = multiplier;
        }
    }
}
