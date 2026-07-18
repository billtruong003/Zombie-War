using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// Costume picker ngoai MenuScene (task #65).
    /// Nut COSTUME -> panel liet ke cac slot (Hair, Face, ...) -> bam slot mo picker
    /// parts (Default + toan bo part cua slot). Chon part: apply ngay len preview
    /// character (CharacterModularApplier) + save vao LoadoutState (guid).
    /// UI build bang code giong LoadoutMenuController de khong phu thuoc prefab UI.
    public class CostumeMenuController : MonoBehaviour
    {
        public ModularCostumeCatalog catalog;
        public CharacterModularApplier previewApplier;

        private Canvas _canvas;
        private GameObject _panel;                 // panel liet ke slot
        private GameObject _picker;                // overlay chon part
        private readonly List<Button> _slotButtons = new();
        private readonly List<Text> _slotLabels = new();

        private void Start()
        {
            if (catalog == null)
            {
                Debug.LogError("[CostumeMenu] catalog chua gan", this);
                enabled = false;
                return;
            }

            if (previewApplier != null) previewApplier.ApplySavedParts();

            BuildCanvas();
            BuildToggleButton();
            BuildPanel();
            RefreshSlotLabels();
        }

        // ================= build =================

        private void BuildCanvas()
        {
            var go = new GameObject("CostumeCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60; // tren LoadoutCanvas (50)
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }

        private void BuildToggleButton()
        {
            var btn = MakeButton(_canvas.transform, "CostumeBtn", "COSTUME", new Color(0.55f, 0.35f, 0.75f, 0.95f));
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-30f, 40f);
            rt.sizeDelta = new Vector2(300f, 110f);
            btn.onClick.AddListener(TogglePanel);
        }

        private void BuildPanel()
        {
            _panel = MakePanel(_canvas.transform, "CostumePanel", new Vector2(760f, 1300f));

            MakeText(_panel.transform, "Title", "COSTUME", 52, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, -50f), new Vector2(700f, 80f));

            var view = MakeScroll(_panel.transform, out var content, new Vector2(0f, -60f), new Vector2(700f, 1080f));

            _slotButtons.Clear();
            _slotLabels.Clear();
            foreach (var slot in catalog.slots)
            {
                var s = slot; // capture
                var b = MakeButton(content, "Slot_" + s.slot, s.slot, new Color(0.22f, 0.24f, 0.30f, 0.95f));
                ((RectTransform)b.transform).sizeDelta = new Vector2(680f, 96f);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 96f;
                le.preferredWidth = 680f;
                b.onClick.AddListener(() => OpenPicker(s));
                _slotButtons.Add(b);
                _slotLabels.Add(b.GetComponentInChildren<Text>());
            }

            var close = MakeButton(_panel.transform, "CloseBtn", "X", new Color(0.6f, 0.2f, 0.2f, 0.95f));
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-14f, -14f);
            crt.sizeDelta = new Vector2(80f, 80f);
            close.onClick.AddListener(TogglePanel);

            _panel.SetActive(false);
            if (previewApplier != null) previewApplier.gameObject.SetActive(false);
        }

        private void TogglePanel()
        {
            if (_picker != null) { Destroy(_picker); _picker = null; }
            _panel.SetActive(!_panel.activeSelf);
            if (previewApplier != null) previewApplier.gameObject.SetActive(_panel.activeSelf);
            if (_panel.activeSelf) RefreshSlotLabels();
        }

        // ================= picker =================

        private void OpenPicker(ModularCostumeCatalog.Slot slot)
        {
            if (_picker != null) Destroy(_picker);

            _picker = MakePanel(_canvas.transform, "PartPicker", new Vector2(820f, 1500f));

            MakeText(_picker.transform, "Title", slot.slot.ToUpperInvariant(), 48, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(0f, -46f), new Vector2(760f, 70f));

            MakeScroll(_picker.transform, out var content, new Vector2(0f, -60f), new Vector2(760f, 1290f));

            // Default = thao part (khong cho voi base body — luon phai co body).
            if (!slot.isBaseBody)
                AddPartButton(content, "— Default —", () => Pick(slot.slot, null, default, false));

            foreach (var part in slot.parts)
            {
                var p = part; // capture
                AddPartButton(content, p.name, () => Pick(slot.slot, p.guid, p, true));
            }

            var close = MakeButton(_picker.transform, "CloseBtn", "X", new Color(0.6f, 0.2f, 0.2f, 0.95f));
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-14f, -14f);
            crt.sizeDelta = new Vector2(80f, 80f);
            close.onClick.AddListener(() => { Destroy(_picker); _picker = null; });
        }

        private void AddPartButton(Transform content, string label, UnityEngine.Events.UnityAction onClick)
        {
            var b = MakeButton(content, "Part_" + label, label, new Color(0.20f, 0.28f, 0.24f, 0.95f));
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 84f;
            le.preferredWidth = 740f;
            b.GetComponentInChildren<Text>().fontSize = 30;
            b.onClick.AddListener(onClick);
        }

        private void Pick(string slotName, string guid, ModularCostumeCatalog.PartEntry entry, bool equip)
        {
            LoadoutState.SetPart(slotName, guid);

            if (previewApplier != null)
            {
                if (equip) previewApplier.Apply(slotName, entry);
                else previewApplier.Clear(slotName);
            }

            RefreshSlotLabels();
            if (_picker != null) { Destroy(_picker); _picker = null; }
        }

        private void RefreshSlotLabels()
        {
            for (int i = 0; i < catalog.slots.Count && i < _slotLabels.Count; i++)
            {
                var slot = catalog.slots[i];
                string guid = LoadoutState.GetPart(slot.slot);
                string current = "Default";
                if (!string.IsNullOrEmpty(guid))
                {
                    foreach (var p in slot.parts)
                        if (p.guid == guid) { current = p.name; break; }
                }
                _slotLabels[i].text = $"{slot.slot}   <{current}>";
            }
        }

        // ================= UI helpers =================

        private static Font UiFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static GameObject MakePanel(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.08f, 0.09f, 0.12f, 0.97f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            return go;
        }

        private static Button MakeButton(Transform parent, string name, string label, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(300f, 96f);

            var txt = MakeText(go.transform, "Label", label, 34, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero);
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            return btn;
        }

        private static Text MakeText(Transform parent, string name, string content, int size,
            FontStyle style, TextAnchor anchor, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UiFont();
            t.text = content;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            if (dim != Vector2.zero)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = dim;
            }
            return t;
        }

        private static GameObject MakeScroll(Transform parent, out Transform content, Vector2 pos, Vector2 size)
        {
            var viewGo = new GameObject("Scroll", typeof(RectTransform));
            viewGo.transform.SetParent(parent, false);
            var vrt = (RectTransform)viewGo.transform;
            vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 1f);
            vrt.pivot = new Vector2(0.5f, 1f);
            vrt.anchoredPosition = new Vector2(pos.x, pos.y - 80f);
            vrt.sizeDelta = size;

            var vimg = viewGo.AddComponent<Image>();
            vimg.color = new Color(0f, 0f, 0f, 0.25f);
            viewGo.AddComponent<RectMask2D>();

            var scroll = viewGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewGo.transform, false);
            var crt = contentGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(size.x, 0f);

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = crt;
            content = contentGo.transform;
            return viewGo;
        }
    }
}
