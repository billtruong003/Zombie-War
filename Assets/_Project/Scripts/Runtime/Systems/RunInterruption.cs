using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// A run's coin survives Android closing the game in the background (07/10). Walking away from
    /// a run banks its coin (<see cref="RunClosure"/>), but a backgrounded game the system kills
    /// never reaches a closure and paid nothing - more likely the less memory the phone has.
    /// When the game goes to the background mid-run the coin carried is noted; coming back or
    /// closing the run clears the note; a note found at the next launch is banked.
    /// </summary>
    public static class RunInterruption
    {
        const string Key = "hc.run.interrupted";

        /// <summary>The app went to (or came back from) the background.</summary>
        public static void NotePause(bool paused, RunState run)
        {
            if (!paused || run == null || run.IsOver || run.HasPaidOut) { Clear(); return; }
            if (run.Coin <= 0) return;
            PlayerPrefs.SetString(Key, run.Coin.ToString(System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        /// <summary>The run closed normally: whatever it carried was paid there.</summary>
        public static void Clear()
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        /// <summary>At launch: banks the coin of a run the system closed. Returns the amount.</summary>
        public static long Recover()
        {
            if (!PlayerPrefs.HasKey(Key)) return 0;
            long.TryParse(PlayerPrefs.GetString(Key), System.Globalization.NumberStyles.Integer,
                          System.Globalization.CultureInfo.InvariantCulture, out long coin);
            PlayerPrefs.DeleteKey(Key);   // first: a failure below must not pay it twice
            PlayerPrefs.Save();
            if (coin <= 0 || !PlayerProfile.HasProfile) return 0;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, coin);
            PlayerProfile.FlushIfDirty();
            Online.GameAnalytics.Log("run_interrupted_banked", ("coin", coin));
            return coin;
        }
    }
}
