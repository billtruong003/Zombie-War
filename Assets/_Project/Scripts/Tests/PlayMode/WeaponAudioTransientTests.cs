using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BillGameCore;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5.1.3 CP3 - the weapon-audio completeness contract, proven head-to-head against the old path.
    ///
    /// A human playtest heard silent pistol shots. Cause: <see cref="AudioService.PlayCue"/> counts a
    /// voice busy for the WHOLE imported clip (handgun 1.25 s), so at pistol cadence the global
    /// perKeyLimit=3 dropped every shot past the third overlap. These tests reproduce that drop, then
    /// prove the bounded guaranteed-transient path accepts every visible shot within its budget.
    /// </summary>
    public class WeaponAudioTransientTests
    {
        private const string Key = "test.weapon.fire";
        private const float ClipSeconds = 1.25f;   // matches the production handgun clip length

        private GameObject _serviceGo;
        private AudioService _service;
        private readonly List<AudioClip> _clips = new();

        [SetUp]
        public void SetUp()
        {
            _serviceGo = new GameObject("TestAudioService");
            _service = _serviceGo.AddComponent<AudioService>();
            _service.Initialize();
            _service.ConfigureVoices(VoicePolicy.Default);   // maxVoices 16, perKeyLimit 3

            // Three variants, like the production handgun.
            var entries = new List<AudioLibrary.Entry>();
            for (int i = 0; i < 3; i++)
            {
                var clip = AudioClip.Create($"variant{i}", (int)(44100 * ClipSeconds), 1, 44100, false);
                _clips.Add(clip);
                entries.Add(new AudioLibrary.Entry { key = Key, clip = clip, volume = 1f, pitch = 1f });
            }
            var lib = ScriptableObject.CreateInstance<AudioLibrary>();
            lib.ReplaceEntries(entries.ToArray());
            typeof(AudioService).GetField("_lib", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_service, lib);

            AudioService.Diagnostics.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            if (_serviceGo != null) Object.DestroyImmediate(_serviceGo);
            foreach (var c in _clips) if (c != null) Object.DestroyImmediate(c);
            _clips.Clear();
            AudioService.Diagnostics.Reset();
        }

        /// <summary>Fires <paramref name="shots"/> requests spaced by an unscaled real interval.</summary>
        private IEnumerator FireFor(int shots, float interval, System.Action shot)
        {
            for (int i = 0; i < shots; i++)
            {
                shot();
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        [UnityTest]
        public IEnumerator OldCuePath_DropsPistolShots_ReproducingTheReportedGap()
        {
            const float interval = 0.25f;   // 4 shots/s, the production pistol's authored rate
            yield return FireFor(8, interval, () => _service.PlayCue(Key, SfxPriority.High));

            Assert.Greater(AudioService.Diagnostics.DroppedPerKey, 0,
                "baseline did not reproduce the per-key drop the human playtest heard");
            Assert.Less(AudioService.Diagnostics.Accepted, 8,
                "baseline accepted every shot - the defect is not being reproduced");
        }

        [UnityTest]
        public IEnumerator GuaranteedTransient_AcceptsEveryPistolShot()
        {
            const float interval = 0.25f;
            const int shots = 12;
            yield return FireFor(shots, interval,
                () => _service.PlayGuaranteedTransient(Key, SfxPriority.High, 3));

            Assert.AreEqual(shots, AudioService.Diagnostics.Requested);
            Assert.AreEqual(shots, AudioService.Diagnostics.Accepted,
                "a visible shot was left silent");
            Assert.AreEqual(0, AudioService.Diagnostics.DroppedPerKey);
            Assert.AreEqual(0, AudioService.Diagnostics.DroppedRetrigger);
            Assert.AreEqual(0, AudioService.Diagnostics.DroppedGlobal);
        }

        [UnityTest]
        public IEnumerator GuaranteedTransient_AcceptsEveryShotAtPerkedCadence()
        {
            const float interval = 0.13f;   // ~7.7 shots/s: fastest sidearm with a fire-rate perk
            const int shots = 20;
            yield return FireFor(shots, interval,
                () => _service.PlayGuaranteedTransient(Key, SfxPriority.High, 3));

            Assert.AreEqual(shots, AudioService.Diagnostics.Accepted,
                "perked cadence lost a visible shot");
        }

        [UnityTest]
        public IEnumerator GuaranteedTransient_NeverExceedsItsSameKeyBudget()
        {
            const int budget = 3;
            for (int i = 0; i < 25; i++)
            {
                _service.PlayGuaranteedTransient(Key, SfxPriority.High, budget);
                Assert.LessOrEqual(_service.BusyVoiceCountForKey(Key), budget,
                    "the transient path grew past its same-key bound");
                yield return null;
            }
            Assert.LessOrEqual(_service.BusyVoiceCount, 16, "global voice budget exceeded");
        }

        [UnityTest]
        public IEnumerator ShotgunBudget_AcceptsEveryBlast()
        {
            const float interval = 0.33f;   // AA12 at 3 shots/s
            const int shots = 10;
            yield return FireFor(shots, interval,
                () => _service.PlayGuaranteedTransient(Key, SfxPriority.High, 4));

            Assert.AreEqual(shots, AudioService.Diagnostics.Accepted, "a visible blast was silent");
            Assert.LessOrEqual(_service.BusyVoiceCountForKey(Key), 4);
        }

        [UnityTest]
        public IEnumerator AutomaticRetrigger_HoldsExactlyOneVoice()
        {
            var handle = AudioCueHandle.None;
            for (int i = 0; i < 30; i++)
            {
                handle = _service.RetriggerCue(Key, handle, SfxPriority.High);
                Assert.LessOrEqual(_service.BusyVoiceCountForKey(Key), 1,
                    "the automatic path grew beyond its single managed voice");
                yield return null;
            }
            Assert.IsTrue(_service.IsCueAlive(handle));

            _service.StopCue(handle, 0f);
            Assert.IsFalse(_service.IsCueAlive(handle), "stopping must release the managed voice");
        }

        [UnityTest]
        public IEnumerator AllVariantsRemainReachable_WithoutImmediateRepeats()
        {
            var seen = new HashSet<string>();
            string previous = null;
            for (int i = 0; i < 30; i++)
            {
                // Read the clip off the voice this call actually started - with three overlapping
                // voices, scanning "whatever is playing" reports an older shot, not this one.
                var handle = _service.PlayGuaranteedTransient(Key, SfxPriority.High, 3);
                string current = _service.ClipNameOfVoice(handle.VoiceIndex);
                Assert.IsNotEmpty(current, "the transient path started no clip");

                seen.Add(current);
                if (previous != null) Assert.AreNotEqual(previous, current,
                    "the same variant played twice in a row");
                previous = current;
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Assert.AreEqual(3, seen.Count, "not every authored variant stayed reachable");
        }
    }
}
