using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// UI-authoring metadata cho prototype content — KHÔNG phải economy backend.
    /// Gameplay data thật nằm ở WeaponData/ModularCostumeCatalog; asset này chỉ thêm phần
    /// UI chưa có chỗ chứa: icon (thumbnail generate/override), owned/featured prototype state,
    /// cheat unlock để duyệt toàn bộ item khi backend equipment/economy chưa tồn tại.
    /// Populate bằng ZombieWar/UI/Authoring/Generate Item Thumbnails.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/UI Prototype Catalog", fileName = "UIPrototypeCatalog")]
    public class UIPrototypeCatalog : ScriptableObject
    {
        [Serializable]
        public class WeaponEntry
        {
            public WeaponData data;
            public Sprite icon;
            public bool owned;
            public bool featured;
        }

        [Serializable]
        public class CostumeIcon
        {
            public string guid;    // PartEntry.guid — ổn định qua rename
            public Sprite icon;
        }

        [Header("Cheat / prototype state")]
        [Tooltip("Bật = mọi weapon/costume xem được như đã sở hữu. Prototype-only, không phải economy thật.")]
        public bool cheatUnlockAll = true;

        [Header("Weapons (nguồn: Assets/_Project/Data/Weapons)")]
        public List<WeaponEntry> weapons = new();
        public Sprite weaponFallbackIcon;

        [Header("Costume icons (guid → sprite, sparse — phần thiếu dùng fallback)")]
        public List<CostumeIcon> costumeIcons = new();
        public Sprite costumeFallbackIcon;

        public Sprite GetWeaponIcon(WeaponData data)
        {
            if (data != null)
                for (int i = 0; i < weapons.Count; i++)
                    if (weapons[i].data == data && weapons[i].icon != null)
                        return weapons[i].icon;
            return weaponFallbackIcon;
        }

        public bool IsWeaponOwned(WeaponData data)
        {
            if (cheatUnlockAll) return true;
            for (int i = 0; i < weapons.Count; i++)
                if (weapons[i].data == data)
                    return weapons[i].owned;
            return false;
        }

        public Sprite GetCostumeIcon(string guid)
        {
            if (!string.IsNullOrEmpty(guid))
                for (int i = 0; i < costumeIcons.Count; i++)
                    if (costumeIcons[i].guid == guid && costumeIcons[i].icon != null)
                        return costumeIcons[i].icon;
            return costumeFallbackIcon;
        }
    }
}
