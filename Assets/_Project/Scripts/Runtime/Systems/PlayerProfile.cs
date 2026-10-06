using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// Profile nguoi choi DUY NHAT va co version — nguon su that cho vi tien (Coin/Gold/Gem),
    /// so huu sung (canonical WeaponId), 3 slot sung dang trang bi, so huu/trang bi costume (GUID)
    /// va field upgrade giu cho tuong lai (chua co hieu ung gameplay).
    ///
    /// Luu qua Bill.Save (BillGameCore SaveService — PlayerPrefs-backed, key thuc te "s0_zw.profile").
    /// UI/gameplay KHONG doc PlayerPrefs truc tiep: LoadoutState delegate storage vao day va giu
    /// nguyen seam ApplyTo cho PlayerSpawner.
    ///
    /// Migration (chay dung 1 lan, khi chua co profile hop le): import "zw.loadout" +
    /// "wallet_coin/gold/gem" cu. Key cu KHONG bi xoa/ghi de — giu nguyen de rollback.
    public static partial class PlayerProfile
    {
        /// v2: the three weapon slots collapsed into the single run weapon (M6 one-weapon contract).
        public const int SchemaVersion = 3;   // v3: voice-line flags moved out of ftueSteps into voFlags
        public const string SaveKey = "zw.profile";

        public enum CurrencyKind { Coin, Gold, Gem }

        [Serializable]
        public struct WeaponUpgradeEntry
        {
            public string weaponId;
            public int level;
        }

        [Serializable]
        public struct WeaponShardEntry
        {
            public string weaponId;
            public int count;
        }

        /// <summary>One gun's lifetime run stats (backlog #18). Mastery XP lives here too (#21).</summary>
        [Serializable]
        public struct GunStatEntry
        {
            public string weaponId;
            public int runs;
            public long kills;
            public float seconds;
            public long bestScore;
            public int masteryXp;
        }

        [Serializable]
        public struct GachaPityEntry
        {
            public string poolId;
            public int count;
        }

        [Serializable]
        public class SavedLook { public List<LoadoutState.PartSel> parts = new List<LoadoutState.PartSel>(); }

        [Serializable]
        public class ProfileData
        {
            public int version = SchemaVersion;
            public long coin;
            public long gold;
            public long gem;
            public List<string> ownedWeaponIds = new();
            // The one weapon the player carries into a run (M6: no in-run switching).
            public string weapon = "";
            // Pre-v2 three-slot loadout. Read once by Normalize to seed `weapon`, then cleared.
            public string pistol = "";
            public string longA = "";
            public string longB = "";
            public List<string> ownedCostumeGuids = new();
            public List<LoadoutState.PartSel> equippedParts = new();
            // Body composite (Slice 4.2): mau + bien the tai (khong luu qua equippedParts vi Body =
            // 2 renderer va identity la presentation, khong phai raw GUID).
            public string bodyColor = "";
            public string bodyEar = "";
            public List<string> ownedBodyColors = new();
            public List<string> ownedBodyEars = new();
            // Gacha pity (Slice 6): dem pull khong ra rarity cao, per pool.
            public List<GachaPityEntry> gachaPity = new();
            // New-item badge (Slice 7): id chua xem.
            public List<string> unseenItems = new();
            // Giu cho phase Upgrades — persist duoc ngay tu v1 de khoi phai migrate schema sau.
            public List<WeaponUpgradeEntry> weaponUpgrades = new();
            public List<WeaponShardEntry> weaponShards = new();
            public List<GunStatEntry> gunStats = new();
            public List<string> achievements = new();
            public List<string> achievementsClaimed = new();
            public List<string> evolvedGuns = new();
            // Endless-run personal best (seconds survived in one run).
            public float bestSurvivalSeconds;
            // Battle Pass. Progress is keyed by mission ID; the reset keys record which UTC
            // day/week the current daily/weekly progress belongs to, so a rollover wipes only the
            // scope that actually expired.
            public List<MissionProgressEntry> missionProgress = new();
            public List<string> claimedMissionIds = new();
            public int missionDayKey;
            public int missionWeekKey;
            public int passXp;
            // Daily Ops chest (05/10): the game day it was last opened and the streak of days in a row.
            public int dailyChestDay;
            public int dailyStreak;
            // The day's Daily Ops, dealt once (QA 05/10).
            public int dailyOpsDay;
            public List<string> dailyOpsIds = new();
            // M9 account level (AccountProgress turns XP into a level and feature gates).
            public int accountXp;
            // M10 Profile: a display name and a stable 8-digit player id (made on first load).
            public string displayName;
            public string playerId;
            public int runsPlayed;
            // M10 Profile stats.
            public long totalKills;
            public int peakThreat;
            public float totalSeconds;
            public int bossesDefeated;
            // Best kills in one run (2026-10-01): the kill score's record, shown once the result
            // screen mockup is approved.
            public int bestKills;
            public long bestScore;
            // First-time-user steps already done (Ftue.Move, Ftue.Reveal, ...).
            public List<string> ftueSteps = new List<string>();
            // Radio lines already used once, conversations and yearly eggs (v3; were "vo.*" FTUE steps).
            public List<string> voFlags = new List<string>();
            // M10 Daily: gacha tickets, avatar frames, the 7-day welcome check-in, the 28-day stamp card.
            public long tickets;
            public List<string> ownedFrames = new List<string>();
            public string avatarId;   // AvatarCatalog id; empty = the live character
            public string frameId;    // AvatarCatalog frame id; empty = classic
            public int welcomeClaims;
            public int welcomeLastDay;
            public int stampCycleStart;
            public int stampCount;
            public int stampLastDay;
            public int stampMakeUps;
            // M10 Pass v2 and gun skins.
            public List<string> ownedSkins = new List<string>();
            public int passSeason;
            public int passSeasonStart;
            public bool passPremium;          // legacy (v3 and earlier); premium is passPremiumSeason
            public int passPremiumSeason;     // the season premium was bought for; 0 = none
            public long lastSeenUtcTicks;     // GameClock: the latest time seen, so the clock never runs back
            public List<string> passClaimed = new List<string>();
            public List<string> equippedSkins = new List<string>();   // "weaponId|skinId"
            // M10 Shop v2.
            public int dealDay;
            public List<string> dealsBought = new List<string>();
            public List<string> packsBought = new List<string>();
            public bool noAds;
            public long starterEndsTicks;   // UTC end of the one-time starter offer (0 = not shown yet)
            // M10 Gacha v2.
            public int gachaEpoch;
            public int gachaFreeDay;
            // M10 Studio saved looks.
            public List<SavedLook> looks = new List<SavedLook>();
        }

        [Serializable]
        public struct MissionProgressEntry
        {
            public string missionId;
            public int amount;
            /// <summary>The gun carried when progress was last made: Daily Ops pay its shards.</summary>
            public string weaponId;
        }

        /// Vi tien doi (mua sung/costume o slice sau se nghe event nay de refresh so du).
        public static event Action WalletChanged;

        /// Slot sung / so huu SUNG doi (Loadout/Shop screen nghe event nay).
        public static event Action LoadoutChanged;

        /// Costume ownership/equipment doi (Costume screen + preview nghe event nay —
        /// tach khoi LoadoutChanged de man sung khong refresh oan khi doi do).
        public static event Action CostumeChanged;

        private static ProfileData _data;
        private static SaveService _fallbackStorage;
        private static bool _warnedCorrupt;
        private static readonly HashSet<string> _warnedUnknownIds = new();

        // ===== Test seams (InternalsVisibleTo _Project.Tests) =====
        // Storage that ra ngoai la Bill.Save; test cam mot ISaveService in-memory vao de khong
        // dung PlayerPrefs that. Legacy reader tach rieng vi key cu la PlayerPrefs KHONG prefix.
        internal static ISaveService StorageOverride;
        internal static Func<string, string> LegacyReadString = key => PlayerPrefs.GetString(key, "");
        internal static Func<string, int> LegacyReadInt = key => PlayerPrefs.GetInt(key, 0);

        // Domain reload is off: without this the cached profile, the flush flags and every event's
        // subscriber list (screens destroyed in the last play session) survive into the next Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ResetCacheForTests();
            _saveDirty = false;
            _flushScheduled = false;
            _batchDepth = 0;
            _flushTimer = null;
            _pendingChanges = Change.None;
            _batchSnapshot = null;
            _newerSchema = false;
            WalletChanged = null;
            LoadoutChanged = null;
            CostumeChanged = null;
            AccountChanged = null;
            MissionsChanged = null;
        }

        internal static void ResetCacheForTests()
        {
            _data = null;
            InvalidateLookups();
            _newerSchema = false;
            _warnedCorrupt = false;
            _warnedUnknownIds.Clear();
            _warnedDefaultIssues.Clear();
        }

        // Bill.Save chi ton tai sau bootstrap; fallback dung SaveService cuc bo — vo hai vi
        // SaveService stateless tren PlayerPrefs voi cung format key "s0_*". Neu du an bat dau
        // dung SetSlot (multi-slot save) thi fallback nay phai bo.
        private static ISaveService Storage => StorageOverride ?? Protected(Bill.IsReady ? Bill.Save : _fallbackStorage ??= new SaveService());

        // The profile and its backup are stored encrypted (ProtectedSave); the wrapper follows Bill.Save.
        private static ProtectedSave _protected;
        private static ISaveService Protected(ISaveService inner)
        {
            if (_protected == null || !ReferenceEquals(_protected.Inner, inner))
                _protected = new ProtectedSave(inner, key => key == SaveKey || key == BackupKey || key == CorruptKey);
            return _protected;
        }

        public static bool HasProfile => Storage.Has(SaveKey);

        private static ProfileData Data
        {
            get
            {
                if (_data != null) return _data;

                var storage = Storage;
                if (storage.Has(SaveKey))
                {
                    var loaded = storage.Get<ProfileData>(SaveKey); // null neu JSON hong (Get<T> catch)
                    if (loaded == null)
                    {
                        // A damaged save is never overwritten blind: keep its raw text for support,
                        // then fall back to the last profile that loaded cleanly.
                        storage.Set(CorruptKey, storage.GetString(SaveKey));
                        loaded = storage.Get<ProfileData>(BackupKey);
                        if (loaded != null) Debug.LogWarning("[PlayerProfile] Profile unreadable - restored the last good backup; the damaged copy is kept as " + CorruptKey);
                    }
                    if (loaded != null)
                    {
                        // A save written by a newer build is read but never written back: JsonUtility
                        // would drop the fields this build does not know.
                        _newerSchema = loaded.version > SchemaVersion;
                        if (_newerSchema) Debug.LogWarning($"[PlayerProfile] Profile schema v{loaded.version} is newer than v{SchemaVersion}; saving is disabled for this session.");
                        else storage.Set(BackupKey, storage.GetString(SaveKey));   // last good copy
                        // An older schema is migrated by Normalize and written back once, so the
                        // upgrade is persisted rather than silently re-run on every launch.
                        // A profile from before M10 has no player ID; the one Normalize makes must be kept.
                        bool outdated = loaded.version < SchemaVersion || string.IsNullOrEmpty(loaded.playerId);
                        _data = Normalize(loaded);
                        InvalidateLookups();
                        if (outdated) SaveNow();
                        return _data;
                    }
                    if (!_warnedCorrupt)
                    {
                        Debug.LogWarning("[PlayerProfile] Profile hong/khong parse duoc — khoi phuc tu du lieu legacy (key cu van con nguyen).");
                        _warnedCorrupt = true;
                    }
                }

                _data = Normalize(MigrateFromLegacy());
                InvalidateLookups();
                SaveNow();
                return _data;
            }
        }

        private static bool _saveDirty;
        private static bool _flushScheduled;
        private const float DeferredFlushSeconds = 1.5f;   // secured currency (gems): written soon
        private const float LazyFlushSeconds = 20f;        // bookkeeping (missions, voice flags): run end,
                                                           // pause and quit flush it; this is the crash net
        private static float _flushDue;
        private static TimerHandle _flushTimer;

        public const string BackupKey = "zw.profile.bak", CorruptKey = "zw.profile.corrupt";
        private static bool _newerSchema;

        // ===== Write path =====
        // Three ways to persist, by how hot the caller is:
        //  - SaveNow: write now (purchases, claims). Inside a Batch it only marks the profile dirty.
        //  - MarkDirty: coalesced write a moment later (per-kill mission progress, voice-line flags):
        //    the old save-on-every-kill wrote the whole profile to disk ~1x per kill mid-horde.
        //  - Batch: many changes, one write and one round of events, rolled back as a unit on failure.

        [Flags]
        private enum Change { None = 0, Wallet = 1, Loadout = 2, Costume = 4, Account = 8, Missions = 16 }

        private static int _batchDepth;
        private static Change _pendingChanges;
        private static string _batchSnapshot;

        private static void Notify(Change c)
        {
            if (_batchDepth > 0) { _pendingChanges |= c; return; }
            if ((c & Change.Wallet) != 0) WalletChanged?.Invoke();
            if ((c & Change.Loadout) != 0) LoadoutChanged?.Invoke();
            if ((c & Change.Costume) != 0) CostumeChanged?.Invoke();
            if ((c & Change.Account) != 0) AccountChanged?.Invoke();
            if ((c & Change.Missions) != 0) MissionsChanged?.Invoke();
        }

        /// <summary>Runs <paramref name="body"/> as one transaction: every save inside is coalesced into
        /// one write at the end and every change event fires once, after it. If the body throws or the
        /// write fails, the whole profile goes back to how it was and no event fires.</summary>
        public static bool Batch(Action body)
        {
            if (body == null) return true;
            if (_batchDepth == 0) { _batchSnapshot = Snapshot(); _pendingChanges = Change.None; }
            _batchDepth++;
            bool ok = true;
            try { body(); }
            catch (Exception e) { ok = false; Debug.LogException(e); }
            finally { _batchDepth--; }
            if (_batchDepth > 0) return ok;   // the outermost batch commits

            var changes = _pendingChanges;
            _pendingChanges = Change.None;
            if (ok && _saveDirty) ok = TryCommit(null, "[PlayerProfile] Batch save failed - rollback.");
            if (!ok) { Restore(_batchSnapshot); _batchSnapshot = null; return false; }
            _batchSnapshot = null;
            Notify(changes);
            return true;
        }

        /// <summary>Saves; on failure runs <paramref name="rollback"/> and logs. The one copy of the
        /// save-or-undo step every purchase/equip used to repeat.</summary>
        private static bool TryCommit(Action rollback, string failure)
        {
            try { SaveNow(); return true; }
            catch (Exception e)
            {
                try { rollback?.Invoke(); } catch (Exception r) { Debug.LogException(r); }
                Debug.LogError(failure + " " + e.Message);
                return false;
            }
        }

        private static string Snapshot() => JsonUtility.ToJson(Data);

        private static void Restore(string snapshot)
        {
            if (string.IsNullOrEmpty(snapshot)) return;
            _data = Normalize(JsonUtility.FromJson<ProfileData>(snapshot));
            InvalidateLookups();
        }

        /// <summary>Marks the profile changed and lets a short timer write it (or the end of the run,
        /// app pause or quit, whichever comes first).</summary>
        private static void MarkDirty(bool soon = false)
        {
            _saveDirty = true;
            if (_batchDepth == 0) ScheduleFlush(soon ? DeferredFlushSeconds : LazyFlushSeconds);
        }

        private static void SaveNow()
        {
            if (_batchDepth > 0) { _saveDirty = true; return; }
            _saveDirty = false;
            if (_newerSchema) return;
            var storage = Storage;
            storage.Set(SaveKey, _data);
            storage.Flush();
        }
    }
}
