using System;
using System.Collections;
using System.Collections.Generic;
using BillGameCore;
using UnityEngine;

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
    /// Plays the agents' radio voice-over (2026-10-03). Clips live in Resources/VO, named by line id
    /// (vo_riley_ftue_home …); they are mastered to the same loudness (-18 LUFS, peak -1 dBFS) so no
    /// line jumps out, and carry a baked radio layer (120 Hz–5.5 kHz band, light saturation, squelch)
    /// because WebGL has no runtime audio filters. Clean masters and the scripts that make both live in
    /// Review/Lore/voices/ftue_v4 (master.py, radio_fx.py). One line at a time, queued in order; music and SFX are ducked while an agent
    /// talks. Clips load on demand and are released once played, so the VO costs no memory at rest.
    /// <see cref="SayOnce"/> remembers a line in the profile (as FTUE step "vo.&lt;id&gt;"), so the
    /// FTUE QA reset (zw.ftue.reset) plays the lines again.
    /// </summary>
    public sealed class RadioVoice : MonoBehaviour
    {
        const string Folder = "VO/";
        const int MaxQueued = 3;
        const float Gap = 0.25f, MusicDuck = 0.35f, SfxDuck = 0.6f;

        static RadioVoice _instance;

        AudioSource _source;
        readonly List<string> _queue = new();
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
        public static bool Said(string id) => PlayerProfile.HasFtueStep("vo." + id);

        /// <summary>Queue a line. A line already playing or queued is not queued twice.</summary>
        public static void Say(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var v = Instance;
            if (v._current == id || v._queue.Contains(id)) return;
            if (v._queue.Count >= MaxQueued) v._queue.RemoveAt(0);   // stale lines go first
            v._queue.Add(id);
        }

        /// <summary>Queue a line the first time ever; false if it was already used.</summary>
        public static bool SayOnce(string id)
        {
            if (Said(id)) return false;
            PlayerProfile.MarkFtueStep("vo." + id);
            Say(id);
            return true;
        }

        /// <summary>After <paramref name="seconds"/> of real time, say the line once if
        /// <paramref name="stillNeeded"/> still holds (a nudge for a player who has not acted).</summary>
        public static void Nudge(string id, float seconds, Func<bool> stillNeeded)
        {
            if (Said(id)) return;
            var v = Instance;
            v.StartCoroutine(v.CoNudge(id, seconds, stillNeeded));
        }

        IEnumerator CoNudge(string id, float seconds, Func<bool> stillNeeded)
        {
            yield return CoAfter(seconds, stillNeeded, () => SayOnce(id));
        }

        /// <summary>After <paramref name="seconds"/> of real time and a quiet radio, run
        /// <paramref name="act"/> if <paramref name="stillNeeded"/> still holds.</summary>
        public static void After(float seconds, Func<bool> stillNeeded, Action act)
        {
            var v = Instance;
            v.StartCoroutine(v.CoAfter(seconds, stillNeeded, act));
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
            var id = _queue[0];
            _queue.RemoveAt(0);
            StartCoroutine(CoPlay(id));
        }

        IEnumerator CoPlay(string id)
        {
            _current = id;
            _loading = true;
            var request = Resources.LoadAsync<AudioClip>(Folder + id);
            yield return request;
            _loading = false;
            var clip = request.asset as AudioClip;
            if (clip == null)
            {
                Debug.LogWarning($"[RadioVoice] Missing clip Resources/{Folder}{id}");
                _current = null;
                yield break;
            }
            Duck(true);
            _source.clip = clip;
            _source.volume = Volume();
            _source.Play();
            if (Bill.IsReady) Bill.Events.Fire(new RadioLineEvent(id, AgentOf(id), clip.length));
        }

        void Finish()
        {
            var clip = _source.clip;
            _source.clip = null;
            if (clip != null) Resources.UnloadAsset(clip);
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

        void OnDestroy()
        {
            Duck(false);
            if (_instance == this) _instance = null;
        }
    }
}
