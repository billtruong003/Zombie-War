using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Màn 08 BATTLE PASS (spec §4.5): track thưởng free + premium strip (khoá) + quest hôm nay.
    /// Presentation-only — backend battle pass/quest chưa tồn tại; CLAIM và GachaLink fail-safe.
    /// </summary>
    public sealed class PassScreen : UIScreen
    {
        [Header("Nav")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button gachaLinkButton;
        [SerializeField] private ShopScreen shopScreen;

        [Header("Quests (placeholder)")]
        [SerializeField] private Button[] claimButtons;

        protected override void Awake()
        {
            base.Awake();
            Wire(backButton, () => UIManager.Instance.Pop());
            Wire(gachaLinkButton, () =>
            {
                if (shopScreen == null) return;
                shopScreen.OpenTab(1);   // "Quay Gacha" phải mở đúng tab Gacha, không phải Weapons
                UIManager.Instance.Push(shopScreen);
            });
            if (claimButtons != null)
                foreach (var b in claimButtons)
                    Wire(b, () => Debug.Log("[PassScreen] CLAIM: backend battle pass chưa có (placeholder)."));
        }

        public override bool OnEscape() { UIManager.Instance.Pop(); return true; }

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
