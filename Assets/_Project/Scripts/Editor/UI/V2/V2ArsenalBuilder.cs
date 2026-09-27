using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.Skins;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Arsenal.prefab from the approved V2_Arsenal mockup: selected-gun card with
    /// the live 3D turntable, stars and stat bars; skin row (horizontal scroll); gun grid (vertical
    /// scroll); big 360 overlay; tab bar.
    /// </summary>
    public static class V2ArsenalBuilder
    {
        public const string Path = V2KitSample.Dir + "UI_V2_Arsenal.prefab";
        const string Economy = "Assets/_Project/Data/Economy/EconomyConfig.asset";
        const string Catalog = "Assets/_Project/UI/Data/UIPrototypeCatalog.asset";

        [MenuItem("HordeCall/UI v2/Build Arsenal")]
        public static string Build()
        {
            var r = ScreenRoot("ArsenalScreen");
            try
            {
                r.gameObject.AddComponent<CanvasGroup>();
                ScreenBackground(r);
                var s = r.gameObject.AddComponent<ArsenalScreen>();
                var turn = r.gameObject.AddComponent<GunTurntable>();
                Wire(s, "turntable", turn);
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();

                V2PassBuilder.TabHeader(safe, s, "ARSENAL");
                GunCard(safe, s, turn);
                SkinRow(safe, s);
                Grid(safe, s);
                var buttons = NavBar(safe, 1, out var dots);
                var nav = safe.Find("Nav").gameObject.AddComponent<NavBarV2>();
                WireArray(nav, "tabs", buttons);
                WireArray(nav, "dots", dots);
                Wire(s, "nav", nav);
                BigView(r, s, turn);

                Wire(s, "economy", AssetDatabase.LoadAssetAtPath<EconomyConfig>(Economy));
                Wire(s, "catalog", AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(Catalog));
                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        // card y64, padding 10: preview 136x92 + info, then buttons 42
        static void GunCard(RectTransform safe, ArsenalScreen s, GunTurntable turn)
        {
            var card = TopBand(Node(safe, "GunCard"), 64, 164, 14, 14);
            Surface(card, Card, RCard);
            var pv = Box(Node(card, "Preview"), new Vector2(0, 1), new Vector2(0, 1), 10, -10, 136, 92);
            Wire(s, "previewBg", Surface(pv, Rarity[3], RTile, true));
            pv.gameObject.AddComponent<RectMask2D>();
            var raw = Fill(Node(pv, "Gun"), 0, 0, 0, 0).gameObject.AddComponent<RawImage>();
            raw.color = new Color(1, 1, 1, 0);   // shown once the turntable has a texture
            // The texture is square; cover the wide tile without stretching it.
            var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 1f;
            Wire(s, "preview", raw);
            var drag = pv.gameObject.AddComponent<TurntableDrag>(); drag.Turntable = turn; drag.AllowZoom = false;   // pinch lives in the big 360 view

            var info = Fill(Node(card, "Info"), 156, 10, 10, 62);
            var nameRow = TopBand(Node(info, "Name"), 0, 26);
            var h = Row(nameRow, 6, TextAnchor.MiddleLeft); h.childForceExpandHeight = false;
            var nm = Node(nameRow, "N"); Size(nm, -1, 26);
            Wire(s, "gunName", Title(nm, "G36C", 20f));
            var tag = Tag(nameRow, "Tier", "EPIC", Rarity[3], OutlineInk);
            Wire(s, "tierLabel", tag.Find("Text").GetComponent<TextMeshProUGUI>());
            Wire(s, "tierBg", tag.GetComponent<Image>());

            var starRow = TopBand(Node(info, "Stars"), 29, 16);
            var starImgs = new Image[3];
            for (int i = 0; i < 3; i++)
                starImgs[i] = IconImage(Box(Node(starRow, "S" + i), new Vector2(0, 0.5f), new Vector2(0, 0.5f), i * 17, 0, 15, 15), "Star_Gold");
            WireArray(s, "stars", starImgs);

            Wire(s, "dmgBar", StatRow(info, "DMG", 52, Blue, out var bonus));
            Wire(s, "dmgBonus", bonus);
            Wire(s, "rateBar", StatRow(info, "RATE", 70, Green, out var nb));
            nb.gameObject.SetActive(false);

            var btns = BottomBand(Node(card, "Buttons"), 10, 42, 10, 10);
            var star = Fill(Node(btns, "Star"), 0, 0, 50, 0);
            Wire(s, "starButton", Button(star, "STAR 3 · 60/80", Role.Info, 14f));
            Wire(s, "starLabel", star.Find("Face/Label").GetComponent<TextMeshProUGUI>());
            var view = Box(Node(btns, "View"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 42, 42);
            Wire(s, "viewButton", Button(view, "360", Role.Quiet, 13f));
        }

        static RectTransform StatRow(RectTransform info, string label, float y, Color c, out TextMeshProUGUI bonus)
        {
            var row = TopBand(Node(info, label), y, 14);
            Label(Box(Node(row, "L"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 34, 14), label, null, 10f);
            var bar = Fill(Node(row, "Bar"), 38, 3.5f, 34, 3.5f);
            var clip = Bar(bar, c, 0.7f);
            bonus = Body(Box(Node(row, "Bonus"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 32, 14), "+5%", 10f, Gem, TextAlignmentOptions.MidlineRight);
            return (RectTransform)clip.transform;
        }

        static void SkinRow(RectTransform safe, ArsenalScreen s)
        {
            var head = TopBand(Node(safe, "SkinHead"), 240, 22, 14, 14);
            Title(Box(Node(head, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 60, 22), "SKIN", 17f);
            Label(Box(Node(head, "Sub"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 52, -1, 240, 16), "ONE SET FITS EVERY GUN · ADDS DAMAGE");

            var view = TopBand(Node(safe, "Skins"), 270, 86, 14, 0);
            Flat(view, new Color(0, 0, 0, 0), true);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Node(view, "Content");
            content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var h = Row(content, 8, TextAnchor.UpperLeft, false, 0, 14); h.childForceExpandHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = view; sr.horizontal = true; sr.vertical = false;

            var so = new SerializedObject(s);
            var arr = so.FindProperty("skins"); arr.arraySize = WeaponSkins.Season1.Length;
            for (int i = 0; i < WeaponSkins.Season1.Length; i++)
            {
                var set = WeaponSkins.Season1[i];
                var cell = Node(content, set.id); Size(cell, 64, 86);
                var bg = Surface(cell, Card, RButton, true);
                var sw = TopBand(Node(cell, "Swatch"), 5, 40, 5, 5);
                Surface(sw, (Color)(set.a * 0.5f + set.b * 0.5f), 7f);
                var glow = Box(Node(sw, "Glow"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 30, 8);
                Surface(glow, new Color(Mathf.Clamp01(set.c.r / 2f), Mathf.Clamp01(set.c.g / 2f), Mathf.Clamp01(set.c.b / 2f)), 4f);
                Body(TopBand(Node(cell, "Name"), 48, 14, 2, 2), set.name, 10f, Ink, TextAlignmentOptions.Center);
                var bonus = Body(TopBand(Node(cell, "Bonus"), 64, 14, 2, 2), "+5% DMG", 10f, Green, TextAlignmentOptions.Center);
                bonus.gameObject.SetActive(false);
                var lockTag = TopBand(Node(cell, "Lock"), 64, 14, 2, 2);
                Picto(Box(Node(lockTag, "I"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -16, 0, 11, 11), "Lock_1", Dim);
                var src = Body(Box(Node(lockTag, "Src"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 7, 0, 40, 14), "PASS", 10f, Dim, TextAlignmentOptions.Center);
                var sel = Fill(Node(cell, "Selected"), 0, 0, 0, 0);
                var si = sel.gameObject.AddComponent<Image>(); si.sprite = Spr("frame_24"); si.type = Image.Type.Sliced; si.color = Gem; si.raycastTarget = false;
                si.pixelsPerUnitMultiplier = UITheme.MultiplierFor("frame_24", Px(RButton));
                sel.gameObject.SetActive(false);
                var b = cell.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;

                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = b;
                e.FindPropertyRelative("bonus").objectReferenceValue = bonus;
                e.FindPropertyRelative("lockTag").objectReferenceValue = lockTag.gameObject;
                e.FindPropertyRelative("source").objectReferenceValue = src;
                e.FindPropertyRelative("selected").objectReferenceValue = sel.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Grid(RectTransform safe, ArsenalScreen s)
        {
            var head = TopBand(Node(safe, "GunsHead"), 366, 22, 14, 14);
            Title(Box(Node(head, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 70, 22), "GUNS", 17f);
            Wire(s, "gunsHeader", Label(Box(Node(head, "Sub"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 62, -1, 200, 16), "4 / 25 OWNED"));

            var page = Page(safe, 396, 64);
            var grid = Node(page, "Grid");
            UIKitV2.Grid(grid, 3, 100, 8);
            var so = new SerializedObject(s);
            var arr = so.FindProperty("cells"); arr.arraySize = ArsenalScreen.MaxGuns;
            for (int i = 0; i < ArsenalScreen.MaxGuns; i++)
            {
                var cell = Node(grid, "Gun" + i);
                var bg = Surface(cell, Card, RButton, true);
                var tile = TopBand(Node(cell, "Tile"), 6, 52, 6, 6);
                var ti = Surface(tile, Rarity[3], 7f);
                var icon = Fill(Node(tile, "Icon"), 4, 3, 4, 3).gameObject.AddComponent<Image>();
                icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = false;
                var nm = Body(TopBand(Node(cell, "Name"), 62, 15, 6, 6), "G36C", 11f);
                var starRow = TopBand(Node(cell, "Stars"), 80, 13, 6, 6);
                var st = new Image[3];
                for (int k = 0; k < 3; k++) st[k] = IconImage(Box(Node(starRow, "S" + k), new Vector2(0, 0.5f), new Vector2(0, 0.5f), k * 13, 0, 12, 12), "Star_Gold");
                var shop = TopBand(Node(cell, "Shop"), 80, 13, 6, 6);
                Picto(Box(Node(shop, "I"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 11, 11), "Cart_1", Dim);
                Body(Box(Node(shop, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 14, 0, 60, 13), "SHOP", 10f, Dim);
                shop.gameObject.SetActive(false);
                var sel = Fill(Node(cell, "Selected"), 0, 0, 0, 0);
                var si = sel.gameObject.AddComponent<Image>(); si.sprite = Spr("frame_24"); si.type = Image.Type.Sliced; si.color = Yellow; si.raycastTarget = false;
                si.pixelsPerUnitMultiplier = UITheme.MultiplierFor("frame_24", Px(RButton));
                sel.gameObject.SetActive(i == 0);
                var b = cell.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
                cell.gameObject.AddComponent<UIPressFeel>();

                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = b;
                e.FindPropertyRelative("tile").objectReferenceValue = ti;
                e.FindPropertyRelative("icon").objectReferenceValue = icon;
                e.FindPropertyRelative("name").objectReferenceValue = nm;
                var sp = e.FindPropertyRelative("stars"); sp.arraySize = 3;
                for (int k = 0; k < 3; k++) sp.GetArrayElementAtIndex(k).objectReferenceValue = st[k];
                e.FindPropertyRelative("shopTag").objectReferenceValue = shop.gameObject;
                e.FindPropertyRelative("selected").objectReferenceValue = sel.gameObject;
                if (i >= 12) cell.gameObject.SetActive(false);   // the screen shows as many as the catalog has
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BigView(RectTransform root, ArsenalScreen s, GunTurntable turn)
        {
            var v = Fill(Node(root, "BigView"), 0, 0, 0, 0);
            UIKitV2.NoTheme = true;   // a dark stage in every theme (a themed tint left it see-through)
            try { Flat(v, new Color(0.08f, 0.09f, 0.12f, 0.97f), true); } finally { UIKitV2.NoTheme = false; }
            var raw = Box(Node(v, "Gun"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 20, 380, 380).gameObject.AddComponent<RawImage>();
            raw.color = new Color(1, 1, 1, 0);
            Wire(s, "bigPreview", raw);
            var drag = raw.gameObject.AddComponent<TurntableDrag>(); drag.Turntable = turn;
            UIKitV2.NoTheme = true;
            try { Label(Box(Node(v, "Hint"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -190, 300, 16), "DRAG TO TURN · PINCH TO ZOOM", Hex("c9cfdb")).alignment = TextAlignmentOptions.Center; }
            finally { UIKitV2.NoTheme = false; }
            var close = Box(Node(v, "Close"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 40, 200, 50);
            Wire(s, "bigClose", Button(close, "CLOSE", Role.Quiet, 17f));
            Wire(s, "bigView", v.gameObject);
            v.gameObject.SetActive(false);
        }
    }
}
