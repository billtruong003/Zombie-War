using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// M10 Settings screen values, kept in PlayerPrefs (device settings, not profile progress, so
    /// "Delete my data" leaves them alone). <see cref="Apply"/> runs once at boot from
    /// <see cref="BootstrapEntry"/>, after Bill services exist.
    /// </summary>
    public static class GameSettings
    {
        public enum Graphics { Low, Mid, High }

        const string KMusic = "set.music", KSfx = "set.sfx", KGraphics = "set.graphics", KFps = "set.fps", KNotify = "set.notify";
        /// Shared with UIFeedback and the in-run pause panel.
        public const string KHaptics = "haptics";

        public static float Music { get => PlayerPrefs.GetFloat(KMusic, 0.8f); set { PlayerPrefs.SetFloat(KMusic, Mathf.Clamp01(value)); Bill.Audio?.SetVolume(AudioChannel.Music, Music); } }
        public static float Sfx { get => PlayerPrefs.GetFloat(KSfx, 1f); set { PlayerPrefs.SetFloat(KSfx, Mathf.Clamp01(value)); Bill.Audio?.SetVolume(AudioChannel.SFX, Sfx); } }
        public static bool Haptics { get => PlayerPrefs.GetInt(KHaptics, 1) == 1; set => PlayerPrefs.SetInt(KHaptics, value ? 1 : 0); }
        public static bool Notifications { get => PlayerPrefs.GetInt(KNotify, 1) == 1; set => PlayerPrefs.SetInt(KNotify, value ? 1 : 0); }

        public static Graphics Quality
        {
            get => (Graphics)Mathf.Clamp(PlayerPrefs.GetInt(KGraphics, (int)Graphics.Mid), 0, 2);
            set { PlayerPrefs.SetInt(KGraphics, (int)value); ApplyGraphics(); }
        }

        /// <summary>30 or 60.</summary>
        public static int Fps
        {
            get => PlayerPrefs.GetInt(KFps, 60) >= 60 ? 60 : 30;
            set { PlayerPrefs.SetInt(KFps, value >= 60 ? 60 : 30); Application.targetFrameRate = Fps; }
        }

        public static void Apply()
        {
            Bill.Audio?.SetVolume(AudioChannel.Music, Music);
            Bill.Audio?.SetVolume(AudioChannel.SFX, Sfx);
            Application.targetFrameRate = Fps;
            ApplyGraphics();
        }

        /// <summary>Render resolution as a share of the native screen (Low 70%, Mid 85%, High 100%).
        /// Changes the device resolution, never the URP asset, so nothing is written to project files.</summary>
        public static float ResolutionScale(Graphics g) => g switch { Graphics.Low => 0.7f, Graphics.Mid => 0.85f, _ => 1f };

        static int _nativeW, _nativeH;

        static void ApplyGraphics()
        {
            if (Application.isEditor) return;
            if (_nativeW == 0) { _nativeW = Screen.currentResolution.width; _nativeH = Screen.currentResolution.height; }
            if (_nativeW <= 0 || _nativeH <= 0) return;
            float s = ResolutionScale(Quality);
            Screen.SetResolution(Mathf.RoundToInt(_nativeW * s), Mathf.RoundToInt(_nativeH * s), true);
        }
    }
}
