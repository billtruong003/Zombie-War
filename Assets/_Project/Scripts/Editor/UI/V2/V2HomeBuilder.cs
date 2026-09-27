using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Home.prefab from the approved V2_Home mockup (390x844 artboard, stacked from
    /// the bottom: tab bar 64, PLAY row 84, missions + next buy 96, info strip 66; the stage with the
    /// side rails and gun card takes the rest). Screen targets are wired by the Menu installer.
    /// </summary>
    public static class V2HomeBuilder
    {
        public const string Path = V2KitSample.Dir + "UI_V2_Home.prefab";
        const string PreviewRT = "Assets/_Project/UI/RenderTextures/MenuCharacterPreview.renderTexture";
        const string Catalog = "Assets/_Project/UI/Data/UIPrototypeCatalog.asset";

        [MenuItem("HordeCall/UI v2/Build Home")]
        public static string Build()
        {
            System.IO.Directory.CreateDirectory(V2KitSample.Dir);
            var r = ScreenRoot("HomeScreen");
            try
            {
                r.gameObject.AddComponent<CanvasGroup>();
                ScreenBackground(r);
                var home = r.gameObject.AddComponent<HomeScreen>();
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();

                TopBar(safe, home);
                Stage(safe, home);
                Strip(safe, home);
                Missions(safe, home);
                FirstRunCard(safe, home);
                PlayRow(safe, home);
                var buttons = NavBar(safe, 0, out var dots);
                var nav = safe.Find("Nav").gameObject.AddComponent<NavBarV2>();
                WireArray(nav, "tabs", buttons);
                WireArray(nav, "dots", dots);
                Wire(home, "nav", nav);
                Wire(home, "catalog", AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(Catalog));
                Reveal(r, home);

                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        // ------------------------------------------------------------ top bar (y 12, h 42)
        static void TopBar(RectTransform safe, HomeScreen home)
        {
            var bar = TopBand(Node(safe, "TopBar"), 12, 42, 12, 12);

            var chip = Box(Node(bar, "Profile"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 150, 42);
            var chipImg = chip.gameObject.AddComponent<Image>(); chipImg.color = new Color(0, 0, 0, 0);
            Wire(home, "profileButton", chip.gameObject.AddComponent<Button>());
            var ringBg = Box(Node(chip, "Ring"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 42, 42);
            Surface(ringBg, Deep, 12f);
            var ring = Fill(Node(ringBg, "Fill"), 0, 0, 0, 0);
            var ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.sprite = Spr("rounded_24"); ringImg.type = Image.Type.Filled; ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillOrigin = (int)Image.Origin360.Top; ringImg.color = Yellow; ringImg.fillAmount = 0.6f; ringImg.raycastTarget = false;
            Wire(home, "xpRing", ringImg);
            var avatar = Fill(Node(ringBg, "Avatar"), 3, 3, 3, 3);
            Surface(avatar, Blue, 9f);
            Picto(Fill(Node(avatar, "Face"), 6, 5, 6, 5), "User", Ink);
            var lvl = Box(Node(ringBg, "Level"), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), 0, 0, 22, 16);
            Surface(lvl, Ground, 5f);
            Wire(home, "levelLabel", Title(Fill(Node(lvl, "T"), 0, 0, 0, 0), "12", 12f, Ink, TextAlignmentOptions.Center));
            var nameText = Body(Box(Node(chip, "Name"), new Vector2(0, 0.5f), new Vector2(0, 0), 50, 1, 86, 20), "Survivor 3390", 12f);
            nameText.enableAutoSizing = true; nameText.fontSizeMin = Px(9); nameText.fontSizeMax = Px(12);
            Wire(home, "nameLabel", nameText);
            Wire(home, "bestLabel", Body(Box(Node(chip, "Best"), new Vector2(0, 0.5f), new Vector2(0, 1), 50, 0, 100, 16), "BEST 9:42", 10f, Yellow));

            var right = Box(Node(bar, "Right"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 226, 34);
            var row = Row(right, 6, TextAnchor.MiddleRight); row.childForceExpandHeight = true;
            var coin = Node(right, "Coin"); Size(coin, 88, 32);
            Wire(home, "coinLabel", Pill(coin, "Money_Coin", "96.7K", out var coinPlus)); Wire(home, "coinPlus", coinPlus);
            var gem = Node(right, "Gem"); Size(gem, 84, 32);
            Wire(home, "gemLabel", Pill(gem, "Gem_Diamond_Purple", "240", out var gemPlus)); Wire(home, "gemPlus", gemPlus);
            var set = Node(right, "Settings"); Size(set, 34, 34);
            Surface(set, Card, 10f, true);
            Picto(Fill(Node(set, "Gear"), 7, 7, 7, 7), "Setting");
            Wire(home, "settingsButton", set.gameObject.AddComponent<Button>());
            set.gameObject.AddComponent<UIPressFeel>();
        }

        // ------------------------------------------------------------ stage (y 62 .. 310 from bottom)
        static void Stage(RectTransform safe, HomeScreen home)
        {
            var stage = Fill(Node(safe, "Stage"), 0, 62, 0, 310);

            // Owner: the 3D view needs only the shadow blob under the feet (drawn in the preview
            // itself), no panel or pedestal behind the character.
            // The fitter sizes against its PARENT, so the character sits in its own area that stops
            // above the gun card; fitting against the whole stage put the feet under the card.
            var area = Fill(Node(stage, "CharacterArea"), 70, 6, 70, 6 + 62 + 8);
            var view = Node(area, "Character");
            var raw = view.gameObject.AddComponent<RawImage>();
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(PreviewRT);
            raw.texture = rt; raw.raycastTarget = true;
            if (rt != null)
            {
                var fit = view.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = rt.width / (float)rt.height;
            }
            Wire(home, "stageButton", view.gameObject.AddComponent<Button>());

            var outfit = Box(Node(stage, "Outfit"), new Vector2(1, 1), new Vector2(1, 1), -78, -10, 70, 28);
            Surface(outfit, Card, 9f, true);
            Body(Fill(Node(outfit, "T"), 0, 0, 0, 0), "OUTFIT", 10f, Ink, TextAlignmentOptions.Center);
            Wire(home, "outfitButton", outfit.gameObject.AddComponent<Button>());

            var left = Box(Node(stage, "LeftRail"), new Vector2(0, 1), new Vector2(0, 1), 12, -6, 60, 300);
            Column(left, 8);
            WireRail(home, "daily", RailBtn(left, "Daily", "DAILY", "Calendar_Check", Hex("263a2e")));
            WireRail(home, "events", RailBtn(left, "Events", "EVENTS", "Skull", Hex("4a1f14")));
            WireRail(home, "mail", RailBtn(left, "Mail", "MAIL", "Mail", Hex("1f3150")));
            WireRail(home, "rank", RailBtn(left, "Rank", "RANK", "Trophy_Gold", Gold3A));

            var right = Box(Node(stage, "RightRail"), new Vector2(1, 1), new Vector2(1, 1), -12, -6, 60, 300);
            Column(right, 8);
            left.Find("Mail").gameObject.SetActive(FeatureFlags.Backend);
            left.Find("Rank").gameObject.SetActive(FeatureFlags.Backend);
            WireRail(home, "gacha", RailBtn(right, "Gacha", "GACHA", "Ticket_Gold", Hex("4a1f14")));
            WireRail(home, "pass", RailBtn(right, "Pass", "PASS", "Star_Gold", Gold3A));
            WireRail(home, "starter", RailBtn(right, "Starter", "STARTER", "Gift_Green", Hex("1f3a26")));

            GunCard(stage, home);
        }

        /// <summary>Side-rail button: icon, label, optional sub line (timer / "LV 3"), badge, lock.</summary>
        static HomeScreen.Rail RailBtn(RectTransform parent, string name, string label, string icon, Color iconBg)
        {
            var rt = Node(parent, name); Size(rt, 60, 62);
            Surface(rt, Card, 12f, true);
            var b = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<UIPressFeel>();
            var ib = Box(Node(rt, "IconBg"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -6, 34, 28);
            Surface(ib, iconBg, 8f);
            var ic = IconImage(Fill(Node(ib, "Icon"), 3, 2, 3, 2), icon);
            Body(Box(Node(rt, "Label"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -36, 60, 12), label, 9f, Ink, TextAlignmentOptions.Center);
            var sub = Body(Box(Node(rt, "Sub"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -48, 60, 11), "", 8f, Hex("ffb27a"), TextAlignmentOptions.Center);
            var lockRt = Box(Node(rt, "Lock"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), 0, -20, 16, 16);
            Picto(lockRt, "Lock_1", Ink);
            lockRt.gameObject.SetActive(false);
            var count = Badge(rt, "1");
            var badge = count.transform.parent.gameObject;
            badge.SetActive(false);
            return new HomeScreen.Rail { button = b, badge = badge, badgeCount = count, sub = sub, lockIcon = lockRt.gameObject, icon = ic };
        }

        static void GunCard(RectTransform stage, HomeScreen home)
        {
            var card = BottomBand(Node(stage, "GunCard"), 6, 62, 12, 12);
            Surface(card, Card, RCard, true);
            Wire(home, "gunCard", card.gameObject.AddComponent<Button>());
            var tile = Box(Node(card, "Tile"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 66, 46);
            Wire(home, "gunTile", Surface(tile, Rarity[3], RTile));
            var icon = Fill(Node(tile, "Icon"), 4, 4, 4, 4);
            var ii = icon.gameObject.AddComponent<Image>(); ii.preserveAspect = true; ii.raycastTarget = false; ii.enabled = false;
            Wire(home, "gunIcon", ii);

            var line1 = Box(Node(card, "Line1"), new Vector2(0, 0.5f), new Vector2(0, 0), 84, 2, 200, 20);
            var row = Row(line1, 6); row.childForceExpandHeight = false;
            var nm = Node(line1, "Name"); Size(nm, -1, 20); Wire(home, "gunName", Body(nm, "G36C", 14f));
            var tag = Tag(line1, "Tier", "EPIC", Rarity[3], OutlineInk);
            Size(tag, -1, 18);   // width follows the tier name (UNCOMMON is long)
            Wire(home, "gunTier", tag.Find("Text").GetComponent<TMP_Text>());
            Wire(home, "gunTierBg", tag.GetComponent<Image>());
            var stars = Node(line1, "Stars"); Size(stars, 44, 14);
            Row(stars, 1);
            var starImgs = new Image[3];
            for (int i = 0; i < 3; i++) { var s = Node(stars, "S" + i); Size(s, 14, 14); starImgs[i] = IconImage(s, "Star_Gold"); }
            WireArray(home, "gunStars", starImgs);
            Wire(home, "gunMeta", Body(Box(Node(card, "Meta"), new Vector2(0, 0.5f), new Vector2(0, 1), 84, -2, 210, 16), "RIFLE · POWER 1,280", 10f, Gem));

            var swap = Box(Node(card, "Swap"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -8, 0, 56, 28);
            Surface(swap, Blue, 7f);
            Body(Fill(Node(swap, "T"), 0, 0, 0, 0), "SWAP", 11f, Hex("0b2340"), TextAlignmentOptions.Center);
        }

        // ------------------------------------------------------------ info strip (bottom 244, h 58)
        static void Strip(RectTransform safe, HomeScreen home)
        {
            var strip = BottomBand(Node(safe, "Strip"), 244, 58, 12, 12);
            Row(strip, 6, TextAnchor.MiddleLeft, true);
            var d = StripCard(strip, "Daily", Hex("263a2e"), "Calendar_Check", "Stamp card", "Check in today", Green);
            Wire(home, "stripDaily", d.button); Wire(home, "stripDailyText", d.sub);
            var g = StripCard(strip, "Gacha", Hex("4a1f14"), "Ticket_Gold", "Inferno", "EVENT BANNER", Hex("ffb27a"));
            Wire(home, "stripGacha", g.button);
            var p = StripCard(strip, "Pass", Hex("1f3150"), "Star_Gold", "Frostbite", "PASS LV 10", Hex("9fd0f5"));
            Wire(home, "stripPass", p.button); Wire(home, "stripPassText", p.sub);
        }

        static (Button button, TextMeshProUGUI sub) StripCard(RectTransform parent, string name, Color bg, string icon, string title, string sub, Color subColor)
        {
            var rt = Node(parent, name);
            Surface(rt, bg, 12f, true);
            var b = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<UIPressFeel>();
            IconImage(Box(Node(rt, "Icon"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 30, 30), icon);
            Body(Box(Node(rt, "Title"), new Vector2(0, 0.5f), new Vector2(0, 0), 42, 1, 70, 16), title, 11f);
            var s = Body(Box(Node(rt, "Sub"), new Vector2(0, 0.5f), new Vector2(0, 1), 42, -1, 70, 14), sub, 9f, subColor);
            return (b, s);
        }

        // ------------------------------------------------------------ missions + next buy (bottom 148, h 88)
        static void Missions(RectTransform safe, HomeScreen home)
        {
            var band = BottomBand(Node(safe, "Info"), 148, 88, 12, 12);
            Row(band, 8, TextAnchor.MiddleLeft, true);

            var mc = Node(band, "Missions"); Size(mc, -1, 88, 1.4f);
            Surface(mc, Card, RCard, true);
            Wire(home, "missionsCard", mc.gameObject.AddComponent<Button>());
            Wire(home, "missionsHeader", Label(Box(Node(mc, "Header"), new Vector2(0, 1), new Vector2(0, 1), 10, -8, 180, 14), "Missions · 2 ready"));
            var rows = new HomeScreen.MissionRow[3];
            for (int i = 0; i < 3; i++)
            {
                var row = Box(Node(mc, "Row" + i), new Vector2(0, 1), new Vector2(0, 1), 10, -26 - i * 20, 196, 18);
                row.anchorMax = new Vector2(1, 1); row.offsetMax = new Vector2(-Px(10), row.offsetMax.y);
                var title = Body(Fill(Node(row, "Title"), 0, 0, 62, 0), "Kill 150 zombies", 12f);
                var claim = Tag(row, "Claim", "CLAIM", Green, OnGreen, 16f, 9f);
                claim.anchorMin = claim.anchorMax = new Vector2(1, 0.5f); claim.pivot = new Vector2(1, 0.5f); claim.anchoredPosition = Vector2.zero;
                Object.DestroyImmediate(claim.GetComponent<HorizontalLayoutGroup>()); claim.sizeDelta = new Vector2(Px(46), Px(16));
                claim.gameObject.SetActive(false);
                var track = Box(Node(row, "Track"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), 0, 0, 54, 6);
                var bar = Bar(track, Blue, 0.6f);
                rows[i] = new HomeScreen.MissionRow { root = row.gameObject, title = title, claimTag = claim.gameObject, bar = (RectTransform)bar.transform };
            }
            WireMissionRows(home, rows);

            var nb = Node(band, "NextBuy"); Size(nb, -1, 88, 1f);
            Surface(nb, Card, RCard, true);
            Wire(home, "nextBuyCard", nb.gameObject.AddComponent<Button>());
            Label(Box(Node(nb, "Header"), new Vector2(0, 1), new Vector2(0, 1), 10, -8, 100, 14), "Next buy");
            var ic = Box(Node(nb, "Icon"), new Vector2(0, 1), new Vector2(0, 1), 10, -26, 40, 20);
            var ii = ic.gameObject.AddComponent<Image>(); ii.preserveAspect = true; ii.raycastTarget = false; ii.enabled = false;
            Wire(home, "nextBuyIcon", ii);
            Wire(home, "nextBuyName", Body(Box(Node(nb, "Name"), new Vector2(0, 1), new Vector2(0, 1), 56, -26, 80, 20), "AK-47", 12f));
            var track2 = Box(Node(nb, "Track"), new Vector2(0, 1), new Vector2(0, 1), 10, -54, 110, 6);
            track2.anchorMax = new Vector2(1, 1); track2.offsetMax = new Vector2(-Px(10), track2.offsetMax.y);
            Wire(home, "nextBuyBar", (RectTransform)Bar(track2, Yellow, 0.48f).transform);
            Wire(home, "nextBuyValue", Body(Box(Node(nb, "Value"), new Vector2(0, 1), new Vector2(0, 1), 10, -64, 120, 14), "1,240 / 2,600", 10f, Dim));
        }

        // ------------------------------------------------------------ FTUE (owner 2026-09-27)
        /// First launch (approved HomeFirst mockup): a "First run" card where missions and next buy
        /// sit, with the unlock ladder above it. HomeScreen swaps it in while no run was played.
        static void FirstRunCard(RectTransform safe, HomeScreen home)
        {
            var band = BottomBand(Node(safe, "FirstRun"), 148, 88, 12, 12);
            var ladder = TopBand(Node(band, "Ladder"), 0, 34);
            Row(ladder, 6, TextAnchor.MiddleLeft, true);
            string[] lv = { "AFTER RUN 1", "LV 2", "LV 3" }, what = { "Arsenal + shop", "Pass + missions", "Gacha + events" };
            for (int i = 0; i < 3; i++)
            {
                var w = Node(ladder, "Step" + i);
                Surface(w, Deep, 10f);
                Label(Box(Node(w, "L"), new Vector2(0, 1), new Vector2(0, 1), 8, -4, 110, 12), lv[i], null, 9f);
                Shrink(Body(Box(Node(w, "T"), new Vector2(0, 1), new Vector2(0, 1), 8, -16, 110, 14), what[i], 11f));
            }
            var card = BottomBand(Node(band, "Card"), 0, 48);
            Surface(card, Card, RCard);
            var n = Box(Node(card, "N"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 8, 0, 34, 34);
            Surface(n, Hex("c7efff"), 10f);
            Title(Fill(Node(n, "T"), 0, 0, 0, 0), "1", 18f, Ink, TextAlignmentOptions.Center);
            Body(Box(Node(card, "Title"), new Vector2(0, 0.5f), new Vector2(0, 0), 50, 1, 260, 18), "First run", 13f);
            Shrink(Body(Box(Node(card, "Sub"), new Vector2(0, 0.5f), new Vector2(0, 1), 50, -1, 280, 16), "Drag to move. Your gun fires by itself.", 11f, Dim));
            Wire(home, "firstRunCard", band.gameObject);
            band.gameObject.SetActive(false);
        }

        /// After the first run: the Arsenal and Shop open with a short reveal of the player's gun.
        static void Reveal(RectTransform root, HomeScreen home)
        {
            var p = Fill(Node(root, "FirstRunReveal"), 0, 0, 0, 0);
            UIKitV2.NoTheme = true;
            try { Flat(p, new Color(0.047f, 0.055f, 0.078f, 0.82f), true); } finally { UIKitV2.NoTheme = false; }
            var card = Box(Node(p, "Card"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 10, 330, 410);
            Surface(card, Card, 18f);
            var glow = Box(Node(card, "Glow"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), 0, -130, 300, 300).gameObject.AddComponent<Image>();
            glow.sprite = Spr("glow_soft"); glow.color = new Color(1f, 0.79f, 0.24f, 0.45f); glow.raycastTarget = false;
            Label(TopBand(Node(card, "Kicker"), 18, 14), "UNLOCKED").alignment = TextAlignmentOptions.Center;
            Title(TopBand(Node(card, "Title"), 34, 36), "ARSENAL + SHOP", 28f, Ink, TextAlignmentOptions.Center);
            var tile = Box(Node(card, "Gun"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -84, 220, 120);
            var ti = Surface(tile, Rarity[0], 14f);
            Wire(home, "revealTile", ti);
            var gi = Fill(Node(tile, "Icon"), 14, 10, 14, 10).gameObject.AddComponent<Image>();
            gi.preserveAspect = true; gi.raycastTarget = false;
            Wire(home, "revealIcon", gi);
            Wire(home, "revealName", Title(TopBand(Node(card, "Name"), 214, 28), "PISTOL", 22f, Ink, TextAlignmentOptions.Center));
            var sub = Body(TopBand(Node(card, "Sub"), 244, 46, 20, 20), "Your gun levels up with stars in the Arsenal. New guns wait in the Shop.", 12f, Dim, TextAlignmentOptions.Top);
            sub.enableWordWrapping = true; sub.enableAutoSizing = true; sub.fontSizeMin = sub.fontSize * 0.75f; sub.fontSizeMax = sub.fontSize;
            var go = BottomBand(Node(card, "Open"), 60, 52, 18, 18);
            Wire(home, "revealOpen", Button(go, "OPEN ARSENAL", Role.Primary, 20f));
            var later = BottomBand(Node(card, "Later"), 14, 36, 18, 18);
            Wire(home, "revealLater", Button(later, "LATER", Role.Quiet, 14f));
            Wire(home, "revealRoot", p.gameObject);
            p.gameObject.SetActive(false);
        }

        static void WireRail(HomeScreen home, string field, HomeScreen.Rail rail)
        {
            var so = new SerializedObject(home);
            var p = so.FindProperty(field);
            p.FindPropertyRelative("button").objectReferenceValue = rail.button;
            p.FindPropertyRelative("badge").objectReferenceValue = rail.badge;
            p.FindPropertyRelative("badgeCount").objectReferenceValue = rail.badgeCount;
            p.FindPropertyRelative("sub").objectReferenceValue = rail.sub;
            p.FindPropertyRelative("lockIcon").objectReferenceValue = rail.lockIcon;
            p.FindPropertyRelative("icon").objectReferenceValue = rail.icon;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireMissionRows(HomeScreen home, HomeScreen.MissionRow[] rows)
        {
            var so = new SerializedObject(home);
            var p = so.FindProperty("missionRows");
            p.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                var e = p.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                e.FindPropertyRelative("title").objectReferenceValue = rows[i].title;
                e.FindPropertyRelative("claimTag").objectReferenceValue = rows[i].claimTag;
                e.FindPropertyRelative("bar").objectReferenceValue = rows[i].bar;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------ PLAY (bottom 64, h 84)
        static void PlayRow(RectTransform safe, HomeScreen home)
        {
            var band = BottomBand(Node(safe, "PlayRow"), 64 + 10, 66, 12, 12);
            // Owner: Endless is the only mode for now, so no Mode button; PLAY takes the row.
            var play = Fill(Node(band, "Play"), 0, 0, 0, 0);
            var btn = Button(play, "PLAY", Role.Primary, 32f);
            Wire(home, "playButton", btn);
            ((RectTransform)play.Find("Face/Label")).offsetMin = new Vector2(0, Px(14));
            Wire(home, "playSub", Body(Box(Node(play.Find("Face") as RectTransform, "Sub"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 8, 240, 14), "Beat your best 9:42", 11f, OnYellow, TextAlignmentOptions.Center));

            var hint = Box(Node(band, "Hint"), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), -40, -6, 40, 48);
            Picto(hint, "Hand_Touch", Ink);
            hint.gameObject.AddComponent<UIFxPulse>();   // first run: the hand points at PLAY
            Wire(home, "playHint", hint.gameObject);
        }
    }
}
