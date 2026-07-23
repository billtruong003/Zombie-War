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
                new Color(1f, 0.949f, 0f, 1f), false);   // hand-tuned: vàng #FFF200 đặc
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
            var face = K.IconImage(avatarBg.rectTransform, "Face", "Icon_Face", A.C, Vector2.zero, new Vector2(64, 64));
            face.color = new Color(0.11f, 1f, 0f, 1f);   // hand-tuned: mặt avatar xanh lá #1CFF00
            var recCaption = K.Rect("RecordCaption", avatarChip);
            K.Place(recCaption, A.TL, new Vector2(112, -14), new Vector2(220, 30));
            K.IconImage(recCaption, "Trophy", "Icon_Trophy", A.ML, Vector2.zero, new Vector2(26, 26));
            var recLbl = K.Text(recCaption, "L", "BEST", 26, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Full(recLbl.rectTransform, 34, 0, 0, 0);
            var record = K.Text(avatarChip, "RecordValue", "—", 40, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            K.Place(record.rectTransform, A.BL, new Vector2(112, 12), new Vector2(220, 46));

            // ============ CurrencyRow — pill 200w + nút "+" nhô ra mép phải (hand-tuned) ============
            var coinLbl = HubPill(safe, "PillCoin", new Vector2(-275, -44), "Coin", out var coinPlus);
            var gemLbl = HubPill(safe, "PillGem", new Vector2(-32, -44), "Gem", out var gemPlus);
            var cluster = safe.gameObject.AddComponent<CurrencyClusterWidget>();

            // ============ Mission focus — stretch ngang, margin 100 mỗi bên (hand-tuned) ============
            var mission = K.Card(safe, "MissionCard", A.TC, Vector2.zero,
                new Vector2(1016, 150), out var missionGlow, out var missionBorder, out var missionBg);
            mission.anchorMin = new Vector2(0f, 1f);
            mission.anchorMax = Vector2.one;
            mission.pivot = new Vector2(0.5f, 1f);
            mission.anchoredPosition = new Vector2(0, -200);
            mission.sizeDelta = new Vector2(-200, 150);
            missionGlow.rectTransform.sizeDelta = new Vector2(48, 48);
            missionGlow.color = UITheme.Alpha(UITheme.Cyan, UITheme.GlowAlpha);
            missionGlow.gameObject.SetActive(true);
            missionBorder.color = UITheme.Cyan;
            missionBorder.gameObject.SetActive(true);
            var missionBtn = mission.gameObject.AddComponent<Button>();
            missionBtn.targetGraphic = missionBg;
            mission.gameObject.AddComponent<UIFxPress>();
            K.IconImage(mission, "MissionIcon", "Icon_MapPoint", A.ML, new Vector2(28, 0), new Vector2(76, 76));
            var missionTitle = K.Text(mission, "Title", "NEXT MISSION", 24, UITheme.TextDim,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(missionTitle.rectTransform, A.TL, new Vector2(124, -22), new Vector2(540, 34));
            var missionName = K.Text(mission, "Name", "SURVIVE THE NEXT WAVE", 34, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(missionName.rectTransform, A.BL, new Vector2(124, 24), new Vector2(650, 48));
            // Reward pill: accent cyan (decision #3), nền Button01_l_White_Bg tint — khớp hand-tuned.
            var rewardBg = SC.Image(mission, "Reward", "Components/Button/Button01_l_White_Bg.png", UITheme.Cyan, false);
            K.Place(rewardBg.rectTransform, A.MR, new Vector2(-24, 0), new Vector2(184, 72));
            var rewardIcon = SC.Image(rewardBg.rectTransform, "Icon",
                "Components/UI_Etc/ResourceBar_Demo_Icon_Coin.png", Color.white, false);
            K.Place(rewardIcon.rectTransform, A.ML, new Vector2(-2, 0), new Vector2(58, 58));
            rewardIcon.preserveAspect = true;
            var reward = K.Text(rewardBg.rectTransform, "Value", "+250", 31, UITheme.OnGold,
                FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            K.Full(reward.rectTransform, 56, 4, 18, 8);
            reward.rectTransform.anchoredPosition = new Vector2(19, 2);
            reward.rectTransform.sizeDelta = new Vector2(-74, -12);

            // ============ Character focus — RawImage preplaced trỏ RT ASSET ============
            var preview = K.Rect("Podium", safe);
            K.Place(preview, A.TC, new Vector2(0, -400), new Vector2(760, 800));
            // hand-tuned: frame VÀNG (không phải Navy mặc định của SC.ItemFrame)
            var previewFrame = SC.Image(preview, "SuperCasualFrame",
                "Components/Frame/ItemFrame01_Demo_Yellow.png", Color.white, false);
            K.Full(previewFrame.rectTransform);
            var rawGo = new GameObject("CharacterRT", typeof(RectTransform));
            var rawRt = (RectTransform)rawGo.transform;
            rawRt.SetParent(preview, false);
            K.Place(rawRt, A.C, new Vector2(0, 10), new Vector2(0, 900)); // width do AspectRatioFitter set
            var raw = rawGo.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.texture = MenuCharacterStageInstaller.EnsureRenderTexture();
            K.RtAspect(rawGo, raw.texture);

            var editBtn = SC.Button(preview, "EditChip", "COSTUME", new Vector2(250, 78), A.BR,
                new Vector2(-24, 24), "Blue", Color.white, 25);
            var editRelay = editBtn.gameObject.AddComponent<ButtonRelay>();

            // ============ Panel Reward — HLG container chứa Daily/Event (hand-tuned) ============
            var rewardPanel = K.Rect("Panel Reward", safe);
            rewardPanel.anchorMin = rewardPanel.anchorMax = new Vector2(0.5f, 1f);
            rewardPanel.pivot = new Vector2(0.5f, 1f);
            rewardPanel.anchoredPosition = new Vector2(0, -1300);
            rewardPanel.sizeDelta = new Vector2(1008, 150);
            var rewardHlg = rewardPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            rewardHlg.childControlWidth = false; rewardHlg.childControlHeight = false;
            rewardHlg.childForceExpandWidth = true; rewardHlg.childForceExpandHeight = true;
            rewardHlg.childAlignment = TextAnchor.MiddleCenter;

            var daily = K.Card(rewardPanel, "DailyCard", A.TC, Vector2.zero,
                new Vector2(400, 150), out _, out var dailyBorder, out _);
            dailyBorder.color = UITheme.Green;
            K.IconImage(daily, "Icon", "BtnIcon_Gift", A.ML, new Vector2(28, 0), new Vector2(84, 84));
            var dailyTitle = K.Text(daily, "Title", "DAILY REWARD", 28, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(dailyTitle.rectTransform, A.TL, new Vector2(132, -34), new Vector2(320, 42));
            var dailyHint = K.Text(daily, "Hint", "Ready to claim", 24, UITheme.Green,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(dailyHint.rectTransform, A.BL, new Vector2(132, 34), new Vector2(320, 38));

            var eventCard = K.Card(rewardPanel, "EventCard", A.TC, Vector2.zero,
                new Vector2(400, 150), out _, out var eventBorder, out _);
            eventBorder.color = UITheme.Gold;
            eventBorder.gameObject.SetActive(true);
            K.IconImage(eventCard, "Icon", "Icon_Ticket01", A.ML, new Vector2(28, 0), new Vector2(84, 84));
            var eventTitle = K.Text(eventCard, "Title", "ZOMBIE HUNT", 28, UITheme.TextMain,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(eventTitle.rectTransform, A.TL, new Vector2(132, -34), new Vector2(320, 42));
            var eventHint = K.Text(eventCard, "Hint", "2 days left", 24, UITheme.Gold,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(eventHint.rectTransform, A.BL, new Vector2(132, 34), new Vector2(320, 38));

            // ============ PLAY: stretch ngang margin 200, cao 200, label 88 (hand-tuned) ============
            var playWrap = K.Rect("PlayButtonWrap", safe);
            playWrap.anchorMin = new Vector2(0f, 0f);
            playWrap.anchorMax = new Vector2(1f, 0f);
            playWrap.pivot = new Vector2(0.5f, 0f);
            playWrap.anchoredPosition = new Vector2(0, 300);
            playWrap.sizeDelta = new Vector2(-400, 200);
            playWrap.gameObject.AddComponent<UIFxBreathe>();
            var playImg = SC.Image(playWrap, "PlayButton", "Components/Button/Button01_l_Yellow.png",
                Color.white, true);
            K.Full(playImg.rectTransform);
            var playBtn = playImg.gameObject.AddComponent<Button>();
            playBtn.targetGraphic = playImg;
            playImg.gameObject.AddComponent<UIFxPress>();
            var playLbl = K.Text(playImg.rectTransform, "Label", "PLAY", 88, Color.white, FontStyles.Bold);
            K.Full(playLbl.rectTransform, 24, 8, 24, 14);
            playLbl.rectTransform.anchoredPosition = new Vector2(0, 3);
            playLbl.rectTransform.sizeDelta = new Vector2(-48, -22);
            playLbl.outlineWidth = 0.12f;
            playLbl.outlineColor = new Color32(8, 13, 25, 230);

            // ============ Dock — full-width đáy, cao 200, HLG chia đều 5 tab (hand-tuned) ============
            var dock = SC.Dock(safe, "Dock");
            dock.color = UITheme.Surface;
            var dockRt = dock.rectTransform;
            dockRt.anchorMin = new Vector2(0f, 0f);
            dockRt.anchorMax = new Vector2(1f, 0f);
            dockRt.pivot = new Vector2(0.5f, 0f);
            dockRt.anchoredPosition = Vector2.zero;
            dockRt.sizeDelta = new Vector2(0, 200);
            var dockHlg = dock.gameObject.AddComponent<HorizontalLayoutGroup>();
            dockHlg.childControlWidth = false; dockHlg.childControlHeight = false;
            dockHlg.childForceExpandWidth = true; dockHlg.childForceExpandHeight = true;
            dockHlg.childAlignment = TextAnchor.MiddleCenter;

            var homeBtn    = DockTab(dockRt, "HomeTab",    "HOME",    "Icon_Castle");
            var loadoutBtn = DockTab(dockRt, "LoadoutTab", "LOADOUT", "Icon_Fire01");
            var shopBtn    = DockTab(dockRt, "ShopTab",    "SHOP",    "Components/Icon_ItemIcons/256/ItemIcon_Shop.Png");
            var costumeBtn = DockTab(dockRt, "CostumeTab", "COSTUME", "Icon_Helmet");
            var passBtn    = DockTab(dockRt, "PassTab",    "PASS",    "Icon_Ticket02");
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
            K.Wire(soHub, "coinPlusButton", coinPlus);
            K.Wire(soHub, "gemPlusButton", gemPlus);
            K.Wire(soHub, "missionButton", missionBtn);
            K.Wire(soHub, "missionNameLabel", missionName);
            K.Wire(soHub, "missionRewardLabel", reward);
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

        /// Tab dock hand-tuned: HLG cha chia đều; icon 96 top-center; Notify dot 32 (default OFF,
        /// runtime bật qua HubScreen.RefreshBadges); label strip 50px đáy.
        static Button DockTab(RectTransform dock, string name, string label, string iconName)
        {
            var rt = K.Rect(name, dock);
            rt.sizeDelta = new Vector2(1016f / 5f, 152);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            rt.gameObject.AddComponent<UIFxPress>();

            // ItemIcon_Shop không nằm trong Demo_Icon — nhận cả relative path lẫn icon name.
            var icon = iconName.Contains("/")
                ? SC.Image(rt, "Icon", iconName, Color.white, false)
                : K.IconImage(rt, "Icon", iconName, A.TC, Vector2.zero, new Vector2(96, 96));
            if (iconName.Contains("/"))
            {
                K.Place(icon.rectTransform, A.TC, Vector2.zero, new Vector2(96, 96));
                icon.preserveAspect = true;
            }

            var notify = SC.Image(icon.rectTransform, "Notify",
                "Components/Icon_PictoIcons/256/PictoIcon_Info_3.Png", new Color(1f, 0.278f, 0.263f, 1f), false);
            var notifyRt = notify.rectTransform;
            notifyRt.anchorMin = notifyRt.anchorMax = Vector2.one;
            notifyRt.pivot = Vector2.one;
            notifyRt.anchoredPosition = Vector2.zero;
            notifyRt.sizeDelta = new Vector2(32, 32);
            notify.gameObject.AddComponent<UIFxPulse>();
            notify.gameObject.SetActive(false);

            var txt = K.Text(rt, "Label", label, 28, Color.white, FontStyles.Bold);
            txt.outlineWidth = 0.12f;
            txt.outlineColor = new Color32(8, 13, 25, 230);
            var txtRt = txt.rectTransform;
            txtRt.anchorMin = new Vector2(0f, 0f);
            txtRt.anchorMax = new Vector2(1f, 0f);
            txtRt.pivot = new Vector2(0.5f, 0f);
            txtRt.anchoredPosition = Vector2.zero;
            txtRt.sizeDelta = new Vector2(0, 50);
            return btn;
        }

        /// Pill tiền hand-tuned: 200×72, value chừa chỗ phải, nút "+" 50×50 nhô ra mép phải
        /// (BorderFrame_Round24 tím + PictoIcon_Plus_1 vàng chanh).
        static TMP_Text HubPill(RectTransform safe, string name, Vector2 pos, string iconName, out Button plusBtn)
        {
            var pill = SC.Image(safe, name, "Components/UI_Etc/ResourceBar_Demo_Bg.png", UITheme.Surface, false);
            K.Place(pill.rectTransform, A.TR, pos, new Vector2(200, 72));

            var icon = SC.Image(pill.rectTransform, "Icon",
                $"Components/UI_Etc/ResourceBar_Demo_Icon_{iconName}.png", Color.white, false);
            K.Place(icon.rectTransform, A.ML, new Vector2(-2, 0), new Vector2(58, 58));
            icon.preserveAspect = true;

            var plus = SC.Image(pill.rectTransform, "Plus",
                "Components/Frame/BorderFrame_Round24.png", new Color(0.588f, 0f, 1f, 1f), true);
            var plusRt = plus.rectTransform;
            plusRt.anchorMin = plusRt.anchorMax = new Vector2(1f, 0.5f);
            plusRt.pivot = new Vector2(0f, 0.5f);
            plusRt.anchoredPosition = new Vector2(-25, 0);
            plusRt.sizeDelta = new Vector2(50, 50);
            plusBtn = plus.gameObject.AddComponent<Button>();
            plusBtn.targetGraphic = plus;
            plus.gameObject.AddComponent<UIFxPress>();
            var plusGlyph = SC.Image(plusRt, "Image",
                "Components/Icon_PictoIcons/256/PictoIcon_Plus_1.Png", new Color(0.918f, 1f, 0f, 1f), false);
            K.Full(plusGlyph.rectTransform, 5, 5, 5, 5);

            var value = K.Text(pill.rectTransform, "Value", "0", 31, Color.white,
                FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            value.outlineWidth = 0.14f;
            value.outlineColor = new Color32(10, 16, 29, 235);
            K.Full(value.rectTransform, 56, 4, 18, 8);
            value.rectTransform.anchoredPosition = new Vector2(10, 0);
            value.rectTransform.sizeDelta = new Vector2(-100, -30);
            return value;
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
