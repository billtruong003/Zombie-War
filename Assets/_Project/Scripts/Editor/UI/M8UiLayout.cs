using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// M8 UI layout (owner-approved mockup canvas, 2026-09-26) applied to the owner's prefabs.
    ///
    /// Idempotent: every element is found-or-created by name, so running it again only re-applies
    /// sizes, colours and wiring. Retired widgets (bomb, weapon switch, victory) are deleted. The
    /// owner approved editing these prefabs for M8; each screen has its own menu so one can be
    /// re-applied without touching the others.
    /// </summary>
    public static class M8UiLayout
    {
        const string ScreensDir = "Assets/_Project/UI/Prefabs/Screens/";
        const string SpriteDir = "Assets/_Project/UI/Sprites/";
        const string LayerLabIcons = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/Icon_ItemIcons/128/";
        const string DisplayFont = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Fonts/Cairo_Line_Black SDF_Light.asset";

        static readonly Color Dark = Hex("0F121AC8");
        static readonly Color Panel = Hex("1B2130E6");
        static readonly Color XpBlue = Hex("4FA3FF");
        static readonly Color ThreatOrange = Hex("FF9B3D");
        static readonly Color HordeRed = Hex("D63B3B");
        static readonly Color Ink = Hex("1F2330");

        // ═════════════════════════════════════════════════════════════ HUD

        [MenuItem("ZombieWar/UI/M8/Apply HUD Layout")]
        public static void ApplyHud()
        {
            string path = ScreensDir + "UI_Hud.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var safe = root.transform.Find("Safe");
                var overlays = root.transform.Find("Overlays");

                // Retired: there is no bomb, no weapon switch and no victory in M6/M8.
                Delete(safe, "BombBtn");
                Delete(safe, "WeaponBtn");
                Delete(safe, "WavePill");
                Delete(overlays, "VictoryPanel");

                // XP bar across the very top.
                var xp = Rect(safe, "XpBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 18));
                Img(xp, "pill", Dark, true);
                var xpFill = Rect(xp, "Fill", Vector2.zero, new Vector2(0.3f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
                Img(xpFill, "pill", XpBlue, true);

                // HP bar keeps its place, just below the XP bar, with a heart at its start.
                var hp = safe.Find("HpBar") as RectTransform;
                if (hp != null)
                {
                    hp.anchoredPosition = new Vector2(84, -52);
                    hp.sizeDelta = new Vector2(300, hp.sizeDelta.y);   // leaves the centre to the clock
                    var heart = Rect(hp, "Heart", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-4, 0), new Vector2(64, 64));
                    IconImg(heart, "ItemIcon_Heart_Red.Png");
                    heart.SetAsLastSibling();
                    var hpLabel = hp.Find("HpLabel")?.GetComponent<TMP_Text>();
                    if (hpLabel != null)
                    {
                        Font(hpLabel, 30);
                        var lr = hpLabel.rectTransform;
                        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
                    }
                }

                var level = Rect(safe, "LevelChip", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -104), new Vector2(128, 52));
                Img(level, "pill", XpBlue, true);
                var levelLabel = Label(level, "Label", "Lv 1", 32, Color.white);

                var kill = Rect(safe, "KillChip", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(188, -104), new Vector2(176, 52));
                Img(kill, "pill", Panel, true);
                var skull = Rect(kill, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(44, 44));
                IconImg(skull, "ItemIcon_Skull.png");
                var killLabel = Label(kill, "Label", "0", 32, Color.white);
                var klr = killLabel.rectTransform;
                klr.anchorMin = new Vector2(0, 0); klr.anchorMax = new Vector2(1, 1); klr.offsetMin = new Vector2(56, 0); klr.offsetMax = new Vector2(-12, 0);
                killLabel.alignment = TextAlignmentOptions.Left;

                // Survival clock, big, with the threat chip under it.
                var clock = Rect(safe, "Clock", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(360, 110));
                var clockLabel = Label(clock, "Label", "0:00", 96, Color.white);
                var threat = Rect(safe, "ThreatChip", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(236, 54));
                var threatImg = Img(threat, "pill", ThreatOrange, true);
                var threatLabel = Label(threat, "Label", "THREAT 0", 30, Ink);

                // Horde banner: warns before a surge, counts it down while it runs.
                var banner = Rect(safe, "HordeBanner", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -218), new Vector2(760, 96));
                Img(banner, "rounded_24", HordeRed, true);
                var bannerLabel = Label(banner, "Label", "HORDE INCOMING  3", 44, Color.white);
                banner.gameObject.SetActive(false);

                // Skill bar, bottom-right (the joystick owns bottom-left).
                var bar = Rect(safe, "SkillBar", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 190), new Vector2(412, 330));
                var slotsRt = Rect(bar, "Slots", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 270));
                var grid = slotsRt.GetComponent<GridLayoutGroup>() ?? slotsRt.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(128, 128);
                grid.spacing = new Vector2(14, 14);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                grid.childAlignment = TextAnchor.UpperRight;
                grid.startCorner = GridLayoutGroup.Corner.UpperRight;

                var slots = new SkillSlotView[6];
                for (int i = 0; i < 6; i++) slots[i] = Slot(slotsRt, "Slot" + i);

                var pipsRt = Rect(bar, "Pips", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 44));
                var row = pipsRt.GetComponent<HorizontalLayoutGroup>() ?? pipsRt.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 8; row.childAlignment = TextAnchor.MiddleRight;
                row.childControlWidth = false; row.childControlHeight = false; row.childForceExpandWidth = false;
                var pips = new TMP_Text[6];
                for (int i = 0; i < 6; i++)
                {
                    var pip = Rect(pipsRt, "Pip" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62, 44));
                    Img(pip, "rounded_24", Dark, true);
                    pips[i] = Label(pip, "Label", "DU1", 24, Color.white);
                    pip.gameObject.SetActive(false);
                }

                var view = bar.GetComponent<SkillBarView>() ?? bar.gameObject.AddComponent<SkillBarView>();
                var soView = new SerializedObject(view);
                SetArray(soView, "slots", slots);
                SetArray(soView, "passivePips", pips);
                soView.FindProperty("icons").objectReferenceValue = SkillIconSetBuilder.Refresh();
                soView.ApplyModifiedPropertiesWithoutUndo();

                // Coin label in the display font too.
                var coinValue = safe.Find("CoinPill/Value")?.GetComponent<TMP_Text>();
                if (coinValue != null) Font(coinValue, 34);

                var hud = root.GetComponent<HudController>();
                var so = new SerializedObject(hud);
                so.FindProperty("runPill").objectReferenceValue = null;
                so.FindProperty("clockLabel").objectReferenceValue = clockLabel;
                so.FindProperty("threatChip").objectReferenceValue = threatImg;
                so.FindProperty("threatLabel").objectReferenceValue = threatLabel;
                so.FindProperty("levelLabel").objectReferenceValue = levelLabel;
                so.FindProperty("killLabel").objectReferenceValue = killLabel;
                so.FindProperty("xpFillRect").objectReferenceValue = xpFill;
                so.FindProperty("hordeBanner").objectReferenceValue = banner.gameObject;
                so.FindProperty("hordeLabel").objectReferenceValue = bannerLabel;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[M8 UI] HUD layout applied.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ═════════════════════════════════════════════════════════════ Level Up

        [MenuItem("ZombieWar/UI/M8/Apply Level Up Layout")]
        public static void ApplyLevelUp()
        {
            string path = ScreensDir + "UI_Hud.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var lu = root.transform.Find("Overlays/LevelUpOverlay") as RectTransform;
                var dim = lu.Find("Dim")?.GetComponent<Image>();
                if (dim != null) dim.color = new Color(0.047f, 0.055f, 0.078f, 0.8f);   // gameplay recedes

                var title = lu.Find("Title")?.GetComponent<TMP_Text>();
                if (title != null) Font(title, 104);
                var sub = Rect(lu, "Sub", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(800, 50));
                Label(sub, "Label", "Level 2 · choose one", 34, new Color(0.79f, 0.81f, 0.86f));

                for (int i = 0; i < 3; i++)
                {
                    var card = lu.Find("Perk" + i) as RectTransform;
                    if (card == null) continue;
                    var icon = card.Find("Icon") as RectTransform;
                    if (icon != null)
                    {
                        icon.sizeDelta = new Vector2(120, 120);
                        icon.anchoredPosition = new Vector2(24, 0);
                        var mini = icon.Find("Mini");
                        if (mini != null) mini.gameObject.SetActive(false);
                        var art = Rect(icon, "Art", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96));
                        var artImg = art.GetComponent<Image>() ?? art.gameObject.AddComponent<Image>();
                        artImg.preserveAspect = true; artImg.raycastTarget = false; artImg.enabled = false;
                        Label(icon, "Badge", "OB", 50, Color.white);
                    }
                    var name = card.Find("Name") as RectTransform;
                    if (name != null) { name.anchoredPosition = new Vector2(168, 34); name.sizeDelta = new Vector2(600, 60); Font(name.GetComponent<TMP_Text>(), 48); }
                    var desc = card.Find("Desc") as RectTransform;
                    if (desc != null) { desc.anchoredPosition = new Vector2(168, -18); desc.sizeDelta = new Vector2(600, 50); }

                    var pips = Rect(card, "Pips", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(168, -62), new Vector2(300, 16));
                    var row = pips.GetComponent<HorizontalLayoutGroup>() ?? pips.gameObject.AddComponent<HorizontalLayoutGroup>();
                    row.spacing = 8; row.childControlWidth = false; row.childControlHeight = false;
                    row.childForceExpandWidth = false; row.childAlignment = TextAnchor.MiddleLeft;
                    for (int k = 0; k < 5; k++)
                    {
                        var pip = Rect(pips, "Pip" + k, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 14));
                        Img(pip, "pill", Dark, true);
                    }
                }

                // The run's build so far, under the cards.
                var build = Rect(lu, "Build", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(900, 150));
                var cap = Rect(build, "Caption", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 40));
                Label(cap, "Label", "YOUR BUILD", 26, new Color(0.6f, 0.63f, 0.69f));
                var items = Rect(build, "Items", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 90));
                var hl = items.GetComponent<HorizontalLayoutGroup>() ?? items.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 14; hl.childControlWidth = false; hl.childControlHeight = false;
                hl.childForceExpandWidth = false; hl.childAlignment = TextAnchor.MiddleCenter;
                for (int k = 0; k < 10; k++)
                {
                    var it = Rect(items, "Item" + k, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
                    Img(it, "rounded_24", Panel, true);
                    var art = Rect(it, "Art", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
                    var a = art.GetComponent<Image>() ?? art.gameObject.AddComponent<Image>();
                    a.preserveAspect = true; a.raycastTarget = false; a.enabled = false;
                    Label(it, "Badge", "OB", 30, Color.white);
                    it.gameObject.SetActive(false);
                }

                var hint = lu.Find("Hint")?.GetComponent<TMP_Text>();
                if (hint != null) { hint.text = "Auto-picks in 30 s"; Font(hint, 28); hint.rectTransform.anchoredPosition = new Vector2(0, 120); }

                var ov = root.GetComponent<RunOverlays>();
                var so = new SerializedObject(ov);
                so.FindProperty("skillIcons").objectReferenceValue = SkillIconSetBuilder.Refresh();
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[M8 UI] Level Up layout applied.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Phase A1 (owner-approved mockup SK_LevelUp, 2026-09-29): the build strip under the cards
        /// becomes the 6 skill slots and 4 stat slots — a gap between the groups, a rank number on
        /// every slot. Run after Apply Level Up Layout; idempotent.
        /// </summary>
        [MenuItem("ZombieWar/UI/M8/Apply Level Up Slots")]
        public static void ApplyLevelUpSlots()
        {
            string path = ScreensDir + "UI_Hud.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var items = root.transform.Find("Overlays/LevelUpOverlay/Build/Items") as RectTransform;
                if (items == null) throw new System.Exception("Build/Items missing: run Apply Level Up Layout first");
                for (int k = 0; k < 10; k++)
                {
                    var it = items.Find("Item" + k) as RectTransform;
                    if (it == null) continue;
                    var rank = Rect(it, "Rank", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-4, 2), new Vector2(40, 30));
                    var t = Label(rank, "Label", "5", 24, Color.white);
                    t.alignment = TextAlignmentOptions.BottomRight;
                    t.fontStyle = FontStyles.Bold;
                }
                // A spacer between slot 6 (last skill) and slot 7 (first stat) keeps the groups apart.
                var gap = items.Find("Gap") as RectTransform ?? Rect(items, "Gap", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 80));
                gap.sizeDelta = new Vector2(18, 80);
                var sixth = items.Find("Item5");
                if (sixth != null) gap.SetSiblingIndex(sixth.GetSiblingIndex() + 1);
                var hl = items.GetComponent<HorizontalLayoutGroup>();
                if (hl != null) hl.spacing = 10;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[M8 UI] Level Up slots applied.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Phase A7 (owner-approved mockup SK_Chest, 2026-09-29): the chest overlay over the frozen
        /// run — the gold chest, the card it gives (an evolution with its recipe, a rank-up or a
        /// bonus) and CLAIM. Built next to the level-up overlay in UI_Hud; idempotent. RunOverlays
        /// finds it by name and binds it (RunOverlays.Chest.cs).
        /// </summary>
        [MenuItem("ZombieWar/UI/M8/Apply Chest Overlay")]
        public static void ApplyChestOverlay()
        {
            string path = ScreensDir + "UI_Hud.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var lu = root.transform.Find("Overlays/LevelUpOverlay") as RectTransform;
                if (lu == null) throw new System.Exception("Overlays/LevelUpOverlay missing");
                var overlays = lu.parent;
                var ch = Rect(overlays, "ChestOverlay", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                ch.SetSiblingIndex(lu.GetSiblingIndex() + 1);
                var gold = UITheme.M8Yellow;

                var dim = Rect(ch, "Dim", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var dimImg = Img(dim, "rounded_24", UITheme.M8Scrim, false);
                dimImg.sprite = null; dimImg.raycastTarget = true;   // swallows taps on the game behind

                var glow = Rect(ch, "Glow", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -640), new Vector2(900, 900));
                Img(glow, "glow_soft", new Color(gold.r, gold.g, gold.b, 0.55f), false);

                var title = Rect(ch, "Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(1000, 130));
                Label(title, "Label", "TREASURE CHEST", 96, UITheme.M8Ink);
                var sub = Rect(ch, "Sub", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -385), new Vector2(1000, 50));
                Label(sub, "Label", "Bosses always drop one · elites and Supply Drops can too", 30, new Color(1f, 0.91f, 0.65f));

                var chest = Rect(ch, "Chest", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -620), new Vector2(330, 330));
                IconImg(chest, "ItemIcon_Chest_Gold.Png");

                var card = Rect(ch, "Card", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -800), new Vector2(920, 690));
                Img(card, "rounded_32", UITheme.M8Card, true);
                var frame = Rect(card, "Frame", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
                Img(frame, "frame_32", gold, true);

                var tag = Rect(card, "Tag", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -52), new Vector2(300, 56));
                Img(tag, "pill", gold, true);
                Label(tag, "Label", "EVOLUTION", 30, UITheme.M8Ink);

                Tile(card, "Icon", new Vector2(0, -195), 180, 56);
                var name = Rect(card, "Name", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(860, 80));
                var nameText = Label(name, "Label", "Buzzsaw Halo", 64, UITheme.M8Ink);
                MoveText(nameText, name);
                var desc = Rect(card, "Desc", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -400), new Vector2(840, 70));
                var descText = Label(desc, "Label", "6 bigger, faster blades", 32, UITheme.M8InkDim);
                descText.enableWordWrapping = true;
                MoveText(descText, desc);

                var recipe = Rect(card, "Recipe", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -515), new Vector2(620, 120));
                Img(recipe, "rounded_24", UITheme.M8Deep, true);
                Tile(recipe, "A", new Vector2(-210, 0), 104, 34, center: true);
                var plus = Rect(recipe, "Plus", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-105, 0), new Vector2(80, 80));
                Label(plus, "Label", "+", 60, UITheme.M8Ink);
                Tile(recipe, "B", new Vector2(0, 0), 104, 34, center: true);
                var eq = Rect(recipe, "Eq", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(105, 0), new Vector2(80, 80));
                Label(eq, "Label", "=", 60, UITheme.M8Ink);
                Tile(recipe, "Evo", new Vector2(210, 0), 104, 34, center: true);

                var req = Rect(card, "Req", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -630), new Vector2(860, 50));
                var reqText = Label(req, "Label", "ORBIT BLADES RANK 5 + MOVE SPEED UP", 26, new Color(1f, 0.85f, 0.45f));
                MoveText(reqText, req);

                // CLAIM: the yellow primary button with its lip.
                var claim = Rect(ch, "Claim", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(820, 140));
                var lip = Rect(claim, "Lip", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -10), Vector2.zero);
                Img(lip, "rounded_32", UITheme.M8YellowLip, true);
                var face = Rect(claim, "Face", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var faceImg = Img(face, "rounded_32", UITheme.M8Yellow, true);
                faceImg.raycastTarget = true;
                Label(face, "Label", "CLAIM", 64, UITheme.M8Ink);
                var btn = claim.GetComponent<Button>() ?? claim.gameObject.AddComponent<Button>();
                btn.targetGraphic = faceImg; btn.transition = Selectable.Transition.None;
                if (claim.GetComponent<UIFxPress>() == null) claim.gameObject.AddComponent<UIFxPress>();

                var hint = Rect(ch, "Hint", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(900, 50));
                var hintText = Label(hint, "Label", "Auto-claims in 10 s", 28, UITheme.M8InkDim);
                MoveText(hintText, hint);

                ch.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[M8 UI] Chest overlay applied.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// A card tile: tinted rounded square, icon art, and the two-letter badge fallback.
        static RectTransform Tile(RectTransform parent, string name, Vector2 pos, float size, float badge, bool center = false)
        {
            var anchor = center ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1);
            var t = Rect(parent, name, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            Img(t, "rounded_24", UITheme.M8Card, true);
            var art = Rect(t, "Art", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.8f, size * 0.8f));
            var a = art.GetComponent<Image>() ?? art.gameObject.AddComponent<Image>();
            a.preserveAspect = true; a.raycastTarget = false; a.enabled = false;
            Label(t, "Badge", "BZ", badge, Color.white);
            return t;
        }

        /// RunOverlays binds "Card/Name" etc. as the TMP itself: move the label's text component onto
        /// the named node so the path resolves to the text, not to an empty holder.
        static void MoveText(TMP_Text label, RectTransform onto)
        {
            if (label.transform == onto) return;
            var existing = onto.GetComponent<TextMeshProUGUI>();
            if (existing == null)
            {
                existing = onto.gameObject.AddComponent<TextMeshProUGUI>();
                existing.font = label.font; existing.fontSize = label.fontSize; existing.color = label.color;
                existing.alignment = label.alignment; existing.text = label.text; existing.raycastTarget = false;
                existing.enableWordWrapping = label.enableWordWrapping; existing.overflowMode = label.overflowMode;
            }
            Object.DestroyImmediate(label.gameObject);
        }

        // ═════════════════════════════════════════════════════════════ Result

        [MenuItem("ZombieWar/UI/M8/Apply Result Layout")]
        public static void ApplyResult()
        {
            string path = ScreensDir + "UI_Hud.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rs = root.transform.Find("Overlays/GameOverScreen") as RectTransform;

                var banner = rs.Find("Banner") as RectTransform;
                if (banner != null)
                {
                    banner.anchoredPosition = new Vector2(0, -120); banner.sizeDelta = new Vector2(900, 60);
                    var t = banner.GetComponent<TMP_Text>(); Font(t, 38); t.color = Hex("FF8A8A");
                }
                var time = Rect(rs, "Time", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(900, 200));
                Label(time, "Label", "0:00", 190, Color.white);

                var pill = rs.Find("RecordPill") as RectTransform;
                if (pill != null)
                {
                    pill.anchoredPosition = new Vector2(0, -392); pill.sizeDelta = new Vector2(480, 70);
                    var l = pill.Find("L")?.GetComponent<TMP_Text>(); if (l != null) Font(l, 34);
                }

                var stats = Rect(rs, "Stats", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -500), new Vector2(952, 170));
                var sg = stats.GetComponent<HorizontalLayoutGroup>() ?? stats.gameObject.AddComponent<HorizontalLayoutGroup>();
                sg.spacing = 20; sg.childControlWidth = true; sg.childControlHeight = true; sg.childForceExpandWidth = true;
                string[] caps = { "KILLS", "LEVEL", "PEAK THREAT" };
                for (int i = 0; i < 3; i++)
                {
                    var box = Rect(stats, "Stat" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 170));
                    Img(box, "rounded_32", Panel, true);
                    var v = Rect(box, "Value", new Vector2(0, 0.35f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    Label(v, "Label", "0", 72, Color.white);
                    var c = Rect(box, "Caption", new Vector2(0, 0), new Vector2(1, 0.38f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    Label(c, "Label", caps[i], 26, new Color(0.6f, 0.63f, 0.69f));
                }

                var card = rs.Find("PayoutCard") as RectTransform;
                if (card != null) card.anchoredPosition = new Vector2(0, -700);

                // Mockup ground: dark slate, not the old red wash (the vignette still frames it).
                var bgImg = rs.Find("Bg")?.GetComponent<Image>();
                if (bgImg != null) bgImg.color = Hex("262A36FF");
                var vig = rs.Find("Vignette")?.GetComponent<Image>();
                if (vig != null) vig.color = new Color(0f, 0f, 0f, 0.35f);   // darkens the edges, no red wash
                var shop = rs.Find("ShopLink") as RectTransform;
                if (shop != null) shop.anchoredPosition = new Vector2(0, -1440);

                var build = Rect(rs, "Build", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1250), new Vector2(952, 150));
                var cap = Rect(build, "Caption", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 40));
                var capLabel = Label(cap, "Label", "YOUR BUILD", 26, new Color(0.6f, 0.63f, 0.69f));
                capLabel.alignment = TextAlignmentOptions.Left;
                var items = Rect(build, "Items", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 96));
                var hl = items.GetComponent<HorizontalLayoutGroup>() ?? items.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 14; hl.childControlWidth = false; hl.childControlHeight = false;
                hl.childForceExpandWidth = false; hl.childAlignment = TextAnchor.MiddleLeft;
                for (int k = 0; k < 10; k++)
                {
                    var it = Rect(items, "Item" + k, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
                    Img(it, "rounded_24", Panel, true);
                    var art = Rect(it, "Art", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
                    var ai = art.GetComponent<Image>() ?? art.gameObject.AddComponent<Image>();
                    ai.preserveAspect = true; ai.raycastTarget = false; ai.enabled = false;
                    Label(it, "Badge", "OB", 30, Color.white);
                    var rk = Rect(it, "Rank", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(8, -6), new Vector2(54, 30));
                    Img(rk, "pill", new Color(0.96f, 0.95f, 0.92f), true);
                    var rkl = Label(rk, "Label", "1", 20, Ink);
                    rkl.enableAutoSizing = true; rkl.fontSizeMin = 12; rkl.fontSizeMax = 20;
                    it.gameObject.SetActive(false);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[M8 UI] Result layout applied.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ═════════════════════════════════════════════════════════════ Hub

        [MenuItem("ZombieWar/UI/M8/Apply Hub Layout")]
        public static void ApplyHub()
        {
            string path = ScreensDir + "UI_HubScreen.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var safe = root.transform.Find("Safe");
                Delete(safe, "CampaignSelector");   // stages are gone (M6): one endless world

                // Best survival: caption above the value, no overlap.
                var chip = safe.Find("AvatarChip");
                var cap = chip?.Find("RecordCaption") as RectTransform;
                if (cap != null)
                {
                    cap.anchorMin = cap.anchorMax = new Vector2(0, 1); cap.pivot = new Vector2(0, 1);
                    cap.anchoredPosition = new Vector2(112, -12); cap.sizeDelta = new Vector2(220, 30);
                    var l = cap.Find("L")?.GetComponent<TMP_Text>(); if (l != null) { l.text = "BEST SURVIVAL"; l.fontSize = 22; }
                }
                var val = chip?.Find("RecordValue") as RectTransform;
                if (val != null)
                {
                    val.anchorMin = val.anchorMax = new Vector2(0, 1); val.pivot = new Vector2(0, 1);
                    val.anchoredPosition = new Vector2(112, -40); val.sizeDelta = new Vector2(220, 50);
                }

                var podium = safe.Find("Podium") as RectTransform;
                var edit = podium.Find("EditChip") as RectTransform;
                if (edit != null) { edit.anchorMin = edit.anchorMax = new Vector2(1, 1); edit.pivot = new Vector2(1, 1); edit.anchoredPosition = new Vector2(-28, -28); }

                // Equipped weapon plate over the bottom of the podium.
                var plate = Rect(podium, "WeaponPlate", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(-56, 176));
                Img(plate, "rounded_32", Hex("2A2F3DF2"), true);
                var tile = Rect(plate, "Tile", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(190, 144));
                var tileImg = Img(tile, "rounded_24", Hex("3A2A55"), true);
                // Icons keep the roster's relative scale (a pistol is smaller than a rifle), so the
                // art is allowed to spill past the tile rather than shrink to nothing.
                var icon = Rect(tile, "Icon", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 40));
                var iconImg = icon.GetComponent<Image>() ?? icon.gameObject.AddComponent<Image>();
                iconImg.preserveAspect = true; iconImg.raycastTarget = false;
                var nameRt = Rect(plate, "Name", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(222, -14), new Vector2(-440, 58));
                var nameL = Label(nameRt, "Label", "FAMAS", 48, Color.white); nameL.alignment = TextAlignmentOptions.Left;
                nameL.enableAutoSizing = true; nameL.fontSizeMin = 28; nameL.fontSizeMax = 48;
                var metaRt = Rect(plate, "Meta", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(222, -70), new Vector2(-440, 34));
                var metaL = Label(metaRt, "Label", "EPIC · RIFLE", 26, Hex("C9CFDB")); metaL.alignment = TextAlignmentOptions.Left;
                var dmg = StatBar(plate, "Damage", "DMG", new Vector2(222, -108), XpBlue);
                var rate = StatBar(plate, "Rate", "RATE", new Vector2(222, -140), Hex("5BD68A"));
                var change = Rect(plate, "ChangeBtn", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(190, 100));
                Img(change, "rounded_24", XpBlue, true).raycastTarget = true;
                var changeBtn = change.GetComponent<Button>() ?? change.gameObject.AddComponent<Button>();
                Label(change, "Label", "CHANGE", 34, Color.white);

                // PLAY: the label moves up to make room for the goal line.
                var playLabel = safe.Find("PlayButtonWrap/PlayButton/Label") as RectTransform;
                if (playLabel != null) playLabel.anchoredPosition = new Vector2(0, 22);
                var playBtn = safe.Find("PlayButtonWrap/PlayButton") as RectTransform;
                var sub = Rect(playBtn, "Sub", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(0, 44));
                var subL = Label(sub, "Label", "Beat your best: 6:29", 32, Hex("5A3E00"));

                var hub = root.GetComponent<ZombieWar.UI.HubScreen>();
                var so = new SerializedObject(hub);
                so.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>("Assets/_Project/UI/Data/UIPrototypeCatalog.asset");
                so.FindProperty("weaponIcon").objectReferenceValue = iconImg;
                so.FindProperty("weaponTile").objectReferenceValue = tileImg;
                so.FindProperty("weaponNameLabel").objectReferenceValue = nameL;
                so.FindProperty("weaponMetaLabel").objectReferenceValue = metaL;
                so.FindProperty("damageFill").objectReferenceValue = dmg;
                so.FindProperty("rateFill").objectReferenceValue = rate;
                so.FindProperty("changeWeaponButton").objectReferenceValue = changeBtn;
                so.FindProperty("playSubLabel").objectReferenceValue = subL;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[M8 UI] Hub layout applied.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ═════════════════════════════════════════════════════════════ Loadout

        [MenuItem("ZombieWar/UI/M8/Apply Loadout Layout")]
        public static void ApplyLoadout()
        {
            string path = ScreensDir + "UI_LoadoutScreen.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var safe = root.transform.Find("Safe");
                // One weapon per run: the slot row and the bomb row are gone for good.
                Delete(safe, "PanelLoad");
                Delete(safe.Find("InfoPanel"), "BombRow");

                var info = safe.Find("InfoPanel") as RectTransform;
                info.anchoredPosition = new Vector2(info.anchoredPosition.x, -150);
                info.sizeDelta = new Vector2(info.sizeDelta.x, 540);
                // The weapon block rides up to leave the bottom strip to the signature cards.
                var hero = info.Find("HeroBackdrop") as RectTransform;
                if (hero != null) hero.anchoredPosition = new Vector2(hero.anchoredPosition.x, 70);

                var sig = Rect(info, "Signatures", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(-60, 120));
                var capRt = Rect(sig, "Caption", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 36));
                var cap = Label(capRt, "Label", "RIFLE CARDS IN A RUN", 24, Hex("9AA1B0")); cap.alignment = TextAlignmentOptions.Left;
                var row = Rect(sig, "Row", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 76));
                var hl = row.GetComponent<HorizontalLayoutGroup>() ?? row.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 16; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true;
                var labels = new TMP_Text[2];
                for (int i = 0; i < 2; i++)
                {
                    var chip = Rect(row, "Card" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 76));
                    Img(chip, "rounded_24", Hex("3A2F22"), true);
                    labels[i] = Label(chip, "Label", "Focus Fire", 32, Hex("FFB46B"));
                }

                var kho = safe.Find("KhoArea") as RectTransform;
                if (kho != null) { kho.anchoredPosition = new Vector2(kho.anchoredPosition.x, -730); kho.sizeDelta = new Vector2(kho.sizeDelta.x, 960); }
                var khoLabel = kho?.Find("KhoLabel")?.GetComponent<TMP_Text>();
                if (khoLabel != null) khoLabel.text = "ARSENAL";

                // The bottom link was "No guns yet? Go to Shop" even with 54 guns owned.
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                    if (t.text.Contains("No guns yet"))
                        t.text = "<color=#F5B841>More guns in the Shop \u2192</color>";

                var screen = root.GetComponent<ZombieWar.UI.LoadoutScreen>();
                var so = new SerializedObject(screen);
                so.FindProperty("signatureCaption").objectReferenceValue = cap;
                SetArray(so, "signatureLabels", labels);
                so.FindProperty("heroBackdrop").objectReferenceValue = info.Find("HeroBackdrop")?.GetComponent<Image>();
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[M8 UI] Loadout layout applied.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ═════════════════════════════════════════════════════════════ Shop

        [MenuItem("ZombieWar/UI/M8/Apply Shop Layout")]
        public static void ApplyShop()
        {
            string path = ScreensDir + "UI_ShopScreen.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var safe = root.transform.Find("Safe");

                // Two shipped tabs share the bar (Gacha and Upgrades stay hidden, M6).
                var tab0 = safe.Find("Tabs/Tab0") as RectTransform;
                var tab2 = safe.Find("Tabs/Tab2") as RectTransform;
                if (tab0 != null) { tab0.anchoredPosition = new Vector2(6, tab0.anchoredPosition.y); tab0.sizeDelta = new Vector2(500, tab0.sizeDelta.y); }
                if (tab2 != null) { tab2.anchoredPosition = new Vector2(510, tab2.anchoredPosition.y); tab2.sizeDelta = new Vector2(500, tab2.sizeDelta.y); }

                // Three compact columns instead of two big ones.
                const float cellW = 322f, cellH = 300f, gap = 16f;
                foreach (var grid in safe.GetComponentsInChildren<GridLayoutGroup>(true))
                {
                    if (!grid.name.StartsWith("Grid_")) continue;
                    grid.cellSize = new Vector2(cellW, cellH);
                    grid.spacing = new Vector2(gap, gap);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 3;
                    int n = grid.transform.childCount;
                    int rows = Mathf.CeilToInt(n / 3f);
                    float h = rows * cellH + Mathf.Max(0, rows - 1) * gap;
                    var le = grid.GetComponent<LayoutElement>();
                    if (le != null) { le.preferredHeight = h; le.minHeight = h; }
                    ((RectTransform)grid.transform).sizeDelta = new Vector2(((RectTransform)grid.transform).sizeDelta.x, h);

                    for (int i = 0; i < n; i++)
                    {
                        var card = grid.transform.GetChild(i);
                        var name = card.Find("Name") as RectTransform;
                        if (name != null)
                        {
                            name.anchorMin = new Vector2(0, name.anchorMin.y); name.anchorMax = new Vector2(1, name.anchorMax.y);
                            name.sizeDelta = new Vector2(-16, name.sizeDelta.y);
                            var t = name.GetComponent<TMP_Text>();
                            if (t != null) { t.enableAutoSizing = true; t.fontSizeMin = 22; t.fontSizeMax = Mathf.Max(t.fontSizeMax, t.fontSize); }
                        }
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[M8 UI] Shop layout applied.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// A caption + bar row; returns the fill (scaled through anchorMax.x at runtime).
        static RectTransform StatBar(RectTransform parent, string name, string caption, Vector2 pos, Color color)
        {
            var row = Rect(parent, name, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), pos, new Vector2(-440, 26));
            var capRt = Rect(row, "Caption", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(80, 0));
            var capL = Label(capRt, "Label", caption, 22, Hex("C9CFDB")); capL.alignment = TextAlignmentOptions.Left;
            var track = Rect(row, "Track", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(86, 0), new Vector2(-86, 14));
            Img(track, "pill", Hex("1F2330"), true);
            var fill = Rect(track, "Fill", Vector2.zero, new Vector2(0.5f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            Img(fill, "pill", color, true);
            return fill;
        }

        static SkillSlotView Slot(Transform parent, string name)
        {
            var slot = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(128, 128));
            Img(slot, "rounded_24", Dark, true);
            var content = Rect(slot, "Content", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var icon = Rect(content, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96));
            var iconImg = icon.GetComponent<Image>() ?? icon.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true; iconImg.raycastTarget = false; iconImg.enabled = false;
            var badge = Label(content, "Badge", "OB", 46, Color.white);
            var cd = Rect(content, "Cooldown", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var cdImg = Img(cd, "rounded_24", new Color(0.04f, 0.05f, 0.08f, 0.62f), false);
            cdImg.type = Image.Type.Filled; cdImg.fillMethod = Image.FillMethod.Radial360;
            cdImg.fillOrigin = (int)Image.Origin360.Top; cdImg.fillClockwise = false; cdImg.fillAmount = 0f;
            var rankRt = Rect(content, "Rank", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(8, -8), new Vector2(76, 38));
            Img(rankRt, "pill", new Color(0.96f, 0.95f, 0.92f), true);
            var rank = Label(rankRt, "Label", "1", 24, Ink);
            rank.enableAutoSizing = true; rank.fontSizeMin = 14; rank.fontSizeMax = 24;
            var border = Rect(slot, "Border", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var borderImg = Img(border, "frame_24", new Color(0.29f, 0.32f, 0.39f, 0.8f), true);
            border.SetAsLastSibling();   // the coloured frame sits on top of the icon and the cooldown shade

            var view = slot.GetComponent<SkillSlotView>() ?? slot.gameObject.AddComponent<SkillSlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("icon").objectReferenceValue = iconImg;
            so.FindProperty("badge").objectReferenceValue = badge;
            so.FindProperty("border").objectReferenceValue = borderImg;
            so.FindProperty("cooldown").objectReferenceValue = cdImg;
            so.FindProperty("rank").objectReferenceValue = rank;
            so.FindProperty("content").objectReferenceValue = content.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            content.gameObject.SetActive(false);
            return view;
        }

        // ═════════════════════════════════════════════════════════════ helpers

        static void Delete(Transform parent, string name)
        {
            var t = parent != null ? parent.Find(name) : null;
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot,
                                  Vector2 pos, Vector2 size)
        {
            var t = parent.Find(name) as RectTransform;
            if (t == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                t = (RectTransform)go.transform;
                t.SetParent(parent, false);
            }
            t.anchorMin = aMin; t.anchorMax = aMax; t.pivot = pivot;
            t.anchoredPosition = pos; t.sizeDelta = size;
            t.localScale = Vector3.one;
            return t;
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

        static Image IconImg(RectTransform t, string file)
        {
            var img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LayerLabIcons + file);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Color.white;
            return img;
        }

        static TMP_Text Label(RectTransform parent, string name, string text, float size, Color color)
        {
            var t = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var tmp = t.GetComponent<TextMeshProUGUI>() ?? t.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            Font(tmp, size);
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        static void Font(TMP_Text t, float size)
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFont);
            if (f != null) t.font = f;
            t.fontSize = size;
            t.fontStyle = FontStyles.Normal;
        }

        static void SetArray<T>(SerializedObject so, string prop, T[] values) where T : Object
        {
            var p = so.FindProperty(prop);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
    }
}
