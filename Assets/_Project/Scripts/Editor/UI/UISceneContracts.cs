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
            var costume = Object.FindFirstObjectByType<CostumeScreen>(FindObjectsInactive.Include);
            if (costume != null)
                foreach (var f in new[] { "backButton", "randomButton", "catalog", "uiCatalog", "previewStage",
                                          "pagePrevButton", "pageNextButton", "pageLabel" })
                    CheckRef(costume, f, r, ref fails);
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
            r.AppendLine($"  ✓ Assets: catalog {catalog.weapons.Count} weapons, {catalog.costumeIcons.Count} costume icons, cheatUnlockAll={catalog.cheatUnlockAll}");
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
