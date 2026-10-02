using System;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Graphics tier (2026-10-02): the game targets weak-mid phones up to high-end ones. The tier is
    /// guessed once from the device (memory, cores, GPU memory) and can be overridden (QA panel now,
    /// the settings screen later); everything that costs GPU time asks it how much it may spend.
    /// </summary>
    public static class GraphicsTier
    {
        public enum Level { Low = 0, Mid = 1, High = 2 }

        const string PrefKey = "zw.gfx.tier";

        static Level? _current;

        /// <summary>Raised when the tier changes (override or reset), so systems can re-read their budget.</summary>
        public static event Action<Level> Changed;

        public static Level Current
        {
            get
            {
                if (_current == null)
                {
                    int saved = PlayerPrefs.GetInt(PrefKey, -1);
                    _current = saved >= 0 && saved <= 2 ? (Level)saved : Detect();
                }
                return _current.Value;
            }
        }

        /// <summary>Overrides the detected tier and remembers it.</summary>
        public static void Set(Level level)
        {
            _current = level;
            PlayerPrefs.SetInt(PrefKey, (int)level);
            PlayerPrefs.Save();
            Changed?.Invoke(level);
        }

        /// <summary>Forgets the override and goes back to the device guess.</summary>
        public static void ResetToDetected()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            _current = Detect();
            Changed?.Invoke(_current.Value);
        }

        /// <summary>The device guess: low under ~3 GB or 4 cores, high from ~6 GB with 8 cores, mid between.</summary>
        public static Level Detect()
        {
            if (Application.isEditor) return Level.High;
            int ram = SystemInfo.systemMemorySize;        // MB
            int cores = SystemInfo.processorCount;
            int vram = SystemInfo.graphicsMemorySize;      // MB, often shared on phones
            if (ram < 3200 || cores <= 4 || (vram > 0 && vram < 768)) return Level.Low;
            if (ram >= 5800 && cores >= 8) return Level.High;
            return Level.Mid;
        }

        // ── budgets ──────────────────────────────────────────────────────────────────────

        /// <summary>Point lights the toon shaders evaluate per pixel.</summary>
        public static int PointLights => Current switch { Level.Low => 4, Level.Mid => 8, _ => 16 };

        /// <summary>Bloom passes: 0 = off, else down/up iterations below quarter resolution.</summary>
        public static int BloomIterations => Current switch { Level.Low => 0, Level.Mid => 2, _ => 3 };

        // Domain reload is off in this project: statics survive Play Mode exit.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForNewSession() { _current = null; Changed = null; }
    }
}
