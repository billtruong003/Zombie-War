using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// PlayerProfile, part: Dev resets and QA cheat-panel shortcuts.
    public static partial class PlayerProfile
    {
        // ===== Dev reset =====

        /// Xoa profile de test tu dau (Editor/dev build). Key legacy giu nguyen — lan load ke tiep
        /// se migrate lai tu chung (dung de test migration lap lai).
        public static void ResetForDev()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
            var storage = Storage;
            storage.Delete(SaveKey);
            storage.Flush();
            ResetCacheForTests();
            Debug.Log("[PlayerProfile] Da xoa profile (dev reset). Legacy keys giu nguyen.");
#else
            Debug.LogWarning("[PlayerProfile] ResetForDev chi chay trong Editor/dev build.");
#endif
        }

        // ===== Dev (QA cheat panel) =====
        // Shortcuts the QA panel needs to reach states that normally take days of play: pity on
        // the edge, today's free pull and stamp again, pass XP, every skin and frame.
        // Compiled only where the QA panel exists (the _Project.Dev assembly's constraint).
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS

        public static void DevSetPity(string poolId, int count)
        {
            if (string.IsNullOrEmpty(poolId)) return;
            SetPityInMemory(poolId, Math.Max(0, count));
            SaveNow(); Notify(Change.Wallet);
        }

        /// Today's free pull, daily stamp, welcome reward and the mission window, all fresh again.
        public static void DevResetDailyClocks()
        {
            Data.gachaFreeDay = -1; Data.stampLastDay = -1; Data.welcomeLastDay = -1; Data.missionDayKey = 0;
            SaveNow(); Notify(Change.Wallet); Notify(Change.Missions);
        }

        public static void DevAddPassXp(int xp)
        {
            Data.passXp = Math.Max(0, Data.passXp + xp);
            SaveNow(); Notify(Change.Missions);
        }

        public static int DevUnlockAllSkins()
        {
            int n = 0;
            foreach (var s in ZombieWar.Skins.WeaponSkins.Season1)
                if (s != null && !Data.ownedSkins.Contains(s.id)) { Data.ownedSkins.Add(s.id); n++; }
            SaveNow(); Notify(Change.Loadout);
            return n;
        }

        public static int DevUnlockAllFrames()
        {
            int n = 0;
            foreach (var f in AvatarCatalog.Frames)
                if (f != null && !Data.ownedFrames.Contains(f.id)) { Data.ownedFrames.Add(f.id); n++; }
            SaveNow(); Notify(Change.Account);
            return n;
        }
#endif
    }
}
