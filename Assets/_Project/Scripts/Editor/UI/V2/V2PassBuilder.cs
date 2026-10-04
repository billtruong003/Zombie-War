using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Pass.prefab from the approved V2_Pass mockup: level card, free/premium
    /// track (30 columns, horizontal scroll), mission list (vertical scroll), premium button pinned
    /// above the tab bar.
    /// </summary>
    public static class V2PassBuilder
    {
        public const string Path = V2KitSample.Dir + "UI_V2_Pass.prefab";
        const float ColW = (362f - 4 * 6f) / 5f;   // five columns visible

        [MenuItem("HordeCall/UI v2/Build Pass")]
        public static string Build()
        {
            var r = ScreenRoot("PassScreen");
            try
            {
                r.gameObject.AddComponent<CanvasGroup>();
                ScreenBackground(r);
                var s = r.gameObject.AddComponent<PassScreenV2>();
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();

                TabHeader(safe, s, "PASS");
                LevelCard(safe, s);
                Track(safe, s);
                Missions(safe, s);

                var pb = BottomBand(Node(safe, "Premium"), 64 + 10, 50, 14, 14);
                Wire(s, "premiumButton", Button(pb, $"UNLOCK PREMIUM · {PassRewards.PremiumPrice}", Role.Primary, 17f));
                Wire(s, "premiumLabel", pb.Find("Face/Label").GetComponent<TextMeshProUGUI>());

                var buttons = NavBar(safe, 4, out var dots);
                var nav = safe.Find("Nav").gameObject.AddComponent<NavBarV2>();
                WireArray(nav, "tabs", buttons);
                WireArray(nav, "dots", dots);
                Wire(s, "nav", nav);

                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        /// Tab screens have no back button: title left, wallet right.
        public static void TabHeader(RectTransform safe, Component s, string title)
        {
            var bar = TopBand(Node(safe, "Header"), 14, 38, 14, 14);
            Title(Box(Node(bar, "Title"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 200, 38), title, 24f);
            var right = Box(Node(bar, "Right"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 190, 30);
            var row = Row(right, 6, TextAnchor.MiddleRight); row.childForceExpandHeight = true;
            var coin = Node(right, "Coin"); Size(coin, 84, 30);
            Wire(s, "coinLabel", Pill(coin, "Money_Coin", "96.7K", out var cp)); cp.gameObject.SetActive(false);
            var gem = Node(right, "Gem"); Size(gem, 66, 30);
            Wire(s, "gemLabel", Pill(gem, "Gem_Diamond_Purple", "240", out var gp)); gp.gameObject.SetActive(false);
        }

        static void LevelCard(RectTransform safe, PassScreenV2 s)
        {
            var card = TopBand(Node(safe, "LevelCard"), 64, 78, 14, 14);
            Surface(card, Card, RCard);
            var badge = Box(Node(card, "Level"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 12, 0, 54, 54);
            Surface(badge, YellowLip, RButton);
            var face = Fill(Node(badge, "Face"), 0, 0, 0, 4); Surface(face, Yellow, RButton);
            Body(TopBand(Node(face, "L"), 5, 12), "LEVEL", 9f, Hex("6b4a00"), TextAlignmentOptions.Center);
            Wire(s, "levelLabel", Title(Fill(Node(face, "N"), 0, 14, 0, 0), "7", 24f, Ink, TextAlignmentOptions.Center));

            var info = Fill(Node(card, "Info"), 78, 12, 12, 12);
            Wire(s, "seasonLabel", Title(Box(Node(info, "Season"), new Vector2(0, 1), new Vector2(0, 1), 0, 0, 110, 22), "SEASON 1", 18f));
            Wire(s, "daysLabel", Label(Box(Node(info, "Days"), new Vector2(0, 1), new Vector2(0, 1), 104, -3, 150, 18), "21 DAYS LEFT"));
            var bar = TopBand(Node(info, "Bar"), 27, 10);
            Wire(s, "xpBar", (RectTransform)Bar(bar, Yellow, 0.64f).transform);
            Wire(s, "xpLabel", Label(TopBand(Node(info, "Xp"), 40, 14), "320 / 500 XP · XP COMES FROM MISSIONS", null, 10f));
        }

        static void Track(RectTransform safe, PassScreenV2 s)
        {
            var lanes = TopBand(Node(safe, "Lanes"), 154, 16, 14, 14);
            Body(Fill(Node(lanes, "Free"), 0, 0, 0, 0), "FREE", 11f, Green).characterSpacing = 6f;
            Body(Fill(Node(lanes, "Premium"), 0, 0, 0, 0), "PREMIUM", 11f, GoldText, TextAlignmentOptions.MidlineRight).characterSpacing = 6f;

            var view = TopBand(Node(safe, "Track"), 176, 154, 14, 14);
            Flat(view, new Color(0, 0, 0, 0), true);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Node(view, "Content");
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var h = Row(content, 6, TextAnchor.UpperLeft); h.childForceExpandHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = view; sr.horizontal = true; sr.vertical = false; sr.scrollSensitivity = 30f;
            Wire(s, "track", sr);

            var so = new SerializedObject(s);
            var arr = so.FindProperty("columns"); arr.arraySize = ZombieWar.PassRewards.MaxLevel;
            for (int i = 0; i < ZombieWar.PassRewards.MaxLevel; i++)
            {
                int level = i + 1;
                var col = Node(content, "L" + level); Size(col, ColW, 154);
                var fr = ZombieWar.PassRewards.Free(level); var pr = ZombieWar.PassRewards.Premium(level);

                var free = TopBand(Node(col, "Free"), 0, 58);
                var fbg = Surface(free, Card, RTile, true);
                RewardFace(free, fr.Icon, fr.Label);
                var fring = Ring(free, Green);
                var fcheck = Check(free);
                var fb = free.gameObject.AddComponent<Button>(); fb.targetGraphic = fbg; fb.transition = Selectable.Transition.None;

                var node = Box(Node(col, "Node"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -64, 26, 26);
                var nimg = node.gameObject.AddComponent<Image>(); nimg.sprite = Spr("circle"); nimg.color = Edge; nimg.raycastTarget = false;
                Title(Fill(Node(node, "N"), 0, 0, 0, 0), level.ToString(), 13f, Ink, TextAlignmentOptions.Center);

                var prem = TopBand(Node(col, "Premium"), 96, 58);
                var pbg = Surface(prem, Gold3A, RTile, true);
                RewardFace(prem, pr.Icon, pr.Label);
                var plock = Box(Node(prem, "Lock"), new Vector2(1, 1), new Vector2(1, 1), -4, -4, 16, 16);
                Picto(plock, "Lock_1", GoldText);
                var pring = Ring(prem, Yellow);
                var pcheck = Check(prem);
                var pb = prem.gameObject.AddComponent<Button>(); pb.targetGraphic = pbg; pb.transition = Selectable.Transition.None;

                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("freeButton").objectReferenceValue = fb;
                e.FindPropertyRelative("freeBg").objectReferenceValue = fbg;
                e.FindPropertyRelative("freeCheck").objectReferenceValue = fcheck;
                e.FindPropertyRelative("freeRing").objectReferenceValue = fring;
                e.FindPropertyRelative("premButton").objectReferenceValue = pb;
                e.FindPropertyRelative("premCheck").objectReferenceValue = pcheck;
                e.FindPropertyRelative("premLock").objectReferenceValue = plock.gameObject;
                e.FindPropertyRelative("premRing").objectReferenceValue = pring;
                e.FindPropertyRelative("node").objectReferenceValue = nimg;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            Body(TopBand(Node(safe, "Note"), 336, 16, 14, 14), "Level 10: Frostbite skin free · Level 30: Inferno skin premium", 11f, Dim);
        }

        static void RewardFace(RectTransform tile, string icon, string label)
        {
            IconImage(Box(Node(tile, "Icon"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 6, 28, 28), icon);
            Body(BottomBand(Node(tile, "Amount"), 3, 14, 2, 2), label, 10f, Ink, TextAlignmentOptions.Center);
        }

        static GameObject Ring(RectTransform tile, Color c)
        {
            var ring = Fill(Node(tile, "Ring"), 0, 0, 0, 0);
            var ri = ring.gameObject.AddComponent<Image>(); ri.sprite = Spr("frame_24"); ri.type = Image.Type.Sliced; ri.color = c; ri.raycastTarget = false;
            ri.pixelsPerUnitMultiplier = UITheme.MultiplierFor("frame_24", Px(RTile));
            ring.gameObject.SetActive(false);
            return ring.gameObject;
        }

        static GameObject Check(RectTransform tile)
        {
            var check = Box(Node(tile, "Check"), new Vector2(1, 1), new Vector2(1, 1), -3, -3, 16, 16);
            Surface(check, Green, 8f);
            Picto(Fill(Node(check, "I"), 3, 3, 3, 3), "Check_1", OnGreen);
            check.gameObject.SetActive(false);
            return check.gameObject;
        }

        static void Missions(RectTransform safe, PassScreenV2 s)
        {
            var head = TopBand(Node(safe, "MissionsHead"), 364, 22, 14, 14);
            Title(Box(Node(head, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 100, 22), "MISSIONS", 17f);
            Wire(s, "resetLabel", Label(Box(Node(head, "Reset"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 96, -1, 200, 16), "DAILY RESETS 11H"));

            var page = Page(safe, 394, 64 + 10 + 50 + 10);
            page.GetComponent<VerticalLayoutGroup>().spacing = Px(6);
            var so = new SerializedObject(s);
            var arr = so.FindProperty("missions"); arr.arraySize = PassMissions.DailyCount + PassMissions.WeeklyCount;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var card = Node(page, "Mission" + i); Size(card, -1, 54);
                Surface(card, Card, RCard);
                var title = Body(TopBand(Node(card, "Title"), 10, 18, 12, 110), "Kill 150 zombies", 13f);
                var bar = TopBand(Node(card, "Bar"), 34, 7, 12, 110);
                var clip = Bar(bar, Blue, 0.6f);
                var btn = Box(Node(card, "Claim"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -10, 0, 88, 36);
                var claim = Button(btn, "+100 XP", Role.Claim, 13f);
                btn.gameObject.SetActive(false);
                var xp = Label(Box(Node(card, "Xp"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -12, 0, 90, 18), "150 XP");
                xp.alignment = TextAlignmentOptions.MidlineRight;

                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = card.gameObject;
                e.FindPropertyRelative("title").objectReferenceValue = title;
                e.FindPropertyRelative("bar").objectReferenceValue = (RectTransform)clip.transform;
                e.FindPropertyRelative("barFill").objectReferenceValue = clip.Graphic.GetComponent<Image>();
                e.FindPropertyRelative("claimButton").objectReferenceValue = claim;
                e.FindPropertyRelative("claimLabel").objectReferenceValue = btn.Find("Face/Label").GetComponent<TextMeshProUGUI>();
                e.FindPropertyRelative("xpLabel").objectReferenceValue = xp;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
