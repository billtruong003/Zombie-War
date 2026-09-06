using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5.1.1 CP2 - the aim-lock stability contract.
    ///
    /// The danger-radius branch bypasses dwell and stickiness, so two attackers at near-equal
    /// range could alternate the lock on every recompute - aim flicker at up to the recompute
    /// rate. These tests fabricate exactly that situation with stub targets and assert the lock
    /// holds, while a genuinely closer threat, or the current target dying, still switches
    /// immediately.
    /// </summary>
    public class TargetStabilityTests
    {
        private sealed class StubTarget : MonoBehaviour, ITargetable
        {
            public bool Targetable = true;
            Transform ITargetable.Transform => transform;
            bool ITargetable.IsTargetable => Targetable;

            private void OnEnable() => TargetRegistry.Register(this);
            private void OnDisable() => TargetRegistry.Unregister(this);
        }

        private GameObject _playerGo;
        private PlayerMovement _player;
        private StubTarget _a, _b;

        [SetUp]
        public void SetUp()
        {
            // DisableDomainReload keeps statics alive across play sessions; purge any leftover
            // registry entries from a previous live session so the nearest-target scan only ever
            // sees this fixture's stubs.
            var targets = (System.Collections.IList)typeof(TargetRegistry)
                .GetField("_targets", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            targets.Clear();
            RunState.Abandon();

            _playerGo = new GameObject("StabilityPlayer", typeof(Rigidbody));
            _playerGo.GetComponent<Rigidbody>().isKinematic = true;
            _player = _playerGo.AddComponent<PlayerMovement>();
            typeof(PlayerMovement).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, _player);

            _a = MakeTarget("A", new Vector3(4f, 0f, 0f));
            _b = MakeTarget("B", new Vector3(0f, 0f, 4f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in new[] { _a, _b })
                if (s != null) Object.DestroyImmediate(s.gameObject);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
        }

        private static StubTarget MakeTarget(string name, Vector3 pos)
        {
            var go = new GameObject("Stub_" + name);
            go.transform.position = pos;
            return go.AddComponent<StubTarget>();
        }

        /// <summary>Runs enough fixed steps for several aim recomputes (interval 0.1 s).</summary>
        private static IEnumerator RunFixedSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator EqualDistanceDangerTargets_DoNotOscillate()
        {
            yield return RunFixedSeconds(0.3f);   // acquire an initial lock

            int switches = 0;
            Transform last = _player.AimTargetTransform;
            float end = Time.time + 2f;
            int flip = 0;
            while (Time.time < end)
            {
                // Alternate which stub is a few centimetres nearer - the coin-flip case.
                flip++;
                float jitter = (flip % 2 == 0) ? 0.05f : -0.05f;
                _a.transform.position = new Vector3(4f + jitter, 0f, 0f);
                _b.transform.position = new Vector3(0f, 0f, 4f - jitter);

                yield return new WaitForFixedUpdate();
                var t = _player.AimTargetTransform;
                if (!ReferenceEquals(t, last)) { switches++; last = t; }
            }

            Assert.LessOrEqual(switches, 2,
                $"aim lock flickered between two near-equidistant danger targets ({switches} switches in 2 s)");
        }

        [UnityTest]
        public IEnumerator DeadDangerTarget_IsReplacedImmediately()
        {
            yield return RunFixedSeconds(0.3f);
            Assert.IsNotNull(_player.AimTargetTransform, "no initial lock acquired");

            var locked = _player.AimTargetTransform;
            var lockedStub = locked.GetComponent<StubTarget>();
            Debug.Log($"[TSDBG] initial lock={locked.name} aStub={_a.name} bStub={_b.name}");
            lockedStub.Targetable = false;   // "died" this frame

            yield return RunFixedSeconds(0.25f);   // at most a couple of recomputes

            var current = _player.AimTargetTransform;
            string diag = $"locked={(locked != null ? locked.name : "null")} " +
                          $"current={(current != null ? current.name : "null")} " +
                          $"aTargetable={_a.Targetable} bTargetable={_b.Targetable} " +
                          $"hasTarget={_player.HasTarget} " +
                          $"aPos={_a.transform.position} bPos={_b.transform.position} " +
                          $"playerPos={_playerGo.transform.position}";
            Assert.IsNotNull(current, "lock was dropped instead of replaced: " + diag);
            Assert.IsFalse(ReferenceEquals(locked, current), "a dead target must not keep the aim lock: " + diag);
        }

        [UnityTest]
        public IEnumerator MateriallyCloserThreat_StealsTheLock()
        {
            yield return RunFixedSeconds(0.3f);
            var locked = _player.AimTargetTransform;
            Assert.IsNotNull(locked);

            // Move the OTHER stub decisively closer than the current lock.
            var other = ReferenceEquals(locked, _a.transform) ? _b : _a;
            other.transform.position = _playerGo.transform.position + new Vector3(1.5f, 0f, 0f);

            yield return RunFixedSeconds(0.25f);

            Assert.AreEqual(other.transform, _player.AimTargetTransform,
                "a materially closer danger target must take the lock promptly");
        }

        [UnityTest]
        public IEnumerator TieBreak_IsDeterministicForEquivalentState()
        {
            yield return RunFixedSeconds(0.5f);
            var first = _player.AimTargetTransform;
            yield return RunFixedSeconds(0.5f);
            Assert.AreEqual(first, _player.AimTargetTransform,
                "with static equidistant targets the lock must not wander");
        }
    }
}
