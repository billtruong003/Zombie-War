using UnityEditor;
using UnityEngine;
using TMPro;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>M9.2 check: every kit component on one screen, rendered by UiShot next to the
    /// Design system mockup for comparison.</summary>
    public static class V2KitSample
    {
        public const string Dir = "Assets/_Project/UI/Prefabs/V2/";
        public const string Path = Dir + "UI_V2_KitSample.prefab";

        [MenuItem("HordeCall/UI v2/Build Kit Sample")]
        public static string Build()
        {
            System.IO.Directory.CreateDirectory(Dir);
            var r = ScreenRoot("UI_V2_KitSample");
            var root = r.gameObject;
            try
            {
                Flat(r, Ground);

                var head = TopBand(Node(r, "Header"), 16, 34, 16, 16);
                Title(head, "DESIGN SYSTEM", 26f);

                string[] names = { "Ground", "Card", "Deep", "Edge", "Yellow", "Gem", "Green", "Blue", "Red" };
                Color[] cols = { Ground, Card, Deep, Edge, Yellow, Gem, Green, Blue, Red };
                var sw = TopBand(Node(r, "Swatches"), 60, 110, 16, 16);
                Grid(sw, 3, 34, 10);
                for (int i = 0; i < names.Length; i++)
                {
                    var cell = Node(sw, names[i]);
                    var chip = Box(Node(cell, "Chip"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 34, 34);
                    Surface(chip, cols[i], RTile);
                    var t = Fill(Node(cell, "Name"), 42, 0, 0, 0);
                    Body(t, names[i], 12f);
                }

                var rar = TopBand(Node(r, "Rarity"), 184, 20, 16, 16);
                Row(rar, 5);
                string[] rn = { "COMMON", "UNCOMMON", "RARE", "EPIC", "LEGEND" };
                for (int i = 0; i < 5; i++) Tag(rar, rn[i], rn[i], Rarity[i], Outline);

                var lab = TopBand(Node(r, "TextLabel"), 218, 16, 16, 16); Label(lab, "Text");
                var ok = TopBand(Node(r, "TextOk"), 238, 34, 16, 16); Title(ok, "PLAY AGAIN", 24f);
                var dark = TopBand(Node(r, "TextForcedLight"), 274, 34, 16, 16); Title(dark, "DARK ASKED, LIGHT GIVEN", 20f, OnYellow);
                var body = TopBand(Node(r, "TextBody"), 312, 22, 16, 16); Body(body, "Body text 15, any contrast colour", 15f, Dim);

                var btns = TopBand(Node(r, "Buttons"), 348, 104, 16, 16);
                Grid(btns, 2, 46, 8);
                Button(Node(btns, "Play"), "PLAY", Role.Primary);
                Button(Node(btns, "Gem"), "180", Role.Gem);
                Button(Node(btns, "Claim"), "CLAIM", Role.Claim);
                Button(Node(btns, "Quiet"), "DETAILS", Role.Quiet);

                var pills = TopBand(Node(r, "Pills"), 470, 30, 16, 16);
                Row(pills, 8);
                var p1 = Node(pills, "Coin"); Size(p1, 120, 30); Pill(p1, "Money_Coin", "96.7K", out _);
                var p2 = Node(pills, "Gem"); Size(p2, 100, 30); Pill(p2, "Gem_Diamond_Purple", "240", out _);

                var nest = TopBand(Node(r, "Nested"), 516, 80, 16, 16);
                var outer = Box(Node(nest, "Card"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 110, 80);
                Surface(outer, Card, RCard);
                Surface(Fill(Node(outer, "Tile"), 8, 8, 8, 8), Deep, RTile);
                Body(Fill(Node(nest, "Rule"), 124, 0, 0, 0), "Card 14, tile 8, inset 8", 12f, Dim);

                var bar = TopBand(Node(r, "Bar"), 612, 10, 16, 16); Bar(bar, Yellow, 0.64f);
                var badgeHost = Box(Node(r, "BadgeHost"), new Vector2(0, 1), new Vector2(0, 1), 16, -636, 60, 60);
                Surface(badgeHost, Card, 12f); Badge(badgeHost, "3");

                NavBar(r, 0, out _);
                var go = PrefabUtility.SaveAsPrefabAsset(root, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
