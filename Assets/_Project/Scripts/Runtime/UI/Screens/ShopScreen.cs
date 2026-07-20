using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Màn 04–07 SHOP (spec §4.4): shell chung + SegmentedTabs 4 tab
    /// (WEAPONS · GACHA · COSTUME · UPGRADES). Mỗi tab = 1 content panel bật/tắt.
    /// Active tab = fill pill màu ngữ cảnh (GACHA gold, còn lại green — spec §3.6).
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

        [Header("Prototype weapon cards (bake sẵn — click = selected visual, không mua thật)")]
        [SerializeField] private WeaponItemCardView[] weaponCards;

        private int _active;
        private int _pendingTab;
        private WeaponItemCardView _selectedCard;

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
                        c.button.onClick.AddListener(() => SelectCard(c));
                }
        }

        private void SelectCard(WeaponItemCardView card)
        {
            if (_selectedCard != null) _selectedCard.SetSelected(false);
            _selectedCard = card;
            if (card != null) card.SetSelected(true);
        }

        /// <summary>Đặt tab sẽ mở (0 Weapons · 1 Gacha · 2 Costume · 3 Upgrades) — gọi TRƯỚC Push.
        /// Pass "Quay Gacha" dùng để mở thẳng Gacha; Hub mở mặc định Weapons (pending reset sau mỗi Show).</summary>
        public void OpenTab(int index)
        {
            _pendingTab = Mathf.Clamp(index, 0, tabPanels != null ? tabPanels.Length - 1 : 0);
            if (IsShown) SelectTab(_pendingTab);
        }

        protected override void OnShow()
        {
            SelectTab(_pendingTab);
            _pendingTab = 0;
        }

        public override bool OnEscape() { UIManager.Instance.Pop(); return true; }

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

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
