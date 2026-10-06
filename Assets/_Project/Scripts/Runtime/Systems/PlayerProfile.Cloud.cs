using System;
using UnityEngine;

namespace ZombieWar
{
    public static partial class PlayerProfile
    {
        /// <summary>The whole profile as JSON for the cloud copy; null when this build must not
        /// write it (a save from a newer build).</summary>
        public static string ExportJson() => _newerSchema ? null : Snapshot();

        /// <summary>How far a profile got (runs played, then account XP), to tell which of two
        /// copies to keep. Null when the JSON is not a profile this build can read.</summary>
        public static (int runs, int xp)? ProgressOf(string json)
        {
            try
            {
                var d = JsonUtility.FromJson<ProfileData>(json);
                return d == null || d.version > SchemaVersion ? null : (d.runsPlayed, d.accountXp);
            }
            catch (Exception) { return null; }
        }

        public static (int runs, int xp) Progress => HasProfile ? (Data.runsPlayed, Data.accountXp) : (0, 0);

        /// <summary>Replaces the profile with a cloud copy. A copy that does not parse or comes from a
        /// newer build is refused and the local profile stays.</summary>
        public static bool ImportJson(string json)
        {
            ProfileData loaded;
            try { loaded = JsonUtility.FromJson<ProfileData>(json); }
            catch (Exception e) { Debug.LogWarning("[PlayerProfile] Cloud copy unreadable: " + e.Message); return false; }
            if (loaded == null || loaded.version > SchemaVersion) return false;
            _newerSchema = false;
            _data = Normalize(loaded);
            InvalidateLookups();
            SaveNow();
            Notify(Change.Wallet | Change.Loadout | Change.Costume | Change.Account | Change.Missions);
            return true;
        }
    }
}
