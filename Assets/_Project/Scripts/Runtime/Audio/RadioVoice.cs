using System;
using System.Collections;
using System.Collections.Generic;
using BillGameCore;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ZombieWar.Audio
{
    /// <summary>A radio line started: who speaks, which clip, how long. For subtitles and the radio card.</summary>
    public readonly struct RadioLineEvent : IEvent
    {
        public readonly string Id;
        public readonly string Agent;
        public readonly float Duration;
        public RadioLineEvent(string id, string agent, float duration) { Id = id; Agent = agent; Duration = duration; }
    }

    /// <summary>
    /// Plays the agents' radio voice-over (2026-10-03). Clips are Addressables "vo/&lt;line id&gt;" (G12.10;
    /// they lived in Resources/VO, inside the WebGL first download), named by line id
    /// (vo_riley_ftue_home …); they are mastered to the same loudness (-18 LUFS, peak -1 dBFS) so no
    /// line jumps out, and carry a baked radio layer (120 Hz–5.5 kHz band, light saturation, squelch)
    /// because WebGL has no runtime audio filters. Clean masters and the scripts that make both live in
    /// Review/Lore/voices/ftue_v4 (master.py, radio_fx.py). One line at a time, queued in order; music and SFX are ducked while an agent
    /// talks. Clips load on demand and are released once played, so the VO costs no memory at rest.
    /// <see cref="SayOnce"/> remembers a line in the profile (PlayerProfile voFlags), so the
    /// FTUE QA reset (zw.ftue.reset) plays the lines again.
    /// </summary>
    public sealed class RadioVoice : MonoBehaviour
    {
        public const string AddressPrefix = "vo/";
        const int MaxQueuedBlocks = 3;

        /// What wins when the queue is full: a first-time FTUE line beats a conversation, which beats
        /// chatter. A conversation is one block, so a full queue drops whole blocks, never its first line
        /// (the old 3-line cap silently lost line 1 of every 4-line conversation).
        public enum Priority { Chatter = 0, Conversation = 1, Ftue = 2 }

        sealed class Block
        {
            public readonly List<string> Lines;
            public readonly Priority Priority;
            public Block(List<string> lines, Priority priority) { Lines = lines; Priority = priority; }
        }
        const float Gap = 0.25f, MusicDuck = 0.35f, SfxDuck = 0.6f;

        static RadioVoice _instance;

        AudioSource _source;
        readonly List<Block> _queue = new();
        string _current;
        bool _loading;
        float _nextAt;
        int _duckMusic = -1, _duckSfx = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        static RadioVoice Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[ZombieWar.RadioVoice]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RadioVoice>();
                return _instance;
            }
        }

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = true;   // the level-up and chest overlays pause the game, not the radio
            _source.priority = 16;
        }

        /// <summary>True while a line plays, loads or waits in the queue.</summary>
        public static bool Busy => _instance != null && (_instance._current != null || _instance._queue.Count > 0);

        /// <summary>Whether <see cref="SayOnce"/> already used this line.</summary>
        public static bool Said(string id) => PlayerProfile.HasVoFlag(id);

        /// <summary>Queue a line. A line already playing or queued is not queued twice.</summary>
        public static void Say(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            Enqueue(new List<string>(1) { id }, id.Contains("_ftue_") ? Priority.Ftue : Priority.Chatter);
        }

        /// <summary>Queue a conversation: its lines play back to back and are kept or dropped together.</summary>
        public static void SayAll(IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0) return;
            Enqueue(new List<string>(ids), Priority.Conversation);
        }

        static void Enqueue(List<string> lines, Priority priority)
        {
            var v = Instance;
            for (int i = lines.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(lines[i]) || v._current == lines[i] || v.IsQueued(lines[i])) lines.RemoveAt(i);
            if (lines.Count == 0) return;

            if (v._queue.Count >= MaxQueuedBlocks)
            {
                // Drop the oldest block of the lowest priority; if every queued block outranks the
                // new one, the new one is the one that goes.
                int drop = -1;
                for (int i = 0; i < v._queue.Count; i++)
                    if (v._queue[i].Priority <= priority && (drop < 0 || v._queue[i].Priority < v._queue[drop].Priority)) drop = i;
                if (drop < 0) return;
                v._queue.RemoveAt(drop);
            }

            // Higher priority plays sooner: insert after every block at the same or higher priority.
            int at = v._queue.Count;
            while (at > 0 && v._queue[at - 1].Priority < priority) at--;
            v._queue.Insert(at, new Block(lines, priority));
        }

        bool IsQueued(string id)
        {
            for (int i = 0; i < _queue.Count; i++) if (_queue[i].Lines.Contains(id)) return true;
            return false;
        }

        /// <summary>Queue a line the first time ever; false if it was already used.</summary>
        public static bool SayOnce(string id)
        {
            if (Said(id)) return false;
            PlayerProfile.MarkVoFlag(id);
            Say(id);
            return true;
        }

        /// <summary>After <paramref name="seconds"/> of real time, say the line once if
        /// <paramref name="stillNeeded"/> still holds (a nudge for a player who has not acted).</summary>
        public static void Nudge(string id, float seconds, Func<bool> stillNeeded)
        {
            if (Said(id)) return;
            var v = Instance;
            v.StartTimer(id, v.CoNudge(id, seconds, stillNeeded));
        }

        // One pending timer per key: asking again (Home shown again) restarts it instead of stacking
        // another 20-120 s coroutine that held the old screen's closure.
        readonly Dictionary<string, Coroutine> _timers = new();

        void StartTimer(string key, IEnumerator routine)
        {
            if (_timers.TryGetValue(key, out var old) && old != null) StopCoroutine(old);
            _timers[key] = StartCoroutine(Timed(key, routine));
        }

        IEnumerator Timed(string key, IEnumerator routine)
        {
            yield return routine;
            _timers.Remove(key);
        }

        IEnumerator CoNudge(string id, float seconds, Func<bool> stillNeeded)
        {
            yield return CoAfter(seconds, stillNeeded, () => SayOnce(id));
        }

        /// <summary>After <paramref name="seconds"/> of real time and a quiet radio, run
        /// <paramref name="act"/> if <paramref name="stillNeeded"/> still holds.</summary>
        /// <param name="key">Timers with the same key replace each other.</param>
        public static void After(string key, float seconds, Func<bool> stillNeeded, Action act)
        {
            var v = Instance;
            v.StartTimer(key, v.CoAfter(seconds, stillNeeded, act));
        }

        IEnumerator CoAfter(float seconds, Func<bool> stillNeeded, Action act)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until || Busy) yield return null;
            bool needed;
            try { needed = stillNeeded == null || stillNeeded(); }
            catch (MissingReferenceException) { needed = false; }   // the screen that asked is gone
            if (needed) act?.Invoke();
        }

        void Update()
        {
            if (_current != null)
            {
                if (_loading || _source.isPlaying) { _source.volume = Volume(); return; }
                Finish();
            }
            if (_queue.Count == 0 || Time.unscaledTime < _nextAt) return;
            var block = _queue[0];
            var id = block.Lines[0];
            block.Lines.RemoveAt(0);
            if (block.Lines.Count == 0) _queue.RemoveAt(0);
            StartCoroutine(CoPlay(id));
        }

        IEnumerator CoPlay(string id)
        {
            _current = id;
            _loading = true;
            var request = Addressables.LoadAssetAsync<AudioClip>(AddressPrefix + id);
            yield return request;
            _loading = false;
            var clip = request.Status == AsyncOperationStatus.Succeeded ? request.Result : null;
            if (clip == null)
            {
                Debug.LogWarning($"[RadioVoice] Missing voice clip {AddressPrefix}{id}");
                if (request.IsValid()) Addressables.Release(request);
                _current = null;
                yield break;
            }
            _clipHandle = request;
            Duck(true);
            _source.clip = clip;
            _source.volume = Volume();
            _source.Play();
            if (Bill.IsReady) Bill.Events.Fire(new RadioLineEvent(id, AgentOf(id), clip.length));
        }

        void Finish()
        {
            _source.clip = null;
            ReleaseClip();
            _current = null;
            _nextAt = Time.unscaledTime + Gap;
            if (_queue.Count == 0) Duck(false);
        }

        static float Volume() => Bill.IsReady && Bill.Audio != null ? Bill.Audio.GetVolume(AudioChannel.Voice) : 1f;

        void Duck(bool on)
        {
            if (!Bill.IsReady || Bill.Audio == null) return;
            if (on && _duckMusic < 0)
            {
                _duckMusic = Bill.Audio.Duck(AudioChannel.Music, MusicDuck, 0.15f);
                _duckSfx = Bill.Audio.Duck(AudioChannel.SFX, SfxDuck, 0.15f);
            }
            else if (!on && _duckMusic >= 0)
            {
                Bill.Audio.Unduck(_duckMusic, 0.4f);
                Bill.Audio.Unduck(_duckSfx, 0.4f);
                _duckMusic = _duckSfx = -1;
            }
        }

        /// <summary>vo_riley_ftue_home → riley; vo_dlg_03_2_mai → mai.</summary>
        public static string AgentOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var parts = id.Split('_');
            if (parts.Length > 1 && parts[1] != "dlg") return parts[1];
            return parts[parts.Length - 1];
        }

        AsyncOperationHandle<AudioClip> _clipHandle;

        // A played line is let go, so the VO costs no memory at rest.
        void ReleaseClip()
        {
            if (_clipHandle.IsValid()) Addressables.Release(_clipHandle);
            _clipHandle = default;
        }

        void OnDestroy()
        {
            ReleaseClip();
            Duck(false);
            if (_instance == this) _instance = null;
        }
    }
}
