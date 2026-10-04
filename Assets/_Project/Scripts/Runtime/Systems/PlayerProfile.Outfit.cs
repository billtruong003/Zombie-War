using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// PlayerProfile, part: Costume equip transactions and outfit repair.
    public static partial class PlayerProfile
    {
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
            if (!TryCommit(() => { SetPartInMemory(slotName, previous); }, $"[PlayerProfile] Luu profile that bai khi equip costume '{guid}' — rollback.")) return CostumeEquipResult.SaveFailed;
            Notify(Change.Costume);
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
            if (!TryCommit(() => { SetPartInMemory(slot.slot, previous); }, $"[PlayerProfile] Luu profile that bai khi clear slot '{slotName}' — rollback.")) return CostumeEquipResult.SaveFailed;
            Notify(Change.Costume);
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
            if (!TryCommit(null, "[PlayerProfile] Luu profile that bai khi ensure costume defaults.")) return false;
            Notify(Change.Costume);
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
            if (!TryCommit(null, "[PlayerProfile] Luu profile that bai khi ensure Casual costume.")) return false;
            Notify(Change.Costume);
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
            if (!TryCommit(() => { d.equippedParts = backup; }, "[PlayerProfile] Save fail reset Casual outfit — rollback.")) return CostumeEquipResult.SaveFailed;
            Notify(Change.Costume);
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
            if (!TryCommit(() => { d.equippedParts = backup; }, "[PlayerProfile] Save fail set Casual outfit — rollback.")) return CostumeEquipResult.SaveFailed;
            Notify(Change.Costume);
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
            if (!TryCommit(() => { d.equippedParts = backup; d.bodyColor = bcBak; d.bodyEar = beBak; }, "[PlayerProfile] Luu profile that bai khi reset outfit — rollback.")) return CostumeEquipResult.SaveFailed;
            Notify(Change.Costume);
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

            if (!TryCommit(() => { Data.equippedParts = backup; }, "[PlayerProfile] Luu profile that bai khi equip outfit — rollback.")) return CostumeEquipResult.SaveFailed;
            Notify(Change.Costume);
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
            if (!TryCommit(() => { d.equippedParts = backup; d.bodyColor = bcBak; d.bodyEar = beBak; }, "[PlayerProfile] Save that bai khi equip look — rollback.")) return CostumeEquipResult.SaveFailed;
            Notify(Change.Costume);
            return CostumeEquipResult.Equipped;
        }

        /// DEV-ONLY: mo khoa toan bo part hop le trong catalog (14 wardrobe slot; held-item
        /// categories khong nam trong catalog nen tu dong bi loai). 1 batch, 1 save, 1 event.
        /// Idempotent — chi them guid con thieu. Tra ve so entry moi duoc them.
        public static int UnlockAllCostumes(ModularCostumeCatalog catalog)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
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
            if (!TryCommit(() => { owned.RemoveRange(backupCount, Math.Max(0, owned.Count - backupCount)); }, "[PlayerProfile] Luu profile that bai khi unlock all costume — rollback.")) return 0;
            Notify(Change.Costume);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
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
                Notify(Change.Costume);
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
            Notify(Change.Costume);
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
    }
}
