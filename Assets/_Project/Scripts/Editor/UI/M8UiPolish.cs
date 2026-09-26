using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// M8-C2 visual pass (owner-approved plan, 2026-09-26): brings every screen to the approved
    /// mockup's look on top of the M8 layout (M8UiLayout) and the slice rules (UiKitApply).
    /// - One type family: every LiberationSans label moves to Cairo Line Black (the Hub's face).
    /// - Buttons with a darker bottom lip (face + lip), in the mockup's yellow / green / red / card.
    /// - Modals on a dark scrim, as cards; non-working placeholders (language, restore) hidden.
    /// - Weapon cards get a solid rarity tile behind the icon (the M8 owned / locked icons).
    /// Idempotent: every step sets absolute values, so re-running re-applies the same look.
    /// </summary>
    public static class M8UiPolish
    {
        const string ScreensDir = "Assets/_Project/UI/Prefabs/Screens/";
        const string SpriteDir = "Assets/_Project/UI/Sprites/";
        const string DisplayFontPath = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Fonts/Cairo_Line_Black SDF_Light.asset";
        const float Lip = 14f;
        const float ButtonRadius = 0.6f;   // rounded_24 at 0.6 → ~40 px corners, the mockup's 14-16 css px
        const float CardRadius = 0.75f;    // rounded_32 at 0.75 → ~43 px corners

        [MenuItem("ZombieWar/UI/M8/Polish All Screens (visual pass)")]
        public static void PolishAll()
        {
            foreach (var p in UiAudit.ScreenPrefabs())
                Debug.Log($"[M8 Polish] {System.IO.Path.GetFileName(p)}: {Polish(p)}");
        }

        public static string Polish(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var log = new List<string>();
            try
            {
                log.Add($"fonts {UnifyFonts(root)}");
                switch (System.IO.Path.GetFileNameWithoutExtension(path))
                {
                    case "UI_Hud": PolishHud(root, log); break;
                    case "UI_LoadoutScreen": PolishLoadout(root, log); break;
                    case "UI_ShopScreen": PolishShop(root, log); break;
                }
                log.Add($"press-feel {AddPressFeel(root)}");
                log.Add($"slice-fit +{UiKitApply.AddSliceFit(root)}");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return string.Join(", ", log);
        }

        // ─────────────────────────────────────────────────────────── shared

        static TMP_FontAsset _display;
        static TMP_FontAsset Display => _display != null ? _display : (_display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath));

        static Sprite Spr(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");

        static int UnifyFonts(GameObject root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.font == null || !t.font.name.StartsWith("LiberationSans")) continue;
                t.font = Display;
                t.fontSharedMaterial = Display.material;
                t.fontStyle &= ~FontStyles.Bold;
                n++;
            }
            return n;
        }

        static void Style(TMP_Text t, float size, Color color)
        {
            if (t == null) return;
            t.font = Display;
            t.fontSharedMaterial = Display.material;
            t.fontSize = size;
            t.color = color;
        }

        static Image Rounded(Transform t, string sprite, Color color, float radius)
        {
            if (t == null) return null;
            var img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            img.sprite = Spr(sprite);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.pixelsPerUnitMultiplier = radius;
            var fit = t.GetComponent<UISliceFit>() ?? t.gameObject.AddComponent<UISliceFit>();
            fit.BaseMultiplier = radius;
            return img;
        }

        /// A button as face over a darker lip: the button's own image is the lip, child "Face" sits
        /// Lip px higher. Labels live inside the face so they move with it.
        static void LipButton(Transform btn, Color face, Color lip, Color? label = null, float? labelSize = null)
        {
            if (btn == null) return;
            Rounded(btn, "rounded_24", lip, ButtonRadius);
            var faceT = btn.Find("Face");
            if (faceT == null)
            {
                var go = new GameObject("Face", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = btn.gameObject.layer;
                faceT = go.transform;
                faceT.SetParent(btn, false);
                // Everything that was on the button (labels, icons) now rides on the face.
                for (int i = btn.childCount - 1; i >= 0; i--)
                {
                    var c = btn.GetChild(i);
                    if (c != faceT) c.SetParent(faceT, false);
                }
            }
            faceT.SetAsFirstSibling();
            var frt = (RectTransform)faceT;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(0f, Lip); frt.offsetMax = Vector2.zero;
            var fi = Rounded(faceT, "rounded_24", face, ButtonRadius);
            fi.raycastTarget = false;
            var b = btn.GetComponent<Button>();
            if (b != null) { b.targetGraphic = btn.GetComponent<Image>(); b.transition = Selectable.Transition.None; }
            foreach (var t in faceT.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = Display; t.fontSharedMaterial = Display.material;
                if (label.HasValue) t.color = label.Value;
                if (labelSize.HasValue) t.fontSize = labelSize.Value;
            }
        }

        // Linear colour space: UI alpha blends in linear light, so the mockup's 0.78 sRGB scrim needs
        // ~0.9 here to look as dark on screen.
        static void Scrim(Transform dim, float alpha = 0.9f)
        {
            var img = dim != null ? dim.GetComponent<Image>() : null;
            if (img == null) return;
            var c = UITheme.M8Scrim; c.a = alpha; img.color = c;
        }

        static void Card(Transform t, Color? color = null) => Rounded(t, "rounded_32", color ?? UITheme.M8Card, CardRadius);

        static void SetRect(Transform t, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = t as RectTransform; if (rt == null) return;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        /// Every button and toggle answers a tap: sound by role, lip buttons sink while held.
        public static int AddPressFeel(GameObject root)
        {
            int n = 0;
            foreach (var sel in root.GetComponentsInChildren<Selectable>(true))
            {
                if (!(sel is Button) && !(sel is Toggle)) continue;
                var feel = sel.GetComponent<UIPressFeel>() ?? sel.gameObject.AddComponent<UIPressFeel>();
                var face = sel.transform.Find("Face") as RectTransform;
                bool lip = face != null && face.offsetMin.y > 1f;
                feel.Configure(SoundFor(sel), lip ? face : null, lip ? Mathf.Min(10f, face.offsetMin.y) : 0f);
                n++;
            }
            return n;
        }

        static UIPressFeel.Sound SoundFor(Selectable sel)
        {
            string n = sel.name.ToLowerInvariant();
            // Purchases and level-up picks have their own, richer sounds.
            if (n.Contains("confirm") && sel.GetComponentInParent<ShopScreen>(true) != null) return UIPressFeel.Sound.None;
            if (n.StartsWith("perk")) return UIPressFeel.Sound.None;
            if (n.Contains("back") || n.Contains("close") || n.Contains("cancel") || n == "no" || n.Contains("home")
                || n.Contains("skip") || n == "dim") return UIPressFeel.Sound.Back;
            if (n.Contains("play") || n.Contains("replay") || n.Contains("resume") || n.Contains("claim")
                || n == "yes" || n.Contains("confirm") || n.Contains("equip")) return UIPressFeel.Sound.Confirm;
            return UIPressFeel.Sound.Tap;
        }

        /// Menu.unity's own buttons (nav bar, header) — objects that are not part of a screen prefab.
        [MenuItem("ZombieWar/UI/M8/Add Press Feel To Menu Scene Buttons")]
        public static void PressFeelMenuScene()
        {
            const string menu = "Assets/_Project/Scenes/Menu.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(menu, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            int n = 0;
            try
            {
                foreach (var go in scene.GetRootGameObjects())
                    foreach (var sel in go.GetComponentsInChildren<Selectable>(true))
                    {
                        if (!(sel is Button) && !(sel is Toggle)) continue;
                        if (PrefabUtility.IsPartOfPrefabInstance(sel)) continue;   // screens get it from their prefab
                        var feel = sel.GetComponent<UIPressFeel>() ?? sel.gameObject.AddComponent<UIPressFeel>();
                        feel.Configure(SoundFor(sel), null, 0f);
                        n++;
                    }
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
            Debug.Log($"[M8 Polish] Menu scene buttons with press feel: {n}");
        }

        // ─────────────────────────────────────────────────────────── HUD + overlays

        static void PolishHud(GameObject root, List<string> log)
        {
            var o = root.transform.Find("Overlays");

            // Pause
            var pause = o.Find("PauseModal");
            Scrim(pause.Find("Dim"));
            var pp = pause.Find("Panel");
            Card(pp);
            Style(pp.Find("Title")?.GetComponent<TMP_Text>(), 64, UITheme.M8Ink);
            Rounded(pp.Find("GearBtn"), "rounded_24", UITheme.M8Deep, ButtonRadius);
            LipButton(pp.Find("ResumeBtn"), UITheme.M8Green, UITheme.M8GreenLip, UITheme.M8OnGreen, 56);
            LipButton(pp.Find("ExitBtn"), UITheme.M8Red, UITheme.M8RedLip, Color.white, 52);
            foreach (var row in new[] { "SoundRow", "VibRow" })
                Style(pp.Find(row + "/Label")?.GetComponent<TMP_Text>(), 40, UITheme.M8Ink);

            // Confirm quit: staying is the safe, primary choice.
            var confirm = o.Find("ConfirmModal");
            Scrim(confirm.Find("Dim"));
            var cp = confirm.Find("Panel");
            Card(cp);
            Style(cp.Find("Title")?.GetComponent<TMP_Text>(), 40, UITheme.M8Ink);
            LipButton(cp.Find("Yes"), UITheme.M8Red, UITheme.M8RedLip, Color.white, 46);
            LipButton(cp.Find("No"), UITheme.M8Green, UITheme.M8GreenLip, UITheme.M8OnGreen, 46);

            // Settings: language and restore purchases do nothing yet, so they are not shown.
            var settings = o.Find("SettingsModal");
            Scrim(settings.Find("Dim"));
            var sp = settings.Find("Panel");
            Card(sp);
            ((RectTransform)sp).sizeDelta = new Vector2(840, 500);
            Style(sp.Find("Title")?.GetComponent<TMP_Text>(), 64, UITheme.M8Ink);
            sp.Find("LangRow")?.gameObject.SetActive(false);
            sp.Find("Restore")?.gameObject.SetActive(false);
            foreach (var row in new[] { "MusicRow", "SfxRow", "VibRow" })
                Style(sp.Find(row + "/Label")?.GetComponent<TMP_Text>(), 40, UITheme.M8Ink);
            foreach (var slider in sp.GetComponentsInChildren<Slider>(true)) StyleSlider(slider);
            CloseButton(sp, settings.gameObject);

            // Revive (presentation only today)
            var revive = o.Find("ReviveModal");
            Scrim(revive.Find("Dim"));
            var rp = revive.Find("Panel");
            Card(rp);
            Style(rp.Find("Title")?.GetComponent<TMP_Text>(), 64, UITheme.M8Ink);
            LipButton(rp.Find("AdBtn"), UITheme.M8Green, UITheme.M8GreenLip, UITheme.M8OnGreen, 52);
            LipButton(rp.Find("SkipBtn"), UITheme.M8Card, UITheme.M8CardLip, UITheme.M8InkDim, 38);

            // Level up: the run behind it goes dark, cards sit on the mockup's card colour.
            var lu = o.Find("LevelUpOverlay");
            Scrim(lu.Find("Dim"), 0.93f);
            for (int i = 0; i < 3; i++)
            {
                var perk = lu.Find("Perk" + i);
                if (perk == null) continue;
                Card(perk.Find("Bg"));
                Rounded(perk.Find("Icon"), "rounded_24", UITheme.M8Deep, ButtonRadius);
                Style(perk.Find("Desc")?.GetComponent<TMP_Text>(), 30, Hex("D9DDE6"));
                foreach (Transform pip in perk.Find("Pips")) pip.GetComponent<Image>().color = UITheme.M8Deep;
            }
            BuildStrip(lu.Find("Build/Items"), false);

            // Result
            var res = o.Find("GameOverScreen");
            res.Find("Bg").GetComponent<Image>().color = UITheme.M8Ground;
            foreach (var s in new[] { "Stat0", "Stat1", "Stat2" }) Card(res.Find("Stats/" + s));
            var pay = res.Find("PayoutCard");
            Card(pay.Find("Bg"));
            foreach (Transform c in pay)
            {
                var t = c.GetComponent<TMP_Text>();
                if (t == null) continue;
                bool value = c.name.EndsWith("V");
                bool total = c.name.StartsWith("Total");
                Style(t, total ? (value ? 64 : 52) : 36, value ? t.color : (total ? UITheme.M8Ink : Hex("C9CFDB")));
            }
            Rounded(res.Find("RecordPill"), "pill", UITheme.M8Card, 1f);
            LipButton(res.Find("ReplayBtn"), UITheme.M8Yellow, UITheme.M8YellowLip, UITheme.M8OnYellow, 64);
            LipButton(res.Find("HomeBtn"), UITheme.M8Card, UITheme.M8CardLip, UITheme.M8Ink, 42);
            BuildStrip(res.Find("Build/Items"), true);

            // FTUE skip + resume countdown
            LipButton(o.Find("FtueOverlay/SkipBtn"), UITheme.M8Card, UITheme.M8CardLip, UITheme.M8InkDim, 38);

            // Skill bar: the rank sits on the slot's corner like the mockup, not across its bottom.
            foreach (var view in root.GetComponentsInChildren<SkillSlotView>(true))
            {
                var rank = view.transform.Find("Content/Rank");
                if (rank != null) SetRect(rank, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(10, -10), new Vector2(52, 36));
                view.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.78f);
            }
            log.Add("hud");
        }

        /// Build strip items: card tiles, rank badge tucked on the corner.
        static void BuildStrip(Transform items, bool withRank)
        {
            if (items == null) return;
            foreach (Transform it in items)
            {
                Rounded(it, "rounded_24", UITheme.M8Card, ButtonRadius);
                var rank = it.Find("Rank");
                if (withRank && rank != null)
                    SetRect(rank, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(8, -8), new Vector2(40, 30));
            }
        }

        static void StyleSlider(Slider s)
        {
            var bg = s.transform.Find("Bg");
            if (bg != null) { SetRect(bg, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 20)); bg.GetComponent<Image>().color = UITheme.M8Deep; }
            var fillArea = s.transform.Find("Fill Area") as RectTransform;
            if (fillArea != null) fillArea.sizeDelta = new Vector2(fillArea.sizeDelta.x, 20);
            var graphic = s.fillRect != null ? s.fillRect.Find("Graphic") : null;
            if (graphic != null) graphic.GetComponent<Image>().color = UITheme.M8Green;
            if (s.handleRect != null)
            {
                // The slider drives x anchors only; a y-stretched handle turns the knob into a capsule.
                s.handleRect.anchorMin = new Vector2(s.handleRect.anchorMin.x, 0.5f);
                s.handleRect.anchorMax = new Vector2(s.handleRect.anchorMax.x, 0.5f);
                s.handleRect.sizeDelta = new Vector2(52, 52);
                var h = s.handleRect.GetComponent<Image>();
                if (h != null) { h.sprite = Spr("circle"); h.type = Image.Type.Simple; h.color = UITheme.M8Ink; }
            }
        }

        /// A visible close button in the panel corner; hides the modal (same as tapping the scrim).
        static void CloseButton(Transform panel, GameObject modal)
        {
            var t = panel.Find("CloseBtn");
            if (t == null)
            {
                var go = new GameObject("CloseBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.layer = panel.gameObject.layer;
                t = go.transform; t.SetParent(panel, false);
                var lab = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                lab.transform.SetParent(t, false);
                var lrt = (RectTransform)lab.transform; lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                var tx = lab.GetComponent<TextMeshProUGUI>(); tx.text = "X"; tx.alignment = TextAlignmentOptions.Center; tx.raycastTarget = false;
                UnityEventTools.AddBoolPersistentListener(go.GetComponent<Button>().onClick, modal.SetActive, false);
                go.AddComponent<UIFxPress>();
            }
            SetRect(t, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24, -24), new Vector2(84, 84));
            Rounded(t, "rounded_24", UITheme.M8Deep, ButtonRadius);
            Style(t.Find("Label")?.GetComponent<TMP_Text>(), 44, UITheme.M8InkDim);
        }

        // ─────────────────────────────────────────────────────────── weapon cards

        /// Solid rarity tile behind a weapon card's icon (coloured at runtime from WeaponData.TileColor).
        static void CardTile(WeaponItemCardView card, float bottomReserve)
        {
            var icon = card.icon != null ? card.icon.transform : null;
            if (icon == null) return;
            var tileT = card.transform.Find("Tile");
            if (tileT == null)
            {
                var go = new GameObject("Tile", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = card.gameObject.layer;
                tileT = go.transform; tileT.SetParent(card.transform, false);
            }
            // Tile directly under the icon; written so a second run keeps that order.
            tileT.SetSiblingIndex(icon.GetSiblingIndex());
            icon.SetSiblingIndex(tileT.GetSiblingIndex() + 1);
            var trt = (RectTransform)tileT;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
            trt.offsetMin = new Vector2(14, bottomReserve); trt.offsetMax = new Vector2(-14, -14);
            var ti = Rounded(tileT, "rounded_24", UITheme.M8Deep, ButtonRadius);
            ti.raycastTarget = false;
            card.tile = ti;

            var irt = (RectTransform)icon;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.pivot = new Vector2(0.5f, 0.5f);
            irt.offsetMin = new Vector2(22, bottomReserve + 8); irt.offsetMax = new Vector2(-22, -22);
            card.icon.preserveAspect = true;
            card.icon.raycastTarget = false;

            var bg = card.transform.Find("Bg")?.GetComponent<Image>();
            if (bg != null) { bg.color = UITheme.M8Card; }
            var lockOv = card.lockOverlay != null ? card.lockOverlay.GetComponent<Image>() : null;
            if (lockOv != null) lockOv.color = new Color(0.047f, 0.055f, 0.078f, 0.35f);   // the silhouette already says "locked"
            // The padlock sits on the tile's corner instead of over the gun.
            if (card.lockOverlay != null && card.lockOverlay.transform.childCount > 0)
                SetRect(card.lockOverlay.transform.GetChild(0), Vector2.one, Vector2.one, Vector2.one, new Vector2(-20, -20), new Vector2(56, 56));
        }

        static void PolishLoadout(GameObject root, List<string> log)
        {
            var safe = root.transform.Find("Safe");
            int n = 0;
            foreach (var card in root.GetComponentsInChildren<WeaponItemCardView>(true)) { CardTile(card, 64); n++; }

            // Details card: plain card with a subtle edge (the rarity shows on the tile and the name).
            var info = safe.Find("InfoPanel");
            Card(info.Find("Bg"));
            var border = info.Find("Border")?.GetComponent<Image>();
            if (border != null) border.color = UITheme.M8Edge;
            info.Find("Glow")?.gameObject.SetActive(false);
            Rounded(info.Find("HeroBackdrop"), "rounded_32", UITheme.M8Deep, CardRadius);
            var heroIcon = info.Find("HeroBackdrop/HeroWeaponIcon") as RectTransform;
            if (heroIcon != null) { heroIcon.offsetMin = new Vector2(16, 16); heroIcon.offsetMax = new Vector2(-16, -16); }
            foreach (Transform chip in info.Find("Signatures/Row"))
                Rounded(chip, "rounded_24", Hex("3A2F22"), ButtonRadius);

            // ARSENAL heading sits above the grid instead of on the details card's edge.
            var kho = safe.Find("KhoArea");
            var label = kho.Find("KhoLabel");
            SetRect(label, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, 0), new Vector2(500, 64));
            var lt = label.GetComponent<TMP_Text>(); if (lt != null) { lt.color = UITheme.M8Ink; lt.alignment = TextAlignmentOptions.MidlineLeft; }
            var scroll = kho.Find("KhoScroll") as RectTransform;
            if (scroll != null) { scroll.offsetMax = new Vector2(scroll.offsetMax.x, -72); }
            log.Add($"loadout tiles {n}");
        }

        static void PolishShop(GameObject root, List<string> log)
        {
            int n = 0;
            foreach (var card in root.GetComponentsInChildren<WeaponItemCardView>(true))
            {
                bool featured = card.name.StartsWith("Featured_");
                CardTile(card, featured ? 104 : 104);
                n++;
            }
            // Purchase confirm: card panel, the item on a tile, BUY / CANCEL lip buttons.
            var modal = root.transform.Find("PurchaseModal");
            if (modal != null)
            {
                var scrim = modal.GetComponent<Image>();
                if (scrim != null) { var c = UITheme.M8Scrim; c.a = 0.9f; scrim.color = c; scrim.sprite = null; }
                var panel = modal.Find("Panel");
                ((RectTransform)panel).sizeDelta = new Vector2(820, 700);
                Card(panel.Find("Bg"));
                var edge = panel.Find("Border")?.GetComponent<Image>(); if (edge != null) edge.color = UITheme.M8Edge;
                panel.Find("Glow")?.gameObject.SetActive(false);
                var icon = panel.Find("Icon");
                var tileT = panel.Find("IconTile");
                if (tileT == null)
                {
                    var go = new GameObject("IconTile", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.layer = panel.gameObject.layer; tileT = go.transform; tileT.SetParent(panel, false);
                }
                tileT.SetSiblingIndex(icon.GetSiblingIndex());
                icon.SetSiblingIndex(tileT.GetSiblingIndex() + 1);
                SetRect(tileT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(420, 300));
                Rounded(tileT, "rounded_32", UITheme.M8Deep, CardRadius).raycastTarget = false;
                SetRect(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(380, 260));
                var iconImg = icon.GetComponent<Image>(); iconImg.preserveAspect = true; iconImg.raycastTarget = false;
                Style(panel.Find("Title")?.GetComponent<TMP_Text>(), 56, UITheme.M8Ink);
                var price = panel.Find("Price"); if (price != null) SetRect(price, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(560, 70));
                Style(price?.GetComponent<TMP_Text>(), 48, UITheme.M8Yellow);
                LipButton(panel.Find("Cancel"), UITheme.M8Card, UITheme.M8CardLip, UITheme.M8Ink, 40);
                LipButton(panel.Find("Confirm"), UITheme.M8Green, UITheme.M8GreenLip, UITheme.M8OnGreen, 44);
                var buy = panel.Find("Confirm/Face/Label")?.GetComponent<TMP_Text>(); if (buy != null) buy.text = "BUY";
                var screen = root.GetComponent<ShopScreen>();
                var so = new SerializedObject(screen);
                so.FindProperty("purchaseIconTile").objectReferenceValue = tileT.GetComponent<Image>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Add($"shop tiles {n}");
        }

        static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.magenta;
    }
}
