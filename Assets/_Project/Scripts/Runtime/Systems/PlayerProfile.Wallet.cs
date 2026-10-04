using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// PlayerProfile, part: Wallet, purchases (weapon, costume) and gacha pity / grants.
    public static partial class PlayerProfile
    {
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
            Notify(Change.Wallet);
        }

        /// <summary>
        /// Like <see cref="Add"/>, for currency picked up mid-run: the balance changes at once, the disk
        /// write is coalesced (one save at most every 1.5 s). Saving the whole profile on every gem
        /// pickup stalled the frame whenever several gems were collected together.
        /// </summary>
        public static void AddDeferred(CurrencyKind kind, long amount)
        {
            if (amount <= 0) return;
            long current = GetBalance(kind);
            long next = current + amount;
            if (next < current) next = long.MaxValue;
            SetBalance(kind, next);
            Notify(Change.Wallet);
            MarkDirty(soon: true);
        }

        /// <summary>Writes the profile if a deferred change is still waiting.</summary>
        public static void FlushIfDirty()
        {
            if (_saveDirty) SaveNow();
        }

        private static void ScheduleFlush(float delay)
        {
            // No timer means nothing will flush later, whatever the flag says: a flag left set by a
            // timer that never fired (Play exited first; statics survive without a domain reload)
            // used to swallow every deferred save after it.
            var timer = Bill.IsReady ? Bill.Timer : null;
            if (timer == null) { _flushScheduled = false; SaveNow(); return; }
            float due = Time.unscaledTime + delay;
            if (_flushScheduled && _flushDue <= due) return;   // an earlier flush already covers it
            if (_flushScheduled && _flushTimer != null) timer.Cancel(_flushTimer);
            _flushScheduled = true;
            _flushDue = due;
            _flushTimer = timer.Delay(delay, () => { _flushScheduled = false; _flushTimer = null; FlushIfDirty(); }, true);
        }

        /// Tru tien nguyen tu: false (khong doi gi) neu amount am hoac so du khong du.
        public static bool TrySpend(CurrencyKind kind, long amount)
        {
            if (amount < 0) return false;
            long current = GetBalance(kind);
            if (current < amount) return false;
            SetBalance(kind, current - amount);
            SaveNow();
            Notify(Change.Wallet);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
            SetBalance(kind, Math.Max(0, value));
            SaveNow();
            Notify(Change.Wallet);
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
            if (!TryCommit(() => { d.coin += price; d.ownedWeaponIds.Remove(weaponId); }, $"[PlayerProfile] Luu profile that bai khi mua '{weaponId}' — rollback, khong tru tien.")) return PurchaseResult.SaveFailed;

            if (price > 0) Notify(Change.Wallet);
            Notify(Change.Loadout);
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
            if (!TryCommit(() => { SetBalance(kind, before); RevokeCostumeItem(itemId); }, $"[PlayerProfile] Save fail mua costume '{itemId}' — rollback.")) return PurchaseResult.SaveFailed;
            if (price > 0) Notify(Change.Wallet);
            Notify(Change.Costume);
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

            string snapshot = Snapshot();
            SetBalance(kind, GetBalance(kind) - price);
            for (int i = 0; i < set.itemIds.Count; i++) GrantCostumeItem(set.itemIds[i]);
            if (!TryCommit(() => Restore(snapshot), $"[PlayerProfile] Save failed while purchasing set '{setId}' - rollback.")) return PurchaseResult.SaveFailed;
            Notify(Change.Wallet);
            Notify(Change.Costume);
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

        public enum WeaponUpgradeResult { Upgraded, MaxLevel, NotOwned, InsufficientShards, InsufficientGold, InvalidData, SaveFailed, Locked }

        public static WeaponUpgradeResult TryUpgradeWeapon(WeaponData weapon, EconomyConfig economy)
        {
            if (weapon == null || economy == null || string.IsNullOrEmpty(weapon.WeaponId)) return WeaponUpgradeResult.InvalidData;
            if (!IsWeaponOwned(weapon.WeaponId)) return WeaponUpgradeResult.NotOwned;
            // Gun stars open at account level 5 (AccountProgress). Until 2026-09-29 only a test knew.
            if (!AccountProgress.IsUnlocked(AccountProgress.Feature.GunStars, AccountLevel)) return WeaponUpgradeResult.Locked;
            int level = GetWeaponLevel(weapon.WeaponId);
            if (level >= 3) return WeaponUpgradeResult.MaxLevel;
            int tier = Mathf.Clamp((int)weapon.tier, 0, 4);
            int[] shardTable = level == 1 ? economy.weaponStar2ShardCost : economy.weaponStar3ShardCost;
            long[] goldTable = level == 1 ? economy.weaponStar2GoldCost : economy.weaponStar3GoldCost;
            if (shardTable == null || shardTable.Length <= tier || goldTable == null || goldTable.Length <= tier)
                return WeaponUpgradeResult.InvalidData;
            int shardCost = shardTable[tier]; long goldCost = goldTable[tier];
            if (GetWeaponShards(weapon.WeaponId) < shardCost) return WeaponUpgradeResult.InsufficientShards;
            // Owner (M10): Gold is Coin. The star cost tables keep their "Gold" names but charge Coin.
            if (Coin < goldCost) return WeaponUpgradeResult.InsufficientGold;

            string snapshot = Snapshot();
            SetWeaponShardsInMemory(weapon.WeaponId, GetWeaponShards(weapon.WeaponId) - shardCost);
            SetBalance(CurrencyKind.Coin, Coin - goldCost);
            SetWeaponLevelInMemory(weapon.WeaponId, level + 1);
            if (!TryCommit(() => Restore(snapshot), $"[PlayerProfile] Save failed upgrading '{weapon.WeaponId}' - rollback.")) return WeaponUpgradeResult.SaveFailed;
            Notify(Change.Wallet); Notify(Change.Loadout);
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
            string snapshot = Snapshot();
            if (!SpendInMemory(spendKind, spend)) return false;
            applyGrants();
            if (!TryCommit(() => Restore(snapshot), "[PlayerProfile] Save fail gacha — rollback.")) return false;
            Notify(Change.Wallet);
            Notify(Change.Loadout);
            Notify(Change.Costume);
            return true;
        }
    }
}
