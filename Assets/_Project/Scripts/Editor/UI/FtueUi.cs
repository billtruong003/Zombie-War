using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.EditorTools.V2;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// FTUE v2 widgets in the HUD (owner-approved mockup 03/10, canvas page "FTUE v2"). Positions are
    /// the mockup's, measured on 1080 x 1920 captures of the real game, which is the HUD's reference
    /// size. Everything is added hidden; RunOverlays, RunEndV2 and FtueCoach switch it on once:
    /// <list type="bullet">
    /// <item>Overlays/FtueOverlay: "drag to move" card, joystick ring and hand, XP hint;</item>
    /// <item>Safe/XpBar/FtueGlow: the XP bar's glow on the first kills;</item>
    /// <item>LevelUpOverlay: FtueRing and FtueTag on the suggested card, FtueCoach line;</item>
    /// <item>ChestOverlay/Card/FtueEvo: how evolutions work;</item>
    /// <item>Overlays/FtueCoach: station callout and item toast;</item>
    /// <item>RunEndV2 revive: the NEWCOMER tag on the free revive.</item>
    /// </list>
    /// </summary>
    public static class FtueUi
    {
        const string HudPath = "Assets/_Project/UI/Prefabs/Screens/UI_Hud.prefab";
        const string SpriteDir = "Assets/_Project/UI/Sprites/";
        public const string IconDir = "Assets/_Project/UI/Icons/Ftue/";

        static readonly Color Card = UITheme.M8Card, Ink = UITheme.M8Ink, InkDim = UITheme.M8InkDim;
        static readonly Color Yellow = UITheme.M8Yellow, OnYellow = UITheme.M8OnYellow;

        [MenuItem("HordeCall/UI/FTUE/Build In-Run")]
        public static void BuildInRun()
        {
            ImportIcons();
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var o = root.transform.Find("Overlays");
                MoveOverlay(o.Find("FtueOverlay") as RectTransform);
                XpGlow(root.transform.Find("Safe/XpBar") as RectTransform);
                LevelUp(o.Find("LevelUpOverlay") as RectTransform);
                Chest(o.Find("ChestOverlay/Card") as RectTransform);
                Coach(o);
                Revive(o.Find("RunEndV2/Revive/Safe/Col/Ad") as RectTransform);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
                Debug.Log("[FTUE UI] in-run widgets built into UI_Hud.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        const string ArsenalPath = "Assets/_Project/UI/Prefabs/V2/UI_V2_Arsenal.prefab";
        const string ItemIcons = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/Icon_ItemIcons/128/";

        /// After the run: the Arsenal's first-gun spotlight (mockup FTUE2_09) and the icons of the
        /// LV2 / LV3 / LV5 feature popups (FTUE2_10..12).
        [MenuItem("HordeCall/UI/FTUE/Build After-Run")]
        public static void BuildAfterRun()
        {
            ImportIcons();
            var root = PrefabUtility.LoadPrefabContents(ArsenalPath);
            try
            {
                var spot = Rect(root.transform, "FtueGun", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                spot.SetAsLastSibling();
                var dim = Rect(spot, "Dim", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var dimImg = dim.GetComponent<Image>() ?? dim.gameObject.AddComponent<Image>();
                dimImg.sprite = null; dimImg.color = new Color(0.03f, 0.05f, 0.09f, 0.45f); dimImg.raycastTarget = false;
                foreach (var ring in new[] { "CellRing", "ButtonRing" })
                {
                    var r = Rect(spot, ring, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 300));
                    Img(r, "frame_32", Yellow, true);
                }
                var coach = Panel(spot, "Coach", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(880, 250), Yellow);
                Text(coach, "Title", "YOUR FIRST NEW GUN", 54, Ink, false, new Vector2(0, -26), new Vector2(-56, 66), TextAlignmentOptions.Left);
                var body = Text(coach, "Body", "You have 400 coins.", 33, Ink, true, new Vector2(0, -100), new Vector2(-56, 120), TextAlignmentOptions.TopLeft, wrap: true);
                body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = new Vector2(1, 1);
                var hand = Rect(spot, "Hand", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.2f, 1f), Vector2.zero, new Vector2(104, 152));
                Icon(hand, "ftue.hand.png");
                spot.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, ArsenalPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var hud = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var end = hud.GetComponentInChildren<ZombieWar.UI.RunEndV2>(true);
                var so = new SerializedObject(end);
                var p = so.FindProperty("featureIcons");
                var files = new[] { "ItemIcon_Calendar_Check.Png", "ItemIcon_Ticket_Gold.Png", "ItemIcon_Star_Gold.Png" };
                p.arraySize = files.Length;
                for (int i = 0; i < files.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ItemIcons + files[i]);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(hud, HudPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hud); }
            Debug.Log("[FTUE UI] after-run widgets built (Arsenal spotlight, unlock icons).");
        }

        static void ImportIcons()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconDir.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;
                if (ti.textureType == TextureImporterType.Sprite && ti.alphaIsTransparency) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------ pieces
        static void MoveOverlay(RectTransform f)
        {
            if (f == null) return;
            var dim = f.Find("Dim")?.GetComponent<Image>();
            if (dim != null) dim.color = new Color(0.03f, 0.05f, 0.09f, 0.28f);
            foreach (var old in new[] { "TooltipChip", "SkipBtn", "StepDots" }) f.Find(old)?.gameObject.SetActive(false);

            // The joystick sits at the bottom left: its centre 240 px in and 243 px up.
            var ring = f.Find("HighlightRing") as RectTransform;
            if (ring != null)
            {
                Place(ring, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(240, 243), new Vector2(300, 300));
                var img = Img(ring, "ring_thin", Yellow, false);
                img.preserveAspect = true;
            }
            var hand = Rect(f, "Hand", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.2f, 1f), new Vector2(252, 238), new Vector2(104, 152));
            Icon(hand, "ftue.hand.png");

            var coach = Panel(f, "Coach", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -123), new Vector2(925, 215), Yellow);
            Text(coach, "Title", "DRAG TO MOVE", 78, Ink, false, new Vector2(0, -28), new Vector2(-40, 92), TextAlignmentOptions.Center);
            Text(coach, "Sub", "Your gun fires at the nearest monster by itself.", 38, Ink, true, new Vector2(0, -128), new Vector2(-48, 56), TextAlignmentOptions.Center);

            var hint = Rect(f, "XpHint", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -235), new Vector2(1000, 66));
            Img(hint, "pill", Yellow, true);
            Text(hint, "Label", "The bar at the top glows on your first 3 kills: that is XP", 30, OnYellow, true, Vector2.zero, new Vector2(-40, 0), TextAlignmentOptions.Center, stretch: true);
        }

        static void XpGlow(RectTransform bar)
        {
            if (bar == null) return;
            // The bar is 18 px at the very top of the screen: the glow is a yellow band behind its fill,
            // reaching 16 px below it, so the whole bar lights up.
            var g = Rect(bar, "FtueGlow", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 34));
            var img = g.GetComponent<Image>() ?? g.gameObject.AddComponent<Image>();
            img.sprite = null; img.type = Image.Type.Simple; img.color = Yellow; img.raycastTarget = false;
            g.SetAsFirstSibling();
            g.gameObject.SetActive(false);
        }

        static void LevelUp(RectTransform lu)
        {
            if (lu == null) return;
            var ring = Rect(lu, "FtueRing", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(828, 198));
            Img(ring, "frame_32", Yellow, true);
            var perk0 = lu.Find("Perk0");
            if (perk0 != null) ring.SetSiblingIndex(perk0.GetSiblingIndex());   // behind the cards' faces, above the dim
            ring.gameObject.SetActive(false);

            var tag = Rect(lu, "FtueTag", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(220, 56));
            Img(tag, "pill", Yellow, true);
            Text(tag, "Label", "TRY THIS", 30, OnYellow, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
            tag.SetAsLastSibling();
            tag.gameObject.SetActive(false);

            var coach = Panel(lu, "FtueCoach", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1000, 100), Yellow);
            Text(coach, "Label", "One card each level. Up to 6 skills and 4 stats, then cards rank them up.", 32, Ink, true, Vector2.zero, new Vector2(-48, -16), TextAlignmentOptions.Center, stretch: true, wrap: true);
            coach.gameObject.SetActive(false);
        }

        static void Chest(RectTransform card)
        {
            if (card == null) return;
            var box = Panel(card, "FtueEvo", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -442), new Vector2(860, 156), Yellow, new Color(0.15f, 0.17f, 0.23f, 1f));
            var tag = Rect(box, "Tag", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -14), new Vector2(430, 46));
            Img(tag, "pill", Yellow, true);
            Text(tag, "Label", "HOW EVOLUTIONS WORK", 26, OnYellow, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
            var body = Text(box, "Body", "Rank a power to 5 and own its partner card. Your next chest turns it into an evolution.", 24, Ink, true,
                            new Vector2(0, -68), new Vector2(-44, 76), TextAlignmentOptions.TopLeft, wrap: true);
            body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = new Vector2(1, 1);
            box.gameObject.SetActive(false);
        }

        static void Coach(Transform overlays)
        {
            var root = Rect(overlays, "FtueCoach", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            if (root.GetComponent<FtueCoach>() == null) root.gameObject.AddComponent<FtueCoach>();
            root.SetSiblingIndex(overlays.Find("LevelUpOverlay")?.GetSiblingIndex() ?? 0);   // under every modal

            // Station callout: hangs under the station, the arrow pointing back up at it.
            var call = Rect(root, "StationCallout", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(960, 340));
            var arrow = Rect(call, "Arrow", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(46, 46));
            Img(arrow, "rounded_24", Yellow, false);
            arrow.localRotation = Quaternion.Euler(0, 0, 45);
            arrow.SetAsFirstSibling();
            PanelInto(call, Yellow, Card);
            var head = Rect(call, "Head", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(26, -20), new Vector2(-52, 62));
            var tagC = Rect(head, "Tag", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(260, 52));
            Img(tagC, "pill", Yellow, true);
            Text(tagC, "Label", "NEW STATION", 28, OnYellow, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
            var title = Text(head, "Title", "SIGNAL RELAY", 56, Ink, false, new Vector2(268, 0), new Vector2(-268, 0), TextAlignmentOptions.Left, stretch: true);
            title.rectTransform.offsetMin = new Vector2(280, 0); title.rectTransform.offsetMax = Vector2.zero;
            TopText(call, "How", "Stand inside for 12 s.", 32, Ink, -92, 104);
            TopText(call, "Reward", "Reward: pick 1 of 3 cards", 33, Yellow, -210, 46);
            TopText(call, "Note", "Once per relay", 28, InkDim, -266, 40);
            call.gameObject.SetActive(false);

            // Item toast: under the HUD's top row.
            var toast = Rect(root, "ItemToast", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -233), new Vector2(1003, 196));
            PanelInto(toast, Yellow, Card);
            var icon = Rect(toast, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(144, 144));
            foreach (var (name, file) in new[] { ("Magnet", "ftue.item.magnet.png"), ("Bomb", "ftue.item.bomb.png"), ("Freeze", "ftue.item.freeze.png") })
            {
                var i = Rect(icon, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Icon(i, file);
            }
            var headT = Rect(toast, "Head", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(190, -22), new Vector2(-212, 60));
            var tagT = Rect(headT, "Tag", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(240, 52));
            Img(tagT, "pill", Yellow, true);
            Text(tagT, "Label", "NEW ITEM", 30, OnYellow, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
            var titleT = Text(headT, "Title", "MAGNET", 62, Ink, false, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, stretch: true);
            titleT.rectTransform.offsetMin = new Vector2(258, 0); titleT.rectTransform.offsetMax = Vector2.zero;
            var desc = Text(toast, "Desc", "Pulls every coin and gem on the map to you.", 36, Ink, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft, wrap: true);
            var dr = desc.rectTransform;
            dr.anchorMin = new Vector2(0, 1); dr.anchorMax = new Vector2(1, 1); dr.pivot = new Vector2(0, 1);
            dr.anchoredPosition = new Vector2(190, -92); dr.sizeDelta = new Vector2(-212, 92);
            toast.gameObject.SetActive(false);
        }

        static void Revive(RectTransform ad)
        {
            if (ad == null) return;
            var tag = Rect(ad, "FtueTag", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(270, 52));
            Img(tag, "pill", UITheme.M8Green, true);
            Text(tag, "Label", "NEWCOMER", 28, UITheme.M8OnGreen, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
            tag.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ helpers
        static RectTransform Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Color border, Color? face = null)
        {
            var p = Rect(parent, name, aMin, aMax, pivot, pos, size);
            PanelInto(p, border, face ?? Card);
            return p;
        }

        /// A coloured border under a card face inset by 6 px: the mockup's "coach" card.
        static void PanelInto(RectTransform p, Color border, Color face)
        {
            var b = Rect(p, "Border", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Img(b, "rounded_32", border, true);
            var f = Rect(p, "Face", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12, -12));
            Img(f, "rounded_32", face, true);
            int after = p.Find("Arrow") != null ? 1 : 0;
            b.SetSiblingIndex(after); f.SetSiblingIndex(after + 1);
        }

        static void TopText(RectTransform parent, string name, string text, float size, Color color, float y, float h)
        {
            var t = Text(parent, name, text, size, color, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft, wrap: true);
            var r = t.rectTransform;
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(26, y); r.sizeDelta = new Vector2(-52, h);
        }

        static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var t = parent.Find(name) as RectTransform;
            if (t == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                t = (RectTransform)go.transform;
                t.SetParent(parent, false);
            }
            Place(t, aMin, aMax, pivot, pos, size);
            return t;
        }

        static void Place(RectTransform t, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            t.anchorMin = aMin; t.anchorMax = aMax; t.pivot = pivot;
            t.anchoredPosition = pos; t.sizeDelta = size; t.localScale = Vector3.one;
        }

        static Image Img(RectTransform t, string sprite, Color color, bool sliced)
        {
            var img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + sprite + ".png");
            img.type = sliced && img.sprite != null && img.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void Icon(RectTransform t, string file)
        {
            var img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + file);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Color.white;
        }

        /// Display text (outlined font, always light) or body text (plain font, any colour).
        static TMP_Text Text(RectTransform parent, string name, string text, float size, Color color, bool body,
                             Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align, bool stretch = false, bool wrap = false)
        {
            var t = stretch ? Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), pos, sizeDelta)
                            : Rect(parent, name, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), pos, sizeDelta);
            var tmp = t.GetComponent<TextMeshProUGUI>() ?? t.gameObject.AddComponent<TextMeshProUGUI>();
            var font = body ? UIKitV2.BodyFont : UIKitV2.DisplayFont;
            if (font != null) { tmp.font = font; tmp.fontSharedMaterial = font.material; }
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = body ? FontStyles.Bold : FontStyles.Normal;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }
    }
}
