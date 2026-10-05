using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// 05/10 screens from the approved "UI 05/10" mockups: Daily Ops (U6 + chest sheet F2), gun
    /// mastery (U7 + evolution moment F3) and achievements (U8). They are saved under Resources/UI/Late
    /// and put into the menu at runtime by <see cref="LateScreens"/>, so Menu.unity is never edited.
    /// </summary>
    public static class V2LateScreensBuilder
    {
        public const string Dir = "Assets/_Project/Resources/UI/Late/";
        const string Catalog = "Assets/_Project/UI/Data/UIPrototypeCatalog.asset";

        [MenuItem("HordeCall/UI v2/Build 05-10 screens (Daily Ops, Mastery, Achievements)")]
        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(Dir);
            BuildDailyOps();
            BuildMastery();
            BuildAchievements();
            AssetDatabase.SaveAssets();
        }

        static RectTransform Screen(string name, out RectTransform safe)
        {
            var r = ScreenRoot(name);
            r.gameObject.AddComponent<CanvasGroup>();
            ScreenBackground(r);
            safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
            safe.gameObject.AddComponent<SafeArea>();
            return r;
        }

        static void Wallet(RectTransform right, Object s)
        {
            var coin = Node(right, "Coin"); Size(coin, 84, 30);
            Wire(s, "coinLabel", Pill(coin, "Money_Coin", "96.7K", out var cp)); cp.gameObject.SetActive(false);
            var gem = Node(right, "Gem"); Size(gem, 66, 30);
            Wire(s, "gemLabel", Pill(gem, "Gem_Diamond_Purple", "240", out var gp)); gp.gameObject.SetActive(false);
        }

        static void Save(RectTransform r, string file)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            // Long titles ("ACHIEVEMENTS") shrink before they run under the wallet pills.
            var title = r.Find("Safe/Header/Title")?.GetComponent<TMP_Text>() as TextMeshProUGUI;
            if (title != null) { title.rectTransform.sizeDelta = new Vector2(Px(150), title.rectTransform.sizeDelta.y); Shrink(title, 0.6f); }
            try { PrefabUtility.SaveAsPrefabAsset(r.gameObject, Dir + file + ".prefab"); }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        static RectTransform Sheet(RectTransform root, string name, Color dim)
        {
            var sheet = Fill(Node(root, name), 0, 0, 0, 0);
            var bg = Flat(sheet, dim, true);
            bg.raycastTarget = true;
            sheet.gameObject.SetActive(false);
            return sheet;
        }

        // ================================================================ Daily Ops (U6, F2)
        public static void BuildDailyOps()
        {
            var r = Screen("DailyOpsScreen", out var safe);
            var s = r.gameObject.AddComponent<DailyOpsScreen>();
            Wire(s, "backButton", Header(safe, "DAILY OPS", out var right));
            Wallet(right, s);
            var page = Page(safe, 52 + 12);
            page.GetComponent<VerticalLayoutGroup>().spacing = Px(10);

            // chest card
            var chest = CardColumn(page, "Chest", 12, 8);
            var cb = chest.gameObject.AddComponent<Button>(); cb.transition = Selectable.Transition.None;
            chest.gameObject.AddComponent<UIPressFeel>();
            Wire(s, "chestCard", cb);
            var top = Node(chest, "Top"); Size(top, -1, 44);
            IconImage(Box(Node(top, "Icon"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 40, 40), "Chest_Gold");
            Wire(s, "chestTitle", Title(Box(Node(top, "Title"), new Vector2(0, 1), new Vector2(0, 1), 50, 0, 190, 22), "Daily chest · 2 / 4", 17f));
            Wire(s, "chestSub", Label(Box(Node(top, "Sub"), new Vector2(0, 0), new Vector2(0, 0), 50, 2, 230, 16), "40 GEMS · 1 TICKET · 15 GUN SHARDS", null, 10f));
            var streak = Box(Node(top, "Streak"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 6, 72, 22);
            Surface(streak, Yellow, RTag);
            Wire(s, "streakLabel", Title(Fill(Node(streak, "T"), 4, 0, 4, 0), "STREAK 3", 11f, Ink, TextAlignmentOptions.Center));
            Wire(s, "streakTag", streak.gameObject);
            var bar = Node(chest, "Bar"); Size(bar, -1, 10);
            Wire(s, "chestBar", (RectTransform)Bar(bar, Yellow, 0.5f).transform);
            var pips = Node(chest, "Days"); Size(pips, -1, 30);
            Row(pips, 6, TextAnchor.MiddleLeft, true).childForceExpandHeight = true;
            var pipImgs = new Image[7];
            for (int i = 0; i < 7; i++)
            {
                var p = Node(pips, "D" + (i + 1)); Size(p, -1, 30, 1);
                pipImgs[i] = Surface(p, Deep, RTile);
                Title(Fill(Node(p, "T"), 0, 0, 0, 0), i < 6 ? "D" + (i + 1) : "GUN", 11f, Ink, TextAlignmentOptions.Center);
            }
            WireArray(s, "dayPips", pipImgs);

            // today
            var todayRow = Node(page, "Today"); Size(todayRow, -1, 26);
            Label(Box(Node(todayRow, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 120, 16), "TODAY");
            Wire(s, "resetLabel", Label(Box(Node(todayRow, "Reset"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 200, 16), "RESETS IN 7:42:10", null, 10f));
            ((TMP_Text)new SerializedObject(s).FindProperty("resetLabel").objectReferenceValue).alignment = TextAlignmentOptions.MidlineRight;

            var so = new SerializedObject(s);
            var rows = so.FindProperty("rows"); rows.arraySize = DailyOps.Count;
            for (int i = 0; i < DailyOps.Count; i++)
            {
                var row = Node(page, "Op" + i); Size(row, -1, 66);
                Surface(row, Card, RCard);
                var iconBox = Box(Node(row, "IconBox"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 10, 0, 44, 44);
                Surface(iconBox, Deep, RTile);
                var icon = Fill(Node(iconBox, "Icon"), 4, 4, 4, 4).gameObject.AddComponent<Image>(); icon.raycastTarget = false; icon.preserveAspect = true;
                var title = Body(Box(Node(row, "Title"), new Vector2(0, 1), new Vector2(0, 1), 64, -8, 200, 32), "Defeat 250 monsters with a SHOTGUN", 13f);
                title.enableWordWrapping = true; Shrink(title, 0.75f);
                var barRt = Box(Node(row, "Bar"), new Vector2(0, 0), new Vector2(0, 0), 64, 12, 186, 8);
                var barClip = Bar(barRt, Green, 0.6f);
                var count = Label(Box(Node(row, "Count"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -10, 0, 64, 16), "160/250", null, 10f);
                count.alignment = TextAlignmentOptions.MidlineRight;
                var claimRt = Box(Node(row, "Claim"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -10, 0, 78, 36);
                var claim = Button(claimRt, "CLAIM", Role.Claim, 13f);
                claimRt.gameObject.SetActive(false);
                var done = Title(Box(Node(row, "Done"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -10, 0, 64, 18), "DONE", 12f, Green, TextAlignmentOptions.MidlineRight);
                done.gameObject.SetActive(false);
                var e = rows.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = row.gameObject;
                e.FindPropertyRelative("icon").objectReferenceValue = icon;
                e.FindPropertyRelative("title").objectReferenceValue = title;
                e.FindPropertyRelative("count").objectReferenceValue = count;
                e.FindPropertyRelative("bar").objectReferenceValue = barClip.transform;
                e.FindPropertyRelative("claim").objectReferenceValue = claim;
                e.FindPropertyRelative("done").objectReferenceValue = done.gameObject;
            }
            var icons = so.FindProperty("templateIcons"); icons.arraySize = 6;
            string[] names = { "Skull", "Heart_Red", "Book_1_Purple", "Map", "Skull", "Battle" };
            for (int i = 0; i < 6; i++) icons.GetArrayElementAtIndex(i).objectReferenceValue = Icon(names[i]);
            so.FindProperty("weaponIcons").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(Catalog);
            so.ApplyModifiedPropertiesWithoutUndo();

            // chest sheet (F2_ChestOpen)
            var sheet = Sheet(r, "ChestSheet", new Color(0.05f, 0.08f, 0.14f, 0.985f));   // linear-space UI: ~1 reads opaque
            Wire(s, "sheet", sheet.gameObject);
            Wire(s, "sheetClose", sheet.gameObject.AddComponent<Button>());
            Title(TopBand(Node(sheet, "Title"), 120, 44), "DAILY CHEST", 30f, Ink, TextAlignmentOptions.Center);
            var subTag = Box(Node(sheet, "Sub"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -170, 180, 24);
            Surface(subTag, Yellow, RTag);
            Wire(s, "sheetSub", Title(Fill(Node(subTag, "T"), 4, 0, 4, 0), "4 / 4 DONE · STREAK 4", 11f, Ink, TextAlignmentOptions.Center));
            IconImage(Box(Node(sheet, "Chest"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -210, 150, 150), "Chest_Gold");
            var rw = Box(Node(sheet, "Rewards"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -380, 362, 86);
            Surface(rw, Card, RCard);
            var rwRow = Fill(Node(rw, "Row"), 8, 8, 8, 8); Row(rwRow, 8, TextAnchor.MiddleCenter, true).childForceExpandHeight = true;
            string[] rIcons = { "Gem_Diamond_Purple", "Ticket_Gold", "Chest_Gold" };
            string[] rText = { $"{PlayerProfile.DailyChestGems} gems", $"{PlayerProfile.DailyChestTickets} ticket", "15 shards" };
            for (int i = 0; i < 3; i++)
            {
                var t = Node(rwRow, "R" + i); Size(t, -1, 70, 1); Surface(t, Deep, RTile);
                IconImage(Box(Node(t, "I"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -6, 34, 34), rIcons[i]);
                var lbl = Body(BottomBand(Node(t, "L"), 6, 18), rText[i], 12f, Ink, TextAlignmentOptions.Center);
                if (i == 2) Wire(s, "sheetShards", lbl);
            }
            var note = Label(Box(Node(sheet, "Note"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -478, 360, 18), "COME BACK TOMORROW · DAY 7 GIVES A GUN", null, 11f);
            note.alignment = TextAlignmentOptions.Center;
            Wire(s, "sheetNote", note);
            var claimBtn = BottomBand(Node(sheet, "Claim"), 40, 56, 24, 24);
            Wire(s, "sheetClaim", Button(claimBtn, "CLAIM", Role.Claim, 20f));
            Save(r, "UI_V2_DailyOps");
        }

        // ================================================================ Mastery (U7, F3)
        public static void BuildMastery()
        {
            var r = Screen("MasteryScreen", out var safe);
            var s = r.gameObject.AddComponent<MasteryScreen>();
            Wire(s, "backButton", Header(safe, "M1911", out var right));
            Wire(s, "titleLabel", safe.Find("Header/Title").GetComponent<TMP_Text>());
            Wallet(right, s);
            var page = Page(safe, 52 + 12, 12 + 56 + 10);
            page.GetComponent<VerticalLayoutGroup>().spacing = Px(10);

            var gun = Node(page, "Gun"); Size(gun, -1, 96); Surface(gun, Card, RCard);
            var ib = Box(Node(gun, "IconBox"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 120, 80); Surface(ib, Deep, RTile);
            var gi = Fill(Node(ib, "Icon"), 6, 6, 6, 6).gameObject.AddComponent<Image>(); gi.preserveAspect = true; gi.raycastTarget = false;
            Wire(s, "gunIcon", gi);
            var tag = Box(Node(gun, "Rarity"), new Vector2(0, 1), new Vector2(0, 1), 138, -14, 150, 18); Surface(tag, Deep, RTag);
            Wire(s, "rarityLabel", Body(Fill(Node(tag, "T"), 6, 0, 6, 0), "COMMON · PISTOL", 10f, Ink, TextAlignmentOptions.MidlineLeft));
            var starImgs = new Image[3];
            for (int i = 0; i < 3; i++) starImgs[i] = IconImage(Box(Node(gun, "Star" + i), new Vector2(0, 1), new Vector2(0, 1), 138 + i * 22, -38, 20, 20), "Star_Gold");
            WireArray(s, "stars", starImgs);
            Wire(s, "powerLabel", Label(Box(Node(gun, "Power"), new Vector2(0, 1), new Vector2(0, 1), 138, -64, 150, 16), "POWER 480", null, 10f));

            var m = CardColumn(page, "Mastery", 12, 8);
            var mt = Node(m, "Top"); Size(mt, -1, 24);
            Wire(s, "masteryTitle", Title(Box(Node(mt, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 170, 24), "Mastery 6 / 10", 17f));
            var xp = Label(Box(Node(mt, "Xp"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 160, 16), "1,240 / 2,000 XP", null, 10f);
            xp.alignment = TextAlignmentOptions.MidlineRight; Wire(s, "xpLabel", xp);
            var mb = Node(m, "Bar"); Size(mb, -1, 10);
            Wire(s, "xpBar", (RectTransform)Bar(mb, Yellow, 0.6f).transform);
            var lp = Node(m, "Levels"); Size(lp, -1, 30);
            Row(lp, 5, TextAnchor.MiddleLeft, true).childForceExpandHeight = true;
            var lvl = new Image[GunMastery.MaxLevel];
            for (int i = 0; i < lvl.Length; i++)
            {
                var p = Node(lp, "L" + (i + 1)); Size(p, -1, 30, 1);
                lvl[i] = Surface(p, Deep, RTile);
                Title(Fill(Node(p, "T"), 0, 0, 0, 0), (i + 1).ToString(), 12f, Ink, TextAlignmentOptions.Center);
            }
            WireArray(s, "levelPips", lvl);
            var lg = Node(m, "Legend"); Size(lg, -1, 16);
            Wire(s, "legend", Label(Fill(Node(lg, "T"), 0, 0, 0, 0), "LEVEL 5: GUN SKIN · LEVEL 10: EVOLUTION", null, 10f));

            var fam = CardColumn(page, "Family", 12, 6);
            Wire(s, "familyTitle", Label(Fill(Node(Sized(fam, 16), "T"), 0, 0, 0, 0), "PISTOL FAMILY BONUS · ALL GUNS", null, 10f));
            Wire(s, "bonus1", Body(Fill(Node(Sized(fam, 18), "T"), 0, 0, 0, 0), "Mastery 5: +5% crit chance · active", 12f));
            Wire(s, "bonus2", Body(Fill(Node(Sized(fam, 18), "T"), 0, 0, 0, 0), "Mastery 10: +10% crit chance · locked", 12f));

            var evo = CardColumn(page, "Evolution", 12, 6);
            Wire(s, "evoTitle", Label(Fill(Node(Sized(evo, 16), "T"), 0, 0, 0, 0), "EVOLUTION · ACCOUNT LEVEL 8", Gem, 10f));
            Wire(s, "evoTrait", Title(Fill(Node(Sized(evo, 22), "T"), 0, 0, 0, 0), "Shots bounce to nearby enemies", 15f));
            Wire(s, "evoState", Body(Fill(Node(Sized(evo, 16), "T"), 0, 0, 0, 0), "3 stars: done · Mastery 10: 6/10", 11f, Dim));
            var eb = Sized(evo, 44);
            var ebtn = Button(eb, "EVOLVE · LOCKED", Role.Gem, 15f);
            Wire(s, "evolveButton", ebtn);
            Wire(s, "evolveLabel", eb.Find("Face/Label").GetComponent<TMP_Text>());

            var src = CardColumn(page, "Sources", 12, 6);
            Label(Fill(Node(Sized(src, 16), "T"), 0, 0, 0, 0), "HOW THIS GUN EARNS MASTERY XP", null, 10f);
            string[,] lines = { { "Each kill with it", "+1 XP" }, { "Each minute survived", $"+{GunMastery.XpPerMinute} XP" }, { "Daily Ops mission with it", "x2 that run" }, { "Pistols", "x1.5 faster" } };
            for (int i = 0; i < lines.GetLength(0); i++)
            {
                var row = Sized(src, 18);
                Body(Box(Node(row, "A"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 220, 18), lines[i, 0], 12f);
                var v = Body(Box(Node(row, "B"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 120, 18), lines[i, 1], 12f, i >= 2 ? Green : Dim);
                v.alignment = TextAlignmentOptions.MidlineRight;
            }

            var equip = BottomBand(Node(safe, "Equip"), 12, 56, 14, 14);
            Wire(s, "equipButton", Button(equip, "EQUIP", Role.Primary, 20f));
            Wire(s, "equipLabel", equip.Find("Face/Label").GetComponent<TMP_Text>());
            Wire(s, "weaponIcons", AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(Catalog));

            // evolution moment (F3_Evolve)
            var sheet = Sheet(r, "EvolveSheet", new Color(0.23f, 0.13f, 0.45f, 1f));
            Wire(s, "evoSheet", sheet.gameObject);
            Title(TopBand(Node(sheet, "Title"), 110, 46), "EVOLUTION!", 32f, Ink, TextAlignmentOptions.Center);
            var st = Box(Node(sheet, "Sub"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -165, 240, 24); Surface(st, Gem, RTag);
            Wire(s, "evoSheetSub", Title(Fill(Node(st, "T"), 4, 0, 4, 0), "M1911 · MASTERY 10 · ★★★", 11f, Ink, TextAlignmentOptions.Center));
            var eg = Box(Node(sheet, "Gun"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -205, 280, 170).gameObject.AddComponent<Image>();
            eg.preserveAspect = true; eg.raycastTarget = false; Wire(s, "evoGun", eg);
            var ec = Box(Node(sheet, "Effect"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -390, 362, 110); Surface(ec, Card, RCard);
            Label(TopBand(Node(ec, "L"), 14, 14), "NEW EFFECT", Gem, 10f).alignment = TextAlignmentOptions.Center;
            Wire(s, "evoSheetEffect", Title(TopBand(Node(ec, "E"), 34, 24, 12, 12), "Every 6th shot ricochets to 3 monsters", 15f, Ink, TextAlignmentOptions.Center));
            Wire(s, "evoSheetBonus", Body(TopBand(Node(ec, "B"), 64, 18, 12, 12), "Pistol family bonus for ALL guns", 11f, Dim, TextAlignmentOptions.Center));
            var se = BottomBand(Node(sheet, "Equip"), 40, 56, 24, 24);
            Wire(s, "evoSheetEquip", Button(se, "EQUIP", Role.Primary, 20f));
            Save(r, "UI_V2_Mastery");
        }

        static RectTransform Sized(RectTransform parent, float h)
        {
            var rt = Node(parent, "Row" + parent.childCount); Size(rt, -1, h);
            return rt;
        }

        // ================================================================ Achievements (U8)
        public static void BuildAchievements()
        {
            var r = Screen("AchievementsScreen", out var safe);
            var s = r.gameObject.AddComponent<AchievementsScreen>();
            Wire(s, "backButton", Header(safe, "ACHIEVEMENTS", out var right));
            var head1 = safe.Find("Header/Title").GetComponent<TextMeshProUGUI>();
            head1.enableAutoSizing = false; head1.fontSize = Px(18);   // the long title fits beside the wallet
            Wallet(right, s);
            var page = Page(safe, 52 + 12);
            page.GetComponent<VerticalLayoutGroup>().spacing = Px(8);
            var head = Node(page, "Head"); Size(head, -1, 22);
            Wire(s, "countLabel", Title(Box(Node(head, "Count"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 80, 22), "4 / 10", 15f));
            var hint = Label(Box(Node(head, "Hint"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 230, 16), "EACH ONE HAS A RADIO LINE", null, 10f);
            hint.alignment = TextAlignmentOptions.MidlineRight;

            var so = new SerializedObject(s);
            int count = Achievements.All.Count;
            var rows = so.FindProperty("rows"); rows.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                var row = Node(page, "A" + i); Size(row, -1, 60);
                Surface(row, Card, RCard);
                var icon = IconImage(Box(Node(row, "Icon"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 10, 0, 40, 40), "Trophy_Silver");
                var title = Title(Box(Node(row, "Title"), new Vector2(0, 1), new Vector2(0, 1), 60, -8, 200, 20), "First Alpha", 14f);
                var sub = Label(Box(Node(row, "Sub"), new Vector2(0, 1), new Vector2(0, 1), 60, -28, 220, 14), "DEFEAT AN ELITE", null, 10f);
                var barRt = Box(Node(row, "Bar"), new Vector2(0, 0), new Vector2(0, 0), 60, 8, 200, 6);
                var bar = Bar(barRt, Green, 0.4f);
                var done = Title(Box(Node(row, "Done"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -12, 0, 60, 18), "DONE", 12f, Green, TextAlignmentOptions.MidlineRight);
                done.gameObject.SetActive(false);
                var claimRt = Box(Node(row, "Claim"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -10, 0, 70, 36);
                var claim = Button(claimRt, "+50", Role.Gem, 13f);
                claimRt.gameObject.SetActive(false);
                var e = rows.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = row.gameObject;
                e.FindPropertyRelative("icon").objectReferenceValue = icon;
                e.FindPropertyRelative("title").objectReferenceValue = title;
                e.FindPropertyRelative("sub").objectReferenceValue = sub;
                e.FindPropertyRelative("done").objectReferenceValue = done;
                e.FindPropertyRelative("bar").objectReferenceValue = bar.transform;
                e.FindPropertyRelative("claim").objectReferenceValue = claim;
                e.FindPropertyRelative("claimLabel").objectReferenceValue = claimRt.Find("Face/Label").GetComponent<TMP_Text>();
            }
            so.FindProperty("lockedIcon").objectReferenceValue = Icon("Trophy_Silver");
            so.FindProperty("unlockedIcon").objectReferenceValue = Icon("Trophy_Gold");
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(r, "UI_V2_Achievements");
        }
    }
}
