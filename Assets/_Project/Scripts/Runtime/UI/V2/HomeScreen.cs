using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Home (owner-approved Meta v2 mockup): profile chip with the account level ring, wallet,
    /// settings; side rails (Daily, Events, Mail, Rank | Gacha, Pass, Starter) with count badges and
    /// level locks; the 3D character (tap = Studio); the equipped gun card; an info strip; missions;
    /// "next buy"; PLAY. The same screen plays the first-day state: locked rails show the level that
    /// opens them and a hand points at PLAY until the first run is played.
    /// Built by HordeCall/UI v2/Build Home; every reference here is wired by that builder.
    /// </summary>
    public sealed class HomeScreen : UIScreen
    {
        [Serializable]
        public sealed class Rail
        {
            public Button button;
            public GameObject badge;
            public TMP_Text badgeCount;
            public TMP_Text sub;          // timer or "LV 3"
            public GameObject lockIcon;
            public Image icon;
        }

        [Serializable]
        public sealed class MissionRow
        {
            public GameObject root;
            public TMP_Text title;
            public GameObject claimTag;
            public RectTransform bar;
        }

        [Header("Top bar")]
        [SerializeField] private Button profileButton;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private Image xpRing;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text bestLabel;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text gemLabel;
        [SerializeField] private Button coinPlus;
        [SerializeField] private Button gemPlus;
        [SerializeField] private Button settingsButton;

        [Header("Rails")]
        [SerializeField] private Rail daily;
        [SerializeField] private Rail events;
        [SerializeField] private Rail mail;
        [SerializeField] private Rail rank;
        [SerializeField] private Rail gacha;
        [SerializeField] private Rail pass;
        [SerializeField] private Rail starter;

        [Header("Stage")]
        [SerializeField] private Button stageButton;
        [SerializeField] private Button outfitButton;

        [Header("Gun card")]
        [SerializeField] private Button gunCard;
        [SerializeField] private Image gunTile;
        [SerializeField] private Image gunIcon;
        [SerializeField] private TMP_Text gunName;
        [SerializeField] private TMP_Text gunTier;
        [SerializeField] private Image gunTierBg;
        [SerializeField] private Image[] gunStars = new Image[3];
        [SerializeField] private TMP_Text gunMeta;

        [Header("Strip, missions, next buy")]
        [SerializeField] private Button stripDaily;
        [SerializeField] private TMP_Text stripDailyText;
        [SerializeField] private Button stripGacha;
        [SerializeField] private Button stripPass;
        [SerializeField] private TMP_Text stripPassText;
        [SerializeField] private Button missionsCard;
        [SerializeField] private TMP_Text missionsHeader;
        [SerializeField] private MissionRow[] missionRows = new MissionRow[3];
        [SerializeField] private Button nextBuyCard;
        [SerializeField] private Image nextBuyIcon;
        [SerializeField] private TMP_Text nextBuyName;
        [SerializeField] private RectTransform nextBuyBar;
        [SerializeField] private TMP_Text nextBuyValue;

        [Header("Play")]
        [SerializeField] private Button playButton;
        [SerializeField] private TMP_Text playSub;
        [SerializeField] private GameObject playHint;
        [SerializeField] private NavBarV2 nav;

        [Header("Targets")]
        [SerializeField] private UIPrototypeCatalog catalog;
        [SerializeField] private UIScreen profileScreen;
        [SerializeField] private UIScreen settingsScreen;
        [SerializeField] private UIScreen studioScreen;
        [SerializeField] private UIScreen dailyScreen;
        [SerializeField] private UIScreen shopScreen;
        [SerializeField] private UIScreen gachaScreen;
        [SerializeField] private UIScreen passScreen;
        [SerializeField] private UIScreen arsenalScreen;

        bool _launching;

        protected override void Awake()
        {
            base.Awake();
            On(profileButton, () => Open(profileScreen));
            On(settingsButton, () => Open(settingsScreen));
            On(coinPlus, () => Open(shopScreen));
            On(gemPlus, () => Open(shopScreen));
            On(daily?.button, () => Open(dailyScreen));
            On(events?.button, () => OpenGated(gachaScreen, AccountProgress.Feature.Events));
            On(gacha?.button, () => OpenGated(gachaScreen, AccountProgress.Feature.Gacha));
            On(pass?.button, () => OpenGated(passScreen, AccountProgress.Feature.Pass));
            On(starter?.button, () => Open(shopScreen));
            On(stageButton, () => Open(studioScreen));
            On(outfitButton, () => Open(studioScreen));
            On(gunCard, () => Open(arsenalScreen));
            On(stripDaily, () => Open(dailyScreen));
            On(stripGacha, () => OpenGated(gachaScreen, AccountProgress.Feature.Gacha));
            On(stripPass, () => OpenGated(passScreen, AccountProgress.Feature.Pass));
            On(missionsCard, () => OpenGated(passScreen, AccountProgress.Feature.Missions));
            On(nextBuyCard, () => Open(shopScreen));
            On(playButton, Play);
            // Server features are designed, not built (owner rule).
            if (mail?.button != null) mail.button.gameObject.SetActive(FeatureFlags.Backend);
            if (rank?.button != null) rank.button.gameObject.SetActive(FeatureFlags.Backend);
        }

        private void OnEnable()
        {
            _launching = false;
            PlayerProfile.MissionsChanged += Refresh;
            PlayerProfile.LoadoutChanged += Refresh;
            PlayerProfile.AccountChanged += Refresh;
        }

        private void OnDisable()
        {
            PlayerProfile.MissionsChanged -= Refresh;
            PlayerProfile.LoadoutChanged -= Refresh;
            PlayerProfile.AccountChanged -= Refresh;
        }

        protected override void OnShow() => Refresh();
        protected override void OnFocus() => Refresh();
        public override bool OnEscape() => true;   // root screen

        void Play()
        {
            if (_launching) return;
            _launching = true;
            UIFeedback.Confirm();
            GameFlow.StartGameplay();
        }

        void Open(UIScreen s)
        {
            if (s == null) { Toast.Show("Coming soon"); return; }
            UIFeedback.Tap();
            UIManager.Instance?.Push(s);
        }

        void OpenGated(UIScreen s, AccountProgress.Feature f)
        {
            if (!AccountProgress.IsUnlocked(f))
            {
                UIFeedback.Error();
                Toast.Show($"Unlocks at level {AccountProgress.RequiredLevel(f)}");
                return;
            }
            Open(s);
        }

        // ------------------------------------------------------------------ refresh
        public void Refresh()
        {
            PlayerProfile.RefreshMissionWindow(DateTime.UtcNow);
            int level = PlayerProfile.AccountLevel;
            int xp = PlayerProfile.AccountXp;
            Set(levelLabel, level.ToString());
            if (xpRing != null) xpRing.fillAmount = AccountProgress.Progress01(xp);
            Set(nameLabel, PlayerProfile.DisplayName);
            int best = Mathf.FloorToInt(PlayerProfile.BestSurvivalSeconds);
            Set(bestLabel, best > 0 ? "BEST " + HudController.FormatClock(best) : "NEW PLAYER");
            Set(coinLabel, Short(PlayerProfile.Coin));
            Set(gemLabel, Short(PlayerProfile.Gem));

            int claimable = ClaimableMissions();
            int today = DailyRewards.Today;
            int dailyCount = DailyRewards.ClaimableCount(today);
            RailState(daily, true, dailyCount, "", "");
            RailState(events, AccountProgress.IsUnlocked(AccountProgress.Feature.Events), 0, "", "LV " + AccountProgress.RequiredLevel(AccountProgress.Feature.Events));
            RailState(gacha, AccountProgress.IsUnlocked(AccountProgress.Feature.Gacha), 0, "", "LV " + AccountProgress.RequiredLevel(AccountProgress.Feature.Gacha));
            RailState(pass, AccountProgress.IsUnlocked(AccountProgress.Feature.Pass), claimable, "", "LV " + AccountProgress.RequiredLevel(AccountProgress.Feature.Pass));
            RailState(starter, true, 0, "OFFER", "");

            RefreshGun();
            RefreshMissions(claimable);
            RefreshNextBuy();
            Set(stripDailyText, DailyRewards.CanStamp(today) ? $"Stamp day {DailyRewards.Stamps + 1}" : DailyRewards.Stamps >= DailyRewards.CardDays ? "Card complete" : "Back tomorrow");
            Set(stripPassText, AccountProgress.IsUnlocked(AccountProgress.Feature.Pass) ? $"PASS LV {PassLevel()}" : "LV 2 UNLOCKS");

            bool firstRun = PlayerProfile.RunsPlayed == 0;
            if (playHint != null) playHint.SetActive(firstRun);
            Set(playSub, best > 0 ? $"Beat your best {HudController.FormatClock(best)}" : "Survive as long as you can");
            if (nav != null) nav.SetDot(4, claimable > 0 && AccountProgress.IsUnlocked(AccountProgress.Feature.Pass));
        }

        static int PassLevel() => 1 + PlayerProfile.PassXp / 500;

        static void RailState(Rail r, bool unlocked, int count, string sub, string lockedSub)
        {
            if (r == null) return;
            if (r.lockIcon != null) r.lockIcon.SetActive(!unlocked);
            if (r.icon != null) r.icon.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            if (r.badge != null) r.badge.SetActive(unlocked && count > 0);
            if (r.badgeCount != null) r.badgeCount.text = count.ToString();
            if (r.sub != null)
            {
                r.sub.text = unlocked ? sub : lockedSub;
                r.sub.gameObject.SetActive(!string.IsNullOrEmpty(r.sub.text));
            }
        }

        void RefreshGun()
        {
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            var d = LoadoutState.Resolve(PlayerProfile.EquippedWeaponId, all);
            if (d == null) { Set(gunName, "Pistol"); Set(gunMeta, "STARTER"); return; }
            Set(gunName, d.weaponName);
            Set(gunTier, d.tier.ToString().ToUpperInvariant());
            if (gunTierBg != null) gunTierBg.color = d.TierColor;
            if (gunTile != null) gunTile.color = d.TileColor;
            var icon = catalog != null ? catalog.GetWeaponIcon(d, true) : null;
            if (gunIcon != null) { gunIcon.enabled = icon != null; if (icon != null) { gunIcon.sprite = icon; gunIcon.preserveAspect = true; } }
            int stars = Mathf.Clamp(PlayerProfile.GetWeaponLevel(d.WeaponId), 1, 3);
            for (int i = 0; i < gunStars.Length; i++)
                if (gunStars[i] != null) gunStars[i].color = i < stars ? Color.white : new Color(0.25f, 0.27f, 0.33f, 1f);
            int power = Mathf.RoundToInt(CombatPower.WeaponPower(d, stars));
            Set(gunMeta, $"{HubScreen.FamilyName(d.weaponClass)} · POWER {power:N0}");
        }

        static int ClaimableMissions() =>
            PassMissions.ActiveFor(DateTime.UtcNow).Count(m => PlayerProfile.IsMissionComplete(m) && !PlayerProfile.IsMissionClaimed(m.id));

        void RefreshMissions(int claimable)
        {
            bool unlocked = AccountProgress.IsUnlocked(AccountProgress.Feature.Missions);
            Set(missionsHeader, unlocked ? (claimable > 0 ? $"MISSIONS · {claimable} READY" : "MISSIONS") : "MISSIONS · LV 2");
            var list = PassMissions.ActiveFor(DateTime.UtcNow)
                .Where(m => !PlayerProfile.IsMissionClaimed(m.id))
                .OrderByDescending(m => PlayerProfile.IsMissionComplete(m) ? 2f : PlayerProfile.GetMissionProgress(m.id) / (float)m.target)
                .Take(missionRows.Length).ToList();
            for (int i = 0; i < missionRows.Length; i++)
            {
                var row = missionRows[i];
                if (row == null || row.root == null) continue;
                bool has = i < list.Count;
                row.root.SetActive(has);
                if (!has) continue;
                var m = list[i];
                bool done = PlayerProfile.IsMissionComplete(m);
                Set(row.title, m.title);
                if (row.claimTag != null) row.claimTag.SetActive(done && unlocked);
                if (row.bar != null)
                {
                    row.bar.parent.gameObject.SetActive(!done || !unlocked);
                    UIBarClip.Set(row.bar, PlayerProfile.GetMissionProgress(m.id) / (float)Mathf.Max(1, m.target));
                }
            }
        }

        void RefreshNextBuy()
        {
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            var next = all?.Where(w => w != null && w.price > 0 && !PlayerProfile.IsWeaponOwned(w.WeaponId))
                          .OrderBy(w => w.price).FirstOrDefault();
            if (nextBuyCard != null) nextBuyCard.gameObject.SetActive(next != null);
            if (next == null) return;
            Set(nextBuyName, next.weaponName);
            long coins = PlayerProfile.Coin;
            UIBarClip.Set(nextBuyBar, Mathf.Clamp01(coins / (float)next.price));
            Set(nextBuyValue, coins >= next.price ? "READY TO BUY" : $"{coins:N0} / {next.price:N0}");
            var icon = catalog != null ? catalog.GetWeaponIcon(next, false) : null;
            if (nextBuyIcon != null) { nextBuyIcon.enabled = icon != null; if (icon != null) { nextBuyIcon.sprite = icon; nextBuyIcon.preserveAspect = true; } }
        }

        // ------------------------------------------------------------------ helpers
        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }
        static void On(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }

        public static string Short(long v) =>
            v >= 1_000_000 ? (v / 1_000_000f).ToString("0.#") + "M" :
            v >= 10_000 ? (v / 1000f).ToString("0.#") + "K" : v.ToString("N0");
    }
}
