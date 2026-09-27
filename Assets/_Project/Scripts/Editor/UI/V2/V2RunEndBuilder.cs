using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds the v2 Revive and Result panels (approved V2_Revive / V2_Result mockups) into
    /// UI_Hud.prefab under Overlays as "RunEndV2", wires RunOverlays.endV2, and fixes the quit
    /// dialog, which still said rewards are lost although every ending keeps every coin (M8).
    /// Idempotent: the old RunEndV2 child is replaced.
    /// </summary>
    public static class V2RunEndBuilder
    {
        const string Hud = "Assets/_Project/UI/Prefabs/Screens/UI_Hud.prefab";

        [MenuItem("HordeCall/UI v2/Build Run End (into UI_Hud)")]
        public static string Build()
        {
            var hud = PrefabUtility.LoadPrefabContents(Hud);
            try
            {
                var overlays = hud.transform.Find("Overlays");
                if (overlays == null) return "Overlays not found";
                var old = overlays.Find("RunEndV2"); if (old != null) Object.DestroyImmediate(old.gameObject);

                var root = Node(overlays, "RunEndV2");
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
                var end = root.gameObject.AddComponent<RunEndV2>();
                UIKitV2.NoTheme = true;   // the revive overlay stays dark in every theme
                try { Revive(root, end); } finally { UIKitV2.NoTheme = false; }
                Result(root, end);

                // Same scaling as the menu (Expand @1080x1920): taller phones get more room and a
                // tablet no longer pushes the revive and result buttons off the bottom.
                var scaler = hud.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                var ro = hud.GetComponentInChildren<RunOverlays>(true);
                Wire(ro, "endV2", end);
                var confirm = new SerializedObject(ro).FindProperty("confirmRoot").objectReferenceValue as GameObject;
                var title = confirm != null ? confirm.transform.Find("Title")?.GetComponent<TMP_Text>() : null;
                if (title == null && confirm != null) foreach (var t in confirm.GetComponentsInChildren<TMP_Text>(true)) if (t.text.Contains("lost")) title = t;
                if (title != null) title.text = "End the run?\nYou keep every coin";

                PrefabUtility.SaveAsPrefabAsset(hud, Hud);
                return "RunEndV2 built into UI_Hud";
            }
            finally { PrefabUtility.UnloadPrefabContents(hud); }
        }

        /// A standalone copy for review renders (UiShot needs a prefab of its own).
        [MenuItem("HordeCall/UI v2/Render Run End (review)")]
        public static string RenderReview()
        {
            var r = ScreenRoot("RunEndReview");
            try
            {
                var end = r.gameObject.AddComponent<RunEndV2>();
                UIKitV2.NoTheme = true; try { Revive(r, end); } finally { UIKitV2.NoTheme = false; } Result(r, end);
                r.Find("Revive").gameObject.SetActive(true);
                var p1 = PrefabUtility.SaveAsPrefabAsset(r.gameObject, "Assets/_Project/UI/Prefabs/V2/Review_Revive.prefab");
                r.Find("Revive").gameObject.SetActive(false); r.Find("Result").gameObject.SetActive(true);
                var p2 = PrefabUtility.SaveAsPrefabAsset(r.gameObject, "Assets/_Project/UI/Prefabs/V2/Review_Result.prefab");
                var a = UiShot.RenderAll(AssetDatabase.GetAssetPath(p1), "Revive");
                var b = UiShot.RenderAll(AssetDatabase.GetAssetPath(p2), "Result");
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(p1));
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(p2));
                return a + " | " + b;
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        static void Revive(RectTransform root, RunEndV2 e)
        {
            var p = Fill(Node(root, "Revive"), 0, 0, 0, 0);
            Flat(p, Hex("1b1e27"), true);
            var glow = Box(Node(p, "Glow"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), 0, -300, 520, 520);
            var gi = glow.gameObject.AddComponent<Image>(); gi.sprite = Spr("glow_soft"); gi.color = new Color(0.45f, 0.12f, 0.14f, 0.9f); gi.raycastTarget = false;
            var col = Fill(Node(p, "Col"), 22, 0, 22, 0);
            Title(TopBand(Node(col, "Title"), 70, 48), "DOWN!", 40f, Ink, TextAlignmentOptions.Center);
            Body(TopBand(Node(col, "Sub"), 122, 20), "Get back up and keep your run going", 14f, Hex("d9c2c4"), TextAlignmentOptions.Center);

            var ringBg = Box(Node(col, "Ring"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -160, 150, 150);
            var rb = ringBg.gameObject.AddComponent<Image>(); rb.sprite = Spr("circle"); rb.color = Card; rb.raycastTarget = false;
            var fill = Fill(Node(ringBg, "Fill"), 0, 0, 0, 0).gameObject.AddComponent<Image>();
            fill.sprite = Spr("circle"); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Radial360; fill.fillOrigin = (int)Image.Origin360.Top;
            fill.color = Yellow; fill.fillAmount = 0.7f; fill.raycastTarget = false;
            Wire(e, "ring", fill);
            var inner = Fill(Node(ringBg, "Inner"), 13, 13, 13, 13).gameObject.AddComponent<Image>(); inner.sprite = Spr("circle"); inner.color = Deep; inner.raycastTarget = false;
            Wire(e, "seconds", Title(Box(Node(ringBg, "N"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 8, 100, 50), "7", 46f, Ink, TextAlignmentOptions.Center));
            Label(Box(Node(ringBg, "S"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -26, 100, 14), "SECONDS").alignment = TextAlignmentOptions.Center;

            var card = TopBand(Node(col, "Card"), 328, 92);
            Surface(card, Card, RCard);
            Body(TopBand(Node(card, "L0"), 12, 20, 12, 120), "You are carrying", 13f);
            Wire(e, "carried", CoinValue(card, 12, Ink));
            Wire(e, "costLabel", Body(TopBand(Node(card, "L1"), 38, 20, 12, 120), "Revive 2 of 3 costs", 13f));
            Wire(e, "costValue", CoinValue(card, 38, Yellow));
            var note = Shrink(Body(TopBand(Node(card, "Note"), 62, 26, 12, 12), "Price doubles each time. This one costs more than you carry.", 11f, Hex("e5a0a2")), 0.8f);
            note.enableWordWrapping = true;
            Wire(e, "costNote", note);

            var ad = TopBand(Node(col, "Ad"), 432, 60);
            Wire(e, "adButton", Button(ad, "WATCH AD · FREE", Role.Claim, 20f));
            var coin = TopBand(Node(col, "Coin"), 500, 54);
            Wire(e, "coinButton", Button(coin, "REVIVE · 1,488", Role.Primary, 18f));
            Wire(e, "coinLabel", coin.Find("Face/Label").GetComponent<TextMeshProUGUI>());
            var no = TopBand(Node(col, "No"), 562, 46);
            Wire(e, "noButton", Button(no, "NO THANKS", Role.Quiet, 15f));
            Wire(e, "reviveRoot", p.gameObject);
            p.gameObject.SetActive(false);
        }

        static TextMeshProUGUI CoinValue(RectTransform card, float y, Color c)
        {
            var row = TopBand(Node(card, "V" + y), y, 20, 150, 12);
            IconImage(Box(Node(row, "I"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -70, 0, 16, 16), "Money_Coin");
            return Body(Box(Node(row, "T"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 66, 20), "1,240", 14f, c, TextAlignmentOptions.MidlineRight);
        }

        static void Result(RectTransform root, RunEndV2 e)
        {
            var p = Fill(Node(root, "Result"), 0, 0, 0, 0);
            ScreenBackground(p);
            var safe = Fill(Node(p, "Safe"), 0, 0, 0, 0); safe.gameObject.AddComponent<SafeArea>();
            Wire(e, "banner", Title(TopBand(Node(safe, "Banner"), 36, 26, 16, 16), "THE HORDE GOT YOU", 20f, Ink, TextAlignmentOptions.Center));
            Wire(e, "time", Title(TopBand(Node(safe, "Time"), 62, 72, 16, 16), "9:58", 64f, Ink, TextAlignmentOptions.Center));
            var best = Box(Node(safe, "Best"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -138, 110, 24);
            Surface(best, Yellow, RTag);
            Wire(e, "bestLabel", Body(Fill(Node(best, "T"), 4, 0, 4, 0), "NEW BEST", 11f, OnYellow, TextAlignmentOptions.Center));
            Wire(e, "newBest", best.gameObject);

            var stats = TopBand(Node(safe, "Stats"), 176, 64, 14, 14);
            Row(stats, 8, TextAnchor.MiddleLeft, true);
            Wire(e, "kills", Stat(stats, "KILLS", "412"));
            Wire(e, "level", Stat(stats, "LEVEL", "24"));
            Wire(e, "threat", Stat(stats, "THREAT", "6"));

            var coins = TopBand(Node(safe, "Coins"), 250, 124, 14, 14);
            Surface(coins, Card, RCard);
            Body(TopBand(Node(coins, "L"), 14, 24, 12, 150), "Coins kept (100%)", 14f);
            var cv = TopBand(Node(coins, "V"), 12, 28, 150, 12);
            IconImage(Box(Node(cv, "I"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -96, 0, 20, 20), "Money_Coin");
            Wire(e, "coins", Title(Box(Node(cv, "T"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 92, 28), "1,860", 24f, Ink, TextAlignmentOptions.MidlineRight));
            var dbl = TopBand(Node(coins, "Double"), 46, 48, 12, 12);
            Wire(e, "doubleButton", Button(dbl, "WATCH AD · DOUBLE TO 3,720", Role.Gem, 16f));
            Wire(e, "doubleLabel", dbl.Find("Face/Label").GetComponent<TextMeshProUGUI>());
            Body(TopBand(Node(coins, "GL"), 100, 18, 12, 150), "Gems", 13f);
            var gv = TopBand(Node(coins, "GV"), 100, 18, 150, 12);
            IconImage(Box(Node(gv, "I"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -40, 0, 16, 16), "Gem_Diamond_Purple");
            Wire(e, "gems", Body(Box(Node(gv, "T"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 36, 18), "+3", 13f, Ink, TextAlignmentOptions.MidlineRight));

            var prog = TopBand(Node(safe, "Progress"), 384, 118, 14, 14);
            Surface(prog, Card, RCard);
            Wire(e, "progressCard", prog.gameObject);
            Label(TopBand(Node(prog, "H"), 12, 14, 12, 12), "PROGRESS THIS RUN");
            var rows = new GameObject[3]; var texts = new TextMeshProUGUI[3]; var tags = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                var row = TopBand(Node(prog, "Row" + i), 34 + i * 26, 22, 12, 12);
                texts[i] = Body(Fill(Node(row, "T"), 0, 0, 110, 0), "Kill 150 zombies", 13f);
                var tag = Box(Node(row, "Tag"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 104, 18);
                Surface(tag, Green, RTag);
                tags[i] = Body(Fill(Node(tag, "T"), 4, 0, 4, 0), "DONE +100 XP", 10f, OnGreen, TextAlignmentOptions.Center);
                rows[i] = row.gameObject;
                if (i == 2)
                {
                    tag.gameObject.SetActive(false);
                    var link = Box(Node(row, "Shop"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 70, 22);
                    Body(Fill(Node(link, "T"), 0, 0, 0, 0), "Shop ›", 12f, Yellow, TextAlignmentOptions.MidlineRight);
                    var li = link.gameObject.AddComponent<Image>(); li.color = new Color(0, 0, 0, 0);
                    Wire(e, "shopLink", link.gameObject.AddComponent<Button>());
                }
            }
            WireArray(e, "progressRows", rows);
            WireArray(e, "progressText", texts);
            WireArray(e, "progressTag", tags);

            var home = BottomBand(Node(safe, "Home"), 16, 46, 14, 14);
            Wire(e, "home", Button(home, "HOME", Role.Quiet, 16f));
            var again = BottomBand(Node(safe, "Again"), 16 + 46 + 8, 62, 14, 14);
            Wire(e, "playAgain", Button(again, "PLAY AGAIN", Role.Primary, 26f));
            Wire(e, "resultRoot", p.gameObject);
            p.gameObject.SetActive(false);
        }

        static TextMeshProUGUI Stat(RectTransform row, string label, string value)
        {
            var c = Node(row, label);
            Surface(c, Card, RCard);
            var v = Title(TopBand(Node(c, "V"), 8, 30), value, 24f, Ink, TextAlignmentOptions.Center);
            Label(BottomBand(Node(c, "L"), 8, 14), label).alignment = TextAlignmentOptions.Center;
            return v;
        }
    }
}
