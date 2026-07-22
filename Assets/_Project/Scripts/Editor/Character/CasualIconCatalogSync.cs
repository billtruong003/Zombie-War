using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Editor
{
    /// <summary>Copies the Pro Casual catalog's already-baked item sprites into UI lookup metadata.</summary>
    public static class CasualIconCatalogSync
    {
        private const string CatalogPath = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        private const string UiCatalogPath = "Assets/_Project/UI/Data/UIPrototypeCatalog.asset";

        [MenuItem("ZombieWar/Costume/Sync Casual Icons to UI Catalog")]
        public static void Sync()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CatalogPath);
            var ui = AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(UiCatalogPath);
            if (catalog == null || ui == null)
            {
                Debug.LogError("[CasualIconSync] Missing CasualCostumeCatalog or UIPrototypeCatalog.");
                return;
            }

            var icons = new List<UIPrototypeCatalog.CostumeIcon>();
            int missing = 0;
            foreach (var slot in catalog.slots)
            {
                if (catalog.IsTechnicalCasualSlot(slot.slot)) continue;
                foreach (var part in slot.parts)
                {
                    if (string.IsNullOrEmpty(part.itemId)) continue;
                    if (part.icon == null) missing++;
                    icons.Add(new UIPrototypeCatalog.CostumeIcon { guid = part.itemId, icon = part.icon });
                }
            }
            ui.costumeIcons = icons;
            EditorUtility.SetDirty(ui);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CasualIconSync] Synced {icons.Count} Pro Casual item icons; missing={missing}.");
        }
    }
}
