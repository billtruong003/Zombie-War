using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Authoring BỔ SUNG (idempotent, không destructive) cho UI_CostumeScreen.prefab:
    /// thêm hàng chip chọn logical slot (SlotChips — 8 chip bake sẵn, scroll ngang) giữa
    /// PartTabs và PartScroll, wire vào CostumeScreen.slotChips. Chạy lại không nhân đôi.
    /// </summary>
    public static class CostumeSlotChipInstaller
    {
        private const string PrefabPath = "Assets/_Project/UI/Prefabs/Screens/UI_CostumeScreen.prefab";
        private const string EconomyPath = "Assets/_Project/Data/Economy/EconomyConfig.asset";
        private const int ChipCount = 11; // Pro Casual head group is the largest: 11 logical slots

        [MenuItem("ZombieWar/UI/Authoring/Ensure Costume Slot Selector")]
        public static void Ensure()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var screen = root.GetComponent<CostumeScreen>();
                var safe = root.transform.Find("Safe") as RectTransform;
                if (screen == null || safe == null)
                {
                    Debug.LogError("[CostumeSlotChips] Prefab thiếu CostumeScreen/Safe — không sửa gì.");
                    return;
                }

                var tabs = EnsurePartTabs(safe);

                var chipsRoot = safe.Find("SlotChips") as RectTransform;
                bool created = false;
                if (chipsRoot == null)
                {
                    chipsRoot = BuildChipRow(safe);
                    NudgePartScroll(safe);
                    created = true;
                }

                var chipContent = chipsRoot.Find("Content") as RectTransform;
                var chips = chipsRoot.GetComponentsInChildren<CostumeSlotChipView>(true);
                for (int i = chips.Length; i < ChipCount; i++) BuildChip(chipContent, i);
                chips = chipsRoot.GetComponentsInChildren<CostumeSlotChipView>(true);

                var resetBtn = EnsureResetOutfitButton(safe, out bool createdReset);
                var rotator = EnsurePreviewRotator(safe, out bool createdRot);
                var modal = EnsurePurchaseModal(safe);

                var so = new SerializedObject(screen);
                SetArray(so, "partTabs", tabs.buttons);
                SetArray(so, "tabFills", tabs.fills);
                SetArray(so, "tabLabels", tabs.labels);
                var prop = so.FindProperty("slotChips");
                prop.arraySize = ChipCount;
                for (int i = 0; i < ChipCount; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = chips[i];
                var resetProp = so.FindProperty("resetOutfitButton");
                if (resetProp != null) resetProp.objectReferenceValue = resetBtn;
                var rotProp = so.FindProperty("dragRotator");
                if (rotProp != null && rotator != null) rotProp.objectReferenceValue = rotator;
                Set(so, "economy", AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyPath));
                Set(so, "purchaseModalRoot", modal.root);
                Set(so, "purchaseModalIcon", modal.icon);
                Set(so, "purchaseModalTitle", modal.title);
                Set(so, "purchaseModalDescription", modal.description);
                Set(so, "purchaseModalPrice", modal.price);
                Set(so, "purchaseModalConfirm", modal.confirm);
                Set(so, "purchaseModalCancel", modal.cancel);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[CostumeSlotChips] {(created ? "Đã tạo" : "Đã reuse")} SlotChips ({ChipCount} chip), " +
                          $"{(createdReset ? "đã tạo" : "đã reuse")} nút MẶC ĐỊNH, {(createdRot ? "đã tạo" : "đã reuse")} drag rotator + wire references.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private struct PurchaseModalRefs
        {
            public GameObject root; public Image icon; public TMP_Text title, description, price;
            public Button confirm, cancel;
        }

        private struct PartTabRefs
        {
            public Button[] buttons; public Image[] fills; public TMP_Text[] labels;
        }

        private static PartTabRefs EnsurePartTabs(RectTransform safe)
        {
            var old = safe.Find("PartTabs");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var buttons = UIKit.SegmentedTabs(safe, "PartTabs", UIKit.Anch.TC,
                new Vector2(0, -680), new Vector2(900, 88),
                new[] { "ĐẦU", "THÂN", "CHÂN", "BỘ" }, UITheme.Green, 0,
                out var fills, out var labels);
            return new PartTabRefs { buttons = buttons, fills = fills, labels = labels };
        }

        private static PurchaseModalRefs EnsurePurchaseModal(RectTransform safe)
        {
            var old = safe.Find("PurchaseModal");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = UIKit.Rect("PurchaseModal", safe);
            UIKit.Full(root);
            var shade = UIKit.Image(root, "Shade", null, new Color(0, 0, 0, .82f), false);
            UIKit.Full(shade.rectTransform);

            var panel = UIKit.Rect("Panel", root);
            UIKit.Place(panel, UIKit.Anch.C, Vector2.zero, new Vector2(760, 820));
            var bg = UIKit.Image(panel, "Bg", UIKit.Rounded32, UITheme.Surface2, false);
            UIKit.Full(bg.rectTransform);
            var border = UIKit.Image(panel, "Border", UIKit.Frame32, UITheme.Gold, false);
            UIKit.Full(border.rectTransform);

            var title = UIKit.Text(panel, "Title", "XÁC NHẬN MUA", 38, UITheme.TextMain, FontStyles.Bold);
            UIKit.Place(title.rectTransform, UIKit.Anch.TC, new Vector2(0, -42), new Vector2(660, 60));
            var icon = UIKit.Image(panel, "ItemIcon", UIKit.Rounded24, Color.white, false);
            UIKit.Place(icon.rectTransform, UIKit.Anch.TC, new Vector2(0, -250), new Vector2(300, 300));
            icon.preserveAspect = true;
            var desc = UIKit.Text(panel, "Description", "Mua món đồ này?", 28, UITheme.TextDim, FontStyles.Normal);
            UIKit.Place(desc.rectTransform, UIKit.Anch.TC, new Vector2(0, -460), new Vector2(650, 100));

            var cancel = UIKit.BtnGhost(panel, "Cancel", "HỦY", new Vector2(260, 96), UIKit.Anch.BC, new Vector2(-150, 44));
            var confirm = UIKit.BtnPrimary(panel, "Confirm", "MUA", new Vector2(300, 96), UIKit.Anch.BC, new Vector2(150, 44));
            var price = confirm.GetComponentInChildren<TMP_Text>(true);
            root.gameObject.SetActive(false);
            return new PurchaseModalRefs { root = root.gameObject, icon = icon, title = title,
                description = desc, price = price, confirm = confirm, cancel = cancel };
        }

        private static void Set(SerializedObject so, string property, Object value)
        {
            var p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"[CostumeSlotChips] Missing serialized field '{property}'.");
        }

        private static void SetArray<T>(SerializedObject so, string property, T[] values) where T : Object
        {
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogWarning($"[CostumeSlotChips] Missing serialized array '{property}'."); return; }
            p.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < p.arraySize; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static RectTransform BuildChipRow(RectTransform safe)
        {
            var row = UIKit.Rect("SlotChips", safe);
            UIKit.StretchTop(row, 60, -776, 32, 32);
            // đặt ngay dưới PartTabs (index giữ thứ tự hierarchy hợp lý)
            var tabs = safe.Find("PartTabs");
            if (tabs != null) row.SetSiblingIndex(tabs.GetSiblingIndex() + 1);

            row.gameObject.AddComponent<RectMask2D>();
            var scroll = row.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            var content = UIKit.Rect("Content", row);
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.offsetMin = new Vector2(0, 0);
            content.offsetMax = new Vector2(0, 0);
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            for (int i = 0; i < ChipCount; i++)
                BuildChip(content, i);
            return row;
        }

        private static void BuildChip(RectTransform parent, int index)
        {
            // touch target 168×56 (>=132px yêu cầu A11Y theo chiều rộng; hàng chip có hit-area đầy chip)
            var bg = UIKit.Image(parent, "Chip" + index, UIKit.Pill, UITheme.Surface2);
            bg.rectTransform.sizeDelta = new Vector2(168, 56);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;

            var fill = UIKit.Image(bg.rectTransform, "Fill", UIKit.Pill, Color.clear, false);
            UIKit.Full(fill.rectTransform, 3, 3, 3, 3);

            var label = UIKit.Text(bg.rectTransform, "L", "Slot", 26, UITheme.TextDim, FontStyles.Bold);
            UIKit.Full(label.rectTransform);

            var view = bg.gameObject.AddComponent<CostumeSlotChipView>();
            view.button = btn;
            view.fill = fill;
            view.label = label;
        }

        /// Nút runtime "MẶC ĐỊNH" (reset outfit, GIU ownership) — header top-right, ghost style,
        /// touch target 220×88 (>=132px chiều rộng), không đụng cụm Pager/RandomBtn đáy màn.
        private static UnityEngine.UI.Button EnsureResetOutfitButton(RectTransform safe, out bool created)
        {
            var existing = safe.Find("ResetOutfitBtn");
            if (existing != null)
            {
                created = false;
                return existing.GetComponent<UnityEngine.UI.Button>();
            }
            created = true;
            var btn = UIKit.BtnGhost(safe, "ResetOutfitBtn", "MẶC ĐỊNH", new Vector2(220, 88),
                UIKit.Anch.TR, new Vector2(-32, -32));
            return btn;
        }

        /// Drag rotator tren PreviewRT (RawImage) — kéo xoay preview. Target set runtime tu CostumeScreen.
        private static ZombieWar.UI.CostumePreviewDragRotator EnsurePreviewRotator(RectTransform safe, out bool created)
        {
            created = false;
            var previewRT = safe.Find("PreviewCard/PreviewRT");
            if (previewRT == null) { Debug.LogWarning("[CostumeSlotChips] Không thấy PreviewCard/PreviewRT — bỏ qua rotator."); return null; }
            var rot = previewRT.GetComponent<ZombieWar.UI.CostumePreviewDragRotator>();
            if (rot == null) { rot = previewRT.gameObject.AddComponent<ZombieWar.UI.CostumePreviewDragRotator>(); created = true; }
            var img = previewRT.GetComponent<UnityEngine.UI.Graphic>();
            if (img != null) img.raycastTarget = true; // nhận pointer drag
            return rot;
        }

        private static void NudgePartScroll(RectTransform safe)
        {
            // Chip row chiếm 60px + 8px gap: PartScroll (grid) tụt xuống 48px cho đủ chỗ.
            foreach (var name in new[] { "PartScroll", "PartArea" })
            {
                var rt = safe.Find(name) as RectTransform;
                if (rt == null) continue;
                rt.anchoredPosition += new Vector2(0, -48);
                rt.sizeDelta += new Vector2(0, -48);
            }
        }
    }
}
