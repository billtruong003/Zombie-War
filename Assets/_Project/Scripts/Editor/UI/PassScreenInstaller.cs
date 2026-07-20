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
    /// Màn 08 BATTLE PASS theo spec §4.5. Idempotent; tự wire Hub.passButton + Shop cho GachaLink.
    /// Backend chưa có — mọi số liệu là placeholder trình bày.
    /// </summary>
    public static class PassScreenInstaller
    {
        const string MenuScene = "Assets/_Project/Scenes/Menu.unity";

        [MenuItem("ZombieWar/UI/Authoring/Rebuild Pass Screen (Destructive)...")]
        public static void BuildInteractive()
        {
            if (!UIPrefabizer.ConfirmDestructive("PassScreen")) return;
            Build();
        }

        public static void Build()
        {
            if (EnsureScene()) return;

            var canvas = UIRootInstaller.EnsureRoot();
            var root = (RectTransform)canvas.transform;

            var old = root.Find("PassScreen");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("PassScreen", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            K.Full(rt);
            var scr = go.AddComponent<PassScreen>();

            var bg = K.Image(rt, "Bg", null, UITheme.Bg, false);
            K.Full(bg.rectTransform);

            var safe = K.Rect("Safe", rt);
            K.Full(safe);
            safe.gameObject.AddComponent<SafeArea>();

            var back = K.HeaderBack(safe, "BATTLE PASS", out _);

            // ---- MIỄN PHÍ + track ngang 200×200 ----
            var ml = K.Text(safe, "MilestoneLabel", "MIỄN PHÍ", UITheme.FontLabel, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(ml.rectTransform, A.TL, new Vector2(32, -150), new Vector2(300, 40));

            var track = K.Rect("TrackScroll", safe);
            track.anchorMin = new Vector2(0, 1); track.anchorMax = new Vector2(1, 1);
            track.pivot = new Vector2(0.5f, 1);
            track.offsetMin = new Vector2(32, 0); track.offsetMax = new Vector2(-32, 0);
            track.sizeDelta = new Vector2(track.sizeDelta.x, 240);
            track.anchoredPosition = new Vector2(0, -200);
            track.gameObject.AddComponent<RectMask2D>();
            var hscroll = track.gameObject.AddComponent<ScrollRect>();
            hscroll.horizontal = true; hscroll.vertical = false;
            hscroll.movementType = ScrollRect.MovementType.Elastic;
            var tContent = K.Rect("Content", track);
            tContent.anchorMin = new Vector2(0, 0); tContent.anchorMax = new Vector2(0, 1);
            tContent.pivot = new Vector2(0, 0.5f);
            tContent.offsetMin = Vector2.zero; tContent.offsetMax = Vector2.zero;
            var hlg = tContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.padding = new RectOffset(4, 4, 20, 20);
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            var tFit = tContent.gameObject.AddComponent<ContentSizeFitter>();
            tFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            hscroll.content = tContent;

            string[] tileIcons = { "Icon_Coin", "Icon_Gem", "Icon_Ticket01", "Icon_Coin", "Icon_Crown", "Icon_Gem" };
            for (int i = 0; i < 6; i++)
            {
                bool claimed = i == 0;
                bool current = i == 1;
                bool locked = i >= 3;
                var tile = K.RarityCard(tContent, $"Tile{i}", A.C, Vector2.zero, new Vector2(200, 200),
                    i % 3 == 2 ? 3 : 1, "", i % 3 == 1 ? "" : "", locked, false, out _);
                var iconImg = tile.Find("Icon")?.GetComponent<Image>();
                var rewardSprite = K.Icon(tileIcons[i]);
                if (iconImg != null && rewardSprite != null)
                {
                    iconImg.sprite = rewardSprite;
                    iconImg.color = Color.white;
                    iconImg.preserveAspect = true;
                }
                if (claimed)
                {
                    var bgImg = tile.Find("Bg").GetComponent<Image>();
                    bgImg.color = UITheme.Alpha(UITheme.Surface, 0.5f);
                    var check = K.Image((RectTransform)tile, "Check", K.Circle, UITheme.Green, false);
                    K.Place(check.rectTransform, A.C, Vector2.zero, new Vector2(44, 44));
                    var inner = K.Image(check.rectTransform, "Inner", K.Circle, Color.white, false);
                    K.Place(inner.rectTransform, A.C, Vector2.zero, new Vector2(18, 18));
                }
                if (current)
                {
                    tile.Find("Border").GetComponent<Image>().color = UITheme.Gold;
                    var glowImg = tile.Find("Glow").GetComponent<Image>();
                    glowImg.color = UITheme.Alpha(UITheme.Gold, UITheme.GlowAlpha);
                }
            }

            // ---- Premium strip (khoá, không bán) ----
            var prem = K.Card(safe, "PremiumBanner", A.TC, new Vector2(0, -480), new Vector2(1016, 120), out _, out _, out var pbg);
            pbg.color = UITheme.Surface2;
            K.LockGlyph(prem, 48).anchoredPosition = new Vector2(-440, 0);
            var pt = K.Text(prem, "Label", "Premium Pass  [PLACEHOLDER]", UITheme.FontBody, UITheme.TextDim, FontStyles.Normal, TextAlignmentOptions.Center);
            K.Full(pt.rectTransform, 100, 0, 40, 0);

            // ---- Nhiệm vụ hôm nay ----
            var ql = K.Text(safe, "QuestLabel", "Nhiệm vụ hôm nay", UITheme.FontSub, UITheme.TextMain, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            K.Place(ql.rectTransform, A.TL, new Vector2(32, -640), new Vector2(600, 60));

            string[] quests = { "Giết 200 zombie", "Sống sót wave 5", "Nhặt 500 coin" };
            float[] progress = { 1f, 0.6f, 0.35f };
            var claims = new System.Collections.Generic.List<Button>();
            for (int i = 0; i < 3; i++)
            {
                float y = -730 - i * 156;
                var row = K.Card(safe, $"Quest{i}", A.TC, new Vector2(0, y), new Vector2(1016, 140), out _, out _, out _);
                var qn = K.Text(row, "Name", quests[i], UITheme.FontBody, UITheme.TextMain, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                K.Place(qn.rectTransform, A.TL, new Vector2(32, -20), new Vector2(600, 44));
                K.ProgressBar(row, "Bar", A.BL, new Vector2(32, 28), new Vector2(600, 20), progress[i], UITheme.Green);
                if (progress[i] >= 1f)
                {
                    var claim = K.BtnGreen(row, "Claim", "CLAIM", new Vector2(180, 80), A.MR, new Vector2(-32, 0));
                    claim.GetComponentInChildren<TMP_Text>().fontSize = 30;
                    claims.Add(claim);
                }
                else
                {
                    var counter = K.Text(row, "Counter", $"{Mathf.RoundToInt(progress[i] * 100)}%", 34, UITheme.TextDim, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
                    K.Place(counter.rectTransform, A.MR, new Vector2(-32, 0), new Vector2(160, 60));
                }
            }

            // ---- Gacha link ----
            var linkRt = K.Rect("GachaLink", safe);
            K.Place(linkRt, A.BC, new Vector2(0, 40), new Vector2(420, 72));
            var linkHit = linkRt.gameObject.AddComponent<Image>();
            linkHit.color = Color.clear;
            var gachaLink = linkRt.gameObject.AddComponent<Button>();
            gachaLink.targetGraphic = linkHit;
            var linkTxt = K.Text(linkRt, "Label", "<color=#F5B841>Quay Gacha →</color>", 34, UITheme.Gold, FontStyles.Bold);
            K.Full(linkTxt.rectTransform);

            // ---- wire ----
            var shop = Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
            var so = new SerializedObject(scr);
            K.Wire(so, "backButton", back);
            K.Wire(so, "gachaLinkButton", gachaLink);
            K.Wire(so, "shopScreen", shop);
            var pClaims = so.FindProperty("claimButtons");
            pClaims.arraySize = claims.Count;
            for (int i = 0; i < claims.Count; i++)
                pClaims.GetArrayElementAtIndex(i).objectReferenceValue = claims[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);

            var hub = Object.FindFirstObjectByType<HubScreen>(FindObjectsInactive.Include);
            if (hub != null)
            {
                var soHub = new SerializedObject(hub);
                K.Wire(soHub, "passScreen", scr);
                soHub.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("[PassScreenInstaller] Không thấy HubScreen — PASS tab chưa wire.");

            UIPrefabizer.ReconnectAfterRebuild(go, $"{UIPrefabizer.ScreensDir}/UI_PassScreen.prefab");

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            Debug.Log("[PassScreenInstaller] PassScreen built + wired Hub PASS tab + prefab reconnected.");
        }

        static bool EnsureScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path == MenuScene) return false;
            if (!File.Exists(MenuScene)) { Debug.LogError($"[PassScreenInstaller] Không thấy {MenuScene}"); return true; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return true;
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            return false;
        }
    }
}
