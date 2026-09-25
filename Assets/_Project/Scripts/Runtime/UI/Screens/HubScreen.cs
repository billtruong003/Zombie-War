using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// The Hub: PLAY enters the endless world, a 5-tab dock (HOME/LOADOUT/SHOP/COSTUME/PASS), the
    /// currency cluster, the best survival time under the avatar and a mission card bound to the Pass.
    /// Notify dots are authored in the prefab (Icon/Notify + UIFxPulse); runtime only toggles them.
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
        [SerializeField] private Button coinPlusButton;
        [SerializeField] private Button gemPlusButton;
        [SerializeField] private Button missionButton;

        [Header("Labels")]
        [SerializeField] private TMP_Text recordLabel;
        [SerializeField] private TMP_Text missionNameLabel;
        [SerializeField] private TMP_Text missionRewardLabel;

        [Header("Điều hướng")]
        [SerializeField] private UIScreen loadoutScreen;
        [SerializeField] private UIScreen shopScreen;
        [SerializeField] private UIScreen costumeScreen;
        [SerializeField] private UIScreen passScreen;
        [SerializeField] private UIScreen settingsScreen;

        protected override void Awake()
        {
            base.Awake();
            Wire(playButton, Play);
            Wire(loadoutButton, () => Open(loadoutScreen, "LOADOUT"));
            Wire(shopButton, () => OpenShop(0));
            Wire(costumeButton, () => Open(costumeScreen, "COSTUME"));
            Wire(passButton, () => Open(passScreen, "BATTLE PASS"));
            Wire(settingsButton, () => Open(settingsScreen, "SETTINGS"));
            // "+" mở boundary thương mại đang tồn tại (Shop). IAP/earn flow riêng chưa có —
            // documented gap: Coin+ → Shop Weapons, Gem+ → Shop Gacha.
            Wire(coinPlusButton, () => OpenShop(0));
            Wire(gemPlusButton, () => OpenShop(1));
            Wire(missionButton, () => Open(passScreen, "BATTLE PASS"));
        }

        // Guards against a double tap loading the map twice: the scene load is async, so a second
        // press before GameplayState is entered would otherwise start a second additive load.
        private bool _launching;

        private void Play()
        {
            if (_launching) return;
            _launching = true;
            GameFlow.StartGameplay();
        }

        private void OnEnable()
        {
            _launching = false;   // back on the Hub: PLAY is armed again
            PlayerProfile.MissionsChanged += RefreshMissionUi;
            PlayerProfile.LoadoutChanged += RefreshBadges;
            PlayerProfile.CostumeChanged += RefreshBadges;
        }

        private void OnDisable()
        {
            PlayerProfile.MissionsChanged -= RefreshMissionUi;
            PlayerProfile.LoadoutChanged -= RefreshBadges;
            PlayerProfile.CostumeChanged -= RefreshBadges;
        }

        protected override void OnShow() { RefreshAll(); }
        protected override void OnFocus() { RefreshAll(); }

        public override bool OnEscape() => true;   // HUB là root — back không pop

        private void RefreshAll()
        {
            PlayerProfile.RefreshMissionWindow(DateTime.UtcNow);
            RefreshRecord();
            RefreshBadges();
            RefreshMissionCard();
        }

        private void RefreshMissionUi()
        {
            RefreshBadges();
            RefreshMissionCard();
        }

        /// Notify dot authored trong prefab tại TabButton/Icon/Notify. LOADOUT = súng gacha chưa xem,
        /// COSTUME = skin chưa xem, PASS = có mission claim được. HOME/SHOP chưa có tín hiệu → luôn tắt.
        private void RefreshBadges()
        {
            SetNotify(loadoutButton, PlayerProfile.HasUnseenWeapon());
            SetNotify(costumeButton, PlayerProfile.HasUnseenCostume());
            SetNotify(passButton, HasClaimableMission());
            SetNotify(shopButton, false);
        }

        private static void SetNotify(Button host, bool on)
        {
            if (host == null) return;
            var dot = host.transform.Find("Icon/Notify");
            if (dot != null) dot.gameObject.SetActive(on);
        }

        private static bool HasClaimableMission()
        {
            foreach (var m in PassMissions.ActiveFor(DateTime.UtcNow))
                if (PlayerProfile.IsMissionComplete(m) && !PlayerProfile.IsMissionClaimed(m.id))
                    return true;
            return false;
        }

        /// Mission card = mission Pass đang active gần hoàn thành nhất (ưu tiên claim được).
        /// Tap card → mở màn Pass.
        private void RefreshMissionCard()
        {
            if (missionNameLabel == null && missionRewardLabel == null) return;

            PassMission best = null;
            float bestScore = -1f;
            foreach (var m in PassMissions.ActiveFor(DateTime.UtcNow))
            {
                if (PlayerProfile.IsMissionClaimed(m.id)) continue;
                bool complete = PlayerProfile.IsMissionComplete(m);
                float score = complete ? 2f : PlayerProfile.GetMissionProgress(m.id) / (float)m.target;
                if (score > bestScore) { bestScore = score; best = m; }
            }

            if (best == null)
            {
                if (missionNameLabel != null) missionNameLabel.text = "ALL MISSIONS CLAIMED";
                if (missionRewardLabel != null) missionRewardLabel.text = "—";
                return;
            }

            bool claimable = PlayerProfile.IsMissionComplete(best);
            if (missionNameLabel != null)
                missionNameLabel.text = claimable ? $"{best.title} — CLAIM!" : best.title;
            if (missionRewardLabel != null)
                missionRewardLabel.text = $"+{best.coinReward}";
        }

        private void RefreshRecord()
        {
            if (recordLabel == null) return;
            int best = Mathf.FloorToInt(PlayerProfile.BestSurvivalSeconds);
            recordLabel.text = best > 0 ? HudController.FormatClock(best) : "—";
        }

        private void OpenShop(int tab)
        {
            if (shopScreen is ShopScreen shop) shop.OpenTab(tab);
            Open(shopScreen, "SHOP");
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
