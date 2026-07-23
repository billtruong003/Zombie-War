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
using SC = ZombieWar.Editor.UI.SuperCasualSkin;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// DESTRUCTIVE generator màn 01 HUB (spec §4.1): 1 PLAY gold duy nhất (breathing trên wrapper,
    /// press trên button — không tranh transform), dock 5 tab icon Layer Lab, preview RawImage
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
            var diag = K.Image(root, "BgDiagonal", K.DiagonalBg,
                new Color(1f, 1f, 1f, 0.25f), false);
            K.Full(diag.rectTransform);
            diag.type = Image.Type.Tiled;

            var safe = K.Rect("Safe", root);
            K.Full(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // ============ AvatarChip ============
            var avatarChip = K.Rect("AvatarChip", safe);
            K.Place(avatarChip, A.TL, new Vector2(32, -32), new Vector2(340, 96));
            var avatarPanel = SC.Image(avatarChip, "Bg", "Components/UI_Etc/ResourceBar_Demo_Bg.png", UITheme.Surface, false);
            K.Full(avatarPanel.rectTransform);
            var avatarBg = SC.Image(avatarChip, "Avatar", "Components/Button/Button_Circle122.png", Color.white, false);
            K.Place(avatarBg.rectTransform, A.ML, Vector2.zero, new Vector2(96, 96));
            K.IconImage(avatarBg.rectTransform, "Face", "Icon_Face", A.C, Vector2.zero, new Vector2(64, 64));
            var recCaption = K.Rect("RecordCaption", avatarChip);
            K.Place(recCaption, A.TL, new Vector2(112, -14), new Vector2(220, 30));
            K.IconImage(recCaption, "Trophy", "Icon_Trophy", A.ML, Vector2.zero, new Vector2(26, 26));
            var recLbl = K.Text(recCaption, "L", "BEST", 26, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Full(recLbl.rectTransform, 34, 0, 0, 0);
            var record = K.Text(avatarChip, "RecordValue", "—", 40, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            K.Place(record.rectTransform, A.BL, new Vector2(112, 12), new Vector2(220, 46));

            // ============ CurrencyRow ============
            var coinLbl = SC.ResourcePill(safe, "PillCoin", A.TR, new Vector2(-32 - 192, -44), 176, "Coin");
            var gemLbl = SC.ResourcePill(safe, "PillGem", A.TR, new Vector2(-32, -44), 176, "Gem");
            coinLbl.transform.parent.GetComponent<Image>().color = UITheme.Surface;
            gemLbl.transform.parent.GetComponent<Image>().color = UITheme.Surface;
            var cluster = safe.gameObject.AddComponent<CurrencyClusterWidget>();

            // ============ Mission focus ============
            var mission = K.Card(safe, "MissionCard", A.TC, new Vector2(0, -152),
                new Vector2(1016, 142), out var missionGlow, out var missionBorder, out _);
            missionGlow.color = UITheme.Alpha(UITheme.Cyan, UITheme.GlowAlpha);
            missionGlow.gameObject.SetActive(true);
            missionBorder.color = UITheme.Cyan;
            missionBorder.gameObject.SetActive(true);
            K.IconImage(mission, "MissionIcon", "Icon_MapPoint", A.ML, new Vector2(28, 0), new Vector2(76, 76));
            var missionTitle = K.Text(mission, "Title", "NEXT MISSION", 24, UITheme.TextDim,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(missionTitle.rectTransform, A.TL, new Vector2(124, -22), new Vector2(540, 34));
            var missionName = K.Text(mission, "Name", "SURVIVE THE NEXT WAVE", 34, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(missionName.rectTransform, A.BL, new Vector2(124, 24), new Vector2(650, 48));
            var reward = SC.ResourcePill(mission, "Reward", A.MR, new Vector2(-24, 0), 184, "Coin");
            reward.text = "+250";
            // Accent chủ đích (decision #3): cyan khớp border MissionCard, KHÔNG trắng và khác PLAY vàng.
            reward.transform.parent.GetComponent<Image>().color = UITheme.Cyan;
            reward.color = UITheme.OnGold;

            // ============ Character focus — RawImage preplaced trỏ RT ASSET ============
            var preview = K.Rect("Podium", safe);
            K.Place(preview, A.TC, new Vector2(0, -324), new Vector2(760, 800));
            var previewFrame = SC.ItemFrame(preview, "SuperCasualFrame", Color.white);
            K.Full(previewFrame.rectTransform);
            var rawGo = new GameObject("CharacterRT", typeof(RectTransform));
            var rawRt = (RectTransform)rawGo.transform;
            rawRt.SetParent(preview, false);
            K.Place(rawRt, A.C, new Vector2(0, 10), new Vector2(520, 760));
            var raw = rawGo.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.texture = MenuCharacterStageInstaller.EnsureRenderTexture();
            K.RtAspect(rawGo, raw.texture);

            var editBtn = SC.Button(preview, "EditChip", "COSTUME", new Vector2(250, 78), A.BR,
                new Vector2(-24, 24), "Blue", Color.white, 25);
            var editRelay = editBtn.gameObject.AddComponent<ButtonRelay>();

            // ============ Compact supporting cards ============
            var daily = K.Card(safe, "DailyCard", A.TC, new Vector2(-258, -1160),
                new Vector2(492, 176), out _, out _, out _);
            K.IconImage(daily, "Icon", "BtnIcon_Gift", A.ML, new Vector2(28, 0), new Vector2(84, 84));
            var dailyTitle = K.Text(daily, "Title", "DAILY REWARD", 28, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(dailyTitle.rectTransform, A.TL, new Vector2(132, -34), new Vector2(320, 42));
            var dailyHint = K.Text(daily, "Hint", "Ready to claim", 24, UITheme.Green,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(dailyHint.rectTransform, A.BL, new Vector2(132, 34), new Vector2(320, 38));

            var eventCard = K.Card(safe, "EventCard", A.TC, new Vector2(258, -1160),
                new Vector2(492, 176), out _, out var eventBorder, out _);
            eventBorder.color = UITheme.Gold;
            eventBorder.gameObject.SetActive(true);
            K.IconImage(eventCard, "Icon", "Icon_Ticket01", A.ML, new Vector2(28, 0), new Vector2(84, 84));
            var eventTitle = K.Text(eventCard, "Title", "ZOMBIE HUNT", 28, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(eventTitle.rectTransform, A.TL, new Vector2(132, -34), new Vector2(320, 42));
            var eventHint = K.Text(eventCard, "Hint", "2 days left", 24, UITheme.Gold,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(eventHint.rectTransform, A.BL, new Vector2(132, 34), new Vector2(320, 38));

            // ============ PLAY: breathing trên WRAPPER, press trên button (không tranh scale) ============
            var playWrap = K.Rect("PlayButtonWrap", safe);
            K.Place(playWrap, A.BC, new Vector2(0, 246), new Vector2(1016, 150));
            playWrap.gameObject.AddComponent<UIFxBreathe>();
            var playBtn = SC.Button(playWrap, "PlayButton", "PLAY", new Vector2(1016, 150), A.C,
                Vector2.zero, "Yellow", UITheme.OnGold, 58);

            // ============ Dock 5 tab — Home là trạng thái hiện tại ============
            var dock = SC.Dock(safe, "Dock");
            dock.color = UITheme.Surface;
            K.Place(dock.rectTransform, A.BC, new Vector2(0, 32), new Vector2(1016, 152));

            var homeBtn    = DockTab(dock.rectTransform, "HomeTab",    "HOME",    0, "Icon_Castle", false);
            var loadoutBtn = DockTab(dock.rectTransform, "LoadoutTab", "LOADOUT", 1, "Icon_Sword01", false);
            var shopBtn    = DockTab(dock.rectTransform, "ShopTab",    "SHOP",    2, "Icon_Coin", true);
            var costumeBtn = DockTab(dock.rectTransform, "CostumeTab", "COSTUME", 3, "Icon_Helmet", false);
            var passBtn    = DockTab(dock.rectTransform, "PassTab",    "PASS",    4, "Icon_Ticket01", false);
            homeBtn.interactable = false;
            homeBtn.GetComponentInChildren<TMP_Text>().color = UITheme.Gold;

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

            // Hub is rebuilt after the other standalone screen prefabs in some authoring flows.
            // Cross-prefab navigation cannot live inside UI_HubScreen.prefab itself, so repair
            // the serialized scene-instance references every time this prefab is reconnected.
            UISceneContracts.EnsureMenu();
            Debug.Log("[HubInstaller] HUB rebuilt + prefab reconnected + menu references repaired.");
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
            const float tabWidth = 1016f / 5f;
            rt.sizeDelta = new Vector2(tabWidth, 152);
            rt.anchoredPosition = new Vector2(i * tabWidth, 0);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;

            var icon = K.IconImage(rt, "Icon", iconName, A.C, new Vector2(0, 28), new Vector2(64, 64));

            var txt = K.Text(rt, "Label", label, 24, Color.white, FontStyles.Bold);
            txt.outlineWidth = 0.12f;
            txt.outlineColor = new Color32(8, 13, 25, 230);
            K.Place(txt.rectTransform, A.C, new Vector2(0, -40), new Vector2(tabWidth, 32));

            if (dot)
            {
                var d = SC.Image(icon.rectTransform, "Dot", "Components/UI_Etc/Alert_Dot_Bg.png", Color.white, false);
                K.Place(d.rectTransform, A.TR, new Vector2(8, 8), new Vector2(28, 28));
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
