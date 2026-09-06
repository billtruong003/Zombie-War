using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BillGameCore;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5.1.2 CP3 - the terminal screen must obey the ledger's first-wins close, in BOTH orders.
    ///
    /// The defect: RunOverlays drove its result roots from raw AllWavesCleared/GameOver events, so
    /// a death that landed after a locked Victory repainted the screen as Defeat while the payout
    /// stayed Victory. Terminal presentation now flows only through RunFinishedEvent, which
    /// RunDirector emits exactly once from RunClosure's frozen result.
    /// </summary>
    public class TerminalResultFirstWinsTests
    {
        private GameObject _overlayHost;
        private GameObject _directorHost;
        private GameObject _victoryRoot, _gameOverRoot;
        private RunOverlays _overlays;

        [SetUp]
        public void SetUp()
        {
            RunState.Abandon();

            _overlayHost = new GameObject("OverlaysHost");
            _overlayHost.SetActive(false);
            _overlays = _overlayHost.AddComponent<RunOverlays>();

            _victoryRoot = new GameObject("VictoryRootStub");
            _gameOverRoot = new GameObject("GameOverRootStub");
            _victoryRoot.SetActive(false);
            _gameOverRoot.SetActive(false);

            var f = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RunOverlays).GetField("victoryRoot", f).SetValue(_overlays, _victoryRoot);
            typeof(RunOverlays).GetField("gameOverRoot", f).SetValue(_overlays, _gameOverRoot);
            _overlayHost.SetActive(true);

            _directorHost = new GameObject("RunDirectorHost");
            _directorHost.AddComponent<RunDirector>();   // campaign null: a zero-coin run pays nothing

            RunState.Begin("");   // empty level id: no campaign completion writes on close
        }

        [TearDown]
        public void TearDown()
        {
            RunState.Abandon();
            if (_overlayHost != null) Object.DestroyImmediate(_overlayHost);
            if (_directorHost != null) Object.DestroyImmediate(_directorHost);
            if (_victoryRoot != null) Object.DestroyImmediate(_victoryRoot);
            if (_gameOverRoot != null) Object.DestroyImmediate(_gameOverRoot);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator VictoryThenLateDefeat_KeepsVictoryScreenAndLedger()
        {
            Bill.Events.Fire(new AllWavesClearedEvent());
            yield return null;

            Assert.IsTrue(_victoryRoot.activeSelf, "victory root did not show");
            Assert.IsFalse(_gameOverRoot.activeSelf);
            Assert.AreEqual(RunOutcome.Victory, RunState.Current.Outcome);

            Bill.Events.Fire(new GameOverEvent());   // late death after the ledger locked Victory
            yield return null;

            Assert.IsTrue(_victoryRoot.activeSelf,
                "a late GameOverEvent replaced the locked Victory screen");
            Assert.IsFalse(_gameOverRoot.activeSelf,
                "the defeat root appeared after Victory had already closed the run");
            Assert.AreEqual(RunOutcome.Victory, RunState.Current.Outcome, "ledger must stay Victory");
        }

        [UnityTest]
        public IEnumerator DefeatThenLateVictory_KeepsDefeatScreenAndLedger()
        {
            Bill.Events.Fire(new GameOverEvent());
            yield return null;

            Assert.IsTrue(_gameOverRoot.activeSelf, "defeat root did not show");
            Assert.IsFalse(_victoryRoot.activeSelf);
            Assert.AreEqual(RunOutcome.Defeat, RunState.Current.Outcome);

            Bill.Events.Fire(new AllWavesClearedEvent());   // late clear after the locked Defeat
            yield return null;

            Assert.IsTrue(_gameOverRoot.activeSelf,
                "a late AllWavesClearedEvent replaced the locked Defeat screen");
            Assert.IsFalse(_victoryRoot.activeSelf);
            Assert.AreEqual(RunOutcome.Defeat, RunState.Current.Outcome, "ledger must stay Defeat");
        }

        [UnityTest]
        public IEnumerator RepeatedTerminalEvents_ProduceExactlyOnePayout()
        {
            Bill.Events.Fire(new AllWavesClearedEvent());
            yield return null;
            Bill.Events.Fire(new AllWavesClearedEvent());
            Bill.Events.Fire(new GameOverEvent());
            yield return null;

            Assert.IsTrue(RunState.Current.HasPaidOut, "the first close must pay");
            Assert.IsTrue(_victoryRoot.activeSelf);
            Assert.IsFalse(_gameOverRoot.activeSelf);
        }

        [UnityTest]
        public IEnumerator ReplayedRunFinishedEvent_DoesNotRerunTheTerminalTransition()
        {
            Bill.Events.Fire(new AllWavesClearedEvent());
            yield return null;

            // Replay the finished event with a DEFEAT summary - a robust screen must ignore it.
            var run = RunState.Current;
            var fakeSummary = new RunSummary(RunOutcome.Defeat, 0, 0, 1, 0, 0, 0, 0, 0f);
            var fakeResult = new RunClosure.Result(true, fakeSummary, false, false, false);
            Bill.Events.Fire(new RunFinishedEvent(fakeResult));
            yield return null;

            Assert.IsTrue(_victoryRoot.activeSelf, "replayed RunFinishedEvent re-ran the transition");
            Assert.IsFalse(_gameOverRoot.activeSelf);
        }
    }
}
