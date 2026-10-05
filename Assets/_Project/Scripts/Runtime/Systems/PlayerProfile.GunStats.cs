using System;

namespace ZombieWar
{
    /// PlayerProfile, part: per-gun run stats (backlog #18, owner 05/10: Daily Ops, mastery and gun
    /// evolution all ask "what did this gun do"). One entry per gun that has been taken into a run.
    public static partial class PlayerProfile
    {
        /// <summary>What a gun has done across every run it was carried in.</summary>
        public static GunStatEntry GunStat(string weaponId)
        {
            if (!string.IsNullOrEmpty(weaponId))
                for (int i = 0; i < Data.gunStats.Count; i++)
                    if (Data.gunStats[i].weaponId == weaponId) return Data.gunStats[i];
            return new GunStatEntry { weaponId = weaponId };
        }

        /// <summary>
        /// Adds one closed run to the gun's stats. The run has one gun (no in-run switching), so
        /// every kill of the run counts for it. A coalesced write: called inside the run closure batch.
        /// </summary>
        public static void RecordGunRun(string weaponId, int kills, float seconds, long score)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            int at = -1;
            for (int i = 0; i < Data.gunStats.Count; i++) if (Data.gunStats[i].weaponId == weaponId) { at = i; break; }
            var e = at >= 0 ? Data.gunStats[at] : new GunStatEntry { weaponId = weaponId };
            e.runs++;
            e.kills += Math.Max(0, kills);
            if (!float.IsNaN(seconds) && seconds > 0f) e.seconds += seconds;
            e.bestScore = Math.Max(e.bestScore, score);
            if (at >= 0) Data.gunStats[at] = e; else Data.gunStats.Add(e);
            MarkDirty();
        }
    }
}
