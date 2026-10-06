using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BillGameCore;

namespace ZombieWar.Tests
{
    /// <summary>
    /// The result screen must obey the ledger's first-wins close. Presentation flows only through
    /// RunFinishedEvent, which RunDirector emits exactly once from RunClosure's frozen result, so a
    /// death and a walk-away racing each other can never show two endings or pay twice.
    /// </summary>
    public class TerminalResultFirstWinsTests
    {
        private GameObject _overlayHost;
        private GameObject _directorHost;
        private GameObject _resultRoot;
        private RunOverlays _overlays;

        [SetUp]
        public void SetUp()
        {
            RunState.Abandon();

            _overlayHost = new GameObject("OverlaysHost");
            _overlayHost.SetActive(false);
            _overlays = _overlayHost.AddComponent<RunOverlays>();

            _resultRoot = new GameObject("ResultRootStub");
            _resultRoot.SetActive(false);

            var f = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RunOverlays).GetField("resultRoot", f).SetValue(_overlays, _resultRoot);
            _overlayHost.SetActive(true);

            _directorHost = new GameObject("RunDirectorHost");
            _directorHost.AddComponent<RunDirector>();

            RunState.Begin();   // zero-coin run: closing pays nothing
        }

        // The stub has no RunEndV2 (the real result screen lives in the HUD prefab), so the terminal
        // transition logs this exactly once and freezes the world. A second log would fail the test
        // as unexpected: that is how these tests see a transition run twice. (Since 04/10 the V1
        // resultRoot is retired and stays hidden; these tests used to check it.)
        static void ExpectOneTransition() =>
            LogAssert.Expect(LogType.Error, "[RunOverlays] No result screen (endV2) wired.");

        [TearDown]
        public void TearDown()
        {
            RunState.Abandon();
            if (_overlayHost != null) Object.DestroyImmediate(_overlayHost);
            if (_directorHost != null) Object.DestroyImmediate(_directorHost);
            if (_resultRoot != null) Object.DestroyImmediate(_resultRoot);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator DeathThenWalkAway_KeepsTheDeath()
        {
            ExpectOneTransition();
            Bill.Events.Fire(new GameOverEvent());
            yield return null;

            Assert.AreEqual(0f, Time.timeScale, "the result transition did not run");
            Assert.AreEqual(RunOutcome.Died, RunState.Current.Outcome);

            Bill.Events.Fire(new RunAbandonRequestedEvent());
            yield return null;

            Assert.AreEqual(RunOutcome.Died, RunState.Current.Outcome, "ledger must stay Died");
        }

        [UnityTest]
        public IEnumerator WalkAway_ShowsTheResultAndFreezesTheWorld()
        {
            ExpectOneTransition();
            Bill.Events.Fire(new RunAbandonRequestedEvent());
            yield return null;

            Assert.AreEqual(RunOutcome.Abandoned, RunState.Current.Outcome);
            Assert.AreEqual(0f, Time.timeScale, "nothing may keep hitting a player reading the result");
        }

        [UnityTest]
        public IEnumerator RepeatedTerminalEvents_ProduceExactlyOnePayout()
        {
            ExpectOneTransition();
            Bill.Events.Fire(new GameOverEvent());
            yield return null;
            Bill.Events.Fire(new GameOverEvent());
            Bill.Events.Fire(new RunAbandonRequestedEvent());
            yield return null;

            Assert.IsTrue(RunState.Current.HasPaidOut, "the first close must pay");
            Assert.AreEqual(RunOutcome.Died, RunState.Current.Outcome);
        }

        [UnityTest]
        public IEnumerator ReplayedRunFinishedEvent_DoesNotRerunTheTerminalTransition()
        {
            ExpectOneTransition();   // a replay that re-ran it would log a second, unexpected time
            Bill.Events.Fire(new GameOverEvent());
            yield return null;

            var fakeSummary = new RunSummary(RunOutcome.Abandoned, 0, 1, 0, 0, 0, 0, 0f);
            Bill.Events.Fire(new RunFinishedEvent(new RunClosure.Result(true, fakeSummary, 0, false)));
            yield return null;

            Assert.AreEqual(RunOutcome.Died, RunState.Current.Outcome, "the replayed summary must not replace the real one");
        }
    }
}
