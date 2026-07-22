using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Màn 03 COSTUME. Data-driven từ catalog đang hoạt động:
    /// - Casual (compositeBody=false): tab + slot dựng từ catalog.slotDefinitions (nhóm Head/Body/Legs),
    ///   card dùng icon thật per-item (PartEntry.icon), identity = stable itemId. Slot bắt buộc chỉ có
    ///   card part (không "Không mang"); slot optional có "Không mang". Chưa sở hữu = khoá thật.
    ///   Face base và Body_1..4 là renderer infrastructure, không xuất hiện trong UI/ownership/commerce.
    ///   Body variant tự đổi theo trạng thái Găng tay/Giày để tránh mesh overlap.
    /// - Fantasy (compositeBody=true): giữ nguyên presentation cũ (composite Body màu/tai, essential/
    ///   optional theo static array) để rollback — gated sau _casual.
    ///
    /// Ownership/equip qua PlayerProfile (atomic). Refresh theo CostumeChanged. Randomize/Reset = 1 batch.
    /// </summary>
    public sealed class CostumeScreen : UIScreen
    {
        [Header("Nav")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button randomButton;
        [SerializeField] private Button resetOutfitButton; // "MẶC ĐỊNH"

        [Header("Primary tabs (nhóm cơ thể)")]
        [SerializeField] private Button[] partTabs;
        [SerializeField] private Image[] tabFills;
        [SerializeField] private TMP_Text[] tabLabels;
        [SerializeField] private Color activeColor = new Color(0.298f, 0.686f, 0.431f);
        [SerializeField] private Color labelDimColor = new Color(0.604f, 0.639f, 0.698f);

        [Header("Logical slot chips (8 bake sẵn)")]
        [SerializeField] private CostumeSlotChipView[] slotChips;

        [Header("Data")]
        [SerializeField] private ModularCostumeCatalog catalog;
        [SerializeField] private UIPrototypeCatalog uiCatalog;
        [SerializeField] private EconomyConfig economy; // Shared authoritative commerce (Casual sets/items + Fantasy rollback).
        [SerializeField] private MenuCharacterStage previewStage;
        [SerializeField] private CostumePreviewDragRotator dragRotator;

        [Header("Pool + paging")]
        [SerializeField] private CostumeItemCardView[] cells;
        [SerializeField] private Button pagePrevButton;
        [SerializeField] private Button pageNextButton;
        [SerializeField] private TMP_Text pageLabel;

        [Header("Purchase confirmation (authored in prefab)")]
        [SerializeField] private GameObject purchaseModalRoot;
        [SerializeField] private Image purchaseModalIcon;
        [SerializeField] private TMP_Text purchaseModalTitle;
        [SerializeField] private TMP_Text purchaseModalDescription;
        [SerializeField] private TMP_Text purchaseModalPrice;
        [SerializeField] private Button purchaseModalConfirm;
        [SerializeField] private Button purchaseModalCancel;

        // ---- Fantasy fallback presentation (compositeBody=true) ----
        private static readonly string[][] FantasyTabSlots =
        {
            new[] { "Hair", "Beard", "Brow", "Mouth", "Eyewear", "Eye", "Earring", "Head" },
            new[] { "Chest", "Hands", "Back", "Body" },
            new[] { "Legs", "Feet" },
        };

        private static readonly Dictionary<string, string> FantasySlotLabels = new()
        {
            { "Hair", "Tóc" }, { "Beard", "Râu" }, { "Brow", "Mày" }, { "Mouth", "Miệng" },
            { "Eyewear", "Kính" }, { "Eye", "Mắt" }, { "Earring", "Khuyên" }, { "Head", "Mũ" },
            { "Chest", "Áo" }, { "Hands", "Găng" }, { "Back", "Lưng" }, { "Body", "Thân" },
            { "Legs", "Quần" }, { "Feet", "Giày" },
        };

        private static readonly Color LockedTint = new(0.45f, 0.45f, 0.45f, 1f);

        private const string SetsSlot = "__sets";
        private enum OptKind { Part, Set, Default, None, BodyColor, BodyEar }
        private struct Opt { public OptKind kind; public string data; }

        private readonly List<Opt> _options = new();
        private readonly Opt[] _cellOpt = new Opt[64];
        private int _tab, _slotIndex, _page;
        private string _pendingBuyId;
        private string _modalItemId;
        private EconomyConfig.CostumeSetEntry _modalSet;

        // Active slot model (built from the catalog at OnShow).
        private string[][] _tabSlots = FantasyTabSlots;
        private bool _casual;

        private string CurrentSlot => _tabSlots[Mathf.Clamp(_tab, 0, _tabSlots.Length - 1)]
            [Mathf.Clamp(_slotIndex, 0, _tabSlots[_tab].Length - 1)];

        protected override void Awake()
        {
            base.Awake();
            Wire(backButton, () => UIManager.Instance.Pop());
            Wire(randomButton, RandomizeOutfit);
            Wire(resetOutfitButton, ResetOutfit);
            if (partTabs != null)
                for (int i = 0; i < partTabs.Length; i++)
                {
                    int idx = i;
                    Wire(partTabs[i], () => SelectTab(idx));
                }
            if (slotChips != null)
                for (int i = 0; i < slotChips.Length; i++)
                {
                    int idx = i;
                    if (slotChips[i] != null && slotChips[i].button != null)
                        slotChips[i].button.onClick.AddListener(() => SelectSlot(idx));
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
            Wire(purchaseModalCancel, HidePurchaseModal);
            Wire(purchaseModalConfirm, ConfirmPurchase);
            if (purchaseModalRoot != null) purchaseModalRoot.SetActive(false);
        }

        private void OnEnable()
        {
            PlayerProfile.CostumeChanged += RefreshFromState;
            PlayerProfile.WalletChanged += RefreshFromState;
        }

        private void OnDisable()
        {
            PlayerProfile.CostumeChanged -= RefreshFromState;
            PlayerProfile.WalletChanged -= RefreshFromState;
        }

        protected override void OnShow()
        {
            BuildSlotModel();
            PlayerProfile.EnsureValidCostumeLoadout(catalog);
            PlayerProfile.ClearUnseenCostumes();
            if (dragRotator != null && previewStage != null)
            {
                dragRotator.SetTarget(previewStage.CharacterRoot);
                dragRotator.ResetToDefault();
            }
            SelectTab(0);
        }

        public override bool OnEscape()
        {
            if (purchaseModalRoot != null && purchaseModalRoot.activeSelf) { HidePurchaseModal(); return true; }
            UIManager.Instance.Pop(); return true;
        }

        // ------------------------------------------------ slot model

        // Build the tab/slot layout from the active catalog. Casual → slotDefinitions grouped by
        // CostumeGroup (Head/Body/Legs) ordered by sortOrder. Fantasy → static arrays.
        private void BuildSlotModel()
        {
            _casual = catalog != null && !catalog.compositeBody && catalog.slotDefinitions.Count > 0;
            if (!_casual) { _tabSlots = FantasyTabSlots; return; }

            var groups = new List<string>[4] { new(), new(), new(), new() };
            foreach (var def in catalog.slotDefinitions.OrderBy(d => d.sortOrder))
            {
                int g = (int)def.group;
                if (g >= 0 && g < 3) groups[g].Add(def.id);
            }
            groups[3].Add(SetsSlot);
            _tabSlots = groups.Select(g => g.ToArray()).ToArray();
        }

        private string SlotLabel(string slot)
        {
            if (slot == SetsSlot) return "Bộ";
            if (_casual)
            {
                var def = catalog.GetSlotDefinition(slot);
                return def != null ? def.displayName : slot;
            }
            return FantasySlotLabels.TryGetValue(slot, out var vn) ? vn : slot;
        }

        // ------------------------------------------------ selection

        private void SelectTab(int idx)
        {
            _tab = Mathf.Clamp(idx, 0, _tabSlots.Length - 1);
            if (tabFills != null)
                for (int i = 0; i < tabFills.Length; i++)
                {
                    if (tabFills[i] == null) continue;
                    bool on = i == _tab;
                    tabFills[i].color = on ? activeColor : Color.clear;
                    if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                        tabLabels[i].color = on ? Color.white : labelDimColor;
                }
            SelectSlot(0);
        }

        private void SelectSlot(int idx)
        {
            var slots = _tabSlots[_tab];
            _slotIndex = Mathf.Clamp(idx, 0, slots.Length - 1);
            _page = 0;
            _pendingBuyId = null;
            BindChips();
            RebuildOptions();
            BindPage();
        }

        private void BindChips()
        {
            if (slotChips == null) return;
            if (CurrentSlot == SetsSlot)
            {
                for (int i = 0; i < slotChips.Length; i++) if (slotChips[i] != null) slotChips[i].Hide();
                return;
            }
            var slots = _tabSlots[_tab];
            for (int i = 0; i < slotChips.Length; i++)
            {
                if (slotChips[i] == null) continue;
                if (i >= slots.Length) { slotChips[i].Hide(); continue; }
                int count = PresentableCount(slots[i]);
                slotChips[i].Bind($"{SlotLabel(slots[i])} ({count})", i == _slotIndex, activeColor, labelDimColor);
            }
        }

        private int PresentableCount(string slot)
        {
            if (slot == SetsSlot) return economy != null && economy.costumeSets != null ? economy.costumeSets.Count : 0;
            if (!_casual && slot == ModularCostumeCatalog.BodySlot) return ModularCostumeCatalog.BodyColors.Length;
            var s = catalog != null ? catalog.GetSlot(slot) : null;
            return s != null ? s.parts.Count : 0;
        }

        private void RebuildOptions()
        {
            _options.Clear();
            string slotName = CurrentSlot;

            if (_casual)
            {
                if (slotName == SetsSlot)
                {
                    if (economy != null && economy.costumeSets != null)
                        foreach (var set in economy.costumeSets)
                            if (set != null) _options.Add(new Opt { kind = OptKind.Set, data = set.setId });
                    return;
                }
                var def = catalog.GetSlotDefinition(slotName);
                if (def != null && def.allowNone)
                    _options.Add(new Opt { kind = OptKind.None });
                var cs = catalog.GetSlot(slotName);
                if (cs != null)
                    for (int i = 0; i < cs.parts.Count; i++)
                        _options.Add(new Opt { kind = OptKind.Part, data = cs.parts[i].itemId });
                return;
            }

            // ---- Fantasy ----
            if (slotName == ModularCostumeCatalog.BodySlot)
            {
                foreach (var c in ModularCostumeCatalog.BodyColors) _options.Add(new Opt { kind = OptKind.BodyColor, data = c });
                foreach (var e in ModularCostumeCatalog.BodyEars) _options.Add(new Opt { kind = OptKind.BodyEar, data = e });
                return;
            }
            _options.Add(ModularCostumeCatalog.IsEssentialSlot(slotName)
                ? new Opt { kind = OptKind.Default }
                : new Opt { kind = OptKind.None });
            var slot = catalog != null ? catalog.GetSlot(slotName) : null;
            if (slot != null)
                for (int i = 0; i < slot.parts.Count; i++)
                    _options.Add(new Opt { kind = OptKind.Part, data = slot.parts[i].guid });
        }

        // ------------------------------------------------ paging + cell state

        private int PageCount => cells == null || cells.Length == 0
            ? 1 : Mathf.Max(1, Mathf.CeilToInt(_options.Count / (float)cells.Length));

        private void TurnPage(int dir)
        {
            _page = Mathf.Clamp(_page + dir, 0, PageCount - 1);
            _pendingBuyId = null;
            BindPage();
        }

        private void BindPage()
        {
            if (cells == null) return;
            string slotName = CurrentSlot;
            int start = _page * cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;
                int idx = start + i;
                if (idx >= _options.Count) { cell.Clear(); _cellOpt[i] = default; continue; }
                _cellOpt[i] = _options[idx];
                if (_casual) BindCasualCell(cell, slotName, _options[idx]);
                else BindFantasyCell(cell, slotName, _options[idx]);
            }
            if (pageLabel != null) pageLabel.text = $"{_page + 1}/{PageCount}";
            if (pagePrevButton != null) pagePrevButton.interactable = _page > 0;
            if (pageNextButton != null) pageNextButton.interactable = _page < PageCount - 1;
        }

        private void BindCasualCell(CostumeItemCardView cell, string slotName, Opt opt)
        {
            if (opt.kind == OptKind.Set)
            {
                if (economy == null || !economy.TryGetCostumeSet(opt.data, out var set)) { cell.Clear(); return; }
                bool ownedSet = PlayerProfile.IsCostumeSetOwned(set);
                economy.TryGetCostumeSetPrice(set, out var setCurrency, out long setPrice);
                int ownedCount = set.itemIds.Count(PlayerProfile.IsCostumeItemOwned);
                string setLabel = $"{set.displayName}\n{PriceText(setCurrency, setPrice)} · {ownedCount}/{set.itemIds.Count} món";
                cell.BindOption(setLabel, set.icon);
                if (cell.icon != null) cell.icon.color = ownedSet ? Color.white : LockedTint;
                if (cell.nameLabel != null) cell.nameLabel.color = ownedSet ? Color.white : UITheme.Gold;
                cell.SetSelected(false);
                return;
            }

            if (opt.kind == OptKind.None)
            {
                cell.BindOption("Không mang", NeutralIcon);
                if (cell.icon != null) cell.icon.color = Color.white;
                if (cell.nameLabel != null) cell.nameLabel.color = Color.white;
                cell.SetSelected(string.IsNullOrEmpty(PlayerProfile.GetPart(slotName)));
                return;
            }

            var e = FindCasualPart(slotName, opt.data);
            bool owned = PlayerProfile.IsCostumeOwned(opt.data); // itemId key
            bool selected = PlayerProfile.GetPart(slotName) == opt.data;
            string label = e?.name ?? "?";
            if (economy != null && economy.TryGetCostume(opt.data, out var commerce))
            {
                label = string.IsNullOrEmpty(commerce.displayName) ? label : commerce.displayName;
                if (!owned && economy.TryGetCostumePrice(opt.data, out var currency, out long price))
                    label += $"\n{PriceText(currency, price)}";
            }
            cell.BindOption(label, e?.icon);
            if (cell.icon != null) cell.icon.color = owned ? Color.white : LockedTint;
            if (cell.nameLabel != null) cell.nameLabel.color = owned ? Color.white : labelDimColor;
            cell.SetSelected(selected);
        }

        private void BindFantasyCell(CostumeItemCardView cell, string slotName, Opt opt)
        {
            Sprite icon = null; string label = null; bool owned = true, selected = false;
            switch (opt.kind)
            {
                case OptKind.Part:
                    var e = FindFantasyPart(slotName, opt.data);
                    label = e?.name ?? "?";
                    icon = uiCatalog != null ? uiCatalog.GetCostumeIcon(opt.data) : null;
                    owned = PlayerProfile.IsCostumeOwned(opt.data);
                    selected = PlayerProfile.GetPart(slotName) == opt.data;
                    break;
                case OptKind.Default:
                    label = "Mặc định";
                    string defGuid = catalog.defaults.GetEquippedGuid(slotName);
                    icon = defGuid != null && uiCatalog != null ? uiCatalog.GetCostumeIcon(defGuid) : NeutralIcon;
                    selected = PlayerProfile.GetPart(slotName) == defGuid;
                    break;
                case OptKind.None:
                    label = "Không mang"; icon = NeutralIcon;
                    selected = string.IsNullOrEmpty(PlayerProfile.GetPart(slotName));
                    break;
                case OptKind.BodyColor:
                    label = opt.data;
                    icon = uiCatalog != null ? uiCatalog.GetBodyColorIcon(opt.data) : null;
                    owned = PlayerProfile.IsBodyColorOwned(opt.data);
                    selected = PlayerProfile.BodyColor == opt.data;
                    break;
                case OptKind.BodyEar:
                    label = opt.data == "Elf" ? "Tai elf" : "Tai thường";
                    icon = NeutralIcon;
                    owned = PlayerProfile.IsBodyEarOwned(opt.data);
                    selected = PlayerProfile.BodyEar == opt.data;
                    break;
            }

            bool confirmBuy = false;
            Color labelColor = owned ? Color.white : labelDimColor;
            if (!owned)
            {
                string itemId = FantasyItemIdOf(opt, slotName);
                if (itemId != null && economy != null && economy.TryGetCostume(itemId, out var e)
                    && (e.source == AcquireSource.Shop || e.source == AcquireSource.ShopAndGacha)
                    && economy.TryGetPrice(e.rarity, out var cur, out long price))
                {
                    bool affordable = PlayerProfile.GetBalance(ToKind(cur)) >= price;
                    confirmBuy = _pendingBuyId == itemId;
                    label = confirmBuy ? $"MUA? {PriceText(cur, price)}" : $"{label}\n{PriceText(cur, price)}";
                    labelColor = affordable ? UITheme.Gold : UITheme.Danger;
                }
            }
            cell.BindOption(label, icon);
            if (cell.icon != null) cell.icon.color = owned ? Color.white : LockedTint;
            if (cell.nameLabel != null) cell.nameLabel.color = labelColor;
            cell.SetSelected(selected || confirmBuy);
        }

        private static string FantasyItemIdOf(Opt opt, string slotName)
        {
            switch (opt.kind)
            {
                case OptKind.Part: return opt.data;
                case OptKind.BodyColor: return EconomyConfig.BodyColorId(opt.data);
                case OptKind.BodyEar: return EconomyConfig.BodyEarId(opt.data);
                default: return null;
            }
        }

        private static PlayerProfile.CurrencyKind ToKind(WalletCurrency c) =>
            c == WalletCurrency.Gold ? PlayerProfile.CurrencyKind.Gold :
            c == WalletCurrency.Gem ? PlayerProfile.CurrencyKind.Gem : PlayerProfile.CurrencyKind.Coin;

        private static string PriceText(WalletCurrency c, long p) =>
            p.ToString("N0") + (c == WalletCurrency.Gem ? " KC" : c == WalletCurrency.Gold ? " V" : " C");

        private Sprite NeutralIcon => uiCatalog != null ? uiCatalog.costumeFallbackIcon : null;

        private ModularCostumeCatalog.PartEntry? FindCasualPart(string slotName, string itemId)
        {
            if (catalog != null && catalog.TryFindByItemId(itemId, out _, out var e)) return e;
            return null;
        }

        private ModularCostumeCatalog.PartEntry? FindFantasyPart(string slotName, string guid)
        {
            var slot = catalog != null ? catalog.GetSlot(slotName) : null;
            if (slot == null) return null;
            for (int i = 0; i < slot.parts.Count; i++)
                if (slot.parts[i].guid == guid) return slot.parts[i];
            return null;
        }

        private void RefreshFromState()
        {
            if (!IsShown) return;
            BindPage();
        }

        // ------------------------------------------------ click dispatch

        private void OnCellClicked(CostumeItemCardView cell)
        {
            int i = Array.IndexOf(cells, cell);
            if (i < 0) return;
            var opt = _cellOpt[i];
            string slotName = CurrentSlot;

            if (opt.kind == OptKind.Default || opt.kind == OptKind.None)
            {
                EquipResult(PlayerProfile.TryClearCostumeSlot(catalog, slotName), cell);
                return;
            }

            if (_casual)
            {
                if (opt.kind == OptKind.Set)
                {
                    if (economy == null || !economy.TryGetCostumeSet(opt.data, out var set)) return;
                    if (PlayerProfile.IsCostumeSetOwned(set)) EquipSet(set, cell);
                    else ShowPurchaseModal(set.setId, set, set.icon);
                    return;
                }
                if (opt.kind == OptKind.Part && PlayerProfile.IsCostumeOwned(opt.data))
                    EquipResult(PlayerProfile.TryEquipCostume(catalog, opt.data), cell);
                else if (opt.kind == OptKind.Part)
                {
                    if (economy != null && economy.TryGetCostume(opt.data, out var item)
                        && (item.source == AcquireSource.Shop || item.source == AcquireSource.ShopAndGacha))
                        ShowPurchaseModal(opt.data, null, FindCasualPart(slotName, opt.data)?.icon);
                    else UIFx.Shake((RectTransform)cell.transform);
                }
                return;
            }

            // ---- Fantasy commerce path ----
            string fid = FantasyItemIdOf(opt, slotName);
            bool ownedF = fid != null && PlayerProfile.IsCostumeItemOwned(fid);
            if (!ownedF)
            {
                if (economy == null || fid == null || !economy.TryGetCostume(fid, out var e)
                    || (e.source != AcquireSource.Shop && e.source != AcquireSource.ShopAndGacha))
                {
                    UIFx.Shake((RectTransform)cell.transform); return;
                }
                ShowPurchaseModal(fid, null, ResolveOptIcon(opt, slotName));
                return;
            }
            EquipFantasyOpt(opt, cell);
        }

        private Sprite ResolveOptIcon(Opt opt, string slotName)
        {
            if (opt.kind != OptKind.Part) return NeutralIcon;
            return _casual ? FindCasualPart(slotName, opt.data)?.icon
                : uiCatalog != null ? uiCatalog.GetCostumeIcon(opt.data) : null;
        }

        private void ShowPurchaseModal(string itemId, EconomyConfig.CostumeSetEntry set, Sprite icon)
        {
            if (purchaseModalRoot == null || economy == null) return;
            _modalItemId = itemId;
            _modalSet = set;
            if (purchaseModalIcon != null)
            {
                purchaseModalIcon.sprite = set?.icon != null ? set.icon : icon;
                purchaseModalIcon.enabled = purchaseModalIcon.sprite != null;
            }
            string itemName = null;
            if (set == null && economy.TryGetCostume(itemId, out var namedItem)) itemName = namedItem.displayName;
            if (purchaseModalTitle != null) purchaseModalTitle.text = set != null ? set.displayName
                : string.IsNullOrEmpty(itemName) ? "Xác nhận mua" : itemName;
            if (purchaseModalDescription != null) purchaseModalDescription.text = set != null
                ? $"Mua trọn bộ {set.itemIds.Count} món?" : "Mua món đồ này?";
            long price = 0; WalletCurrency currency = WalletCurrency.Gem;
            if (set != null) economy.TryGetCostumeSetPrice(set, out currency, out price);
            else economy.TryGetCostumePrice(itemId, out currency, out price);
            if (purchaseModalPrice != null) purchaseModalPrice.text = PriceText(currency, price);
            purchaseModalRoot.SetActive(true);
            purchaseModalRoot.transform.SetAsLastSibling();
        }

        private void HidePurchaseModal()
        {
            if (purchaseModalRoot != null) purchaseModalRoot.SetActive(false);
            _modalItemId = null;
            _modalSet = null;
        }

        private void ConfirmPurchase()
        {
            if (economy == null || string.IsNullOrEmpty(_modalItemId)) return;
            var result = _modalSet != null
                ? PlayerProfile.TryPurchaseCostumeSet(economy, _modalSet.setId)
                : PlayerProfile.TryPurchaseCostume(economy, _modalItemId);
            if (result != PlayerProfile.PurchaseResult.Purchased && result != PlayerProfile.PurchaseResult.AlreadyOwned)
            {
                if (purchaseModalConfirm != null) UIFx.Shake((RectTransform)purchaseModalConfirm.transform);
                return;
            }

            if (_modalSet != null && _casual)
            {
                EquipSet(_modalSet, null);
            }
            else if (_casual) PlayerProfile.TryEquipCostume(catalog, _modalItemId);
            HidePurchaseModal();
            BindPage();
        }

        private void EquipSet(EconomyConfig.CostumeSetEntry set, CostumeItemCardView cell)
        {
            var outfit = new List<LoadoutState.PartSel>();
            foreach (var id in set.itemIds)
                if (catalog.TryFindByItemId(id, out var slot, out _))
                    outfit.Add(new LoadoutState.PartSel { slot = slot, guid = id });
            var result = PlayerProfile.TrySetCasualOutfit(catalog, outfit);
            if (cell != null) EquipResult(result, cell);
        }

        private void EquipFantasyOpt(Opt opt, CostumeItemCardView cell)
        {
            PlayerProfile.CostumeEquipResult r;
            switch (opt.kind)
            {
                case OptKind.Part: r = PlayerProfile.TryEquipCostume(catalog, opt.data); break;
                case OptKind.BodyColor: r = PlayerProfile.TryEquipBodyColor(catalog, opt.data); break;
                case OptKind.BodyEar: r = PlayerProfile.TryEquipBodyEar(catalog, opt.data); break;
                default: return;
            }
            EquipResult(r, cell);
        }

        private static void EquipResult(PlayerProfile.CostumeEquipResult r, CostumeItemCardView cell)
        {
            if (r != PlayerProfile.CostumeEquipResult.Equipped
                && r != PlayerProfile.CostumeEquipResult.AlreadyEquipped)
                UIFx.Shake((RectTransform)cell.transform);
        }

        // ------------------------------------------------ randomize / reset

        private void RandomizeOutfit()
        {
            if (catalog == null) return;
            if (_casual) { RandomizeCasual(); return; }
            RandomizeFantasy();
        }

        private void RandomizeCasual()
        {
            var outfit = new List<LoadoutState.PartSel>();
            foreach (var def in catalog.slotDefinitions)
            {
                var slot = catalog.GetSlot(def.id);
                if (slot == null) continue;
                var owned = new List<string>();
                for (int i = 0; i < slot.parts.Count; i++)
                    if (PlayerProfile.IsCostumeOwned(slot.parts[i].itemId)) owned.Add(slot.parts[i].itemId);
                if (owned.Count == 0) continue;

                // optional slot: chance of "Không mang"; required: always pick one.
                if (def.allowNone && UnityEngine.Random.value < 0.5f) continue;
                outfit.Add(new LoadoutState.PartSel { slot = def.id, guid = owned[UnityEngine.Random.Range(0, owned.Count)] });
            }
            PlayerProfile.TrySetCasualOutfit(catalog, outfit);
        }

        private void RandomizeFantasy()
        {
            var outfit = new List<LoadoutState.PartSel>();
            foreach (var group in FantasyTabSlots)
                foreach (var slotName in group)
                {
                    if (slotName == ModularCostumeCatalog.BodySlot) continue;
                    var slot = catalog.GetSlot(slotName);
                    if (slot == null) continue;
                    var owned = new List<string>();
                    for (int i = 0; i < slot.parts.Count; i++)
                        if (PlayerProfile.IsCostumeOwned(slot.parts[i].guid)) owned.Add(slot.parts[i].guid);

                    if (ModularCostumeCatalog.IsOptionalSlot(slotName))
                    {
                        if (owned.Count == 0 || UnityEngine.Random.value < 0.5f) continue;
                        outfit.Add(new LoadoutState.PartSel { slot = slotName, guid = owned[UnityEngine.Random.Range(0, owned.Count)] });
                    }
                    else if (owned.Count > 0)
                        outfit.Add(new LoadoutState.PartSel { slot = slotName, guid = owned[UnityEngine.Random.Range(0, owned.Count)] });
                }
            string color = RandomOwned(ModularCostumeCatalog.BodyColors, PlayerProfile.IsBodyColorOwned, PlayerProfile.BodyColor);
            string ear = RandomOwned(ModularCostumeCatalog.BodyEars, PlayerProfile.IsBodyEarOwned, PlayerProfile.BodyEar);
            PlayerProfile.TryEquipLook(catalog, outfit, color, ear);
        }

        private static string RandomOwned(string[] all, Func<string, bool> isOwned, string fallback)
        {
            var owned = new List<string>();
            foreach (var v in all) if (isOwned(v)) owned.Add(v);
            return owned.Count > 0 ? owned[UnityEngine.Random.Range(0, owned.Count)] : fallback;
        }

        private void ResetOutfit() => PlayerProfile.TryResetOutfitToDefaults(catalog);

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
