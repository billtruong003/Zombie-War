using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Gacha.prefab from the approved V2_Gacha mockup. Stacked from the bottom so
    /// the banner art takes whatever height the phone has (16:9 to 20:9): tab bar 64, the "in this
    /// banner" block 136, pull buttons 58; banner tabs and the banner fill the rest.
    /// </summary>
    public static class V2GachaBuilder
    {
        public const string Path = V2KitSample.Dir + "UI_V2_Gacha.prefab";
        const string Economy = "Assets/_Project/Data/Economy/EconomyConfig.asset";
        const string OutfitRT = "Assets/_Project/UI/RenderTextures/MenuCharacterPreview.renderTexture";
        const float InfoH = 136, PullH = 58, NavH = 64;

        [MenuItem("HordeCall/UI v2/Build Gacha")]
        public static string Build()
        {
            var r = ScreenRoot("GachaScreen");
            try
            {
                r.gameObject.AddComponent<CanvasGroup>();
                ScreenBackground(r);
                var s = r.gameObject.AddComponent<GachaScreen>();
                var turn = r.gameObject.AddComponent<GunTurntable>();
                Wire(s, "turntable", turn);
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();
                V2PassBuilder.TabHeader(safe, s, "GACHA");

                Tabs(safe, s);
                Banner(safe, s);
                Pulls(safe, s);
                Info(safe, s);
                var buttons = NavBar(safe, 3, out var dots);
                var nav = safe.Find("Nav").gameObject.AddComponent<NavBarV2>();
                WireArray(nav, "tabs", buttons);
                WireArray(nav, "dots", dots);
                Wire(s, "nav", nav);
                UIKitV2.NoTheme = true;   // result and rate sheets are dark overlays in every theme
                try { Results(r, s); Rates(r, s); } finally { UIKitV2.NoTheme = false; }
                Wire(s, "economy", AssetDatabase.LoadAssetAtPath<EconomyConfig>(Economy));

                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        static void Tabs(RectTransform safe, GachaScreen s)
        {
            var row = TopBand(Node(safe, "Banners"), 62, 52, 14, 14);
            Row(row, 6, TextAnchor.MiddleLeft, true);
            var so = new SerializedObject(s);
            var arr = so.FindProperty("tabs"); arr.arraySize = ZombieWar.GachaBanners.All.Length;
            for (int i = 0; i < ZombieWar.GachaBanners.All.Length; i++)
            {
                var b = ZombieWar.GachaBanners.All[i];
                var t = Node(row, b.id);
                var bg = Surface(t, i == 0 ? Hex("4a1f3a") : Card, RButton, true);
                Title(TopBand(Node(t, "T"), 8, 20), b.title, 12f, Ink, TextAlignmentOptions.Center);
                var sub = Body(TopBand(Node(t, "S"), 29, 14), b.subtitle, 9f, i == 0 ? Hex("ff9ad6") : Dim, TextAlignmentOptions.Center);
                var sel = Fill(Node(t, "Selected"), 0, 0, 0, 0);
                var si = sel.gameObject.AddComponent<Image>(); si.sprite = Spr("frame_24"); si.type = Image.Type.Sliced; si.color = Hex("ff5fc8"); si.raycastTarget = false;
                si.pixelsPerUnitMultiplier = UITheme.MultiplierFor("frame_24", Px(RButton));
                sel.gameObject.SetActive(i == 0);
                var btn = t.gameObject.AddComponent<Button>(); btn.targetGraphic = bg; btn.transition = Selectable.Transition.None;
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = btn;
                e.FindPropertyRelative("sub").objectReferenceValue = sub;
                e.FindPropertyRelative("selected").objectReferenceValue = sel.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Banner(RectTransform safe, GachaScreen s)
        {
            float bottom = NavH + 12 + InfoH + 12 + PullH + 10;
            var b = Fill(Node(safe, "Banner"), 14, 124, 14, bottom);
            Wire(s, "bannerBg", Surface(b, Hex("4a1f3a"), 18f));
            b.gameObject.AddComponent<RectMask2D>();
            var glow = Box(Node(b, "Glow"), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), 0, 0, 300, 300);
            var gi = glow.gameObject.AddComponent<Image>(); gi.sprite = Spr("glow_soft"); gi.color = new Color(1f, 0.4f, 0.8f, 0.35f); gi.raycastTarget = false;

            // The art lives between the title (top 104) and the featured block (bottom 110), square,
            // so it never sits on the text whatever the banner height is.
            var artArea = Fill(Node(b, "ArtArea"), 20, 104, 20, 110);
            var gun = Node(artArea, "GunArt").gameObject.AddComponent<RawImage>();
            var gfit = gun.gameObject.AddComponent<AspectRatioFitter>();
            gfit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; gfit.aspectRatio = 1f;
            gun.color = new Color(1, 1, 1, 0); gun.raycastTarget = true;
            Wire(s, "gunArt", gun);
            var drag = gun.gameObject.AddComponent<TurntableDrag>(); Wire(drag, "turntable", s.GetComponent<GunTurntable>());
            var outfit = Box(Node(b, "OutfitArt"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 30, -10, 160, 320).gameObject.AddComponent<RawImage>();
            outfit.texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(OutfitRT); outfit.raycastTarget = false;
            outfit.gameObject.SetActive(false);
            Wire(s, "outfitArt", outfit);

            Wire(s, "bannerTimer", Label(TopBand(Node(b, "Timer"), 14, 14, 14, 14), "EVENT BANNER · ENDS IN 6D", Hex("ff9ad6")));
            var title = Title(TopBand(Node(b, "Title"), 30, 72, 14, 14), "NEON\nNIGHTS", 34f);
            title.enableWordWrapping = true; title.alignment = TextAlignmentOptions.TopLeft; title.lineSpacing = -48f;
            Wire(s, "bannerTitle", title);

            var feat = BottomBand(Node(b, "Featured"), 60, 44, 14, 14);
            var tag = TopBand(Node(feat, "Tag"), 0, 22);
            var tagW = Box(Node(tag, "Bg"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 230, 22);
            Surface(tagW, Rarity[4], RTag);
            Wire(s, "featuredTag", Body(Fill(Node(tagW, "T"), 6, 0, 6, 0), "FEATURED · NEON CIRCUIT SKIN SET", 11f, OutlineInk));
            Wire(s, "featuredSub", Body(TopBand(Node(feat, "Sub"), 26, 16), "+8% damage on any gun · only here", 12f, Hex("ffd2e8")));

            var pity = BottomBand(Node(b, "Pity"), 14, 34, 14, 14);
            Wire(s, "pityLabel", Label(TopBand(Node(pity, "L"), 0, 16), "FEATURED GUARANTEED IN 34", Hex("ffd2e8")));
            var rates = Box(Node(pity, "Rates"), new Vector2(1, 1), new Vector2(1, 1), 0, 0, 70, 18);
            Body(Fill(Node(rates, "T"), 0, 0, 0, 0), "Rates ›", 11f, Yellow, TextAlignmentOptions.MidlineRight);
            var ri = rates.gameObject.AddComponent<Image>(); ri.color = new Color(0, 0, 0, 0);
            Wire(s, "ratesButton", rates.gameObject.AddComponent<Button>());
            var bar = BottomBand(Node(pity, "Bar"), 0, 10);
            Wire(s, "pityBar", (RectTransform)Bar(bar, Hex("ff5fc8"), 0.58f).transform);
        }

        static void Pulls(RectTransform safe, GachaScreen s)
        {
            var row = BottomBand(Node(safe, "Pulls"), NavH + 12 + InfoH + 12, PullH, 14, 14);
            var one = Fill(Node(row, "One"), 0, 0, 0, 0); one.anchorMax = new Vector2(0.43f, 1); one.offsetMax = new Vector2(-Px(4), 0);
            Wire(s, "pullOne", Button(one, "PULL ×1", Role.Gem, 16f));
            Wire(s, "pullOneSub", Sub(one, "1 TICKET OR 30 GEMS", OnGem));
            var ten = Fill(Node(row, "Ten"), 0, 0, 0, 0); ten.anchorMin = new Vector2(0.43f, 0); ten.offsetMin = new Vector2(Px(4), 0);
            Wire(s, "pullTen", Button(ten, "PULL ×10", Role.Primary, 16f));
            Wire(s, "pullTenSub", Sub(ten, "270 GEMS · 1 EPIC+", OnYellow));
        }

        static TextMeshProUGUI Sub(RectTransform btn, string text, Color c)
        {
            var face = (RectTransform)btn.Find("Face");
            ((RectTransform)face.Find("Label")).offsetMin = new Vector2(Px(6), Px(18));
            return Body(BottomBand(Node(face, "Sub"), 8, 14, 4, 4), text, 11f, c, TextAlignmentOptions.Center);
        }

        static void Info(RectTransform safe, GachaScreen s)
        {
            var info = BottomBand(Node(safe, "Info"), NavH + 12, InfoH, 14, 14);
            var head = TopBand(Node(info, "Head"), 0, 22);
            Title(Box(Node(head, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 150, 22), "IN THIS BANNER", 16f);
            Wire(s, "freeLabel", Label(Box(Node(head, "Free"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, -1, 200, 16), "FREE PULL IN 12:40:00"));
            var row = TopBand(Node(info, "Rates"), 30, 62);
            Row(row, 6, TextAnchor.MiddleLeft, true);
            var so = new SerializedObject(s);
            var arr = so.FindProperty("rateTiles"); arr.arraySize = 5;
            string[] icons = { "Gear_Sword", "Gear_Armor_Top", "Chest_Gold", "Ticket_Gold", "Money_Coin" };
            for (int i = 0; i < 5; i++)
            {
                var t = Node(row, "Rate" + i);
                var bg = Surface(t, Card, RTile);
                IconImage(Box(Node(t, "I"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -6, 26, 26), icons[i]);
                var pct = Body(BottomBand(Node(t, "P"), 18, 13), "12%", 9f, Ink, TextAlignmentOptions.Center);
                var what = Shrink(Body(BottomBand(Node(t, "W"), 3, 15, 2, 2), "item", 8f, Dim, TextAlignmentOptions.Center), 0.7f);
                what.enableWordWrapping = true;
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = t.gameObject;
                e.FindPropertyRelative("bg").objectReferenceValue = bg;
                e.FindPropertyRelative("label").objectReferenceValue = pct;
                e.FindPropertyRelative("note").objectReferenceValue = what;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var card = BottomBand(Node(info, "Note"), 0, 36);
            Surface(card, Card, RCard);
            var n = Body(Fill(Node(card, "T"), 12, 0, 12, 0), "Duplicates turn into tickets. Pity carries over to the next banner.", 11f, Ink, TextAlignmentOptions.MidlineLeft);
            n.enableWordWrapping = true; Shrink(n, 0.75f);
            Wire(s, "note", n);
        }

        /// Results sheet: 11 boxes (10 + bonus) in a 4-column grid, centred, each a chest that opens
        /// into the prize. A full-screen transparent button behind the grid skips the animation.
        static void Results(RectTransform root, GachaScreen s)
        {
            var sheet = Fill(Node(root, "Results"), 0, 0, 0, 0);
            Flat(sheet, new Color(0.05f, 0.06f, 0.08f, 0.98f), true);   // linear space: 0.94 still showed the banner through
            var skip = Fill(Node(sheet, "Skip"), 0, 0, 0, 0);
            var si = skip.gameObject.AddComponent<Image>(); si.color = new Color(0, 0, 0, 0);
            Wire(s, "resultsSkip", skip.gameObject.AddComponent<Button>());
            Label(Box(Node(skip, "Hint"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 90, 200, 16), "TAP TO SKIP").alignment = TextAlignmentOptions.Center;

            Wire(s, "resultsTitle", Title(Box(Node(sheet, "T"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 235, 340, 36), "OPENING", 24f, Ink, TextAlignmentOptions.Center));
            var grid = Box(Node(sheet, "Grid"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 20, 362, 3 * 118 + 2 * 8);
            var g = UIKitV2.Grid(grid, 4, 118, 8);
            g.childAlignment = TextAnchor.UpperCenter;
            var so = new SerializedObject(s);
            var arr = so.FindProperty("resultTiles"); arr.arraySize = ZombieWar.GachaBanners.MultiBoxes;
            for (int i = 0; i < ZombieWar.GachaBanners.MultiBoxes; i++)
            {
                var t = Node(grid, "Box" + i);
                var bg = Surface(t, Card, RTile);
                var chest = Box(Node(t, "Chest"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -8, 62, 62).gameObject.AddComponent<Image>();
                chest.preserveAspect = true; chest.raycastTarget = false; chest.sprite = Icon("Chest_Wood");
                var icon = Box(Node(t, "Icon"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -10, 52, 52).gameObject.AddComponent<Image>();
                icon.preserveAspect = true; icon.raycastTarget = false; icon.gameObject.SetActive(false);
                var label = Shrink(Body(BottomBand(Node(t, "L"), 20, 28, 4, 4), "Gun shards ×10", 10f, Ink, TextAlignmentOptions.Center), 0.7f);
                var note = Shrink(Body(BottomBand(Node(t, "N"), 5, 14, 3, 3), "NEW", 8f, Green, TextAlignmentOptions.Center), 0.7f);
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = t.gameObject;
                e.FindPropertyRelative("bg").objectReferenceValue = bg;
                e.FindPropertyRelative("label").objectReferenceValue = label;
                e.FindPropertyRelative("note").objectReferenceValue = note;
                e.FindPropertyRelative("chest").objectReferenceValue = chest;
                e.FindPropertyRelative("icon").objectReferenceValue = icon;
            }
            // Chest per rarity (Common, Uncommon, Rare, Epic, Legendary) and the prize icons by name.
            var chestArr = so.FindProperty("chests"); chestArr.arraySize = 5;
            string[] chestNames = { "Chest_Wood", "Chest_Wood", "Chest_Gold", "Chest_Gem", "Chest_Premium" };
            for (int i = 0; i < 5; i++) chestArr.GetArrayElementAtIndex(i).objectReferenceValue = Icon(chestNames[i]);
            string[] prizes = { "Gear_Sword", "Gear_Armor_Top", "Chest_Gold", "Ticket_Gold", "Money_Coin" };
            var icons = so.FindProperty("rewardIcons"); icons.arraySize = prizes.Length;
            for (int i = 0; i < prizes.Length; i++)
            {
                var e = icons.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = prizes[i];
                e.FindPropertyRelative("sprite").objectReferenceValue = Icon(prizes[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var ok = Box(Node(sheet, "Ok"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -215, 200, 50);
            Wire(s, "resultsOk", Button(ok, "OK", Role.Primary, 18f));
            Wire(s, "resultsSheet", sheet.gameObject);
            sheet.gameObject.SetActive(false);
        }

        static void Rates(RectTransform root, GachaScreen s)
        {
            var sheet = Fill(Node(root, "RatesSheet"), 0, 0, 0, 0);
            Flat(sheet, new Color(0.05f, 0.06f, 0.08f, 0.98f), true);   // linear space: 0.94 still showed the banner through
            var panel = Box(Node(sheet, "Panel"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 340, 330);
            Surface(panel, Card, RPanel);
            Title(TopBand(Node(panel, "T"), 16, 28, 16, 16), "DROP RATES", 22f);
            var t = Body(Fill(Node(panel, "Text"), 16, 52, 16, 70), "0.8%  Neon Circuit skin set", 12f, Ink, TextAlignmentOptions.TopLeft, false);
            t.enableWordWrapping = true;
            Wire(s, "ratesText", t);
            var ok = BottomBand(Node(panel, "Ok"), 14, 46, 16, 16);
            Wire(s, "ratesOk", Button(ok, "OK", Role.Quiet, 17f));
            Wire(s, "ratesSheet", sheet.gameObject);
            sheet.gameObject.SetActive(false);
        }
    }
}
