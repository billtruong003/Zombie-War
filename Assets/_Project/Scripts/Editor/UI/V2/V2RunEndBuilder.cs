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
                Unlock(root, end);

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
                UIKitV2.NoTheme = true; try { Revive(r, end); } finally { UIKitV2.NoTheme = false; } Result(r, end); Unlock(r, end);
                r.Find("Revive").gameObject.SetActive(true);
                var p1 = PrefabUtility.SaveAsPrefabAsset(r.gameObject, "Assets/_Project/UI/Prefabs/V2/Review_Revive.prefab");
                r.Find("Revive").gameObject.SetActive(false); r.Find("Result").gameObject.SetActive(true);
                var p2 = PrefabUtility.SaveAsPrefabAsset(r.gameObject, "Assets/_Project/UI/Prefabs/V2/Review_Result.prefab");
                r.Find("Result").gameObject.SetActive(false); r.Find("Unlock").gameObject.SetActive(true);
                var p3 = PrefabUtility.SaveAsPrefabAsset(r.gameObject, "Assets/_Project/UI/Prefabs/V2/Review_Unlock.prefab");
                var a = UiShot.RenderAll(AssetDatabase.GetAssetPath(p1), "Revive");
                var b = UiShot.RenderAll(AssetDatabase.GetAssetPath(p2), "Result");
                var c = UiShot.RenderAll(AssetDatabase.GetAssetPath(p3), "Unlock");
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(p1));
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(p2));
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(p3));
                return a + " | " + b + " | " + c;
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        static void Revive(RectTransform root, RunEndV2 e)
        {
            // Owner-approved R2_Revive: the frozen game blurred behind a dark scrim with a red edge,
            // a countdown ring around the player, revives left, the run so far, the near-miss bar,
            // then the free (ad) and coin revives and a quiet "Give up". Fits 16:9 (693 tall).
            var p = Fill(Node(root, "Revive"), 0, 0, 0, 0);
            Flat(p, Hex("1b1e27"), true);
            var bd = Fill(Node(p, "Backdrop"), 0, 0, 0, 0).gameObject.AddComponent<RawImage>();
            bd.color = Color.white; bd.raycastTarget = false;
            Wire(e, "backdrop", bd);
            var scrim = Fill(Node(p, "Scrim"), 0, 0, 0, 0).gameObject.AddComponent<Image>();
            scrim.color = new Color(0.106f, 0.118f, 0.153f, 0.72f); scrim.raycastTarget = false;
            // The vignette sprite fades out a quarter in; pushed past the screen edges so only a thin
            // red glow stays at the rim (the mockup's inset shadow), not a red wash.
            var edge = Fill(Node(p, "RedEdge"), -110, -260, -110, -260).gameObject.AddComponent<Image>();
            edge.sprite = Spr("grad_red_vignette"); edge.color = new Color(1f, 1f, 1f, 0.45f); edge.raycastTarget = false;

            var safe = Fill(Node(p, "Safe"), 0, 0, 0, 0); safe.gameObject.AddComponent<SafeArea>();
            var col = Fill(Node(safe, "Col"), 20, 0, 20, 0);
            Title(TopBand(Node(col, "Title"), 34, 48), "DOWN!", 44f, Ink, TextAlignmentOptions.Center);
            Wire(e, "nearMiss", Shrink(Body(TopBand(Node(col, "Sub"), 84, 20), "Only 0:18 from your best!", 14f, Yellow, TextAlignmentOptions.Center)));

            // Ring: track, draining yellow fill, the player's still inside a circle mask, seconds badge.
            var ring = Box(Node(col, "Ring"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -112, 184, 184);
            var track = ring.gameObject.AddComponent<Image>(); track.sprite = Spr("circle_hd"); track.color = Card; track.raycastTarget = false;
            var fill = Fill(Node(ring, "Fill"), 0, 0, 0, 0).gameObject.AddComponent<Image>();
            fill.sprite = Spr("circle_hd"); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Radial360; fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false; fill.color = Yellow; fill.fillAmount = 0.7f; fill.raycastTarget = false;
            Wire(e, "ring", fill);
            var hole = Fill(Node(ring, "Inner"), 12, 12, 12, 12);
            var hi = hole.gameObject.AddComponent<Image>(); hi.sprite = Spr("circle_hd"); hi.color = Hex("333a49"); hi.raycastTarget = false;
            hole.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var still = Fill(Node(hole, "Portrait"), 0, 0, 0, 0).gameObject.AddComponent<RawImage>(); still.raycastTarget = false;
            Wire(e, "portrait", still);
            var badge = Box(Node(ring, "Badge"), new Vector2(1, 1), new Vector2(0.5f, 0.5f), -18, -18, 54, 54);
            var bi = badge.gameObject.AddComponent<Image>(); bi.sprite = Spr("circle_hd"); bi.color = Yellow; bi.raycastTarget = false;
            Wire(e, "seconds", Title(Box(Node(badge, "N"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 5, 50, 28), "5", 24f, Ink, TextAlignmentOptions.Center));
            Body(Box(Node(badge, "S"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -14, 50, 12), "SEC", 8f, OnYellow, TextAlignmentOptions.Center);

            // Revives left.
            var hearts = Box(Node(col, "Hearts"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -304, 220, 22);
            Label(Box(Node(hearts, "L"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 110, 14), "REVIVES LEFT").alignment = TextAlignmentOptions.MidlineRight;
            var hs = new Image[3];
            for (int i = 0; i < 3; i++) hs[i] = IconImage(Box(Node(hearts, "H" + i), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 118 + i * 26, 0, 22, 22), "Heart_Red");
            WireArray(e, "hearts", hs);
            Wire(e, "heartFull", Icon("Heart_Red"));
            Wire(e, "heartEmpty", Icon("Heart_Dimmed"));

            // The run so far.
            var stats = TopBand(Node(col, "Stats"), 334, 52);
            Row(stats, 8, TextAnchor.MiddleLeft, true);
            Wire(e, "runTime", Well(stats, "TIME", "9:42", false));
            Wire(e, "runKills", Well(stats, "KILLS", "412", false));
            Wire(e, "carried", Well(stats, "CARRYING", "1,240", true));

            // Near-miss bar against the best time.
            var best = TopBand(Node(col, "Best"), 394, 46);
            Surface(best, Deep, 10f);
            Wire(e, "bestCard", best.gameObject);
            Wire(e, "bestLabel2", Label(TopBand(Node(best, "L"), 8, 14, 12, 60), "YOUR BEST 10:00"));
            var pctT = Label(TopBand(Node(best, "P"), 8, 14, 200, 12), "97%", Yellow); pctT.alignment = TextAlignmentOptions.MidlineRight;
            Wire(e, "bestPercent", pctT);
            var bar = TopBand(Node(best, "Bar"), 26, 10, 12, 12);
            Surface(bar, Card, 5f);
            var bfr = Fill(Node(bar, "Fill"), 0, 0, 0, 0);
            var bf = Surface(bfr, Yellow, 5f); bfr.anchorMax = new Vector2(0.97f, 1f);
            Wire(e, "bestFill", bf);
            var mark = Box(Node(bar, "Mark"), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 4, 20);
            Surface(mark, Ink, 2f);

            // Actions, from the bottom up.
            var no = BottomBand(Node(col, "No"), 10, 30, 110, 110);
            var nt = Body(Fill(Node(no, "T"), 0, 0, 0, 0), "<u>Give up</u>", 14f, Dim, TextAlignmentOptions.Center);
            nt.raycastTarget = true;
            Wire(e, "noButton", no.gameObject.AddComponent<Button>());
            Wire(e, "costNote", Shrink(Body(BottomBand(Node(col, "Note"), 42, 16), "Coin price doubles each revive · you carry 1,240", 11f, Dim, TextAlignmentOptions.Center)));
            var coin = BottomBand(Node(col, "Coin"), 62, 52);
            var cb = Button(coin, "REVIVE", Role.Primary, 18f);
            Wire(e, "coinButton", cb);
            var clbl = coin.Find("Face/Label") as RectTransform;
            clbl.anchorMin = new Vector2(0, 0); clbl.anchorMax = new Vector2(0.5f, 1); clbl.offsetMin = Vector2.zero; clbl.offsetMax = new Vector2(-Px(4), 0);
            clbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;
            IconImage(Box(Node(coin.Find("Face"), "Coin"), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), 4, 0, 20, 20), "Money_Coin");
            var cv = Title(Box(Node(coin.Find("Face"), "Value"), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), 28, 0, 130, 30), "1,488", 18f, Ink, TextAlignmentOptions.MidlineLeft);
            Wire(e, "coinLabel", cv);
            var ad = BottomBand(Node(col, "Ad"), 120, 62);
            var ab = Button(ad, "REVIVE FREE", Role.Claim, 22f);
            Wire(e, "adButton", ab);
            var alb = ad.Find("Face/Label") as RectTransform; alb.offsetMin = new Vector2(Px(34), 0);
            IconImage(Box(Node(ad.Find("Face"), "Video"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -86, 1, 28, 28), "Video");
            var dtrack = BottomBand(Node(ad.Find("Face"), "Drain"), 5, 5, 12, 12);
            var dr = Fill(Node(dtrack, "Fill"), 0, 0, 0, 0);
            var drain = Surface(dr, new Color(0f, 0.2f, 0.14f, 0.35f), 2.5f); dr.anchorMax = new Vector2(0.7f, 1f);
            Wire(e, "adDrain", drain);
            Wire(e, "reviveRoot", p.gameObject);
            p.gameObject.SetActive(false);
        }

        static TextMeshProUGUI Well(RectTransform row, string label, string value, bool coin)
        {
            var c = Node(row, label);
            Surface(c, Deep, 10f);
            var v = Title(TopBand(Node(c, "V"), 6, 26, coin ? 26 : 4, 4), value, 20f, Ink, TextAlignmentOptions.Center);
            Shrink(v);
            if (coin) IconImage(Box(Node(c, "I"), new Vector2(0, 1), new Vector2(0, 0.5f), 8, -19, 16, 16), "Money_Coin");
            Label(BottomBand(Node(c, "L"), 5, 12), label, null, 9f).alignment = TextAlignmentOptions.Center;
            return v;
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

        /// <summary>
        /// Phase A1 (owner-approved mockup SK_UnlockPopup, 2026-09-29): after a run that raised the
        /// account level, each card that level unlocks is shown over the result, one at a time — the
        /// card, the evolution it opens, the collection count — with Try it now (a new run) and Next.
        /// </summary>
        static void Unlock(RectTransform root, RunEndV2 e)
        {
            var p = Fill(Node(root, "Unlock"), 0, 0, 0, 0);
            ScreenBackground(p);
            var safe = Fill(Node(p, "Safe"), 0, 0, 0, 0); safe.gameObject.AddComponent<SafeArea>();
            var col = Fill(Node(safe, "Col"), 18, 0, 18, 0);
            Wire(e, "unlockLevel", Title(TopBand(Node(col, "Level"), 40, 36), "ACCOUNT LEVEL 7", 28f, Ink, TextAlignmentOptions.Center));
            Body(TopBand(Node(col, "Sub"), 80, 20), "New skill unlocked!", 15f, Yellow, TextAlignmentOptions.Center);

            var card = TopBand(Node(col, "Card"), 116, 300);
            Surface(card, Card, RCard);
            var tagBox = Box(Node(card, "Tag"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -16, 120, 22);
            Wire(e, "unlockTagBg", Surface(tagBox, Gem, RTag));
            Wire(e, "unlockTag", Body(Fill(Node(tagBox, "T"), 4, 0, 4, 0), "POWER", 11f, OnGem, TextAlignmentOptions.Center));
            var iconBox = Box(Node(card, "Icon"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -50, 104, 104);
            Wire(e, "unlockFrame", Surface(iconBox, Deep, RCard));
            var art = Fill(Node(iconBox, "Art"), 8, 8, 8, 8).gameObject.AddComponent<Image>();
            art.preserveAspect = true; art.raycastTarget = false;
            Wire(e, "unlockIcon", art);
            Wire(e, "unlockBadge", Title(Fill(Node(iconBox, "Badge"), 0, 0, 0, 0), "TC", 32f, Ink, TextAlignmentOptions.Center));
            Wire(e, "unlockName", Shrink(Title(TopBand(Node(card, "Name"), 164, 34, 12, 12), "Toxic Cloud", 26f, Ink, TextAlignmentOptions.Center)));
            Wire(e, "unlockDesc", Body(TopBand(Node(card, "Desc"), 202, 44, 16, 16), "A poison cloud lands on the biggest crowd", 14f, Ink, TextAlignmentOptions.Center, false));
            Wire(e, "unlockHint", Body(TopBand(Node(card, "Hint"), 252, 36, 16, 16), "Also opens an evolution", 12f, GoldText, TextAlignmentOptions.Center));

            var coll = TopBand(Node(col, "Collection"), 432, 58);
            Surface(coll, Card, RCard);
            Label(TopBand(Node(coll, "L"), 10, 14, 12, 120), "SKILL COLLECTION");
            var cnt = Label(TopBand(Node(coll, "N"), 10, 14, 150, 12), "20 / 47", Ink);
            cnt.alignment = TextAlignmentOptions.MidlineRight;
            Wire(e, "unlockCount", cnt);
            var bar = TopBand(Node(coll, "Bar"), 32, 12, 12, 12);
            Surface(bar, Deep, 6f);
            var fr = Fill(Node(bar, "Fill"), 0, 0, 0, 0);
            var fill = Surface(fr, Gem, 6f); fr.anchorMax = new Vector2(0.43f, 1f);
            Wire(e, "unlockFill", fill);

            var next = BottomBand(Node(col, "Next"), 16, 46);
            Wire(e, "unlockNext", Button(next, "NEXT", Role.Quiet, 16f));
            Wire(e, "unlockNextLabel", next.Find("Face/Label").GetComponent<TextMeshProUGUI>());
            var tryIt = BottomBand(Node(col, "Try"), 16 + 46 + 8, 62);
            Wire(e, "unlockTry", Button(tryIt, "TRY IT NOW", Role.Claim, 24f));
            Wire(e, "skillIcons", SkillIconSetBuilder.Refresh());
            Wire(e, "unlockRoot", p.gameObject);
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
