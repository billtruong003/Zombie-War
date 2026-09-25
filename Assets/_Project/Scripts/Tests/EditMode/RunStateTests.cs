using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>Contract tests for the run ledger. The two that matter most are the payout
    /// idempotency and the "abandon does not pay" case - those are the ones that would silently
    /// hand the player free currency if they ever regressed.</summary>
    public class RunStateTests
    {
        private ZombieData _walker;

        [SetUp]
        public void SetUp()
        {
            _walker = ScriptableObject.CreateInstance<ZombieData>();
            _walker.enemyId = "enemy.test.walker";
            _walker.coinReward = 3;
            _walker.xpReward = 4;
        }

        [TearDown]
        public void TearDown()
        {
            if (_walker != null) Object.DestroyImmediate(_walker);
            RunState.Abandon();
        }

        [Test]
        public void Begin_StartsCleanRun()
        {
            var run = RunState.Begin();

            Assert.AreSame(run, RunState.Current);
            Assert.AreEqual(0, run.Kills);
            Assert.AreEqual(0, run.Coin);
            Assert.AreEqual(1, run.Level);
            Assert.AreEqual(RunOutcome.InProgress, run.Outcome);
            Assert.IsFalse(run.IsOver);
        }

        [Test]
        public void RecordKill_BanksCoinAndXp()
        {
            var run = RunState.Begin();
            run.RecordKill(_walker);
            run.RecordKill(_walker);

            Assert.AreEqual(2, run.Kills);
            Assert.AreEqual(6, run.Coin);
        }

        [Test]
        public void RecordKill_DoesNothingAfterRunEnds()
        {
            var run = RunState.Begin();
            run.Finish(RunOutcome.Died);
            run.RecordKill(_walker);

            Assert.AreEqual(0, run.Kills, "a finished run must not keep accruing rewards");
            Assert.AreEqual(0, run.Coin);
        }

        [Test]
        public void AddXp_LevelsUpAndReturnsLevelsGained()
        {
            var run = RunState.Begin();
            int need = run.XpForNextLevel;

            int gained = run.AddXp(need);

            Assert.AreEqual(1, gained);
            Assert.AreEqual(2, run.Level);
            Assert.AreEqual(0, run.Xp);
        }

        [Test]
        public void XpCurve_WidensSoLevelUpsSpreadOut()
        {
            Assert.AreEqual(25, RunState.XpForLevel(1), "the first card needs ~25 early kills (30-45 s)");
            int previousGap = 0;
            for (int level = 1; level < 15; level++)
            {
                int gap = RunState.XpForLevel(level);
                Assert.Greater(gap, previousGap, $"level {level} must cost more than the one before");
                previousGap = gap;
            }
        }

        [Test]
        public void AddXp_HandlesMultipleLevelsInOneGrant()
        {
            var run = RunState.Begin();
            int gained = run.AddXp(500);

            Assert.Greater(gained, 1);
            Assert.AreEqual(gained + 1, run.Level);
        }

        [Test]
        public void Finish_FirstOutcomeWins()
        {
            var run = RunState.Begin();
            run.Finish(RunOutcome.Died);
            run.Finish(RunOutcome.Abandoned);

            Assert.AreEqual(RunOutcome.Died, run.Outcome,
                "a later walk-away must not overwrite the death that already ended the run");
        }

        [Test]
        public void Snapshot_MatchesLedger()
        {
            var run = RunState.Begin();
            run.RecordKill(_walker);
            run.ReportThreatTier(4);
            run.Tick(12.5f);
            var snap = run.Finish(RunOutcome.Died);

            Assert.AreEqual(RunOutcome.Died, snap.Outcome);
            Assert.AreEqual(1, snap.Kills);
            Assert.AreEqual(4, snap.PeakThreatTier);
            Assert.AreEqual(3, snap.Coin);
            Assert.AreEqual(12.5f, snap.Duration, 0.0001f);
        }

        [Test]
        public void Payout_IsIdempotent()
        {
            long before = PlayerProfile.Coin;

            var run = RunState.Begin();
            run.RecordKill(_walker);      // 3 coin
            run.Finish(RunOutcome.Died);

            Assert.IsTrue(run.Payout(1f), "first payout should bank the run");
            Assert.IsFalse(run.Payout(1f), "second payout must be refused");
            Assert.IsFalse(run.Payout(1f));

            Assert.AreEqual(before + 3, PlayerProfile.Coin,
                "currency must be credited exactly once no matter how often Payout is called");
        }

        [Test]
        public void ThreatTier_RecordsThePeakOnly()
        {
            var run = RunState.Begin();
            run.ReportThreatTier(5);
            run.ReportThreatTier(2);

            Assert.AreEqual(5, run.PeakThreatTier);
        }

        [Test]
        public void EveryRun_GetsItsOwnSeed()
        {
            int first = RunState.Begin().Seed;
            System.Threading.Thread.Sleep(20);
            int second = RunState.Begin().Seed;

            Assert.AreNotEqual(first, second, "two runs must not deal the same level-up offers");
        }

        [Test]
        public void Abandon_ClearsCurrentWithoutPaying()
        {
            long before = PlayerProfile.Coin;

            var run = RunState.Begin();
            run.RecordKill(_walker);
            RunState.Abandon();

            Assert.IsNull(RunState.Current);
            Assert.AreEqual(before, PlayerProfile.Coin,
                "leaving a run mid-way must never bank its currency");
        }

    }
}
