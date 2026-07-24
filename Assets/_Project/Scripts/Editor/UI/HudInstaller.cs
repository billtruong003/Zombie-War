using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using K = ZombieWar.Editor.UI.UIKit;
using A = ZombieWar.Editor.UI.UIKit.Anch;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// HUD in-run + overlays cho Map_Level1 theo spec §4.6–§4.12 và Sheet B (mọi toạ độ grid 48).
    /// CHỈ đụng HUD canvas — không rebuild scene khác (SceneFlowBuilder.BuildAll sẽ phá Menu UI).
    /// Giữ nguyên Joystick_BG (PlayerSpawner reference) — chỉ restyle. Idempotent.
    /// Auto-aim/auto-fire: KHÔNG có nút bắn/reload; slot phải = weapon switch + bomb.
    /// </summary>
    public static class HudInstaller
    {
        const string MapScene = "Assets/_Project/Scenes/Map_Level1.unity";

        [MenuItem("ZombieWar/UI/Authoring/Rebuild HUD Map_Level1 (Destructive)...")]
        public static void BuildInteractive()
        {
            if (!UIPrefabizer.ConfirmDestructive("HUD + overlays (Map_Level1)")) return;
            Build();
        }

        public static void Build()
        {
            if (EnsureScene()) return;

            var hudGo = GameObject.Find("HUD");
            if (hudGo == null) { Debug.LogError("[HudInstaller] Không thấy HUD canvas trong Map_Level1."); return; }
            var hudRt = (RectTransform)hudGo.transform;

            var scaler = hudGo.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(UITheme.RefWidth, UITheme.RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // ---- dọn widget cũ (giữ joystick) ----
            foreach (var n in new[] { "WaveLabel", "ZombieLabel", "HealthFrame", "GameOverPanel", "VictoryPanel",
                                      "WeaponButton", "Safe", "Overlays", "BombButton", "WeaponRoster" })
            {
                var old = hudRt.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var oldCtrl = hudGo.GetComponent<HudController>();
            if (oldCtrl != null) Object.DestroyImmediate(oldCtrl);
            var oldOverlays = hudGo.GetComponent<RunOverlays>();
            if (oldOverlays != null) Object.DestroyImmediate(oldOverlays);

            // ---- joystick restyle in place (BL 96,96 · outer 288 · knob 128) ----
            var joyRt = hudRt.Find("Joystick_BG") as RectTransform;
            if (joyRt != null)
            {
                joyRt.anchorMin = joyRt.anchorMax = new Vector2(0, 0);
                joyRt.pivot = new Vector2(0, 0);
                joyRt.anchoredPosition = new Vector2(96, 96);
                joyRt.sizeDelta = new Vector2(288, 288);
                var joyImg = joyRt.GetComponent<Image>();
                joyImg.sprite = K.Circle; joyImg.type = Image.Type.Simple;
                joyImg.color = UITheme.Alpha(UITheme.Surface, 0.6f);
                var handle = joyRt.Find("Handle") as RectTransform;
                if (handle != null)
                {
                    handle.sizeDelta = new Vector2(128, 128);
                    var hImg = handle.GetComponent<Image>();
                    hImg.sprite = K.Circle; hImg.type = Image.Type.Simple;
                    hImg.color = UITheme.Alpha(UITheme.Surface2, 0.95f);
                }
            }
            else Debug.LogWarning("[HudInstaller] Không thấy Joystick_BG — bỏ qua restyle.");

            // ---- Safe container cho top pills + nút phải ----
            var safe = K.Rect("Safe", hudRt);
            K.Full(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // HP pill TL (48,-48) 400×36
            var hpTrack = K.Image(safe, "HpBar", K.Pill, UITheme.Alpha(UITheme.Surface2, 0.9f), false);
            K.Place(hpTrack.rectTransform, A.TL, new Vector2(48, -48), new Vector2(400, 36));
            var hpFill = K.Image(hpTrack.rectTransform, "Fill", K.Pill, UITheme.Green, false);
            hpFill.rectTransform.anchorMin = Vector2.zero;
            hpFill.rectTransform.anchorMax = Vector2.one;
            hpFill.rectTransform.offsetMin = new Vector2(4, 4);
            hpFill.rectTransform.offsetMax = new Vector2(-4, -4);
            // HP label nằm TRONG bar (max width = bar) — số dài đã compact-format nên không tràn sang Wave pill
            var hpLabel = K.Text(safe, "HpLabel", "100", 24, Color.white, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            K.Place(hpLabel.rectTransform, A.TL, new Vector2(48, -48), new Vector2(400, 36));
            hpLabel.rectTransform.pivot = new Vector2(0, 1);
            hpLabel.margin = new Vector4(12, 0, 16, 0);
            hpLabel.enableWordWrapping = false;
            hpLabel.overflowMode = TextOverflowModes.Ellipsis;

            // Wave pill TC (0,-48)
            var wavePillBg = K.Image(safe, "WavePill", K.Pill, UITheme.Alpha(UITheme.Surface, 0.85f), false);
            K.Place(wavePillBg.rectTransform, A.TC, new Vector2(0, -48), new Vector2(360, 64));
            var waveTxt = K.Text(wavePillBg.rectTransform, "Label", "Wave 1 — 0", 34, UITheme.TextMain, FontStyles.Bold);
            K.Full(waveTxt.rectTransform);

            // Coin pill TR (-48,-48)
            var coinLbl = K.CurrencyPill(safe, "CoinPill", UITheme.Coin, A.TR, new Vector2(-48, -48));

            // Pause TR (-48,-144) 96×96
            var pauseBg = K.Image(safe, "PauseBtn", K.Rounded24, UITheme.Alpha(UITheme.Surface, 0.85f));
            K.Place(pauseBg.rectTransform, A.TR, new Vector2(-48, -144), new Vector2(96, 96));
            var pauseBtn = pauseBg.gameObject.AddComponent<Button>();
            pauseBtn.targetGraphic = pauseBg;
            K.IconImage(pauseBg.rectTransform, "Icon", "Icon_Pause", A.C, Vector2.zero, new Vector2(48, 48));

            // Bomb BR (-48,432) 112×112 (thumb zone)
            var bombBg = K.Image(safe, "BombBtn", K.Rounded24, UITheme.Rarity[2]);
            K.Place(bombBg.rectTransform, A.BR, new Vector2(-48, 432), new Vector2(112, 112));
            var bombBtn = bombBg.gameObject.AddComponent<Button>();
            bombBtn.targetGraphic = bombBg;
            var bombIcon = K.Image(bombBg.rectTransform, "Icon", K.Circle, UITheme.Bg, false);
            K.Place(bombIcon.rectTransform, A.C, new Vector2(0, 8), new Vector2(52, 52));
            var bombLbl = K.Text(bombBg.rectTransform, "Count", "x3", 26, Color.white, FontStyles.Bold, TextAlignmentOptions.Bottom);
            K.Full(bombLbl.rectTransform, 0, 0, 0, 6);

            // Weapon slot BR (-96,96) 224×224: switch weapon + ammo ring (KHÔNG phải nút bắn)
            var wepBg = K.Image(safe, "WeaponBtn", K.Circle, UITheme.Green);
            K.Place(wepBg.rectTransform, A.BR, new Vector2(-96, 96), new Vector2(224, 224));
            var wepBtn = wepBg.gameObject.AddComponent<Button>();
            wepBtn.targetGraphic = wepBg;
            var ring = K.Image(wepBg.rectTransform, "AmmoRing", K.Ring, UITheme.Gold, false);
            K.Full(ring.rectTransform, 8, 8, 8, 8);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = false;
            var wepFallback = K.Icon("Icon_Sword02");
            var wepIcon = K.Image(wepBg.rectTransform, "Icon",
                wepFallback != null ? wepFallback : K.Rounded24,
                wepFallback != null ? Color.white : UITheme.Alpha(UITheme.Bg, 0.55f), false);
            K.Place(wepIcon.rectTransform, A.C, new Vector2(0, 10), new Vector2(96, 96));
            wepIcon.preserveAspect = true;
            var wepLbl = K.Text(wepBg.rectTransform, "Ammo", "", 40, Color.white, FontStyles.Bold, TextAlignmentOptions.Bottom);
            K.Full(wepLbl.rectTransform, 0, 0, 0, 24);

            // ---- Overlays root (trên cùng) ----
            var overlays = K.Rect("Overlays", hudRt);
            K.Full(overlays);

            var pause = BuildPauseModal(overlays, out var resumeBtn, out var soundTg, out var vibTg,
                out var exitBtn, out var gearBtn);
            var confirm = BuildConfirmModal(overlays, out var confirmYes, out var confirmNo);
            var settings = BuildSettingsModal(overlays, out var musicSl, out var sfxSl, out var hapticTg, out var settingsClose);
            var revive = BuildReviveModal(overlays, out var reviveCount, out var reviveAd, out var reviveSkip);
            var levelUp = BuildLevelUpOverlay(overlays, out var perkBtns);
            var gameOver = BuildGameOver(overlays, out var replayBtn, out var homeBtn);
            var victory = BuildVictory(overlays, out var victoryHomeBtn);
            var ftue = BuildFtue(overlays, joyRt, out var ftueSkip);

            var resumeCount = K.Text(overlays, "ResumeCount", "3", 200, UITheme.Gold, FontStyles.Bold);
            K.Place(resumeCount.rectTransform, A.C, Vector2.zero, new Vector2(300, 240));
            resumeCount.gameObject.SetActive(false);

            // ---- controllers + wiring ----
            var ctrl = hudGo.AddComponent<HudController>();
            var soCtrl = new SerializedObject(ctrl);
            K.Wire(soCtrl, "healthFillRect", hpFill.rectTransform);
            K.Wire(soCtrl, "healthFillImage", hpFill);
            K.Wire(soCtrl, "healthLabel", hpLabel);
            K.Wire(soCtrl, "wavePill", waveTxt);
            K.Wire(soCtrl, "coinPill", coinLbl);
            K.Wire(soCtrl, "pauseButton", pauseBtn);
            K.Wire(soCtrl, "bombButton", bombBtn);
            K.Wire(soCtrl, "bombLabel", bombLbl);
            K.Wire(soCtrl, "weaponButton", wepBtn);
            K.Wire(soCtrl, "weaponIcon", wepIcon);
            K.Wire(soCtrl, "weaponLabel", wepLbl);
            K.Wire(soCtrl, "ammoRing", ring);
            K.Wire(soCtrl, "victoryPanel", victory);
            K.Wire(soCtrl, "prototypeCatalog", UIThumbnailGenerator.EnsureCatalogAsset());
            soCtrl.ApplyModifiedPropertiesWithoutUndo();

            var run = hudGo.AddComponent<RunOverlays>();
            var soRun = new SerializedObject(run);
            K.Wire(soRun, "pauseRoot", pause);
            K.Wire(soRun, "resumeButton", resumeBtn);
            K.Wire(soRun, "soundToggle", soundTg);
            K.Wire(soRun, "vibrateToggle", vibTg);
            K.Wire(soRun, "exitButton", exitBtn);
            K.Wire(soRun, "settingsButton", gearBtn);
            K.Wire(soRun, "confirmRoot", confirm);
            K.Wire(soRun, "confirmYesButton", confirmYes);
            K.Wire(soRun, "confirmNoButton", confirmNo);
            K.Wire(soRun, "resumeCountText", resumeCount);
            K.Wire(soRun, "settingsRoot", settings);
            K.Wire(soRun, "musicSlider", musicSl);
            K.Wire(soRun, "sfxSlider", sfxSl);
            K.Wire(soRun, "hapticToggle", hapticTg);
            K.Wire(soRun, "settingsCloseButton", settingsClose);
            K.Wire(soRun, "reviveRoot", revive);
            K.Wire(soRun, "reviveCountText", reviveCount);
            K.Wire(soRun, "reviveAdButton", reviveAd);
            K.Wire(soRun, "reviveSkipButton", reviveSkip);
            K.Wire(soRun, "levelUpRoot", levelUp);
            K.Wire(soRun, "gameOverRoot", gameOver);
            K.Wire(soRun, "replayButton", replayBtn);
            K.Wire(soRun, "homeButton", homeBtn);
            K.Wire(soRun, "victoryRoot", victory);
            K.Wire(soRun, "victoryHomeButton", victoryHomeBtn);
            K.Wire(soRun, "ftueRoot", ftue);
            K.Wire(soRun, "ftueSkipButton", ftueSkip);
            var pPerks = soRun.FindProperty("perkButtons");
            pPerks.arraySize = perkBtns.Length;
            for (int i = 0; i < perkBtns.Length; i++)
                pPerks.GetArrayElementAtIndex(i).objectReferenceValue = perkBtns[i];
            soRun.ApplyModifiedPropertiesWithoutUndo();

            UIPrefabizer.ReconnectAfterRebuild(hudGo, $"{UIPrefabizer.ScreensDir}/UI_Hud.prefab");

            EditorSceneManager.MarkSceneDirty(hudGo.scene);
            EditorSceneManager.SaveScene(hudGo.scene);
            Debug.Log("[HudInstaller] HUD + overlays rebuilt + prefab reconnected.");
        }

        // ============================================================ overlays

        static GameObject BuildPauseModal(RectTransform parent, out Button resume, out Toggle sound,
            out Toggle vibrate, out Button exit, out Button gear)
        {
            var root = K.Modal(parent, "PauseModal", 800, out var panel, out _);
            panel.sizeDelta = new Vector2(800, 640);

            var title = K.Text(panel, "Title", "PAUSED", UITheme.FontSub + 8, UITheme.Green, FontStyles.Bold);
            K.Place(title.rectTransform, A.TC, new Vector2(0, -40), new Vector2(600, 64));

            gear = K.Image(panel, "GearBtn", K.Rounded24, UITheme.Surface2).gameObject.AddComponent<Button>();
            var gearRt = (RectTransform)gear.transform;
            K.Place(gearRt, A.TR, new Vector2(-24, -24), new Vector2(72, 72));
            gear.targetGraphic = gear.GetComponent<Image>();
            K.IconImage(gearRt, "Icon", "Icon_Setting", A.C, Vector2.zero, new Vector2(40, 40));

            resume = K.BtnGreen(panel, "ResumeBtn", "RESUME", new Vector2(720, 120), A.TC, new Vector2(0, -130));

            sound = ToggleRow(panel, "SoundRow", "Sound", -290);
            vibrate = ToggleRow(panel, "VibRow", "Vibration", -390);

            exit = K.BtnDanger(panel, "ExitBtn", "QUIT", new Vector2(720, 110), A.BC, new Vector2(0, 32));
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        static Toggle ToggleRow(RectTransform panel, string name, string label, float y)
        {
            var row = K.Rect(name, panel);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = new Vector2(40, 0); row.offsetMax = new Vector2(-40, 0);
            row.sizeDelta = new Vector2(row.sizeDelta.x, 80);
            row.anchoredPosition = new Vector2(0, y);
            var lbl = K.Text(row, "Label", label, UITheme.FontBody, UITheme.TextMain, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            K.Full(lbl.rectTransform, 8, 0, 0, 0);
            return K.Toggle(row, "Toggle", A.MR, new Vector2(-8, 0), true);
        }

        static GameObject BuildConfirmModal(RectTransform parent, out Button yes, out Button no)
        {
            var root = K.Modal(parent, "ConfirmModal", 640, out var panel, out _);
            panel.sizeDelta = new Vector2(640, 320);
            var t = K.Text(panel, "Title", "Quit run?  Rewards lost", UITheme.FontBody + 4, UITheme.TextMain, FontStyles.Bold);
            K.Place(t.rectTransform, A.TC, new Vector2(0, -48), new Vector2(560, 60));
            yes = K.BtnDanger(panel, "Yes", "QUIT", new Vector2(260, 96), A.BC, new Vector2(-140, 40));
            no = K.BtnGreen(panel, "No", "STAY", new Vector2(260, 96), A.BC, new Vector2(140, 40));
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        static GameObject BuildSettingsModal(RectTransform parent, out Slider music, out Slider sfx,
            out Toggle haptic, out Button close)
        {
            var root = K.Modal(parent, "SettingsModal", 840, out var panel, out var dim);
            panel.sizeDelta = new Vector2(840, 720);
            close = dim;   // tap ngoài panel = đóng

            var title = K.Text(panel, "Title", "SETTINGS", UITheme.FontSub + 8, UITheme.TextMain, FontStyles.Bold);
            K.Place(title.rectTransform, A.TC, new Vector2(0, -36), new Vector2(500, 64));

            music = SliderRow(panel, "MusicRow", "Music", -140);
            sfx = SliderRow(panel, "SfxRow", "Sound FX", -260);

            var vibRow = K.Rect("VibRow", panel);
            vibRow.anchorMin = new Vector2(0, 1); vibRow.anchorMax = new Vector2(1, 1);
            vibRow.pivot = new Vector2(0.5f, 1);
            vibRow.offsetMin = new Vector2(48, 0); vibRow.offsetMax = new Vector2(-48, 0);
            vibRow.sizeDelta = new Vector2(vibRow.sizeDelta.x, 80);
            vibRow.anchoredPosition = new Vector2(0, -380);
            var vl = K.Text(vibRow, "Label", "Vibration", UITheme.FontBody, UITheme.TextMain, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            K.Full(vl.rectTransform);
            haptic = K.Toggle(vibRow, "Toggle", A.MR, Vector2.zero, true);

            var langRow = K.Rect("LangRow", panel);
            langRow.anchorMin = new Vector2(0, 1); langRow.anchorMax = new Vector2(1, 1);
            langRow.pivot = new Vector2(0.5f, 1);
            langRow.offsetMin = new Vector2(48, 0); langRow.offsetMax = new Vector2(-48, 0);
            langRow.sizeDelta = new Vector2(langRow.sizeDelta.x, 80);
            langRow.anchoredPosition = new Vector2(0, -490);
            var ll = K.Text(langRow, "Label", "Language", UITheme.FontBody, UITheme.TextMain, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            K.Full(ll.rectTransform);
            var vi = K.Image(langRow, "VI", K.Pill, UITheme.Surface2);
            K.Place(vi.rectTransform, A.MR, new Vector2(-110, 0), new Vector2(96, 60));
            var viT = K.Text(vi.rectTransform, "L", "VI", UITheme.FontLabel, UITheme.TextDim, FontStyles.Bold);
            K.Full(viT.rectTransform);
            var en = K.Image(langRow, "EN", K.Pill, UITheme.Green);
            K.Place(en.rectTransform, A.MR, new Vector2(0, 0), new Vector2(96, 60));
            var enT = K.Text(en.rectTransform, "L", "EN", UITheme.FontLabel, Color.white, FontStyles.Bold);
            K.Full(enT.rectTransform);

            var restore = K.Text(panel, "Restore", "Restore purchases  [PLACEHOLDER]", 28, UITheme.TextDim, FontStyles.Underline);
            K.Place(restore.rectTransform, A.BC, new Vector2(0, 36), new Vector2(600, 44));

            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        static Slider SliderRow(RectTransform panel, string name, string label, float y)
        {
            var row = K.Rect(name, panel);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = new Vector2(48, 0); row.offsetMax = new Vector2(-48, 0);
            row.sizeDelta = new Vector2(row.sizeDelta.x, 100);
            row.anchoredPosition = new Vector2(0, y);
            var lbl = K.Text(row, "Label", label, UITheme.FontBody, UITheme.TextMain, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            K.Place(lbl.rectTransform, A.ML, new Vector2(0, 0), new Vector2(260, 60));

            var sRoot = K.Rect("Slider", row);
            K.Place(sRoot, A.MR, Vector2.zero, new Vector2(420, 44));
            var slider = sRoot.gameObject.AddComponent<Slider>();

            var track = K.Image(sRoot, "Bg", K.Pill, UITheme.Surface2, false);
            track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0, 16);

            var fillArea = K.Rect("Fill Area", sRoot);
            fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(0, -8); fillArea.offsetMax = new Vector2(-22, 8);
            var fill = K.Image(fillArea, "Fill", K.Pill, Color.white, false);
            fill.rectTransform.sizeDelta = new Vector2(22, 0);

            var handleArea = K.Rect("Handle Slide Area", sRoot);
            K.Full(handleArea, 11, 0, 11, 0);
            var handle = K.Image(handleArea, "Handle", K.Circle, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(44, 44);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.value = 1f;
            return slider;
        }

        static GameObject BuildReviveModal(RectTransform parent, out TMP_Text count, out Button ad, out Button skip)
        {
            var root = K.Modal(parent, "ReviveModal", 760, out var panel, out _);
            panel.sizeDelta = new Vector2(760, 620);

            var title = K.Text(panel, "Title", "REVIVE?", UITheme.FontSub + 8, UITheme.TextMain, FontStyles.Bold);
            K.Place(title.rectTransform, A.TC, new Vector2(0, -36), new Vector2(500, 64));

            var circle = K.Image(panel, "CountCircle", K.Ring, UITheme.Gold, false);
            K.Place(circle.rectTransform, A.TC, new Vector2(0, -120), new Vector2(200, 200));
            count = K.Text(circle.rectTransform, "Count", "5", 96, UITheme.Gold, FontStyles.Bold);
            K.Full(count.rectTransform);

            ad = K.BtnGreen(panel, "AdBtn", "Watch ad", new Vector2(640, 120), A.TC, new Vector2(0, -360));
            var adChip = K.Image((RectTransform)ad.transform, "AdChip", K.Pill, UITheme.Gold, false);
            K.Place(adChip.rectTransform, A.TR, new Vector2(-10, -10), new Vector2(72, 44));
            var adT = K.Text(adChip.rectTransform, "L", "AD", 24, UITheme.OnGold, FontStyles.Bold);
            K.Full(adT.rectTransform);

            skip = K.BtnGhost(panel, "SkipBtn", "No thanks", new Vector2(280, 88), A.BC, new Vector2(0, 28));
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        static GameObject BuildLevelUpOverlay(RectTransform parent, out Button[] perks)
        {
            var root = K.Rect("LevelUpOverlay", parent);
            K.Full(root);
            var dim = K.Image(root, "Dim", null, new Color(0, 0, 0, 0.7f));
            K.Full(dim.rectTransform);

            var title = K.Text(root, "Title", "LEVEL UP!", 96, UITheme.Gold, FontStyles.Bold);
            K.Place(title.rectTransform, A.TC, new Vector2(0, -360), new Vector2(800, 120));

            string[] names = { "Fire Rate", "Armor", "Move Speed" };
            string[] descs = { "+15% fire rate", "+20% armor", "-8% enemy move speed" };
            int[] rarities = { 2, 3, 0 };
            perks = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                float y = 238 - i * 198;
                var card = K.Card(root, $"Perk{i}", A.C, new Vector2(0, y), new Vector2(800, 170),
                    out var glow, out var border, out _);
                border.color = UITheme.RarityColor(rarities[i]); border.gameObject.SetActive(true);
                if (i == 1) { glow.color = UITheme.Alpha(UITheme.RarityColor(3), UITheme.GlowAlpha); glow.gameObject.SetActive(true); card.localScale = Vector3.one * 1.05f; }
                var icon = K.Image(card, "Icon", K.Rounded24, UITheme.Surface2, false);
                K.Place(icon.rectTransform, A.ML, new Vector2(28, 0), new Vector2(72, 72));
                var mini = K.Image(icon.rectTransform, "Mini", K.Circle, UITheme.RarityColor(rarities[i]), false);
                K.Place(mini.rectTransform, A.C, Vector2.zero, new Vector2(36, 36));
                var nm = K.Text(card, "Name", names[i], UITheme.FontSub, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                K.Place(nm.rectTransform, A.ML, new Vector2(128, 28), new Vector2(500, 56));
                var ds = K.Text(card, "Desc", descs[i], 30, UITheme.TextDim, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                K.Place(ds.rectTransform, A.ML, new Vector2(128, -28), new Vector2(560, 44));
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = card.Find("Bg").GetComponent<Image>();
                perks[i] = btn;
            }

            var hint = K.Text(root, "Hint", "Tap to choose", 30, UITheme.TextDim, FontStyles.Normal);
            K.Place(hint.rectTransform, A.BC, new Vector2(0, 140), new Vector2(400, 44));
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        static GameObject BuildGameOver(RectTransform parent, out Button replay, out Button home)
        {
            var root = K.Rect("GameOverScreen", parent);
            K.Full(root);
            var bg = K.Image(root, "Bg", null, UITheme.Bg);
            K.Full(bg.rectTransform);
            var vign = K.Image(root, "Vignette", K.RedVignette, new Color(1, 1, 1, 0.5f), false);
            K.Full(vign.rectTransform);

            var banner = K.Text(root, "Banner", "RUN OVER", 80, UITheme.Gold, FontStyles.Bold);
            K.Place(banner.rectTransform, A.TC, new Vector2(0, -140), new Vector2(700, 100));

            var recPill = K.Image(root, "RecordPill", K.Pill, UITheme.Alpha(UITheme.Surface, 0.9f), false);
            K.Place(recPill.rectTransform, A.TC, new Vector2(0, -260), new Vector2(420, 72));
            var recT = K.Text(recPill.rectTransform, "L", "BEST  Wave 12", 34, UITheme.TextDim, FontStyles.Bold);
            K.Full(recT.rectTransform);

            // Payout card — số liệu placeholder (WalletService/payout backend chưa có)
            var card = K.Card(root, "PayoutCard", A.TC, new Vector2(0, -560), new Vector2(952, 520), out _, out _, out _);
            string[] rows = { "Collected in run", "Wave bonus", "First-clear bonus" };
            string[] vals = { "+1,250", "+350", "+580" };
            for (int i = 0; i < 3; i++)
            {
                float y = -36 - i * 84;
                var l = K.Text(card, $"Row{i}L", rows[i], UITheme.FontBody, UITheme.TextDim, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                K.Place(l.rectTransform, A.TL, new Vector2(40, y), new Vector2(480, 60));
                var v = K.Text(card, $"Row{i}V", vals[i], 38, UITheme.Gold, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
                K.Place(v.rectTransform, A.TR, new Vector2(-40, y), new Vector2(300, 60));
            }
            var divider = K.Image(card, "Divider", null, UITheme.Hairline, false);
            K.Place(divider.rectTransform, A.TC, new Vector2(0, -300), new Vector2(872, 2));
            var totL = K.Text(card, "TotalL", "Total", UITheme.FontSub, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(totL.rectTransform, A.TL, new Vector2(40, -320), new Vector2(300, 70));
            var totV = K.Text(card, "TotalV", "2,180", 64, UITheme.Gold, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            K.Place(totV.rectTransform, A.TR, new Vector2(-40, -320), new Vector2(400, 70));
            var kc = K.Text(card, "KcRow", "Gems collected  +5", 34, UITheme.Cyan, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(kc.rectTransform, A.BL, new Vector2(40, 24), new Vector2(500, 52));

            K.ProgressBar(root, "PassXpBar", A.TC, new Vector2(0, -1130), new Vector2(952, 20), 0.4f, UITheme.RarityColor(3));
            var passLbl = K.Text(root, "PassXpLabel", "PASS XP +40", UITheme.FontLabel, UITheme.TextDim, FontStyles.Bold);
            K.Place(passLbl.rectTransform, A.TC, new Vector2(0, -1160), new Vector2(400, 40));

            var shopLink = K.Text(root, "ShopLink", "<color=#F5B841>New guns in the Shop →</color>", 34, UITheme.Gold, FontStyles.Bold);
            K.Place(shopLink.rectTransform, A.TC, new Vector2(0, -1230), new Vector2(500, 48));

            replay = K.BtnPrimary(root, "ReplayBtn", "PLAY AGAIN", new Vector2(960, 150), A.BC, new Vector2(0, 220));
            home = K.BtnGhost(root, "HomeBtn", "HOME", new Vector2(960, 110), A.BC, new Vector2(0, 88));
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        static GameObject BuildVictory(RectTransform parent, out Button home)
        {
            var root = K.Rect("VictoryPanel", parent);
            K.Full(root);
            var dim = K.Image(root, "Dim", null, new Color(0.015f, 0.08f, 0.035f, 0.94f));
            K.Full(dim.rectTransform);
            var t = K.Text(root, "Label", "SURVIVED!", 96, UITheme.Green, FontStyles.Bold);
            K.Place(t.rectTransform, A.C, new Vector2(0, 180), new Vector2(800, 140));
            var sub = K.Text(root, "Subtitle", "Area secured", UITheme.FontBody, UITheme.TextDim, FontStyles.Normal);
            K.Place(sub.rectTransform, A.C, new Vector2(0, 80), new Vector2(700, 64));
            home = K.BtnPrimary(root, "BackToMapBtn", "BACK TO MAP", new Vector2(760, 132),
                A.C, new Vector2(0, -100));
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        static GameObject BuildFtue(RectTransform parent, RectTransform joystick, out Button skip)
        {
            var root = K.Rect("FtueOverlay", parent);
            K.Full(root);
            // Dim nhẹ toàn màn (cutout stencil thật để đợt asset/shader — ring highlight thay thế).
            // ray=false bắt buộc: dim mà chặn raycast thì joystick/pause/bomb chết hết trong lúc FTUE,
            // người chơi không làm được chính cái hành động tutorial đang dạy.
            var dim = K.Image(root, "Dim", null, new Color(0, 0, 0, 0.55f), false);
            K.Full(dim.rectTransform);

            var ring = K.Image(root, "HighlightRing", K.Dashed, UITheme.Gold, false);
            K.Place(ring.rectTransform, A.BL, new Vector2(96 + 144, 96 + 144), new Vector2(640, 640));
            ring.rectTransform.anchoredPosition = new Vector2(240, 240);

            var tip = K.Image(root, "TooltipChip", K.Pill, UITheme.Gold, false);
            K.Place(tip.rectTransform, A.BL, new Vector2(120, 620), new Vector2(360, 84));
            var tipT = K.Text(tip.rectTransform, "L", "Drag to move!", 34, UITheme.OnGold, FontStyles.Bold);
            K.Full(tipT.rectTransform);

            // FtueOverlay phủ full canvas, KHÔNG nằm dưới Safe — TR (-32,-32) rơi vào vùng notch
            // trên máy thật nên không bấm được. Đặt BC trên hàng step dots, xa notch lẫn joystick.
            skip = K.BtnGhost(root, "SkipBtn", "Skip", new Vector2(220, 88), A.BC, new Vector2(0, 150));

            var dots = K.Rect("StepDots", root);
            K.Place(dots, A.BC, new Vector2(0, 64), new Vector2(120, 16));
            for (int i = 0; i < 3; i++)
            {
                var d = K.Image(dots, "Dot" + i, K.Circle, i == 0 ? UITheme.Gold : UITheme.Surface2, false);
                K.Place(d.rectTransform, A.ML, new Vector2(i * 44, 0), new Vector2(16, 16));
            }
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        static bool EnsureScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path == MapScene) return false;
            if (!File.Exists(MapScene)) { Debug.LogError($"[HudInstaller] Không thấy {MapScene}"); return true; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return true;
            EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
            return false;
        }
    }
}
