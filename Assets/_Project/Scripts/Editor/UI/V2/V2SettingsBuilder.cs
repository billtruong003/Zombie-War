using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;
using static ZombieWar.EditorTools.V2.UIKitV2;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// M10: builds UI_V2_Settings.prefab from the approved V2_Settings mockup: SOUND, GAME, ACCOUNT,
    /// SUPPORT and PRIVACY cards of 40 px rows, version line, and a delete-data confirm modal.
    /// </summary>
    public static class V2SettingsBuilder
    {
        public const string Path = V2KitSample.Dir + "UI_V2_Settings.prefab";

        [MenuItem("HordeCall/UI v2/Build Settings")]
        public static string Build()
        {
            var r = ScreenRoot("SettingsScreen");
            try
            {
                r.gameObject.AddComponent<CanvasGroup>();
                ScreenBackground(r);
                var s = r.gameObject.AddComponent<SettingsScreen>();
                var safe = Fill(Node(r, "Safe"), 0, 0, 0, 0);
                safe.gameObject.AddComponent<SafeArea>();
                Wire(s, "backButton", Header(safe, "SETTINGS", out _));
                var page = Page(safe, 52);

                SectionLabel(page, "SOUND");
                var sound = CardColumn(page, "Sound");
                Wire(s, "musicSlider", Slider(RowNode(sound, "Music", true), 0.7f));
                Wire(s, "sfxSlider", Slider(RowNode(sound, "Sound effects"), 0.85f));
                WireSwitch(s, "vibration", Switch(RowNode(sound, "Vibration")));

                SectionLabel(page, "GAME");
                var game = CardColumn(page, "Game");
                WireArray(s, "graphics", Segment(RowNode(game, "Graphics", true), "LOW", "MID", "HIGH"));
                WireArray(s, "fps", Segment(RowNode(game, "Frame rate"), "30", "60"));
                WireArray(s, "themes", Segment(RowNode(game, "Theme"), "SKY", "DARK", "CANDY", "MEADOW"));
                Wire(s, "languageRow", Tappable(RowNode(game, "Language"), "English"));
                WireSwitch(s, "notifications", Switch(RowNode(game, "Notifications")));

                SectionLabel(page, "ACCOUNT");
                var account = CardColumn(page, "Account");
                var gpg = RowNode(account, "Google Play Games", true);
                var linked = Tag(gpg, "Linked", "LINKED", Green, OnGreen);
                RightOf(linked, 12, 54);
                var cloud = RowNode(account, "Cloud save");
                Tappable(cloud, "Saved 2 min ago");
                WireArray(s, "backendRows", new List<GameObject> { gpg.gameObject, cloud.gameObject });
                gpg.gameObject.SetActive(FeatureFlags.Backend); cloud.gameObject.SetActive(FeatureFlags.Backend);
                Wire(s, "restoreRow", Tappable(RowNode(account, "Restore purchases", true), ""));
                var idRow = RowNode(account, "Player ID");
                Wire(s, "playerIdRow", Tappable(idRow, "4821 3390"));
                Wire(s, "playerIdValue", idRow.Find("Value").GetComponent<TextMeshProUGUI>());
                // Top border on the first visible row only: the Google row is hidden until a backend exists.
                account.Find("Restore purchases/Line")?.gameObject.SetActive(false);

                SectionLabel(page, "SUPPORT & PRIVACY");
                var support = CardColumn(page, "Support");
                Wire(s, "helpRow", Tappable(RowNode(support, "Help and contact", true), ""));
                Wire(s, "adPrivacyRow", Tappable(RowNode(support, "Ad privacy choices"), ""));
                Wire(s, "privacyRow", Tappable(RowNode(support, "Privacy policy · Terms"), ""));
                var del = RowNode(support, "Delete my data");
                del.Find("Key").GetComponent<TextMeshProUGUI>().color = Red;
                Wire(s, "deleteRow", Tappable(del, ""));

                var ver = Node(page, "Version"); Size(ver, -1, 12 + 16);
                Wire(s, "versionLabel", Body(Fill(Node(ver, "T"), 0, 12, 0, 0), "HordeCall 1.0.0 · Season 1", 11f, Hex("6c7384"), TextAlignmentOptions.Center));

                DeleteModal(r, s);

                var go = PrefabUtility.SaveAsPrefabAsset(r.gameObject, Path);
                return AssetDatabase.GetAssetPath(go);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        /// A 40 px row: key text on the left, a 1 px top border unless first.
        static RectTransform RowNode(RectTransform card, string key, bool first = false)
        {
            var row = Node(card, key); Size(row, -1, 40);
            var line = TopBand(Node(row, "Line"), 0, 1); Flat(line, Edge);
            line.gameObject.SetActive(!first);
            Body(Fill(Node(row, "Key"), 12, 0, 150, 0), key, 13f);
            return row;
        }

        static void RightOf(RectTransform rt, float right, float w, float h = 18)
        {
            var le = rt.GetComponent<LayoutElement>(); if (le != null) Object.DestroyImmediate(le);
            Box(rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), -right, 0, w, h);
        }

        /// Whole row is a button; optional dim value and a chevron on the right.
        static Button Tappable(RectTransform row, string value)
        {
            var hit = row.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            var b = row.gameObject.AddComponent<Button>();
            Picto(Box(Node(row, "Chevron"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -12, 0, 12, 12), "Arrow_Right_1", Dim);
            Body(Box(Node(row, "Value"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -32, 0, 150, 20), value, 11f, Dim, TextAlignmentOptions.MidlineRight);
            return b;
        }

        static Slider Slider(RectTransform row, float value)
        {
            var rt = Box(Node(row, "Slider"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -12, 0, 120, 18);
            var track = Fill(Node(rt, "Track"), 0, 5, 0, 5); Surface(track, Deep, 4f);
            var fillArea = Fill(Node(rt, "FillArea"), 0, 5, 0, 5);
            var fill = Node(fillArea, "Fill"); Surface(fill, Blue, 4f);
            fill.sizeDelta = Vector2.zero;
            var handleArea = Fill(Node(rt, "HandleArea"), 9, 0, 9, 0);
            var handle = Node(handleArea, "Handle"); handle.sizeDelta = new Vector2(Px(18), 0);
            var hImg = handle.gameObject.AddComponent<Image>(); hImg.sprite = Spr("circle"); hImg.color = Ink;
            var sl = rt.gameObject.AddComponent<UnityEngine.UI.Slider>();
            sl.fillRect = fill; sl.handleRect = handle; sl.targetGraphic = hImg;
            sl.direction = UnityEngine.UI.Slider.Direction.LeftToRight; sl.minValue = 0; sl.maxValue = 1;
            sl.transition = Selectable.Transition.None;
            sl.SetValueWithoutNotify(value);
            return sl;
        }

        static SettingsScreen.Switch Switch(RectTransform row)
        {
            var rt = Box(Node(row, "Switch"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -12, 0, 42, 24);
            var track = Surface(rt, Green, 12f, true);
            var knob = Box(Node(rt, "Knob"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -3, 0, 18, 18);
            knob.anchoredPosition = new Vector2(-8f, 0f);
            var k = knob.gameObject.AddComponent<Image>(); k.sprite = Spr("circle"); k.color = Color.white; k.raycastTarget = false;
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = track; b.transition = Selectable.Transition.None;
            return new SettingsScreen.Switch { button = b, track = track, knob = knob };
        }

        static void WireSwitch(SettingsScreen s, string field, SettingsScreen.Switch sw)
        {
            var so = new SerializedObject(s);
            var p = so.FindProperty(field);
            p.FindPropertyRelative("button").objectReferenceValue = sw.button;
            p.FindPropertyRelative("track").objectReferenceValue = sw.track;
            p.FindPropertyRelative("knob").objectReferenceValue = sw.knob;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// Segmented control (deep well, padding 3, gap 3, 26 high options).
        static Button[] Segment(RectTransform row, params string[] options)
        {
            float w = options.Length == 4 ? 48 : options.Length == 3 ? 40 : 32;
            float total = options.Length * w + (options.Length - 1) * 3 + 6;
            var rt = Box(Node(row, "Segment"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), -12, 0, total, 32);
            Surface(rt, Deep, 9f);
            var h = Row(rt, 3, TextAnchor.MiddleLeft, true, 3, 3);
            h.padding.top = h.padding.bottom = Mathf.RoundToInt(Px(3));
            var buttons = new Button[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                var o = Node(rt, options[i]);
                var img = Surface(o, i == 1 ? Ink : new Color(0, 0, 0, 0), 7f, true);
                Body(Fill(Node(o, "T"), 0, 0, 0, 0), options[i], 11f, i == 1 ? Ground : Dim, TextAlignmentOptions.Center);
                buttons[i] = o.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = img; buttons[i].transition = Selectable.Transition.None;
            }
            return buttons;
        }

        static void DeleteModal(RectTransform root, SettingsScreen s)
        {
            var modal = Fill(Node(root, "DeleteConfirm"), 0, 0, 0, 0);
            Flat(modal, new Color(0.05f, 0.06f, 0.08f, 0.9f), true);
            var panel = Box(Node(modal, "Panel"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 320, 200);
            Surface(panel, Card, RPanel, true);
            Title(TopBand(Node(panel, "Title"), 18, 30, 18, 18), "DELETE MY DATA?", 22f, Ink, TextAlignmentOptions.Center);
            var body = Body(TopBand(Node(panel, "Body"), 54, 60, 22, 22),
                "Coins, gems, guns, outfits and progress on this device are erased. This cannot be undone.", 13f, Dim, TextAlignmentOptions.Center, false);
            body.enableWordWrapping = true;
            var cancel = Box(Node(panel, "Cancel"), new Vector2(0, 0), new Vector2(0, 0), 16, 16, 136, 48);
            Wire(s, "deleteCancel", Button(cancel, "KEEP", Role.Quiet, 18f));
            var yes = Box(Node(panel, "Delete"), new Vector2(1, 0), new Vector2(1, 0), -16, 16, 136, 48);
            Wire(s, "deleteYes", Button(yes, "DELETE", Role.Danger, 18f));
            Wire(s, "deleteConfirm", modal.gameObject);
            modal.gameObject.SetActive(false);
        }
    }
}
