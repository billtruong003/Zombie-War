using System.Collections;
using System.Reflection;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// <summary>
    /// The half of the audio contract that only exists once coroutines actually run: fades, duck
    /// ramps, and the stale-transition guard that fires when an older fade completes after a newer
    /// one has taken over its source. EditMode cannot cover these - Unity does not tick coroutines
    /// outside Play Mode - so the synchronous decisions live in AudioTransitionTests and the
    /// time-dependent behaviour lives here.
    /// </summary>
    public class AudioTransitionPlayTests
    {
        private GameObject _host;
        private AudioService _audio;
        private AudioLibrary _lib;

        private static AudioClip MakeClip(string name, float seconds)
        {
            const int rate = 4000;
            return AudioClip.Create(name, Mathf.Max(1, (int)(rate * seconds)), 1, rate, false);
        }

        private static AudioLibrary.Entry Entry(string key, AudioClip clip, bool loop = false) =>
            new AudioLibrary.Entry { key = key, clip = clip, volume = 1f, pitch = 1f, pitchVariation = 0f, loop = loop };

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            _host = new GameObject("AudioServicePlayTestHost");
            _audio = _host.AddComponent<AudioService>();

            _lib = ScriptableObject.CreateInstance<AudioLibrary>();
            _lib.ReplaceEntries(new[]
            {
                Entry("music.a", MakeClip("MusicA", 2f), true),
                Entry("music.b", MakeClip("MusicB", 2f), true),
                Entry("stinger.long", MakeClip("Stinger", 6f)),
                Entry("sfx.combat", MakeClip("Combat", 3f)),
            });

            _audio.Initialize();
            typeof(AudioService).GetField("_lib", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_audio, _lib);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (_host != null) Object.DestroyImmediate(_host);
            if (_lib != null) Object.DestroyImmediate(_lib);
        }

        private AudioSource MusicA => (AudioSource)typeof(AudioService)
            .GetField("_musicA", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_audio);
        private AudioSource MusicB => (AudioSource)typeof(AudioService)
            .GetField("_musicB", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_audio);

        private static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            // Frame-bounded as well as time-bounded so a stalled clock can never hang the runner.
            for (int i = 0; i < 600 && t < seconds; i++) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator StaleFade_DoesNotStopTheSourceANewerTransitionTookOver()
        {
            // The exact regression that made music silent everywhere: transition #1's fade-out
            // completion stopping the source transition #2 had already started playing on.
            _audio.PlayMusic("music.a", 0.25f);
            yield return null;

            _audio.PlayMusic("music.b", 0.25f);   // supersedes mid-fade
            yield return Wait(0.6f);              // past transition #1's original completion time

            bool anyAudible = (MusicA.isPlaying && MusicA.volume > 0.01f)
                           || (MusicB.isPlaying && MusicB.volume > 0.01f);
            Assert.IsTrue(anyAudible, "the superseding track must survive the stale fade's callback");
            Assert.AreEqual("music.b", _audio.CurrentMusicKey);
        }

        [UnityTest]
        public IEnumerator SettledSequence_LeavesExactlyOneAudibleSource()
        {
            _audio.PlayMusic("music.a", 0.2f);
            yield return Wait(0.3f);
            _audio.PlayMusic("music.b", 0.2f);
            yield return Wait(0.6f);

            int audible = 0;
            if (MusicA.isPlaying && MusicA.volume > 0.01f) audible++;
            if (MusicB.isPlaying && MusicB.volume > 0.01f) audible++;

            Assert.AreEqual(1, audible,
                "outside an intentional crossfade exactly one music source may be audible");
        }

        [UnityTest]
        public IEnumerator RapidTransitions_DoNotLeaveTwoTracksRunning()
        {
            // Menu -> Gameplay -> Menu at speed, which is the Retry/Home case.
            _audio.PlayMusic("music.a", 0.3f);
            yield return null;
            _audio.PlayMusic("music.b", 0.3f);
            yield return null;
            _audio.PlayMusic("music.a", 0.3f);
            yield return Wait(0.8f);

            int audible = 0;
            if (MusicA.isPlaying && MusicA.volume > 0.01f) audible++;
            if (MusicB.isPlaying && MusicB.volume > 0.01f) audible++;

            Assert.AreEqual(1, audible, "rapid transitions must still settle on one track");
            Assert.AreEqual("music.a", _audio.CurrentMusicKey);
        }

        [UnityTest]
        public IEnumerator Duck_AttenuatesMusic_AndRestoresOnRelease()
        {
            _audio.PlayMusic("music.a", 0f);
            yield return null;
            var live = MusicA.isPlaying ? MusicA : MusicB;
            float full = live.volume;
            Assert.Greater(full, 0.01f, "precondition: music must be audible before ducking");

            int token = _audio.Duck(AudioChannel.Music, 0.25f, 0f);
            yield return Wait(0.1f);
            Assert.Less(live.volume, full * 0.5f, "duck must attenuate the live bed");

            _audio.Unduck(token, 0f);
            yield return Wait(0.1f);
            Assert.AreEqual(full, live.volume, 0.02f, "release must restore the original level");
        }

        [UnityTest]
        public IEnumerator OverlappingDucks_ReleasingOneDoesNotRestoreOverTheOther()
        {
            _audio.PlayMusic("music.a", 0f);
            yield return null;
            var live = MusicA.isPlaying ? MusicA : MusicB;
            float full = live.volume;

            int shallow = _audio.Duck(AudioChannel.Music, 0.6f, 0f);
            int deep = _audio.Duck(AudioChannel.Music, 0.2f, 0f);
            yield return Wait(0.1f);

            _audio.Unduck(shallow, 0f);   // the deeper duck is still held
            yield return Wait(0.1f);
            Assert.Less(live.volume, full * 0.5f,
                "releasing the shallower duck must not lift volume over a duck still held");

            _audio.Unduck(deep, 0f);
            yield return Wait(0.1f);
            Assert.AreEqual(full, live.volume, 0.02f);
        }

        [UnityTest]
        public IEnumerator DuckCancelledMidAttack_StillRestoresFully()
        {
            _audio.PlayMusic("music.a", 0f);
            yield return null;
            var live = MusicA.isPlaying ? MusicA : MusicB;
            float full = live.volume;

            int token = _audio.Duck(AudioChannel.Music, 0.1f, 0.4f);
            yield return null;                 // interrupt while the attack ramp is still running
            _audio.Unduck(token, 0f);
            yield return Wait(0.2f);

            Assert.AreEqual(full, live.volume, 0.02f,
                "a cancelled duck must not strand the mix at a partial level");
        }

        private AudioSource SourceOf(AudioCueHandle h)
        {
            var voices = (System.Collections.IList)typeof(AudioService)
                .GetField("_voices", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_audio);
            var v = voices[h.VoiceIndex];
            return (AudioSource)v.GetType().GetField("source").GetValue(v);
        }

        [UnityTest]
        public IEnumerator ResultStingerIsSilenced_BeforeTheNewBedComesUp()
        {
            // Victory -> Retry. The stinger is 6s; the retry lands after 0.5s. Previously the
            // fanfare kept playing straight over the new run bed for the remaining 5.5 seconds.
            var stinger = _audio.PlayManagedCue("stinger.long", SfxPriority.Critical, CueGroup.Critical, 1f);
            _audio.PlayMusic("music.a", 0f);
            yield return Wait(0.5f);
            Assert.IsTrue(_audio.IsCueAlive(stinger), "precondition: stinger still running at retry time");

            _audio.StopCue(stinger, 0.2f);          // what CancelResultSequence does
            _audio.PlayMusic("music.b", 0.2f);      // run bed comes up
            yield return Wait(0.5f);

            Assert.IsFalse(_audio.IsCueAlive(stinger), "the stinger must not survive the retry");
            Assert.AreEqual("music.b", _audio.CurrentMusicKey);
        }

        [UnityTest]
        public IEnumerator ResultStingerIsSilenced_OnHomeToHub()
        {
            var stinger = _audio.PlayManagedCue("stinger.long", SfxPriority.Critical, CueGroup.Critical, 1f);
            yield return Wait(0.5f);

            _audio.StopCue(stinger, 0.2f);
            _audio.PlayMusic("music.a", 0.2f);      // hub bed
            yield return Wait(0.5f);

            Assert.IsFalse(_audio.IsCueAlive(stinger));
            Assert.AreEqual("music.a", _audio.CurrentMusicKey);
        }

        [UnityTest]
        public IEnumerator CombatDuckRamp_MovesLiveVoicesOverTime()
        {
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 8, perKeyLimit = 9, perKeyRetrigger = 0f });
            var combat = _audio.PlayManagedCue("sfx.combat", SfxPriority.Medium, CueGroup.Combat, 1f);
            float before = SourceOf(combat).volume;

            int token = _audio.DuckGroup(CueGroup.Combat, 0.2f, 0.15f);
            yield return Wait(0.35f);
            Assert.Less(SourceOf(combat).volume, before * 0.5f, "ramp must reach the live voice");

            _audio.Unduck(token, 0.15f);
            yield return Wait(0.35f);
            Assert.AreEqual(before, SourceOf(combat).volume, 0.02f, "release must restore exactly");
        }

        [UnityTest]
        public IEnumerator SupersededDuckRamp_StopsWritingImmediately()
        {
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 8, perKeyLimit = 9, perKeyRetrigger = 0f });
            var combat = _audio.PlayManagedCue("sfx.combat", SfxPriority.Medium, CueGroup.Combat, 1f);
            float full = SourceOf(combat).volume;

            int deep = _audio.DuckGroup(CueGroup.Combat, 0.1f, 1.0f);   // slow ramp we will supersede
            yield return null;
            int shallow = _audio.DuckGroup(CueGroup.Combat, 0.5f, 0f);  // instant, supersedes the ramp
            yield return Wait(0.3f);

            // If the slow ramp were still writing, the level would keep sliding toward 0.1.
            float settled = SourceOf(combat).volume;
            yield return Wait(0.3f);
            Assert.AreEqual(settled, SourceOf(combat).volume, 0.02f,
                "a superseded ramp must stop writing, not keep dragging the level");

            // Both tokens must go: releasing only the shallow one correctly leaves the deeper
            // request holding the bus, which is the overlapping-duck contract, not a bug.
            _audio.Unduck(shallow, 0f);
            _audio.Unduck(deep, 0f);
            yield return Wait(0.1f);
            Assert.AreEqual(full, SourceOf(combat).volume, 0.02f, "final release restores exactly 1.0 gain");
        }

        [UnityTest]
        public IEnumerator StopMusicDuringCrossfade_LeavesBothSourcesStopped()
        {
            _audio.PlayMusic("music.a", 0f);
            _audio.PlayMusic("music.b", 0.6f);   // crossfade in flight
            yield return Wait(0.2f);
            _audio.StopMusic(0.15f);
            yield return Wait(0.5f);

            Assert.IsFalse(MusicA.isPlaying, "MusicA must be stopped");
            Assert.IsFalse(MusicB.isPlaying, "MusicB must be stopped - the interrupted incoming source too");
        }

        [UnityTest]
        public IEnumerator HubRunResultRetry_EndsWithOnlyRunBed()
        {
            _audio.PlayMusic("music.a", 0.2f);   // hub
            yield return null;
            _audio.PlayMusic("music.b", 0.2f);   // run
            yield return null;
            _audio.StopMusic(0.2f);              // result
            yield return null;
            _audio.PlayMusic("music.b", 0.2f);   // retry -> run again
            yield return Wait(0.7f);

            int audible = 0;
            if (MusicA.isPlaying && MusicA.volume > 0.01f) audible++;
            if (MusicB.isPlaying && MusicB.volume > 0.01f) audible++;
            Assert.AreEqual(1, audible);
            Assert.AreEqual("music.b", _audio.CurrentMusicKey);
        }

        [UnityTest]
        public IEnumerator RunResultHome_EndsWithOnlyHubBed()
        {
            _audio.PlayMusic("music.b", 0.2f);   // run
            yield return null;
            _audio.StopMusic(0.2f);              // result
            yield return null;
            _audio.PlayMusic("music.a", 0.2f);   // home -> hub
            yield return Wait(0.7f);

            int audible = 0;
            if (MusicA.isPlaying && MusicA.volume > 0.01f) audible++;
            if (MusicB.isPlaying && MusicB.volume > 0.01f) audible++;
            Assert.AreEqual(1, audible);
            Assert.AreEqual("music.a", _audio.CurrentMusicKey);
        }

    }
}