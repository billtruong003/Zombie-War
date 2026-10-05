using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// PlayerProfile, part: Run records, account level and XP, tickets, frames and avatars.
    public static partial class PlayerProfile
    {
        // ===== Run records =====

        /// <summary>Longest single run, in seconds. The endless mode's personal best - there is no
        /// stage to clear, so time survived is the record the Hub shows.</summary>
        public static float BestSurvivalSeconds => Data.bestSurvivalSeconds;

        /// <summary>Keeps the longer of the stored and the given survival time. Returns true when it
        /// set a new record.</summary>
        public static bool RecordSurvival(float seconds)
        {
            if (seconds <= Data.bestSurvivalSeconds) return false;
            Data.bestSurvivalSeconds = seconds;
            SaveNow();
            return true;
        }

        /// <summary>Best kill-based score of any run: the record the result screen celebrates.</summary>
        public static long BestScore => Data.bestScore;

        /// <summary>Keeps the higher score. True when it set a new record (a zero never does).</summary>
        public static bool RecordScore(long score)
        {
            if (score <= 0 || score <= Data.bestScore) return false;
            Data.bestScore = score;
            MarkDirty();
            return true;
        }

        // ===== Account level (M9) =====

        public static event Action AccountChanged;

        /// <summary>GameClock's high-water mark. Written lazily: it only guards against a clock set
        /// back, so losing the last minute of it costs nothing.</summary>
        internal static DateTime LastSeenUtc
        {
            get => Data.lastSeenUtcTicks > 0 ? new DateTime(Data.lastSeenUtcTicks, DateTimeKind.Utc) : DateTime.MinValue;
            set { Data.lastSeenUtcTicks = value.Ticks; MarkDirty(); }
        }

        public static int AccountXp => Data.accountXp;
        public static int AccountLevel => AccountProgress.LevelFor(Data.accountXp);

        public static string DisplayName => Data.displayName;
        public static string PlayerId => Data.playerId;
        public static int RunsPlayed => Data.runsPlayed;
        public static int BestKills => Data.bestKills;

        // Hash lookups over the saved lists: FTUE and radio checks run several times a second.
        private static HashSet<string> _ftueSet, _voSet;

        private static void InvalidateLookups() { _ftueSet = null; _voSet = null; }

        private static HashSet<string> FtueSet => _ftueSet ??= new HashSet<string>(Data.ftueSteps);
        private static HashSet<string> VoSet => _voSet ??= new HashSet<string>(Data.voFlags);

        public static bool HasFtueStep(string step) => !string.IsNullOrEmpty(step) && FtueSet.Contains(step);

        /// <summary>Records a first-time step. A coalesced write (MarkDirty): steps are marked in the
        /// middle of play, where a synchronous full save hitches the frame.</summary>
        public static void MarkFtueStep(string step)
        {
            if (string.IsNullOrEmpty(step) || !FtueSet.Add(step)) return;
            Data.ftueSteps.Add(step);
            MarkDirty();
        }

        /// <summary>Whether a once-only radio line, conversation or yearly egg was already used.</summary>
        public static bool HasVoFlag(string flag) => !string.IsNullOrEmpty(flag) && VoSet.Contains(flag);

        public static void MarkVoFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !VoSet.Add(flag)) return;
            Data.voFlags.Add(flag);
            MarkDirty();
        }

        /// <summary>QA: forget every first-time step and radio flag so the FTUE and its lines play again.</summary>
        public static void ClearFtueSteps()
        {
            Data.ftueSteps.Clear();
            Data.voFlags.Clear();
            InvalidateLookups();
            SaveNow();
        }
        public static long TotalKills => Data.totalKills;
        public static int PeakThreat => Data.peakThreat;
        public static float TotalSeconds => Data.totalSeconds;
        public static int BossesDefeated => Data.bossesDefeated;

        // ===== M10 tickets and frames =====
        public static long Tickets => Data.tickets;
        public static void AddTickets(long n)
        {
            if (n <= 0) return;
            Data.tickets += n; SaveNow(); Notify(Change.Wallet);
        }
        public static bool TrySpendTickets(long n)
        {
            if (n < 0 || Data.tickets < n) return false;
            Data.tickets -= n; SaveNow(); Notify(Change.Wallet);
            return true;
        }
        /// <summary>Owned avatar frames besides the default one.</summary>
        public static IReadOnlyList<string> OwnedFrames => Data.ownedFrames;
        /// <summary>Selected profile picture and frame (AvatarCatalog ids).</summary>
        public static string AvatarId => string.IsNullOrEmpty(Data.avatarId) ? AvatarCatalog.LiveAvatar : Data.avatarId;
        public static string FrameId => string.IsNullOrEmpty(Data.frameId) ? AvatarCatalog.DefaultFrame : Data.frameId;
        public static void SetAvatar(string id) { if (Data.avatarId == id) return; Data.avatarId = id; SaveNow(); Notify(Change.Account); }
        public static void SetFrame(string id) { if (Data.frameId == id) return; Data.frameId = id; SaveNow(); Notify(Change.Account); }

        public static void AddFrame(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.ownedFrames.Contains(id)) return;
            Data.ownedFrames.Add(id); SaveNow(); Notify(Change.Account);
        }

        /// <summary>Owned gun skin sets (ids from WeaponSkins.Season1).</summary>
        public static IReadOnlyList<string> OwnedSkins => Data.ownedSkins;
        public static bool IsSkinOwned(string id) => Data.ownedSkins.Contains(id);
        public static void AddSkin(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.ownedSkins.Contains(id)) return;
            Data.ownedSkins.Add(id); SaveNow(); Notify(Change.Loadout);
        }

        public static void AddWeaponShards(string weaponId, int amount)
        {
            if (string.IsNullOrEmpty(weaponId) || amount <= 0) return;
            AddWeaponShardsInMemory(weaponId, amount);
            SaveNow(); Notify(Change.Loadout);
        }

        public static bool NoAds => Data.noAds;

        public const int MaxLooks = 3;
        public static int LookCount => Data.looks.Count;

        /// <summary>Saves the current outfit into look <paramref name="index"/> (appends when index == count).</summary>
        public static bool SaveLook(int index)
        {
            if (index < 0 || index > Data.looks.Count || index >= MaxLooks) return false;
            var look = new SavedLook { parts = new List<LoadoutState.PartSel>(Data.equippedParts) };
            if (index == Data.looks.Count) Data.looks.Add(look); else Data.looks[index] = look;
            SaveNow();
            return true;
        }

        public static IReadOnlyList<LoadoutState.PartSel> GetLook(int index) =>
            index >= 0 && index < Data.looks.Count ? Data.looks[index].parts : null;

        /// <summary>True when the worn outfit is exactly look <paramref name="index"/>.</summary>
        public static bool IsWearingLook(int index)
        {
            var l = GetLook(index);
            if (l == null || l.Count != Data.equippedParts.Count) return false;
            foreach (var p in l) if (!Data.equippedParts.Exists(x => x.slot == p.slot && x.guid == p.guid)) return false;
            return true;
        }

        /// <summary>The skin set shown and counted on a gun, or null for the plain gun.</summary>
        public static string GetEquippedSkin(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            string prefix = weaponId + "|";
            foreach (var e in Data.equippedSkins) if (e.StartsWith(prefix, StringComparison.Ordinal)) return e.Substring(prefix.Length);
            return null;
        }

        /// <summary>Puts an owned set on a gun (null takes it off). False when not owned.</summary>
        public static bool SetEquippedSkin(string weaponId, string skinId)
        {
            if (string.IsNullOrEmpty(weaponId)) return false;
            if (!string.IsNullOrEmpty(skinId) && !IsSkinOwned(skinId)) return false;
            string prefix = weaponId + "|";
            Data.equippedSkins.RemoveAll(e => e.StartsWith(prefix, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(skinId)) Data.equippedSkins.Add(prefix + skinId);
            SaveNow(); Notify(Change.Loadout);
            return true;
        }

        /// <summary>Pass season XP. Only <see cref="PassRewards"/> resets it at a season change.</summary>
        internal static void ResetPassXp() { Data.passXp = 0; }

        /// <summary>Daily state lives in the profile save; <see cref="DailyRewards"/> owns the rules.</summary>
        internal static ProfileData DailyData => Data;
        internal static void SaveDaily() { SaveNow(); Notify(Change.Account); }

        public static void RecordBossDefeated()
        {
            Data.bossesDefeated++;
            SaveNow();
        }

        /// <summary>
        /// Settings "Delete my data": wipes the saved profile in every build (store privacy rule),
        /// unlike <see cref="ResetForDev"/>, which only runs in dev builds.
        /// </summary>
        public static void DeleteAllData()
        {
            var storage = Storage;
            storage.Delete(SaveKey);
            storage.Delete(BackupKey);
            // The pre-profile keys too, or the next load migrated the old wallet and loadout back in.
            foreach (var key in LegacyPrefKeys) PlayerPrefs.DeleteKey(key);
            storage.Flush();
            _saveDirty = false;
            _flushScheduled = false;
            ResetCacheForTests();
            Notify(Change.Account);
        }

        /// <summary>Lifetime stats for the Profile screen, added once per closed run.</summary>
        /// <summary>
        /// One finished run: lifetime stats, the best-kills record, and the run count. Every closed
        /// run counts, even one that earned no account XP (a run under 2 s with no kill used to leave
        /// the first-run gate locked because only XP counted it).
        /// </summary>
        public static void RecordRunStats(int kills, int peakThreat, float seconds)
        {
            Data.runsPlayed++;
            Data.bestKills = Math.Max(Data.bestKills, kills);
            Data.totalKills += Math.Max(0, kills);
            Data.peakThreat = Math.Max(Data.peakThreat, peakThreat);
            if (!float.IsNaN(seconds) && seconds > 0f) Data.totalSeconds += seconds;
            SaveNow();
        }

        /// <summary>Renames the player (Profile screen). Trims, caps at 16 characters, ignores blanks.</summary>
        public static bool SetDisplayName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return false;
            if (name.Length > 16) name = name.Substring(0, 16);
            Data.displayName = name;
            SaveNow();
            Notify(Change.Account);
            return true;
        }

        /// <summary>Adds account XP and returns how many levels it gained.</summary>
        public static int AddAccountXp(int xp)
        {
            if (xp <= 0) return 0;
            int before = AccountLevel;
            Data.accountXp += xp;
            SaveNow();
            Notify(Change.Account);
            return AccountLevel - before;
        }
    }
}
