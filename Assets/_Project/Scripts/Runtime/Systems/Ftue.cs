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

        // FTUE v2 (owner-approved mockup 03/10, canvas page "FTUE v2").
        public const string XpGlow = "xp";       // the XP bar glowed on the first kills
        public const string Card = "card";       // the first level-up ever: no timer, one card suggested
        public const string Chest = "chest";     // the first chest ever: no timer, how evolutions work
        public const string Revive = "revive";   // the first revive ever was free
        public const string Gift = "gift";       // the first result topped coins up to the cheapest gun
        public const string Gun = "gun";         // the Arsenal pointed at the first gun to buy

        /// <summary>The first time each station kind is met (a callout with its name and use).</summary>
        public static string Station(ZombieWar.Stations.StationKind kind) => "station." + kind;

        /// <summary>The first pickup of each mechanic item (a toast with what it did).</summary>
        public static string Item(PickupEffect effect) => "item." + effect;

        /// <summary>The account-level feature unlock popup (LV2 missions + pass, LV3 gacha, LV5 stars).</summary>
        public static string Unlock(int level) => "unlock." + level;

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

        /// <summary>QA (cheat zw.ftue.reset): every step plays again, legacy keys included.</summary>
        public static void ResetAll()
        {
            PlayerProfile.ClearFtueSteps();
            foreach (var key in LegacyKeys.Values) PlayerPrefs.DeleteKey(key);
        }

        public static void Complete(string step)
        {
            if (Done(step)) return;
            PlayerProfile.MarkFtueStep(step);
            if (Bill.IsReady) Bill.Events.Fire(new FtueStepEvent { Step = step });
        }
    }
}
