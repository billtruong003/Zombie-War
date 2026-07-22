using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Màn 04–07 SHOP (spec §4.4): shell chung + SegmentedTabs 4 tab
    /// (WEAPONS · GACHA · COSTUME · UPGRADES). Mỗi tab = 1 content panel bật/tắt.
    ///
    /// Slice 3 — tab WEAPONS mua thật:
    /// - State card (owned/giá/affordable) bind runtime từ PlayerProfile + WeaponData.price,
    ///   ghi đè mọi state installer bake sẵn. cheatUnlockAll KHÔNG có tác dụng ở đây.
    /// - Tap 1 = chọn card (xem state, chưa mua). Tap lần 2 vào card ĐANG chọn = mua qua
    ///   PlayerProfile.TryPurchaseWeapon (atomic). Mua xong KHÔNG tự equip — equip ở Loadout.
    /// - Thiếu tiền/lỗi: shake + giá màu danger. Refresh theo WalletChanged/LoadoutChanged.
    /// - Tab GACHA/COSTUME/UPGRADES chưa có backend: mọi button trong panel bị disable (honest).
    /// </summary>
    public sealed class ShopScreen : UIScreen
    {
        [Header("Nav")]
        [SerializeField] private Button backButton;

        [Header("Tabs + content (index khớp nhau)")]
        [SerializeField] private Button[] tabButtons;      // 4 tabs
        [SerializeField] private GameObject[] tabPanels;   // 4 content roots
        [SerializeField] private Image[] tabFills;         // pill fill của từng tab
        [SerializeField] private TMP_Text[] tabLabels;
        [SerializeField] private Color[] tabActiveColors;  // màu active theo ngữ cảnh tab
        [SerializeField] private Color labelDimColor = new Color(0.604f, 0.639f, 0.698f);

        [Header("Data (icon metadata — ownership thật nằm ở PlayerProfile)")]
        [SerializeField] private UIPrototypeCatalog catalog;

        [Header("Weapon cards (bake sẵn trong prefab, state bind runtime)")]
        [SerializeField] private WeaponItemCardView[] weaponCards;

        [Header("Gacha (Slice 6 — pool config lấy từ EconomyConfig)")]
        [SerializeField] private EconomyConfig economy;

        [Header("Costume shop (editor-authored card pool)")]
        [SerializeField] private Button[] costumeModeButtons;
        [SerializeField] private ShopCostumeCardView[] costumeCards;
        [SerializeField] private Button costumePrevButton;
        [SerializeField] private Button costumeNextButton;
        [SerializeField] private TMP_Text costumePageLabel;

        [Header("Purchase confirmation (shared by item/set)")]
        [SerializeField] private GameObject purchaseModal;
        [SerializeField] private Image purchaseIcon;
        [SerializeField] private TMP_Text purchaseTitle;
        [SerializeField] private TMP_Text purchasePrice;
        [SerializeField] private Button purchaseConfirmButton;
        [SerializeField] private Button purchaseCancelButton;

        [Header("Weapon upgrades (editor-authored card pool)")]
        [SerializeField] private WeaponUpgradeCardView[] upgradeCards;
        [SerializeField] private Button upgradePrevButton;
        [SerializeField] private Button upgradeNextButton;
        [SerializeField] private TMP_Text upgradePageLabel;

        private int _active;
        private int _pendingTab;
        private WeaponItemCardView _selectedCard;
        private int _costumeMode;
        private int _costumePage;
        private int _upgradePage;
        private string _pendingCostumeId;
        private bool _pendingCostumeIsSet;

        // Reveal overlay dung lazily (code-built, khong bake prefab).
        private GameObject _revealRoot;
        private TMP_Text _revealBody;

        protected override void Awake()
        {
            base.Awake();
            Wire(backButton, () => UIManager.Instance.Pop());
            if (tabButtons != null)
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    int idx = i;
                    Wire(tabButtons[i], () => SelectTab(idx));
                }
            if (weaponCards != null)
                foreach (var card in weaponCards)
                {
                    var c = card;
                    if (c != null && c.button != null)
                        c.button.onClick.AddListener(() => OnCardClicked(c));
                }
            WireGacha();
            WireCostumeShop();
            WireWeaponUpgrades();
            Wire(purchaseConfirmButton, ConfirmCostumePurchase);
            Wire(purchaseCancelButton, ClosePurchaseModal);
            if (purchaseModal != null) purchaseModal.SetActive(false);
        }

        /// Tab COSTUME của Shop là cổng vào — mua/mặc costume thật ở màn Costume (có preview sống).
        /// Mọi button trong panel này mở CostumeScreen thay vì giả vờ bán.
        private void WireCostumeRedirect()
        {
            if (tabPanels == null || tabPanels.Length < 3 || tabPanels[2] == null) return;
            foreach (var b in tabPanels[2].GetComponentsInChildren<Button>(true))
                Wire(b, () => UIManager.Instance.Push<CostumeScreen>());
        }

        private void OnEnable()
        {
            PlayerProfile.WalletChanged += RefreshCards;
            PlayerProfile.LoadoutChanged += RefreshCards;
            PlayerProfile.CostumeChanged += RefreshCards;
        }

        private void OnDisable()
        {
            PlayerProfile.WalletChanged -= RefreshCards;
            PlayerProfile.LoadoutChanged -= RefreshCards;
            PlayerProfile.CostumeChanged -= RefreshCards;
        }

        protected override void OnShow()
        {
            RefreshCards();
            SelectTab(_pendingTab);
            _pendingTab = 0;
        }

        public override bool OnEscape() { UIManager.Instance.Pop(); return true; }

        // ------------------------------------------------ weapons tab (mua thật)

        /// Tap 1 = chọn (xem, không mua). Tap lần 2 vào card đang chọn = mua.
        private void OnCardClicked(WeaponItemCardView card)
        {
            if (card == null || card.data == null) return;

            if (_selectedCard != card)
            {
                if (_selectedCard != null) _selectedCard.SetSelected(false);
                _selectedCard = card;
                card.SetSelected(true);
                return;
            }

            TryPurchase(card);
        }

        private void TryPurchase(WeaponItemCardView card)
        {
            var d = card.data;
            if (string.IsNullOrEmpty(d.WeaponId))
            {
                Debug.LogWarning($"[ShopScreen] '{d.name}' thiếu WeaponId — không thể mua.");
                UIFx.Shake((RectTransform)card.transform);
                return;
            }
            if (PlayerProfile.IsWeaponOwned(d.WeaponId)) return; // đã có — không bán lại, không charge

            var result = PlayerProfile.TryPurchaseWeapon(d.WeaponId, d.price);
            switch (result)
            {
                case PlayerProfile.PurchaseResult.Purchased:
                    // WalletChanged/LoadoutChanged đã refresh card + currency cluster;
                    // badge "ĐÃ CÓ" hiện ra chính là feedback thành công.
                    break;
                case PlayerProfile.PurchaseResult.AlreadyOwned:
                    break; // idempotent, không charge
                default: // InsufficientFunds / InvalidWeapon / InvalidPrice / SaveFailed
                    UIFx.Shake((RectTransform)card.transform);
                    break;
            }
        }

        /// Ghi đè state bake sẵn: owned từ PlayerProfile, giá từ WeaponData.price (KHÔNG unlockCost),
        /// affordable từ ví profile. Icon bind 1 lần từ catalog (không bake trong prefab).
        private void RefreshCards()
        {
            if (!IsShown && !gameObject.activeInHierarchy) return;
            if (weaponCards == null) return;
            long coin = PlayerProfile.Coin;
            foreach (var card in weaponCards)
            {
                if (card == null || card.data == null) continue;
                var d = card.data;

                if (card.icon != null && card.icon.sprite == null && catalog != null)
                {
                    var sprite = catalog.GetWeaponIcon(d);
                    if (sprite != null) { card.icon.sprite = sprite; card.icon.color = Color.white; }
                }

                bool owned = PlayerProfile.IsWeaponOwned(d.WeaponId);
                card.SetOwned(owned, d.price);
                if (!owned && card.priceLabel != null)
                    card.priceLabel.color = coin >= d.price ? UITheme.Gold : UITheme.Danger;
            }
            RefreshCostumeCards();
            RefreshUpgradeCards();
        }

        // ------------------------------------------------ tabs

        /// <summary>Đặt tab sẽ mở (0 Weapons · 1 Gacha · 2 Costume · 3 Upgrades) — gọi TRƯỚC Push.
        /// Pass "Quay Gacha" dùng để mở thẳng Gacha; Hub mở mặc định Weapons (pending reset sau mỗi Show).</summary>
        public void OpenTab(int index)
        {
            _pendingTab = Mathf.Clamp(index, 0, tabPanels != null ? tabPanels.Length - 1 : 0);
            if (IsShown) SelectTab(_pendingTab);
        }

        private void SelectTab(int idx)
        {
            _active = idx;
            if (tabPanels != null)
                for (int i = 0; i < tabPanels.Length; i++)
                    if (tabPanels[i] != null) tabPanels[i].SetActive(i == idx);

            if (tabFills != null)
                for (int i = 0; i < tabFills.Length; i++)
                {
                    if (tabFills[i] == null) continue;
                    bool on = i == idx;
                    var activeCol = tabActiveColors != null && i < tabActiveColors.Length
                        ? tabActiveColors[i] : new Color(0.298f, 0.686f, 0.431f);
                    tabFills[i].color = on ? activeCol : Color.clear;
                    if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                        tabLabels[i].color = on ? Color.white : labelDimColor;
                }
        }

        /// Chỉ còn UPGRADES chưa có backend — disable button trong panel đó (KHÔNG fake success).
        /// Gacha (index 1) đã wired thật; Costume (index 2) mua qua màn Costume.
        private void DisableUnimplementedTabActions()
        {
            if (tabPanels == null || tabPanels.Length < 4 || tabPanels[3] == null) return;
            foreach (var b in tabPanels[3].GetComponentsInChildren<Button>(true))
                b.interactable = false;
        }

        // ------------------------------------------------ costume commerce

        private void WireCostumeShop()
        {
            if (costumeModeButtons != null)
                for (int i = 0; i < costumeModeButtons.Length; i++)
                {
                    int mode = i;
                    Wire(costumeModeButtons[i], () => { _costumeMode = mode; _costumePage = 0; RefreshCostumeCards(); });
                }
            if (costumeCards != null)
                foreach (var card in costumeCards)
                {
                    var captured = card;
                    if (captured != null) Wire(captured.button, () => OpenCostumePurchase(captured));
                }
            Wire(costumePrevButton, () => { _costumePage = Mathf.Max(0, _costumePage - 1); RefreshCostumeCards(); });
            Wire(costumeNextButton, () => { _costumePage++; RefreshCostumeCards(); });
        }

        private void RefreshCostumeCards()
        {
            if (costumeCards == null || costumeCards.Length == 0 || economy == null) return;
            if (costumeModeButtons != null)
                for (int i = 0; i < costumeModeButtons.Length; i++)
                {
                    var button = costumeModeButtons[i];
                    if (button == null) continue;
                    bool active = i == _costumeMode;
                    if (button.targetGraphic != null) button.targetGraphic.color = active ? UITheme.Green : UITheme.Surface2;
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null) label.color = active ? Color.white : UITheme.TextMain;
                }
            var items = _costumeMode == 0 ? ShopCostumeItems() : null;
            var sets = _costumeMode == 1 ? ShopCostumeSets() : null;
            int total = _costumeMode == 0 ? items.Count : sets.Count;
            int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)costumeCards.Length));
            _costumePage = Mathf.Clamp(_costumePage, 0, pages - 1);
            if (costumePageLabel != null) costumePageLabel.text = $"{_costumePage + 1}/{pages}";
            if (costumePrevButton != null) costumePrevButton.interactable = _costumePage > 0;
            if (costumeNextButton != null) costumeNextButton.interactable = _costumePage + 1 < pages;

            int start = _costumePage * costumeCards.Length;
            for (int i = 0; i < costumeCards.Length; i++)
            {
                var card = costumeCards[i];
                int index = start + i;
                if (card == null) continue;
                if (_costumeMode == 0 && index < items.Count)
                {
                    var entry = items[index];
                    economy.TryGetCostumePrice(entry.itemId, out var currency, out long price);
                    card.Bind(entry.itemId, false, entry.displayName,
                        catalog != null ? catalog.GetCostumeIcon(entry.itemId) : null,
                        entry.rarity, currency, price, PlayerProfile.IsCostumeItemOwned(entry.itemId));
                }
                else if (_costumeMode == 1 && index < sets.Count)
                {
                    var set = sets[index];
                    economy.TryGetCostumeSetPrice(set, out var currency, out long price);
                    card.Bind(set.setId, true, set.displayName, set.icon, set.rarity, currency, price,
                        PlayerProfile.IsCostumeSetOwned(set));
                }
                else card.Clear();
            }
        }

        private List<EconomyConfig.CostumeEntry> ShopCostumeItems()
        {
            var result = new List<EconomyConfig.CostumeEntry>();
            if (economy == null || economy.costumeItems == null) return result;
            foreach (var item in economy.costumeItems)
                if (item.source == AcquireSource.Shop || item.source == AcquireSource.ShopAndGacha) result.Add(item);
            return result;
        }

        private List<EconomyConfig.CostumeSetEntry> ShopCostumeSets()
        {
            var result = new List<EconomyConfig.CostumeSetEntry>();
            if (economy == null || economy.costumeSets == null) return result;
            foreach (var set in economy.costumeSets)
                if (set != null && (set.source == AcquireSource.Shop || set.source == AcquireSource.ShopAndGacha)) result.Add(set);
            return result;
        }

        private void OpenCostumePurchase(ShopCostumeCardView card)
        {
            if (card == null || string.IsNullOrEmpty(card.offerId) || economy == null) return;
            if (card.isSet && economy.TryGetCostumeSet(card.offerId, out var set))
            {
                if (PlayerProfile.IsCostumeSetOwned(set)) return;
                economy.TryGetCostumeSetPrice(set, out var currency, out long price);
                ShowPurchaseModal(card.offerId, true, set.displayName, set.icon, currency, price);
            }
            else if (!card.isSet && economy.TryGetCostume(card.offerId, out var item))
            {
                if (PlayerProfile.IsCostumeItemOwned(item.itemId)) return;
                economy.TryGetCostumePrice(item.itemId, out var currency, out long price);
                ShowPurchaseModal(item.itemId, false, item.displayName,
                    catalog != null ? catalog.GetCostumeIcon(item.itemId) : null, currency, price);
            }
        }

        private void ShowPurchaseModal(string id, bool isSet, string displayName, Sprite icon,
            WalletCurrency currency, long price)
        {
            _pendingCostumeId = id;
            _pendingCostumeIsSet = isSet;
            if (purchaseTitle != null) purchaseTitle.text = $"Mua {displayName}?";
            if (purchaseIcon != null) { purchaseIcon.sprite = icon; purchaseIcon.enabled = icon != null; }
            if (purchasePrice != null) purchasePrice.text = $"{CurTag(currency)} {price:N0}";
            if (purchaseModal != null) { purchaseModal.SetActive(true); purchaseModal.transform.SetAsLastSibling(); }
        }

        private void ConfirmCostumePurchase()
        {
            if (economy == null || string.IsNullOrEmpty(_pendingCostumeId)) return;
            var result = _pendingCostumeIsSet
                ? PlayerProfile.TryPurchaseCostumeSet(economy, _pendingCostumeId)
                : PlayerProfile.TryPurchaseCostume(economy, _pendingCostumeId);
            if (result == PlayerProfile.PurchaseResult.Purchased || result == PlayerProfile.PurchaseResult.AlreadyOwned)
                ClosePurchaseModal();
            else if (purchaseModal != null) UIFx.Shake((RectTransform)purchaseModal.transform);
            RefreshCards();
        }

        private void ClosePurchaseModal()
        {
            _pendingCostumeId = null;
            if (purchaseModal != null) purchaseModal.SetActive(false);
        }

        // ------------------------------------------------ permanent weapon upgrades

        private void WireWeaponUpgrades()
        {
            if (upgradeCards != null)
                foreach (var card in upgradeCards)
                {
                    var captured = card;
                    if (captured != null) Wire(captured.button, () => TryUpgradeWeapon(captured));
                }
            Wire(upgradePrevButton, () => { _upgradePage = Mathf.Max(0, _upgradePage - 1); RefreshUpgradeCards(); });
            Wire(upgradeNextButton, () => { _upgradePage++; RefreshUpgradeCards(); });
        }

        private List<WeaponData> OwnedWeapons()
        {
            var result = new List<WeaponData>();
            foreach (var weapon in WeaponList())
                if (weapon != null && PlayerProfile.IsWeaponOwned(weapon.WeaponId)) result.Add(weapon);
            result.Sort((a, b) => a.CatalogOrder.CompareTo(b.CatalogOrder));
            return result;
        }

        private void RefreshUpgradeCards()
        {
            if (upgradeCards == null || upgradeCards.Length == 0 || economy == null) return;
            var owned = OwnedWeapons();
            int pages = Mathf.Max(1, Mathf.CeilToInt(owned.Count / (float)upgradeCards.Length));
            _upgradePage = Mathf.Clamp(_upgradePage, 0, pages - 1);
            if (upgradePageLabel != null) upgradePageLabel.text = $"{_upgradePage + 1}/{pages}";
            if (upgradePrevButton != null) upgradePrevButton.interactable = _upgradePage > 0;
            if (upgradeNextButton != null) upgradeNextButton.interactable = _upgradePage + 1 < pages;
            int start = _upgradePage * upgradeCards.Length;
            for (int i = 0; i < upgradeCards.Length; i++)
            {
                var card = upgradeCards[i];
                if (card == null) continue;
                int index = start + i;
                if (index >= owned.Count) { card.Clear(); continue; }
                BindUpgradeCard(card, owned[index]);
            }
        }

        private void BindUpgradeCard(WeaponUpgradeCardView card, WeaponData weapon)
        {
            card.data = weapon;
            int level = PlayerProfile.GetWeaponLevel(weapon.WeaponId);
            int next = Mathf.Min(3, level + 1);
            if (card.icon != null)
            {
                card.icon.sprite = catalog != null ? catalog.GetWeaponIcon(weapon) : null;
                card.icon.enabled = card.icon.sprite != null;
            }
            if (card.border != null) card.border.color = weapon.TierColor;
            if (card.nameLabel != null) card.nameLabel.text = weapon.weaponName;
            if (card.levelLabel != null) card.levelLabel.text = $"CẤP {level}/3";
            if (card.statLabel != null) card.statLabel.text = level >= 3
                ? $"DMG {WeaponUpgradeMath.EffectiveDamage(weapon, level):0.#} · ROF {WeaponUpgradeMath.EffectiveFireRate(weapon, level):0.##}"
                : $"DMG {WeaponUpgradeMath.EffectiveDamage(weapon, level):0.#} → {WeaponUpgradeMath.EffectiveDamage(weapon, next):0.#}\nROF {WeaponUpgradeMath.EffectiveFireRate(weapon, level):0.##} → {WeaponUpgradeMath.EffectiveFireRate(weapon, next):0.##}";
            if (card.resourceLabel != null)
            {
                if (level >= 3) card.resourceLabel.text = "MAX";
                else
                {
                    int tier = Mathf.Clamp((int)weapon.tier, 0, 4);
                    int shards = (level == 1 ? economy.weaponStar2ShardCost : economy.weaponStar3ShardCost)[tier];
                    long gold = (level == 1 ? economy.weaponStar2GoldCost : economy.weaponStar3GoldCost)[tier];
                    card.resourceLabel.text = $"{PlayerProfile.GetWeaponShards(weapon.WeaponId)}/{shards} mảnh · {gold:N0} V";
                }
            }
            if (card.button != null) card.button.interactable = level < 3;
            card.gameObject.SetActive(true);
        }

        private void TryUpgradeWeapon(WeaponUpgradeCardView card)
        {
            if (card == null || card.data == null || economy == null) return;
            var result = PlayerProfile.TryUpgradeWeapon(card.data, economy);
            if (result != PlayerProfile.WeaponUpgradeResult.Upgraded && result != PlayerProfile.WeaponUpgradeResult.MaxLevel)
                UIFx.Shake((RectTransform)card.transform);
            RefreshCards();
        }

        // ------------------------------------------------ gacha (Slice 6)

        private void WireGacha()
        {
            if (tabPanels == null || tabPanels.Length < 2 || tabPanels[1] == null || economy == null) return;
            var g = tabPanels[1].transform;
            Wire(FindBtn(g, "SungGacha", "Quay1"),  () => Pull(economy.weaponPool, 1));
            Wire(FindBtn(g, "SungGacha", "Quay10"), () => Pull(economy.weaponPool, economy.weaponPool.multiCount));
            Wire(FindBtn(g, "SkinGacha", "Quay1"),  () => Pull(economy.costumePool, 1));
            Wire(FindBtn(g, "SkinGacha", "Quay10"), () => Pull(economy.costumePool, economy.costumePool.multiCount));
            UpdateGachaLabels(g);
        }

        /// Đồng bộ nhãn nút + rate với pool config (không hardcode giá trong prefab).
        private void UpdateGachaLabels(Transform g)
        {
            SetBtnLabel(FindBtn(g, "SungGacha", "Quay1"),  $"Quay 1 · {economy.weaponPool.singleCost}");
            SetBtnLabel(FindBtn(g, "SungGacha", "Quay10"), $"Quay {economy.weaponPool.multiCount} · {economy.weaponPool.multiCost}");
            SetBtnLabel(FindBtn(g, "SkinGacha", "Quay1"),  $"Quay 1 · {economy.costumePool.singleCost}");
            SetBtnLabel(FindBtn(g, "SkinGacha", "Quay10"), $"Quay {economy.costumePool.multiCount} · {economy.costumePool.multiCost}");
        }

        private void Pull(EconomyConfig.GachaPool pool, int count)
        {
            if (economy == null || pool == null) return;
            var poolItems = GachaService.BuildPool(pool, economy, WeaponList(), StarterWeaponIds());
            var results = GachaService.Pull(economy, pool, poolItems, count, new GachaService.SystemRng());
            if (results == null)
            {
                ShowReveal(pool.displayName, "Không đủ tiền hoặc pool trống — không quay.");
                return;
            }
            ShowReveal(pool.displayName, FormatResults(results));
        }

        private System.Collections.Generic.List<WeaponData> WeaponList()
        {
            var list = new System.Collections.Generic.List<WeaponData>();
            if (catalog != null && catalog.weapons != null)
                foreach (var w in catalog.weapons)
                    if (w != null && w.data != null) list.Add(w.data);
            return list;
        }

        // Starter = súng giá 0 (Pistol khởi đầu) — loại khỏi pool gacha.
        private System.Collections.Generic.HashSet<string> StarterWeaponIds()
        {
            var set = new System.Collections.Generic.HashSet<string>();
            foreach (var w in WeaponList())
                if (w.price == 0 && !string.IsNullOrEmpty(w.WeaponId)) set.Add(w.WeaponId);
            return set;
        }

        private static readonly string[] RarityVi = { "Xám", "Xanh lá", "Xanh biển", "Tím", "Cam" };

        private static string FormatResults(System.Collections.Generic.List<GachaService.GachaResult> rs)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in rs)
            {
                var col = UITheme.Rarity != null && (int)r.rarity < UITheme.Rarity.Length ? UITheme.Rarity[(int)r.rarity] : Color.white;
                string hex = ColorUtility.ToHtmlStringRGB(col);
                string rv = (int)r.rarity < RarityVi.Length ? RarityVi[(int)r.rarity] : r.rarity.ToString();
                string duplicate = r.isWeapon
                    ? $"<color=#9AA3B2>Trùng +{r.weaponShards} mảnh</color>"
                    : $"<color=#9AA3B2>Trùng +{r.dupComp} {CurTag(r.dupCurrency)}</color>";
                string tag = r.isNew ? "<color=#4ECB6E>MỚI</color>" : duplicate;
                sb.Append($"<color=#{hex}>●</color> {r.displayName} <size=70%>[{rv}]</size>  {tag}\n");
            }
            return sb.ToString();
        }

        private static string CurTag(WalletCurrency c) => c == WalletCurrency.Gem ? "KC" : c == WalletCurrency.Gold ? "V" : "C";

        private static Button FindBtn(Transform root, string cardName, string btnName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == cardName)
                    foreach (var b in t.GetComponentsInChildren<Button>(true))
                        if (b.name == btnName) return b;
            return null;
        }

        private static void SetBtnLabel(Button b, string text)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = text;
        }

        // ------------------------------------------------ reveal overlay (code-built, lazy)

        private void ShowReveal(string title, string body)
        {
            EnsureRevealBuilt();
            _revealRoot.SetActive(true);
            _revealRoot.transform.SetAsLastSibling();
            if (_revealBody != null) _revealBody.text = $"<b>{title}</b>\n\n{body}";
        }

        private void EnsureRevealBuilt()
        {
            if (_revealRoot != null) return;

            _revealRoot = new GameObject("GachaReveal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = (RectTransform)_revealRoot.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = _revealRoot.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.82f);
            _revealRoot.GetComponent<Button>().onClick.AddListener(() => _revealRoot.SetActive(false)); // tap-nền đóng

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var prt = (RectTransform)panel.transform;
            prt.SetParent(rt, false);
            prt.anchorMin = new Vector2(0.5f, 0.5f); prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f); prt.sizeDelta = new Vector2(880, 900);
            panel.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.14f, 1f);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer));
            var brt = (RectTransform)bodyGo.transform;
            brt.SetParent(prt, false);
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(48, 96); brt.offsetMax = new Vector2(-48, -48);
            _revealBody = bodyGo.AddComponent<TextMeshProUGUI>();
            _revealBody.enableWordWrapping = true;
            _revealBody.fontSize = 34;
            _revealBody.color = Color.white;
            _revealBody.alignment = TextAlignmentOptions.TopLeft;
            _revealBody.richText = true;

            var hint = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer));
            var hrt = (RectTransform)hint.transform;
            hrt.SetParent(prt, false);
            hrt.anchorMin = new Vector2(0, 0); hrt.anchorMax = new Vector2(1, 0); hrt.pivot = new Vector2(0.5f, 0);
            hrt.offsetMin = new Vector2(0, 24); hrt.offsetMax = new Vector2(0, 72);
            var ht = hint.AddComponent<TextMeshProUGUI>();
            ht.text = "Chạm để đóng"; ht.fontSize = 26; ht.color = new Color(0.6f, 0.64f, 0.7f);
            ht.alignment = TextAlignmentOptions.Center;
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
