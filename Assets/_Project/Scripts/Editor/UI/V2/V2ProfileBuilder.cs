using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Profile.prefab from the approved V2_Profile mockup: header with wallet,
    /// identity card, stats 3x2, collection bars, avatar frames, main gun, badges 5x2.
    /// </summary>
    public static class V2ProfileBuilder
    {
        public const string Path = V2KitSample.Dir + "UI_V2_Profile.prefab";
        const string PreviewRT = "Assets/_Project/UI/RenderTextures/MenuCharacterPreview.renderTexture";
        const string Catalog = "Assets/_Project/UI/Data/UIPrototypeCatalog.asset";
        const string Costumes = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";

        [MenuItem("HordeCall/UI v2/Build Profile")]
        public static string Build()
        {
            var r = ScreenRoot("ProfileScreen");
            try
            {
                r.gameObject.AddComponent<CanvasGroup>();
                ScreenBackground(r);
                var s = r.gameObject.AddComponent<ProfileScreen>();
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();

                Wire(s, "backButton", Header(safe, "PROFILE", out var right));
                var coin = Node(right, "Coin"); Size(coin, 84, 30);
                Wire(s, "coinLabel", Pill(coin, "Money_Coin", "96.7K", out var cp)); cp.gameObject.SetActive(false);
                var gem = Node(right, "Gem"); Size(gem, 66, 30);
                Wire(s, "gemLabel", Pill(gem, "Gem_Diamond_Purple", "240", out var gp)); gp.gameObject.SetActive(false);

                var page = Page(safe, 52 + 12);
                Identity(page, s);
                Stats(page, s);
                Collection(page, s);
                Frames(page);
                Gun(page, s);
                Badges(page, s);

                Wire(s, "catalog", AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(Catalog));
                var costumes = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(Costumes);
                if (costumes == null) costumes = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>("Assets/_Project/Data/Character/ModularCostumeCatalog.asset");
                Wire(s, "costumes", costumes);

                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        // identity card: avatar 74 + name / id / level (card padding 12)
        static void Identity(RectTransform page, ProfileScreen s)
        {
            var card = Node(page, "Identity"); Size(card, -1, 98);
            Surface(card, Card, RCard);

            var frame = Box(Node(card, "Avatar"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 12, 0, 74, 74);
            Surface(frame, Yellow, RPanel);
            var inner = Fill(Node(frame, "Inner"), 4, 4, 4, 4);
            Surface(inner, Blue, 12f);
            inner.gameObject.AddComponent<RectMask2D>();
            var raw = Fill(Node(inner, "Character"), -8, -4, -8, -30).gameObject.AddComponent<RawImage>();
            raw.texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(PreviewRT); raw.raycastTarget = false;
            var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
            var prt = raw.texture as RenderTexture;
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = prt != null ? prt.width / (float)prt.height : 0.8f;

            var info = Fill(Node(card, "Info"), 98, 12, 12, 12);
            // name row (y 0, h 28)
            var name = TopBand(Node(info, "Name"), 0, 28);
            var nameText = Title(Fill(Node(name, "T"), 0, 0, 34, 0), "Survivor 3390", 22f);
            nameText.enableAutoSizing = true; nameText.fontSizeMin = Px(14); nameText.fontSizeMax = Px(22);
            Wire(s, "nameLabel", nameText);
            var edit = Box(Node(name, "Edit"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 26, 26);
            Surface(edit, Ground, 7f, true);
            Picto(Fill(Node(edit, "I"), 6, 6, 6, 6), "Pencil");
            Wire(s, "editNameButton", edit.gameObject.AddComponent<Button>());
            edit.gameObject.AddComponent<UIPressFeel>();
            Wire(s, "nameInput", NameInput(name));

            // id row (y 32, h 18)
            var id = TopBand(Node(info, "Id"), 32, 18);
            var idRow = Row(id, 6, TextAnchor.MiddleLeft); idRow.childForceExpandHeight = true;
            var idText = Node(id, "Value"); Size(idText, 86, 18);
            Wire(s, "idLabel", Body(idText, "ID 4821 3390", 11f, Dim));
            var copy = Tag(id, "Copy", "COPY", Deep, Ink);
            copy.GetComponent<Image>().raycastTarget = true;
            Wire(s, "copyIdButton", copy.gameObject.AddComponent<Button>());

            // level row (y 54, h 18)
            var lv = TopBand(Node(info, "Level"), 54, 18);
            var lvRow = Row(lv, 6, TextAnchor.MiddleLeft); lvRow.childForceExpandHeight = false;
            var tag = Tag(lv, "Tag", "LV 12", Yellow, OnYellow);
            Wire(s, "levelTag", tag.Find("Text").GetComponent<TextMeshProUGUI>());
            var bar = Node(lv, "Bar"); Size(bar, -1, 8, 1);
            Wire(s, "levelBar", (RectTransform)Bar(bar, Yellow, 0.62f).transform);
            var val = Node(lv, "Value"); Size(val, 64, 18);
            Wire(s, "levelValue", Body(val, "1,240/2,000", 10f, Dim, TextAlignmentOptions.MidlineRight));
        }

        static TMP_InputField NameInput(RectTransform parent)
        {
            var rt = Fill(Node(parent, "Input"), -4, -2, -4, -2);
            Surface(rt, Deep, RTile, true);
            var area = Fill(Node(rt, "Area"), 8, 0, 8, 0);
            area.gameObject.AddComponent<RectMask2D>();
            var text = Body(Fill(Node(area, "Text"), 0, 0, 0, 0), "", 18f, Ink);
            var ph = Body(Fill(Node(area, "Placeholder"), 0, 0, 0, 0), "Your name", 18f, Dim);
            var input = rt.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = text; input.placeholder = ph;
            input.characterLimit = 16; input.lineType = TMP_InputField.LineType.SingleLine;
            input.fontAsset = BodyFont; input.pointSize = Px(18);
            rt.gameObject.SetActive(false);
            return input;
        }

        // stats: 3 columns x 2 rows of wells (58 high, gap 6)
        static void Stats(RectTransform page, ProfileScreen s)
        {
            SectionLabel(page, "STATS");
            var grid = Node(page, "Stats"); Size(grid, -1, 52 * 2 + 6);
            Grid(grid, 3, 52, 6);
            string[] names = { "BEST TIME", "RUNS", "KILLS", "TOP THREAT", "BOSSES", "PLAY TIME" };
            string[] demo = { "9:42", "148", "38.2K", "9", "27", "31H" };
            var values = new TextMeshProUGUI[6];
            for (int i = 0; i < 6; i++)
            {
                var w = Node(grid, names[i]); Surface(w, Deep, 10f);
                Label(TopBand(Node(w, "Name"), 8, 13, 10, 6), names[i], null, 9f);
                values[i] = Title(TopBand(Node(w, "Value"), 22, 24, 10, 6), demo[i], 18f);
            }
            WireArray(s, "stats", values);
        }

        // collection: 4 rows (name 96 | bar | value 52), card padding 12, gap 9
        static void Collection(RectTransform page, ProfileScreen s)
        {
            SectionLabel(page, "COLLECTION");
            var card = CardColumn(page, "Collection", 11, 7);
            string[] names = { "Guns", "Outfit pieces", "Gun skins", "Frames" };
            Color[] colors = { Rarity[2], Rarity[3], Rarity[4], Green };
            var bars = new RectTransform[4]; var values = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                var row = Node(card, names[i]); Size(row, -1, 16);
                var h = Row(row, 8, TextAnchor.MiddleLeft); h.childForceExpandHeight = false;
                var n = Node(row, "Name"); Size(n, 96, 16); Body(n, names[i], 12f);
                var b = Node(row, "Bar"); Size(b, -1, 8, 1);
                bars[i] = (RectTransform)Bar(b, colors[i], 0.2f).transform;
                var v = Node(row, "Value"); Size(v, 52, 16);
                values[i] = Body(v, "0/0", 11f, Dim, TextAlignmentOptions.MidlineRight);
            }
            WireArray(s, "collectionBars", bars);
            WireArray(s, "collectionValues", values);
        }

        // avatar frames: 46 px tiles, only the default is owned until the stamp card ships
        static void Frames(RectTransform page)
        {
            SectionLabel(page, "AVATAR FRAME");
            var row = Node(page, "Frames"); Size(row, -1, 46);
            var h = Row(row, 8, TextAnchor.MiddleLeft); h.childForceExpandHeight = false;
            Color[] rims = { Yellow, Hex("b9dcf2"), Red, Edge };
            for (int i = 0; i < 4; i++)
            {
                var f = Node(row, "Frame" + i); Size(f, 46, 46);
                if (i == 0) { Surface(f, Ink, 12f); f = Fill(Node(f, "Rim"), 2, 2, 2, 2); }
                Surface(f, rims[i], 11f);
                var inner = Fill(Node(f, "Inner"), 3, 3, 3, 3); Surface(inner, Blue, 9f);
                Picto(Fill(Node(inner, "Face"), 6, 6, 6, 6), i == 0 ? "User" : "Lock_1", i == 0 ? Ink : new Color(1, 1, 1, 0.8f));
                if (i > 0) f.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
            }
            var note = Node(row, "Note"); Size(note, -1, 46, 1);
            var t = Body(note, "Stamp card day 28 gives a new frame", 11f, Dim);
            t.enableWordWrapping = true;
        }

        // main gun card: tile 66x44, name + meta
        static void Gun(RectTransform page, ProfileScreen s)
        {
            SectionLabel(page, "MAIN GUN");
            var card = Node(page, "Gun"); Size(card, -1, 60);
            Surface(card, Card, RCard);
            var tile = Box(Node(card, "Tile"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 66, 44);
            Wire(s, "gunTile", Surface(tile, Rarity[3], RTile));
            var icon = Fill(Node(tile, "Icon"), 4, 3, 4, 3).gameObject.AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = false;
            Wire(s, "gunIcon", icon);
            Wire(s, "gunName", Body(Box(Node(card, "Name"), new Vector2(0, 0.5f), new Vector2(0, 0), 84, 0, 240, 20), "G36C", 13f));
            Wire(s, "gunMeta", Body(Box(Node(card, "Meta"), new Vector2(0, 0.5f), new Vector2(0, 1), 84, 0, 260, 18), "RIFLE · POWER 1,240", 11f, Dim, TextAlignmentOptions.MidlineLeft));
        }

        // badges: 5 columns x 2 rows, tile 58, gap 6
        static void Badges(RectTransform page, ProfileScreen s)
        {
            var header = SectionLabel(page, "BADGES · 0 / 10", "Label_BADGES");
            Wire(s, "badgesHeader", header);
            var grid = Node(page, "Badges"); Size(grid, -1, 50 * 2 + 6);
            Grid(grid, 5, 50, 6);
            string[,] b =
            {
                { "10'", "SURVIVOR" }, { "1K", "SLAYER" }, { "5", "BOSSES" }, { "T9", "THREAT" }, { "100", "RUNS" },
                { "20'", "VETERAN" }, { "10K", "REAPER" }, { "25", "HUNTER" }, { "LV20", "LEGEND" }, { "10", "ARMORY" },
            };
            var tiles = new Image[ProfileScreen.BadgeCount];
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = Node(grid, b[i, 1]);
                tiles[i] = Surface(t, Edge, RTile);
                t.gameObject.AddComponent<CanvasGroup>();
                Title(TopBand(Node(t, "Value"), 6, 24), b[i, 0], 16f, Ink, TextAlignmentOptions.Center);
                Body(TopBand(Node(t, "Name"), 31, 13), b[i, 1], 8f, OutlineInk, TextAlignmentOptions.Center);
            }
            WireArray(s, "badgeTiles", tiles);
        }
    }
}
