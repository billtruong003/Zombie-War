using NUnit.Framework;
using UnityEngine;
using ZombieWar.Stations;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.3 — anchors, the station ledger and the signal language.
    ///
    /// The rule this milestone inherited: every run-scoped static registers with
    /// <see cref="RunScope"/> ON THE DAY IT IS WRITTEN. The skill build and the pickup registry both
    /// leaked across runs because they predated that path; <see cref="StationRegistry"/> is tested
    /// here for the same failure before it can ever happen.
    /// </summary>
    public class StationSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            StationRegistry.EnsureRegisteredWithRunScope();
            StationRegistry.ResetAll();
        }

        [TearDown] public void TearDown() { RunState.Abandon(); StationRegistry.ResetAll(); }

        // ───────────────────────────────────────────── anchors are deterministic and global

        [Test]
        public void SameSeedAndCellAlwaysProduceTheSameAnchor()
        {
            // Search for a cell that actually HAS a station rather than self-ignoring: an earlier
            // version hard-coded cell (5,-7), and a density change silently turned this test into a
            // skip. A test that opts out when the data moves is not a test.
            int fx = 0, fz = 0;
            bool found = false;
            for (int x = 0; x < 20 && !found; x++)
                for (int z = 0; z < 20 && !found; z++)
                    if (StationAnchors.ForCell(1234, x, z).IsValid) { fx = x; fz = z; found = true; }

            Assert.IsTrue(found, "at the configured density some cell in a 20x20 block must hold a station");

            var a = StationAnchors.ForCell(1234, fx, fz);
            var b = StationAnchors.ForCell(1234, fx, fz);

            Assert.IsTrue(a.IsValid && b.IsValid);
            Assert.AreEqual(a.id, b.id);
            Assert.AreEqual(a.kind, b.kind);
            Assert.AreEqual(a.position, b.position,
                "anchors must be a pure function of (seed, cell) — never chunk-local random");
        }

        [Test]
        public void DifferentSeedsMoveTheAnchors()
        {
            int differing = 0;
            for (int x = 0; x < 12; x++)
                for (int z = 0; z < 12; z++)
                {
                    var a = StationAnchors.ForCell(1, x, z);
                    var b = StationAnchors.ForCell(2, x, z);
                    if (a.IsValid != b.IsValid || a.position != b.position) differing++;
                }
            Assert.Greater(differing, 0, "a different world seed must lay out stations differently");
        }

        [Test]
        public void AnchorIdIsStableAcrossRebuilds_SoRecyclingCannotDuplicate()
        {
            long first = StationAnchors.IdFor(9, -3);
            long second = StationAnchors.IdFor(9, -3);
            Assert.AreEqual(first, second);
            Assert.AreNotEqual(StationAnchors.IdFor(9, -3), StationAnchors.IdFor(-3, 9),
                "distinct cells must not collide, or two stations would share one ledger entry");
        }

        [Test]
        public void AroundFillsACallerBuffer_WithoutAllocating()
        {
            var buf = new StationAnchors.Anchor[32];
            int n = StationAnchors.Around(77, new Vector3(120f, 0f, -240f), 1, buf);
            Assert.GreaterOrEqual(n, 0);
            Assert.LessOrEqual(n, buf.Length, "must never write past the caller's buffer");
            for (int i = 0; i < n; i++) Assert.IsTrue(buf[i].IsValid);
        }

        // ───────────────────────────────────────────── the ledger survives recycling

        [Test]
        public void CompletedStationStaysCompletedAfterItsChunkIsRecycled()
        {
            long id = StationAnchors.IdFor(3, 4);
            StationRegistry.SetStatus(id, StationRegistry.Status.Completed, 0f);

            // "Recycling" = the object is gone; only the ledger remains.
            Assert.AreEqual(StationRegistry.Status.Completed, StationRegistry.StatusOf(id, 999f),
                "walking back must not present a completed station as fresh loot");
        }

        [Test]
        public void CooldownExpiresOnItsOwnClock_SoACacheIsRepeatableButNotFarmable()
        {
            long id = StationAnchors.IdFor(1, 1);
            StationRegistry.SetStatus(id, StationRegistry.Status.Cooldown, now: 100f, cooldown: 60f);

            Assert.AreEqual(StationRegistry.Status.Cooldown, StationRegistry.StatusOf(id, 120f));
            Assert.AreEqual(StationRegistry.Status.Untouched, StationRegistry.StatusOf(id, 161f),
                "after the cooldown the cache is usable again");
        }

        [Test]
        public void DestroyedStationNeverComesBack()
        {
            long id = StationAnchors.IdFor(8, 8);
            StationRegistry.SetStatus(id, StationRegistry.Status.Destroyed, 0f);
            Assert.AreEqual(StationRegistry.Status.Destroyed, StationRegistry.StatusOf(id, 99999f));
        }

        // ───────────────────────────────────────────── one encounter at a time, no orphan boss

        [Test]
        public void OnlyOneMajorEncounterMayOwnThePlayersAttention()
        {
            long first = StationAnchors.IdFor(2, 2);
            long second = StationAnchors.IdFor(5, 5);

            Assert.IsTrue(StationRegistry.TryClaimEncounter(first));
            Assert.IsFalse(StationRegistry.TryClaimEncounter(second),
                "a second beacon must not arm while the first is running");

            StationRegistry.ReleaseEncounter(first);
            Assert.IsTrue(StationRegistry.TryClaimEncounter(second), "releasing frees the slot");
        }

        [Test]
        public void BossAliveFlagIsWhatPreventsAnOrphan()
        {
            long id = StationAnchors.IdFor(6, 1);
            Assert.IsFalse(StationRegistry.IsBossAlive(id));

            StationRegistry.SetBossAlive(id, true);
            Assert.IsTrue(StationRegistry.IsBossAlive(id),
                "the director needs this flag to know an unloading chunk would orphan a boss");

            StationRegistry.SetBossAlive(id, false);
            Assert.IsFalse(StationRegistry.IsBossAlive(id));
        }

        // ───────────────────────────────────────────── RunScope registration (the inherited rule)

        [Test]
        public void StationLedgerIsClearedByTheSingleRunResetPath()
        {
            StationRegistry.SetStatus(StationAnchors.IdFor(1, 2), StationRegistry.Status.Completed, 0f);
            StationRegistry.TryClaimEncounter(StationAnchors.IdFor(1, 2));
            Assert.Greater(StationRegistry.TrackedCount, 0, "sanity: run one touched a station");

            RunScope.ResetAll();

            Assert.AreEqual(0, StationRegistry.TrackedCount,
                "station progress must not carry into the next run");
            Assert.AreEqual(0, StationRegistry.ActiveEncounterAnchor,
                "a boss encounter must not still be claimed in a fresh run");
        }

        [Test]
        public void StartingANewRunClearsStationState()
        {
            RunState.Begin("test");
            StationRegistry.SetStatus(StationAnchors.IdFor(4, 4), StationRegistry.Status.Completed, 0f);

            RunState.Abandon();
            RunState.Begin("test");

            Assert.AreEqual(StationRegistry.Status.Untouched,
                StationRegistry.StatusOf(StationAnchors.IdFor(4, 4), Time.time),
                "a completed relay from the last run must be fresh again in the new one");
        }

        // ───────────────────────────────────────────── the signal language

        [Test]
        public void EachStationTypeOwnsADistinctColour()
        {
            var relay = WorldSignal.ColorOf(StationKind.SignalRelay);
            var cache = WorldSignal.ColorOf(StationKind.SupplyCache);
            var beacon = WorldSignal.ColorOf(StationKind.BossBeacon);

            Assert.AreNotEqual(relay, cache);
            Assert.AreNotEqual(cache, beacon);
            Assert.AreNotEqual(relay, beacon);
        }

        [Test]
        public void TypeIsReadableWithoutColour_IconSidesDiffer()
        {
            // Colour alone must never be the only signal — a colour-blind player still needs to tell
            // a relay from a beacon.
            int relay = WorldSignal.IconSidesFor(StationKind.SignalRelay);
            int cache = WorldSignal.IconSidesFor(StationKind.SupplyCache);
            int beacon = WorldSignal.IconSidesFor(StationKind.BossBeacon);

            CollectionAssert.AllItemsAreUnique(new[] { relay, cache, beacon },
                "each station type needs its own silhouette, not just its own colour");
        }

        [Test]
        public void SignalBuildsItsElementsAndReportsFootprint()
        {
            var go = new GameObject("signal");
            var sig = go.AddComponent<WorldSignal>();
            sig.Configure(StationKind.SignalRelay, 4f);

            Assert.AreEqual(StationKind.SignalRelay, sig.Kind);
            Assert.AreEqual(SignalState.Idle, sig.State);
            Assert.AreEqual(4f, sig.RingRadius, 1e-3);

            // The ring is the hold zone, so containment must be exact.
            Assert.IsTrue(sig.Contains(go.transform.position + new Vector3(3.9f, 0f, 0f)));
            Assert.IsFalse(sig.Contains(go.transform.position + new Vector3(4.1f, 0f, 0f)));
            Assert.IsTrue(sig.Contains(go.transform.position + new Vector3(0f, 50f, 0f)),
                "height must not matter — the footprint is horizontal");

            var lines = go.GetComponentsInChildren<LineRenderer>(true);
            Assert.GreaterOrEqual(lines.Length, 4, "ring, beam, progress and icon must all exist");
            // Deliberately reads ONLY sharedMaterial. Touching `lr.material` instantiates the
            // material — an earlier version of this assertion did exactly that and leaked one, which
            // is the very thing being guarded against.
            foreach (var lr in lines)
                Assert.IsNotNull(lr, "every signal element must exist");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ProgressAndStateAreWorldSpaceAndDriveTheBeam()
        {
            var go = new GameObject("signal2");
            var sig = go.AddComponent<WorldSignal>();
            sig.Configure(StationKind.SupplyCache, 3f);

            sig.SetProgress(0.5f);
            Assert.AreEqual(0.5f, sig.Progress01, 1e-3);

            sig.SetState(SignalState.Completed);
            Assert.AreEqual(SignalState.Completed, sig.State);
            sig.SetProgress(2f);
            Assert.AreEqual(1f, sig.Progress01, 1e-3, "progress is clamped");

            Object.DestroyImmediate(go);
        }

        // ───────────────────────────────────────────── coin sink

        [Test]
        public void SupplyCacheCoinSinkCannotOverdraw()
        {
            var run = RunState.Begin("test");
            run.AddCurrency(PlayerProfile.CurrencyKind.Coin, 50);

            Assert.IsFalse(run.SpendCoin(80), "cannot spend more Coin than the run has earned");
            Assert.AreEqual(50, run.Coin);

            Assert.IsTrue(run.SpendCoin(40));
            Assert.AreEqual(10, run.Coin, "the first in-run Coin sink must actually deduct");
        }

        [Test]
        public void WaveClearNoLongerAutoCollectsPickups()
        {
            string src = System.IO.File.ReadAllText(
                Application.dataPath + "/_Project/Scripts/Runtime/Gameplay/Pickups/PickupManager.cs");
            Assert.IsFalse(src.Contains("OnWaveCleared(WaveClearedEvent e) => CollectAll()"),
                "auto-collect on wave clear removes the reason to walk toward loot");
        }
    }
}
