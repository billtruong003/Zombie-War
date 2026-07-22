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
            var run = RunState.Begin("level.1");

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
            var run = RunState.Begin("level.1");
            run.RecordKill(_walker);
            run.RecordKill(_walker);

            Assert.AreEqual(2, run.Kills);
            Assert.AreEqual(6, run.Coin);
        }

        [Test]
        public void RecordKill_DoesNothingAfterRunEnds()
        {
            var run = RunState.Begin("level.1");
            run.Finish(RunOutcome.Defeat);
            run.RecordKill(_walker);

            Assert.AreEqual(0, run.Kills, "a finished run must not keep accruing rewards");
            Assert.AreEqual(0, run.Coin);
        }

        [Test]
        public void AddXp_LevelsUpAndReturnsLevelsGained()
        {
            var run = RunState.Begin("level.1");
            int need = run.XpForNextLevel;

            int gained = run.AddXp(need);

            Assert.AreEqual(1, gained);
            Assert.AreEqual(2, run.Level);
            Assert.AreEqual(0, run.Xp);
        }

        [Test]
        public void AddXp_HandlesMultipleLevelsInOneGrant()
        {
            var run = RunState.Begin("level.1");
            int gained = run.AddXp(500);

            Assert.Greater(gained, 1);
            Assert.AreEqual(gained + 1, run.Level);
        }

        [Test]
        public void Multiplier_IsOneWithoutPerks_AndStacks()
        {
            var run = RunState.Begin("level.1");
            Assert.AreEqual(1f, run.Multiplier(RunPerkKind.Damage), 0.0001f);

            run.AddPerk(new RunPerk("a", "", "", RunPerkKind.Damage, 1.5f));
            run.AddPerk(new RunPerk("b", "", "", RunPerkKind.Damage, 2f));
            run.AddPerk(new RunPerk("c", "", "", RunPerkKind.FireRate, 3f));

            Assert.AreEqual(3f, run.Multiplier(RunPerkKind.Damage), 0.0001f);
            Assert.AreEqual(3f, run.Multiplier(RunPerkKind.FireRate), 0.0001f);
            Assert.AreEqual(1f, run.Multiplier(RunPerkKind.MoveSpeed), 0.0001f);
        }

        [Test]
        public void Finish_FirstOutcomeWins()
        {
            var run = RunState.Begin("level.1");
            run.Finish(RunOutcome.Defeat);
            run.Finish(RunOutcome.Victory);

            Assert.AreEqual(RunOutcome.Defeat, run.Outcome,
                "a later Victory must not overwrite the Defeat that already ended the run");
        }

        [Test]
        public void Snapshot_MatchesLedger()
        {
            var run = RunState.Begin("level.1");
            run.RecordKill(_walker);
            run.SetWave(4);
            var snap = run.Finish(RunOutcome.Victory);

            Assert.AreEqual(RunOutcome.Victory, snap.Outcome);
            Assert.AreEqual(1, snap.Kills);
            Assert.AreEqual(4, snap.WaveReached);
            Assert.AreEqual(3, snap.Coin);
        }

        [Test]
        public void Payout_IsIdempotent()
        {
            long before = PlayerProfile.Coin;

            var run = RunState.Begin("level.1");
            run.RecordKill(_walker);      // 3 coin
            run.Finish(RunOutcome.Victory);

            Assert.IsTrue(run.Payout(), "first payout should bank the run");
            Assert.IsFalse(run.Payout(), "second payout must be refused");
            Assert.IsFalse(run.Payout());

            Assert.AreEqual(before + 3, PlayerProfile.Coin,
                "currency must be credited exactly once no matter how often Payout is called");
        }

        [Test]
        public void SetWave_OnlyMovesForward()
        {
            var run = RunState.Begin("level.1");
            run.SetWave(5);
            run.SetWave(2);

            Assert.AreEqual(5, run.WaveReached);
        }

        [Test]
        public void Abandon_ClearsCurrentWithoutPaying()
        {
            long before = PlayerProfile.Coin;

            var run = RunState.Begin("level.1");
            run.RecordKill(_walker);
            RunState.Abandon();

            Assert.IsNull(RunState.Current);
            Assert.AreEqual(before, PlayerProfile.Coin,
                "leaving a run mid-way must never bank its currency");
        }

        [Test]
        public void PerkPool_DrawsDistinctPerks()
        {
            var drawn = RunPerkPool.Draw(3);

            Assert.AreEqual(3, drawn.Count);
            CollectionAssert.AllItemsAreUnique(drawn);
        }
    }
}
