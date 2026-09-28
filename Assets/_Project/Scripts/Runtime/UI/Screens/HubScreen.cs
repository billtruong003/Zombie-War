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

        [Header("M8 equipped weapon plate")]
        [SerializeField] private UIPrototypeCatalog catalog;
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image weaponTile;
        [SerializeField] private TMP_Text weaponNameLabel;
        [SerializeField] private TMP_Text weaponMetaLabel;
        [SerializeField] private RectTransform damageFill;
        [SerializeField] private RectTransform rateFill;
        [SerializeField] private Button changeWeaponButton;
        [SerializeField] private TMP_Text playSubLabel;

        protected override void Awake()
        {
            base.Awake();
            Wire(playButton, Play);
            Wire(loadoutButton, () => Open(loadoutScreen, "LOADOUT"));
            Wire(shopButton, () => OpenShop(ShopScreen.WeaponsTab));
            Wire(costumeButton, () => Open(costumeScreen, "COSTUME"));
            Wire(passButton, () => Open(passScreen, "BATTLE PASS"));
            Wire(settingsButton, () => Open(settingsScreen, "SETTINGS"));
            // "+" opens where that currency is spent: Coin buys weapons, Gem buys outfits.
            // There is no IAP yet, so nothing sells the currency itself.
            Wire(coinPlusButton, () => OpenShop(ShopScreen.WeaponsTab));
            Wire(gemPlusButton, () => OpenShop(ShopScreen.CostumeTab));
            Wire(missionButton, () => Open(passScreen, "BATTLE PASS"));
            Wire(changeWeaponButton, () => Open(loadoutScreen, "LOADOUT"));
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
            PlayerProfile.LoadoutChanged += RefreshWeaponPlate;
            PlayerProfile.CostumeChanged += RefreshBadges;
        }

        private void OnDisable()
        {
            PlayerProfile.MissionsChanged -= RefreshMissionUi;
            PlayerProfile.LoadoutChanged -= RefreshBadges;
            PlayerProfile.LoadoutChanged -= RefreshWeaponPlate;
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
            RefreshWeaponPlate();
        }

        // The run weapon, shown on the Hub so the player sees what PLAY will start with (M8 mockup).
        // Stat bars compare against the strongest gun in the catalog, so they read as "how good".
        private void RefreshWeaponPlate()
        {
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            var d = LoadoutState.Resolve(PlayerProfile.EquippedWeaponId, all);
            if (weaponNameLabel != null) weaponNameLabel.text = d != null ? d.weaponName : "No weapon";
            if (weaponMetaLabel != null)
                weaponMetaLabel.text = d == null ? "" :
                    $"<color=#{ColorUtility.ToHtmlStringRGB(d.TierColor)}>{d.tier.ToString().ToUpperInvariant()}</color> · {FamilyName(d.weaponClass)}";
            if (weaponIcon != null)
            {
                var sprite = d != null && catalog != null ? catalog.GetWeaponIcon(d, true) : null;   // the equipped gun is owned
                weaponIcon.enabled = sprite != null;
                if (sprite != null) { weaponIcon.sprite = sprite; weaponIcon.color = Color.white; weaponIcon.preserveAspect = true; }
            }
            if (weaponTile != null && d != null) weaponTile.color = d.TileColor;

            float maxDamage = 1f, maxRate = 1f;
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null)
                    {
                        maxDamage = Mathf.Max(maxDamage, all[i].damage * Mathf.Max(1, all[i].pelletCount));
                        maxRate = Mathf.Max(maxRate, all[i].fireRate);
                    }
            SetFill(damageFill, d != null ? d.damage * Mathf.Max(1, d.pelletCount) / maxDamage : 0f);
            SetFill(rateFill, d != null ? d.fireRate / maxRate : 0f);
        }

        private static void SetFill(RectTransform fill, float v)
        {
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Clamp01(Mathf.Max(0.04f, v)), 1f);
        }

        public static string FamilyName(WeaponClass c) => c switch
        {
            WeaponClass.Sidearm => "PISTOL",
            WeaponClass.SMG => "SMG",
            WeaponClass.AssaultRifle => "RIFLE",
            WeaponClass.Shotgun => "SHOTGUN",
            WeaponClass.Marksman => "SNIPER",
            WeaponClass.LMG => "LMG",
            WeaponClass.Rocket => "LAUNCHER",
            _ => c.ToString().ToUpperInvariant(),
        };

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
            if (playSubLabel != null)
                playSubLabel.text = best > 0 ? $"Beat your best: {HudController.FormatClock(best)}" : "Survive as long as you can";
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
