using System;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// The current UI theme (owner 2026-09-27: Sky, Dark mode, Candy and Meadow; Meadow became the
    /// default the same day). Kept in PlayerPrefs like other device settings. <see cref="ThemeTint"/> and
    /// <see cref="MenuBackgroundView"/> listen to <see cref="Changed"/>.
    /// </summary>
    public static class ThemeService
    {
        const string Key = "set.theme";
        public const string DefaultId = "meadow";
        static ThemeSet _set;
        static ThemePalette _current;

        public static event Action Changed;

        public static ThemeSet Set => _set != null ? _set : (_set = Resources.Load<ThemeSet>("UI/ThemeSet"));

        public static ThemePalette Current
        {
            get
            {
                if (_current != null) return _current;
                var set = Set;
                if (set == null) return null;
                _current = set.Find(PlayerPrefs.GetString(Key, DefaultId)) ?? set.Find(DefaultId) ?? (set.themes.Length > 0 ? set.themes[0] : null);
                return _current;
            }
        }

        public static string CurrentId => Current != null ? Current.id : DefaultId;

        public static Color Get(ThemeRole role) => Current != null ? Current.Get(role) : Color.magenta;

        public static void Use(string id)
        {
            var p = Set != null ? Set.Find(id) : null;
            if (p == null || p == _current) return;
            _current = p;
            PlayerPrefs.SetString(Key, id);
            Changed?.Invoke();
        }

        /// <summary>Test seam and editor rebuilds: forget the cached theme.</summary>
        public static void Reset() { _current = null; _set = null; }
    }
}
