using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ZombieWar.UI.UIBind;

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
        [Tooltip("G12.8: the gacha strip's title and sub line.")]
        [SerializeField] private TMP_Text stripGachaTitle;
        [SerializeField] private TMP_Text stripGachaSub;
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

        [Header("First run (FTUE)")]
        [Tooltip("Shown instead of missions + next buy until the first run is played.")]
        [SerializeField] private GameObject firstRunCard;
        [Tooltip("v2 post-run popup, kept hidden: the v3 gun step replaced it.")]
        [SerializeField] private GameObject revealRoot;
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

        /// Set once the post-first-run reveal was shown.

        /// Owner (2026-09-27): the first session starts in the menu with only PLAY; everything else
        /// opens after the first run.
        public static bool FirstRunPending => PlayerProfile.RunsPlayed == 0;

        /// Blocks a tap on something locked (<see cref="AccountProgress.LockedMessage(AccountProgress.Feature?)"/>).
        /// True = blocked (toast shown).
        public static bool Gate(AccountProgress.Feature? feature = null)
        {
            string locked = AccountProgress.LockedMessage(feature);
            if (locked == null) return false;
            UIFeedback.Error();
            Toast.Show(locked);
            return true;
        }

        /// Blocks a tap while the first run is pending. True = blocked (toast shown).
        public static bool GateFirstRun() => Gate();

        protected override void Awake()
        {
            base.Awake();
            On(profileButton, () => Open(profileScreen));
            On(settingsButton, () => Open(settingsScreen));
            On(coinPlus, () => { if (!GateFirstRun()) Open(shopScreen); });
            On(gemPlus, () => { if (!GateFirstRun()) Open(shopScreen); });
            On(daily?.button, () => { if (!GateFirstRun()) Open(dailyScreen); });
            On(events?.button, () => OpenGated(gachaScreen, AccountProgress.Feature.Events));
            On(gacha?.button, () => OpenGated(gachaScreen, AccountProgress.Feature.Gacha));
            On(pass?.button, () => OpenGated(passScreen, AccountProgress.Feature.Pass));
            On(starter?.button, () => { if (!GateFirstRun()) Open(shopScreen); });
            On(stageButton, () => { if (!GateFirstRun()) Open(studioScreen); });
            On(outfitButton, () => { if (!GateFirstRun()) Open(studioScreen); });
            On(gunCard, () => { if (!GateFirstRun()) Open(arsenalScreen); });
            On(stripDaily, () => { if (!GateFirstRun()) Open(dailyScreen); });
            On(stripGacha, () => OpenGated(gachaScreen, AccountProgress.Feature.Gacha));
            On(stripPass, () => OpenGated(passScreen, AccountProgress.Feature.Pass));
            // Mockup F2 (05/10): the card is Daily Ops; it opens the Daily Ops screen.
            On(missionsCard, () => { if (!Gate(AccountProgress.Feature.Missions)) { UIFeedback.Tap(); LateScreens.Open<DailyOpsScreen>(); } });
            On(nextBuyCard, () => { if (!GateFirstRun()) Open(shopScreen); });
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

        protected override void OnShow()
        {
            ArsenalScreen.SpareShardsToCoin();
            Refresh();
            if (revealRoot != null) revealRoot.SetActive(false);   // v2 "ARSENAL + SHOP" popup: replaced by the v3 gun step
            ZombieWar.Audio.FtueVoice.HomeShown(() => this != null && isActiveAndEnabled && !_launching);
            if (FirstRunPending && playButton != null)
            {
                if (playHint != null) playHint.SetActive(false);   // v2 hint; v3 uses the hologram hand
                FtueV3.Home(playButton.transform as RectTransform);
            }
            // A link from the run result or an unlock popup lands here first, then goes on.
            var intent = MenuIntent.Take();
            var go = intent == MenuIntent.Shop ? shopScreen
                : intent == MenuIntent.Arsenal ? arsenalScreen
                : intent == MenuIntent.Pass ? passScreen
                : intent == MenuIntent.Gacha ? gachaScreen : null;
            if (go != null) UIManager.Instance?.Push(go);
        }
        protected override void OnFocus() => Refresh();
        public override bool OnEscape()
        {
            // Root screen: Back sends the game to the background like any Android home screen
            // (07/10: it was swallowed and did nothing).
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity?.Call<bool>("moveTaskToBack", true);
            }
            catch (System.Exception e) { Debug.LogWarning("[Home] Back: " + e.Message); }
#endif
            return true;
        }

        void Play()
        {
            if (_launching) return;
            _launching = true;
            UIFeedback.Confirm();
            GameFlow.StartGameplay();
        }

        /// The strip's gacha card names the live event banner. It was the builder's placeholder
        /// ("Inferno") while the Gacha ran Neon Nights (2026-09-30).
        void RefreshGachaStrip(int today)
        {
            if (stripGacha == null) return;
            GachaBanners.Banner evt = null;
            foreach (var b in GachaBanners.All) if (b.kind == GachaBanners.Kind.Event) { evt = b; break; }
            if (evt == null) return;
            var title = stripGachaTitle;
            var sub = stripGachaSub;
            if (title != null) title.text = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(evt.title.ToLowerInvariant());
            if (sub != null)
            {
                int days = GachaBanners.DaysLeft(evt, today);
                sub.text = days > 0 ? $"EVENT · {days}D" : "EVENT BANNER";
            }
        }

        void Open(UIScreen s)
        {
            if (s == null) { Toast.Show("Coming soon"); return; }
            UIFeedback.Tap();
            UIManager.Instance?.Push(s);
        }

        void OpenGated(UIScreen s, AccountProgress.Feature f)
        {
            if (Gate(f)) return;
            Open(s);
        }

        // ------------------------------------------------------------------ refresh
        public void Refresh()
        {
            PlayerProfile.RefreshMissionWindow(GameClock.UtcNow);
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
            RailState(daily, !FirstRunPending, dailyCount, "", "AFTER RUN 1");
            RailState(events, AccountProgress.IsUnlocked(AccountProgress.Feature.Events), 0, "", "LV " + AccountProgress.RequiredLevel(AccountProgress.Feature.Events));
            RailState(gacha, AccountProgress.IsUnlocked(AccountProgress.Feature.Gacha), 0, "", "LV " + AccountProgress.RequiredLevel(AccountProgress.Feature.Gacha));
            RailState(pass, AccountProgress.IsUnlocked(AccountProgress.Feature.Pass), claimable + PassRewards.ClaimableCount(), "", "LV " + AccountProgress.RequiredLevel(AccountProgress.Feature.Pass));
            RailState(starter, true, 0, "OFFER", "");

            RefreshGun();
            RefreshMissions();
            RefreshNextBuy();
            Set(stripDailyText, DailyRewards.CanStamp(today) ? $"Stamp day {DailyRewards.Stamps + 1}" : DailyRewards.Stamps >= DailyRewards.CardDays ? "Card complete" : "Back tomorrow");
            Set(stripPassText, AccountProgress.IsUnlocked(AccountProgress.Feature.Pass) ? $"PASS LV {PassLevel()}" : $"LV {AccountProgress.RequiredLevel(AccountProgress.Feature.Pass)} UNLOCKS");
            RefreshGachaStrip(today);

            bool firstRun = FirstRunPending;
            if (playHint != null) playHint.SetActive(firstRun);
            if (firstRunCard != null) firstRunCard.SetActive(firstRun);
            if (missionsCard != null) missionsCard.transform.parent.gameObject.SetActive(!firstRun);
            if (stripDaily != null) stripDaily.transform.parent.gameObject.SetActive(!firstRun);
            // First launch: only Daily stays on the rails; starter offer and gated rails wait.
            if (starter?.button != null) starter.button.gameObject.SetActive(!firstRun);
            // No Events screen yet (the rail used to open Gacha): hidden until one exists (backlog #5).
            if (events?.button != null) events.button.gameObject.SetActive(false);
            nav?.Refresh();
            Set(playSub, firstRun ? "Your first run" : best > 0 ? $"Beat your best {HudController.FormatClock(best)}" : "Survive as long as you can");
            if (nav != null) nav.SetDot(4, claimable + PassRewards.ClaimableCount() > 0 && AccountProgress.IsUnlocked(AccountProgress.Feature.Pass));
        }

        static int PassLevel() => PassRewards.Level;

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
                StarPips.Paint(gunStars[i], i < stars);
            int power = Mathf.RoundToInt(CombatPower.WeaponPower(d, stars) * (1f + Skins.WeaponSkins.DamageBonus(PlayerProfile.GetEquippedSkin(d.WeaponId))));
            Set(gunMeta, $"{WeaponClassNames.Family(d.weaponClass)} · POWER {power:N0}");
        }

        static int ClaimableMissions() =>
            PassMissions.ActiveFor(GameClock.UtcNow).Count(m => PlayerProfile.IsMissionComplete(m) && !PlayerProfile.IsMissionClaimed(m.id));

        void RefreshMissions()
        {
            bool unlocked = AccountProgress.IsUnlocked(AccountProgress.Feature.Missions);
            // The day's missions, as the Pass lists them (owner 04/10: Home shows the dailies).
            var now = GameClock.UtcNow;
            var all = PassMissions.Listed(now).Where(m => m.scope == MissionScope.Daily).ToList();
            int claimedOps = all.Count(m => PlayerProfile.IsMissionClaimed(m.id));
            var daily = all.Where(m => !PlayerProfile.IsMissionClaimed(m.id)).ToList();
            Set(missionsHeader, unlocked ? $"DAILY OPS · {claimedOps} / {all.Count}"
                                         : $"DAILY OPS · LV {AccountProgress.RequiredLevel(AccountProgress.Feature.Missions)}");
            // First row: the daily chest (mockup F2); then the ops still open.
            bool chestReady = PlayerProfile.CanClaimDailyChest(now);
            bool chestOpened = PlayerProfile.DailyChestOpenedOn(PassMissions.DayKey(now));
            int row0 = 0;
            if (missionRows.Length > 0 && missionRows[0]?.root != null)
            {
                var c = missionRows[0];
                c.root.SetActive(true);
                Set(c.title, chestOpened ? "Daily chest · opened" : "Daily chest");
                if (c.claimTag != null) c.claimTag.SetActive(chestReady && unlocked);
                if (c.bar != null)
                {
                    c.bar.parent.gameObject.SetActive(!chestReady);
                    UIBarClip.Set(c.bar, all.Count == 0 ? 0f : claimedOps / (float)all.Count);
                }
                row0 = 1;
            }
            var list = daily.Take(Mathf.Max(0, missionRows.Length - row0)).ToList();
            for (int i = row0; i < missionRows.Length; i++)
            {
                var row = missionRows[i];
                if (row == null || row.root == null) continue;
                int k = i - row0;
                bool has = k < list.Count;
                row.root.SetActive(has);
                if (!has) continue;
                var m = list[k];
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
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
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

        public static string Short(long v) =>
            v >= 1_000_000 ? (v / 1_000_000f).ToString("0.#") + "M" :
            v >= 100_000 ? (v / 1000f).ToString("0") + "K" :   // "118K", not "117.6K": pills are narrow
            v >= 10_000 ? (v / 1000f).ToString("0.#") + "K" : v.ToString("N0");
    
#if UNITY_EDITOR
        /// G12.8: wires what Home used to find by path; returns the paths not found.
        public System.Collections.Generic.List<string> EditorWire()
        {
            var m = new System.Collections.Generic.List<string>();
            var strip = stripGacha != null ? stripGacha.transform : null;
            stripGachaTitle = WireUtil.Find<TMP_Text>(strip, "Title", m);
            stripGachaSub = WireUtil.Find<TMP_Text>(strip, "Sub", m);
            return m;
        }

        public System.Collections.Generic.List<string> EditorUnwired() => WireUtil.Nulls(
            ("stripGachaTitle", stripGachaTitle), ("stripGachaSub", stripGachaSub));
#endif
}
}
