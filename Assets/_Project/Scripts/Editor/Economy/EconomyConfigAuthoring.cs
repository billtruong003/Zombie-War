using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Sinh EconomyConfig.asset TU DONG + DETERMINISTIC tu ModularCostumeCatalog (nguon that).
    /// Idempotent: chay lai cho cung ket qua (rarity theo hash on dinh cua itemId, khong random).
    /// Starter (default outfit + White/Normal) -> source Starter, KHONG ban. Cac itemId khac ->
    /// Shop/Gacha/ShopAndGacha theo rarity. Gia tap trung o rarity band (provisional).
    /// </summary>
    public static class EconomyConfigAuthoring
    {
        const string CatalogPath = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        const string PresetDir = "Assets/ThirdParty/Layer Lab/3D CharactersCasual/3D Characters Pro-Casual/Prefabs/Characters";
        const string EconomyDir = "Assets/_Project/Data/Economy";
        const string EconomyPath = EconomyDir + "/EconomyConfig.asset";

        sealed class SetOffer
        {
            public readonly string preset, id, name;
            public readonly WeaponTier rarity;
            public readonly WalletCurrency currency;
            public readonly long price;

            public SetOffer(string preset, string id, string name, WeaponTier rarity, WalletCurrency currency, long price)
            {
                this.preset = preset; this.id = id; this.name = name;
                this.rarity = rarity; this.currency = currency; this.price = price;
            }
        }

        // Stable vendor-preset manifest. Identity, presentation and value never depend on scan order.
        static readonly SetOffer[] CuratedSets =
        {
            new("Characters_85",  "casual.pro.set.001", "Santa Claus",            WeaponTier.Epic,      WalletCurrency.Gem,  100),
            new("Characters_33",  "casual.pro.set.002", "Street Artist",          WeaponTier.Uncommon,  WalletCurrency.Coin, 3500),
            new("Characters_48",  "casual.pro.set.003", "Little Explorer",        WeaponTier.Rare,      WalletCurrency.Gem,   60),
            new("Characters_115", "casual.pro.set.004", "Red Rebel",              WeaponTier.Rare,      WalletCurrency.Coin, 5000),
            new("Characters_29",  "casual.pro.set.005", "Tech Scholar",           WeaponTier.Rare,      WalletCurrency.Coin, 7500),
            new("Characters_44",  "casual.pro.set.006", "Gardener",               WeaponTier.Uncommon,  WalletCurrency.Coin, 3500),
            new("Characters_6",   "casual.pro.set.007", "Weekend Skater",         WeaponTier.Uncommon,  WalletCurrency.Coin, 3500),
            new("Characters_64",  "casual.pro.set.008", "Playful Tiger Cub",      WeaponTier.Epic,      WalletCurrency.Gem,  100),
            new("Characters_89",  "casual.pro.set.009", "Turtle Shell Ninja",     WeaponTier.Legendary, WalletCurrency.Gem,  180),
            new("Characters_10",  "casual.pro.set.010", "Street Shadow",          WeaponTier.Rare,      WalletCurrency.Coin, 5000),
            new("Characters_111", "casual.pro.set.011", "Blue Shark",             WeaponTier.Epic,      WalletCurrency.Gem,  100),
            new("Characters_119", "casual.pro.set.012", "Offbeat Punk",           WeaponTier.Rare,      WalletCurrency.Coin, 7500),
            new("Characters_14",  "casual.pro.set.013", "Shades Skater",          WeaponTier.Rare,      WalletCurrency.Coin, 5000),
            new("Characters_19",  "casual.pro.set.014", "Candy Scientist",        WeaponTier.Rare,      WalletCurrency.Coin, 7500),
            new("Characters_23",  "casual.pro.set.015", "Autumn Stroll",          WeaponTier.Uncommon,  WalletCurrency.Coin, 3500),
            new("Characters_31",  "casual.pro.set.016", "Winter Warmth",          WeaponTier.Rare,      WalletCurrency.Coin, 5000),
            new("Characters_39",  "casual.pro.set.017", "Navy Cadet",             WeaponTier.Rare,      WalletCurrency.Coin, 5000),
            new("Characters_52",  "casual.pro.set.018", "Cotton Sheep",           WeaponTier.Epic,      WalletCurrency.Gem,  100),
            new("Characters_65",  "casual.pro.set.019", "Red Mask Warrior",       WeaponTier.Epic,      WalletCurrency.Gem,  100),
            new("Characters_7",   "casual.pro.set.020", "Gentle Sunshine",        WeaponTier.Common,    WalletCurrency.Coin, 2500),
            new("Characters_8",   "casual.pro.set.021", "Green Street",           WeaponTier.Uncommon,  WalletCurrency.Coin, 3500),
            new("Characters_9",   "casual.pro.set.022", "Classic Cap",            WeaponTier.Uncommon,  WalletCurrency.Coin, 3500),
            new("Characters_114", "casual.pro.set.023", "Medieval Adventurer",    WeaponTier.Epic,      WalletCurrency.Gem,   80),
            new("Characters_30",  "casual.pro.set.024", "Blue Academy",           WeaponTier.Rare,      WalletCurrency.Coin, 7500),
            new("Characters_49",  "casual.pro.set.025", "Jungle Marksman",        WeaponTier.Epic,      WalletCurrency.Gem,  100),
            new("Characters_58",  "casual.pro.set.026", "Little Sailor",          WeaponTier.Rare,      WalletCurrency.Gem,   60),
            new("Characters_83",  "casual.pro.set.027", "Viking Warrior",         WeaponTier.Legendary, WalletCurrency.Gem,  180),
            new("Characters_95",  "casual.pro.set.028", "Campus Star",            WeaponTier.Rare,      WalletCurrency.Coin, 5000),
            new("Characters_5",   "casual.pro.set.029", "Orange Energy",          WeaponTier.Uncommon,  WalletCurrency.Coin, 3500),
            new("Characters_2",   "casual.pro.set.030", "Minimal Black",          WeaponTier.Common,    WalletCurrency.Coin, 2500),
        };

        [MenuItem("ZombieWar/Economy/Generate Economy Config")]
        public static void Generate()
        {
            var cat = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CatalogPath);
            if (cat == null) { Debug.LogError("[Economy] Thieu ModularCostumeCatalog o " + CatalogPath); return; }

            var econ = AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyPath);
            bool created = false;
            if (econ == null)
            {
                if (!AssetDatabase.IsValidFolder(EconomyDir))
                    AssetDatabase.CreateFolder("Assets/_Project/Data", "Economy");
                econ = ScriptableObject.CreateInstance<EconomyConfig>();
                AssetDatabase.CreateAsset(econ, EconomyPath);
                created = true;
            }

            econ.provisional = true;
            econ.costumeBands = BuildBands();
            econ.costumeItems = BuildCostumeItems(cat);
            econ.costumeSets = BuildCostumeSets(cat, econ.costumeSets);
            // Gacha pool giu default trong asset (editor-tunable); chi set neu chua co poolId.
            if (string.IsNullOrEmpty(econ.weaponPool?.poolId))
                econ.weaponPool = new EconomyConfig.GachaPool { poolId = "gacha.weapon", displayName = "Weapon Gacha", kind = "weapon", currency = WalletCurrency.Gold, singleCost = 100, multiCost = 900 };
            if (string.IsNullOrEmpty(econ.costumePool?.poolId))
                econ.costumePool = new EconomyConfig.GachaPool { poolId = "gacha.costume", displayName = "Skin Gacha", kind = "costume", currency = WalletCurrency.Gem, singleCost = 10, multiCost = 90 };
            econ.weaponPool.weaponDuplicateShards = new[] { 10, 10, 12, 15, 20 };

            econ.RebuildLookups();
            EditorUtility.SetDirty(econ);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int sellable = 0, starter = 0, gacha = 0;
            foreach (var e in econ.costumeItems)
            {
                if (e.source == AcquireSource.Starter) starter++;
                else sellable++;
                if (e.source == AcquireSource.Gacha || e.source == AcquireSource.ShopAndGacha) gacha++;
            }
            Debug.Log($"[Economy] {(created ? "Tao" : "Cap nhat")} EconomyConfig: {econ.costumeItems.Count} item, " +
                      $"{econ.costumeSets.Count} curated Pro outfit sets ({starter} starter item). Asset: {EconomyPath}");
            Selection.activeObject = econ;
        }

        static List<EconomyConfig.RarityBand> BuildBands() => new()
        {
            new EconomyConfig.RarityBand { rarity = WeaponTier.Common,    currency = WalletCurrency.Coin, price = 300 },
            new EconomyConfig.RarityBand { rarity = WeaponTier.Uncommon,  currency = WalletCurrency.Coin, price = 800 },
            new EconomyConfig.RarityBand { rarity = WeaponTier.Rare,      currency = WalletCurrency.Gold, price = 120 },
            new EconomyConfig.RarityBand { rarity = WeaponTier.Epic,      currency = WalletCurrency.Gold, price = 350 },
            new EconomyConfig.RarityBand { rarity = WeaponTier.Legendary, currency = WalletCurrency.Gold, price = 900 },
        };

        static List<EconomyConfig.CostumeEntry> BuildCostumeItems(ModularCostumeCatalog cat)
        {
            var items = new List<EconomyConfig.CostumeEntry>();
            var starterIds = new HashSet<string>();
            if (!cat.compositeBody)
                foreach (var def in cat.slotDefinitions)
                    if (!string.IsNullOrEmpty(def.defaultItemId)) starterIds.Add(def.defaultItemId);
            else if (cat.defaults != null && cat.defaults.ownedGuids != null)
                foreach (var g in cat.defaults.ownedGuids) if (!string.IsNullOrEmpty(g)) starterIds.Add(g);

            // --- Non-Body parts (guid) ---
            foreach (var slot in cat.slots)
            {
                if (slot == null || (cat.compositeBody && slot.slot == ModularCostumeCatalog.BodySlot)) continue;
                if (cat.IsTechnicalCasualSlot(slot.slot)) continue;
                foreach (var p in slot.parts)
                {
                    string id = cat.compositeBody ? p.guid : p.itemId;
                    if (string.IsNullOrEmpty(id)) continue;
                    bool isStarter = starterIds.Contains(id);
                    var offer = ItemOffer(slot.slot, p.name, isStarter);
                    items.Add(new EconomyConfig.CostumeEntry
                    {
                        itemId = id,
                        displayName = PlayerFacingName(slot.slot, p.name),
                        slot = slot.slot,
                        rarity = offer.rarity,
                        source = isStarter ? AcquireSource.Starter : offer.source,
                        currency = offer.currency,
                        price = isStarter ? 0 : offer.price,
                    });
                }
            }

            if (!cat.compositeBody) return items;

            // --- Body colors (composite) ---
            foreach (var col in ModularCostumeCatalog.BodyColors)
            {
                bool isStarter = col == "White";
                var (rarity, source) = BodyColorTier(col);
                items.Add(new EconomyConfig.CostumeEntry
                {
                    itemId = EconomyConfig.BodyColorId(col),
                    displayName = col + " Skin",
                    slot = ModularCostumeCatalog.BodySlot,
                    rarity = isStarter ? WeaponTier.Common : rarity,
                    source = isStarter ? AcquireSource.Starter : source,
                });
            }

            // --- Body ears ---
            foreach (var ear in ModularCostumeCatalog.BodyEars)
            {
                bool isStarter = ear == "Normal";
                items.Add(new EconomyConfig.CostumeEntry
                {
                    itemId = EconomyConfig.BodyEarId(ear),
                    displayName = ear == "Elf" ? "Elf Ears" : ear + " Ears",
                    slot = ModularCostumeCatalog.BodySlot,
                    rarity = isStarter ? WeaponTier.Common : WeaponTier.Epic,
                    source = isStarter ? AcquireSource.Starter : AcquireSource.ShopAndGacha,
                });
            }

            return items;
        }

        static List<EconomyConfig.CostumeSetEntry> BuildCostumeSets(ModularCostumeCatalog cat,
            List<EconomyConfig.CostumeSetEntry> previous)
        {
            var oldIcons = new Dictionary<string, Sprite>();
            if (previous != null) foreach (var x in previous)
                if (x != null && !string.IsNullOrEmpty(x.setId) && x.icon != null) oldIcons[x.setId] = x.icon;

            var byMesh = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var slot in cat.slots)
            {
                if (cat.IsTechnicalCasualSlot(slot.slot)) continue;
                foreach (var p in slot.parts)
                    if (!string.IsNullOrEmpty(p.name) && !string.IsNullOrEmpty(p.itemId)) byMesh[p.name] = p.itemId;
            }

            var candidates = new Dictionary<string, (string path, List<string> ids)>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PresetDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(path);
                if (!n.StartsWith("Characters_", StringComparison.Ordinal) || !int.TryParse(n.Substring(11), out _)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var names = go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(r => r.enabled && r.gameObject.activeSelf && r.sharedMesh != null)
                    .Select(r => r.sharedMesh.name).ToArray();
                if (names.Any(IsHeldItem)) continue;
                var ids = names.Where(byMesh.ContainsKey).Select(x => byMesh[x]).Distinct().OrderBy(x => x).ToList();
                if (ids.Count < 5 || !HasSlot(ids, cat, "Chest") || !HasSlot(ids, cat, "Legs")) continue;
                AddMissingRequiredDefaults(ids, cat);
                ids.Sort(StringComparer.Ordinal);
                string sig = string.Join("|", ids);
                if (candidates.Values.Any(x => string.Join("|", x.ids) == sig)) continue;
                candidates[n] = (path, ids);
            }

            var result = new List<EconomyConfig.CostumeSetEntry>(CuratedSets.Length);
            foreach (var offer in CuratedSets)
            {
                if (!candidates.TryGetValue(offer.preset, out var c))
                {
                    Debug.LogError($"[Economy] Missing curated preset '{offer.preset}'. Set '{offer.id}' was not generated.");
                    continue;
                }
                result.Add(new EconomyConfig.CostumeSetEntry
                {
                    setId = offer.id,
                    displayName = offer.name,
                    rarity = offer.rarity,
                    source = AcquireSource.ShopAndGacha,
                    currency = offer.currency,
                    price = offer.price,
                    gemPrice = offer.currency == WalletCurrency.Gem ? offer.price : 0,
                    itemIds = c.ids,
                    sourcePreset = c.path,
                    icon = oldIcons.TryGetValue(offer.id, out var icon) ? icon : null,
                });
            }
            return result;
        }

        static bool IsHeldItem(string n) => n.StartsWith("Axe_") || n.StartsWith("Sword_")
            || n.StartsWith("Spear_") || n.StartsWith("Shield_");

        static bool HasSlot(List<string> ids, ModularCostumeCatalog cat, string requiredSlot)
        {
            return ids.Any(id => cat.TryFindByItemId(id, out var slot, out _) && slot == requiredSlot);
        }

        static void AddMissingRequiredDefaults(List<string> ids, ModularCostumeCatalog cat)
        {
            foreach (var def in cat.slotDefinitions)
            {
                if (!def.required || HasSlot(ids, cat, def.id) || string.IsNullOrEmpty(def.defaultItemId)) continue;
                ids.Add(def.defaultItemId);
            }
        }

        static (WeaponTier rarity, AcquireSource source, WalletCurrency currency, long price) ItemOffer(
            string slot, string meshName, bool starter)
        {
            if (starter) return (WeaponTier.Common, AcquireSource.Starter, WalletCurrency.Coin, 0);
            int n = TrailingNumber(meshName);
            if (slot == "Mask")
                return n <= 2
                    ? (WeaponTier.Uncommon, AcquireSource.Shop, WalletCurrency.Coin, 1500)
                    : (n >= 8 ? WeaponTier.Epic : WeaponTier.Rare, AcquireSource.ShopAndGacha,
                        WalletCurrency.Gem, n >= 8 ? 50 : 30);
            if (slot == "Head" && (n == 33 || n == 35 || n == 53 || n == 56 || n == 58 || n == 60))
                return (n == 56 || n == 60 ? WeaponTier.Legendary : WeaponTier.Epic,
                    AcquireSource.ShopAndGacha, WalletCurrency.Gem, n == 56 || n == 60 ? 60 : 40);
            if (slot == "Eye" && (n == 11 || n == 12))
                return (WeaponTier.Epic, AcquireSource.ShopAndGacha, WalletCurrency.Gem, 20);
            if (slot == "Mouth" && n == 11)
                return (WeaponTier.Rare, AcquireSource.ShopAndGacha, WalletCurrency.Gem, 15);

            long price = slot switch
            {
                "Eye" => 300, "Brow" => 250, "Mouth" => 300,
                "Hair" => 1200, "Beard" => 600, "HairAccessory" => 900,
                "Head" => 2200, "Eyewear" => 1400, "Earring" => 800,
                "Chest" => 1800, "Legs" => 1600, "Feet" => 1200,
                "Hands" => 1100, "Bracelet" => 700, "HandAccessory" => 900,
                "Watch" => 1000, "Back" => 2200, _ => 800,
            };
            WeaponTier rarity = price >= 2000 ? WeaponTier.Rare
                : price >= 1000 ? WeaponTier.Uncommon : WeaponTier.Common;
            return (rarity, AcquireSource.Shop, WalletCurrency.Coin, price);
        }

        static string PlayerFacingName(string slot, string meshName)
        {
            int n = TrailingNumber(meshName);
            if (slot == "Eye")
            {
                string[] names = { "Natural Open Eyes", "Sleepy Eyes", "Surprised Round Eyes", "Relaxed Closed Eyes",
                    "Sparkling Blue Eyes", "Drowsy Blue Eyes", "Warm Brown Eyes", "Drowsy Brown Eyes",
                    "Smiling Eyes", "Chibi Eyes", "X Dazed Eyes", "Dizzy Eyes" };
                if (n >= 1 && n <= names.Length) return names[n - 1];
            }
            if (slot == "Mouth")
            {
                string[] names = { "Soft Smile", "Bright Smile", "Smirk", "Pout", "Sad Face", "Happy Open Mouth",
                    "Surprised Mouth", "Big Laugh", "Toothy Grin", "Excited Grin", "Fanged Grin" };
                if (n >= 1 && n <= names.Length) return names[n - 1];
            }
            if (slot == "Brow")
            {
                string[] names = { "Natural Black Brows", "Raised Black Brows", "Worried Black Brows", "Focused Black Brows",
                    "Angry Black Brows", "Sad Black Brows", "Natural Blond Brows", "Raised Blond Brows", "Worried Blond Brows",
                    "Focused Blond Brows", "Angry Blond Brows", "Sad Blond Brows", "Confident Black Brows", "Deep Angry Brows",
                    "Soft Black Brows", "Strong Brows", "Right Scar Brows", "Left Scar Brows", "Soft White Brows",
                    "Sharp White Brows", "Bushy Black Brows", "Bushy Blond Brows", "Bushy Orange Brows" };
                if (n >= 1 && n <= names.Length) return names[n - 1];
            }

            string prefix = slot switch
            {
                "Hair" => "Hairstyle", "Beard" => "Beard", "Mask" => "Mask", "HairAccessory" => "Hair Accessory",
                "Head" => "Hat", "Eyewear" => "Glasses", "Earring" => "Earring", "Chest" => "Top",
                "Hands" => "Gloves", "Bracelet" => "Bracelet", "HandAccessory" => "Hand Accessory",
                "Watch" => "Watch", "Back" => "Backpack", "Legs" => "Pants", "Feet" => "Shoes", _ => slot,
            };
            return n > 0 ? $"{prefix} {n:00}" : prefix;
        }

        static int TrailingNumber(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            int end = value.Length - 1;
            while (end >= 0 && char.IsDigit(value[end])) end--;
            return end == value.Length - 1 || !int.TryParse(value.Substring(end + 1), out int n) ? 0 : n;
        }

        // Rarity on dinh theo hash FNV-1a cua itemId (KHONG dung string.GetHashCode — khong stable).
        static WeaponTier RarityOf(string id)
        {
            uint h = 2166136261u;
            for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
            int r = (int)(h % 100u);
            if (r < 45) return WeaponTier.Common;
            if (r < 75) return WeaponTier.Uncommon;
            if (r < 90) return WeaponTier.Rare;
            if (r < 98) return WeaponTier.Epic;
            return WeaponTier.Legendary;
        }

        static AcquireSource SourceForRarity(WeaponTier r) => r switch
        {
            WeaponTier.Legendary => AcquireSource.Gacha,          // chase — gacha-only
            WeaponTier.Epic => AcquireSource.ShopAndGacha,
            WeaponTier.Rare => AcquireSource.ShopAndGacha,
            _ => AcquireSource.Shop,
        };

        static (WeaponTier, AcquireSource) BodyColorTier(string col) => col switch
        {
            "Black" => (WeaponTier.Rare, AcquireSource.Shop),
            "Brown" => (WeaponTier.Rare, AcquireSource.Shop),
            "Green" => (WeaponTier.Epic, AcquireSource.ShopAndGacha),
            "Purple" => (WeaponTier.Epic, AcquireSource.ShopAndGacha),
            "Yellow" => (WeaponTier.Legendary, AcquireSource.Gacha),
            _ => (WeaponTier.Common, AcquireSource.Shop),
        };
    }
}
