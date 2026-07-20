using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Màn 03 COSTUME (spec §4.3). Catalog part rất lớn (~nghìn) nên grid là POOL cố định
    /// (cell bake sẵn trong prefab) + paging — runtime chỉ rebind, không Instantiate.
    /// Click part = apply preview-only lên preview character (không persistence — phase sau).
    /// Tab Đầu/Thân/Chân gom các catalog slot theo nhóm cơ thể.
    /// </summary>
    public sealed class CostumeScreen : UIScreen
    {
        [Header("Nav")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button randomButton;

        [Header("Part tabs")]
        [SerializeField] private Button[] partTabs;   // Đầu / Thân / Chân
        [SerializeField] private Image[] tabFills;
        [SerializeField] private TMP_Text[] tabLabels;
        [SerializeField] private Color activeColor = new Color(0.298f, 0.686f, 0.431f);
        [SerializeField] private Color labelDimColor = new Color(0.604f, 0.639f, 0.698f);

        [Header("Data")]
        [SerializeField] private ModularCostumeCatalog catalog;
        [SerializeField] private UIPrototypeCatalog uiCatalog;
        [SerializeField] private MenuCharacterStage previewStage;   // apply preview-only

        [Header("Pool + paging (cells bake sẵn — không Instantiate)")]
        [SerializeField] private CostumeItemCardView[] cells;
        [SerializeField] private Button pagePrevButton;
        [SerializeField] private Button pageNextButton;
        [SerializeField] private TMP_Text pageLabel;

        // Gom catalog slot → 3 tab cơ thể (danh sách slot thực tế của Layer Lab catalog)
        private static readonly string[][] TabSlots =
        {
            new[] { "Hair", "Beard", "Brow", "Mouth", "Eyewear", "Eye", "Earring", "Head" }, // Đầu
            new[] { "Chest", "Hands", "Back", "Body" },                                      // Thân
            new[] { "Legs", "Feet" },                                                        // Chân
        };

        private readonly List<(string slot, ModularCostumeCatalog.PartEntry entry)> _items = new();
        private int _tab, _page;
        private CostumeItemCardView _selected;

        protected override void Awake()
        {
            base.Awake();
            Wire(backButton, () => UIManager.Instance.Pop());
            Wire(randomButton, Randomize);
            if (partTabs != null)
                for (int i = 0; i < partTabs.Length; i++)
                {
                    int idx = i;
                    Wire(partTabs[i], () => SelectPart(idx));
                }
            Wire(pagePrevButton, () => TurnPage(-1));
            Wire(pageNextButton, () => TurnPage(1));
            if (cells != null)
                foreach (var cell in cells)
                {
                    var c = cell;
                    if (c != null && c.button != null)
                        c.button.onClick.AddListener(() => OnCellClicked(c));
                }
        }

        protected override void OnShow() => SelectPart(0);

        public override bool OnEscape() { UIManager.Instance.Pop(); return true; }

        private void SelectPart(int idx)
        {
            _tab = Mathf.Clamp(idx, 0, TabSlots.Length - 1);
            _page = 0;
            if (tabFills != null)
                for (int i = 0; i < tabFills.Length; i++)
                {
                    if (tabFills[i] == null) continue;
                    bool on = i == _tab;
                    tabFills[i].color = on ? activeColor : Color.clear;
                    if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                        tabLabels[i].color = on ? Color.white : labelDimColor;
                }
            RebuildItemList();
            BindPage();
        }

        private void RebuildItemList()
        {
            _items.Clear();
            if (catalog == null) return;
            foreach (var slotName in TabSlots[_tab])
            {
                var slot = catalog.GetSlot(slotName);
                if (slot == null) continue;
                foreach (var part in slot.parts)
                    _items.Add((slot.slot, part));
            }
        }

        private int PageCount => cells == null || cells.Length == 0
            ? 1 : Mathf.Max(1, Mathf.CeilToInt(_items.Count / (float)cells.Length));

        private void TurnPage(int dir)
        {
            _page = Mathf.Clamp(_page + dir, 0, PageCount - 1);
            BindPage();
        }

        private void BindPage()
        {
            if (cells == null) return;
            int start = _page * cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;
                int idx = start + i;
                if (idx < _items.Count)
                {
                    var (slot, entry) = _items[idx];
                    cell.Bind(slot, entry, uiCatalog != null ? uiCatalog.GetCostumeIcon(entry.guid) : null);
                    cell.SetSelected(_selected == cell);
                }
                else cell.Clear();
            }
            if (pageLabel != null) pageLabel.text = $"{_page + 1}/{PageCount}";
            if (pagePrevButton != null) pagePrevButton.interactable = _page > 0;
            if (pageNextButton != null) pageNextButton.interactable = _page < PageCount - 1;
        }

        private void OnCellClicked(CostumeItemCardView cell)
        {
            if (cell == null || string.IsNullOrEmpty(cell.partGuid)) return;
            if (_selected != null) _selected.SetSelected(false);
            _selected = cell;
            cell.SetSelected(true);
            ApplyPreview(cell.slotName, cell.partName);
        }

        private void Randomize()
        {
            if (_items.Count == 0) return;
            var (slot, entry) = _items[Random.Range(0, _items.Count)];
            ApplyPreview(slot, entry.name);
        }

        /// <summary>Preview-only: mặc part lên preview character. Không lưu (equipment phase sau).</summary>
        private void ApplyPreview(string slot, string partName)
        {
            var applier = previewStage != null ? previewStage.ModularApplier : null;
            if (applier != null) applier.Apply(slot, partName);
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
