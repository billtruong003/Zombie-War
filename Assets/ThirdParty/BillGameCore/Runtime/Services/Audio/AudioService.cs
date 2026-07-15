using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace BillGameCore
{
    public class AudioService : MonoBehaviour, IAudioService, IInitializable, IDisposableService
    {
        private AudioLibrary _lib;
        private AudioSource _musicA, _musicB;
        private bool _isA = true;
        private readonly List<AudioSource> _sfx = new(8);
        private readonly Dictionary<AudioChannel, float> _vol = new();
        private const int MaxSFX = 16;

        public void Initialize()
        {
            var cfg = BillBootstrapConfig.Instance;
            _lib = cfg?.defaultAudioLibrary;
            _vol[AudioChannel.Master] = cfg?.masterVolume ?? 1f;
            _vol[AudioChannel.Music] = cfg?.musicVolume ?? 0.8f;
            _vol[AudioChannel.SFX] = cfg?.sfxVolume ?? 1f;
            _vol[AudioChannel.UI] = 1f;
            _vol[AudioChannel.Voice] = 1f;

            _musicA = MakeSource("MusicA"); _musicA.loop = true; _musicA.priority = 0;
            _musicB = MakeSource("MusicB"); _musicB.loop = true; _musicB.priority = 0; _musicB.volume = 0;
        }

        AudioSource MakeSource(string n) { var g = new GameObject(n); g.transform.SetParent(transform); return g.AddComponent<AudioSource>(); }
        AudioSource Active => _isA ? _musicA : _musicB;
        AudioSource Inactive => _isA ? _musicB : _musicA;
        float ChVol(AudioChannel ch) => (_vol.TryGetValue(AudioChannel.Master, out var m) ? m : 1f) * (_vol.TryGetValue(ch, out var c) ? c : 1f);

        public void Play(string key) => PlaySFX(key, Vector3.zero, 1f, false, 1f);
        public void Play(string key, Vector3 pos) => PlaySFX(key, pos, 1f, true, 1f);
        public void Play(string key, float vol) => PlaySFX(key, Vector3.zero, vol, false, 1f);
        public void Play(string key, Vector3 pos, float vol) => PlaySFX(key, pos, vol, true, 1f);
        public void PlayPitched(string key, float pitchMultiplier, float vol = 1f) => PlaySFX(key, Vector3.zero, vol, false, pitchMultiplier);

        void PlaySFX(string key, Vector3 pos, float volMul, bool spatial, float pitchMul)
        {
            var e = Resolve(key); if (e == null) return;
            var s = GetSFXSource(); if (s == null) return;
            s.clip = e.clip;
            s.volume = e.volume * volMul * ChVol(AudioChannel.SFX);
            s.pitch = (e.pitch + Random.Range(-e.pitchVariation, e.pitchVariation)) * pitchMul;
            s.loop = e.loop;
            s.spatialBlend = spatial ? 1f : 0f;
            if (spatial) s.transform.position = pos;
            if (e.loop) s.Play(); else s.PlayOneShot(e.clip);
        }

        public void PlayMusic(string key) => PlayMusicImpl(key, 0f);
        public void PlayMusic(string key, float fade) => PlayMusicImpl(key, fade);

        void PlayMusicImpl(string key, float fade)
        {
            var e = Resolve(key); if (e == null) return;
            var inc = Inactive;
            inc.clip = e.clip; inc.loop = true;
            float target = e.volume * ChVol(AudioChannel.Music);
            inc.volume = fade > 0 ? 0 : target;
            inc.Play();
            if (fade > 0)
            {
                StartCoroutine(Fade(Active, Active.volume, 0, fade, () => Active.Stop()));
                StartCoroutine(Fade(inc, 0, target, fade));
            }
            else Active.Stop();
            _isA = !_isA;
        }

        public void StopMusic(float fade = 0f)
        {
            if (fade <= 0) Active.Stop();
            else StartCoroutine(Fade(Active, Active.volume, 0, fade, () => Active.Stop()));
        }

        public void SetVolume(AudioChannel ch, float v) { _vol[ch] = Mathf.Clamp01(v); if (ch == AudioChannel.Music || ch == AudioChannel.Master) Active.volume = ChVol(AudioChannel.Music); }
        public float GetVolume(AudioChannel ch) => ChVol(ch);
        public void Mute(AudioChannel ch) => SetVolume(ch, 0);
        public void Unmute(AudioChannel ch) => SetVolume(ch, 1);

        AudioLibrary.Entry Resolve(string key)
        {
            if (_lib == null) { Debug.LogWarning($"[Bill.Audio] No AudioLibrary set."); return null; }
            var e = _lib.Get(key);
            if (e == null) Debug.LogWarning($"[Bill.Audio] Key '{key}' not found.");
            return e;
        }

        AudioSource GetSFXSource()
        {
            foreach (var s in _sfx) if (s != null && !s.isPlaying) return s;
            if (_sfx.Count < MaxSFX) { var s = MakeSource($"SFX_{_sfx.Count}"); _sfx.Add(s); return s; }
            return _sfx[0];
        }

        IEnumerator Fade(AudioSource src, float from, float to, float dur, System.Action done = null)
        {
            float t = 0;
            while (t < dur) { t += Time.unscaledDeltaTime; src.volume = Mathf.Lerp(from, to, t / dur); yield return null; }
            src.volume = to; done?.Invoke();
        }

        public void Cleanup() => _sfx.Clear();
    }
}
