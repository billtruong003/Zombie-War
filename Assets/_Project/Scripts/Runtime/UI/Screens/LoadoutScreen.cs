using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Màn 02 LOADOUT (spec §4.2). Presentation authored sẵn (installer bake card cho từng
    /// WeaponData); runtime CHỈ bind data + selection. Chọn card = preview-only (đổi info panel
    /// + slot 0) — equipment persistence thật thuộc phase sau, không lưu gì ở đây.
    /// </summary>
    public sealed class LoadoutScreen : UIScreen
    {
        [Header("Nav")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button shopLinkButton;
        [SerializeField] private UIScreen shopScreen;

        [Header("Data (prototype)")]
        [SerializeField] private UIPrototypeCatalog catalog;

        [Header("Authored views")]
        [SerializeField] private LoadoutSlotView[] slotViews;      // 3 equipped slots
        [SerializeField] private WeaponItemCardView[] ownedCards;  // 1 card / WeaponData, bake sẵn
        [SerializeField] private TMP_Text infoNameLabel;
        [SerializeField] private Image[] statBars;                 // DMG / TỐC BẮN / TẦM fills

        private WeaponItemCardView _selected;

        protected override void Awake()
        {
            base.Awake();
            Wire(backButton, () => UIManager.Instance.Pop());
            Wire(shopLinkButton, () =>
            {
                if (shopScreen != null) UIManager.Instance.Push(shopScreen);
            });
            if (ownedCards != null)
                foreach (var card in ownedCards)
                {
                    var c = card;
                    if (c != null && c.button != null)
                        c.button.onClick.AddListener(() => Select(c));
                }
        }

        protected override void OnShow()
        {
            // Bind lại lock state (cheat unlock có thể đổi trong Editor giữa 2 lần mở).
            // Grid kho KHÔNG dùng badge/price (chỉ shop) — đừng gọi SetOwned kẻo badge đè tên.
            if (ownedCards == null || catalog == null) return;
            foreach (var card in ownedCards)
            {
                if (card == null || card.data == null) continue;
                bool owned = catalog.IsWeaponOwned(card.data);
                if (card.lockOverlay != null) card.lockOverlay.SetActive(!owned);
                if (card.ownedBadge != null) card.ownedBadge.SetActive(false);
            }
            if (_selected == null && ownedCards.Length > 0 && ownedCards[0] != null)
                Select(ownedCards[0]);
        }

        public override bool OnEscape() { UIManager.Instance.Pop(); return true; }

        private void Select(WeaponItemCardView card)
        {
            if (card == null || card.data == null) return;
            if (_selected != null) _selected.SetSelected(false);
            _selected = card;
            card.SetSelected(true);

            var d = card.data;
            if (infoNameLabel != null)
                infoNameLabel.text =
                    $"{d.weaponName} · <color=#{ColorUtility.ToHtmlStringRGB(d.TierColor)}>{d.tier.ToString().ToUpperInvariant()}</color>";

            // Stat bar chuẩn hoá thô cho prototype (đủ để so sánh tương đối giữa các súng)
            SetStat(0, d.damage / 60f);
            SetStat(1, d.fireRate / 15f);
            SetStat(2, d.range / 60f);

            // Preview-only: slot 0 nhận súng đang chọn
            if (slotViews != null && slotViews.Length > 0 && slotViews[0] != null && catalog != null)
                slotViews[0].Bind(d, catalog.GetWeaponIcon(d));
        }

        private void SetStat(int i, float v01)
        {
            if (statBars == null || i >= statBars.Length || statBars[i] == null) return;
            var rt = statBars[i].rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(v01), 1f);
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
