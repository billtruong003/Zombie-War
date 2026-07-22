using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Slice 4.2: bind icon costume tu OFFICIAL VENDOR SCREENSHOT (Layer Lab), khong dung generated
    /// render nua (eyes/mouth den/lech). Tham chieu Sprite da import san trong ThirdParty — KHONG
    /// copy/sua file goc. Non-Body: name -> ScreenShot/&lt;name&gt;.png. Body: 6 mau -> Body_&lt;Color&gt;.png.
    /// </summary>
    public static class VendorCostumeIconBinder
    {
        const string ScreenShotDir =
            "Assets/ThirdParty/Layer Lab/3D Casual Character/3D Characters Pro - Fantasy/Resources/3D Characters Pro - Fantasy/ScreenShot";
        const string CatalogPath = "Assets/_Project/Data/Character/ModularCostumeCatalog.asset";

        [MenuItem("ZombieWar/UI/Authoring/Bind Vendor Costume Icons")]
        public static void Bind()
        {
            var cat = AssetDatabase.LoadAssetAtPath<ZombieWar.ModularCostumeCatalog>(CatalogPath);
            var ui = UIThumbnailGenerator.EnsureCatalogAsset();
            if (cat == null) { Debug.LogError("[VendorIcons] Thieu ModularCostumeCatalog."); return; }

            var iconMap = new Dictionary<string, Sprite>();
            var missing = new List<string>();
            int nonBody = 0;

            foreach (var slot in cat.slots)
            {
                if (slot.slot == ZombieWar.ModularCostumeCatalog.BodySlot) continue; // Body dung color icon
                foreach (var p in slot.parts)
                {
                    nonBody++;
                    var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"{ScreenShotDir}/{p.name}.png");
                    if (spr == null) { missing.Add($"{slot.slot}/{p.name}"); continue; }
                    iconMap[p.guid] = spr;
                }
            }

            // Ghi de costumeIcons bang mapping vendor (deterministic, khong stale).
            ui.costumeIcons = iconMap.Select(kv => new UIPrototypeCatalog.CostumeIcon { guid = kv.Key, icon = kv.Value }).ToList();

            // Body color icons.
            ui.bodyColorIcons = new List<UIPrototypeCatalog.BodyColorIcon>();
            var missingBody = new List<string>();
            foreach (var col in ZombieWar.ModularCostumeCatalog.BodyColors)
            {
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"{ScreenShotDir}/Body_{col}.png");
                if (spr == null) { missingBody.Add(col); continue; }
                ui.bodyColorIcons.Add(new UIPrototypeCatalog.BodyColorIcon { color = col, icon = spr });
            }

            // Fallback trung tinh (khong helmet).
            ui.costumeFallbackIcon = UISpriteFactory.Load("rounded_dashed");

            EditorUtility.SetDirty(ui);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VendorIcons] Bind DONE — non-Body {iconMap.Count}/{nonBody} vendor icon, " +
                      $"Body {ui.bodyColorIcons.Count}/6 color icon. " +
                      $"missing non-Body={missing.Count}{(missing.Count > 0 ? " (vd " + missing[0] + ")" : "")}, " +
                      $"missing Body={missingBody.Count}.");
        }
    }
}
