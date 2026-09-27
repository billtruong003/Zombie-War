using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Shop.prefab from the approved V2_Shop mockup: section chips, then one
    /// scrolling page (deals, boutique, packs, skin sets, guns), tab bar.
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
                Flat(r, Ground, true);
                var s = r.gameObject.AddComponent<ShopScreenV2>();
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();
                V2PassBuilder.TabHeader(safe, s, "SHOP");

                // chips: DEALS GUNS BOUTIQUE SKINS GEMS
                var chipRow = TopBand(Node(safe, "Chips"), 62, 28, 14, 14);
                var h = Row(chipRow, 6, TextAnchor.MiddleLeft); h.childForceExpandHeight = true;
                string[] chipNames = { "DEALS", "GUNS", "BOUTIQUE", "SKINS", "GEMS" };
                var chips = new Button[5];
                for (int i = 0; i < 5; i++)
                {
                    var c = Node(chipRow, chipNames[i]);
                    var img = Surface(c, i == 0 ? Ink : Card, RTag, true);
                    var hl = c.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hl.padding = new RectOffset((int)Px(10), (int)Px(10), 0, 0); hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false;
                    Body(Node(c, "T"), chipNames[i], 11f, i == 0 ? Ground : Hex("b9bfcc"), TextAlignmentOptions.Center);
                    chips[i] = c.gameObject.AddComponent<Button>(); chips[i].targetGraphic = img; chips[i].transition = Selectable.Transition.None;
                }
                WireArray(s, "chips", chips);

                var page = Page(safe, 96, 64);
                Wire(s, "page", page.parent.GetComponent<ScrollRect>());
                var sections = new RectTransform[5];

                sections[0] = Head(page, "TODAY'S DEALS", out var timer);
                Wire(s, "dealsTimer", timer);
                var deals = Row3(page, "Deals", 125);
                WireCells(s, "deals", new[]
                {
                    Cell(deals, "Deal0", "Money_Coin", "AK-47 shards ×20", "900", Role.Primary, "Gear_Sword", Hex("1f3150")),
                    Cell(deals, "Deal1", "Gem_Diamond_Purple", "Gacha ticket ×2", "50", Role.Gem, "Ticket_Gold", Hex("3a2a55")),
                    Cell(deals, "Deal2", null, "300 coins", "FREE", Role.Claim, "Money_Coin", Hex("1f3a26"), true),
                });

                sections[2] = Head(page, "BOUTIQUE", out var bsub); bsub.text = "LOOK OF THE WEEK";
                Boutique(page, s);

                sections[4] = Head(page, "PACKS", out var psub); psub.text = "REAL MONEY";
                var packs = Row3(page, "Packs", 132);
                WireCells(s, "packs", new[]
                {
                    Cell(packs, "Starter", null, "Starter", "$0.99", Role.Claim, "Gift_Green", Hex("1f3a26")),
                    Cell(packs, "Gems", null, "440 gems", "$4.99", Role.Claim, "Chest_Gem", Hex("3a2a55")),
                    Cell(packs, "NoAds", null, "No ads", "$2.99", Role.Claim, "TV_AdBlock", Hex("4a1f22")),
                });

                sections[3] = Head(page, "SKIN SETS", out var ssub); ssub.text = "FITS EVERY GUN · ADDS DAMAGE";
                var skins = Row3(page, "Skins", 125);
                WireCells(s, "skinSets", new[]
                {
                    Cell(skins, "Biohazard", "Gem_Diamond_Purple", "Biohazard +6%", "320", Role.Gem, "Gear_Sword", Hex("2a3a14")),
                    Cell(skins, "Neon", null, "Neon Circuit +8%", "IN GACHA", Role.Quiet, "Gear_Sword", Hex("123a44")),
                    Cell(skins, "Gilded", null, "Gilded +12%", "LEGEND PACK", Role.Primary, "Gear_Sword", Hex("8a5a14")),
                });

                sections[1] = Head(page, "GUNS", out var gsub); gsub.text = "COINS";
                var grid = Node(page, "Guns");
                UIKitV2.Grid(grid, 3, 125, 8);
                var gunCells = new ShopScreenV2.Cell[ShopScreenV2.MaxGuns];
                for (int i = 0; i < gunCells.Length; i++)
                {
                    gunCells[i] = Cell(grid, "Gun" + i, "Money_Coin", "M4A1", "2,500", Role.Primary, null, Rarity[2]);
                    if (i >= 6) gunCells[i].button.transform.parent.gameObject.SetActive(false);
                }
                WireCells(s, "guns", gunCells);
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
