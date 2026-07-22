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
    public static class PlayerProfile
    {
        public const int SchemaVersion = 1;
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

        [Serializable]
        public struct GachaPityEntry
        {
            public string poolId;
            public int count;
        }

        [Serializable]
        public class ProfileData
        {
            public int version = SchemaVersion;
            public long coin;
            public long gold;
            public long gem;
            public List<string> ownedWeaponIds = new();
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
            // Campaign progress. Stored as stable level IDs, never indices, so reordering or
            // inserting a stage can never silently re-lock or re-unlock the wrong one.
            public List<string> completedLevelIds = new();
            public List<string> claimedFirstClearIds = new();
            public string lastSelectedLevelId = "";
            // Battle Pass. Progress is keyed by mission ID; the reset keys record which UTC
            // day/week the current daily/weekly progress belongs to, so a rollover wipes only the
            // scope that actually expired.
            public List<MissionProgressEntry> missionProgress = new();
            public List<string> claimedMissionIds = new();
            public int missionDayKey;
            public int missionWeekKey;
            public int passXp;
        }

        [Serializable]
        public struct MissionProgressEntry
        {
            public string missionId;
            public int amount;
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

        internal static void ResetCacheForTests()
        {
            _data = null;
            _warnedCorrupt = false;
            _warnedUnknownIds.Clear();
            _warnedDefaultIssues.Clear();
        }

        // Bill.Save chi ton tai sau bootstrap; fallback dung SaveService cuc bo — vo hai vi
        // SaveService stateless tren PlayerPrefs voi cung format key "s0_*". Neu du an bat dau
        // dung SetSlot (multi-slot save) thi fallback nay phai bo.
        private static ISaveService Storage =>
            StorageOverride ?? (Bill.IsReady ? Bill.Save : _fallbackStorage ??= new SaveService());

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
                    if (loaded != null)
                    {
                        _data = Normalize(loaded);
                        return _data;
                    }
                    if (!_warnedCorrupt)
                    {
                        Debug.LogWarning("[PlayerProfile] Profile hong/khong parse duoc — khoi phuc tu du lieu legacy (key cu van con nguyen).");
                        _warnedCorrupt = true;
                    }
                }

                _data = Normalize(MigrateFromLegacy());
                SaveNow();
                return _data;
            }
        }

        private static void SaveNow()
        {
            var storage = Storage;
            storage.Set(SaveKey, _data);
            storage.Flush();
        }

        // ===== Campaign progress =====

        /// Progress doi (Campaign screen nghe event nay de ve lai trang thai khoa/mo).
        public static event Action CampaignChanged;

        public static bool IsLevelCompleted(string levelId) =>
            !string.IsNullOrEmpty(levelId) && Data.completedLevelIds.Contains(levelId);

        public static bool IsFirstClearClaimed(string levelId) =>
            !string.IsNullOrEmpty(levelId) && Data.claimedFirstClearIds.Contains(levelId);

        public static IReadOnlyList<string> CompletedLevelIds => Data.completedLevelIds;

        public static string LastSelectedLevelId
        {
            get => Data.lastSelectedLevelId;
            set
            {
                if (Data.lastSelectedLevelId == value) return;
                Data.lastSelectedLevelId = value ?? "";
                SaveNow();
            }
        }

        /// Danh dau man da qua. Idempotent: goi lai khong nhan doi gi.
        public static void MarkLevelCompleted(string levelId)
        {
            if (string.IsNullOrEmpty(levelId) || Data.completedLevelIds.Contains(levelId)) return;
            Data.completedLevelIds.Add(levelId);
            SaveNow();
            CampaignChanged?.Invoke();
        }

        /// <summary>
        /// Tra thuong lan dau qua man, dung MOT lan duy nhat.
        ///
        /// The claim flag is written BEFORE the currency is granted, so if anything throws between
        /// the two the player loses a reward rather than being able to farm it - the safe failure
        /// direction. Returns false when it was already claimed.
        /// </summary>
        public static bool TryClaimFirstClear(string levelId, long coin, long gold, long gem)
        {
            if (string.IsNullOrEmpty(levelId) || Data.claimedFirstClearIds.Contains(levelId)) return false;

            Data.claimedFirstClearIds.Add(levelId);
            SaveNow();

            if (coin > 0) Add(CurrencyKind.Coin, coin);
            if (gold > 0) Add(CurrencyKind.Gold, gold);
            if (gem > 0) Add(CurrencyKind.Gem, gem);

            CampaignChanged?.Invoke();
            return true;
        }

        // ===== Battle Pass missions =====

        public static event Action MissionsChanged;

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
            MissionsChanged?.Invoke();
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
                Data.missionProgress[i] = new MissionProgressEntry { missionId = missionId, amount = next };
                found = true;
                break;
            }
            if (!found)
                Data.missionProgress.Add(new MissionProgressEntry { missionId = missionId, amount = next });

            SaveNow();
            MissionsChanged?.Invoke();
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

            MissionsChanged?.Invoke();
            return true;
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
            MissionsChanged?.Invoke();
        }

        /// Xoa tien do campaign. Chi dung cho test/dev — khong dung trong gameplay.
        public static void ClearCampaignProgressForTests()
        {
            Data.completedLevelIds.Clear();
            Data.claimedFirstClearIds.Clear();
            Data.lastSelectedLevelId = "";
            SaveNow();
            CampaignChanged?.Invoke();
        }

        // ===== Wallet =====

        public static long Coin => Data.coin;
        public static long Gold => Data.gold;
        public static long Gem => Data.gem;

        public static long GetBalance(CurrencyKind kind) =>
            kind == CurrencyKind.Coin ? Data.coin : kind == CurrencyKind.Gold ? Data.gold : Data.gem;

        /// Cong tien. Am bi tu choi (chi tieu tien qua TrySpend). Overflow clamp ve long.MaxValue.
        public static void Add(CurrencyKind kind, long amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning($"[PlayerProfile] Add({kind}, {amount}) am — bo qua. Dung TrySpend de tru tien.");
                return;
            }
            if (amount == 0) return;
            long current = GetBalance(kind);
            long next = current + amount;
            if (next < current) next = long.MaxValue; // overflow clamp
            SetBalance(kind, next);
            SaveNow();
            WalletChanged?.Invoke();
        }

        /// Tru tien nguyen tu: false (khong doi gi) neu amount am hoac so du khong du.
        public static bool TrySpend(CurrencyKind kind, long amount)
        {
            if (amount < 0) return false;
            long current = GetBalance(kind);
            if (current < amount) return false;
            SetBalance(kind, current - amount);
            SaveNow();
            WalletChanged?.Invoke();
            return true;
        }

        private static void SetBalance(CurrencyKind kind, long value)
        {
            if (kind == CurrencyKind.Coin) Data.coin = value;
            else if (kind == CurrencyKind.Gold) Data.gold = value;
            else Data.gem = value;
        }

        public static void SetBalanceForDev(CurrencyKind kind, long value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SetBalance(kind, Math.Max(0, value));
            SaveNow();
            WalletChanged?.Invoke();
#else
            Debug.LogWarning("[PlayerProfile] SetBalanceForDev is only available in Editor/development builds.");
#endif
        }

        // ===== Weapon purchase (atomic) =====

        public enum PurchaseResult { Purchased, AlreadyOwned, InsufficientFunds, InvalidWeapon, InvalidPrice, SaveFailed }

        /// Giao dich mua sung NGUYEN TU: validate -> tru Coin -> cap so huu -> luu profile DUNG 1 LAN
        /// -> bao event SAU khi commit. Moi duong that bai khong doi bat ky state nao.
        /// price = WeaponData.price (Coin); 0 = sung free/starter. KHONG dung unlockCost.
        /// Neu luu that bai: rollback in-memory ve trang thai truoc giao dich, khong event.
        public static PurchaseResult TryPurchaseWeapon(string weaponId, long price)
        {
            if (string.IsNullOrEmpty(weaponId)) return PurchaseResult.InvalidWeapon;
            if (price < 0) return PurchaseResult.InvalidPrice;

            var d = Data;
            if (d.ownedWeaponIds.Contains(weaponId)) return PurchaseResult.AlreadyOwned;
            if (d.coin < price) return PurchaseResult.InsufficientFunds;

            d.coin -= price;
            d.ownedWeaponIds.Add(weaponId);
            try
            {
                SaveNow();
            }
            catch (Exception e)
            {
                d.coin += price;
                d.ownedWeaponIds.Remove(weaponId);
                Debug.LogError($"[PlayerProfile] Luu profile that bai khi mua '{weaponId}' — rollback, khong tru tien. {e.Message}");
                return PurchaseResult.SaveFailed;
            }

            if (price > 0) WalletChanged?.Invoke();
            LoadoutChanged?.Invoke();
            return PurchaseResult.Purchased;
        }

        // ===== Costume purchase (Slice 5) =====

        /// Mua costume item (guid part | "body:&lt;Color&gt;" | "ear:&lt;Ear&gt;") ATOMIC: validate sellable +
        /// khong phai starter + chua so huu + du tien -> tru + cap so huu + luu 1 lan + event.
        /// Gia lay tu EconomyConfig rarity band (khong rai constant o UI). That bai = khong doi state.
        public static PurchaseResult TryPurchaseCostume(EconomyConfig econ, string itemId)
        {
            if (econ == null || string.IsNullOrEmpty(itemId)) return PurchaseResult.InvalidWeapon;
            if (!econ.TryGetCostume(itemId, out var e)) return PurchaseResult.InvalidWeapon;
            if (e.source == AcquireSource.Starter || e.source == AcquireSource.Disabled || e.source == AcquireSource.Gacha)
                return PurchaseResult.InvalidWeapon; // khong ban starter/gacha-only/disabled
            if (IsCostumeItemOwned(itemId)) return PurchaseResult.AlreadyOwned;
            if (!econ.TryGetCostumePrice(itemId, out var cur, out long price) || price < 0)
                return PurchaseResult.InvalidPrice;

            var kind = ToKind(cur);
            if (GetBalance(kind) < price) return PurchaseResult.InsufficientFunds;

            long before = GetBalance(kind);
            SetBalance(kind, before - price);
            GrantCostumeItem(itemId);
            try { SaveNow(); }
            catch (Exception ex)
            {
                SetBalance(kind, before); RevokeCostumeItem(itemId);
                Debug.LogError($"[PlayerProfile] Save fail mua costume '{itemId}' — rollback. {ex.Message}");
                return PurchaseResult.SaveFailed;
            }
            if (price > 0) WalletChanged?.Invoke();
            CostumeChanged?.Invoke();
            return PurchaseResult.Purchased;
        }

        public static bool IsCostumeSetOwned(EconomyConfig.CostumeSetEntry set)
        {
            if (set == null || set.itemIds == null || set.itemIds.Count == 0) return false;
            for (int i = 0; i < set.itemIds.Count; i++)
                if (!IsCostumeItemOwned(set.itemIds[i])) return false;
            return true;
        }

        /// Buy one curated outfit as a single atomic Gem transaction. Items already owned through
        /// another overlapping set are not charged separately; the set price is the authored offer.
        public static PurchaseResult TryPurchaseCostumeSet(EconomyConfig econ, string setId)
        {
            if (econ == null || !econ.TryGetCostumeSet(setId, out var set) || set.itemIds == null || set.itemIds.Count == 0)
                return PurchaseResult.InvalidWeapon;
            if (set.source != AcquireSource.Shop && set.source != AcquireSource.ShopAndGacha)
                return PurchaseResult.InvalidWeapon;
            if (!econ.TryGetCostumeSetPrice(set, out var currency, out long price) || price < 0)
                return PurchaseResult.InvalidPrice;
            if (IsCostumeSetOwned(set)) return PurchaseResult.AlreadyOwned;
            var kind = ToKind(currency);
            if (GetBalance(kind) < price) return PurchaseResult.InsufficientFunds;

            string snapshot = JsonUtility.ToJson(Data);
            SetBalance(kind, GetBalance(kind) - price);
            for (int i = 0; i < set.itemIds.Count; i++) GrantCostumeItem(set.itemIds[i]);
            try { SaveNow(); }
            catch (Exception e)
            {
                _data = Normalize(JsonUtility.FromJson<ProfileData>(snapshot));
                Debug.LogError($"[PlayerProfile] Save failed while purchasing set '{setId}' - rollback. {e.Message}");
                return PurchaseResult.SaveFailed;
            }
            WalletChanged?.Invoke();
            CostumeChanged?.Invoke();
            return PurchaseResult.Purchased;
        }

        public static bool IsCostumeItemOwned(string itemId)
        {
            if (EconomyConfig.IsBodyColorId(itemId, out var col)) return IsBodyColorOwned(col);
            if (EconomyConfig.IsBodyEarId(itemId, out var ear)) return IsBodyEarOwned(ear);
            return IsCostumeOwned(itemId);
        }

        // Cap/thu so huu 1 item (in-memory, khong save/event — dung trong transaction).
        private static void GrantCostumeItem(string itemId)
        {
            if (EconomyConfig.IsBodyColorId(itemId, out var col)) { if (col != "White" && !Data.ownedBodyColors.Contains(col)) Data.ownedBodyColors.Add(col); }
            else if (EconomyConfig.IsBodyEarId(itemId, out var ear)) { if (ear != "Normal" && !Data.ownedBodyEars.Contains(ear)) Data.ownedBodyEars.Add(ear); }
            else if (!Data.ownedCostumeGuids.Contains(itemId)) Data.ownedCostumeGuids.Add(itemId);
        }

        private static void RevokeCostumeItem(string itemId)
        {
            if (EconomyConfig.IsBodyColorId(itemId, out var col)) Data.ownedBodyColors.Remove(col);
            else if (EconomyConfig.IsBodyEarId(itemId, out var ear)) Data.ownedBodyEars.Remove(ear);
            else Data.ownedCostumeGuids.Remove(itemId);
        }

        private static CurrencyKind ToKind(WalletCurrency c) =>
            c == WalletCurrency.Gold ? CurrencyKind.Gold : c == WalletCurrency.Gem ? CurrencyKind.Gem : CurrencyKind.Coin;

        // ===== Gacha pity + grant (Slice 6) =====

        public static int GetPity(string poolId)
        {
            for (int i = 0; i < Data.gachaPity.Count; i++) if (Data.gachaPity[i].poolId == poolId) return Data.gachaPity[i].count;
            return 0;
        }

        // In-memory pity setter (transaction commit qua GachaService).
        internal static void SetPityInMemory(string poolId, int count)
        {
            for (int i = 0; i < Data.gachaPity.Count; i++)
                if (Data.gachaPity[i].poolId == poolId) { Data.gachaPity[i] = new GachaPityEntry { poolId = poolId, count = count }; return; }
            Data.gachaPity.Add(new GachaPityEntry { poolId = poolId, count = count });
        }

        internal static bool SpendInMemory(CurrencyKind kind, long amount)
        {
            if (amount < 0 || GetBalance(kind) < amount) return false;
            SetBalance(kind, GetBalance(kind) - amount);
            return true;
        }

        internal static void AddInMemory(CurrencyKind kind, long amount)
        {
            if (amount <= 0) return;
            long next = GetBalance(kind) + amount;
            if (next < GetBalance(kind)) next = long.MaxValue;
            SetBalance(kind, next);
        }

        internal static void GrantWeaponInMemory(string id) { if (!string.IsNullOrEmpty(id) && !Data.ownedWeaponIds.Contains(id)) Data.ownedWeaponIds.Add(id); }
        internal static void GrantCostumeInMemory(string itemId) => GrantCostumeItem(itemId);
        internal static void AddWeaponShardsInMemory(string weaponId, int amount)
        {
            if (string.IsNullOrEmpty(weaponId) || amount <= 0) return;
            for (int i = 0; i < Data.weaponShards.Count; i++)
            {
                if (Data.weaponShards[i].weaponId != weaponId) continue;
                long next = (long)Data.weaponShards[i].count + amount;
                Data.weaponShards[i] = new WeaponShardEntry { weaponId = weaponId, count = (int)Math.Min(int.MaxValue, next) };
                return;
            }
            Data.weaponShards.Add(new WeaponShardEntry { weaponId = weaponId, count = amount });
        }

        public static int GetWeaponShards(string weaponId)
        {
            for (int i = 0; i < Data.weaponShards.Count; i++)
                if (Data.weaponShards[i].weaponId == weaponId) return Data.weaponShards[i].count;
            return 0;
        }

        public static int GetWeaponLevel(string weaponId)
        {
            for (int i = 0; i < Data.weaponUpgrades.Count; i++)
                if (Data.weaponUpgrades[i].weaponId == weaponId) return Mathf.Clamp(Data.weaponUpgrades[i].level, 1, 3);
            return IsWeaponOwned(weaponId) ? 1 : 0;
        }

        public enum WeaponUpgradeResult { Upgraded, MaxLevel, NotOwned, InsufficientShards, InsufficientGold, InvalidData, SaveFailed }

        public static WeaponUpgradeResult TryUpgradeWeapon(WeaponData weapon, EconomyConfig economy)
        {
            if (weapon == null || economy == null || string.IsNullOrEmpty(weapon.WeaponId)) return WeaponUpgradeResult.InvalidData;
            if (!IsWeaponOwned(weapon.WeaponId)) return WeaponUpgradeResult.NotOwned;
            int level = GetWeaponLevel(weapon.WeaponId);
            if (level >= 3) return WeaponUpgradeResult.MaxLevel;
            int tier = Mathf.Clamp((int)weapon.tier, 0, 4);
            int[] shardTable = level == 1 ? economy.weaponStar2ShardCost : economy.weaponStar3ShardCost;
            long[] goldTable = level == 1 ? economy.weaponStar2GoldCost : economy.weaponStar3GoldCost;
            if (shardTable == null || shardTable.Length <= tier || goldTable == null || goldTable.Length <= tier)
                return WeaponUpgradeResult.InvalidData;
            int shardCost = shardTable[tier]; long goldCost = goldTable[tier];
            if (GetWeaponShards(weapon.WeaponId) < shardCost) return WeaponUpgradeResult.InsufficientShards;
            if (Gold < goldCost) return WeaponUpgradeResult.InsufficientGold;

            string snapshot = JsonUtility.ToJson(Data);
            SetWeaponShardsInMemory(weapon.WeaponId, GetWeaponShards(weapon.WeaponId) - shardCost);
            SetBalance(CurrencyKind.Gold, Gold - goldCost);
            SetWeaponLevelInMemory(weapon.WeaponId, level + 1);
            try { SaveNow(); }
            catch (Exception e)
            {
                _data = Normalize(JsonUtility.FromJson<ProfileData>(snapshot));
                Debug.LogError($"[PlayerProfile] Save failed upgrading '{weapon.WeaponId}' - rollback. {e.Message}");
                return WeaponUpgradeResult.SaveFailed;
            }
            WalletChanged?.Invoke(); LoadoutChanged?.Invoke();
            return WeaponUpgradeResult.Upgraded;
        }

        private static void SetWeaponShardsInMemory(string weaponId, int count)
        {
            for (int i = 0; i < Data.weaponShards.Count; i++)
                if (Data.weaponShards[i].weaponId == weaponId)
                { Data.weaponShards[i] = new WeaponShardEntry { weaponId = weaponId, count = Math.Max(0, count) }; return; }
            Data.weaponShards.Add(new WeaponShardEntry { weaponId = weaponId, count = Math.Max(0, count) });
        }

        private static void SetWeaponLevelInMemory(string weaponId, int level)
        {
            for (int i = 0; i < Data.weaponUpgrades.Count; i++)
                if (Data.weaponUpgrades[i].weaponId == weaponId)
                { Data.weaponUpgrades[i] = new WeaponUpgradeEntry { weaponId = weaponId, level = level }; return; }
            Data.weaponUpgrades.Add(new WeaponUpgradeEntry { weaponId = weaponId, level = level });
        }
        internal static void MarkUnseen(string id) { if (!string.IsNullOrEmpty(id) && !Data.unseenItems.Contains(id)) Data.unseenItems.Add(id); }
        public static bool IsUnseen(string id) => Data.unseenItems.Contains(id);
        public static void ClearUnseen(string id) { if (Data.unseenItems.Remove(id)) { SaveNow(); } }

        // Canonical weapons use "weapon.*"; Pro Casual costume/set IDs also contain dots, so a
        // generic Contains('.') classifier would route every new Pro costume to the wrong badge.
        private static bool IsWeaponUnseenId(string id) => !string.IsNullOrEmpty(id)
            && id.StartsWith("weapon.", StringComparison.Ordinal);
        public static bool HasUnseenWeapon() { foreach (var id in Data.unseenItems) if (IsWeaponUnseenId(id)) return true; return false; }
        public static bool HasUnseenCostume() { foreach (var id in Data.unseenItems) if (!IsWeaponUnseenId(id)) return true; return false; }
        public static void ClearUnseenWeapons() => ClearUnseenWhere(IsWeaponUnseenId);
        public static void ClearUnseenCostumes() => ClearUnseenWhere(id => !IsWeaponUnseenId(id));
        private static void ClearUnseenWhere(Func<string, bool> pred)
        {
            int n = Data.unseenItems.RemoveAll(x => pred(x));
            if (n > 0) SaveNow();
        }

        /// Commit 1 giao dich gacha da resolve xong (RNG o GachaService): tru tien + cap thuong + den bu
        /// dupe + cap nhat pity (tat ca trong applyGrants) — 1 save, 1 event. Rollback (snapshot JSON)
        /// neu tien khong du hoac save fail. Nothing-committed-without-debit dam bao boi thu tu nay.
        internal static bool CommitGacha(CurrencyKind spendKind, long spend, System.Action applyGrants)
        {
            string snapshot = JsonUtility.ToJson(Data);
            if (!SpendInMemory(spendKind, spend)) return false;
            applyGrants();
            try { SaveNow(); }
            catch (Exception e)
            {
                _data = Normalize(JsonUtility.FromJson<ProfileData>(snapshot));
                Debug.LogError($"[PlayerProfile] Save fail gacha — rollback. {e.Message}");
                return false;
            }
            WalletChanged?.Invoke();
            LoadoutChanged?.Invoke();
            CostumeChanged?.Invoke();
            return true;
        }

        // ===== Weapon ownership =====

        public static IReadOnlyList<string> OwnedWeaponIds => Data.ownedWeaponIds;

        public static bool IsWeaponOwned(string weaponId) =>
            !string.IsNullOrEmpty(weaponId) && Data.ownedWeaponIds.Contains(weaponId);

        public static void AddOwnedWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || Data.ownedWeaponIds.Contains(weaponId)) return;
            Data.ownedWeaponIds.Add(weaponId);
            SaveNow();
            LoadoutChanged?.Invoke();
        }

        public static int UnlockAllWeaponsForDev(IReadOnlyList<WeaponData> weapons)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (weapons == null) return 0;
            int added = 0;
            for (int i = 0; i < weapons.Count; i++)
            {
                string id = weapons[i] != null ? weapons[i].WeaponId : null;
                if (string.IsNullOrEmpty(id) || Data.ownedWeaponIds.Contains(id)) continue;
                Data.ownedWeaponIds.Add(id); added++;
            }
            if (added > 0) { SaveNow(); LoadoutChanged?.Invoke(); }
            return added;
#else
            return 0;
#endif
        }

        // ===== Equipped weapon slots (0=pistol, 1-2=long) =====

        public static string GetWeaponSlot(int slot) =>
            slot == 0 ? Data.pistol : slot == 1 ? Data.longA : slot == 2 ? Data.longB : "";

        /// id = "" nghia la slot trong (chi hop le cho slot 1-2; slot 0 khong duoc de trong).
        public static void SetWeaponSlot(int slot, string id)
        {
            id ??= "";
            if (slot == 0) { if (id.Length == 0) return; Data.pistol = id; }
            else if (slot == 1) Data.longA = id;
            else if (slot == 2) Data.longB = id;
            else return;
            SaveNow();
            LoadoutChanged?.Invoke();
        }

        /// Chuan hoa loadout theo arsenal that truoc khi trang bi (goi tu LoadoutState.ApplyTo):
        /// - Slot 0 trong -> seed starter = khau 1-tay DAU TIEN trong arsenal (dung rule tu-fill
        ///   co san cua Weapon.Start, khong hard-code id) va cap so huu.
        /// - Id resolve qua legacy alias -> nang cap ve WeaponId chuan (migrate-on-load cu).
        /// - Id khong resolve duoc -> canh bao 1 lan, GIU NGUYEN save, khong thay sung khac.
        /// - Moi sung dang trang bi (resolve duoc) deu phai owned.
        /// Pure data — khong dung den Weapon component nen EditMode test duoc.
        public static void EnsureValidLoadout(IReadOnlyList<WeaponData> arsenal)
        {
            if (arsenal == null || arsenal.Count == 0) return;
            var d = Data;
            bool changed = false;

            if (string.IsNullOrEmpty(d.pistol))
            {
                // Starter = khau 1-tay co CatalogOrder NHO NHAT — khong phu thuoc thu tu list
                // cua caller (Player roster va Loadout card array co the khac thu tu serialize).
                WeaponData starter = null;
                for (int i = 0; i < arsenal.Count; i++)
                {
                    var w = arsenal[i];
                    if (w == null || w.twoHanded || string.IsNullOrEmpty(w.WeaponId)) continue;
                    if (starter == null || w.CatalogOrder < starter.CatalogOrder) starter = w;
                }
                if (starter != null)
                {
                    d.pistol = starter.WeaponId;
                    changed = true;
                }
            }

            changed |= CanonicalizeSlot(ref d.pistol, arsenal, d);
            changed |= CanonicalizeSlot(ref d.longA, arsenal, d);
            changed |= CanonicalizeSlot(ref d.longB, arsenal, d);

            if (changed)
            {
                SaveNow();
                LoadoutChanged?.Invoke();
            }
        }

        private static bool CanonicalizeSlot(ref string id, IReadOnlyList<WeaponData> arsenal, ProfileData d)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var resolved = LoadoutState.Resolve(id, arsenal);
            if (resolved == null)
            {
                if (_warnedUnknownIds.Add(id))
                    Debug.LogWarning($"[PlayerProfile] Weapon id '{id}' khong resolve duoc trong arsenal — giu nguyen save, khong trang bi.");
                return false;
            }

            bool changed = false;
            if (!string.IsNullOrEmpty(resolved.WeaponId) && id != resolved.WeaponId)
            {
                id = resolved.WeaponId;
                changed = true;
            }
            if (!string.IsNullOrEmpty(resolved.WeaponId) && !d.ownedWeaponIds.Contains(resolved.WeaponId))
            {
                d.ownedWeaponIds.Add(resolved.WeaponId);
                changed = true;
            }
            return changed;
        }

        // ===== Costume =====

        public static IReadOnlyList<LoadoutState.PartSel> Parts => Data.equippedParts;

        public static bool IsCostumeOwned(string guid) =>
            !string.IsNullOrEmpty(guid) && Data.ownedCostumeGuids.Contains(guid);

        public static void AddOwnedCostume(string guid)
        {
            if (string.IsNullOrEmpty(guid) || Data.ownedCostumeGuids.Contains(guid)) return;
            Data.ownedCostumeGuids.Add(guid);
            SaveNow();
            CostumeChanged?.Invoke();
        }

        // ===== Body composite (Slice 4.2): mau + bien the tai =====

        public static string BodyColor => string.IsNullOrEmpty(Data.bodyColor) ? "White" : Data.bodyColor;
        public static string BodyEar => string.IsNullOrEmpty(Data.bodyEar) ? "Normal" : Data.bodyEar;
        public static bool IsBodyColorOwned(string color) => color == "White" || Data.ownedBodyColors.Contains(color);
        public static bool IsBodyEarOwned(string ear) => ear == "Normal" || Data.ownedBodyEars.Contains(ear);

        public static void AddOwnedBodyColor(string color)
        {
            if (string.IsNullOrEmpty(color) || color == "White" || Data.ownedBodyColors.Contains(color)) return;
            Data.ownedBodyColors.Add(color); SaveNow(); CostumeChanged?.Invoke();
        }

        public static void AddOwnedBodyEar(string ear)
        {
            if (string.IsNullOrEmpty(ear) || ear == "Normal" || Data.ownedBodyEars.Contains(ear)) return;
            Data.ownedBodyEars.Add(ear); SaveNow(); CostumeChanged?.Invoke();
        }

        /// Doi mau body: validate mau hop le + so huu + resolve duoc mesh body & head cho mau+tai
        /// hien tai. 1 save, 1 event, rollback. Body va head luon cung mau (khong lech mau).
        public static CostumeEquipResult TryEquipBodyColor(ModularCostumeCatalog catalog, string color)
        {
            if (catalog == null || !ModularCostumeCatalog.IsValidBodyColor(color)) return CostumeEquipResult.InvalidPart;
            if (!IsBodyColorOwned(color)) return CostumeEquipResult.NotOwned;
            if (!BodyMeshesResolve(catalog, color, BodyEar)) return CostumeEquipResult.InvalidPart;
            if (BodyColor == color) return CostumeEquipResult.AlreadyEquipped;

            string prev = Data.bodyColor; Data.bodyColor = color;
            if (!Commit()) { Data.bodyColor = prev; return CostumeEquipResult.SaveFailed; }
            CostumeChanged?.Invoke(); return CostumeEquipResult.Equipped;
        }

        /// Doi bien the tai (Normal/Elf): giu nguyen mau, chi doi mesh head.
        public static CostumeEquipResult TryEquipBodyEar(ModularCostumeCatalog catalog, string ear)
        {
            if (catalog == null || !ModularCostumeCatalog.IsValidBodyEar(ear)) return CostumeEquipResult.InvalidPart;
            if (!IsBodyEarOwned(ear)) return CostumeEquipResult.NotOwned;
            if (!BodyMeshesResolve(catalog, BodyColor, ear)) return CostumeEquipResult.InvalidPart;
            if (BodyEar == ear) return CostumeEquipResult.AlreadyEquipped;

            string prev = Data.bodyEar; Data.bodyEar = ear;
            if (!Commit()) { Data.bodyEar = prev; return CostumeEquipResult.SaveFailed; }
            CostumeChanged?.Invoke(); return CostumeEquipResult.Equipped;
        }

        private static bool BodyMeshesResolve(ModularCostumeCatalog catalog, string color, string ear)
        {
            var body = catalog.FindPartByName(ModularCostumeCatalog.BodySlot, ModularCostumeCatalog.BodyMeshName(color));
            var head = catalog.FindPartByName(ModularCostumeCatalog.BodySlot, ModularCostumeCatalog.BodyHeadName(color, ear));
            return body.HasValue && body.Value.skinnedMesh != null && head.HasValue && head.Value.skinnedMesh != null;
        }

        private static bool Commit()
        {
            try { SaveNow(); return true; }
            catch (Exception e) { Debug.LogError($"[PlayerProfile] Save that bai — rollback. {e.Message}"); return false; }
        }

        public static string GetPart(string slot)
        {
            var parts = Data.equippedParts;
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].slot == slot) return parts[i].guid;
            return null;
        }

        /// guid = null/"" -> bo chon part slot do (ve default cua prefab).
        /// Raw setter KHONG validate catalog/ownership — UI phai dung TryEquipCostume/TryClearCostumeSlot.
        public static void SetPart(string slot, string guid)
        {
            if (string.IsNullOrEmpty(slot)) return;
            if (!SetPartInMemory(slot, guid)) return;
            SaveNow();
            CostumeChanged?.Invoke();
        }

        /// True neu co thay doi thuc su (dung cho batch: gom nhieu thay doi vao 1 save/event).
        private static bool SetPartInMemory(string slot, string guid)
        {
            var parts = Data.equippedParts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].slot != slot) continue;
                if (string.IsNullOrEmpty(guid)) { parts.RemoveAt(i); return true; }
                if (parts[i].guid == guid) return false;
                parts[i] = new LoadoutState.PartSel { slot = slot, guid = guid };
                return true;
            }
            if (string.IsNullOrEmpty(guid)) return false;
            parts.Add(new LoadoutState.PartSel { slot = slot, guid = guid });
            return true;
        }

        // ===== Costume equip transactions (Slice 4) =====

        public enum CostumeEquipResult
        {
            Equipped, AlreadyEquipped, NotOwned, InvalidPart, InvalidSlot, CannotClearBaseBody, SaveFailed
        }

        /// Trang bi 1 costume part theo GUID. Slot LUON resolve tu catalog (caller khong duoc
        /// tu chi dinh slot -> khong the equip sai slot). Validate: ton tai trong catalog,
        /// da so huu. Thanh cong = doi dung 1 slot, luu 1 lan, CostumeChanged sau khi commit.
        public static CostumeEquipResult TryEquipCostume(ModularCostumeCatalog catalog, string guid)
        {
            if (catalog == null || string.IsNullOrEmpty(guid)) return CostumeEquipResult.InvalidPart;
            if (!TryResolvePart(catalog, guid, out string slotName)) return CostumeEquipResult.InvalidPart;
            if (catalog.IsTechnicalCasualSlot(slotName)) return CostumeEquipResult.InvalidSlot;
            if (!Data.ownedCostumeGuids.Contains(guid)) return CostumeEquipResult.NotOwned;
            if (GetPart(slotName) == guid) return CostumeEquipResult.AlreadyEquipped;

            string previous = GetPart(slotName);
            SetPartInMemory(slotName, guid);
            try { SaveNow(); }
            catch (Exception e)
            {
                SetPartInMemory(slotName, previous);
                Debug.LogError($"[PlayerProfile] Luu profile that bai khi equip costume '{guid}' — rollback. {e.Message}");
                return CostumeEquipResult.SaveFailed;
            }
            CostumeChanged?.Invoke();
            return CostumeEquipResult.Equipped;
        }

        /// Bo trang bi 1 slot. Slot base body (isBaseBody) KHONG duoc clear. Slot BAT BUOC
        /// (co default trong catalog.defaults: Hair/Chest/Legs/Feet — invariant "khong duoc
        /// tran truong") thi clear = TRO VE part mac dinh thay vi de trong. Slot optional
        /// clear ve default prefab (underlayer an toan).
        public static CostumeEquipResult TryClearCostumeSlot(ModularCostumeCatalog catalog, string slotName)
        {
            if (catalog == null || string.IsNullOrEmpty(slotName)) return CostumeEquipResult.InvalidSlot;
            var slot = catalog.GetSlot(slotName);
            if (slot == null) return CostumeEquipResult.InvalidSlot;
            if (slot.isBaseBody) return CostumeEquipResult.CannotClearBaseBody;

            string mandatoryDefault = catalog.defaults != null ? catalog.defaults.GetEquippedGuid(slot.slot) : null;
            string target = string.IsNullOrEmpty(mandatoryDefault) ? "" : mandatoryDefault;
            if (GetPart(slot.slot) == (target.Length == 0 ? null : target)
                || (target.Length == 0 && string.IsNullOrEmpty(GetPart(slot.slot))))
                return CostumeEquipResult.AlreadyEquipped;

            string previous = GetPart(slot.slot);
            SetPartInMemory(slot.slot, target);
            try { SaveNow(); }
            catch (Exception e)
            {
                SetPartInMemory(slot.slot, previous);
                Debug.LogError($"[PlayerProfile] Luu profile that bai khi clear slot '{slotName}' — rollback. {e.Message}");
                return CostumeEquipResult.SaveFailed;
            }
            CostumeChanged?.Invoke();
            return CostumeEquipResult.Equipped;
        }

        /// Idempotent repair (fresh profile + migration): cap ownership mac dinh con thieu va
        /// sua cac slot BAT BUOC dang trong/hong ve part mac dinh. GIU nguyen moi ownership da
        /// mua va moi optional item hop le. Khong doi gi -> khong save, khong event.
        public static bool EnsureValidCostumeLoadout(ModularCostumeCatalog catalog)
        {
            if (catalog == null) return false;
            if (!catalog.compositeBody) return EnsureValidCasualLoadout(catalog);
            if (catalog.defaults == null || !catalog.defaults.IsAuthored) return false;
            if (!ValidateDefaults(catalog)) return false; // defaults hong -> da log ro, khong sua bay

            MigrateRawBodyEquip(catalog); // 4.1 profile co the co Body GUID trong equippedParts

            var d = Data;
            bool changed = false;

            foreach (var guid in catalog.defaults.ownedGuids)
                if (!string.IsNullOrEmpty(guid) && !d.ownedCostumeGuids.Contains(guid))
                {
                    d.ownedCostumeGuids.Add(guid);
                    changed = true;
                }

            foreach (var def in catalog.defaults.equipped)
            {
                string current = GetPart(def.slot);
                bool valid = !string.IsNullOrEmpty(current) && TryResolvePart(catalog, current, out string s) && s == def.slot;
                if (!valid)
                    changed |= SetPartInMemory(def.slot, def.guid);
            }

            // Body composite: mac dinh mau/tai neu trong hoac khong resolve duoc mesh.
            string defColor = string.IsNullOrEmpty(catalog.defaults.defaultBodyColor) ? "White" : catalog.defaults.defaultBodyColor;
            string defEar = string.IsNullOrEmpty(catalog.defaults.defaultBodyEar) ? "Normal" : catalog.defaults.defaultBodyEar;
            if (!ModularCostumeCatalog.IsValidBodyColor(d.bodyColor) || !BodyMeshesResolve(catalog, d.bodyColor, "Normal"))
            { d.bodyColor = defColor; changed = true; }
            if (!ModularCostumeCatalog.IsValidBodyEar(d.bodyEar) || !BodyMeshesResolve(catalog, d.bodyColor, d.bodyEar))
            { d.bodyEar = defEar; changed = true; }

            if (!changed) return false;
            try { SaveNow(); }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerProfile] Luu profile that bai khi ensure costume defaults. {e.Message}");
                return false;
            }
            CostumeChanged?.Invoke();
            return true;
        }

        /// Casual (compositeBody=false): identity is the stable itemId (stored in PartSel.guid as an
        /// opaque key). Seed required slots from slotDefinitions.defaultItemId and own them; seed an
        /// optional slot's default (e.g. Feet shoes) only once — once owned it is never re-seeded, so a
        /// later clear stays cleared. Idempotent: a valid, owned outfit produces no change/save/event.
        private static bool EnsureValidCasualLoadout(ModularCostumeCatalog catalog)
        {
            var d = Data;
            bool changed = false;

            // Purge stale entries (Fantasy leftovers from legacy migration / moved assets): any equipped
            // key that doesn't resolve to a Casual part is dropped, so migrating profiles carry no
            // non-rendering junk and optional defaults (Feet) can re-seed on a now-empty slot.
            for (int i = d.equippedParts.Count - 1; i >= 0; i--)
            {
                bool resolves = catalog.TryFindByItemId(d.equippedParts[i].guid, out string slot, out _);
                if (!resolves || catalog.IsTechnicalCasualSlot(slot))
                { d.equippedParts.RemoveAt(i); changed = true; }
            }

            // Free-Casual -> Pro-Casual migration. Both generations use stable "casual.*" keys,
            // but their meshes and slot model are different. Remove only stale Casual ownership;
            // keep opaque legacy/Fantasy GUIDs until the final dependency-removal phase.
            for (int i = d.ownedCostumeGuids.Count - 1; i >= 0; i--)
            {
                string id = d.ownedCostumeGuids[i];
                if (string.IsNullOrEmpty(id) || !id.StartsWith("casual.", StringComparison.Ordinal)) continue;
                if (catalog.TryFindByItemId(id, out string slot, out _) && !catalog.IsTechnicalCasualSlot(slot)) continue;
                d.ownedCostumeGuids.RemoveAt(i);
                changed = true;
            }

            foreach (var def in catalog.slotDefinitions)
            {
                if (!def.required) continue;
                string current = GetPart(def.id);
                bool valid = !string.IsNullOrEmpty(current)
                             && catalog.TryFindByItemId(current, out string s, out _) && s == def.id;
                if (!valid)
                {
                    string fallback = def.defaultItemId;
                    if (string.IsNullOrEmpty(fallback) || !catalog.TryFindByItemId(fallback, out _, out _))
                    {
                        var slot = catalog.GetSlot(def.id);
                        fallback = slot != null && slot.parts.Count > 0 ? slot.parts[0].itemId : null;
                    }
                    if (string.IsNullOrEmpty(fallback)) continue;
                    changed |= SetPartInMemory(def.id, fallback);
                }
                string eq = GetPart(def.id);
                if (!string.IsNullOrEmpty(eq) && !d.ownedCostumeGuids.Contains(eq))
                { d.ownedCostumeGuids.Add(eq); changed = true; }
            }

            // Optional slot with an authored default (Feet): seed once on a fresh profile only.
            foreach (var def in catalog.slotDefinitions)
            {
                if (def.required || string.IsNullOrEmpty(def.defaultItemId)) continue;
                if (!catalog.TryFindByItemId(def.defaultItemId, out _, out _)) continue;
                if (string.IsNullOrEmpty(GetPart(def.id)) && !d.ownedCostumeGuids.Contains(def.defaultItemId))
                {
                    d.ownedCostumeGuids.Add(def.defaultItemId);
                    changed |= SetPartInMemory(def.id, def.defaultItemId);
                }
            }

            if (!changed) return false;
            try { SaveNow(); }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerProfile] Luu profile that bai khi ensure Casual costume. {e.Message}");
                return false;
            }
            CostumeChanged?.Invoke();
            return true;
        }

        // Resolve a slot's default itemId: authored defaultItemId if valid, else the first catalog part
        // (required slots must always resolve). Returns null for an optional slot with no default.
        private static string CasualSlotDefault(ModularCostumeCatalog catalog, ModularCostumeCatalog.SlotDefinition def)
        {
            string id = def.defaultItemId;
            if (!string.IsNullOrEmpty(id) && catalog.TryFindByItemId(id, out _, out _)) return id;
            if (!def.required) return null;
            var slot = catalog.GetSlot(def.id);
            return slot != null && slot.parts.Count > 0 ? slot.parts[0].itemId : null;
        }

        /// Casual reset: equipped := authored starter (required slots + optional defaults like Feet),
        /// every other optional slot cleared. Owns the starter items (free). Wallet, weapons, upgrades,
        /// pity and every non-costume field are untouched. 1 save, 1 event, rollback.
        private static CostumeEquipResult ResetCasualOutfit(ModularCostumeCatalog catalog)
        {
            var d = Data;
            var backup = new List<LoadoutState.PartSel>(d.equippedParts);
            d.equippedParts.Clear();
            foreach (var def in catalog.slotDefinitions)
            {
                string id = CasualSlotDefault(catalog, def);
                if (string.IsNullOrEmpty(id)) continue; // optional slot with no default -> cleared
                d.equippedParts.Add(new LoadoutState.PartSel { slot = def.id, guid = id });
                if (!d.ownedCostumeGuids.Contains(id)) d.ownedCostumeGuids.Add(id);
            }
            try { SaveNow(); }
            catch (Exception e)
            {
                d.equippedParts = backup;
                Debug.LogError($"[PlayerProfile] Save fail reset Casual outfit — rollback. {e.Message}");
                return CostumeEquipResult.SaveFailed;
            }
            CostumeChanged?.Invoke();
            return CostumeEquipResult.Equipped;
        }

        /// Casual randomize: replace the whole outfit with the given owned items, guaranteeing every
        /// required slot resolves (falls back to that slot's default). Validates ownership + slot first.
        /// 1 save, 1 event, rollback. Non-costume progression untouched.
        public static CostumeEquipResult TrySetCasualOutfit(ModularCostumeCatalog catalog, IReadOnlyList<LoadoutState.PartSel> outfit)
        {
            if (catalog == null || catalog.compositeBody) return CostumeEquipResult.InvalidPart;
            if (outfit != null)
                for (int i = 0; i < outfit.Count; i++)
                {
                    if (!TryResolvePart(catalog, outfit[i].guid, out string sn) || sn != outfit[i].slot
                        || catalog.IsTechnicalCasualSlot(sn))
                        return CostumeEquipResult.InvalidPart;
                    if (!Data.ownedCostumeGuids.Contains(outfit[i].guid)) return CostumeEquipResult.NotOwned;
                }

            var d = Data;
            var backup = new List<LoadoutState.PartSel>(d.equippedParts);
            d.equippedParts.Clear();
            if (outfit != null) d.equippedParts.AddRange(outfit);
            foreach (var def in catalog.slotDefinitions)
            {
                if (!def.required || d.equippedParts.Exists(x => x.slot == def.id)) continue;
                string id = CasualSlotDefault(catalog, def);
                if (string.IsNullOrEmpty(id)) continue;
                d.equippedParts.Add(new LoadoutState.PartSel { slot = def.id, guid = id });
                if (!d.ownedCostumeGuids.Contains(id)) d.ownedCostumeGuids.Add(id);
            }
            try { SaveNow(); }
            catch (Exception e)
            {
                d.equippedParts = backup;
                Debug.LogError($"[PlayerProfile] Save fail set Casual outfit — rollback. {e.Message}");
                return CostumeEquipResult.SaveFailed;
            }
            CostumeChanged?.Invoke();
            return CostumeEquipResult.Equipped;
        }

        /// Runtime "MAC DINH": giu NGUYEN toan bo ownership (mua/dev-unlock), equipped := dung bo
        /// mac dinh (slot bat buoc mac default, slot optional ve trong). 1 save, 1 event, rollback.
        public static CostumeEquipResult TryResetOutfitToDefaults(ModularCostumeCatalog catalog)
        {
            if (catalog == null) return CostumeEquipResult.InvalidPart;
            if (!catalog.compositeBody) return ResetCasualOutfit(catalog);
            if (catalog.defaults == null || !catalog.defaults.IsAuthored)
                return CostumeEquipResult.InvalidPart;
            if (!ValidateDefaults(catalog)) return CostumeEquipResult.InvalidPart;

            var d = Data;
            var target = new List<LoadoutState.PartSel>();
            foreach (var def in catalog.defaults.equipped)
                target.Add(new LoadoutState.PartSel { slot = def.slot, guid = def.guid });
            string defColor = string.IsNullOrEmpty(catalog.defaults.defaultBodyColor) ? "White" : catalog.defaults.defaultBodyColor;
            string defEar = string.IsNullOrEmpty(catalog.defaults.defaultBodyEar) ? "Normal" : catalog.defaults.defaultBodyEar;

            bool same = d.equippedParts.Count == target.Count && d.bodyColor == defColor && d.bodyEar == defEar;
            if (same)
                foreach (var t in target)
                    if (GetPart(t.slot) != t.guid) { same = false; break; }
            if (same) return CostumeEquipResult.AlreadyEquipped;

            var backup = new List<LoadoutState.PartSel>(d.equippedParts);
            string bcBak = d.bodyColor, beBak = d.bodyEar;
            d.equippedParts.Clear();
            d.equippedParts.AddRange(target); // optional slots (Feet/Beard/...) bi bo -> ve Khong mang
            d.bodyColor = defColor; d.bodyEar = defEar;
            try { SaveNow(); }
            catch (Exception e)
            {
                d.equippedParts = backup; d.bodyColor = bcBak; d.bodyEar = beBak;
                Debug.LogError($"[PlayerProfile] Luu profile that bai khi reset outfit — rollback. {e.Message}");
                return CostumeEquipResult.SaveFailed;
            }
            CostumeChanged?.Invoke();
            return CostumeEquipResult.Equipped;
        }

        private static readonly HashSet<string> _warnedDefaultIssues = new();

        /// Defaults phai: guid ton tai trong catalog, dung slot, nam trong ownedGuids, part co
        /// skinned binding. Hong -> log 1 lan, tra false (KHONG fallback lung tung).
        private static bool ValidateDefaults(ModularCostumeCatalog catalog)
        {
            bool ok = true;
            foreach (var def in catalog.defaults.equipped)
            {
                string issue = null;
                if (string.IsNullOrEmpty(def.guid)) issue = "guid rong";
                else if (!TryResolvePart(catalog, def.guid, out string slot)) issue = "khong co trong catalog";
                else if (slot != def.slot) issue = $"guid thuoc slot '{slot}' chu khong phai '{def.slot}'";
                else if (!catalog.defaults.ownedGuids.Contains(def.guid)) issue = "khong nam trong ownedGuids mac dinh";
                if (issue != null)
                {
                    ok = false;
                    if (_warnedDefaultIssues.Add(def.slot + def.guid))
                        Debug.LogError($"[PlayerProfile] Costume default hong ({def.slot}): {issue} — chay lai 'Author Costume Defaults'.");
                }
            }
            return ok;
        }

        /// Trang bi nguyen bo outfit trong MOT giao dich: validate het truoc, apply in-memory,
        /// luu 1 lan, 1 event. Entry khong hop le/khong so huu -> ca batch bi tu choi (khong ap 1 nua).
        public static CostumeEquipResult TryEquipOutfit(ModularCostumeCatalog catalog, IReadOnlyList<LoadoutState.PartSel> outfit)
        {
            if (catalog == null || outfit == null || outfit.Count == 0) return CostumeEquipResult.InvalidPart;
            for (int i = 0; i < outfit.Count; i++)
            {
                if (!TryResolvePart(catalog, outfit[i].guid, out string slotName)) return CostumeEquipResult.InvalidPart;
                if (slotName != outfit[i].slot) return CostumeEquipResult.InvalidSlot;
                if (!Data.ownedCostumeGuids.Contains(outfit[i].guid)) return CostumeEquipResult.NotOwned;
            }

            var backup = new List<LoadoutState.PartSel>(Data.equippedParts);
            bool changed = false;
            for (int i = 0; i < outfit.Count; i++)
                changed |= SetPartInMemory(outfit[i].slot, outfit[i].guid);
            if (!changed) return CostumeEquipResult.AlreadyEquipped;

            try { SaveNow(); }
            catch (Exception e)
            {
                Data.equippedParts = backup;
                Debug.LogError($"[PlayerProfile] Luu profile that bai khi equip outfit — rollback. {e.Message}");
                return CostumeEquipResult.SaveFailed;
            }
            CostumeChanged?.Invoke();
            return CostumeEquipResult.Equipped;
        }

        /// Trang bi CA look (outfit non-Body + Body color/ear) trong MOT giao dich (Randomize).
        /// Validate het truoc, 1 save, 1 event, rollback.
        public static CostumeEquipResult TryEquipLook(ModularCostumeCatalog catalog,
            IReadOnlyList<LoadoutState.PartSel> outfit, string color, string ear)
        {
            if (catalog == null) return CostumeEquipResult.InvalidPart;
            if (!ModularCostumeCatalog.IsValidBodyColor(color) || !IsBodyColorOwned(color)) return CostumeEquipResult.NotOwned;
            if (!ModularCostumeCatalog.IsValidBodyEar(ear) || !IsBodyEarOwned(ear)) return CostumeEquipResult.NotOwned;
            if (!BodyMeshesResolve(catalog, color, ear)) return CostumeEquipResult.InvalidPart;
            if (outfit != null)
                for (int i = 0; i < outfit.Count; i++)
                {
                    if (!TryResolvePart(catalog, outfit[i].guid, out string sn)) return CostumeEquipResult.InvalidPart;
                    if (sn != outfit[i].slot) return CostumeEquipResult.InvalidSlot;
                    if (!Data.ownedCostumeGuids.Contains(outfit[i].guid)) return CostumeEquipResult.NotOwned;
                }

            var d = Data;
            var backup = new List<LoadoutState.PartSel>(d.equippedParts);
            string bcBak = d.bodyColor, beBak = d.bodyEar;
            // Rebuild equipped: chi giu essential mac dinh + cac slot trong outfit; optional khong co -> Khong mang.
            d.equippedParts.Clear();
            if (outfit != null) foreach (var o in outfit) d.equippedParts.Add(o);
            // Dam bao essential luon co (randomize outfit da gom essential owned; nhung neu thieu, EnsureValid se sua)
            d.bodyColor = color; d.bodyEar = ear;
            try { SaveNow(); }
            catch (Exception e)
            {
                d.equippedParts = backup; d.bodyColor = bcBak; d.bodyEar = beBak;
                Debug.LogError($"[PlayerProfile] Save that bai khi equip look — rollback. {e.Message}");
                return CostumeEquipResult.SaveFailed;
            }
            CostumeChanged?.Invoke();
            return CostumeEquipResult.Equipped;
        }

        /// DEV-ONLY: mo khoa toan bo part hop le trong catalog (14 wardrobe slot; held-item
        /// categories khong nam trong catalog nen tu dong bi loai). 1 batch, 1 save, 1 event.
        /// Idempotent — chi them guid con thieu. Tra ve so entry moi duoc them.
        public static int UnlockAllCostumes(ModularCostumeCatalog catalog)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (catalog == null) return 0;
            var owned = Data.ownedCostumeGuids;
            var backupCount = owned.Count;
            int added = 0;
            // Casual/Pro-Casual identity = stable itemId because every part shares one FBX GUID.
            // Fantasy identity = asset GUID and Body remains the special composite presentation.
            for (int s = 0; s < catalog.slots.Count; s++)
            {
                if (catalog.compositeBody && catalog.slots[s].slot == ModularCostumeCatalog.BodySlot) continue;
                if (catalog.IsTechnicalCasualSlot(catalog.slots[s].slot)) continue;
                var parts = catalog.slots[s].parts;
                for (int p = 0; p < parts.Count; p++)
                {
                    string key = catalog.compositeBody ? parts[p].guid : parts[p].itemId;
                    if (string.IsNullOrEmpty(key) || owned.Contains(key)) continue;
                    owned.Add(key);
                    added++;
                }
            }
            if (catalog.compositeBody)
            {
                foreach (var c in ModularCostumeCatalog.BodyColors)
                    if (c != "White" && !Data.ownedBodyColors.Contains(c)) { Data.ownedBodyColors.Add(c); added++; }
                foreach (var e2 in ModularCostumeCatalog.BodyEars)
                    if (e2 != "Normal" && !Data.ownedBodyEars.Contains(e2)) { Data.ownedBodyEars.Add(e2); added++; }
            }
            if (added == 0) return 0;
            try { SaveNow(); }
            catch (Exception e)
            {
                owned.RemoveRange(backupCount, Math.Max(0, owned.Count - backupCount));
                Debug.LogError($"[PlayerProfile] Luu profile that bai khi unlock all costume — rollback. {e.Message}");
                return 0;
            }
            CostumeChanged?.Invoke();
            return added;
#else
            Debug.LogWarning("[PlayerProfile] UnlockAllCostumes chi chay trong Editor/dev build.");
            return 0;
#endif
        }

        /// DEV-ONLY: dua TIEN TRINH costume ve dung design-default — ownership := chinh xac bo
        /// mac dinh (xoa ca dev-unlock/mua thu), equipped := outfit mac dinh. Vi tien, sung,
        /// loadout va moi field khac GIU NGUYEN. 1 save, 1 event.
        public static void ResetCostumeProgressForDev(ModularCostumeCatalog catalog)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (catalog == null)
            {
                Debug.LogError("[PlayerProfile] Costume catalog missing.");
                return;
            }
            if (!catalog.compositeBody)
            {
                var casual = Data;
                casual.ownedCostumeGuids.Clear();
                casual.equippedParts.Clear();
                casual.ownedBodyColors.Clear();
                casual.ownedBodyEars.Clear();
                casual.bodyColor = "";
                casual.bodyEar = "";
                foreach (var def in catalog.slotDefinitions)
                {
                    string id = CasualSlotDefault(catalog, def);
                    if (string.IsNullOrEmpty(id)) continue;
                    casual.ownedCostumeGuids.Add(id);
                    casual.equippedParts.Add(new LoadoutState.PartSel { slot = def.id, guid = id });
                }
                SaveNow();
                CostumeChanged?.Invoke();
                Debug.Log($"[PlayerProfile] Casual costume reset: owned={casual.ownedCostumeGuids.Count}, equipped={casual.equippedParts.Count}. Wallet/weapons preserved.");
                return;
            }
            if (catalog.defaults == null || !catalog.defaults.IsAuthored)
            {
                Debug.LogError("[PlayerProfile] Catalog defaults chua duoc author — chay 'Author Costume Defaults' truoc.");
                return;
            }
            var d = Data;
            d.ownedCostumeGuids.Clear();
            foreach (var g in catalog.defaults.ownedGuids)
                if (!string.IsNullOrEmpty(g) && !d.ownedCostumeGuids.Contains(g))
                    d.ownedCostumeGuids.Add(g);
            d.ownedBodyColors.Clear();   // chi con White (implicit)
            d.ownedBodyEars.Clear();     // chi con Normal (implicit)
            d.equippedParts.Clear();     // optional slots (Feet/Beard/...) ve Khong mang
            foreach (var def in catalog.defaults.equipped)
                d.equippedParts.Add(new LoadoutState.PartSel { slot = def.slot, guid = def.guid });
            d.bodyColor = string.IsNullOrEmpty(catalog.defaults.defaultBodyColor) ? "White" : catalog.defaults.defaultBodyColor;
            d.bodyEar = string.IsNullOrEmpty(catalog.defaults.defaultBodyEar) ? "Normal" : catalog.defaults.defaultBodyEar;
            SaveNow();
            CostumeChanged?.Invoke();
            Debug.Log($"[PlayerProfile] Costume progress ve design default: owned={d.ownedCostumeGuids.Count} guid + White/Normal, equipped={d.equippedParts.Count} slot. Vi/sung giu nguyen.");
#else
            Debug.LogWarning("[PlayerProfile] ResetCostumeProgressForDev chi chay trong Editor/dev build.");
#endif
        }

        /// Migration idempotent (Slice 4.2): profile 4.1 co the co Body GUID trong equippedParts
        /// (khi Body con hien 132 card). Rut ra -> set bodyColor/bodyEar, va map ownership Body_&lt;Color&gt;_1
        /// -> ownedBodyColors. Cac mesh assembly Body trong ownership bi bo (khong player-facing).
        private static void MigrateRawBodyEquip(ModularCostumeCatalog catalog)
        {
            var d = Data;
            var bodySlot = catalog.GetSlot(ModularCostumeCatalog.BodySlot);
            if (bodySlot == null) return;

            // equippedParts: rut entry slot "Body" (neu co) -> suy ra mau/tai tu ten mesh.
            for (int i = d.equippedParts.Count - 1; i >= 0; i--)
            {
                if (d.equippedParts[i].slot != ModularCostumeCatalog.BodySlot) continue;
                string g = d.equippedParts[i].guid;
                foreach (var p in bodySlot.parts)
                {
                    if (p.guid != g) continue;
                    foreach (var col in ModularCostumeCatalog.BodyColors)
                    {
                        if (p.name == ModularCostumeCatalog.BodyMeshName(col)) d.bodyColor = col;
                        else if (p.name == $"Body_{col}_Head_1") { d.bodyColor = col; d.bodyEar = "Normal"; }
                        else if (p.name == $"Body_{col}_Head_2") { d.bodyColor = col; d.bodyEar = "Elf"; }
                    }
                    break;
                }
                d.equippedParts.RemoveAt(i);
            }

            // ownership: map Body_<Color>_1 owned -> ownedBodyColors; bo cac guid Body khoi ownedCostumeGuids.
            for (int i = d.ownedCostumeGuids.Count - 1; i >= 0; i--)
            {
                string g = d.ownedCostumeGuids[i];
                foreach (var p in bodySlot.parts)
                {
                    if (p.guid != g) continue;
                    foreach (var col in ModularCostumeCatalog.BodyColors)
                        if (p.name == ModularCostumeCatalog.BodyMeshName(col) && col != "White" && !d.ownedBodyColors.Contains(col))
                            d.ownedBodyColors.Add(col);
                    d.ownedCostumeGuids.RemoveAt(i);
                    break;
                }
            }
        }

        // Resolve a stored costume key to its slot. Casual keys are stable itemIds (all Casual parts
        // share one fbx GUID, so GUID is ambiguous); Fantasy keys are asset GUIDs. itemId and 32-hex
        // GUIDs never collide, so try itemId first, then fall back to GUID (Fantasy).
        private static bool TryResolvePart(ModularCostumeCatalog catalog, string key, out string slotName)
        {
            slotName = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (catalog.TryFindByItemId(key, out slotName, out _)) return true;
            var slots = catalog.slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var parts = slots[i].parts;
                for (int j = 0; j < parts.Count; j++)
                {
                    if (parts[j].guid != key) continue;
                    slotName = slots[i].slot;
                    return true;
                }
            }
            return false;
        }

        // ===== Dev reset =====

        /// Xoa profile de test tu dau (Editor/dev build). Key legacy giu nguyen — lan load ke tiep
        /// se migrate lai tu chung (dung de test migration lap lai).
        public static void ResetForDev()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var storage = Storage;
            storage.Delete(SaveKey);
            storage.Flush();
            ResetCacheForTests();
            Debug.Log("[PlayerProfile] Da xoa profile (dev reset). Legacy keys giu nguyen.");
#else
            Debug.LogWarning("[PlayerProfile] ResetForDev chi chay trong Editor/dev build.");
#endif
        }

        // ===== Load/normalize/migration =====

        /// Sua du lieu load ve trang thai an toan: list null -> rong, owned trung lap -> bo,
        /// tien am -> 0 (canh bao), version <= 0 -> version hien tai.
        private static ProfileData Normalize(ProfileData d)
        {
            d.ownedWeaponIds = DedupeNonEmpty(d.ownedWeaponIds);
            d.ownedCostumeGuids = DedupeNonEmpty(d.ownedCostumeGuids);
            d.equippedParts ??= new List<LoadoutState.PartSel>();
            d.weaponUpgrades ??= new List<WeaponUpgradeEntry>();
            d.weaponShards ??= new List<WeaponShardEntry>();
            d.ownedBodyColors = DedupeNonEmpty(d.ownedBodyColors);
            d.ownedBodyEars = DedupeNonEmpty(d.ownedBodyEars);
            d.gachaPity ??= new List<GachaPityEntry>();
            d.unseenItems = DedupeNonEmpty(d.unseenItems);
            // Campaign lists: a profile saved before the campaign existed has these as null.
            // Dedupe matters - a duplicated claim ID would be harmless, but a duplicated completion
            // would misreport progress counts to Pass missions.
            d.completedLevelIds = DedupeNonEmpty(d.completedLevelIds);
            d.claimedFirstClearIds = DedupeNonEmpty(d.claimedFirstClearIds);
            d.lastSelectedLevelId ??= "";
            d.missionProgress ??= new List<MissionProgressEntry>();
            d.claimedMissionIds = DedupeNonEmpty(d.claimedMissionIds);
            if (d.passXp < 0) d.passXp = 0;
            d.bodyColor ??= "";
            d.bodyEar ??= "";
            d.pistol ??= "";
            d.longA ??= "";
            d.longB ??= "";

            if (d.coin < 0 || d.gold < 0 || d.gem < 0)
            {
                Debug.LogWarning($"[PlayerProfile] So du am trong save (coin={d.coin}, gold={d.gold}, gem={d.gem}) — clamp ve 0.");
                d.coin = Math.Max(0, d.coin);
                d.gold = Math.Max(0, d.gold);
                d.gem = Math.Max(0, d.gem);
            }

            if (d.version <= 0) d.version = SchemaVersion;
            else if (d.version > SchemaVersion)
                Debug.LogWarning($"[PlayerProfile] Profile version {d.version} moi hon build ({SchemaVersion}) — doc theo schema hien tai.");
            // version < SchemaVersion: chua co buoc upgrade nao (v1 la schema dau tien).
            d.version = Math.Max(d.version, SchemaVersion);
            return d;
        }

        private static List<string> DedupeNonEmpty(List<string> list)
        {
            var result = new List<string>(list?.Count ?? 0);
            if (list == null) return result;
            for (int i = 0; i < list.Count; i++)
                if (!string.IsNullOrEmpty(list[i]) && !result.Contains(list[i]))
                    result.Add(list[i]);
            return result;
        }

        // Mirror cua LoadoutState.SaveData cu — chi de parse "zw.loadout", khong dung cho ghi.
        [Serializable]
        private class LegacyLoadout
        {
            public string pistol = "";
            public string longA = "";
            public string longB = "";
            public List<LoadoutState.PartSel> parts = new();
        }

        /// Import du lieu cu thanh profile moi. Weapon id copy NGUYEN VAN (co the la ten asset cu) —
        /// EnsureValidLoadout se canonical hoa o lan ApplyTo dau tien vi luc do moi co arsenal.
        /// Costume: chi giu entry slot+guid hop le; guid dang trang bi duoc seed lam owned de giu
        /// nguyen ngoai hinh da luu (KHONG cap toan bo catalog).
        private static ProfileData MigrateFromLegacy()
        {
            var p = new ProfileData();

            string json = LegacyReadString("zw.loadout");
            if (!string.IsNullOrEmpty(json))
            {
                LegacyLoadout old = null;
                try { old = JsonUtility.FromJson<LegacyLoadout>(json); }
                catch { /* JSON hong — xu ly nhu khong co */ }

                if (old != null)
                {
                    p.pistol = old.pistol ?? "";
                    p.longA = old.longA ?? "";
                    p.longB = old.longB ?? "";
                    if (old.parts != null)
                    {
                        foreach (var part in old.parts)
                        {
                            if (string.IsNullOrEmpty(part.slot) || string.IsNullOrEmpty(part.guid)) continue;
                            if (p.equippedParts.Exists(x => x.slot == part.slot)) continue;
                            p.equippedParts.Add(part);
                            if (!p.ownedCostumeGuids.Contains(part.guid)) p.ownedCostumeGuids.Add(part.guid);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[PlayerProfile] 'zw.loadout' hong/khong parse duoc — bo qua phan loadout, key cu giu nguyen.");
                }
            }

            p.coin = ReadLegacyCurrency("wallet_coin");
            p.gold = ReadLegacyCurrency("wallet_gold");
            p.gem = ReadLegacyCurrency("wallet_gem");
            return p;
        }

        private static long ReadLegacyCurrency(string key)
        {
            int value = LegacyReadInt(key);
            if (value < 0)
            {
                Debug.LogWarning($"[PlayerProfile] Legacy '{key}' am ({value}) — import ve 0.");
                return 0;
            }
            return value;
        }
    }
}
