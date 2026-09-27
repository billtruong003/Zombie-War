using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Daily.prefab from the approved V2_Daily mockup: welcome check-in card
    /// (4 + 2 + wide day 7), stamp card (7x4 grid, grand prize, milestones, make-up and stamp buttons).
    /// </summary>
    public static class V2DailyBuilder
    {
        public const string Path = V2KitSample.Dir + "UI_V2_Daily.prefab";

        [MenuItem("HordeCall/UI v2/Build Daily")]
        public static string Build()
        {
            var r = ScreenRoot("DailyScreen");
            try
            {
                r.gameObject.AddComponent<CanvasGroup>();
                ScreenBackground(r);
                var s = r.gameObject.AddComponent<DailyScreen>();
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();
                Wire(s, "backButton", Header(safe, "DAILY", out var right));
                var coin = Node(right, "Coin"); Size(coin, 84, 30);
                Wire(s, "coinLabel", Pill(coin, "Money_Coin", "96.7K", out var cp)); cp.gameObject.SetActive(false);
                var gem = Node(right, "Gem"); Size(gem, 66, 30);
                Wire(s, "gemLabel", Pill(gem, "Gem_Diamond_Purple", "240", out var gp)); gp.gameObject.SetActive(false);

                // The action row is pinned to the bottom so it stays on screen on short (16:9) phones;
                // the cards above scroll.
                var page = Page(safe, 52, 12 + 50 + 10);
                page.GetComponent<VerticalLayoutGroup>().spacing = Px(10);
                page.GetComponent<VerticalLayoutGroup>().padding.top = Mathf.RoundToInt(Px(12));
                Welcome(page, s);
                StampCard(page, safe, s);

                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        static RectTransform TitleRow(RectTransform card, string title, out TextMeshProUGUI sub, float w)
        {
            var row = Node(card, "Title"); Size(row, -1, 22);
            Title(Box(Node(row, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, w, 22), title, 17f);
            sub = Label(Box(Node(row, "Sub"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), w + 8, -1, 220, 16), "SUB");
            return row;
        }

        static void Welcome(RectTransform page, DailyScreen s)
        {
            var card = CardColumn(page, "Welcome", 10, 8);
            Wire(s, "welcomeCard", card.gameObject);
            TitleRow(card, "WELCOME CHECK-IN", out var sub, 158);
            Wire(s, "welcomeSub", sub);

            var so = new SerializedObject(s);
            var arr = so.FindProperty("welcomeTiles"); arr.arraySize = DailyRewards.WelcomeDays;
            var grid = Node(card, "Days"); Size(grid, -1, 80 * 2 + 6);
            Column(grid, 6).childForceExpandHeight = true;
            var row1 = Node(grid, "Row1"); Row(row1, 6, TextAnchor.MiddleLeft, true);
            var row2 = Node(grid, "Row2"); Row(row2, 6, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < DailyRewards.WelcomeDays; i++)
            {
                var rw = DailyRewards.WelcomeReward(i + 1);
                bool wide = i == DailyRewards.WelcomeDays - 1;
                var tile = Node(i < 4 ? row1 : row2, "Day" + (i + 1));
                Size(tile, -1, 80, wide ? 2 : 1);
                var bg = Surface(tile, wide ? Hex("3a2a55") : Deep, RTile, true);
                if (wide)
                {
                    IconImage(Box(Node(tile, "Icon"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 44, 44), rw.Icon);
                    Label(Box(Node(tile, "Day"), new Vector2(0, 0.5f), new Vector2(0, 0), 58, 1, 110, 14), "DAY 7", Hex("c9a7ff"), 9f);
                    Body(Box(Node(tile, "What"), new Vector2(0, 0.5f), new Vector2(0, 1), 58, 0, 110, 18), "Rare gun", 11f);
                }
                else
                {
                    Label(TopBand(Node(tile, "Day"), 6, 13), "DAY " + (i + 1), null, 9f).alignment = TextAlignmentOptions.Center;
                    IconImage(Box(Node(tile, "Icon"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 2, 30, 30), rw.Icon);
                    Body(BottomBand(Node(tile, "Amount"), 5, 16), rw.label, 11f, Ink, TextAlignmentOptions.Center);
                }
                var ring = Fill(Node(tile, "Ring"), 0, 0, 0, 0);
                var ri = ring.gameObject.AddComponent<Image>(); ri.sprite = Spr("frame_24"); ri.type = Image.Type.Sliced; ri.color = Green; ri.raycastTarget = false;
                ri.pixelsPerUnitMultiplier = UITheme.MultiplierFor("frame_24", Px(RTile));
                ring.gameObject.SetActive(false);
                var check = Box(Node(tile, "Check"), new Vector2(1, 1), new Vector2(1, 1), -4, -4, 18, 18);
                Surface(check, Green, 9f);
                Picto(Fill(Node(check, "I"), 3, 3, 3, 3), "Check_1", OnGreen);
                check.gameObject.SetActive(false);
                var b = tile.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;

                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = b;
                e.FindPropertyRelative("bg").objectReferenceValue = bg;
                e.FindPropertyRelative("ring").objectReferenceValue = ring.gameObject;
                e.FindPropertyRelative("check").objectReferenceValue = check.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void StampCard(RectTransform page, RectTransform safe, DailyScreen s)
        {
            var card = CardColumn(page, "StampCard", 10, 8);
            TitleRow(card, "STAMP CARD", out var sub, 104);
            Wire(s, "cardSub", sub);

            var grid = Node(card, "Grid"); Size(grid, -1, 52 * 4 + 5 * 3);
            Grid(grid, 7, 52, 5);
            var so = new SerializedObject(s);
            var arr = so.FindProperty("stampTiles"); arr.arraySize = DailyRewards.CardDays;
            for (int i = 0; i < DailyRewards.CardDays; i++)
            {
                bool ms = DailyRewards.IsMilestone(i + 1);
                var tile = Node(grid, "Stamp" + (i + 1));
                var bg = Surface(tile, ms ? Gold3A : Deep, RTile);
                var num = Title(Fill(Node(tile, "Number"), 0, 0, 0, 0), (i + 1).ToString(), 14f, ms ? GoldText : Dim, TextAlignmentOptions.Center);
                var stamp = Box(Node(tile, "Stamp"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 36, 36);
                stamp.localEulerAngles = new Vector3(0, 0, -14f);
                var ring = stamp.gameObject.AddComponent<Image>(); ring.sprite = Spr("ring_thin"); ring.color = Red; ring.raycastTarget = false;
                Title(Fill(Node(stamp, "N"), 0, 0, 0, 0), (i + 1).ToString(), 14f, Red, TextAlignmentOptions.Center).color = Red;
                stamp.gameObject.SetActive(false);
                var today = Fill(Node(tile, "Today"), 0, 0, 0, 0);
                var ti = today.gameObject.AddComponent<Image>(); ti.sprite = Spr("frame_24"); ti.type = Image.Type.Sliced; ti.color = Yellow; ti.raycastTarget = false;
                ti.pixelsPerUnitMultiplier = UITheme.MultiplierFor("frame_24", Px(RTile));
                today.gameObject.SetActive(false);

                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("bg").objectReferenceValue = bg;
                e.FindPropertyRelative("number").objectReferenceValue = num;
                e.FindPropertyRelative("stamp").objectReferenceValue = stamp.gameObject;
                e.FindPropertyRelative("ring").objectReferenceValue = today.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // grand prize (well, padding 8, 58x64 art)
            var prize = Node(card, "GrandPrize"); Size(prize, -1, 80);
            Surface(prize, Deep, 10f);
            var art = Box(Node(prize, "Art"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 58, 64);
            Surface(art, Gold3A, RTile);
            IconImage(Fill(Node(art, "I"), 8, 10, 8, 10), "Medal_Gold_1");
            Label(Box(Node(prize, "Head"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 76, 20, 240, 16), "ALL 28 STAMPS", GoldText);
            Body(Box(Node(prize, "What"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 76, 1, 240, 20), "Stamp Master avatar frame", 13f);
            Wire(s, "makeUpHint", Body(Box(Node(prize, "Hint"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 76, -18, 240, 16), "2 missed days can be made up", 11f, Dim));

            // milestones 7 / 14 / 21
            var ms7 = Node(card, "Milestones"); Size(ms7, -1, 32);
            Row(ms7, 6, TextAnchor.MiddleLeft, true);
            foreach (int d in new[] { 7, 14, 21 })
            {
                var rw = DailyRewards.StampReward(d);
                var w = Node(ms7, "M" + d); Surface(w, Deep, 10f);
                Title(Box(Node(w, "Day"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 24, 20), d.ToString(), 13f, GoldText);
                IconImage(Box(Node(w, "I"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 34, 0, 20, 20), rw.Icon);
                Body(Box(Node(w, "A"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 58, 0, 50, 20), rw.label, 11f);
            }

            // buttons, pinned under the page: make up (quiet, flex 1) | stamp (green, flex 1.3)
            var btns = BottomBand(Node(safe, "Buttons"), 12, 50, 14, 14);
            Row(btns, 8, TextAnchor.MiddleLeft, false);
            var mu = Node(btns, "MakeUp"); Size(mu, -1, 50, 1f);
            var mub = Button(mu, "MAKE UP 1 DAY", Role.Quiet, 13f);
            var face = mu.Find("Face");
            ((RectTransform)face.Find("Label")).offsetMin = new Vector2(Px(6), Px(16));
            Wire(s, "makeUpSub", Body(BottomBand(Node(face, "Sub"), 6, 14), "20 GEMS · 2 LEFT", 10f, Dim, TextAlignmentOptions.Center));
            mu.gameObject.AddComponent<CanvasGroup>();
            Wire(s, "makeUpButton", mub);
            var st = Node(btns, "Stamp"); Size(st, -1, 50, 1.3f);
            Wire(s, "stampButton", Button(st, "STAMP DAY 13", Role.Claim, 18f));
            Wire(s, "stampLabel", st.Find("Face/Label").GetComponent<TextMeshProUGUI>());
            st.gameObject.AddComponent<CanvasGroup>();
        }
    }
}
