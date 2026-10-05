using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// PlayerProfile, part: Battle Pass mission progress and claims.
    public static partial class PlayerProfile
    {
        // ===== Battle Pass missions =====

        public static event Action MissionsChanged;
        /// <summary>A mission was claimed (the radio reacts: done, or a pass level up).</summary>
        public static event Action<PassMission> MissionClaimed;

        public static int PassXp => Data.passXp;

        /// <summary>
        /// Expires daily/weekly progress when the UTC day/week has rolled over.
        ///
        /// Each scope is cleared independently: a new day must not wipe weekly progress. Claims are
        /// dropped alongside progress for the expiring scope, so tomorrow's copy of the same mission
        /// is claimable again.
        /// </summary>
        public static void RefreshMissionWindow(DateTime utcNow)
        {
            int day = PassMissions.DayKey(utcNow);
            int week = PassMissions.WeekKey(utcNow);
            bool dirty = false;

            if (Data.missionDayKey != day)
            {
                Data.missionDayKey = day;
                dirty |= ClearScope(MissionScope.Daily);
            }
            if (Data.missionWeekKey != week)
            {
                Data.missionWeekKey = week;
                dirty |= ClearScope(MissionScope.Weekly);
            }

            if (!dirty) return;
            SaveNow();
            Notify(Change.Missions);
        }

        static bool ClearScope(MissionScope scope)
        {
            bool changed = Data.missionProgress.RemoveAll(e =>
            {
                var m = PassMissions.Find(e.missionId);
                return m != null && m.scope == scope;
            }) > 0;

            changed |= Data.claimedMissionIds.RemoveAll(id =>
            {
                var m = PassMissions.Find(id);
                return m != null && m.scope == scope;
            }) > 0;

            return changed;
        }

        public static int GetMissionProgress(string missionId)
        {
            for (int i = 0; i < Data.missionProgress.Count; i++)
                if (Data.missionProgress[i].missionId == missionId) return Data.missionProgress[i].amount;
            return 0;
        }

        public static bool IsMissionClaimed(string missionId) =>
            !string.IsNullOrEmpty(missionId) && Data.claimedMissionIds.Contains(missionId);

        public static bool IsMissionComplete(PassMission mission) =>
            mission != null && GetMissionProgress(mission.id) >= mission.target;

        /// <summary>Adds progress, clamped at the target so a huge final kill cannot bank extra.</summary>
        public static void AddMissionProgress(string missionId, int amount)
        {
            if (string.IsNullOrEmpty(missionId) || amount <= 0) return;
            var mission = PassMissions.Find(missionId);
            if (mission == null) return;

            int current = GetMissionProgress(missionId);
            if (current >= mission.target) return;   // already done; nothing to record

            int next = Math.Min(mission.target, current + amount);
            bool found = false;
            for (int i = 0; i < Data.missionProgress.Count; i++)
            {
                if (Data.missionProgress[i].missionId != missionId) continue;
                Data.missionProgress[i] = new MissionProgressEntry { missionId = missionId, amount = next, weaponId = Data.weapon };
                found = true;
                break;
            }
            if (!found)
                Data.missionProgress.Add(new MissionProgressEntry { missionId = missionId, amount = next, weaponId = Data.weapon });

            MarkDirty();   // per kill: coalesced, not a disk write per zombie
            Notify(Change.Missions);
        }

        /// <summary>
        /// Claims a completed mission's reward exactly once.
        ///
        /// Same ordering as first-clear rewards: the claim is recorded and saved BEFORE the currency
        /// is granted, so an interruption costs the player a reward rather than letting them repeat it.
        /// </summary>
        public static bool TryClaimMission(string missionId)
        {
            var mission = PassMissions.Find(missionId);
            if (mission == null) return false;
            if (!IsMissionComplete(mission)) return false;
            if (Data.claimedMissionIds.Contains(missionId)) return false;

            Data.claimedMissionIds.Add(missionId);
            Data.passXp += mission.passXp;
            SaveNow();

            if (mission.coinReward > 0) Add(CurrencyKind.Coin, mission.coinReward);
            MissionClaimed?.Invoke(mission);
            // Daily Ops: 5 shards of the gun that finished it (owner 05/10: play a gun, it grows).
            if (mission is DailyOps.DailyOp)
            {
                string gun = MissionGun(missionId);
                AddWeaponShards(string.IsNullOrEmpty(gun) ? Data.weapon : gun, DailyOps.ShardsPerMission);
            }

            Notify(Change.Missions);
            return true;
        }

        /// <summary>The gun carried when the mission last moved (its shard reward goes there).</summary>
        public static string MissionGun(string missionId)
        {
            for (int i = 0; i < Data.missionProgress.Count; i++)
                if (Data.missionProgress[i].missionId == missionId) return Data.missionProgress[i].weaponId;
            return null;
        }

        /// Xoa tien do Pass. Chi dung cho test/dev.
        public static void ClearMissionProgressForTests()
        {
            Data.missionProgress.Clear();
            Data.claimedMissionIds.Clear();
            Data.missionDayKey = 0;
            Data.missionWeekKey = 0;
            Data.passXp = 0;
            SaveNow();
            Notify(Change.Missions);
        }
    }
}
