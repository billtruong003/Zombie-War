using NUnit.Framework;
using UnityEngine;
using ZombieWar.Stations;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.3b — the owner's bug: standing inside a Signal Relay ring never advances the hold.
    ///
    /// The station system is unplayable until this works, and the logic reads correctly on
    /// inspection, so the failure is in a detail. These tests drive the exact thing the player does:
    /// stand inside the DRAWN ring and wait.
    /// </summary>
    public class StationHoldTests
    {
        GameObject _go;
        Station _station;
        WorldSignal _signal;

        [SetUp]
        public void SetUp()
        {
            StationRegistry.ResetAll();
            _go = new GameObject("station");
            _go.transform.position = new Vector3(100f, 0f, 100f);
            _signal = _go.AddComponent<WorldSignal>();
            _station = _go.AddComponent<Station>();
            _station.Bind(new StationAnchors.Anchor(12345L, _go.transform.position, StationKind.SignalRelay), _signal);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            StationRegistry.ResetAll();
            RunState.Abandon();
        }

        /// <summary>
        /// The headline. A player standing dead centre must accumulate hold progress.
        /// </summary>
        [Test]
        public void StandingInTheRingAdvancesProgress()
        {
            Vector3 centre = _go.transform.position;

            Assert.AreEqual(0f, _station.Progress01, 1e-4, "sanity: nothing held yet");

            for (int i = 0; i < 10; i++) _station.Tick(0.1f, centre);

            Assert.Greater(_station.Progress01, 0f,
                "standing inside the ring must advance the hold — this is the owner's bug");
        }

        /// <summary>
        /// The ring the player SEES and the ring the code TESTS must be the same size. If Configure
        /// does not push its radius into the field Contains reads, the player stands inside the drawn
        /// circle and is outside the logical one.
        /// </summary>
        [Test]
        public void DrawnRingAndLogicalRingAreTheSameSize()
        {
            float r = _signal.RingRadius;
            Vector3 centre = _go.transform.position;

            Assert.IsTrue(_signal.Contains(centre + new Vector3(r * 0.95f, 0f, 0f)),
                "just inside the drawn ring must count as inside");
            Assert.IsFalse(_signal.Contains(centre + new Vector3(r * 1.05f, 0f, 0f)),
                "just outside the drawn ring must count as outside");
        }

        [Test]
        public void HoldIsPlanar_PlayerHeightDoesNotMatter()
        {
            // The station sits on the ground; the player's transform origin is at capsule centre.
            // A full 3D distance check would silently push the player outside the ring.
            Vector3 raised = _go.transform.position + new Vector3(0f, 1.2f, 0f);
            for (int i = 0; i < 10; i++) _station.Tick(0.1f, raised);

            Assert.Greater(_station.Progress01, 0f, "a player standing at capsule height is still inside");
        }

        [Test]
        public void ProgressRingIsActuallyRedrawn_NotJustCounted()
        {
            // A hold that advances invisibly is the same bug from the player's side.
            Vector3 centre = _go.transform.position;
            for (int i = 0; i < 30; i++) _station.Tick(0.1f, centre);

            var progressLine = _go.transform.Find("progress");
            Assert.IsNotNull(progressLine, "the progress element must exist");

            var lr = progressLine.GetComponent<LineRenderer>();
            Assert.IsNotNull(lr);
            Assert.IsTrue(lr.enabled, "the progress ring must be visible while holding");

            // With partial progress the arc must not be a full closed circle.
            Vector3 first = lr.GetPosition(0);
            Vector3 last = lr.GetPosition(lr.positionCount - 1);
            Assert.Less(_station.Progress01, 1f, "sanity: this is a partial hold");
            Assert.Greater((first - last).sqrMagnitude, 1e-6f,
                "a partial hold must draw a partial arc, not a closed ring");
        }

        [Test]
        public void LeavingBleedsProgressRatherThanZeroingIt()
        {
            Vector3 centre = _go.transform.position;
            Vector3 far = centre + new Vector3(50f, 0f, 0f);

            for (int i = 0; i < 40; i++) _station.Tick(0.1f, centre);
            float held = _station.Progress01;
            Assert.Greater(held, 0f);

            _station.Tick(0.5f, far);
            float afterLeaving = _station.Progress01;

            Assert.Less(afterLeaving, held, "leaving must bleed progress");
            Assert.Greater(afterLeaving, 0f, "being chased off must not erase the whole attempt");
        }

        [Test]
        public void CompletingTheHoldFinishesTheStationAndRecordsIt()
        {
            Vector3 centre = _go.transform.position;
            for (int i = 0; i < 400; i++) _station.Tick(0.1f, centre);   // 40 s, well past the 12 s hold

            Assert.IsTrue(_station.Finished, "a completed hold must finish the station");
            Assert.AreEqual(1f, _station.Progress01, 1e-3);
            Assert.AreEqual(StationRegistry.Status.Completed,
                StationRegistry.StatusOf(12345L, Time.time),
                "completion must be recorded in the ledger so recycling cannot undo it");
        }

        [Test]
        public void StandingOutsideNeverAdvancesProgress()
        {
            Vector3 far = _go.transform.position + new Vector3(20f, 0f, 0f);
            for (int i = 0; i < 50; i++) _station.Tick(0.1f, far);
            Assert.AreEqual(0f, _station.Progress01, 1e-4);
        }
    }
}
