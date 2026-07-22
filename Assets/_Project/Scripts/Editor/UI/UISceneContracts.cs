using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZombieWar.EditorTools;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Scene contract cho UI authoring:
    /// - Ensure = NON-DESTRUCTIVE: chỉ tạo instance/reference THIẾU (từ prefab), không đụng
    ///   object/manual edit đang có, log mọi thay đổi.
    /// - Validate = READ-ONLY: báo pass/fail từng mục, không sửa gì.
    /// </summary>
    public static class UISceneContracts
    {
        const string MenuScene = "Assets/_Project/Scenes/Menu.unity";
        const string MapScene = "Assets/_Project/Scenes/Map_Level1.unity";

        static readonly (string name, System.Type type)[] MenuScreens =
        {
            ("HubScreen", typeof(HubScreen)),
            ("LoadoutScreen", typeof(LoadoutScreen)),
            ("CostumeScreen", typeof(CostumeScreen)),
            ("ShopScreen", typeof(ShopScreen)),
            ("PassScreen", typeof(PassScreen)),
        };

        // ================================================================ ENSURE

        [MenuItem("ZombieWar/UI/Authoring/Ensure Menu Scene Contract")]
        public static void EnsureMenu()
        {
            var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            bool dirty = false;

            var canvasGo = GameObject.Find("UIRoot");
            if (canvasGo == null)
            {
                UIRootInstaller.EnsureRoot();
                canvasGo = GameObject.Find("UIRoot");
                dirty = true;
                Debug.Log("[Ensure] Tạo UIRoot (thiếu).");
            }
            var canvasRt = (RectTransform)canvasGo.transform;

            foreach (var (name, type) in MenuScreens)
            {
                if (Object.FindFirstObjectByType(type, FindObjectsInactive.Include) != null) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{UIPrefabizer.ScreensDir}/UI_{name}.prefab");
                if (prefab == null)
                {
                    Debug.LogWarning($"[Ensure] Menu thiếu {name} và chưa có prefab UI_{name} — chạy Rebuild (Destructive) để scaffold lần đầu.");
                    continue;
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.transform.SetParent(canvasRt, false);
                inst.name = name;
                dirty = true;
                Debug.Log($"[Ensure] Instantiate {name} từ prefab (thiếu trong scene).");
            }

            if (MenuCharacterStageInstaller.EnsureInOpenScene() != null) { }

            // wire các reference đang NULL (không overwrite reference đã gán)
            var hub = Object.FindFirstObjectByType<HubScreen>(FindObjectsInactive.Include);
            var loadout = Object.FindFirstObjectByType<LoadoutScreen>(FindObjectsInactive.Include);
            var costume = Object.FindFirstObjectByType<CostumeScreen>(FindObjectsInactive.Include);
            var shop = Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            var pass = Object.FindFirstObjectByType<PassScreen>(FindObjectsInactive.Include);
            var stage = Object.FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);

            dirty |= WireIfNull(hub, "loadoutScreen", loadout);
            dirty |= WireIfNull(hub, "shopScreen", shop);
            dirty |= WireIfNull(hub, "costumeScreen", costume);
            dirty |= WireIfNull(hub, "passScreen", pass);
            dirty |= WireIfNull(loadout, "shopScreen", shop);
            dirty |= WireIfNull(pass, "shopScreen", shop);
            dirty |= WireIfNull(costume, "previewStage", stage);

            var mgr = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            dirty |= WireIfNull(mgr, "initialScreen", hub);

            // RawImage preview texture null → gán RT asset
            var rt = MenuCharacterStageInstaller.EnsureRenderTexture();
            foreach (var rawImg in Object.FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rawImg.texture != null) continue;
                if (rawImg.name != "CharacterRT" && rawImg.name != "PreviewRT") continue;
                rawImg.texture = rt;
                EditorUtility.SetDirty(rawImg);
                dirty = true;
                Debug.Log($"[Ensure] Gán RT asset cho RawImage '{GetPath(rawImg.transform)}' (null).");
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Ensure] Menu contract: đã sửa phần thiếu + save.");
            }
            else Debug.Log("[Ensure] Menu contract: đủ — không đổi gì.");
        }

        [MenuItem("ZombieWar/UI/Authoring/Ensure Gameplay HUD Contract")]
        public static void EnsureHud()
        {
            var scene = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
            bool dirty = false;

            var hud = GameObject.Find("HUD");
            if (hud == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{UIPrefabizer.ScreensDir}/UI_Hud.prefab");
                if (prefab == null)
                {
                    Debug.LogWarning("[Ensure] Map thiếu HUD và chưa có prefab UI_Hud — chạy Rebuild HUD (Destructive) để scaffold.");
                    return;
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.name = "HUD";
                dirty = true;
                Debug.Log("[Ensure] Instantiate HUD từ prefab (thiếu trong scene).");
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else Debug.Log("[Ensure] HUD contract: đủ — không đổi gì.");
        }

        static bool WireIfNull(Component target, string field, Object value)
        {
            if (target == null || value == null) return false;
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue != null) return false;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Ensure] Wire {target.GetType().Name}.{field} → {value.name} (null trước đó).");
            return true;
        }

        // ================================================================ VALIDATE (read-only)

        [MenuItem("ZombieWar/UI/Authoring/Validate All UI References")]
        public static void ValidateAll()
        {
            var report = new StringBuilder();
            int fails = 0;

            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            fails += ValidateMenu(report);
            EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
            fails += ValidateMap(report);
            fails += ValidateAssets(report);

            if (fails == 0) Debug.Log("[Validate] PASS — toàn bộ UI contract OK.\n" + report);
            else Debug.LogError($"[Validate] FAIL — {fails} lỗi:\n" + report);
        }

        static int ValidateMenu(StringBuilder r)
        {
            int fails = 0;
            void Fail(string m) { r.AppendLine("  ✗ Menu: " + m); fails++; }
            void Ok(string m) => r.AppendLine("  ✓ Menu: " + m);

            var canvases = GameObject.Find("UIRoot");
            if (canvases == null) { Fail("thiếu UIRoot"); return fails; }
            var mgrs = Object.FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (mgrs.Length != 1) Fail($"UIManager count = {mgrs.Length} (phải 1)"); else Ok("1 UIManager");
            var ess = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (ess.Length != 1) Fail($"EventSystem count = {ess.Length} (phải 1)"); else Ok("1 EventSystem");

            foreach (var (name, type) in MenuScreens)
            {
                var obj = Object.FindFirstObjectByType(type, FindObjectsInactive.Include) as Component;
                if (obj == null) { Fail($"thiếu screen {name}"); continue; }
                if (PrefabUtility.GetCorrespondingObjectFromSource(obj.gameObject) == null)
                    Fail($"{name} KHÔNG phải prefab instance");
                else Ok($"{name} là prefab instance");
            }

            if (mgrs.Length > 0)
                CheckRef(mgrs[0], "initialScreen", r, ref fails);
            var hub = Object.FindFirstObjectByType<HubScreen>(FindObjectsInactive.Include);
            if (hub != null)
                foreach (var f in new[] { "playButton", "loadoutButton", "shopButton", "costumeButton", "passButton",
                                          "recordLabel", "loadoutScreen", "shopScreen", "costumeScreen", "passScreen" })
                    CheckRef(hub, f, r, ref fails);
            var loadout = Object.FindFirstObjectByType<LoadoutScreen>(FindObjectsInactive.Include);
            if (loadout != null)
                foreach (var f in new[] { "backButton", "shopLinkButton", "shopScreen", "catalog", "infoNameLabel" })
                    CheckRef(loadout, f, r, ref fails);
            var shop = Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            if (shop != null)
            {
                foreach (var f in new[] { "backButton", "catalog" })
                    CheckRef(shop, f, r, ref fails);
                // Card coverage: đủ 25 khẩu roster, không null/trùng WeaponData.
                var soShop = new SerializedObject(shop);
                var cardsProp = soShop.FindProperty("weaponCards");
                if (cardsProp == null || !cardsProp.isArray || cardsProp.arraySize == 0)
                    Fail("ShopScreen.weaponCards rỗng");
                else
                {
                    var seenData = new System.Collections.Generic.HashSet<Object>();
                    int nulls = 0, dups = 0;
                    for (int i = 0; i < cardsProp.arraySize; i++)
                    {
                        var view = cardsProp.GetArrayElementAtIndex(i).objectReferenceValue as ZombieWar.UI.WeaponItemCardView;
                        if (view == null || view.data == null) { nulls++; continue; }
                        if (!seenData.Add(view.data)) dups++;
                    }
                    if (nulls > 0) Fail($"ShopScreen có {nulls} card null/thiếu WeaponData");
                    if (dups > 0) Fail($"ShopScreen có {dups} card trùng WeaponData");
                    var cat = AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(UIThumbnailGenerator.CatalogAssetPath);
                    if (cat != null && seenData.Count != cat.weapons.Count)
                        Fail($"ShopScreen card ({seenData.Count}) ≠ roster catalog ({cat.weapons.Count})");
                    else if (nulls == 0 && dups == 0) Ok($"ShopScreen {seenData.Count} card khớp roster");
                }
            }

            var costume = Object.FindFirstObjectByType<CostumeScreen>(FindObjectsInactive.Include);
            if (costume != null)
            {
                foreach (var f in new[] { "backButton", "randomButton", "resetOutfitButton", "catalog", "uiCatalog",
                                          "previewStage", "dragRotator", "pagePrevButton", "pageNextButton", "pageLabel" })
                    CheckRef(costume, f, r, ref fails);

                // Slice 4: hang chip 14 logical slot phai duoc author + wire (8 chip, khong null).
                var soCostume = new SerializedObject(costume);
                var chipsProp = soCostume.FindProperty("slotChips");
                if (chipsProp == null || chipsProp.arraySize < 8)
                    Fail($"CostumeScreen.slotChips = {(chipsProp == null ? "thiếu field" : chipsProp.arraySize + " chip")} (cần 8 — chạy Ensure Costume Slot Selector)");
                else
                {
                    int nullChips = 0;
                    for (int i = 0; i < chipsProp.arraySize; i++)
                        if (chipsProp.GetArrayElementAtIndex(i).objectReferenceValue == null) nullChips++;
                    if (nullChips > 0) Fail($"CostumeScreen.slotChips có {nullChips} chip null");
                    else Ok("CostumeScreen 8 slot chip wired");
                }
                var cellsProp = soCostume.FindProperty("cells");
                if (cellsProp != null && cellsProp.arraySize > 50)
                    Fail($"CostumeScreen.cells = {cellsProp.arraySize} (pool phải bounded, không bake 978 cell)");
            }
            var pass = Object.FindFirstObjectByType<PassScreen>(FindObjectsInactive.Include);
            if (pass != null)
                foreach (var f in new[] { "backButton", "gachaLinkButton", "shopScreen" })
                    CheckRef(pass, f, r, ref fails);

            // preview stage
            var stage = Object.FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            if (stage == null) Fail("thiếu MenuCharacterPreviewStage");
            else
            {
                foreach (var f in new[] { "previewCamera", "previewLight", "characterRoot", "modularApplier", "previewTexture", "animator" })
                    CheckRef(stage, f, r, ref fails);
                var soStage = new SerializedObject(stage);
                var cam = soStage.FindProperty("previewCamera").objectReferenceValue as Camera;
                if (cam != null)
                {
                    if (cam.GetComponent<AudioListener>() != null) Fail("PreviewCamera có AudioListener");
                    int layer = LayerMask.NameToLayer("CharacterPreview");
                    if (layer >= 0 && cam.cullingMask != 1 << layer) Fail("PreviewCamera cullingMask ≠ CharacterPreview");
                    if (cam.targetTexture == null) Fail("PreviewCamera targetTexture null");
                }
            }

            // RawImages
            int rawCount = 0;
            foreach (var raw in Object.FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (raw.name != "CharacterRT" && raw.name != "PreviewRT") continue;
                rawCount++;
                if (raw.texture == null) Fail($"RawImage '{GetPath(raw.transform)}' texture null");
            }
            if (rawCount < 2) Fail($"chỉ thấy {rawCount}/2 preview RawImage (Hub CharacterRT + Costume PreviewRT)");
            else Ok("2 preview RawImage có texture");

            fails += CountMissingScripts(r, "Menu");
            return fails;
        }

        static int ValidateMap(StringBuilder r)
        {
            int fails = 0;
            void Fail(string m) { r.AppendLine("  ✗ Map: " + m); fails++; }
            void Ok(string m) => r.AppendLine("  ✓ Map: " + m);

            var huds = GameObject.Find("HUD");
            if (huds == null) { Fail("thiếu HUD"); return fails; }
            var ctrl = Object.FindFirstObjectByType<HudController>(FindObjectsInactive.Include);
            if (ctrl == null) Fail("thiếu HudController");
            else foreach (var f in new[] { "healthFillRect", "healthLabel", "wavePill", "coinPill", "pauseButton",
                                           "bombButton", "weaponButton", "weaponIcon", "ammoRing", "prototypeCatalog" })
                    CheckRef(ctrl, f, r, ref fails);
            var run = Object.FindFirstObjectByType<RunOverlays>(FindObjectsInactive.Include);
            if (run == null) Fail("thiếu RunOverlays");
            else foreach (var f in new[] { "pauseRoot", "resumeButton", "soundToggle", "vibrateToggle", "exitButton",
                                           "confirmRoot", "settingsRoot", "musicSlider", "sfxSlider",
                                           "reviveRoot", "levelUpRoot", "gameOverRoot", "replayButton", "homeButton",
                                           "ftueRoot", "ftueSkipButton" })
                    CheckRef(run, f, r, ref fails);

            var ess = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (ess.Length != 1) Fail($"EventSystem count = {ess.Length}"); else Ok("1 EventSystem");

            var joy = Object.FindFirstObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
            if (joy == null) Fail("thiếu VirtualJoystick"); else Ok("joystick OK");

            fails += CountMissingScripts(r, "Map");
            return fails;
        }

        static int ValidateAssets(StringBuilder r)
        {
            int fails = 0;
            var catalog = AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(UIThumbnailGenerator.CatalogAssetPath);
            if (catalog == null) { r.AppendLine("  ✗ Assets: thiếu UIPrototypeCatalog"); return 1; }
            var seen = new System.Collections.Generic.HashSet<WeaponData>();
            foreach (var e in catalog.weapons)
            {
                if (e.data == null) { r.AppendLine("  ✗ Assets: catalog có WeaponEntry null"); fails++; continue; }
                if (!seen.Add(e.data)) { r.AppendLine($"  ✗ Assets: WeaponEntry trùng '{e.data.name}'"); fails++; }
            }
            var guids = new System.Collections.Generic.HashSet<string>();
            foreach (var c in catalog.costumeIcons)
                if (!guids.Add(c.guid)) { r.AppendLine($"  ✗ Assets: costumeIcon trùng guid {c.guid}"); fails++; }

            // WeaponId/catalogOrder là persistent identity (PROFILE_SAVE.md) — trùng/thiếu là hỏng save.
            var ids = new System.Collections.Generic.HashSet<string>();
            var orders = new System.Collections.Generic.HashSet<int>();
            foreach (var e in catalog.weapons)
            {
                if (e.data == null) continue;
                if (string.IsNullOrEmpty(e.data.WeaponId)) { r.AppendLine($"  ✗ Assets: '{e.data.name}' thiếu WeaponId"); fails++; }
                else if (!ids.Add(e.data.WeaponId)) { r.AppendLine($"  ✗ Assets: WeaponId trùng '{e.data.WeaponId}'"); fails++; }
                if (!orders.Add(e.data.CatalogOrder)) { r.AppendLine($"  ✗ Assets: catalogOrder trùng {e.data.CatalogOrder} ('{e.data.name}')"); fails++; }
                if (e.data.price < 0) { r.AppendLine($"  ✗ Assets: '{e.data.name}' price âm ({e.data.price})"); fails++; }
                if (e.icon == null) { r.AppendLine($"  ✗ Assets: '{e.data.name}' thiếu icon trong catalog"); fails++; }
            }
            if (fails == 0) r.AppendLine($"  ✓ Assets: {ids.Count} WeaponId duy nhất, catalogOrder không trùng");

            fails += ValidateCostumeCatalog(r);

            r.AppendLine($"  ✓ Assets: catalog {catalog.weapons.Count} weapons, {catalog.costumeIcons.Count} costume icons, cheatUnlockAll={catalog.cheatUnlockAll}");
            return fails;
        }

        // Slice 4: catalog costume phai giu dung 14 wardrobe slot, guid unique, khong lot held-item.
        static readonly string[] WardrobeSlots =
        {
            "Hair", "Beard", "Brow", "Mouth", "Eyewear", "Eye", "Earring", "Head",
            "Chest", "Hands", "Back", "Body", "Legs", "Feet",
        };

        static int ValidateCostumeCatalog(StringBuilder r)
        {
            int fails = 0;
            var cat = AssetDatabase.LoadAssetAtPath<ZombieWar.ModularCostumeCatalog>(
                "Assets/_Project/Data/Character/ModularCostumeCatalog.asset");
            if (cat == null) { r.AppendLine("  ✗ Assets: thiếu ModularCostumeCatalog"); return 1; }

            var expected = new System.Collections.Generic.HashSet<string>(WardrobeSlots);
            var found = new System.Collections.Generic.HashSet<string>();
            var guids = new System.Collections.Generic.HashSet<string>();
            bool hasBaseBody = false;
            int total = 0;

            foreach (var slot in cat.slots)
            {
                if (!expected.Contains(slot.slot))
                { r.AppendLine($"  ✗ Costume: slot lạ '{slot.slot}' (held-item phải bị exclude)"); fails++; continue; }
                if (!found.Add(slot.slot))
                { r.AppendLine($"  ✗ Costume: slot '{slot.slot}' bị trùng"); fails++; }
                if (slot.isBaseBody)
                {
                    hasBaseBody = true;
                    if (slot.slot != "Body") { r.AppendLine($"  ✗ Costume: isBaseBody nằm ở '{slot.slot}' (phải là Body)"); fails++; }
                    if (slot.parts.Count == 0) { r.AppendLine("  ✗ Costume: slot Body rỗng — không có base body hợp lệ"); fails++; }
                }
                foreach (var p in slot.parts)
                {
                    total++;
                    if (string.IsNullOrEmpty(p.guid)) { r.AppendLine($"  ✗ Costume: '{slot.slot}/{p.name}' thiếu guid"); fails++; }
                    else if (!guids.Add(p.guid)) { r.AppendLine($"  ✗ Costume: guid trùng '{p.guid}' ({slot.slot}/{p.name})"); fails++; }
                    if (p.skinnedMesh == null || p.boneNames == null || p.boneNames.Length == 0)
                    { r.AppendLine($"  ✗ Costume: '{slot.slot}/{p.name}' thiếu skinned binding"); fails++; }
                }
            }
            foreach (var s in WardrobeSlots)
                if (!found.Contains(s)) { r.AppendLine($"  ✗ Costume: thiếu slot '{s}'"); fails++; }
            if (!hasBaseBody) { r.AppendLine("  ✗ Costume: không có slot isBaseBody"); fails++; }
            foreach (var ex in new[] { "Wield_Gear_Left", "Wield_Gear_Right" })
                if (found.Contains(ex)) { r.AppendLine($"  ✗ Costume: held category '{ex}' lọt vào wardrobe"); fails++; }

            if (fails == 0)
                r.AppendLine($"  ✓ Costume: {found.Count} slot / {total} part, guid unique, base Body OK, held-item excluded");

            fails += ValidateCostumeDefaults(cat, r);
            fails += ValidateCostumeIconCoverage(cat, r);
            return fails;
        }

        /// Slice 4.1: defaults phai authored, resolve dung slot, equipped ⊆ owned, du 4 slot bat buoc.
        static int ValidateCostumeDefaults(ZombieWar.ModularCostumeCatalog cat, StringBuilder r)
        {
            int fails = 0;
            var d = cat.defaults;
            if (d == null || !d.IsAuthored)
            { r.AppendLine("  ✗ Costume: defaults chưa authored — chạy 'Author Costume Defaults'"); return 1; }

            // Slice 4.2: essential = Hair/Brow/Eye/Mouth/Chest/Legs (Feet KHÔNG mặc, optional).
            foreach (var m in ZombieWar.ModularCostumeCatalog.EssentialSlots)
                if (string.IsNullOrEmpty(d.GetEquippedGuid(m)))
                { r.AppendLine($"  ✗ Costume defaults: thiếu default equip cho slot essential '{m}'"); fails++; }
            if (!string.IsNullOrEmpty(d.GetEquippedGuid("Feet")))
                { r.AppendLine("  ✗ Costume defaults: Feet KHÔNG được tự mặc (optional)"); fails++; }
            if (!ZombieWar.ModularCostumeCatalog.IsValidBodyColor(d.defaultBodyColor))
                { r.AppendLine($"  ✗ Costume defaults: body color '{d.defaultBodyColor}' không hợp lệ"); fails++; }
            if (!ZombieWar.ModularCostumeCatalog.IsValidBodyEar(d.defaultBodyEar))
                { r.AppendLine($"  ✗ Costume defaults: body ear '{d.defaultBodyEar}' không hợp lệ"); fails++; }

            foreach (var eq in d.equipped)
            {
                bool found = false; string realSlot = null; bool hasBinding = false;
                foreach (var slot in cat.slots)
                    foreach (var p in slot.parts)
                        if (p.guid == eq.guid)
                        { found = true; realSlot = slot.slot; hasBinding = p.skinnedMesh != null && p.boneNames != null && p.boneNames.Length > 0; }
                if (!found) { r.AppendLine($"  ✗ Costume defaults: equip '{eq.slot}' guid không có trong catalog"); fails++; }
                else if (realSlot != eq.slot) { r.AppendLine($"  ✗ Costume defaults: equip guid thuộc '{realSlot}' ≠ '{eq.slot}'"); fails++; }
                else if (!hasBinding) { r.AppendLine($"  ✗ Costume defaults: equip '{eq.slot}' thiếu skinned binding"); fails++; }
                if (!d.ownedGuids.Contains(eq.guid))
                { r.AppendLine($"  ✗ Costume defaults: equip '{eq.slot}' không nằm trong ownedGuids"); fails++; }
            }
            foreach (var g in d.ownedGuids)
            {
                bool found = false;
                foreach (var slot in cat.slots) foreach (var p in slot.parts) if (p.guid == g) found = true;
                if (!found) { r.AppendLine($"  ✗ Costume defaults: owned guid '{g}' không có trong catalog"); fails++; }
            }
            if (fails == 0)
                r.AppendLine($"  ✓ Costume defaults: owned={d.ownedGuids.Count}, equipped={d.equipped.Count} slot bắt buộc OK");
            return fails;
        }

        /// Slice 4.1: MỌI part hợp lệ phải có icon thật (mapping non-null) — fallback = LỖI.
        static int ValidateCostumeIconCoverage(ZombieWar.ModularCostumeCatalog cat, StringBuilder r)
        {
            int fails = 0;
            var ui = AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(UIThumbnailGenerator.CatalogAssetPath);
            if (ui == null) { r.AppendLine("  ✗ Icons: thiếu UIPrototypeCatalog"); return 1; }

            var mapped = new System.Collections.Generic.Dictionary<string, Sprite>();
            var dupes = 0;
            foreach (var e in ui.costumeIcons)
            {
                if (mapped.ContainsKey(e.guid)) { dupes++; continue; }
                mapped[e.guid] = e.icon;
            }
            if (dupes > 0) { r.AppendLine($"  ✗ Icons: {dupes} mapping guid trùng"); fails++; }

            // Slice 4.2: non-Body dùng vendor icon (846 phủ 100%). Body dùng bodyColorIcons (6).
            var validGuids = new System.Collections.Generic.HashSet<string>();
            r.AppendLine("  Icon coverage (slot: parts/real/missing):");
            int nonBodyTotal = 0;
            foreach (var slot in cat.slots)
            {
                if (slot.slot == ZombieWar.ModularCostumeCatalog.BodySlot)
                { r.AppendLine($"    Body: {slot.parts.Count} mesh (composite — dùng {ui.bodyColorIcons.Count} color icon)"); continue; }
                int real = 0, missing = 0;
                foreach (var p in slot.parts)
                {
                    validGuids.Add(p.guid); nonBodyTotal++;
                    if (mapped.TryGetValue(p.guid, out var s) && s != null) real++; else missing++;
                }
                r.AppendLine($"    {slot.slot}: {slot.parts.Count}/{real}/{missing}{(missing > 0 ? "  ✗" : "")}");
                if (missing > 0) fails++;
            }
            // Body color icons: đủ 6.
            int bodyIcons = 0;
            foreach (var col in ZombieWar.ModularCostumeCatalog.BodyColors)
                if (ui.GetBodyColorIcon(col) != null && ui.GetBodyColorIcon(col) != ui.costumeFallbackIcon) bodyIcons++;
            if (bodyIcons != 6) { r.AppendLine($"  ✗ Icons: Body color icon {bodyIcons}/6 (thiếu vendor Body_<Color>.png)"); fails++; }
            // Vendor icon không được là generated (đường dẫn phải nằm trong ThirdParty ScreenShot).
            int nonVendor = 0;
            foreach (var kv in mapped)
                if (kv.Value != null && !AssetDatabase.GetAssetPath(kv.Value).Contains("ScreenShot")) nonVendor++;
            if (nonVendor > 0) { r.AppendLine($"  ✗ Icons: {nonVendor} icon KHÔNG phải vendor screenshot (vẫn dùng generated)"); fails++; }

            if (fails == 0) r.AppendLine($"  ✓ Icons: {mapped.Count}/{nonBodyTotal} non-Body vendor + 6 Body color, không generated/fallback");
            return fails;
        }

        static void CheckRef(Component target, string field, StringBuilder r, ref int fails)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { r.AppendLine($"  ✗ {target.GetType().Name}: không có field '{field}'"); fails++; return; }
            if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null)
            {
                r.AppendLine($"  ✗ {target.GetType().Name}.{field} = null ({GetPath(target.transform)})");
                fails++;
            }
        }

        static int CountMissingScripts(StringBuilder r, string label)
        {
            int n = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var c in go.GetComponents<Component>())
                    if (c == null) { r.AppendLine($"  ✗ {label}: missing script trên '{GetPath(go.transform)}'"); n++; break; }
            return n;
        }

        static string GetPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }
    }
}
