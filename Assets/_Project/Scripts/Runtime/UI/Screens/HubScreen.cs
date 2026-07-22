using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Màn 01 HUB (wireframe): PLAY lớn giữa-dưới, dock 4 nút (LOADOUT/SHOP/COSTUME/PASS),
    /// currency cluster góc phải trên, settings góc trái trên, record (best score) dưới title.
    /// SHOP/PASS chưa có screen (UI-4/5) → push nếu được gán, không thì toast log.
    /// </summary>
    public sealed class HubScreen : UIScreen
    {
        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button loadoutButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button costumeButton;
        [SerializeField] private Button passButton;
        [SerializeField] private Button settingsButton;

        [Header("Labels")]
        [SerializeField] private TMP_Text recordLabel;

        [Header("Điều hướng")]
        [SerializeField] private UIScreen loadoutScreen;
        [SerializeField] private UIScreen shopScreen;
        [SerializeField] private UIScreen costumeScreen;
        [SerializeField] private UIScreen passScreen;
        [SerializeField] private UIScreen settingsScreen;

        protected override void Awake()
        {
            base.Awake();
            Wire(playButton, GameFlow.StartGameplay);
            Wire(loadoutButton, () => Open(loadoutScreen, "LOADOUT"));
            Wire(shopButton, () => Open(shopScreen, "SHOP"));
            Wire(costumeButton, () => Open(costumeScreen, "COSTUME"));
            Wire(passButton, () => Open(passScreen, "BATTLE PASS"));
            Wire(settingsButton, () => Open(settingsScreen, "SETTINGS"));
        }

        private GameObject _loadoutBadge, _costumeBadge;

        protected override void OnShow() { RefreshRecord(); RefreshBadges(); }
        protected override void OnFocus() { RefreshRecord(); RefreshBadges(); }

        public override bool OnEscape() => true;   // HUB là root — back không pop

        /// Badge "item mới" (Slice 7, minimal): chấm đỏ trên LOADOUT khi có súng gacha chưa xem,
        /// trên COSTUME khi có skin chưa xem. Clear khi mở màn tương ứng (LoadoutScreen/CostumeScreen).
        private void RefreshBadges()
        {
            if (loadoutButton != null)
                SetBadge(ref _loadoutBadge, loadoutButton, PlayerProfile.HasUnseenWeapon());
            if (costumeButton != null)
                SetBadge(ref _costumeBadge, costumeButton, PlayerProfile.HasUnseenCostume());
        }

        private static void SetBadge(ref GameObject badge, Button host, bool on)
        {
            if (badge == null)
            {
                badge = new GameObject("NewBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)badge.transform;
                rt.SetParent(host.transform, false);
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-6f, -6f);
                rt.sizeDelta = new Vector2(20f, 20f);
                var img = badge.GetComponent<Image>();
                img.color = UITheme.Danger;
                img.raycastTarget = false;
            }
            badge.SetActive(on);
        }

        private void RefreshRecord()
        {
            if (recordLabel == null) return;
            int best = PlayerPrefs.GetInt("best_score", 0);
            recordLabel.text = best > 0
                ? $"WAVE {CurrencyClusterWidget.Format(best)}"
                : "—";
        }

        private void Open(UIScreen screen, string label)
        {
            if (screen != null) { UIManager.Instance.Push(screen); return; }
            Debug.Log($"[HubScreen] {label}: màn chưa được wire (Validate All UI References).");
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
