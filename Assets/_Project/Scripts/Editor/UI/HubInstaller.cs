using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.EditorTools;
using ZombieWar.UI;
using K = ZombieWar.Editor.UI.UIKit;
using A = ZombieWar.Editor.UI.UIKit.Anch;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// DESTRUCTIVE generator màn 01 HUB (spec §4.1): 1 PLAY gold duy nhất (breathing trên wrapper,
    /// press trên button — không tranh transform), dock 4 tab icon Layer Lab, preview RawImage
    /// preplaced trỏ RT asset. Sau migration: prefab là source of truth — chỉ chạy khi user confirm.
    /// </summary>
    public static class HubInstaller
    {
        const string MenuScene = "Assets/_Project/Scenes/Menu.unity";

        [MenuItem("ZombieWar/UI/Authoring/Rebuild Hub Screen (Destructive)...")]
        public static void BuildInteractive()
        {
            if (!UIPrefabizer.ConfirmDestructive("HubScreen")) return;
            Build();
        }

        public static void Build()
        {
            if (EnsureScene()) return;

            var canvas = UIRootInstaller.EnsureRoot();

            var old = canvas.transform.Find("HubScreen");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // legacy runtime-built menu cũ không còn đường vào (HubScreen bỏ fallback) — dọn khỏi scene
            RemoveLegacy<LoadoutMenuController>();
            RemoveLegacy<CostumeMenuController>();

            var stage = MenuCharacterStageInstaller.EnsureInOpenScene();

            var root = K.Rect("HubScreen", (RectTransform)canvas.transform);
            K.Full(root);
            root.gameObject.AddComponent<CanvasGroup>();
            var hub = root.gameObject.AddComponent<HubScreen>();

            var bg = K.Image(root, "Bg", null, UITheme.Bg, false);
            K.Full(bg.rectTransform);
            var diag = K.Image(root, "BgDiagonal", K.DiagonalBg, new Color(1f, 1f, 1f, 0.35f), false);
            K.Full(diag.rectTransform);
            diag.type = Image.Type.Tiled;

            var safe = K.Rect("Safe", root);
            K.Full(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // ============ AvatarChip ============
            var avatarChip = K.Rect("AvatarChip", safe);
            K.Place(avatarChip, A.TL, new Vector2(32, -32), new Vector2(340, 96));
            var avatarBg = K.Image(avatarChip, "Avatar", K.Circle, UITheme.Rarity[2], false);
            K.Place(avatarBg.rectTransform, A.ML, Vector2.zero, new Vector2(96, 96));
            K.IconImage(avatarBg.rectTransform, "Face", "Icon_Face", A.C, Vector2.zero, new Vector2(64, 64));
            var recCaption = K.Rect("RecordCaption", avatarChip);
            K.Place(recCaption, A.TL, new Vector2(112, -14), new Vector2(220, 30));
            K.IconImage(recCaption, "Trophy", "Icon_Trophy", A.ML, Vector2.zero, new Vector2(26, 26));
            var recLbl = K.Text(recCaption, "L", "KỶ LỤC", 26, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Full(recLbl.rectTransform, 34, 0, 0, 0);
            var record = K.Text(avatarChip, "RecordValue", "—", 40, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            K.Place(record.rectTransform, A.BL, new Vector2(112, 12), new Vector2(220, 46));

            // ============ CurrencyRow ============
            var coinLbl = K.CurrencyPill(safe, "PillCoin", UITheme.Coin, A.TR, new Vector2(-32 - 192, -44), 176, "Icon_Coin");
            var gemLbl = K.CurrencyPill(safe, "PillGem", UITheme.Gem, A.TR, new Vector2(-32, -44), 176, "Icon_Gem");
            var cluster = safe.gameObject.AddComponent<CurrencyClusterWidget>();

            // ============ PreviewCard — RawImage preplaced trỏ RT ASSET ============
            var preview = K.Rect("Podium", safe);
            K.Place(preview, A.TC, new Vector2(0, -200), new Vector2(930, 860));
            var dashed = K.Image(preview, "DashedFrame", K.Dashed, UITheme.Hairline, false);
            K.Full(dashed.rectTransform);
            var rawGo = new GameObject("CharacterRT", typeof(RectTransform));
            var rawRt = (RectTransform)rawGo.transform;
            rawRt.SetParent(preview, false);
            K.Place(rawRt, A.C, Vector2.zero, new Vector2(466, 820));
            var raw = rawGo.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.texture = MenuCharacterStageInstaller.EnsureRenderTexture();

            var edit = K.Image(preview, "EditChip", K.Pill, UITheme.Surface);
            K.Place(edit.rectTransform, A.BR, new Vector2(-24, 24), new Vector2(120, 72));
            var editBtn = edit.gameObject.AddComponent<Button>();
            editBtn.targetGraphic = edit;
            var editRelay = edit.gameObject.AddComponent<ButtonRelay>();
            var editTxt = K.Text(edit.rectTransform, "Label", "EDIT", 28, UITheme.Gold, FontStyles.Bold);
            K.Full(editTxt.rectTransform);

            // ============ PLAY: breathing trên WRAPPER, press trên button (không tranh scale) ============
            var playWrap = K.Rect("PlayButtonWrap", safe);
            K.Place(playWrap, A.BC, new Vector2(0, 264), new Vector2(960, 150));
            playWrap.gameObject.AddComponent<UIFxBreathe>();
            var playBtn = K.BtnPrimary(playWrap, "PlayButton", "PLAY", new Vector2(960, 150), A.C, Vector2.zero);

            // ============ Dock 4 tab — icon semantic Layer Lab ============
            var dock = K.Image(safe, "Dock", K.Rounded32, UITheme.Surface);
            K.Place(dock.rectTransform, A.BC, new Vector2(0, 32), new Vector2(1016, 152));
            var dockFrame = K.Image(dock.rectTransform, "Hairline", K.Frame32, UITheme.Hairline, false);
            K.Full(dockFrame.rectTransform);

            var loadoutBtn = DockTab(dock.rectTransform, "LoadoutTab", "LOADOUT", 0, "Icon_Sword01", false);
            var shopBtn    = DockTab(dock.rectTransform, "ShopTab",    "SHOP",    1, "Icon_Coin", true);
            var costumeBtn = DockTab(dock.rectTransform, "CostumeTab", "COSTUME", 2, "Icon_Helmet", false);
            var passBtn    = DockTab(dock.rectTransform, "PassTab",    "PASS",    3, "Icon_Ticket01", false);

            // ============ WIRE ============
            var soRelayEdit = new SerializedObject(editRelay);
            soRelayEdit.FindProperty("target").objectReferenceValue = costumeBtn;
            soRelayEdit.ApplyModifiedPropertiesWithoutUndo();

            var soCluster = new SerializedObject(cluster);
            K.Wire(soCluster, "coinLabel", coinLbl);
            K.Wire(soCluster, "gemLabel", gemLbl);
            soCluster.ApplyModifiedPropertiesWithoutUndo();

            var soHub = new SerializedObject(hub);
            K.Wire(soHub, "playButton", playBtn);
            K.Wire(soHub, "shopButton", shopBtn);
            K.Wire(soHub, "loadoutButton", loadoutBtn);
            K.Wire(soHub, "costumeButton", costumeBtn);
            K.Wire(soHub, "passButton", passBtn);
            K.Wire(soHub, "recordLabel", record);
            soHub.ApplyModifiedPropertiesWithoutUndo();

            UIRootInstaller.SetInitialScreen(hub);

            UIPrefabizer.ReconnectAfterRebuild(root.gameObject, $"{UIPrefabizer.ScreensDir}/UI_HubScreen.prefab");

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("[HubInstaller] HUB rebuilt + prefab reconnected.");
        }

        static void RemoveLegacy<T>() where T : Component
        {
            var legacy = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (legacy == null) return;
            Debug.Log($"[HubInstaller] Xoá legacy '{legacy.gameObject.name}' ({typeof(T).Name}) khỏi Menu scene.");
            Object.DestroyImmediate(legacy.gameObject);
        }

        static Button DockTab(RectTransform dock, string name, string label, int i, string iconName, bool dot)
        {
            var rt = K.Rect(name, dock);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(254, 152);
            rt.anchoredPosition = new Vector2(i * 254, 0);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;

            var icon = K.IconImage(rt, "Icon", iconName, A.C, new Vector2(0, 28), new Vector2(56, 56));

            var txt = K.Text(rt, "Label", label, 24, UITheme.TextDim, FontStyles.Bold);
            K.Place(txt.rectTransform, A.C, new Vector2(0, -40), new Vector2(254, 32));

            if (dot)
            {
                var d = K.Image(icon.rectTransform, "Dot", K.Circle, UITheme.Danger, false);
                K.Place(d.rectTransform, A.TR, new Vector2(6, 6), new Vector2(22, 22));
                d.gameObject.AddComponent<UIFxPulse>();
            }
            return btn;
        }

        static bool EnsureScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path == MenuScene) return false;
            if (!File.Exists(MenuScene)) { Debug.LogError($"[HubInstaller] Không thấy {MenuScene}"); return true; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return true;
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            return false;
        }
    }
}
