using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// UI chon loadout o Menu scene (task #64). Tu dung toan bo UI bang code:
    /// nut LOADOUT (goc trai-duoi) + panel 3 slot (Pistol/Long A/Long B) + picker loc theo slot.
    /// Editor tool "Tools/ZombieWar/Scenes/Install Loadout Menu UI" gan component + weapons list.
    public class LoadoutMenuController : MonoBehaviour
    {
        [Tooltip("Toan bo WeaponData kha dung (gan boi editor installer).")]
        public List<WeaponData> weapons = new();

        static readonly string[] SlotTitles = { "PISTOL", "LONG A", "LONG B" };

        Font _font;
        GameObject _panel;
        GameObject _picker;
        readonly Text[] _slotValues = new Text[3];

        /// <summary>An nut toggle goc man (HUB dock se goi Open() thay the).</summary>
        public bool hideToggleButton;

        void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = BuildCanvas();
            if (!hideToggleButton) BuildToggleButton(canvas);
            BuildPanel(canvas);
            RefreshSlots();
        }

        /// <summary>Mo panel loadout (goi tu HUB dock).</summary>
        public void Open()
        {
            if (_panel == null) return;
            if (_picker != null) { Destroy(_picker); _picker = null; }
            _panel.SetActive(true);
            RefreshSlots();
        }

        // ===== build =====

        Transform BuildCanvas()
        {
            var go = new GameObject("LoadoutCanvas");
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 60;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1080, 1920);
            sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go.transform;
        }

        void BuildToggleButton(Transform parent)
        {
            var b = MakeButton(parent, "LoadoutButton", "LOADOUT", new Color(0.15f, 0.17f, 0.22f, 0.95f));
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(40f, 40f);
            rt.sizeDelta = new Vector2(300f, 110f);
            b.onClick.AddListener(TogglePanel);
        }

        void BuildPanel(Transform parent)
        {
            _panel = MakePanel(parent, "LoadoutPanel", new Vector2(760f, 900f));

            MakeText(_panel.transform, "Title", "LOADOUT", 52, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 80f));

            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                float y = -180f - i * 200f;

                MakeText(_panel.transform, "SlotTitle" + i, SlotTitles[i], 34, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(0.5f, 1f), new Vector2(-160f, y + 55f), new Vector2(360f, 50f));

                var b = MakeButton(_panel.transform, "SlotBtn" + i, "", new Color(0.10f, 0.12f, 0.16f, 1f));
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(640f, 100f);
                b.onClick.AddListener(() => OpenPicker(slot));
                _slotValues[slot] = b.GetComponentInChildren<Text>();
            }

            var close = MakeButton(_panel.transform, "CloseBtn", "DONG", new Color(0.45f, 0.16f, 0.16f, 1f));
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 90f);
            crt.sizeDelta = new Vector2(300f, 100f);
            close.onClick.AddListener(TogglePanel);

            _panel.SetActive(false);
        }

        // ===== behaviour =====

        void TogglePanel()
        {
            if (_picker != null) { Destroy(_picker); _picker = null; }
            _panel.SetActive(!_panel.activeSelf);
            if (_panel.activeSelf) RefreshSlots();
        }

        void RefreshSlots()
        {
            for (int slot = 0; slot < 3; slot++)
            {
                string id = LoadoutState.GetWeaponId(slot);
                var data = LoadoutState.Resolve(id, weapons);
                var label = _slotValues[slot];
                if (label == null) continue;
                if (data != null)
                {
                    label.text = data.weaponName;
                    label.color = data.TierColor;
                }
                else
                {
                    label.text = slot == 0 ? "(mac dinh)" : "— TRONG —";
                    label.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }
            }
        }

        void OpenPicker(int slot)
        {
            if (_picker != null) Destroy(_picker);

            _picker = MakePanel(_panel.transform.parent, "Picker", new Vector2(700f, 1100f));

            MakeText(_picker.transform, "Title", SlotTitles[slot], 44, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 70f));

            // scroll list
            var viewGo = new GameObject("View", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            var vrt = (RectTransform)viewGo.transform;
            vrt.SetParent(_picker.transform, false);
            vrt.anchorMin = new Vector2(0.5f, 0f);
            vrt.anchorMax = new Vector2(0.5f, 1f);
            vrt.anchoredPosition = new Vector2(0f, -35f);
            vrt.sizeDelta = new Vector2(620f, -260f);
            viewGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var crt2 = (RectTransform)content.transform;
            crt2.SetParent(vrt, false);
            crt2.anchorMin = new Vector2(0f, 1f);
            crt2.anchorMax = new Vector2(1f, 1f);
            crt2.pivot = new Vector2(0.5f, 1f);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewGo.GetComponent<ScrollRect>();
            scroll.content = crt2;
            scroll.horizontal = false;
            scroll.viewport = vrt;

            // option EMPTY cho slot long
            if (slot != 0)
                AddPickerRow(content.transform, "— TRONG —", new Color(0.6f, 0.6f, 0.6f, 1f), () => Pick(slot, ""));

            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                if (w == null) continue;
                bool fits = slot == 0 ? !w.twoHanded : w.twoHanded;
                if (!fits) continue;
                var data = w;
                AddPickerRow(content.transform, data.weaponName, data.TierColor, () => Pick(slot, data.name));
            }

            var cancel = MakeButton(_picker.transform, "CancelBtn", "HUY", new Color(0.45f, 0.16f, 0.16f, 1f));
            var brt = (RectTransform)cancel.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 80f);
            brt.sizeDelta = new Vector2(280f, 90f);
            cancel.onClick.AddListener(() => { Destroy(_picker); _picker = null; });
        }

        void AddPickerRow(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var b = MakeButton(parent, "Row_" + label, label, new Color(0.13f, 0.15f, 0.20f, 1f));
            ((RectTransform)b.transform).sizeDelta = new Vector2(600f, 90f);
            var txt = b.GetComponentInChildren<Text>();
            txt.color = color;
            b.onClick.AddListener(onClick);
        }

        void Pick(int slot, string id)
        {
            LoadoutState.SetWeaponId(slot, id);
            if (_picker != null) { Destroy(_picker); _picker = null; }
            RefreshSlots();
        }

        // ===== ui helpers =====

        GameObject MakePanel(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.97f);
            return go;
        }

        Button MakeButton(Transform parent, string name, string label, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(300f, 100f);
            go.GetComponent<Image>().color = bg;
            var b = go.GetComponent<Button>();
            b.targetGraphic = go.GetComponent<Image>();

            var t = MakeText(go.transform, "Label", label, 36, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            return b;
        }

        Text MakeText(Transform parent, string name, string content, int size, FontStyle style, TextAnchor anchor,
            Vector2 anchorPos, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchorPos;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }
    }
}
