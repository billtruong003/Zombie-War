using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    /// <summary>How hard a cue fights for a voice when the pool is full.</summary>
    public enum SfxPriority
    {
        /// <summary>Hurt vocals, footsteps, ambience - first to be dropped.</summary>
        Low = 0,
        /// <summary>Enemy attack/death, impacts, player hurt.</summary>
        Medium = 1,
        /// <summary>Player weapons, explosions, boss and special attacks, wave cues.</summary>
        High = 2,
        /// <summary>Victory, defeat, player death. Always gets a voice.</summary>
        Critical = 3,
    }

    /// <summary>
    /// Which duck bus a cue rides. Deliberately separate from <see cref="SfxPriority"/>: priority
    /// decides who keeps a voice when the pool is full, group decides who gets attenuated when
    /// something else needs the mix. A player weapon is High priority but still ducks under a
    /// victory stinger; the stinger is Critical AND on its own bus so nothing can duck it.
    /// </summary>
    public enum CueGroup
    {
        /// <summary>Result stingers and player death. Never ducked by combat/world.</summary>
        Critical = 0,
        /// <summary>Weapons, creatures, impacts - the gameplay bed.</summary>
        Combat = 1,
        /// <summary>Props, pickups, footsteps, ambience.</summary>
        World = 2,
    }

    /// <summary>
    /// Identity for a cue that may need stopping later. Carries the voice slot AND the version that
    /// slot had when the cue started, so a handle held across a voice reuse is inert rather than
    /// dangerous - stopping a stale handle must never silence whoever owns the voice now.
    /// </summary>
    public readonly struct AudioCueHandle
    {
        public readonly int VoiceIndex;
        public readonly int Version;
        public readonly float Duration;

        public AudioCueHandle(int voiceIndex, int version, float duration)
        {
            VoiceIndex = voiceIndex; Version = version; Duration = duration;
        }

        /// <summary>False for the default handle - i.e. a cue that never actually played.</summary>
        public bool IsAssigned => Version > 0;

        public static AudioCueHandle None => default;
    }

    /// <summary>Voice-pool limits, grouped so the numbers live in one place.</summary>
    [Serializable]
    public struct VoicePolicy
    {
        public int maxVoices;
        /// <summary>Most simultaneous voices any single cue key may hold.</summary>
        public int perKeyLimit;
        /// <summary>Shortest gap between two plays of the same key, in unscaled seconds.</summary>
        public float perKeyRetrigger;

        public static VoicePolicy Default => new VoicePolicy
        {
            maxVoices = 16,
            perKeyLimit = 3,
            perKeyRetrigger = 0.04f,
        };
    }

    public class AudioService : MonoBehaviour, IAudioService, IInitializable, IDisposableService
    {
        private sealed class Voice
        {
            public AudioSource source;
            public SfxPriority priority;
            public CueGroup group;
            public string key;
            public float endsAtUnscaled;
            public bool looping;

            /// <summary>Bumped on every acquisition. A handle from an earlier occupant no longer
            /// matches, which is what makes stale stops harmless.</summary>
            public int version;

            /// <summary>Pre-channel, pre-duck level this cue asked for. Duck gain is applied on top
            /// when writing AudioSource.volume and is never baked in here, so repeated duck ramps
            /// cannot walk the level down.</summary>
            public float baseVolume;

            public bool IsBusy => source != null
                && (Time.unscaledTime < endsAtUnscaled || (looping && source.isPlaying));
        }

        private sealed class DuckRequest
        {
            public bool isGroup;
            public AudioChannel channel;
            public CueGroup group;
            public float amount;
        }

        private AudioLibrary _lib;
        private AudioSource _musicA, _musicB;
        private bool _isA = true;
        private float _baseVolA, _baseVolB;

        private readonly List<Voice> _voices = new(16);
        private readonly Dictionary<AudioChannel, float> _vol = new();
        private readonly Dictionary<string, float> _lastPlayed = new(64);
        private readonly Dictionary<int, DuckRequest> _ducks = new();
        private readonly Dictionary<AudioChannel, float> _channelDuck = new();
        private readonly Dictionary<CueGroup, float> _groupDuck = new();

        // One owned ramp per duck target. A new Duck/Unduck bumps the generation, the previous ramp
        // sees the mismatch and stops writing immediately - without this, two ramps for the same
        // channel interleave their Lerps and the envelope audibly wobbles.
        private readonly Dictionary<AudioChannel, int> _channelRampGen = new();
        private readonly Dictionary<CueGroup, int> _groupRampGen = new();

        private int _nextDuckToken = 1;
        private int _nextVoiceVersion = 1;
        private VoicePolicy _policy = VoicePolicy.Default;

        private int _musicGeneration;
        private string _currentMusicKey = "";

        public string CurrentMusicKey => _currentMusicKey;
        public int MusicGeneration => _musicGeneration;

        public void Initialize()
        {
            var cfg = BillBootstrapConfig.Instance;
            _lib = cfg?.defaultAudioLibrary;
            _vol[AudioChannel.Master] = cfg?.masterVolume ?? 1f;
            _vol[AudioChannel.Music] = cfg?.musicVolume ?? 0.8f;
            _vol[AudioChannel.SFX] = cfg?.sfxVolume ?? 1f;
            _vol[AudioChannel.UI] = 1f;
            _vol[AudioChannel.Voice] = 1f;

            foreach (AudioChannel ch in Enum.GetValues(typeof(AudioChannel))) _channelDuck[ch] = 1f;
            foreach (CueGroup g in Enum.GetValues(typeof(CueGroup))) _groupDuck[g] = 1f;

            _musicA = MakeSource("MusicA"); _musicA.loop = true; _musicA.priority = 0;
            _musicB = MakeSource("MusicB"); _musicB.loop = true; _musicB.priority = 0; _musicB.volume = 0;
        }

        public void ConfigureVoices(VoicePolicy policy)
        {
            _policy = policy;
            if (_policy.maxVoices < 1) _policy.maxVoices = 1;
            if (_policy.perKeyLimit < 1) _policy.perKeyLimit = 1;
        }

        AudioSource MakeSource(string n) { var g = new GameObject(n); g.transform.SetParent(transform); return g.AddComponent<AudioSource>(); }
        AudioSource Active => _isA ? _musicA : _musicB;
        AudioSource Inactive => _isA ? _musicB : _musicA;

        /// <summary>User-facing volume only - master * channel. Duck gain is deliberately NOT folded
        /// in here so it can be re-applied to live voices without compounding.</summary>
        float UserVol(AudioChannel ch) => (_vol.TryGetValue(AudioChannel.Master, out var m) ? m : 1f)
                                        * (_vol.TryGetValue(ch, out var c) ? c : 1f);

        float ChannelDuck(AudioChannel ch) => _channelDuck.TryGetValue(ch, out var d) ? d : 1f;
        float GroupDuck(CueGroup g) => _groupDuck.TryGetValue(g, out var d) ? d : 1f;

        /// <summary>Kept for API compatibility: reports the audible multiplier including duck.</summary>
        public float GetVolume(AudioChannel ch) => UserVol(ch) * ChannelDuck(ch);

        // ---- SFX ---------------------------------------------------------------------------

        public void Play(string key) => PlaySFX(key, Vector3.zero, 1f, false, 1f, SfxPriority.Medium, CueGroup.Combat, out _);
        public void Play(string key, Vector3 pos) => PlaySFX(key, pos, 1f, true, 1f, SfxPriority.Medium, CueGroup.Combat, out _);
        public void Play(string key, float vol) => PlaySFX(key, Vector3.zero, vol, false, 1f, SfxPriority.Medium, CueGroup.Combat, out _);
        public void Play(string key, Vector3 pos, float vol) => PlaySFX(key, pos, vol, true, 1f, SfxPriority.Medium, CueGroup.Combat, out _);
        public void PlayPitched(string key, float pitchMultiplier, float vol = 1f) => PlaySFX(key, Vector3.zero, vol, false, pitchMultiplier, SfxPriority.Medium, CueGroup.Combat, out _);

        public float PlayCue(string key, SfxPriority priority, float volume = 1f)
            => PlaySFX(key, Vector3.zero, volume, false, 1f, priority, GroupFor(priority), out _);

        public float PlayCue(string key, Vector3 position, SfxPriority priority, float volume = 1f)
            => PlaySFX(key, position, volume, true, 1f, priority, GroupFor(priority), out _);

        /// <summary>Plays a cue and returns a handle that can stop it later. Use only for cues that
        /// genuinely need cancelling (result stingers) - an ordinary impact does not need the
        /// bookkeeping, and handing out handles for everything just invites stale ones.</summary>
        public AudioCueHandle PlayManagedCue(string key, SfxPriority priority, CueGroup group, float volume = 1f)
        {
            PlaySFX(key, Vector3.zero, volume, false, 1f, priority, group, out var handle);
            return handle;
        }

        /// <summary>
        /// Plays a cue that MUST be heard, bounded to <paramref name="maxSameKeyVoices"/> voices.
        ///
        /// The ordinary <see cref="PlayCue"/> path treats a voice as busy for the whole imported
        /// clip length, so a 1.25 s handgun clip fired four times a second exhausts
        /// <see cref="VoicePolicy.perKeyLimit"/> and the fourth visible shot is dropped in silence -
        /// exactly the defect a human playtest reported. Raising the global limit would let horde
        /// impacts flood the mix too, so player weapons get their own small budget instead: within
        /// it, voices are acquired normally; once full, the OLDEST same-key voice (the one nearest
        /// its end, whose audible body is already spent) is restarted with the new transient. A
        /// visible shot therefore always makes a sound, and the key can never grow past its bound.
        /// </summary>
        public AudioCueHandle PlayGuaranteedTransient(string key, SfxPriority priority,
                                                      int maxSameKeyVoices, float volume = 1f)
        {
            Diagnostics.Requested++;

            var e = Resolve(key);   // keeps the library's no-immediate-repeat variant rotation
            if (e == null || e.clip == null) return AudioCueHandle.None;

            int bound = Mathf.Max(1, maxSameKeyVoices);
            int index = CountBusy(key) < bound ? AcquireVoice(priority, key) : -1;

            // Budget full, or the global pool refused: recycle this key's oldest voice rather than
            // drop a shot the player can see.
            if (index < 0) index = OldestBusyVoiceForKey(key);
            if (index < 0) { Diagnostics.DroppedGlobal++; return AudioCueHandle.None; }

            var voice = _voices[index];
            var s = voice.source;

            s.Stop();
            s.clip = e.clip;
            s.pitch = (e.pitch + UnityEngine.Random.Range(-e.pitchVariation, e.pitchVariation));
            s.loop = false;
            s.spatialBlend = 0f;
            s.transform.position = Vector3.zero;

            voice.version = _nextVoiceVersion++;
            voice.priority = priority;
            voice.group = GroupFor(priority);
            voice.key = key;
            voice.looping = false;
            voice.baseVolume = e.volume * volume;
            s.volume = voice.baseVolume * UserVol(AudioChannel.SFX) * GroupDuck(voice.group);

            float length = e.clip.length / Mathf.Max(0.01f, Mathf.Abs(s.pitch));
            voice.endsAtUnscaled = Time.unscaledTime + length;
            _lastPlayed[key] = Time.unscaledTime;
            s.Play();

            Diagnostics.Accepted++;
            return new AudioCueHandle(index, voice.version, length);
        }

        /// <summary>Index of the busy voice for this key that is closest to finishing, or -1.</summary>
        int OldestBusyVoiceForKey(string key)
        {
            int oldest = -1;
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (!v.IsBusy || v.key != key) continue;
                if (oldest < 0 || v.endsAtUnscaled < _voices[oldest].endsAtUnscaled) oldest = i;
            }
            return oldest;
        }

        /// <summary>Counters for the weapon-audio gates. Plain int increments - no allocation, and
        /// nothing reads them in a shipping build.</summary>
        public static class Diagnostics
        {
            public static int Requested, Accepted, DroppedRetrigger, DroppedPerKey, DroppedGlobal, Retriggered;

            public static void Reset()
            {
                Requested = Accepted = DroppedRetrigger = DroppedPerKey = DroppedGlobal = Retriggered = 0;
            }

            public static string Summary =>
                $"requested={Requested} accepted={Accepted} dropRetrigger={DroppedRetrigger} " +
                $"dropPerKey={DroppedPerKey} dropGlobal={DroppedGlobal} retriggered={Retriggered}";
        }

        /// <summary>Live voice counts, for tests and diagnostics.</summary>
        public int BusyVoiceCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _voices.Count; i++) if (_voices[i].IsBusy) n++;
                return n;
            }
        }

        public int BusyVoiceCountForKey(string key) => CountBusy(key);

        /// <summary>Clip currently loaded on a specific voice slot. Lets a test read exactly which
        /// variant a returned handle started, instead of guessing from overlapping sources.</summary>
        public string ClipNameOfVoice(int voiceIndex) =>
            voiceIndex >= 0 && voiceIndex < _voices.Count && _voices[voiceIndex].source != null
            && _voices[voiceIndex].source.clip != null
                ? _voices[voiceIndex].source.clip.name
                : "";

        /// <summary>
        /// Restarts a cue on the SAME dedicated voice, at whatever cadence the caller drives.
        ///
        /// Automatic-weapon fire cannot go through the one-shot path: per-shot voices for a fast gun
        /// either exceed <see cref="VoicePolicy.perKeyLimit"/> and get silently dropped (audible as
        /// rhythmic gaps - the M5.1 CP7 defect), or stack into a smear. Restarting one voice gives a
        /// hard transient per shot at the exact fire cadence - the classic automatic-weapon read -
        /// with zero voice growth. The clip's natural tail rings out after the last shot because the
        /// voice simply finishes its final playback.
        /// </summary>
        public AudioCueHandle RetriggerCue(string key, AudioCueHandle handle, SfxPriority priority, float volume = 1f)
        {
            if (IsCueAlive(handle) && _voices[handle.VoiceIndex].key == key)
            {
                var e = Resolve(key);
                if (e == null || e.clip == null) return AudioCueHandle.None;

                var voice = _voices[handle.VoiceIndex];
                var s = voice.source;
                s.Stop();
                s.clip = e.clip;
                s.pitch = e.pitch + UnityEngine.Random.Range(-e.pitchVariation, e.pitchVariation);
                voice.baseVolume = e.volume * volume;
                s.volume = voice.baseVolume * UserVol(AudioChannel.SFX) * GroupDuck(voice.group);
                s.Play();

                voice.endsAtUnscaled = Time.unscaledTime + e.clip.length / Mathf.Max(0.01f, Mathf.Abs(s.pitch));
                _lastPlayed[key] = Time.unscaledTime;
                Diagnostics.Retriggered++;
                return handle;   // same voice, same version - the handle stays valid
            }

            // No live voice yet (first shot, or it was stolen): claim one through the normal path.
            PlaySFX(key, Vector3.zero, volume, false, 1f, priority, GroupFor(priority), out var fresh);
            return fresh;
        }

        /// <summary>Fades a managed cue out and frees its voice. A handle whose voice has since been
        /// reused is ignored - that check is the entire reason handles carry a version.</summary>
        public void StopCue(AudioCueHandle handle, float fadeOut)
        {
            if (!IsCueAlive(handle)) return;
            var voice = _voices[handle.VoiceIndex];

            if (fadeOut <= 0f) { ReleaseVoice(voice); return; }
            StartCoroutine(FadeOutCue(voice, handle.Version, fadeOut));
        }

        /// <summary>True while the handle still identifies the cue that is actually playing.</summary>
        public bool IsCueAlive(AudioCueHandle handle)
        {
            if (!handle.IsAssigned || handle.VoiceIndex < 0 || handle.VoiceIndex >= _voices.Count) return false;
            var v = _voices[handle.VoiceIndex];
            return v.version == handle.Version && v.IsBusy;
        }

        IEnumerator FadeOutCue(Voice voice, int version, float duration)
        {
            float from = voice.source != null ? voice.source.volume : 0f;
            float t = 0f;
            while (t < duration)
            {
                // The voice may be stolen mid-fade; bail rather than write over its new occupant.
                if (voice.version != version || voice.source == null) yield break;
                t += Time.unscaledDeltaTime;
                voice.source.volume = Mathf.Lerp(from, 0f, t / duration);
                yield return null;
            }
            if (voice.version != version) yield break;
            ReleaseVoice(voice);
        }

        void ReleaseVoice(Voice voice)
        {
            if (voice.source != null) { voice.source.Stop(); voice.source.clip = null; }
            voice.endsAtUnscaled = 0f;
            voice.looping = false;
            voice.baseVolume = 0f;
            voice.version = _nextVoiceVersion++;   // invalidates every outstanding handle
        }

        static CueGroup GroupFor(SfxPriority priority) =>
            priority == SfxPriority.Critical ? CueGroup.Critical : CueGroup.Combat;

        float PlaySFX(string key, Vector3 pos, float volMul, bool spatial, float pitchMul,
                      SfxPriority priority, CueGroup group, out AudioCueHandle handle)
        {
            handle = AudioCueHandle.None;
            var e = Resolve(key); if (e == null) return 0f;

            if (priority != SfxPriority.Critical)
            {
                if (_lastPlayed.TryGetValue(key, out float last)
                    && Time.unscaledTime - last < _policy.perKeyRetrigger)
                {
                    Diagnostics.DroppedRetrigger++;
                    return 0f;
                }
                if (CountBusy(key) >= _policy.perKeyLimit)
                {
                    Diagnostics.DroppedPerKey++;
                    return 0f;
                }
            }

            int index = AcquireVoice(priority, key);
            if (index < 0) { Diagnostics.DroppedGlobal++; return 0f; }
            var voice = _voices[index];
            var s = voice.source;

            // Full reset - a stolen source carries the previous cue's clip, loop, spatial and pitch.
            s.Stop();
            s.clip = e.clip;
            s.pitch = (e.pitch + UnityEngine.Random.Range(-e.pitchVariation, e.pitchVariation)) * pitchMul;
            s.loop = e.loop;
            s.spatialBlend = spatial ? 1f : 0f;
            s.transform.position = spatial ? pos : Vector3.zero;

            voice.version = _nextVoiceVersion++;
            voice.priority = priority;
            voice.group = group;
            voice.key = key;
            voice.looping = e.loop;
            voice.baseVolume = e.volume * volMul;
            s.volume = voice.baseVolume * UserVol(AudioChannel.SFX) * GroupDuck(group);

            float length = e.clip != null ? e.clip.length / Mathf.Max(0.01f, Mathf.Abs(s.pitch)) : 0f;
            voice.endsAtUnscaled = e.loop ? float.MaxValue : Time.unscaledTime + length;
            _lastPlayed[key] = Time.unscaledTime;

            if (e.loop) s.Play(); else s.PlayOneShot(e.clip, 1f);

            handle = new AudioCueHandle(index, voice.version, length);
            return length;
        }

        int CountBusy(string key)
        {
            int n = 0;
            for (int i = 0; i < _voices.Count; i++)
                if (_voices[i].IsBusy && _voices[i].key == key) n++;
            return n;
        }

        /// <summary>
        /// Free voice, else a new one, else the best victim. Stealing only ever takes a strictly
        /// lower priority, so nothing can cut a Critical cue; Critical alone may take the weakest
        /// thing playing, which is what makes its path guaranteed. Ties break on closest-to-finishing
        /// because cutting a tail is the least audible edit available.
        /// </summary>
        int AcquireVoice(SfxPriority priority, string key)
        {
            for (int i = 0; i < _voices.Count; i++)
                if (!_voices[i].IsBusy) return i;

            if (_voices.Count < _policy.maxVoices)
            {
                _voices.Add(new Voice { source = MakeSource($"SFX_{_voices.Count}"), version = _nextVoiceVersion++ });
                return _voices.Count - 1;
            }

            int victim = -1;
            for (int i = 0; i < _voices.Count; i++)
            {
                var c = _voices[i];
                bool eligible = c.priority < priority
                    || (priority == SfxPriority.Critical && c.priority <= SfxPriority.Critical);
                if (!eligible) continue;

                if (victim < 0
                    || c.priority < _voices[victim].priority
                    || (c.priority == _voices[victim].priority && c.endsAtUnscaled < _voices[victim].endsAtUnscaled))
                    victim = i;
            }
            return victim;
        }

        /// <summary>Re-applies duck gain to every live voice. Called whenever a group's gain moves,
        /// so a duck affects sounds ALREADY playing - changing only the multiplier used at play time
        /// left the existing combat bed at full level, which is what made the duck inaudible.</summary>
        void RefreshVoiceVolumes()
        {
            float user = UserVol(AudioChannel.SFX);
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (v.source == null || !v.IsBusy) continue;
                v.source.volume = v.baseVolume * user * GroupDuck(v.group);
            }
        }

        // ---- Music -------------------------------------------------------------------------

        public void PlayMusic(string key) => PlayMusicImpl(key, 0f);
        public void PlayMusic(string key, float fade) => PlayMusicImpl(key, fade);

        void PlayMusicImpl(string key, float fade)
        {
            if (key == _currentMusicKey && Active.isPlaying) return;   // never restart mid-phrase

            var e = Resolve(key); if (e == null) return;

            int gen = ++_musicGeneration;   // supersedes every in-flight fade before anything moves
            var inc = Inactive;

            // The incoming source may still be mid-crossfade from an interrupted transition. Reset it
            // to a known state before reassigning, or it inherits a partial volume and a stray clip.
            inc.Stop();
            inc.clip = e.clip;
            inc.loop = true;

            float target = e.volume * UserVol(AudioChannel.Music);
            SetBase(inc, fade > 0 ? 0f : target);

            // Fade out EVERY other source, not just Active - an interrupted crossfade can leave both
            // playing, and only handling Active is how a stray source survives at partial volume.
            FadeOutOthers(inc, fade, gen);

            ApplyMusicVolumes();
            inc.Play();
            if (fade > 0) StartCoroutine(FadeMusic(inc, 0f, target, fade, gen, null));

            _isA = !_isA;
            _currentMusicKey = key;
        }

        public void StopMusic(float fade = 0f)
        {
            int gen = ++_musicGeneration;
            _currentMusicKey = "";
            FadeOutOthers(null, fade, gen);
            ApplyMusicVolumes();
        }

        /// <summary>Brings every music source except <paramref name="keep"/> to silence and stops it,
        /// so a transition always leaves the bus deterministic.</summary>
        void FadeOutOthers(AudioSource keep, float fade, int gen)
        {
            foreach (var src in new[] { _musicA, _musicB })
            {
                if (src == null || src == keep) continue;
                if (!src.isPlaying && GetBase(src) <= 0f) { SetBase(src, 0f); continue; }

                if (fade <= 0f) { SetBase(src, 0f); src.Stop(); continue; }
                StartCoroutine(FadeMusic(src, GetBase(src), 0f, fade, gen, src.Stop));
            }
        }

        void SetBase(AudioSource s, float v) { if (s == _musicA) _baseVolA = v; else _baseVolB = v; }
        float GetBase(AudioSource s) => s == _musicA ? _baseVolA : _baseVolB;

        void ApplyMusicVolumes()
        {
            float duck = ChannelDuck(AudioChannel.Music);
            if (_musicA != null) _musicA.volume = _baseVolA * duck;
            if (_musicB != null) _musicB.volume = _baseVolB * duck;
        }

        // Generation-guarded: a superseded fade returns without writing a volume, invoking its
        // callback, or stopping anything the newer transition now owns.
        IEnumerator FadeMusic(AudioSource src, float from, float to, float dur, int generation, Action done)
        {
            float t = 0f;
            while (t < dur)
            {
                if (generation != _musicGeneration || src == null) yield break;
                t += Time.unscaledDeltaTime;
                SetBase(src, Mathf.Lerp(from, to, t / dur));
                ApplyMusicVolumes();
                yield return null;
            }
            if (generation != _musicGeneration || src == null) yield break;
            SetBase(src, to);
            ApplyMusicVolumes();
            done?.Invoke();
        }

        // ---- Ducking -----------------------------------------------------------------------

        public int Duck(AudioChannel channel, float amount, float attack)
        {
            int token = _nextDuckToken++;
            _ducks[token] = new DuckRequest { isGroup = false, channel = channel, amount = Mathf.Clamp01(amount) };
            StartChannelRamp(channel, attack);
            return token;
        }

        /// <summary>Ducks a cue group. Critical is intentionally duckable by nobody in practice -
        /// callers duck Combat and World, leaving the stinger bus untouched.</summary>
        public int DuckGroup(CueGroup group, float amount, float attack)
        {
            int token = _nextDuckToken++;
            _ducks[token] = new DuckRequest { isGroup = true, group = group, amount = Mathf.Clamp01(amount) };
            StartGroupRamp(group, attack);
            return token;
        }

        public void Unduck(int token, float release)
        {
            if (!_ducks.TryGetValue(token, out var req)) return;
            _ducks.Remove(token);
            if (req.isGroup) StartGroupRamp(req.group, release);
            else StartChannelRamp(req.channel, release);
        }

        float ChannelTarget(AudioChannel ch)
        {
            float target = 1f;
            foreach (var kv in _ducks)
                if (!kv.Value.isGroup && kv.Value.channel == ch && kv.Value.amount < target) target = kv.Value.amount;
            return target;
        }

        float GroupTarget(CueGroup g)
        {
            float target = 1f;
            foreach (var kv in _ducks)
                if (kv.Value.isGroup && kv.Value.group == g && kv.Value.amount < target) target = kv.Value.amount;
            return target;
        }

        void StartChannelRamp(AudioChannel ch, float duration)
        {
            _channelRampGen.TryGetValue(ch, out int g);
            _channelRampGen[ch] = ++g;
            StartCoroutine(RampChannel(ch, duration, g));
        }

        void StartGroupRamp(CueGroup group, float duration)
        {
            _groupRampGen.TryGetValue(group, out int g);
            _groupRampGen[group] = ++g;
            StartCoroutine(RampGroup(group, duration, g));
        }

        // Both ramps start from the CURRENT audible gain and abandon the moment a newer ramp for the
        // same target exists. Starting from "now" rather than a remembered value is what stops the
        // envelope jumping when a duck is released mid-attack.
        IEnumerator RampChannel(AudioChannel ch, float duration, int generation)
        {
            float from = ChannelDuck(ch);
            if (duration <= 0f)
            {
                if (_channelRampGen[ch] == generation) { _channelDuck[ch] = ChannelTarget(ch); ApplyMusicVolumes(); }
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                if (_channelRampGen[ch] != generation) yield break;
                t += Time.unscaledDeltaTime;
                _channelDuck[ch] = Mathf.Lerp(from, ChannelTarget(ch), t / duration);
                ApplyMusicVolumes();
                yield return null;
            }
            if (_channelRampGen[ch] != generation) yield break;
            _channelDuck[ch] = ChannelTarget(ch);
            ApplyMusicVolumes();
        }

        IEnumerator RampGroup(CueGroup group, float duration, int generation)
        {
            float from = GroupDuck(group);
            if (duration <= 0f)
            {
                if (_groupRampGen[group] == generation) { _groupDuck[group] = GroupTarget(group); RefreshVoiceVolumes(); }
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                if (_groupRampGen[group] != generation) yield break;
                t += Time.unscaledDeltaTime;
                _groupDuck[group] = Mathf.Lerp(from, GroupTarget(group), t / duration);
                RefreshVoiceVolumes();
                yield return null;
            }
            if (_groupRampGen[group] != generation) yield break;
            _groupDuck[group] = GroupTarget(group);
            RefreshVoiceVolumes();
        }

        // ---- Misc --------------------------------------------------------------------------

        public void SetVolume(AudioChannel ch, float v)
        {
            _vol[ch] = Mathf.Clamp01(v);
            if (ch == AudioChannel.Music || ch == AudioChannel.Master) ApplyMusicVolumes();
            if (ch == AudioChannel.SFX || ch == AudioChannel.Master) RefreshVoiceVolumes();
        }

        public void Mute(AudioChannel ch) => SetVolume(ch, 0);
        public void Unmute(AudioChannel ch) => SetVolume(ch, 1);

        AudioLibrary.Entry Resolve(string key)
        {
            if (_lib == null) { Debug.LogWarning($"[Bill.Audio] No AudioLibrary set."); return null; }
            var e = _lib.Get(key);
            if (e == null) Debug.LogWarning($"[Bill.Audio] Key '{key}' not found.");
            return e;
        }

        public void Cleanup()
        {
            _voices.Clear();
            _ducks.Clear();
            _lastPlayed.Clear();
            _channelRampGen.Clear();
            _groupRampGen.Clear();
            _currentMusicKey = "";
        }
    }
}
