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
                Frames(page, s);
                Gun(page, s);
                Badges(page, s);

                Wire(s, "catalog", AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(Catalog));
                var costumes = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(Costumes);
                if (costumes == null) costumes = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>("Assets/_Project/Data/Character/ModularCostumeCatalog.asset");
                Wire(s, "costumes", costumes);

                Picker(r, s);
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

            // Profile picture (avatar + frame); tap to change (AvatarPicker).
            var av = Box(Node(card, "Avatar"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 4, 0, 90, 90);
            AvatarBox(av, true);
            var avHit = av.gameObject.AddComponent<Image>(); avHit.color = new Color(0, 0, 0, 0);
            Wire(s, "avatarButton", av.gameObject.AddComponent<Button>());
            var pen = Box(Node(av, "Edit"), new Vector2(1, 0), new Vector2(1, 0), -6, 6, 22, 22);
            Surface(pen, Yellow, 11f);
            Picto(Fill(Node(pen, "P"), 4, 4, 4, 4), "Pencil", OnYellow);

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
        // avatar + frame: the current pair, the count, tap to open the picker on the frame tab
        static void Frames(RectTransform page, ProfileScreen s)
        {
            SectionLabel(page, "PICTURE + FRAME");
            var row = Node(page, "Frames"); Size(row, -1, 64);
            Surface(row, Card, RCard, true);
            Wire(s, "framesButton", row.gameObject.AddComponent<Button>());
            var av = Box(Node(row, "Current"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 4, 0, 62, 62);
            AvatarBox(av, true);
            Wire(s, "framesLabel", Body(Box(Node(row, "Count"), new Vector2(0, 0.5f), new Vector2(0, 0), 72, 1, 220, 20), "1 / 12 FRAMES", 14f));
            var hint = Node(row, "Hint"); hint.anchorMin = new Vector2(0, 0.5f); hint.anchorMax = new Vector2(1, 0.5f); hint.pivot = new Vector2(0, 1);
            hint.offsetMin = new Vector2(Px(72), -Px(15)); hint.offsetMax = new Vector2(-Px(36), -Px(1));
            Shrink(Label(hint, "WIN FRAMES FROM LEVELS, STAMPS, PASS, GACHA"), 0.6f);
            Picto(Box(Node(row, "Go"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -12, 0, 18, 18), "Arrow_Right_1");
        }

        // picture sheet: preview, AVATAR/FRAME tabs, 4x3 grid, equip
        static void Picker(RectTransform root, ProfileScreen s)
        {
            var p = Fill(Node(root, "AvatarPicker"), 0, 0, 0, 0);
            UIKitV2.NoTheme = true;
            try { Flat(p, new Color(0.047f, 0.055f, 0.078f, 0.7f), true); } finally { UIKitV2.NoTheme = false; }
            var picker = p.gameObject.AddComponent<AvatarPicker>();
            var sheet = BottomBand(Node(p, "Sheet"), 0, 600);
            Surface(sheet, Ground, 22f, true);
            var safe = Fill(Node(sheet, "Safe"), 0, 0, 0, 0); safe.gameObject.AddComponent<SafeArea>();
            Title(TopBand(Node(safe, "Title"), 16, 28, 18, 60), "PROFILE PICTURE", 22f);
            var close = Box(Node(safe, "Close"), new Vector2(1, 1), new Vector2(1, 1), -14, -12, 36, 36);
            Surface(close, Card, 18f, true);
            Title(Fill(Node(close, "X"), 0, 0, 0, 2), "X", 18f, Ink, TextAlignmentOptions.Center);
            Wire(picker, "closeButton", close.gameObject.AddComponent<Button>());

            var pv = Box(Node(safe, "Preview"), new Vector2(0, 1), new Vector2(0, 1), 10, -50, 112, 112);
            Wire(picker, "preview", AvatarBox(pv, false));
            Wire(picker, "previewName", Title(TopBand(Node(safe, "Name"), 70, 28, 128, 16), "YOU", 22f));
            var un = Body(TopBand(Node(safe, "Unlock"), 100, 36, 128, 16), "AVATAR · UNLOCKED", 12f, Dim);
            un.enableWordWrapping = true;
            Wire(picker, "previewUnlock", un);

            var tabs = TopBand(Node(safe, "Tabs"), 172, 34, 16, 16);
            Row(tabs, 8, TextAnchor.MiddleLeft, true);
            string[] tn = { "AVATAR", "FRAME" };
            var tb = new Button[2];
            for (int i = 0; i < 2; i++)
            {
                var t = Node(tabs, tn[i]);
                var img = Surface(t, i == 0 ? Ink : Card, 17f, true);
                Title(Fill(Node(t, "T"), 0, 0, 0, 0), tn[i], 15f, Ink, TextAlignmentOptions.Center);
                tb[i] = t.gameObject.AddComponent<Button>(); tb[i].targetGraphic = img; tb[i].transition = Selectable.Transition.None;
            }
            Wire(picker, "avatarTab", tb[0]); Wire(picker, "frameTab", tb[1]);

            var grid = TopBand(Node(safe, "Grid"), 216, 300, 16, 16);
            UIKitV2.Grid(grid, 4, 96, 8);
            var so = new SerializedObject(picker);
            var arr = so.FindProperty("cells"); arr.arraySize = 12;
            for (int i = 0; i < 12; i++)
            {
                var c = Node(grid, "Cell" + i);
                var bg = Surface(c, Card, RTile, true);
                var box = Box(Node(c, "Avatar"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -2, 72, 72);
                var view = AvatarBox(box, false);
                var lb = Shrink(Body(BottomBand(Node(c, "Name"), 4, 16, 3, 3), "Classic", 10f, Ink, TextAlignmentOptions.Center));
                var lk = Box(Node(c, "Lock"), new Vector2(1, 1), new Vector2(1, 1), -4, -4, 20, 20);
                Surface(lk, Ground, 10f);
                Picto(Fill(Node(lk, "I"), 4, 4, 4, 4), "Lock_1");
                var sel = Fill(Node(c, "Selected"), 0, 0, 0, 0);
                var sm = sel.gameObject.AddComponent<Image>(); sm.sprite = Spr("frame_24"); sm.type = Image.Type.Sliced; sm.color = Yellow; sm.raycastTarget = false;
                sm.pixelsPerUnitMultiplier = UITheme.MultiplierFor("frame_24", Px(RTile));
                sel.gameObject.SetActive(false);
                var b = c.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = b;
                e.FindPropertyRelative("view").objectReferenceValue = view;
                e.FindPropertyRelative("locked").objectReferenceValue = lk.gameObject;
                e.FindPropertyRelative("label").objectReferenceValue = lb;
                e.FindPropertyRelative("selected").objectReferenceValue = sel.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var eq = BottomBand(Node(safe, "Equip"), 18, 56, 16, 16);
            Wire(picker, "equipButton", Button(eq, "EQUIP", Role.Primary, 22f));
            Wire(picker, "equipLabel", eq.Find("Face/Label").GetComponent<TextMeshProUGUI>());
            Wire(picker, "root", p.gameObject);
            Wire(s, "picker", picker);
            p.gameObject.SetActive(false);
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
