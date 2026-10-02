using System;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Graphics tier (2026-10-02): the game targets weak-mid phones up to high-end ones. The tier IS
    /// the Settings screen's graphics option (GameSettings.Quality, Low / Mid / High); this class
    /// only names what each level may spend. One source since 02/10: before, this guessed the device
    /// on its own and could disagree with what the player picked.
    /// </summary>
    public static class GraphicsTier
    {
        public enum Level { Low = 0, Mid = 1, High = 2 }

        /// <summary>Raised when the tier changes (Settings, the QA panel), so systems can re-read their budget.</summary>
        public static event Action<Level> Changed;

        // Read every frame by the point lights and the bloom: cached, refreshed on every change.
        static Level? _current;
        public static Level Current => _current ??= (Level)(int)GameSettings.Quality;

        /// <summary>Sets the player's graphics option (the same as the Settings screen).</summary>
        public static void Set(Level level) => GameSettings.Quality = (GameSettings.Graphics)(int)level;

        /// <summary>Forgets the player's choice and goes back to the device guess.</summary>
        public static void ResetToDetected() => GameSettings.ResetQualityToDevice();

        /// <summary>The device guess as a tier.</summary>
        public static Level Detect() => (Level)(int)GameSettings.DeviceGuess();

        internal static void NotifyChanged()
        {
            _current = null;
            Changed?.Invoke(Current);
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
