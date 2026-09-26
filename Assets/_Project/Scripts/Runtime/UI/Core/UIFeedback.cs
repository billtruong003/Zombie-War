using BillGameCore;
using UnityEngine;

namespace ZombieWar.UI
{
    /// M8 P3: one place for UI sounds and haptics, so every screen answers a tap the same way.
    ///
    /// Sounds are the synthesised sfx.ui.* cues (Tools/gen_ui_sfx.py). Haptics are short Android
    /// one-shots and honour the Vibration toggle (PlayerPrefs "haptics", default on); other
    /// platforms get nothing rather than Handheld.Vibrate's long buzz.
    public static class UIFeedback
    {
        public enum Buzz { Tick = 12, Light = 25, Medium = 45, Heavy = 90 }

        public static void Tap() => Play("sfx.ui.tap");
        public static void Confirm() => Play("sfx.ui.confirm");
        public static void Back() => Play("sfx.ui.back");
        public static void Card() => Play("sfx.ui.card");
        public static void Error() { Play("sfx.ui.error"); Haptic(Buzz.Light); }
        public static void Purchase() { Play("sfx.ui.purchase"); Haptic(Buzz.Medium); }
        public static void Equip() { Play("sfx.ui.equip"); Haptic(Buzz.Tick); }
        public static void LevelUp() { Play("sfx.ui.levelup"); Haptic(Buzz.Medium); }

        public static bool HapticsOn => PlayerPrefs.GetInt("haptics", 1) == 1;

        static void Play(string key)
        {
            if (Bill.IsReady) Bill.Audio?.Play(key);
        }

        public static void Haptic(Buzz strength)
        {
            if (!HapticsOn) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_vibrator == null)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using var version = new AndroidJavaClass("android.os.Build$VERSION");
                    _sdk = version.GetStatic<int>("SDK_INT");
                }
                long ms = (long)strength;
                if (_sdk >= 26)
                {
                    using var effect = new AndroidJavaClass("android.os.VibrationEffect");
                    int amplitude = strength >= Buzz.Heavy ? 255 : strength >= Buzz.Medium ? 180 : 110;
                    using var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
                    _vibrator.Call("vibrate", oneShot);
                }
                else _vibrator.Call("vibrate", ms);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[UIFeedback] haptic failed: {e.Message}");
                _vibrator = null;
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static int _sdk;
        // Never called: referencing Handheld.Vibrate makes Unity add the VIBRATE permission.
        static void KeepVibratePermission() => Handheld.Vibrate();
#endif
    }
}
