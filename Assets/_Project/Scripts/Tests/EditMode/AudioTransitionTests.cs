using System.Reflection;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// <summary>
    /// Synchronous music-transition and voice-pool contracts for <see cref="AudioService"/>.
    ///
    /// Unity does not tick coroutines outside Play Mode, so anything that depends on a fade or duck
    /// ramp actually running lives in AudioTransitionPlayTests instead. What is asserted here is
    /// every decision the service makes on the calling thread: the same-key guard, the transition
    /// generation that invalidates stale fades, the duration used to time the result bed, and the
    /// whole voice-stealing policy.
    /// </summary>
    public class AudioTransitionTests
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

            _host = new GameObject("AudioServiceTestHost");
            _audio = _host.AddComponent<AudioService>();

            _lib = ScriptableObject.CreateInstance<AudioLibrary>();
            _lib.ReplaceEntries(new[]
            {
                Entry("music.a", MakeClip("MusicA", 1f), true),
                Entry("music.b", MakeClip("MusicB", 1f), true),
                Entry("stinger.long", MakeClip("Stinger", 2f)),
                Entry("sfx.low", MakeClip("Low", 1f)),
                Entry("sfx.crit", MakeClip("Crit", 1f)),
                Entry("sfx.spam", MakeClip("Spam", 1f)),
            });

            // The service reads its library from BillBootstrapConfig at Initialize; inject it
            // directly so the test needs no bootstrap, no Resources asset and no scene.
            _audio.Initialize();
            typeof(AudioService).GetField("_lib", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_audio, _lib);
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 4, perKeyLimit = 2, perKeyRetrigger = 0f });
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (_host != null) Object.DestroyImmediate(_host);
            if (_lib != null) Object.DestroyImmediate(_lib);
        }

        // ---- Music transitions -------------------------------------------------------------

        [Test]
        public void SameKeyMusicRequest_DoesNotRestartTrack()
        {
            _audio.PlayMusic("music.a", 0f);
            int generation = _audio.MusicGeneration;

            _audio.PlayMusic("music.a", 0.2f);

            Assert.AreEqual(generation, _audio.MusicGeneration,
                "a same-key request must not open a transition, which is what restarts the track");
            Assert.AreEqual("music.a", _audio.CurrentMusicKey);
        }

        [Test]
        public void DifferentKey_OpensANewTransition()
        {
            _audio.PlayMusic("music.a", 0f);
            int generation = _audio.MusicGeneration;

            _audio.PlayMusic("music.b", 0f);

            Assert.Greater(_audio.MusicGeneration, generation);
            Assert.AreEqual("music.b", _audio.CurrentMusicKey);
        }

        [Test]
        public void NewTransition_InvalidatesTheGenerationOlderFadesCaptured()
        {
            // Every fade captures the generation live at its start and aborts once it no longer
            // matches. Proving the generation moves is proving the stale fade is disarmed - the
            // regression that silenced music project-wide was a fade whose callback still ran.
            _audio.PlayMusic("music.a", 1f);
            int captured = _audio.MusicGeneration;

            _audio.PlayMusic("music.b", 1f);
            Assert.AreNotEqual(captured, _audio.MusicGeneration);

            _audio.StopMusic(0.5f);
            Assert.AreNotEqual(captured, _audio.MusicGeneration,
                "StopMusic must also supersede in-flight fades");
        }

        [Test]
        public void StopMusic_ClearsTheCurrentKey_SoTheSameTrackCanBeRestarted()
        {
            _audio.PlayMusic("music.a", 0f);
            _audio.StopMusic(0f);
            Assert.AreEqual("", _audio.CurrentMusicKey);

            int generation = _audio.MusicGeneration;
            _audio.PlayMusic("music.a", 0f);
            Assert.Greater(_audio.MusicGeneration, generation,
                "after a stop the same key is a real transition again, not a no-op");
        }

        [Test]
        public void SelectedVariantDuration_IsReportedForFollowUpTiming()
        {
            // The result bed is scheduled off this number. A constant here is what dropped the bed
            // on top of a 9-13 second stinger.
            float length = _audio.PlayCue("stinger.long", SfxPriority.Critical, 1f);

            Assert.Greater(length, 1.5f, "must report the chosen variant's real length");
            Assert.Less(length, 2.5f);
        }

        [Test]
        public void MissingCue_ReportsZeroLength_SoCallersDoNotWaitOnNothing()
        {
            Assert.AreEqual(0f, _audio.PlayCue("does.not.exist", SfxPriority.High, 1f));
        }

        // ---- Voice priority ----------------------------------------------------------------

        [Test]
        public void LowPriority_CannotStealFromCriticalWhenPoolIsFull()
        {
            for (int i = 0; i < 4; i++) _audio.PlayCue("sfx.crit", SfxPriority.Critical, 1f);

            Assert.AreEqual(0f, _audio.PlayCue("sfx.low", SfxPriority.Low, 1f),
                "a Low cue must be dropped rather than cut a Critical voice");
        }

        [Test]
        public void HighPriority_CannotStealFromCritical()
        {
            for (int i = 0; i < 4; i++) _audio.PlayCue("sfx.crit", SfxPriority.Critical, 1f);

            Assert.AreEqual(0f, _audio.PlayCue("sfx.low", SfxPriority.High, 1f),
                "only Critical may take a Critical voice");
        }

        [Test]
        public void Critical_CanStealFromLowWhenPoolIsFull()
        {
            for (int i = 0; i < 4; i++) _audio.PlayCue("sfx.low", SfxPriority.Low, 1f);

            Assert.Greater(_audio.PlayCue("sfx.crit", SfxPriority.Critical, 1f), 0f,
                "Critical must always find a voice");
        }

        [Test]
        public void MediumCanStealFromLow()
        {
            // perKeyLimit lifted so the pool really is full of Low voices and nothing else is
            // gating the request under test.
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 3, perKeyLimit = 99, perKeyRetrigger = 0f });
            for (int i = 0; i < 3; i++) _audio.PlayCue("sfx.low", SfxPriority.Low, 1f);

            Assert.Greater(_audio.PlayCue("sfx.crit", SfxPriority.Medium, 1f), 0f,
                "a Medium cue must be able to take a Low voice");
        }

        [Test]
        public void EqualPriority_DoesNotSteal()
        {
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 3, perKeyLimit = 99, perKeyRetrigger = 0f });
            for (int i = 0; i < 3; i++) _audio.PlayCue("sfx.spam", SfxPriority.Medium, 1f);

            Assert.AreEqual(0f, _audio.PlayCue("sfx.low", SfxPriority.Medium, 1f),
                "equal priority must not steal - that is what causes voice thrash");
        }

        [Test]
        public void PerKeyConcurrencyCap_IsEnforced()
        {
            // perKeyLimit = 2 in SetUp.
            Assert.Greater(_audio.PlayCue("sfx.spam", SfxPriority.Medium, 1f), 0f);
            Assert.Greater(_audio.PlayCue("sfx.spam", SfxPriority.Medium, 1f), 0f);

            Assert.AreEqual(0f, _audio.PlayCue("sfx.spam", SfxPriority.Medium, 1f),
                "a third simultaneous copy of one key is what turns a horde into a noise wall");
        }

        [Test]
        public void CriticalIgnoresPerKeyCapAndRetrigger()
        {
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 8, perKeyLimit = 1, perKeyRetrigger = 10f });

            _audio.PlayCue("sfx.crit", SfxPriority.Critical, 1f);
            Assert.Greater(_audio.PlayCue("sfx.crit", SfxPriority.Critical, 1f), 0f,
                "neither the cap nor the retrigger window may gate a Critical cue");
        }

        [Test]
        public void RetriggerWindow_SuppressesRapidRepeatsOfTheSameKey()
        {
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 8, perKeyLimit = 8, perKeyRetrigger = 30f });

            Assert.Greater(_audio.PlayCue("sfx.low", SfxPriority.Low, 1f), 0f);
            Assert.AreEqual(0f, _audio.PlayCue("sfx.low", SfxPriority.Low, 1f),
                "a repeat inside the retrigger window must be dropped");
        }

        [Test]
        public void PoolGrowsOnlyToTheConfiguredCeiling()
        {
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 3, perKeyLimit = 99, perKeyRetrigger = 0f });
            for (int i = 0; i < 10; i++) _audio.PlayCue("sfx.low", SfxPriority.Low, 1f);

            var voices = typeof(AudioService)
                .GetField("_voices", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_audio) as System.Collections.ICollection;

            Assert.AreEqual(3, voices.Count, "the pool must never allocate past its ceiling");
        }

        // ---- Managed cue handles -----------------------------------------------------------

        [Test]
        public void ManagedCueHandle_StopsItsOwnVoice()
        {
            var h = _audio.PlayManagedCue("stinger.long", SfxPriority.Critical, CueGroup.Critical, 1f);
            Assert.IsTrue(h.IsAssigned);
            Assert.IsTrue(_audio.IsCueAlive(h));

            _audio.StopCue(h, 0f);
            Assert.IsFalse(_audio.IsCueAlive(h), "stopping a live handle must free its voice");
        }

        [Test]
        public void StaleHandle_CannotStopTheCueThatReusedItsVoice()
        {
            // This is the whole reason the handle carries a version: a Retry firing StopCue on an
            // old stinger must not silence whatever took that slot afterwards.
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 1, perKeyLimit = 9, perKeyRetrigger = 0f });

            var stale = _audio.PlayManagedCue("stinger.long", SfxPriority.Critical, CueGroup.Critical, 1f);
            _audio.StopCue(stale, 0f);                      // frees the single voice
            var fresh = _audio.PlayManagedCue("sfx.crit", SfxPriority.Critical, CueGroup.Critical, 1f);

            _audio.StopCue(stale, 0f);                      // replayed stale stop
            Assert.IsTrue(_audio.IsCueAlive(fresh), "a stale handle must never stop the new occupant");
        }

        [Test]
        public void HandleFromRefusedCue_IsNotAssigned()
        {
            var h = _audio.PlayManagedCue("does.not.exist", SfxPriority.Critical, CueGroup.Critical, 1f);
            Assert.IsFalse(h.IsAssigned);
            Assert.IsFalse(_audio.IsCueAlive(h));
            Assert.DoesNotThrow(() => _audio.StopCue(h, 0.2f), "stopping a never-played cue is a no-op");
        }

        // ---- Group ducking affects live voices ---------------------------------------------

        [Test]
        public void CombatDuck_AttenuatesVoicesAlreadyPlaying()
        {
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 8, perKeyLimit = 9, perKeyRetrigger = 0f });
            _audio.PlayManagedCue("sfx.low", SfxPriority.Medium, CueGroup.Combat, 1f);
            float before = FirstBusySourceVolume();

            _audio.DuckGroup(CueGroup.Combat, 0.25f, 0f);   // instant ramp

            float after = FirstBusySourceVolume();
            Assert.Less(after, before * 0.5f,
                "a duck must move sounds that are ALREADY playing, not just future ones");
        }

        [Test]
        public void CriticalGroup_IgnoresCombatDuck_ButObeysUserVolume()
        {
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 8, perKeyLimit = 9, perKeyRetrigger = 0f });
            var crit = _audio.PlayManagedCue("sfx.crit", SfxPriority.Critical, CueGroup.Critical, 1f);
            float full = VolumeOf(crit);

            _audio.DuckGroup(CueGroup.Combat, 0.1f, 0f);
            Assert.AreEqual(full, VolumeOf(crit), 0.001f, "combat duck must not touch the Critical bus");

            _audio.SetVolume(AudioChannel.SFX, 0.5f);
            Assert.Less(VolumeOf(crit), full * 0.75f, "user SFX volume must still apply to Critical");
        }

        private float FirstBusySourceVolume()
        {
            var voices = typeof(AudioService).GetField("_voices", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_audio) as System.Collections.IList;
            foreach (var v in voices)
            {
                var vt = v.GetType();
                if (!(bool)vt.GetProperty("IsBusy").GetValue(v)) continue;
                return ((AudioSource)vt.GetField("source").GetValue(v)).volume;
            }
            return -1f;
        }

        private float VolumeOf(AudioCueHandle h)
        {
            var voices = typeof(AudioService).GetField("_voices", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_audio) as System.Collections.IList;
            var v = voices[h.VoiceIndex];
            return ((AudioSource)v.GetType().GetField("source").GetValue(v)).volume;
        }

        // ---- Deterministic two-source music ------------------------------------------------

        [Test]
        public void StopMusicInstant_StopsBothSources()
        {
            _audio.PlayMusic("music.a", 0f);
            _audio.PlayMusic("music.b", 0.5f);   // leaves a crossfade in flight
            _audio.StopMusic(0f);

            var a = (AudioSource)typeof(AudioService).GetField("_musicA", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_audio);
            var b = (AudioSource)typeof(AudioService).GetField("_musicB", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_audio);
            Assert.IsFalse(a.isPlaying, "MusicA must be stopped");
            Assert.IsFalse(b.isPlaying, "MusicB must be stopped - an interrupted crossfade leaves both live");
            Assert.AreEqual("", _audio.CurrentMusicKey);
        }

        [Test]
        public void VoiceReset_ClearsLoopSpatialAndClipState()
        {
            _audio.ConfigureVoices(new VoicePolicy { maxVoices = 1, perKeyLimit = 9, perKeyRetrigger = 0f });
            _audio.PlayCue("sfx.low", new Vector3(5f, 0f, 5f), SfxPriority.Low, 1f);   // spatial

            var voices = typeof(AudioService).GetField("_voices", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_audio) as System.Collections.IList;
            var src = (AudioSource)voices[0].GetType().GetField("source").GetValue(voices[0]);
            Assert.AreEqual(1f, src.spatialBlend, 0.001f);

            _audio.PlayCue("sfx.crit", SfxPriority.Critical, 1f);   // non-spatial steals the voice
            Assert.AreEqual(0f, src.spatialBlend, 0.001f, "reuse must reset spatial blend");
            Assert.IsFalse(src.loop, "reuse must reset loop");
            Assert.AreEqual("Crit", src.clip.name, "reuse must reassign the clip");
        }

    }
}