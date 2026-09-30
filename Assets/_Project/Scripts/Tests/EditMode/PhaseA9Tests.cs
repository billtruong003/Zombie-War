using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.Stations;
using ZombieWar.Threat;

namespace ZombieWar.Tests
{
    /// <summary>Phase A9: sci-fi station bodies, the Supply Drop and the Heal Zone.</summary>
    public class PhaseA9Tests
    {
        static readonly StationKind[] All =
            { StationKind.SignalRelay, StationKind.SupplyCache, StationKind.BossBeacon, StationKind.SupplyDrop, StationKind.HealZone };

        GameObject _go;

        [SetUp] public void SetUp() { StationRegistry.ResetAll(); ThreatDirector.ResetRunState(); }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            StationRegistry.ResetAll();
            ThreatDirector.ResetRunState();
            RunState.Abandon();
        }

        Station Make(StationKind kind)
        {
            _go = new GameObject("station");
            _go.transform.position = new Vector3(50f, 0f, 50f);
            var signal = _go.AddComponent<WorldSignal>();
            var st = _go.AddComponent<Station>();
            st.Bind(new StationAnchors.Anchor(777L, _go.transform.position, kind), signal);
            return st;
        }

        [Test]
        public void KindsAreAppendedNeverRenumbered()
        {
            Assert.AreEqual(0, (int)StationKind.SignalRelay);
            Assert.AreEqual(1, (int)StationKind.SupplyCache);
            Assert.AreEqual(2, (int)StationKind.BossBeacon);
            Assert.AreEqual(3, (int)StationKind.SupplyDrop);
            Assert.AreEqual(4, (int)StationKind.HealZone);
        }

        [Test]
        public void TheMixGivesEveryKind_RelayMostOften_BeaconRare()
        {
            var counts = All.ToDictionary(k => k, _ => 0);
            for (int i = 0; i < 1000; i++) counts[StationAnchors.KindFor(i / 1000f)]++;
            foreach (var k in All) Assert.Greater(counts[k], 0, k.ToString());
            Assert.AreEqual(counts.Values.Max(), counts[StationKind.SignalRelay]);
            Assert.LessOrEqual(counts[StationKind.BossBeacon], 150);
        }

        [Test]
        public void EveryKindHasItsOwnColourAndIconShape()
        {
            for (int i = 0; i < All.Length; i++)
                for (int j = i + 1; j < All.Length; j++)
                {
                    var a = WorldSignal.ColorOf(All[i]); var b = WorldSignal.ColorOf(All[j]);
                    float d = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                    Assert.Greater(d, 0.35f, $"{All[i]} vs {All[j]} colour");
                    Assert.AreNotEqual(WorldSignal.IconSidesFor(All[i]), WorldSignal.IconSidesFor(All[j]), $"{All[i]} vs {All[j]} icon");
                }
        }

        [Test]
        public void ASupplyDropOpensAfterAShortStand_AndIsNotAnObjective()
        {
            var st = Make(StationKind.SupplyDrop);
            var centre = _go.transform.position;
            st.Tick(0.5f, centre);
            Assert.Greater(st.Progress01, 0f);
            st.Tick(0.5f, centre + new Vector3(20f, 0f, 0f));   // walked off: resets, no accident
            Assert.AreEqual(0f, st.Progress01, 1e-4);
            for (int i = 0; i < 20; i++) st.Tick(0.1f, centre);
            Assert.IsTrue(st.Finished);
            Assert.AreEqual(StationRegistry.Status.Completed, StationRegistry.StatusOf(777L, Time.time));
            Assert.AreEqual(0, ThreatDirector.ObjectiveProgress, "loot does not raise the threat");
        }

        [Test]
        public void AHealZoneSwitchesOnAndRuns_AndIsNotAnObjective()
        {
            var st = Make(StationKind.HealZone);
            var centre = _go.transform.position;
            for (int i = 0; i < 20; i++) st.Tick(0.1f, centre);
            Assert.IsTrue(st.Healing, "on after the stand");
            Assert.IsFalse(st.Finished, "the field runs before the zone is spent");
            Assert.AreEqual(SignalState.Active, st.Signal.State);
            Assert.AreEqual(0, ThreatDirector.ObjectiveProgress);
        }

        [Test]
        public void ARelayIsStillAnObjective()
        {
            StationDirector.ReportCompleted(StationKind.SignalRelay);
            StationDirector.ReportCompleted(StationKind.SupplyCache);
            StationDirector.ReportCompleted(StationKind.SupplyDrop);
            StationDirector.ReportCompleted(StationKind.HealZone);
            Assert.AreEqual(1, ThreatDirector.ObjectiveProgress);
        }

        [Test]
        public void ACooledDownHealZoneReArmsInPlace()
        {
            var st = Make(StationKind.HealZone);
            StationRegistry.SetStatus(777L, StationRegistry.Status.Cooldown, Time.time - 10f, 5f);   // already expired
            st.Signal.SetState(SignalState.Cooldown);
            typeof(Station).GetField("_finished", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(st, true);
            st.Tick(0.1f, _go.transform.position + new Vector3(30f, 0f, 0f));
            Assert.IsFalse(st.Finished);
            Assert.AreEqual(SignalState.Idle, st.Signal.State);
        }

        [Test]
        public void StationArtDressesEveryKind()
        {
            // A9b: hex pad for Relay, Cache and Beacon (its lines take the type colour), crystal
            // plinth for the Heal Zone, drop pod for the Supply Drop. No floating icons.
            var art = Resources.Load<StationArt>(StationArt.ResourcePath);
            Assert.IsNotNull(art);
            Assert.IsNotNull(art.dissolveMaterial);
            foreach (var k in All)
            {
                var body = art.BodyFor(k);
                Assert.IsNotNull(body, k + " body");
                Assert.IsNull(art.IconFor(k), k + " has no floating icon");
                Assert.IsNotNull(art.CompleteFxFor(k), k + " burst");
                Assert.IsNotNull(body.GetComponent<StationVisual>(), k + " visual");
                bool glow = false;
                foreach (var r in body.GetComponentsInChildren<Renderer>(true))
                    if (r.name.EndsWith("_Glow")) { glow = true; Assert.AreEqual("HordeCall/Station/Energy", r.sharedMaterial.shader.name, k + " glow material"); }
                Assert.IsTrue(glow, k + " has energy lines");
            }
            Assert.AreSame(art.BodyFor(StationKind.SignalRelay), art.BodyFor(StationKind.SupplyCache));
            Assert.AreSame(art.BodyFor(StationKind.SignalRelay), art.BodyFor(StationKind.BossBeacon));
            Assert.IsNotNull(art.healFieldFx);
            Assert.IsNotNull(art.healTickFx);
        }

        [Test]
        public void TheZoneIsTheModelsOwnLitBoundary()
        {
            Assert.AreEqual(2.9f, Station.RadiusFor(StationKind.SignalRelay), 1e-4);   // hex pad rim seam
            Assert.AreEqual(2.45f, Station.RadiusFor(StationKind.SupplyDrop), 1e-4);   // pod ground ring
            Assert.AreEqual(2.2f, Station.RadiusFor(StationKind.HealZone), 1e-4);      // plinth inner ring
        }

        [Test]
        public void ASignalWithABodyHidesItsLinesAndFeedsTheBody()
        {
            _go = new GameObject("station");
            var signal = _go.AddComponent<WorldSignal>();
            signal.Configure(StationKind.SignalRelay, 2.9f);
            var bodyGo = new GameObject("body"); bodyGo.transform.SetParent(_go.transform);
            var visual = bodyGo.AddComponent<StationVisual>();
            signal.UseVisual(visual);
            foreach (var lr in _go.GetComponentsInChildren<LineRenderer>(true)) Assert.IsFalse(lr.enabled, lr.name + " hidden");
            signal.SetProgress(0.4f);
            Assert.AreEqual(0.4f, visual.Fill, 1e-4);
            Assert.IsFalse(visual.Vanishing);
            visual.Vanish();
            Assert.IsTrue(visual.Vanishing, "paid out: it dissolves away");
        }
    }
}
