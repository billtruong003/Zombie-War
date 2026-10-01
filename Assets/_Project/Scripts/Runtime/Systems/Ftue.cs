using System.Collections.Generic;
using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>A first-time step was completed (the FTUE funnel: one event per step, once).</summary>
    public struct FtueStepEvent : IEvent { public string Step; }

    /// <summary>
    /// First-time-user steps, kept in the profile (2026-10-01). They used to live in separate
    /// PlayerPrefs keys while "first run" lived in the profile, so the two could disagree; a step
    /// found under its old key is moved into the profile the first time it is asked about.
    /// Completing a step fires <see cref="FtueStepEvent"/> once, for analytics to count.
    /// </summary>
    public static class Ftue
    {
        public const string Move = "move";       // the in-run "drag to move" overlay was done
        public const string Reveal = "reveal";   // the after-first-run Arsenal + Shop reveal was seen

        static readonly Dictionary<string, string> LegacyKeys = new()
        {
            [Move] = "ftue_done",
            [Reveal] = "ftue_reveal",
        };

        public static IEnumerable<string> LegacyPrefKeys => LegacyKeys.Values;

        public static bool Done(string step)
        {
            if (PlayerProfile.HasFtueStep(step)) return true;
            if (LegacyKeys.TryGetValue(step, out var key) && PlayerPrefs.GetInt(key, 0) == 1)
            {
                PlayerProfile.MarkFtueStep(step);
                return true;
            }
            return false;
        }

        public static void Complete(string step)
        {
            if (Done(step)) return;
            PlayerProfile.MarkFtueStep(step);
            if (Bill.IsReady) Bill.Events.Fire(new FtueStepEvent { Step = step });
        }
    }
}
