using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Audio
{
    /// <summary>
    /// How loud each radio voice line is over time, so the radio card's waveform moves with the voice
    /// (owner 04/10). WebGL cannot read audio samples at run time, so the envelopes are measured once
    /// from the WAV files by Tools/vo_envelopes.py into Resources/VO/vo_envelopes.json (30 values a
    /// second, 0..1 relative to the line's own loud parts).
    /// </summary>
    public static class VoiceEnvelopes
    {
        public const string ResourcePath = "VO/vo_envelopes";

        [Serializable] class Line { public string id; public string env; }
        [Serializable] class File { public int fps = 30; public Line[] lines = Array.Empty<Line>(); }

        static Dictionary<string, byte[]> _env;
        static float _fps = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _env = null;

        static void Load()
        {
            _env = new Dictionary<string, byte[]>();
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) return;
            var file = JsonUtility.FromJson<File>(asset.text);
            if (file?.lines == null) return;
            _fps = Mathf.Max(1, file.fps);
            foreach (var l in file.lines)
                if (!string.IsNullOrEmpty(l.id) && !string.IsNullOrEmpty(l.env)) _env[l.id] = Convert.FromBase64String(l.env);
            Resources.UnloadAsset(asset);
        }

        public static bool Has(string id)
        {
            if (_env == null) Load();
            return id != null && _env.ContainsKey(id);
        }

        /// <summary>Loudness 0..1 of <paramref name="id"/> at <paramref name="seconds"/> into the line;
        /// 0 for a line without an envelope or past its end.</summary>
        public static float At(string id, float seconds)
        {
            if (_env == null) Load();
            if (id == null || !_env.TryGetValue(id, out var env) || env.Length == 0 || seconds < 0f) return 0f;
            int i = (int)(seconds * _fps);
            return i < env.Length ? env[i] / 255f : 0f;
        }
    }
}
