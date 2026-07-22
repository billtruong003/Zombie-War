using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar;
using ZombieWar.EditorTools;
using ZombieWar.UI;
using K = ZombieWar.Editor.UI.UIKit;
using A = ZombieWar.Editor.UI.UIKit.Anch;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// DESTRUCTIVE generator cho 3 màn con Menu (02 Loadout · 03 Costume · 04–07 Shop).
    /// Sau migration prefab: source of truth là prefab — tool này chỉ chạy khi user xác nhận
    /// rebuild (ghi đè prefab asset + reconnect). Ensure/Validate hằng ngày nằm ở UISceneContracts.
    /// Prototype content: card bake từ WeaponData/ModularCostumeCatalog + UIPrototypeCatalog
    /// (icon generate + cheat unlock) — KHÔNG phải economy backend.
    /// Ghi chú VLG: child trực tiếp của VerticalLayoutGroup content PHẢI có LayoutElement.minHeight.
    /// </summary>
    public static class MenuScreensInstaller
    {
        const string MenuScene = "Assets/_Project/Scenes/Menu.unity";

        [MenuItem("ZombieWar/UI/Authoring/Rebuild Menu Screens (Destructive)...")]
        public static void BuildInteractive()
        {
            if (!UIPrefabizer.ConfirmDestructive("LoadoutScreen + CostumeScreen + ShopScreen")) return;
            Build();
        }

        public static void Build()
        {
            if (EnsureScene()) return;

            var canvas = UIRootInstaller.EnsureRoot();
            var root = (RectTransform)canvas.transform;

            foreach (var n in new[] { "LoadoutScreen", "CostumeScreen", "ShopScreen" })
            {
                var old = root.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            if (FindInactive<HubScreen>() == null) HubInstaller.Build();

            // prototype data + preview stage phải sẵn trước khi bake card/RawImage
            var stage = MenuCharacterStageInstaller.EnsureInOpenScene();
            var uiCatalog = UIThumbnailGenerator.EnsureCatalogAsset();
            EnsureWeaponEntries(uiCatalog);
            var costumeCatalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(
                "Assets/_Project/Data/Character/ModularCostumeCatalog.asset");

            var loadout = BuildLoadout(root, uiCatalog);
            var costume = BuildCostume(root, uiCatalog, costumeCatalog, stage);
            var shop    = BuildShop(root, uiCatalog);

            WireHub(loadout, shop, costume);
            WireShopLink(loadout, shop);

            UIRootInstaller.SetInitialScreen(FindInactive<HubScreen>());

            // prefab là source of truth — rebuild ghi đè asset + reconnect instance
            UIPrefabizer.ReconnectAfterRebuild(loadout.gameObject, $"{UIPrefabizer.ScreensDir}/UI_LoadoutScreen.prefab");
            UIPrefabizer.ReconnectAfterRebuild(costume.gameObject, $"{UIPrefabizer.ScreensDir}/UI_CostumeScreen.prefab");
            UIPrefabizer.ReconnectAfterRebuild(shop.gameObject, $"{UIPrefabizer.ScreensDir}/UI_ShopScreen.prefab");

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("[MenuScreensInstaller] Loadout + Costume + Shop rebuilt (prototype content) + prefab reconnected.");
        }

        [MenuItem("ZombieWar/UI/Authoring/Ensure Real Costume Shop + Upgrades")]
        public static void EnsureCommercePrefabs()
        {
            ZombieWar.Editor.CasualIconCatalogSync.Sync();
            const string shopPath = "Assets/_Project/UI/Prefabs/Screens/UI_ShopScreen.prefab";
            var root = PrefabUtility.LoadPrefabContents(shopPath);
            try
            {
                var screen = root.GetComponent<ShopScreen>();
                var safe = root.transform.Find("Safe") as RectTransform;
                if (screen == null || safe == null)
                {
                    Debug.LogError("[MenuScreensInstaller] Shop prefab missing ShopScreen/Safe.");
                    return;
                }

                foreach (var name in new[] { "TabCostume", "TabUpgrades" })
                {
                    var old = safe.Find(name);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }
                var oldModal = root.transform.Find("PurchaseModal");
                if (oldModal != null) Object.DestroyImmediate(oldModal.gameObject);

                var costumePanel = BuildShopCostumeReal(safe);
                var upgradePanel = BuildShopUpgradesReal(safe);
                var modal = BuildShopPurchaseModal((RectTransform)root.transform,
                    out var modalIcon, out var modalTitle, out var modalPrice, out var modalConfirm, out var modalCancel);

                var so = new SerializedObject(screen);
                var panels = so.FindProperty("tabPanels");
                if (panels == null || panels.arraySize < 4)
                {
                    Debug.LogError("[MenuScreensInstaller] Shop tabPanels must contain four tabs.");
                    return;
                }
                panels.GetArrayElementAtIndex(2).objectReferenceValue = costumePanel;
                panels.GetArrayElementAtIndex(3).objectReferenceValue = upgradePanel;
                Set(so, "catalog", AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(
                    "Assets/_Project/UI/Data/UIPrototypeCatalog.asset"));
                Set(so, "economy", AssetDatabase.LoadAssetAtPath<EconomyConfig>(
                    "Assets/_Project/Data/Economy/EconomyConfig.asset"));
                SetArray(so, "costumeModeButtons", new Object[]
                {
                    FindNamed<Button>(costumePanel.transform, "ItemMode"),
                    FindNamed<Button>(costumePanel.transform, "SetMode")
                });
                SetArray(so, "costumeCards", System.Array.ConvertAll(
                    costumePanel.GetComponentsInChildren<ShopCostumeCardView>(true), c => (Object)c));
                Set(so, "costumePrevButton", FindNamed<Button>(costumePanel.transform, "Prev"));
                Set(so, "costumeNextButton", FindNamed<Button>(costumePanel.transform, "Next"));
                Set(so, "costumePageLabel", FindNamed<TMP_Text>(costumePanel.transform, "Page"));
                Set(so, "purchaseModal", modal);
                Set(so, "purchaseIcon", modalIcon);
                Set(so, "purchaseTitle", modalTitle);
                Set(so, "purchasePrice", modalPrice);
                Set(so, "purchaseConfirmButton", modalConfirm);
                Set(so, "purchaseCancelButton", modalCancel);
                SetArray(so, "upgradeCards", System.Array.ConvertAll(
                    upgradePanel.GetComponentsInChildren<WeaponUpgradeCardView>(true), c => (Object)c));
                Set(so, "upgradePrevButton", FindNamed<Button>(upgradePanel.transform, "Prev"));
                Set(so, "upgradeNextButton", FindNamed<Button>(upgradePanel.transform, "Next"));
                Set(so, "upgradePageLabel", FindNamed<TMP_Text>(upgradePanel.transform, "Page"));
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, shopPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            CostumeSlotChipInstaller.Ensure();
            AssetDatabase.SaveAssets();
            Debug.Log("[MenuScreensInstaller] Real Costume Shop, weapon upgrades, purchase modal, and Costume BỘ tab authored.");
        }

        static void EnsureWeaponEntries(UIPrototypeCatalog catalog)
        {
            foreach (var wd in UIThumbnailGenerator.LoadAllWeaponData())
            {
                bool exists = false;
                foreach (var e in catalog.weapons)
                    if (e.data == wd) { exists = true; break; }
                if (!exists)
                    catalog.weapons.Add(new UIPrototypeCatalog.WeaponEntry { data = wd, owned = wd.unlockCost <= 0 });
            }
            if (catalog.weaponFallbackIcon == null) catalog.weaponFallbackIcon = K.Icon("Icon_Sword02");
            if (catalog.costumeFallbackIcon == null) catalog.costumeFallbackIcon = K.Icon("Icon_Helmet");
            EditorUtility.SetDirty(catalog);
        }

        // ============================================================ LOADOUT (§4.2)
        static LoadoutScreen BuildLoadout(RectTransform canvas, UIPrototypeCatalog catalog)
        {
            var scr = ScreenRoot<LoadoutScreen>(canvas, "LoadoutScreen", out var safe);

            Bg(scr, safe);
            var back = K.HeaderBack(safe, "LOADOUT", out _);

            var weapons = UIThumbnailGenerator.LoadAllWeaponData();

            // SlotRow — 3 LoadoutSlotView 200×200; slot 0/1 bind 2 súng đầu, slot 2 trống
            var slots = new LoadoutSlotView[3];
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 224;
                slots[i] = SlotCell(safe, $"Slot{i}", new Vector2(x, -160));
                var wd = i < 2 && i < weapons.Count ? weapons[i] : null;
                BakeSlot(slots[i], wd, catalog);
            }

            // InfoPanel
            var info = K.Card(safe, "InfoPanel", A.TC, new Vector2(0, -420), new Vector2(1016, 300), out _, out _, out _);
            var name = K.Text(info, "Name", "—", UITheme.FontSub, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            K.Place(name.rectTransform, A.TL, new Vector2(32, -24), new Vector2(700, 56));

            string[] statNames = { "DMG", "TỐC BẮN", "TẦM" };
            Color[] statCol = { UITheme.Rarity[2], UITheme.Green, UITheme.Gold };
            var bars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float y = -120 - i * 58;
                var lbl = K.Text(info, $"Stat{i}L", statNames[i], UITheme.FontLabel, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                K.Place(lbl.rectTransform, A.TL, new Vector2(32, y), new Vector2(160, 40));
                bars[i] = K.ProgressBar(info, $"Stat{i}Bar", A.TL, new Vector2(200, y - 10), new Vector2(760, 20), 0.5f, statCol[i]);
            }

            // BombRow
            var bomb = K.Card(safe, "BombRow", A.TC, new Vector2(0, -760), new Vector2(1016, 120), out _, out _, out _);
            var bIcon = K.Image(bomb, "BombIcon", K.Circle, UITheme.Danger, false);
            K.Place(bIcon.rectTransform, A.ML, new Vector2(40, 0), new Vector2(72, 72));
            var bTxt = K.Text(bomb, "BombName", "Bom — 1 loại", UITheme.FontBody, UITheme.TextMain, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            K.Place(bTxt.rectTransform, A.ML, new Vector2(140, 0), new Vector2(500, 60));
            var chip = K.Image(bomb, "CountChip", K.Pill, UITheme.Alpha(UITheme.Gold, 0.18f), false);
            K.Place(chip.rectTransform, A.MR, new Vector2(-32, 0), new Vector2(96, 56));
            var chipTxt = K.Text(chip.rectTransform, "Val", "x3", UITheme.FontLabel, UITheme.Gold, FontStyles.Bold);
            K.Full(chipTxt.rectTransform);

            // KHO SỞ HỮU — 1 card / WeaponData (25 khẩu → bake hết, designer thấy đủ trong prefab)
            var kho = K.Text(safe, "KhoLabel", "KHO SỞ HỮU", UITheme.FontLabel, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            K.Place(kho.rectTransform, A.TL, new Vector2(32, -920), new Vector2(400, 40));

            var area = K.StretchTop(K.Rect("KhoArea", safe), 700, -980, 32, 32);
            var content = K.VScroll(safe, "KhoScroll", area, out _);
            Grid(content, new Vector2(312, 312), new Vector2(24, 24), 3, 16);
            var cards = new List<WeaponItemCardView>();
            foreach (var wd in weapons)
                cards.Add(WeaponCard(content, $"Wpn_{wd.name}", new Vector2(312, 312), wd, catalog, showPrice: false));

            // ShopLink — text link nhỏ
            var linkRt = K.Rect("ShopLink", safe);
            K.Place(linkRt, A.BC, new Vector2(0, 40), new Vector2(620, 72));
            var linkHit = linkRt.gameObject.AddComponent<Image>();
            linkHit.color = Color.clear;
            var shopLink = linkRt.gameObject.AddComponent<Button>();
            shopLink.targetGraphic = linkHit;
            var linkTxt = K.Text(linkRt, "Label",
                "<color=#9AA3B2>Chưa có súng?</color>  <color=#F5B841>Tới Shop →</color>",
                34, UITheme.TextDim, FontStyles.Normal);
            K.Full(linkTxt.rectTransform);

            var so = new SerializedObject(scr);
            Set(so, "backButton", back);
            Set(so, "shopLinkButton", shopLink);
            Set(so, "catalog", catalog);
            SetArray(so, "slotViews", System.Array.ConvertAll(slots, s => (Object)s));
            SetArray(so, "ownedCards", cards.ConvertAll(c => (Object)c).ToArray());
            Set(so, "infoNameLabel", name);
            SetArray(so, "statBars", System.Array.ConvertAll(bars, b => (Object)b));
            so.ApplyModifiedPropertiesWithoutUndo();
            scr.gameObject.SetActive(false);
            return scr;
        }

        static LoadoutSlotView SlotCell(RectTransform parent, string name, Vector2 pos)
        {
            var rootRt = K.Rect(name, parent);
            K.Place(rootRt, A.TC, pos, new Vector2(200, 200));
            var glow = K.Image(rootRt, "Glow", K.Glow, UITheme.Alpha(UITheme.Gold, UITheme.GlowAlpha), false);
            K.Full(glow.rectTransform, -24, -24, -24, -24);
            var bg = K.Image(rootRt, "Bg", K.Rounded24, UITheme.Surface);
            K.Full(bg.rectTransform);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var border = K.Image(rootRt, "Border", K.Frame24, UITheme.Hairline, false);
            K.Full(border.rectTransform);
            var icon = K.Image(rootRt, "Icon", K.Rounded24, UITheme.Surface2, false);
            K.Place(icon.rectTransform, A.C, Vector2.zero, new Vector2(140, 140));
            icon.preserveAspect = true;

            var empty = K.Rect("EmptyState", rootRt);
            K.Full(empty);
            var dashed = K.Image(empty, "Dashed", K.Dashed, UITheme.Hairline, false);
            K.Full(dashed.rectTransform);

            var marker = CheckDot(rootRt);

            var view = rootRt.gameObject.AddComponent<LoadoutSlotView>();
            view.button = btn;
            view.icon = icon;
            view.border = border;
            view.glow = glow;
            view.emptyState = empty.gameObject;
            view.selectedMarker = marker;
            return view;
        }

        static void BakeSlot(LoadoutSlotView slot, WeaponData wd, UIPrototypeCatalog catalog)
        {
            bool has = wd != null;
            slot.data = wd;
            slot.emptyState.SetActive(!has);
            slot.icon.gameObject.SetActive(has);
            slot.glow.gameObject.SetActive(false);
            if (!has) return;
            var iconSprite = catalog.GetWeaponIcon(wd);
            if (iconSprite != null) { slot.icon.sprite = iconSprite; slot.icon.color = Color.white; }
            slot.border.color = wd.TierColor;
        }

        static GameObject CheckDot(RectTransform parent)
        {
            var dot = K.Image(parent, "SelectedMarker", K.Circle, UITheme.Green, false);
            K.Place(dot.rectTransform, A.TR, new Vector2(-10, -10), new Vector2(28, 28));
            var inner = K.Image(dot.rectTransform, "Inner", K.Circle, Color.white, false);
            K.Place(inner.rectTransform, A.C, Vector2.zero, new Vector2(12, 12));
            dot.gameObject.SetActive(false);
            return dot.gameObject;
        }

        /// <summary>Card súng dùng chung Loadout/Shop — mọi child serialize vào WeaponItemCardView.</summary>
        static WeaponItemCardView WeaponCard(RectTransform parent, string name, Vector2 size,
            WeaponData wd, UIPrototypeCatalog catalog, bool showPrice)
        {
            var rootRt = K.Rect(name, parent);
            K.Place(rootRt, A.TC, Vector2.zero, size);

            var glow = K.Image(rootRt, "Glow", K.Glow, UITheme.Alpha(wd.TierColor, UITheme.GlowAlpha), false);
            K.Full(glow.rectTransform, -24, -24, -24, -24);
            glow.gameObject.SetActive(false);

            var bg = K.Image(rootRt, "Bg", K.Rounded24, UITheme.Surface);
            K.Full(bg.rectTransform);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            bg.gameObject.AddComponent<UIFxPress>();

            var border = K.Image(rootRt, "Border", K.Frame24, wd.TierColor, false);
            K.Full(border.rectTransform);

            var iconSprite = catalog.GetWeaponIcon(wd);
            var icon = K.Image(rootRt, "Icon", iconSprite != null ? iconSprite : K.Rounded24,
                iconSprite != null ? Color.white : UITheme.Surface2, false);
            K.Place(icon.rectTransform, A.C, new Vector2(0, size.y * 0.12f), size * 0.55f);
            icon.preserveAspect = true;

            var nameLbl = K.Text(rootRt, "Name", wd.weaponName, UITheme.FontLabel, UITheme.TextMain, FontStyles.Bold);
            K.Place(nameLbl.rectTransform, A.BC, new Vector2(0, showPrice ? 74 : 18), new Vector2(size.x - 24, 40));

            bool owned = catalog.IsWeaponOwned(wd);
            int price = Mathf.Max(wd.price, wd.unlockCost);

            var priceChip = K.Image(rootRt, "PriceChip", K.Pill, UITheme.Surface2, false);
            K.Place(priceChip.rectTransform, A.BC, new Vector2(0, 14), new Vector2(Mathf.Min(size.x * 0.62f, 200), 52));
            var coin = K.IconImage(priceChip.rectTransform, "Coin", "Icon_Coin", A.ML, new Vector2(12, 0), new Vector2(32, 32));
            var priceLbl = K.Text(priceChip.rectTransform, "Val", price.ToString("N0"), UITheme.FontLabel, UITheme.Gold, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            K.Full(priceLbl.rectTransform, 48, 0, 16, 0);
            priceChip.gameObject.SetActive(showPrice && !owned && price > 0);

            var badge = K.Image(rootRt, "OwnedBadge", K.Pill, UITheme.Alpha(UITheme.Green, 0.2f), false);
            K.Place(badge.rectTransform, A.BC, new Vector2(0, 14), new Vector2(160, 52));
            var badgeTxt = K.Text(badge.rectTransform, "L", "ĐÃ CÓ", UITheme.FontLabel, UITheme.Green, FontStyles.Bold);
            K.Full(badgeTxt.rectTransform);
            badge.gameObject.SetActive(showPrice && owned);

            var lockOverlay = K.Image(rootRt, "LockOverlay", K.Rounded24, new Color(0, 0, 0, 0.6f), false);
            K.Full(lockOverlay.rectTransform);
            K.LockGlyph(lockOverlay.rectTransform);
            lockOverlay.gameObject.SetActive(!owned && !showPrice);

            var marker = CheckDot(rootRt);

            var view = rootRt.gameObject.AddComponent<WeaponItemCardView>();
            view.data = wd;
            view.button = btn;
            view.icon = icon;
            view.nameLabel = nameLbl;
            view.border = border;
            view.glow = glow;
            view.priceChip = priceChip.gameObject;
            view.priceLabel = priceLbl;
            view.ownedBadge = badge.gameObject;
            view.lockOverlay = lockOverlay.gameObject;
            view.selectedMarker = marker;
            return view;
        }

        // ============================================================ COSTUME (§4.3)
        static CostumeScreen BuildCostume(RectTransform canvas, UIPrototypeCatalog uiCatalog,
            ModularCostumeCatalog costumeCatalog, MenuCharacterStage stage)
        {
            var scr = ScreenRoot<CostumeScreen>(canvas, "CostumeScreen", out var safe);

            Bg(scr, safe);
            var back = K.HeaderBack(safe, "COSTUME", out _);

            // PreviewCard — RawImage preplaced, texture = RT ASSET (không runtime bind/Find)
            var prev = K.Rect("PreviewCard", safe);
            K.Place(prev, A.TC, new Vector2(0, -160), new Vector2(700, 480));
            var pbg = K.Image(prev, "Bg", K.Rounded32, UITheme.Surface2, false);
            K.Full(pbg.rectTransform);
            var dashed = K.Image(prev, "DashedFrame", K.Dashed, UITheme.Hairline, false);
            K.Full(dashed.rectTransform);
            var rawGo = new GameObject("PreviewRT", typeof(RectTransform));
            var rawRt = (RectTransform)rawGo.transform;
            rawRt.SetParent(prev, false);
            K.Full(rawRt, 16, 16, 16, 16);
            var raw = rawGo.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.texture = MenuCharacterStageInstaller.EnsureRenderTexture();
            raw.uvRect = new Rect(0.02f, 0.42f, 0.96f, 0.42f);   // crop band mặt/thân — chỉnh Inspector

            var partTabs = K.SegmentedTabs(safe, "PartTabs", A.TC, new Vector2(0, -680), new Vector2(900, 88),
                new[] { "ĐẦU", "THÂN", "CHÂN", "BỘ" }, UITheme.Green, 0, out var partFills, out var partLabels);

            // Pool 18 cell cố định (catalog ~nghìn part → paging, không Instantiate runtime)
            var area = K.StretchTop(K.Rect("PartArea", safe), 820, -800, 32, 32);
            var content = K.VScroll(safe, "PartScroll", area, out _);
            Grid(content, new Vector2(292, 292), new Vector2(24, 24), 3, 16);
            var cells = new List<CostumeItemCardView>();
            for (int i = 0; i < 18; i++)
                cells.Add(CostumeCell(content, $"Cell{i}", uiCatalog));

            // Page controls
            var pager = K.Rect("Pager", safe);
            K.Place(pager, A.BC, new Vector2(-160, 60), new Vector2(320, 72));
            var prevB = PageArrow(pager, "PagePrev", "Icon_Arrow_Prev1", new Vector2(-120, 0));
            var pageLabel = K.Text(pager, "PageLabel", "1/1", 30, UITheme.TextDim, FontStyles.Bold);
            K.Place(pageLabel.rectTransform, A.C, Vector2.zero, new Vector2(120, 44));
            var nextB = PageArrow(pager, "PageNext", "Icon_Arrow_Next1", new Vector2(120, 0));

            var rnd = K.BtnPrimary(safe, "RandomBtn", "Ngẫu nhiên", new Vector2(420, 110), A.BC, new Vector2(190, 44));
            rnd.GetComponentInChildren<TMP_Text>().fontSize = UITheme.FontSub;

            var so = new SerializedObject(scr);
            Set(so, "backButton", back);
            Set(so, "randomButton", rnd);
            SetArray(so, "partTabs", System.Array.ConvertAll(partTabs, b => (Object)b));
            SetArray(so, "tabFills", System.Array.ConvertAll(partFills, f => (Object)f));
            SetArray(so, "tabLabels", System.Array.ConvertAll(partLabels, l => (Object)l));
            Set(so, "catalog", costumeCatalog);
            Set(so, "uiCatalog", uiCatalog);
            Set(so, "previewStage", stage);
            SetArray(so, "cells", cells.ConvertAll(c => (Object)c).ToArray());
            Set(so, "pagePrevButton", prevB);
            Set(so, "pageNextButton", nextB);
            Set(so, "pageLabel", pageLabel);
            so.ApplyModifiedPropertiesWithoutUndo();
            scr.gameObject.SetActive(false);
            return scr;
        }

        static CostumeItemCardView CostumeCell(RectTransform parent, string name, UIPrototypeCatalog uiCatalog)
        {
            var rootRt = K.Rect(name, parent);
            K.Place(rootRt, A.TC, Vector2.zero, new Vector2(292, 292));
            var bg = K.Image(rootRt, "Bg", K.Rounded24, UITheme.Surface);
            K.Full(bg.rectTransform);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            bg.gameObject.AddComponent<UIFxPress>();
            var border = K.Image(rootRt, "Border", K.Frame24, UITheme.Hairline, false);
            K.Full(border.rectTransform);
            var fallback = uiCatalog != null ? uiCatalog.costumeFallbackIcon : null;
            var icon = K.Image(rootRt, "Icon", fallback != null ? fallback : K.Rounded24,
                fallback != null ? Color.white : UITheme.Surface2, false);
            K.Place(icon.rectTransform, A.C, new Vector2(0, 24), new Vector2(160, 160));
            icon.preserveAspect = true;
            var nameLbl = K.Text(rootRt, "Name", "—", 24, UITheme.TextMain, FontStyles.Bold);
            K.Place(nameLbl.rectTransform, A.BC, new Vector2(0, 16), new Vector2(268, 36));
            var marker = CheckDot(rootRt);

            var view = rootRt.gameObject.AddComponent<CostumeItemCardView>();
            view.button = btn;
            view.icon = icon;
            view.nameLabel = nameLbl;
            view.border = border;
            view.selectedMarker = marker;
            return view;
        }

        static Button PageArrow(RectTransform parent, string name, string iconName, Vector2 pos)
        {
            var bg = K.Image(parent, name, K.Rounded24, UITheme.Surface);
            K.Place(bg.rectTransform, A.C, pos, new Vector2(72, 72));
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            K.IconImage(bg.rectTransform, "Icon", iconName, A.C, Vector2.zero, new Vector2(36, 36));
            return b;
        }

        // ============================================================ SHOP (§4.4)
        static ShopScreen BuildShop(RectTransform canvas, UIPrototypeCatalog catalog)
        {
            var scr = ScreenRoot<ShopScreen>(canvas, "ShopScreen", out var safe);

            Bg(scr, safe);
            var back = K.HeaderBack(safe, "SHOP", out _);

            var tabs = K.SegmentedTabs(safe, "Tabs", A.TC, new Vector2(0, -140), new Vector2(1016, 88),
                new[] { "WEAPONS", "GACHA", "COSTUME", "UPGRADES" }, UITheme.Green, 0, out var fills, out var labels);

            var weaponCards = new List<WeaponItemCardView>();
            var panels = new GameObject[4];
            panels[0] = BuildShopWeapons(safe, catalog, weaponCards);
            panels[1] = BuildShopGacha(safe);
            panels[2] = BuildShopCostumeReal(safe);
            panels[3] = BuildShopUpgradesReal(safe);
            var modal = BuildShopPurchaseModal((RectTransform)scr.transform,
                out var modalIcon, out var modalTitle, out var modalPrice, out var modalConfirm, out var modalCancel);

            var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(
                "Assets/_Project/Data/Economy/EconomyConfig.asset");

            var so = new SerializedObject(scr);
            Set(so, "backButton", back);
            SetArray(so, "tabButtons", System.Array.ConvertAll(tabs, b => (Object)b));
            SetArray(so, "tabPanels", panels);
            SetArray(so, "tabFills", System.Array.ConvertAll(fills, f => (Object)f));
            SetArray(so, "tabLabels", System.Array.ConvertAll(labels, l => (Object)l));
            SetColorArray(so, "tabActiveColors",
                new[] { UITheme.Green, UITheme.Gold, UITheme.Green, UITheme.Green });
            SetArray(so, "weaponCards", weaponCards.ConvertAll(c => (Object)c).ToArray());
            Set(so, "catalog", catalog);
            Set(so, "economy", economy);
            SetArray(so, "costumeModeButtons", new Object[]
            {
                FindNamed<Button>(panels[2].transform, "ItemMode"),
                FindNamed<Button>(panels[2].transform, "SetMode")
            });
            SetArray(so, "costumeCards", System.Array.ConvertAll(
                panels[2].GetComponentsInChildren<ShopCostumeCardView>(true), c => (Object)c));
            Set(so, "costumePrevButton", FindNamed<Button>(panels[2].transform, "Prev"));
            Set(so, "costumeNextButton", FindNamed<Button>(panels[2].transform, "Next"));
            Set(so, "costumePageLabel", FindNamed<TMP_Text>(panels[2].transform, "Page"));
            Set(so, "purchaseModal", modal);
            Set(so, "purchaseIcon", modalIcon);
            Set(so, "purchaseTitle", modalTitle);
            Set(so, "purchasePrice", modalPrice);
            Set(so, "purchaseConfirmButton", modalConfirm);
            Set(so, "purchaseCancelButton", modalCancel);
            SetArray(so, "upgradeCards", System.Array.ConvertAll(
                panels[3].GetComponentsInChildren<WeaponUpgradeCardView>(true), c => (Object)c));
            Set(so, "upgradePrevButton", FindNamed<Button>(panels[3].transform, "Prev"));
            Set(so, "upgradeNextButton", FindNamed<Button>(panels[3].transform, "Next"));
            Set(so, "upgradePageLabel", FindNamed<TMP_Text>(panels[3].transform, "Page"));
            so.ApplyModifiedPropertiesWithoutUndo();
            scr.gameObject.SetActive(false);
            return scr;
        }

        static RectTransform ShopContent(RectTransform safe, string name, out RectTransform content)
        {
            var panel = K.Full(K.Rect(name, safe), 0, 260, 0, 32);
            content = K.VScroll(panel, "Scroll", panel, out _);
            return panel;
        }

        static LayoutElement LE(Component c, float minHeight)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            return le;
        }

        static string SectionName(WeaponClass cls) => cls switch
        {
            WeaponClass.Sidearm => "PISTOL",
            WeaponClass.AssaultRifle => "RIFLE",
            _ => cls.ToString().ToUpperInvariant(),
        };

        static GameObject BuildShopWeapons(RectTransform safe, UIPrototypeCatalog catalog, List<WeaponItemCardView> outCards)
        {
            var panel = ShopContent(safe, "TabWeapons", out var content);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16; vlg.padding = new RectOffset(24, 24, 8, 8);

            // group toàn bộ WeaponData theo class — mỗi section grid 2 cột
            var byClass = new SortedDictionary<int, List<WeaponData>>();
            foreach (var wd in UIThumbnailGenerator.LoadAllWeaponData())
            {
                int key = (int)wd.weaponClass;
                if (!byClass.TryGetValue(key, out var list)) byClass[key] = list = new List<WeaponData>();
                list.Add(wd);
            }

            foreach (var kv in byClass)
            {
                string sec = SectionName((WeaponClass)kv.Key);
                var sl = K.Text(content, $"Sec_{sec}", sec, UITheme.FontLabel, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                LE(sl, 48);
                var grid = K.Rect($"Grid_{sec}", content);
                var g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(476, 340); g.spacing = new Vector2(24, 24);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 2;
                g.childAlignment = TextAnchor.UpperCenter;
                int rows = Mathf.CeilToInt(kv.Value.Count / 2f);
                LE(grid, rows * 340 + (rows - 1) * 24);
                foreach (var wd in kv.Value)
                    outCards.Add(WeaponCard(grid, $"Shop_{wd.name}", new Vector2(476, 340), wd, catalog, showPrice: true));
            }
            panel.gameObject.SetActive(true);
            return panel.gameObject;
        }

        static GameObject BuildShopGacha(RectTransform safe)
        {
            var panel = ShopContent(safe, "TabGacha", out var content);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 32;
            GachaBanner(content, "SungGacha", "Súng Gacha", "Rớt tỉ lệ theo Tier — xem chi tiết",
                UITheme.Rarity[4], UITheme.Gold, UITheme.GoldLo, UITheme.OnGold, "Quay 1 · 100", "Quay 10 · 900");
            GachaBanner(content, "SkinGacha", "Skin Gacha", "Skin theo bộ — trùng hoàn KC",
                UITheme.Cyan, UITheme.Cyan, new Color(0.23f, 0.6f, 0.66f), UITheme.OnGold, "Quay 1 · 10", "Quay 10 · 90");
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static void GachaBanner(RectTransform content, string name, string title, string rate,
            Color accent, Color btnFace, Color btnEdge, Color btnText, string b1, string b2)
        {
            var card = K.Card(content, name, A.TC, Vector2.zero, new Vector2(952, 560), out var glow, out var border, out _);
            LE(card, 560);
            glow.color = UITheme.Alpha(accent, UITheme.GlowAlpha); glow.gameObject.SetActive(true);
            border.color = accent; border.gameObject.SetActive(true);

            var t = K.Text(card, "Title", title, UITheme.FontSub, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.Center);
            K.Place(t.rectTransform, A.TC, new Vector2(0, -28), new Vector2(800, 60));

            // art cổng gacha: placeholder đúng footprint + icon quà semantic (artwork thật thay sau)
            var art = K.Image(card, "Art", K.Rounded32, UITheme.Bg, false);
            K.Place(art.rectTransform, A.TC, new Vector2(0, -104), new Vector2(360, 260));
            var arch = K.Image(art.rectTransform, "Arch", K.Circle, UITheme.Surface2, false);
            K.Place(arch.rectTransform, A.BC, new Vector2(0, 20), new Vector2(200, 200));
            K.IconImage(art.rectTransform, "GiftIcon", "BtnIcon_Gift", A.C, new Vector2(0, 10), new Vector2(110, 110));

            var r = K.Text(card, "Rate", rate, 30, UITheme.TextDim, FontStyles.Normal, TextAlignmentOptions.Center);
            K.Place(r.rectTransform, A.BC, new Vector2(0, 148), new Vector2(800, 40));

            K.Button(card, "Quay1", b1, new Vector2(440, 96), A.BC, new Vector2(-236, 28), btnFace, btnEdge, btnText, UITheme.FontLabel + 4);
            K.Button(card, "Quay10", b2, new Vector2(440, 96), A.BC, new Vector2(236, 28), btnFace, btnEdge, btnText, UITheme.FontLabel + 4);
        }

        static GameObject BuildShopCostumeReal(RectTransform safe)
        {
            var panel = ShopContent(safe, "TabCostume", out var content);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 20; vlg.padding = new RectOffset(24, 24, 8, 8);

            var switcher = K.Rect("OfferMode", content);
            LE(switcher, 88);
            var itemMode = K.Button(switcher, "ItemMode", "ITEM LẺ", new Vector2(440, 76), A.ML,
                new Vector2(8, 0), UITheme.Green, UITheme.GreenLo, Color.white, UITheme.FontLabel);
            var setMode = K.Button(switcher, "SetMode", "BỘ", new Vector2(440, 76), A.MR,
                new Vector2(-8, 0), UITheme.Surface2, UITheme.Hairline, UITheme.TextMain, UITheme.FontLabel);

            var note = K.Text(content, "Hint", "Chọn món hoặc bộ · xác nhận giá trước khi mua",
                UITheme.FontBody, UITheme.TextDim, FontStyles.Normal, TextAlignmentOptions.Center);
            LE(note, 44);

            var grid = K.Rect("OfferGrid", content);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(452, 290); gl.spacing = new Vector2(20, 20);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 2;
            gl.childAlignment = TextAnchor.UpperCenter;
            LE(grid, 4 * 290 + 3 * 20);
            for (int i = 0; i < 8; i++) BuildShopCostumeCard(grid, i);

            BuildPager(content);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static void BuildShopCostumeCard(RectTransform parent, int index)
        {
            var card = K.Card(parent, $"CostumeOffer{index}", A.TC, Vector2.zero,
                new Vector2(452, 290), out _, out var border, out _);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            var icon = K.Image(card, "Icon", K.Rounded24, Color.white, false);
            K.Place(icon.rectTransform, A.TC, new Vector2(0, -18), new Vector2(230, 150));
            icon.preserveAspect = true;
            var name = K.Text(card, "Name", "Costume", 28, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.Center);
            K.Place(name.rectTransform, A.BC, new Vector2(0, 64), new Vector2(400, 48));
            var price = K.Text(card, "Price", "C 0", 26, UITheme.Gold,
                FontStyles.Bold, TextAlignmentOptions.Center);
            K.Place(price.rectTransform, A.BC, new Vector2(0, 18), new Vector2(320, 42));
            var owned = K.Image(card, "Owned", K.Pill, UITheme.Green, false);
            K.Place(owned.rectTransform, A.TR, new Vector2(-12, -12), new Vector2(116, 42));
            var ownedText = K.Text(owned.rectTransform, "Label", "ĐÃ CÓ", 20, Color.white,
                FontStyles.Bold, TextAlignmentOptions.Center);
            K.Full(ownedText.rectTransform);

            var view = card.gameObject.AddComponent<ShopCostumeCardView>();
            view.button = button; view.icon = icon; view.border = border; view.nameLabel = name;
            view.priceLabel = price; view.ownedBadge = owned.gameObject;
        }

        static GameObject BuildShopUpgradesReal(RectTransform safe)
        {
            var panel = ShopContent(safe, "TabUpgrades", out var content);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 20; vlg.padding = new RectOffset(24, 24, 8, 8);

            var title = K.Text(content, "UpgradeTitle", "NÂNG CẤP VŨ KHÍ",
                UITheme.FontSub, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.Center);
            LE(title, 58);
            var hint = K.Text(content, "UpgradeHint", "Súng trùng → mảnh · mảnh + Vàng tăng sao",
                UITheme.FontBody, UITheme.TextDim, FontStyles.Normal, TextAlignmentOptions.Center);
            LE(hint, 62);

            var grid = K.Rect("UpgradeGrid", content);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(452, 330); gl.spacing = new Vector2(20, 20);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 2;
            gl.childAlignment = TextAnchor.UpperCenter;
            LE(grid, 3 * 330 + 2 * 20);
            for (int i = 0; i < 6; i++) BuildWeaponUpgradeCard(grid, i);

            BuildPager(content);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static void BuildWeaponUpgradeCard(RectTransform parent, int index)
        {
            var card = K.Card(parent, $"WeaponUpgrade{index}", A.TC, Vector2.zero,
                new Vector2(452, 330), out _, out var border, out _);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            var icon = K.Image(card, "Icon", K.Rounded24, Color.white, false);
            K.Place(icon.rectTransform, A.TL, new Vector2(20, -20), new Vector2(150, 110));
            icon.preserveAspect = true;
            var name = K.Text(card, "Name", "Weapon", 27, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(name.rectTransform, A.TR, new Vector2(-20, -22), new Vector2(250, 48));
            var level = K.Text(card, "Level", "CẤP 1/3", 28, UITheme.Gold,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(level.rectTransform, A.TR, new Vector2(-20, -72), new Vector2(250, 42));
            var stats = K.Text(card, "Stats", "DMG 0 → 0\nROF 0 → 0", 25, UITheme.TextDim,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            K.Place(stats.rectTransform, A.BL, new Vector2(24, 96), new Vector2(400, 82));
            var cost = K.Text(card, "Cost", "0/10 mảnh · 100 V", 24, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.Center);
            K.Place(cost.rectTransform, A.BC, new Vector2(0, 28), new Vector2(400, 52));

            var view = card.gameObject.AddComponent<WeaponUpgradeCardView>();
            view.button = button; view.icon = icon; view.border = border; view.nameLabel = name;
            view.levelLabel = level; view.statLabel = stats; view.resourceLabel = cost;
        }

        static void BuildPager(RectTransform parent)
        {
            var pager = K.Rect("Pager", parent);
            LE(pager, 84);
            K.Button(pager, "Prev", "‹", new Vector2(160, 68), A.ML, new Vector2(120, 0),
                UITheme.Surface2, UITheme.Hairline, UITheme.TextMain, 36);
            var page = K.Text(pager, "Page", "1/1", 28, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.Center);
            K.Place(page.rectTransform, A.C, Vector2.zero, new Vector2(240, 68));
            K.Button(pager, "Next", "›", new Vector2(160, 68), A.MR, new Vector2(-120, 0),
                UITheme.Surface2, UITheme.Hairline, UITheme.TextMain, 36);
        }

        static GameObject BuildShopPurchaseModal(RectTransform parent, out Image icon, out TMP_Text title,
            out TMP_Text price, out Button confirm, out Button cancel)
        {
            var root = K.Image(parent, "PurchaseModal", K.Rounded24, new Color(0, 0, 0, 0.82f), false);
            K.Full(root.rectTransform);
            var panel = K.Card(root.rectTransform, "Panel", A.C, Vector2.zero, new Vector2(820, 720),
                out var glow, out var border, out _);
            glow.color = UITheme.Alpha(UITheme.Cyan, UITheme.GlowAlpha); glow.gameObject.SetActive(true);
            border.color = UITheme.Cyan; border.gameObject.SetActive(true);
            title = K.Text(panel, "Title", "Mua item?", UITheme.FontSub, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.Center);
            K.Place(title.rectTransform, A.TC, new Vector2(0, -44), new Vector2(720, 80));
            icon = K.Image(panel, "Icon", K.Rounded24, Color.white, false);
            K.Place(icon.rectTransform, A.C, new Vector2(0, 30), new Vector2(300, 260));
            icon.preserveAspect = true;
            price = K.Text(panel, "Price", "C 0", 40, UITheme.Gold,
                FontStyles.Bold, TextAlignmentOptions.Center);
            K.Place(price.rectTransform, A.BC, new Vector2(0, 170), new Vector2(500, 64));
            cancel = K.Button(panel, "Cancel", "HỦY", new Vector2(320, 96), A.BL, new Vector2(40, 40),
                UITheme.Surface2, UITheme.Hairline, UITheme.TextMain, UITheme.FontLabel);
            confirm = K.Button(panel, "Confirm", "MUA", new Vector2(320, 96), A.BR, new Vector2(-40, 40),
                UITheme.Green, UITheme.GreenLo, Color.white, UITheme.FontLabel);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        static T FindNamed<T>(Transform root, string name) where T : Component
        {
            if (root == null) return null;
            foreach (var component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            return null;
        }

        static GameObject BuildShopCostume(RectTransform safe, UIPrototypeCatalog catalog)
        {
            var panel = ShopContent(safe, "TabCostume", out var content);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 24;

            var set = K.Card(content, "SetCard", A.TC, Vector2.zero, new Vector2(952, 360), out var glow, out var border, out _);
            LE(set, 360);
            glow.color = UITheme.Alpha(UITheme.Rarity[3], UITheme.GlowAlpha); glow.gameObject.SetActive(true);
            border.color = UITheme.Rarity[3]; border.gameObject.SetActive(true);
            var t = K.Text(set, "SetName", "Bộ Sắt Thế Thân Đêm", UITheme.FontSub, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.Center);
            K.Place(t.rectTransform, A.TC, new Vector2(0, -32), new Vector2(800, 60));
            var d = K.Text(set, "SetDesc", "5 món — SET giảm 30%", UITheme.FontBody, UITheme.TextDim, FontStyles.Normal, TextAlignmentOptions.Center);
            K.Place(d.rectTransform, A.C, new Vector2(0, 10), new Vector2(700, 48));
            var price = K.Image(set, "PriceChip", K.Pill, UITheme.Alpha(UITheme.Cyan, 0.15f), false);
            K.Place(price.rectTransform, A.BC, new Vector2(0, 28), new Vector2(220, 64));
            K.IconImage(price.rectTransform, "Gem", "Icon_Gem", A.ML, new Vector2(16, 0), new Vector2(36, 36));
            var pv = K.Text(price.rectTransform, "Val", "300", 34, UITheme.Cyan, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            K.Full(pv.rectTransform, 60, 0, 24, 0);

            var il = K.Text(content, "ItemLe", "ITEM LẺ", UITheme.FontLabel, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LE(il, 48);

            var row = K.Rect("ItemRow", content);
            LE(row, 260);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var fallback = catalog != null ? catalog.costumeFallbackIcon : null;
            for (int i = 0; i < 4; i++)
            {
                var cellRt = K.Rect($"Item{i}", row);
                cellRt.sizeDelta = new Vector2(220, 220);
                var bg = K.Image(cellRt, "Bg", K.Rounded24, UITheme.Surface);
                K.Full(bg.rectTransform);
                var b = K.Image(cellRt, "Border", K.Frame24, UITheme.RarityColor((i % 4) + 1), false);
                K.Full(b.rectTransform);
                var ic = K.Image(cellRt, "Icon", fallback != null ? fallback : K.Rounded24,
                    fallback != null ? Color.white : UITheme.Surface2, false);
                K.Place(ic.rectTransform, A.C, new Vector2(0, 16), new Vector2(120, 120));
                ic.preserveAspect = true;
                if (i >= 1)
                {
                    var chipBg = K.Image(cellRt, "PriceChip", K.Pill, UITheme.Surface2, false);
                    K.Place(chipBg.rectTransform, A.BC, new Vector2(0, 10), new Vector2(130, 44));
                    K.IconImage(chipBg.rectTransform, "Coin", "Icon_Coin", A.ML, new Vector2(8, 0), new Vector2(26, 26));
                    var val = K.Text(chipBg.rectTransform, "Val", "150", 26, UITheme.Gold, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
                    K.Full(val.rectTransform, 36, 0, 12, 0);
                }
                if (i >= 2)
                {
                    var mask = K.Image(cellRt, "LockMask", K.Rounded24, new Color(0, 0, 0, 0.6f), false);
                    K.Full(mask.rectTransform);
                    K.LockGlyph(mask.rectTransform, 48);
                }
            }
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static GameObject BuildShopUpgrades(RectTransform safe)
        {
            var panel = ShopContent(safe, "TabUpgrades", out var content);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 20;

            var header = K.Rect("PowerHeader", content);
            LE(header, 170);
            var tl = K.Text(header, "TotalLbl", "TỔNG SỨC MẠNH", UITheme.FontLabel, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            K.Place(tl.rectTransform, A.TL, new Vector2(8, -8), new Vector2(500, 40));
            var tv = K.Text(header, "TotalVal", "+42%", UITheme.FontHeader, UITheme.Green, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            K.Place(tv.rectTransform, A.BL, new Vector2(8, 8), new Vector2(400, 100));

            (string label, string icon)[] rows = { ("Sát thương", "Icon_Sword02"), ("Máu", "Icon_Heart"), ("Tốc bắn", "Icon_Fire01") };
            int[] filled = { 3, 5, 4 };
            for (int i = 0; i < rows.Length; i++)
            {
                var row = K.Card(content, $"Up{i}", A.TC, Vector2.zero, new Vector2(1016, 176), out _, out _, out _);
                LE(row, 176);
                var iconBg = K.Image(row, "IconBg", K.Rounded24, UITheme.Surface2, false);
                K.Place(iconBg.rectTransform, A.ML, new Vector2(32, 0), new Vector2(88, 88));
                K.IconImage(iconBg.rectTransform, "Icon", rows[i].icon, A.C, Vector2.zero, new Vector2(56, 56));
                var nm = K.Text(row, "Name", rows[i].label, UITheme.FontBody, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                K.Place(nm.rectTransform, A.ML, new Vector2(152, 32), new Vector2(400, 48));
                K.ProgressPips(row, "Pips", A.ML, new Vector2(152, -28), 5, filled[i]);
                if (filled[i] >= 5)
                {
                    var max = K.Image(row, "MaxChip", K.Pill, UITheme.Surface2, false);
                    K.Place(max.rectTransform, A.MR, new Vector2(-32, 0), new Vector2(180, 84));
                    var mt = K.Text(max.rectTransform, "L", "MAX", UITheme.FontLabel, UITheme.TextDim, FontStyles.Bold);
                    K.Full(mt.rectTransform);
                }
                else
                {
                    var up = K.BtnPrimary(row, "UpBtn", $"NÂNG · {(i == 0 ? 200 : 150)}", new Vector2(260, 96), A.MR, new Vector2(-24, 0));
                    up.GetComponentInChildren<TMP_Text>().fontSize = 32;
                }
            }
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        // ============================================================ wiring + helpers
        static void WireHub(LoadoutScreen loadout, ShopScreen shop, CostumeScreen costume)
        {
            var hub = FindInactive<HubScreen>();
            if (hub == null) { Debug.LogWarning("[MenuScreensInstaller] Không thấy HubScreen để wire nav."); return; }
            var so = new SerializedObject(hub);
            Set(so, "loadoutScreen", loadout);
            Set(so, "shopScreen", shop);
            Set(so, "costumeScreen", costume);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Pass (nếu đã build) phải trỏ Shop instance MỚI — rebuild Menu screens không được làm Pass stale
            var pass = FindInactive<PassScreen>();
            if (pass != null)
            {
                var soPass = new SerializedObject(pass);
                Set(soPass, "shopScreen", shop);
                soPass.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void WireShopLink(LoadoutScreen loadout, ShopScreen shop)
        {
            var so = new SerializedObject(loadout);
            Set(so, "shopScreen", shop);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Bg(UIScreen scr, RectTransform safe)
        {
            var root = (RectTransform)scr.transform;
            var bg = K.Image(root, "Bg", null, UITheme.Bg, false);
            K.Full(bg.rectTransform);
            bg.transform.SetAsFirstSibling();
        }

        static T ScreenRoot<T>(RectTransform canvas, string name, out RectTransform safe) where T : UIScreen
        {
            var existing = canvas.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            Stretch(rt);
            var scr = go.AddComponent<T>();

            var safeGo = new GameObject("Safe", typeof(RectTransform));
            safe = (RectTransform)safeGo.transform;
            safe.SetParent(rt, false);
            Stretch(safe);
            safeGo.AddComponent<SafeArea>();
            return scr;
        }

        static void Grid(RectTransform content, Vector2 cell, Vector2 spacing, int cols, float pad)
        {
            var go = content.gameObject;
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) Object.DestroyImmediate(vlg);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) Object.DestroyImmediate(hlg);

            var g = go.GetComponent<GridLayoutGroup>();
            if (g == null) g = go.AddComponent<GridLayoutGroup>();
            g.cellSize = cell; g.spacing = spacing;
            g.padding = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = cols;

            var fit = go.GetComponent<ContentSizeFitter>();
            if (fit == null) fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static bool EnsureScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path == MenuScene) return false;
            if (!File.Exists(MenuScene)) { Debug.LogError($"[MenuScreensInstaller] Không thấy {MenuScene}"); return true; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return true;
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            return false;
        }

        static T FindInactive<T>() where T : Component => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        static void Set(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"[MenuScreensInstaller] Thiếu field '{prop}' trên {so.targetObject.GetType().Name}");
        }

        static void SetArray(SerializedObject so, string prop, Object[] values)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[MenuScreensInstaller] Thiếu array '{prop}'"); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void SetColorArray(SerializedObject so, string prop, Color[] colors)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[MenuScreensInstaller] Thiếu array '{prop}'"); return; }
            p.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++)
                p.GetArrayElementAtIndex(i).colorValue = colors[i];
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
