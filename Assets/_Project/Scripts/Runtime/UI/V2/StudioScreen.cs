using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Studio (owner-approved V2_Studio mockup): the character in the middle with hotspots for
    /// the main slots (hat, face, jacket, back, pants, shoes), a chip row for every slot, a film
    /// strip of that slot's pieces, and a bar for the picked piece. Any piece can be tried on
    /// before buying (visual only; leaving restores the saved outfit). Gacha-only pieces point to
    /// the Gacha. Up to three looks can be saved and worn in one tap.
    /// Built by HordeCall/UI v2/Build Studio.
    /// </summary>
    public sealed class StudioScreen : UIScreen
    {
        [Serializable]
        public sealed class Film { public Button button; public Image bar; public Image icon; public TMP_Text price; public GameObject selected; }

        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text gemLabel;
        [SerializeField] private RawImage character;
        [SerializeField] private Button[] hotspots = new Button[6];
        [SerializeField] private Button[] looks = new Button[PlayerProfile.MaxLooks];
        [SerializeField] private Button saveLook;

        [Header("Sheet")]
        [SerializeField] private TMP_Text slotTitle;
        [SerializeField] private TMP_Text ownedLabel;
        [SerializeField] private GameObject gachaTag;
        [SerializeField] private TMP_Text gachaTagText;
        [SerializeField] private Button[] chips = new Button[24];
        [SerializeField] private ScrollRect strip;
        [SerializeField] private Film[] films = new Film[MaxPieces];

        [Header("Picked piece")]
        [SerializeField] private Image pickedIcon;
        [SerializeField] private TMP_Text pickedName;
        [SerializeField] private TMP_Text pickedState;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        [SerializeField] private ModularCostumeCatalog catalog;
        [SerializeField] private EconomyConfig economy;
        [SerializeField] private UIScreen gachaScreen;

        public const int MaxPieces = 80;
        /// Hotspot order: HAT, FACE, JACKET, BACK, PANTS, SHOES.
        public static readonly string[] HotspotSlots = { "Head", "Eyewear", "Chest", "Back", "Legs", "Feet" };

        static readonly Color Yellow = new(1f, 0.788f, 0.235f), Quiet = new(0.184f, 0.208f, 0.267f),
            Ink = new(0.957f, 0.945f, 0.918f), OnYellow = new(0.165f, 0.114f, 0f);
        static readonly Color[] Rarity = { new(0.604f, 0.639f, 0.698f), new(0.298f, 0.686f, 0.431f), new(0.31f, 0.639f, 0.969f), new(0.545f, 0.482f, 0.847f), new(0.949f, 0.6f, 0.29f) };

        MenuCharacterStage _stage;
        List<ModularCostumeCatalog.SlotDefinition> _slots = new();
        string _slot = "Chest";
        List<ModularCostumeCatalog.PartEntry> _parts = new();
        string _picked;          // itemId being shown (worn or tried)

        protected override void Awake()
        {
            base.Awake();
            if (backButton != null) backButton.onClick.AddListener(() => { UIFeedback.Back(); UIManager.Instance?.Pop(); });
            for (int i = 0; i < hotspots.Length; i++) { int idx = i; if (hotspots[i] != null) hotspots[i].onClick.AddListener(() => SelectSlot(HotspotSlots[idx])); }
            for (int i = 0; i < chips.Length; i++) { int idx = i; if (chips[i] != null) chips[i].onClick.AddListener(() => { if (idx < _slots.Count) SelectSlot(_slots[idx].id); }); }
            for (int i = 0; i < films.Length; i++) { int idx = i; if (films[i]?.button != null) films[i].button.onClick.AddListener(() => Pick(idx)); }
            for (int i = 0; i < looks.Length; i++) { int idx = i; if (looks[i] != null) looks[i].onClick.AddListener(() => WearLook(idx)); }
            if (saveLook != null) saveLook.onClick.AddListener(SaveLook);
            if (actionButton != null) actionButton.onClick.AddListener(Act);
        }

        private void OnEnable() { PlayerProfile.CostumeChanged += Refresh; PlayerProfile.WalletChanged += Refresh; }
        private void OnDisable() { PlayerProfile.CostumeChanged -= Refresh; PlayerProfile.WalletChanged -= Refresh; RestoreOutfit(); }

        protected override void OnShow()
        {
            _stage = FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            if (character != null && _stage != null) character.texture = _stage.Texture;
            _slots = catalog != null ? catalog.slotDefinitions.Where(d => !catalog.IsTechnicalCasualSlot(d.id)).OrderBy(d => d.sortOrder).ToList() : new();
            if (catalog != null) PlayerProfile.EnsureValidCostumeLoadout(catalog);
            SelectSlot(_slot);
        }

        protected override void OnHide() => RestoreOutfit();

        void RestoreOutfit() { if (_stage != null && _stage.ModularApplier != null) _stage.ModularApplier.ApplySavedParts(); }

        void SelectSlot(string slot)
        {
            UIFeedback.Tap();
            RestoreOutfit();
            _slot = slot;
            var s = catalog != null ? catalog.GetSlot(slot) : null;
            _parts = s != null ? s.parts.ToList() : new();
            _picked = PlayerProfile.GetPart(slot);
            if (strip != null) strip.horizontalNormalizedPosition = 0f;
            Refresh();
        }

        void Pick(int i)
        {
            if (i >= _parts.Count) return;
            var p = _parts[i];
            _picked = p.itemId;
            UIFeedback.Tap();
            if (PlayerProfile.IsCostumeOwned(p.itemId)) { if (catalog != null) PlayerProfile.TryEquipCostume(catalog, p.itemId); }
            else if (_stage != null && _stage.ModularApplier != null) _stage.ModularApplier.Apply(_slot, p);   // try on
            Refresh();
        }

        void Act()
        {
            if (string.IsNullOrEmpty(_picked) || economy == null) return;
            if (PlayerProfile.IsCostumeOwned(_picked)) { Toast.Show("Wearing it"); return; }
            if (!economy.TryGetCostume(_picked, out var item)) return;
            if (item.source == AcquireSource.Gacha)
            {
                UIFeedback.Tap();
                if (gachaScreen != null && AccountProgress.IsUnlocked(AccountProgress.Feature.Gacha)) UIManager.Instance?.Push(gachaScreen);
                else Toast.Show($"Gacha only · unlocks at level {AccountProgress.RequiredLevel(AccountProgress.Feature.Gacha)}");
                return;
            }
            var r = PlayerProfile.TryPurchaseCostume(economy, _picked);
            if (r == PlayerProfile.PurchaseResult.Purchased)
            {
                UIFeedback.Purchase();
                if (catalog != null) PlayerProfile.TryEquipCostume(catalog, _picked);
                Toast.Show($"{item.displayName} is yours");
            }
            else if (r == PlayerProfile.PurchaseResult.InsufficientFunds) { UIFeedback.Error(); Toast.Show(item.currency == WalletCurrency.Gem ? "Not enough gems" : "Not enough coins"); }
        }

        void WearLook(int i)
        {
            var l = PlayerProfile.GetLook(i);
            if (l == null) { Toast.Show("Save a look with +"); return; }
            var res = catalog != null ? PlayerProfile.TrySetCasualOutfit(catalog, l) : PlayerProfile.CostumeEquipResult.InvalidPart;
            if (res == PlayerProfile.CostumeEquipResult.Equipped) { UIFeedback.Equip(); _picked = PlayerProfile.GetPart(_slot); }
            else { UIFeedback.Error(); Toast.Show("That look has a piece you no longer own"); }
        }

        void SaveLook()
        {
            int index = PlayerProfile.LookCount < PlayerProfile.MaxLooks ? PlayerProfile.LookCount : 0;
            if (PlayerProfile.SaveLook(index)) { UIFeedback.Confirm(); Toast.Show($"Saved as look {index + 1}"); Refresh(); }
        }

        public void Refresh()
        {
            if (!IsShown && !gameObject.activeInHierarchy) return;
            if (coinLabel != null) coinLabel.text = HomeScreen.Short(PlayerProfile.Coin);
            if (gemLabel != null) gemLabel.text = HomeScreen.Short(PlayerProfile.Gem);

            for (int i = 0; i < hotspots.Length; i++)
            {
                if (hotspots[i] == null) continue;
                bool on = HotspotSlots[i] == _slot;
                if (hotspots[i].targetGraphic is Image img) img.color = on ? Yellow : Quiet;
                var t = hotspots[i].GetComponentInChildren<TMP_Text>(true); if (t != null) t.color = on ? OnYellow : Ink;
            }
            for (int i = 0; i < looks.Length; i++)
            {
                if (looks[i] == null) continue;
                bool has = i < PlayerProfile.LookCount;
                looks[i].gameObject.SetActive(has);
                if (looks[i].targetGraphic is Image img) img.color = PlayerProfile.IsWearingLook(i) ? Yellow : Quiet;
            }
            if (saveLook != null) saveLook.gameObject.SetActive(true);

            var def = catalog != null ? catalog.GetSlotDefinition(_slot) : null;
            if (slotTitle != null) slotTitle.text = (def != null ? def.displayName : _slot).ToUpperInvariant();
            int owned = _parts.Count(p => PlayerProfile.IsCostumeOwned(p.itemId));
            if (ownedLabel != null) ownedLabel.text = $"{owned} / {_parts.Count} OWNED";
            int gachaOnly = _parts.Count(p => economy != null && economy.TryGetCostume(p.itemId, out var e) && e.source == AcquireSource.Gacha);
            if (gachaTag != null) gachaTag.SetActive(gachaOnly > 0);
            if (gachaTagText != null) gachaTagText.text = $"GACHA ONLY {gachaOnly}";

            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] == null) continue;
                bool has = i < _slots.Count; chips[i].gameObject.SetActive(has);
                if (!has) continue;
                bool on = _slots[i].id == _slot;
                if (chips[i].targetGraphic is Image img) img.color = on ? Ink : Quiet;
                var t = chips[i].GetComponentInChildren<TMP_Text>(true);
                if (t != null) { t.text = _slots[i].displayName.ToUpperInvariant(); t.color = on ? new Color(0.149f, 0.165f, 0.212f) : new Color(0.725f, 0.749f, 0.8f); }
            }

            for (int i = 0; i < films.Length; i++)
            {
                var f = films[i]; if (f?.button == null) continue;
                bool has = i < _parts.Count; f.button.gameObject.SetActive(has);
                if (!has) continue;
                var p = _parts[i];
                EconomyConfig.CostumeEntry item = default;
                bool known = economy != null && economy.TryGetCostume(p.itemId, out item);
                bool own = PlayerProfile.IsCostumeOwned(p.itemId);
                if (f.bar != null) f.bar.color = Rarity[Mathf.Clamp(known ? (int)item.rarity : 0, 0, 4)];
                if (f.icon != null) { f.icon.sprite = p.icon; f.icon.enabled = p.icon != null; f.icon.preserveAspect = true; }
                if (f.price != null)
                {
                    f.price.text = own ? "OWNED" : !known ? "-" : item.source == AcquireSource.Gacha ? "GACHA" : $"{item.price:N0}";
                    f.price.color = own ? new Color(0.357f, 0.839f, 0.541f) : known && item.source == AcquireSource.Gacha ? new Color(0.949f, 0.6f, 0.29f)
                                  : known && item.currency == WalletCurrency.Gem ? new Color(0.788f, 0.655f, 1f) : Ink;
                }
                if (f.selected != null) f.selected.SetActive(p.itemId == _picked);
            }

            var picked = _parts.FirstOrDefault(p => p.itemId == _picked);
            bool pickedOwned = !string.IsNullOrEmpty(_picked) && PlayerProfile.IsCostumeOwned(_picked);
            EconomyConfig.CostumeEntry pi = default;
            bool piKnown = economy != null && !string.IsNullOrEmpty(_picked) && economy.TryGetCostume(_picked, out pi);
            if (pickedIcon != null) { pickedIcon.sprite = picked.icon; pickedIcon.enabled = picked.icon != null; pickedIcon.preserveAspect = true; }
            if (pickedName != null) pickedName.text = piKnown ? pi.displayName : string.IsNullOrEmpty(picked.name) ? "Nothing" : picked.name;
            if (pickedState != null) pickedState.text = (pickedOwned ? "WEARING" : "TRYING ON") + (piKnown ? " · " + pi.rarity.ToString().ToUpperInvariant() : "");
            if (actionLabel != null)
                actionLabel.text = pickedOwned || !piKnown ? "WEARING" : pi.source == AcquireSource.Gacha ? "IN GACHA" : $"BUY {pi.price:N0}{(pi.currency == WalletCurrency.Gem ? " GEMS" : "")}";
            if (actionButton != null) actionButton.gameObject.SetActive(!pickedOwned && piKnown);
        }
    }
}
