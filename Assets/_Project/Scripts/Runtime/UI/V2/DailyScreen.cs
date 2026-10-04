using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Daily (owner-approved V2_Daily mockup): the 7-day welcome check-in (hidden once all seven
    /// are claimed) and the 28-day stamp card with milestone rewards, the grand prize and make-up
    /// days. Rules live in <see cref="DailyRewards"/>; this screen only shows and forwards taps.
    /// Built by HordeCall/UI v2/Build Daily.
    /// </summary>
    public sealed class DailyScreen : UIScreen
    {
        [Serializable]
        public sealed class WelcomeTile
        {
            public Button button;
            public Image bg;
            public GameObject ring;
            public GameObject check;
            [Tooltip("The CLAIM pill under today's tile (owner 04/10, Daily option B).")]
            public GameObject claim;
        }

        [Serializable]
        public sealed class StampTile
        {
            public Image bg;
            public TMP_Text number;
            public GameObject stamp;
            public GameObject ring;
        }

        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text gemLabel;

        [Header("Welcome check-in")]
        [SerializeField] private GameObject welcomeCard;
        [SerializeField] private TMP_Text welcomeSub;
        [SerializeField] private WelcomeTile[] welcomeTiles = new WelcomeTile[DailyRewards.WelcomeDays];

        [Header("Stamp card")]
        [SerializeField] private TMP_Text cardSub;
        [SerializeField] private StampTile[] stampTiles = new StampTile[DailyRewards.CardDays];
        [SerializeField] private TMP_Text makeUpHint;
        [SerializeField] private Button makeUpButton;
        [SerializeField] private TMP_Text makeUpSub;
        [SerializeField] private Button stampButton;
        [SerializeField] private TMP_Text stampLabel;


        protected override void Awake()
        {
            base.Awake();
            if (backButton != null) backButton.onClick.AddListener(() => { UIFeedback.Back(); UIManager.Instance?.Pop(); });
            for (int i = 0; i < welcomeTiles.Length; i++)
                if (welcomeTiles[i]?.button != null) welcomeTiles[i].button.onClick.AddListener(ClaimWelcome);
            // Option B (owner 04/10): while today's welcome gift waits, the big button claims it; only
            // then does it stamp. One thing to press at a time.
            if (stampButton != null) stampButton.onClick.AddListener(() =>
            {
                if (DailyRewards.CanClaimWelcome(DailyRewards.Today)) ClaimWelcome(); else Stamp();
            });
            if (makeUpButton != null) makeUpButton.onClick.AddListener(MakeUp);
        }

        private void OnEnable() { PlayerProfile.AccountChanged += Refresh; PlayerProfile.WalletChanged += Refresh; }
        private void OnDisable() { PlayerProfile.AccountChanged -= Refresh; PlayerProfile.WalletChanged -= Refresh; }
        protected override void OnShow() => Refresh();
        protected override void OnFocus() => Refresh();

        void ClaimWelcome()
        {
            int today = DailyRewards.Today;
            if (!DailyRewards.ClaimWelcome(today, out var r)) { Toast.Show("Come back tomorrow"); return; }
            UIFeedback.Purchase();
            Toast.Show("Got " + Describe(r));
        }

        void Stamp()
        {
            int today = DailyRewards.Today;
            if (!DailyRewards.Stamp(today, out var r)) { Toast.Show("Come back tomorrow"); return; }
            UIFeedback.Purchase();
            Toast.Show("Stamped! Got " + Describe(r));
        }

        void MakeUp()
        {
            int today = DailyRewards.Today;
            if (!DailyRewards.CanMakeUp(today)) { UIFeedback.Error(); Toast.Show(DailyRewards.MakeUpsLeft == 0 ? "No make-ups left this cycle" : "No missed days"); return; }
            if (!DailyRewards.MakeUp(today, false, out var r)) { UIFeedback.Error(); Toast.Show("Not enough gems"); return; }
            UIFeedback.Purchase();
            Toast.Show("Day made up! Got " + Describe(r));
        }

        /// "CLAIM DAY 1" over a smaller "500 COINS".
        static string ClaimLabel(int day) =>
            $"CLAIM DAY {day}\n<size=60%>{Describe(DailyRewards.WelcomeReward(day)).ToUpperInvariant()}</size>";

        static string Describe(DailyRewards.Reward r) => r.kind switch
        {
            DailyRewards.Kind.Coin => $"{r.amount:N0} coins",
            DailyRewards.Kind.Gem => $"{r.amount:N0} gems",
            DailyRewards.Kind.Ticket => r.amount == 1 ? "1 ticket" : $"{r.amount} tickets",
            DailyRewards.Kind.Gun => "a new gun",
            _ => r.label,
        };

        public void Refresh()
        {
            int today = DailyRewards.Today;
            DailyRewards.EnsureCycle(today);
            if (coinLabel != null) coinLabel.text = HomeScreen.Short(PlayerProfile.Coin);
            if (gemLabel != null) gemLabel.text = HomeScreen.Short(PlayerProfile.Gem);

            bool welcome = DailyRewards.WelcomeActive;
            if (welcomeCard != null) welcomeCard.SetActive(welcome);
            if (welcome)
            {
                int claimed = DailyRewards.WelcomeClaims;
                bool canClaim = DailyRewards.CanClaimWelcome(today);
                int shownDay = Mathf.Min(DailyRewards.WelcomeDays, claimed + (canClaim ? 1 : 0));
                if (welcomeSub != null) welcomeSub.text = $"NEW PLAYERS · DAY {Mathf.Max(1, shownDay)} / {DailyRewards.WelcomeDays}";
                for (int i = 0; i < welcomeTiles.Length; i++)
                {
                    var t = welcomeTiles[i]; if (t == null) continue;
                    bool done = i < claimed, isToday = canClaim && i == claimed;
                    if (t.check != null) t.check.SetActive(done);
                    if (t.ring != null) t.ring.SetActive(isToday);
                    if (t.claim != null) t.claim.SetActive(isToday);
                    if (t.bg != null && i < DailyRewards.WelcomeDays - 1) ThemeTint.Set(t.bg, done ? ThemeRole.ClaimTint : ThemeRole.Deep);
                    if (t.button != null) t.button.interactable = isToday;
                }
            }

            int stamps = DailyRewards.Stamps;
            bool stampedToday = DailyRewards.StampedToday(today);
            int todayTile = DailyRewards.CanStamp(today) ? stamps : -1;
            if (cardSub != null) cardSub.text = $"{stamps} / {DailyRewards.CardDays} · CYCLE ENDS IN {DailyRewards.DaysLeft(today)}D";
            for (int i = 0; i < stampTiles.Length; i++)
            {
                var t = stampTiles[i]; if (t == null) continue;
                bool milestone = DailyRewards.IsMilestone(i + 1);
                if (t.stamp != null) t.stamp.SetActive(i < stamps);
                if (t.number != null) { t.number.gameObject.SetActive(i >= stamps); ThemeTint.Set(t.number, milestone ? ThemeRole.Gold : ThemeRole.Dim); }
                if (t.ring != null) t.ring.SetActive(i == todayTile);
                ThemeTint.Set(t.bg, milestone ? ThemeRole.PrimaryTint : ThemeRole.Deep);
            }

            int missed = DailyRewards.Missed(today), left = DailyRewards.MakeUpsLeft;
            if (makeUpHint != null)
                makeUpHint.text = missed > 0 ? $"{missed} missed day{(missed == 1 ? "" : "s")} · {left} make-up{(left == 1 ? "" : "s")} left"
                                             : $"{DailyRewards.MakeUpsPerCycle} missed days can be made up";
            if (makeUpSub != null) makeUpSub.text = $"{DailyRewards.MakeUpGemCost} GEMS · {left} LEFT";
            if (makeUpButton != null) makeUpButton.gameObject.GetComponent<CanvasGroup>()?.SetAlpha(DailyRewards.CanMakeUp(today) ? 1f : 0.45f);

            bool full = stamps >= DailyRewards.CardDays;
            bool gift = DailyRewards.CanClaimWelcome(today);
            if (stampLabel != null)
                stampLabel.text = gift ? ClaimLabel(DailyRewards.WelcomeClaims + 1)
                                : full ? "CARD COMPLETE" : stampedToday ? "BACK TOMORROW" : $"STAMP DAY {stamps + 1}";
            if (stampButton != null) stampButton.gameObject.GetComponent<CanvasGroup>()?.SetAlpha(gift || DailyRewards.CanStamp(today) ? 1f : 0.5f);
        }
    }

    static class CanvasGroupExt
    {
        public static void SetAlpha(this CanvasGroup g, float a) { if (g != null) g.alpha = a; }
    }
}
