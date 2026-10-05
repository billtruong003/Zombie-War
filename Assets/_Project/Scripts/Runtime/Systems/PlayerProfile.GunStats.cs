using System;
using System.Collections.Generic;

namespace ZombieWar
{
    /// PlayerProfile, part: per-gun run stats (backlog #18, owner 05/10: Daily Ops, mastery and gun
    /// evolution all ask "what did this gun do"). One entry per gun that has been taken into a run.
    public static partial class PlayerProfile
    {
        public static IReadOnlyList<GunStatEntry> GunStats => Data.gunStats;

        public static int MasteryLevel(string weaponId) => GunMastery.LevelFor(GunStat(weaponId).masteryXp);

        /// <summary>
        /// Adds mastery XP to a gun and pays every level it crosses (backlog #21). Returns the levels
        /// before and after so the result screen can say "M1911 mastery 6".
        /// </summary>
        public static (int before, int after) AddMasteryXp(string weaponId, int xp)
        {
            if (string.IsNullOrEmpty(weaponId) || xp <= 0) { int l = MasteryLevel(weaponId); return (l, l); }
            int at = -1;
            for (int i = 0; i < Data.gunStats.Count; i++) if (Data.gunStats[i].weaponId == weaponId) { at = i; break; }
            var e = at >= 0 ? Data.gunStats[at] : new GunStatEntry { weaponId = weaponId };
            int before = GunMastery.LevelFor(e.masteryXp);
            e.masteryXp = (int)Math.Min(int.MaxValue, (long)e.masteryXp + xp);
            int after = GunMastery.LevelFor(e.masteryXp);
            if (at >= 0) Data.gunStats[at] = e; else Data.gunStats.Add(e);
            MarkDirty();
            for (int l = before + 1; l <= after; l++)
            {
                var r = GunMastery.RewardFor(l);
                switch (r.kind)
                {
                    case GunMastery.RewardKind.Shards: AddWeaponShardsInMemory(weaponId, r.amount); break;
                    case GunMastery.RewardKind.Coin: Add(CurrencyKind.Coin, r.amount); break;
                    case GunMastery.RewardKind.Gem: Add(CurrencyKind.Gem, r.amount); break;
                    default: Data.tickets += r.amount; break;
                }
            }
            if (after > before) Notify(Change.Loadout);
            return (before, after);
        }

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
