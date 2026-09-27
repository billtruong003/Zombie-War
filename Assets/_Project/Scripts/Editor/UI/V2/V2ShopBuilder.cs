using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Shop.prefab from the approved R2_Shop mockup: section chips, then one
    /// scrolling page (starter hero, boutique, daily deals, gem packs, no ads, guns, skin sets), tab bar.
    /// </summary>
    public static class V2ShopBuilder
    {
        public const string Path = V2KitSample.Dir + "UI_V2_Shop.prefab";
        const string Economy = "Assets/_Project/Data/Economy/EconomyConfig.asset";
        const string Catalog = "Assets/_Project/UI/Data/UIPrototypeCatalog.asset";

        [MenuItem("HordeCall/UI v2/Build Shop")]
        public static string Build()
        {
            var r = ScreenRoot("ShopScreen");
            try
            {
                r.gameObject.AddComponent<CanvasGroup>();
                ScreenBackground(r);
                var s = r.gameObject.AddComponent<ShopScreenV2>();
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();
                V2PassBuilder.TabHeader(safe, s, "SHOP");

                // Chips (owner-approved R2_Shop): FEATURED DEALS GEMS GUNS SKINS, pill shaped.
                var chipRow = TopBand(Node(safe, "Chips"), 60, 30, 14, 14);
                var h = Row(chipRow, 6, TextAnchor.MiddleLeft); h.childForceExpandHeight = true;
                string[] chipNames = { "FEATURED", "DEALS", "GEMS", "GUNS", "SKINS" };
                var chips = new Button[5];
                for (int i = 0; i < 5; i++)
                {
                    var c = Node(chipRow, chipNames[i]);
                    var img = Surface(c, i == 0 ? Ink : Card, 15f, true);
                    var hl = c.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hl.padding = new RectOffset((int)Px(12), (int)Px(12), 0, 0); hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false;
                    Body(Node(c, "T"), chipNames[i], 11f, i == 0 ? Ground : Hex("b9bfcc"), TextAlignmentOptions.Center);
                    chips[i] = c.gameObject.AddComponent<Button>(); chips[i].targetGraphic = img; chips[i].transition = Selectable.Transition.None;
                }
                WireArray(s, "chips", chips);

                var page = Page(safe, 96, 64);
                Wire(s, "page", page.parent.GetComponent<ScrollRect>());
                var sections = new RectTransform[5];

                // FEATURED: the starter pack hero, then the boutique look of the week.
                var gapTop = Node(page, "Top"); Size(gapTop, -1, 6);
                sections[0] = gapTop;
                UIKitV2.NoTheme = true;
                try { Hero(page, s); } finally { UIKitV2.NoTheme = false; }
                Head(page, "BOUTIQUE", out var bsub); bsub.text = "LOOK OF THE WEEK";
                Boutique(page, s);

                sections[1] = Head(page, "DAILY DEALS", out var timer);
                Wire(s, "dealsTimer", timer);
                var deals = Row3(page, "Deals", 146);
                WireCells(s, "deals", new[]
                {
                    Deal(deals, "Deal0", "Money_Coin", "AK-47 shards ×20", "900", Role.Primary, "Gear_Sword", Rarity[2]),
                    Deal(deals, "Deal1", "Gem_Diamond_Purple", "Gacha ticket ×2", "50", Role.Gem, "Ticket_Gold", Gem),
                    Deal(deals, "Deal2", null, "300 coins", "FREE", Role.Claim, "Money_Coin", Green, true),
                });

                sections[2] = Head(page, "GEMS", out var gemSub); gemSub.text = "MORE GEMS, MORE BONUS";
                var gems = Node(page, "Gems"); Size(gems, -1, 150);
                Row(gems, 6, TextAnchor.UpperLeft, true);
                var gemCells = new ShopScreenV2.Cell[4]; var bonus = new TextMeshProUGUI[4];
                string[] amounts = { "80", "440", "950", "2,600" }, prices = { "$0.99", "$4.99", "$9.99", "$19.99" }, ribbons = { null, null, "POPULAR", "BEST" };
                for (int i = 0; i < 4; i++) gemCells[i] = GemCard(gems, i, amounts[i], prices[i], ribbons[i], out bonus[i]);
                WireCells(s, "gemPacks", gemCells);
                WireArray(s, "gemBonus", bonus);
                var noAdsGap = Node(page, "NoAdsGap"); Size(noAdsGap, -1, 8);
                WireCell(s, "noAdsCell", NoAds(page));

                sections[3] = Head(page, "GUNS", out var gsub); gsub.text = "COINS";
                var grid = Node(page, "Guns");
                UIKitV2.Grid(grid, 3, 125, 8);
                var gunCells = new ShopScreenV2.Cell[ShopScreenV2.MaxGuns];
                for (int i = 0; i < gunCells.Length; i++)
                {
                    gunCells[i] = Cell(grid, "Gun" + i, "Money_Coin", "M4A1", "2,500", Role.Primary, null, Rarity[2]);
                    if (i >= 6) gunCells[i].button.transform.parent.gameObject.SetActive(false);
                }
                WireCells(s, "guns", gunCells);

                sections[4] = Head(page, "SKIN SETS", out var ssub); ssub.text = "FITS EVERY GUN · ADDS DAMAGE";
                var skins = Row3(page, "Skins", 125);
                WireCells(s, "skinSets", new[]
                {
                    Cell(skins, "Biohazard", "Gem_Diamond_Purple", "Biohazard +6%", "320", Role.Gem, "Gear_Sword", Hex("2a3a14")),
                    Cell(skins, "Neon", null, "Neon Circuit +8%", "IN GACHA", Role.Quiet, "Gear_Sword", Hex("123a44")),
                    Cell(skins, "Gilded", null, "Gilded +12%", "LEGEND PACK", Role.Primary, "Gear_Sword", Hex("8a5a14")),
                });
                WireArray(s, "sections", sections);

                var buttons = NavBar(safe, 2, out var dots);
                var nav = safe.Find("Nav").gameObject.AddComponent<NavBarV2>();
                WireArray(nav, "tabs", buttons);
                WireArray(nav, "dots", dots);
                Wire(s, "nav", nav);
                Wire(s, "economy", AssetDatabase.LoadAssetAtPath<EconomyConfig>(Economy));
                Wire(s, "catalog", AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(Catalog));

                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        /// Section head: 14 above, 22 title, 8 below. Returns the node (scroll anchor) and its sub label.
        static RectTransform Head(RectTransform page, string title, out TextMeshProUGUI sub)
        {
            var rt = Node(page, "Head_" + title); Size(rt, -1, 44);
            var t = Title(Box(Node(rt, "T"), new Vector2(0, 1), new Vector2(0, 1), 0, -14, 160, 22), title, 17f);
            t.enableAutoSizing = false;
            float w = t.GetPreferredValues(title).x / K + 8;
            sub = Label(Box(Node(rt, "Sub"), new Vector2(0, 1), new Vector2(0, 1), w, -17, 220, 16), "");
            return rt;
        }

        static RectTransform Row3(RectTransform page, string name, float h)
        {
            var row = Node(page, name); Size(row, -1, h);
            Row(row, 8, TextAnchor.UpperLeft, true);
            return row;
        }

        /// A shop card: tile with icon, title, price button (optional currency icon, optional AD tag).
        static ShopScreenV2.Cell Cell(RectTransform parent, string name, string priceIcon, string title, string price, Role role, string tileIcon, Color tileColor, bool ad = false)
        {
            var card = Node(parent, name);
            Surface(card, Card, RButton);
            var tile = TopBand(Node(card, "Tile"), 6, 58, 6, 6);
            var ti = Surface(tile, tileColor, 7f);
            var icon = Box(Node(tile, "Icon"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 72, 44).gameObject.AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false;
            if (tileIcon != null) icon.sprite = Icon(tileIcon); else icon.enabled = false;
            var tt = Body(TopBand(Node(card, "Title"), 68, 15, 6, 6), title, 11f);
            var btnRt = BottomBand(Node(card, "Buy"), 6, 32, 6, 6);
            var b = Button(btnRt, price, role, 13f);
            var label = btnRt.Find("Face/Label").GetComponent<TextMeshProUGUI>();
            if (priceIcon != null)
            {
                IconImage(Box(Node(btnRt.Find("Face") as RectTransform, "Cur"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 14, 14), priceIcon);
                ((RectTransform)label.transform).offsetMin = new Vector2(Px(20), 0);
            }
            if (ad)
            {
                var tag = Tag(card, "Ad", "AD", Red, Color.white);
                var le = tag.GetComponent<LayoutElement>(); if (le != null) Object.DestroyImmediate(le);
                Box(tag, new Vector2(1, 1), new Vector2(1, 0.5f), -6, 0, 26, 18);
            }
            var done = Box(Node(tile, "Done"), new Vector2(1, 1), new Vector2(1, 1), -4, -4, 18, 18);
            Surface(done, Green, 9f);
            Picto(Fill(Node(done, "I"), 3, 3, 3, 3), "Check_1", OnGreen);
            done.gameObject.SetActive(false);
            return new ShopScreenV2.Cell { button = b, tile = ti, icon = icon, title = tt, price = label, done = done.gameObject };
        }

        /// Starter pack hero: purple to pink card, chest art, one-time tag with countdown, contents,
        /// green price with the struck "was" price, and the -80% flash. Fixed colours in every theme.
        static void Hero(RectTransform page, ShopScreenV2 s)
        {
            var hero = Node(page, "Hero"); Size(hero, -1, 194);
            Wire(s, "hero", hero.gameObject);
            var body = Fill(Node(hero, "Card"), 0, 0, 0, 6);
            Surface(body, Hex("7663de"), 18f);
            body.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var grad = Fill(Node(body, "Grad"), 0, 0, 0, 0).gameObject.AddComponent<Image>();
            grad.sprite = Spr("grad_h"); grad.color = Hex("e5487e"); grad.raycastTarget = false;
            var glow = Box(Node(body, "Glow"), new Vector2(1, 1), new Vector2(0.5f, 0.5f), -80, -80, 240, 240).gameObject.AddComponent<Image>();
            glow.sprite = Spr("glow_soft"); glow.color = new Color(1, 1, 1, 0.35f); glow.raycastTarget = false;
            IconImage(Box(Node(body, "Chest"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -14, 4, 128, 128), "Chest_Premium");
            var tag = Box(Node(body, "Tag"), new Vector2(0, 1), new Vector2(0, 1), 16, -14, 200, 20);
            Surface(tag, Yellow, RTag);
            var tagFit = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
            tagFit.padding = new RectOffset((int)Px(6), (int)Px(6), 0, 0); tagFit.childControlWidth = true; tagFit.childControlHeight = true; tagFit.childForceExpandWidth = false;
            tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Wire(s, "heroTimer", Body(Node(tag, "T"), "ONE TIME · ENDS 23:14:05", 10f, OnYellow, TextAlignmentOptions.MidlineLeft));
            Title(Box(Node(body, "Title"), new Vector2(0, 1), new Vector2(0, 1), 16, -40, 200, 32), "STARTER", 30f);
            Title(Box(Node(body, "Title2"), new Vector2(0, 1), new Vector2(0, 1), 16, -70, 200, 32), "PACK", 30f);
            var row = Box(Node(body, "Contents"), new Vector2(0, 1), new Vector2(0, 1), 16, -106, 210, 22);
            Row(row, 8, TextAnchor.MiddleLeft);
            Content(row, "Money_Coin", "2,000"); Content(row, "Gem_Diamond_Purple", "100"); Content(row, "Ticket_Gold", "3");
            var buy = Box(Node(body, "Buy"), new Vector2(0, 0), new Vector2(0, 0), 16, 14, 128, 44);
            var b = Button(buy, "$0.99", Role.Claim, 20f);
            Wire(s, "heroButton", b);
            Wire(s, "heroPrice", buy.Find("Face/Label").GetComponent<TextMeshProUGUI>());
            Wire(s, "heroWas", Body(Box(Node(body, "Was"), new Vector2(0, 0), new Vector2(0, 0), 154, 24, 70, 20), "<s>$4.99</s>", 13f, new Color(1, 1, 1, 0.7f)));
            var off = Title(Box(Node(body, "Off"), new Vector2(1, 0), new Vector2(1, 0), -12, 10, 90, 30), "-80%", 22f, Yellow, TextAlignmentOptions.MidlineRight);
            off.rectTransform.localRotation = Quaternion.Euler(0, 0, 8f);
        }

        static void Content(RectTransform row, string icon, string value)
        {
            var c = Node(row, icon);
            var h = Row(c, 3, TextAnchor.MiddleLeft); h.childControlWidth = true; h.childControlHeight = false;
            var i = Node(c, "I"); IconImage(i, icon); Size(i, 17, 17);
            var t = Body(Node(c, "T"), value, 12f, Color.white);
            Size(t.rectTransform, -1, 18);
        }

        /// Daily deal card: rarity band on top, icon, title, price button, optional AD tag.
        static ShopScreenV2.Cell Deal(RectTransform parent, string name, string priceIcon, string title, string price, Role role, string tileIcon, Color band, bool ad = false)
        {
            var card = Node(parent, name);
            Surface(card, Card, RButton);
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var bi = TopBand(Node(card, "Band"), 0, 6).gameObject.AddComponent<Image>(); bi.color = band; bi.raycastTarget = false;
            var icon = Box(Node(card, "Icon"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), 0, -41, 64, 46).gameObject.AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false; icon.sprite = Icon(tileIcon);
            var tt = Shrink(Body(TopBand(Node(card, "Title"), 76, 28, 8, 8), title, 11f));
            tt.enableWordWrapping = true; tt.alignment = TextAlignmentOptions.TopLeft;
            var btnRt = BottomBand(Node(card, "Buy"), 8, 32, 8, 8);
            var b = Button(btnRt, price, role, 14f);
            var label = btnRt.Find("Face/Label").GetComponent<TextMeshProUGUI>();
            if (priceIcon != null)
            {
                IconImage(Box(Node(btnRt.Find("Face") as RectTransform, "Cur"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 14, 14), priceIcon);
                ((RectTransform)label.transform).offsetMin = new Vector2(Px(20), 0);
            }
            if (ad)
            {
                var tag = Tag(card, "Ad", "AD", Red, Color.white);
                var le = tag.GetComponent<LayoutElement>(); if (le != null) Object.DestroyImmediate(le);
                Box(tag, new Vector2(1, 1), new Vector2(1, 1), -6, -12, 26, 18);
            }
            var done = Box(Node(card, "Done"), new Vector2(1, 1), new Vector2(1, 1), ad ? -36 : -6, -12, 18, 18);
            Surface(done, Green, 9f);
            Picto(Fill(Node(done, "I"), 3, 3, 3, 3), "Check_1", OnGreen);
            done.gameObject.SetActive(false);
            return new ShopScreenV2.Cell { button = b, tile = bi, icon = icon, title = tt, price = label, done = done.gameObject };
        }

        /// Gem pack card: gems (more for bigger packs), amount, bonus, green price, optional ribbon.
        static ShopScreenV2.Cell GemCard(RectTransform parent, int i, string amount, string price, string ribbon, out TextMeshProUGUI bonus)
        {
            var card = Node(parent, "Gem" + i);
            Surface(card, Card, RButton);
            var art = TopBand(Node(card, "Art"), 10, 58, 4, 4);
            float g = 22 + i * 2;
            int n = i == 0 ? 1 : 2;
            for (int k = 0; k < n; k++)
                IconImage(Box(Node(art, "G" + k), new Vector2(0.5f, 0), new Vector2(0.5f, 0), n == 1 ? 0 : (k == 0 ? -g * 0.45f : g * 0.45f), 0, g, g), "Gem_Diamond_Purple");
            if (i >= 2) IconImage(Box(Node(art, "G2"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, g * 0.55f, g, g), "Gem_Diamond_Purple");
            var amt = Shrink(Title(TopBand(Node(card, "Amount"), 70, 22, 4, 4), amount, 18f, Ink, TextAlignmentOptions.Center));
            bonus = Shrink(Body(TopBand(Node(card, "Bonus"), 92, 14, 2, 2), i == 0 ? "" : $"+{i * 10}% BONUS", 10f, Green, TextAlignmentOptions.Center));
            var btnRt = BottomBand(Node(card, "Buy"), 8, 32, 6, 6);
            var b = Button(btnRt, price, Role.Claim, 14f);
            if (ribbon != null)
            {
                var r = Box(Node(card, "Ribbon"), new Vector2(0, 1), new Vector2(0, 1), -4, -8, 58, 20);
                Surface(r, Red, RTag);
                Body(Fill(Node(r, "T"), 4, 0, 4, 0), ribbon, 10f, Color.white, TextAlignmentOptions.Center);
            }
            return new ShopScreenV2.Cell { button = b, title = amt, price = btnRt.Find("Face/Label").GetComponent<TextMeshProUGUI>() };
        }

        /// No ads: one slim card under the gem packs.
        static ShopScreenV2.Cell NoAds(RectTransform page)
        {
            var card = Node(page, "NoAds"); Size(card, -1, 58);
            Surface(card, Card, RCard);
            IconImage(Box(Node(card, "I"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 12, 0, 36, 36), "TV_AdBlock");
            var t = Title(Box(Node(card, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 58, 8, 160, 22), "NO ADS", 17f);
            Label(Box(Node(card, "S"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 58, -11, 180, 14), "SKIP BANNERS FOREVER");
            var btnRt = Box(Node(card, "Buy"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -10, 0, 96, 38);
            var b = Button(btnRt, "$2.99", Role.Claim, 15f);
            var done = Box(Node(card, "Done"), new Vector2(1, 1), new Vector2(1, 1), -4, -4, 18, 18);
            Surface(done, Green, 9f);
            Picto(Fill(Node(done, "I"), 3, 3, 3, 3), "Check_1", OnGreen);
            done.gameObject.SetActive(false);
            return new ShopScreenV2.Cell { button = b, title = t, price = btnRt.Find("Face/Label").GetComponent<TextMeshProUGUI>(), done = done.gameObject };
        }

        static void WireCell(ShopScreenV2 s, string field, ShopScreenV2.Cell c)
        {
            var so = new SerializedObject(s);
            var e = so.FindProperty(field);
            e.FindPropertyRelative("button").objectReferenceValue = c.button;
            e.FindPropertyRelative("title").objectReferenceValue = c.title;
            e.FindPropertyRelative("price").objectReferenceValue = c.price;
            e.FindPropertyRelative("done").objectReferenceValue = c.done;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireCells(ShopScreenV2 s, string field, ShopScreenV2.Cell[] cells)
        {
            var so = new SerializedObject(s);
            var arr = so.FindProperty(field); arr.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i); var c = cells[i];
                e.FindPropertyRelative("button").objectReferenceValue = c.button;
                e.FindPropertyRelative("tile").objectReferenceValue = c.tile;
                e.FindPropertyRelative("icon").objectReferenceValue = c.icon;
                e.FindPropertyRelative("title").objectReferenceValue = c.title;
                e.FindPropertyRelative("price").objectReferenceValue = c.price;
                e.FindPropertyRelative("done").objectReferenceValue = c.done;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Boutique(RectTransform page, ShopScreenV2 s)
        {
            var b = Node(page, "Boutique"); Size(b, -1, 150);
            Surface(b, Hex("3a2a55"), RPanel);
            var art = Box(Node(b, "Art"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 10, 0, 100, 120);
            Surface(art, Hex("262040"), RCard);
            var icon = Fill(Node(art, "Icon"), 10, 10, 10, 10).gameObject.AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false; icon.sprite = Icon("Gear_Armor_Top");
            Wire(s, "lookIcon", icon);
            var info = Fill(Node(b, "Info"), 122, 12, 12, 10);
            Wire(s, "lookName", Title(TopBand(Node(info, "Name"), 0, 26), "NEON PUNK", 22f));
            Wire(s, "lookSub", Body(TopBand(Node(info, "Sub"), 28, 16), "4 pieces · look of the week", 11f, Hex("c9bfe0")));
            var btns = BottomBand(Node(info, "Buttons"), 0, 38);
            var tr = Box(Node(btns, "Try"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 78, 38);
            Wire(s, "tryOnButton", Button(tr, "TRY ON", Role.Quiet, 12f));
            var buy = Fill(Node(btns, "Buy"), 84, 0, 0, 0);
            Wire(s, "lookBuyButton", Button(buy, "180 GEMS", Role.Gem, 15f));
            Wire(s, "lookBuyLabel", buy.Find("Face/Label").GetComponent<TextMeshProUGUI>());
        }
    }
}
