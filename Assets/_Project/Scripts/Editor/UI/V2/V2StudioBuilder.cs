using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Studio.prefab from the approved V2_Studio mockup. The bottom sheet has a
    /// fixed height; the stage with the character and hotspots takes the rest, so hotspots sit at
    /// fractions of the stage height and follow the phone shape.
    /// </summary>
    public static class V2StudioBuilder
    {
        public const string Path = V2KitSample.Dir + "UI_V2_Studio.prefab";
        const string CharacterRT = "Assets/_Project/UI/RenderTextures/MenuCharacterPreview.renderTexture";
        const string Economy = "Assets/_Project/Data/Economy/EconomyConfig.asset";
        const string Costumes = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        const float SheetH = 272;

        [MenuItem("HordeCall/UI v2/Build Studio")]
        public static string Build()
        {
            var r = ScreenRoot("StudioScreen");
            try
            {
                r.gameObject.AddComponent<CanvasGroup>();
                Flat(r, Ground, true);
                var s = r.gameObject.AddComponent<StudioScreen>();
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();

                Stage(safe, s);
                Sheet(safe, s);
                Wire(s, "backButton", Header(safe, "STUDIO", out var right));
                var coin = Node(right, "Coin"); Size(coin, 84, 30);
                Wire(s, "coinLabel", Pill(coin, "Money_Coin", "96.7K", out var cp)); cp.gameObject.SetActive(false);
                var gem = Node(right, "Gem"); Size(gem, 66, 30);
                Wire(s, "gemLabel", Pill(gem, "Gem_Diamond_Purple", "240", out var gp)); gp.gameObject.SetActive(false);

                Wire(s, "catalog", AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(Costumes));
                Wire(s, "economy", AssetDatabase.LoadAssetAtPath<EconomyConfig>(Economy));
                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        static void Stage(RectTransform safe, StudioScreen s)
        {
            var st = Fill(Node(safe, "Stage"), 0, 58, 0, SheetH - 16);
            var glow = Box(Node(st, "Glow"), new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), 0, 0, 360, 360);
            var gi = glow.gameObject.AddComponent<Image>(); gi.sprite = Spr("glow_soft"); gi.color = new Color(0.23f, 0.25f, 0.32f, 0.9f); gi.raycastTarget = false;
            // Owner: only the shadow blob under the feet (drawn by the preview), no pedestal.
            var ch = Fill(Node(st, "Character"), 60, 50, 60, 20);
            var raw = Node(ch, "RT").gameObject.AddComponent<RawImage>();
            raw.texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(CharacterRT); raw.raycastTarget = false;
            var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 512f / 900f;   // MenuCharacterPreview is 512x900
            Wire(s, "character", raw);

            string[] names = { "HAT", "FACE", "JACKET", "BACK", "PANTS", "SHOES" };
            string[] icons = { "Hat", "Glasses", "Clothes", "Bag", "Hanger", "Sheos" };
            float[] y = { 0.155f, 0.186f, 0.39f, 0.45f, 0.66f, 0.8f };
            bool[] left = { true, false, true, false, true, false };
            var hs = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                var a = new Vector2(left[i] ? 0 : 1, 1 - y[i]);
                var h = Box(Node(st, names[i]), a, new Vector2(left[i] ? 0 : 1, 0.5f), left[i] ? 14 : -14, 0, 92, 30);
                var lip = Surface(h, Deep, 15f, true);
                var face = Fill(Node(h, "Face"), 0, 0, 0, 0); face.offsetMin = new Vector2(0, Px(3));
                var img = Surface(face, Card, 15f);
                var ic = Box(Node(face, "I"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 4, 0, 22, 22);
                Surface(ic, Deep, 11f);
                Picto(Fill(Node(ic, "P"), 4, 4, 4, 4), icons[i]);
                Body(Fill(Node(face, "T"), 30, 0, 6, 0), names[i], 11f);
                hs[i] = h.gameObject.AddComponent<Button>(); hs[i].targetGraphic = img; hs[i].transition = Selectable.Transition.None;
                h.gameObject.AddComponent<UIPressFeel>();
            }
            WireArray(s, "hotspots", hs);

            // Saved looks, then "+": a row, so "+" follows the looks instead of floating at a fixed x.
            var looks = Box(Node(st, "Looks"), new Vector2(0, 1), new Vector2(0, 1), 14, -14, 220, 28);
            var lr = Row(looks, 6, TextAnchor.MiddleLeft); lr.childForceExpandHeight = false;
            var lt = Node(looks, "L"); Size(lt, 48, 14); Label(lt, "LOOKS");
            var lb = new Button[ZombieWar.PlayerProfile.MaxLooks];
            for (int i = 0; i < lb.Length; i++)
            {
                var b = Node(looks, "Look" + (i + 1)); Size(b, 28, 28);
                var bg = Surface(b, Card, RTile, true);
                Title(Fill(Node(b, "N"), 0, 0, 0, 0), (i + 1).ToString(), 13f, Ink, TextAlignmentOptions.Center);
                lb[i] = b.gameObject.AddComponent<Button>(); lb[i].targetGraphic = bg; lb[i].transition = Selectable.Transition.None;
                b.gameObject.SetActive(false);
            }
            WireArray(s, "looks", lb);
            var save = Node(looks, "Save"); Size(save, 28, 28);
            var si = save.gameObject.AddComponent<Image>(); si.sprite = Spr("rounded_dashed"); si.type = Image.Type.Sliced; si.color = Hex("5a6275");
            Body(Fill(Node(save, "P"), 0, 0, 0, 0), "+", 16f, Dim, TextAlignmentOptions.Center);
            Wire(s, "saveLook", save.gameObject.AddComponent<Button>());
        }

        static void Sheet(RectTransform safe, StudioScreen s)
        {
            var sheet = BottomBand(Node(safe, "Sheet"), 0, SheetH);
            Surface(sheet, Deep, 18f, true);

            var head = TopBand(Node(sheet, "Head"), 12, 26, 14, 14);
            Wire(s, "slotTitle", Title(Box(Node(head, "T"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 110, 26), "JACKET", 20f));
            Wire(s, "ownedLabel", Label(Box(Node(head, "Owned"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 110, -1, 130, 16), "12 / 38 OWNED"));
            var tag = Box(Node(head, "Gacha"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 92, 18);
            Surface(tag, Hex("3a2a55"), RTag);
            Wire(s, "gachaTagText", Body(Fill(Node(tag, "T"), 4, 0, 4, 0), "GACHA ONLY 3", 10f, Hex("c9a7ff"), TextAlignmentOptions.Center));
            Wire(s, "gachaTag", tag.gameObject);

            // slot chips (horizontal scroll)
            var chipsView = TopBand(Node(sheet, "Chips"), 48, 28, 14, 0);
            var chipContent = HScroll(chipsView, 6, out _);
            var chips = new Button[24];
            for (int i = 0; i < chips.Length; i++)
            {
                var c = Node(chipContent, "Chip" + i);
                var img = Surface(c, Card, RTag, true);
                var hl = c.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset((int)Px(10), (int)Px(10), 0, 0); hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false;
                Body(Node(c, "T"), "SLOT", 10f, Hex("b9bfcc"), TextAlignmentOptions.Center);
                chips[i] = c.gameObject.AddComponent<Button>(); chips[i].targetGraphic = img; chips[i].transition = Selectable.Transition.None;
                if (i >= 8) c.gameObject.SetActive(false);
            }
            WireArray(s, "chips", chips);

            // film strip
            var stripView = TopBand(Node(sheet, "Strip"), 86, 98, 14, 0);
            var stripContent = HScroll(stripView, 8, out var sr);
            Wire(s, "strip", sr);
            var so = new SerializedObject(s);
            var arr = so.FindProperty("films"); arr.arraySize = StudioScreen.MaxPieces;
            for (int i = 0; i < StudioScreen.MaxPieces; i++)
            {
                var f = Node(stripContent, "Piece" + i); Size(f, 78, 98);
                var bg = Surface(f, Card, RButton, true);
                var bar = TopBand(Node(f, "Rarity"), 0, 4, 12, 12); var bi = Surface(bar, Rarity[2], 3f);
                var ico = Box(Node(f, "Icon"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -12, 50, 50).gameObject.AddComponent<Image>();
                ico.preserveAspect = true; ico.raycastTarget = false; ico.enabled = false;
                var price = Body(BottomBand(Node(f, "Price"), 8, 16, 4, 4), "1,800", 11f, Ink, TextAlignmentOptions.Center);
                var sel = Fill(Node(f, "Selected"), 0, 0, 0, 0);
                var sm = sel.gameObject.AddComponent<Image>(); sm.sprite = Spr("frame_24"); sm.type = Image.Type.Sliced; sm.color = Yellow; sm.raycastTarget = false;
                sm.pixelsPerUnitMultiplier = UITheme.MultiplierFor("frame_24", Px(RButton));
                sel.gameObject.SetActive(i == 0);
                var b = f.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
                if (i >= 6) f.gameObject.SetActive(false);
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = b;
                e.FindPropertyRelative("bar").objectReferenceValue = bi;
                e.FindPropertyRelative("icon").objectReferenceValue = ico;
                e.FindPropertyRelative("price").objectReferenceValue = price;
                e.FindPropertyRelative("selected").objectReferenceValue = sel.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // picked piece bar
            var pb = BottomBand(Node(sheet, "Picked"), 12, 62, 12, 12);
            Surface(pb, Card, RPanel);
            var pi = Box(Node(pb, "Icon"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 42, 42);
            Surface(pi, Deep, 10f);
            var pimg = Fill(Node(pi, "I"), 3, 3, 3, 3).gameObject.AddComponent<Image>(); pimg.preserveAspect = true; pimg.raycastTarget = false; pimg.enabled = false;
            Wire(s, "pickedIcon", pimg);
            Wire(s, "pickedName", Body(Box(Node(pb, "Name"), new Vector2(0, 0.5f), new Vector2(0, 0), 60, 1, 150, 20), "Varsity Jacket", 14f));
            Wire(s, "pickedState", Label(Box(Node(pb, "State"), new Vector2(0, 0.5f), new Vector2(0, 1), 60, -1, 170, 14), "TRYING ON · RARE", null, 10f));
            var act = Box(Node(pb, "Action"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -8, 0, 140, 46);
            Wire(s, "actionButton", Button(act, "BUY 1,800", Role.Primary, 16f));
            Wire(s, "actionLabel", act.Find("Face/Label").GetComponent<TextMeshProUGUI>());
        }

        static RectTransform HScroll(RectTransform view, float gap, out ScrollRect sr)
        {
            Flat(view, new Color(0, 0, 0, 0), true);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Node(view, "Content");
            content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var h = Row(content, gap, TextAnchor.MiddleLeft, false, 0, 14); h.childForceExpandHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = view; sr.horizontal = true; sr.vertical = false; sr.scrollSensitivity = 30f;
            return content;
        }
    }
}
