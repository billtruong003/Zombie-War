using System;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// The one notion of "now" and "today" for every daily system (welcome check-in, stamp card,
    /// free pull, shop deals, missions, pass seasons). Before this, half of them used the device's
    /// local midnight and the other half UTC midnight, so the game had two different "days", and
    /// turning the device clock back and forth replayed every daily reward.
    ///
    /// Rules:
    ///  - Time is UTC; a game day starts at <see cref="ResetHourUtc"/> (16:00 UTC = midnight in
    ///    PH/SG/WITA, 23:00 in VN/Jakarta - one moment for the launch markets).
    ///  - Time never runs backwards: the latest time ever seen is kept in the profile and the clock
    ///    returns at least that. Setting the device clock back cannot reopen a day, wipe a stamp
    ///    cycle or end a season early.
    ///  - Once the server has answered in this session (its Date header, 06/10), "now" is the server's
    ///    time plus the real time elapsed since, so moving the device clock forward does not open
    ///    tomorrow's rewards either. Offline the device clock is used, still never backwards.
    /// Callers that need testable rules still take a day number; only the UI and services read
    /// <see cref="Today"/>.
    /// </summary>
    public static class GameClock
    {
        public const int ResetHourUtc = 16;

        static readonly DateTime Epoch = new DateTime(2000, 1, 1, ResetHourUtc, 0, 0, DateTimeKind.Utc);
        static readonly TimeSpan PersistStep = TimeSpan.FromMinutes(1);

        /// <summary>Tests and QA can pin the clock; null = the device clock.</summary>
        internal static Func<DateTime> SourceOverride;

        static DateTime? _serverAnchor;
        static double _anchorRealtime;

        /// <summary>The server's time as of now (from a response); later reads add real elapsed time.</summary>
        public static void SetServerTime(DateTime utc)
        {
            _serverAnchor = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            _anchorRealtime = Time.realtimeSinceStartupAsDouble;
        }

        static DateTime SourceNow =>
            SourceOverride != null ? SourceOverride()
            : _serverAnchor is DateTime a ? a.AddSeconds(Time.realtimeSinceStartupAsDouble - _anchorRealtime)
            : DateTime.UtcNow;

        /// <summary>UTC now, never earlier than the latest time this profile has seen.</summary>
        public static DateTime UtcNow
        {
            get
            {
                var now = SourceNow;
                now = DateTime.SpecifyKind(now, DateTimeKind.Utc);
                var seen = PlayerProfile.LastSeenUtc;
                if (now < seen) return seen;
                if (now - seen >= PersistStep) PlayerProfile.LastSeenUtc = now;
                return now;
            }
        }

        /// <summary>Game-day number (days since 2000-01-01 at the reset hour).</summary>
        public static int Today => DayIndex(UtcNow);

        public static int DayIndex(DateTime utc) =>
            (int)Math.Floor((DateTime.SpecifyKind(utc, DateTimeKind.Utc) - Epoch).TotalDays);

        /// <summary>When the current game day ends.</summary>
        public static DateTime NextResetUtc(DateTime utc) => Epoch.AddDays(DayIndex(utc) + 1);

        public static TimeSpan UntilNextReset => NextResetUtc(UtcNow) - UtcNow;

        /// <summary>"16:16:37"-style countdown to the next reset.</summary>
        public static string UntilNextResetText()
        {
            var t = UntilNextReset;
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { SourceOverride = null; _serverAnchor = null; }
    }
}
