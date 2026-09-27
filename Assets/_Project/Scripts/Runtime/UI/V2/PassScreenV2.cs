using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Pass (owner-approved V2_Pass mockup): season level card, a horizontally scrolling free and
    /// premium track of 30 levels (tap an open reward to claim), the mission list that is the only
    /// source of pass XP, and the premium unlock. Rules live in <see cref="PassRewards"/>.
    /// Built by HordeCall/UI v2/Build Pass.
    /// </summary>
    public sealed class PassScreenV2 : UIScreen
    {
        [Serializable]
        public sealed class Column
        {
            public Button freeButton;
            public Image freeBg;
            public GameObject freeCheck;
            public GameObject freeRing;
            public Button premButton;
            public GameObject premCheck;
            public GameObject premLock;
            public GameObject premRing;
            public Image node;
        }

        [Serializable]
        public sealed class MissionCard
        {
            public GameObject root;
            public TMP_Text title;
            public RectTransform bar;
            public Image barFill;
            public Button claimButton;
            public TMP_Text claimLabel;
            public TMP_Text xpLabel;
        }

        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text gemLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text seasonLabel;
        [SerializeField] private TMP_Text daysLabel;
        [SerializeField] private RectTransform xpBar;
        [SerializeField] private TMP_Text xpLabel;
        [SerializeField] private ScrollRect track;
        [SerializeField] private Column[] columns = new Column[PassRewards.MaxLevel];
        [SerializeField] private TMP_Text resetLabel;
        [SerializeField] private MissionCard[] missions = new MissionCard[PassMissions.DailyCount + PassMissions.WeeklyCount];
        [SerializeField] private Button premiumButton;
        [SerializeField] private TMP_Text premiumLabel;
        [SerializeField] private NavBarV2 nav;


        string[] _missionIds = new string[0];

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < columns.Length; i++)
            {
                int level = i + 1; var c = columns[i]; if (c == null) continue;
                if (c.freeButton != null) c.freeButton.onClick.AddListener(() => ClaimLevel(level, false));
                if (c.premButton != null) c.premButton.onClick.AddListener(() => ClaimLevel(level, true));
            }
            for (int i = 0; i < missions.Length; i++)
            {
                int idx = i;
                if (missions[i]?.claimButton != null) missions[i].claimButton.onClick.AddListener(() => ClaimMission(idx));
            }
            if (premiumButton != null) premiumButton.onClick.AddListener(BuyPremium);
        }

        private void OnEnable()
        {
            PlayerProfile.MissionsChanged += Refresh; PlayerProfile.AccountChanged += Refresh; PlayerProfile.WalletChanged += Refresh;
        }
        private void OnDisable()
        {
            PlayerProfile.MissionsChanged -= Refresh; PlayerProfile.AccountChanged -= Refresh; PlayerProfile.WalletChanged -= Refresh;
        }

        protected override void OnShow() { Refresh(); ScrollToLevel(); }
        protected override void OnFocus() => Refresh();

        void ClaimLevel(int level, bool premium)
        {
            if (premium && !PassRewards.IsPremium) { UIFeedback.Error(); Toast.Show("Premium pass reward"); return; }
            if (level > PassRewards.Level) { UIFeedback.Error(); Toast.Show($"Reach pass level {level}"); return; }
            if (!PassRewards.Claim(level, premium, out var r)) return;
            UIFeedback.Purchase();
            Toast.Show($"Got {r.Label}{(r.kind == PassRewards.Kind.Coin ? " coins" : r.kind == PassRewards.Kind.Gem ? " gems" : r.kind == PassRewards.Kind.Ticket ? " tickets" : "")}");
        }

        void ClaimMission(int idx)
        {
            if (idx >= _missionIds.Length) return;
            var m = PassMissions.Find(_missionIds[idx]);
            int before = PassRewards.Level;
            if (m == null || !PlayerProfile.TryClaimMission(m.id)) return;
            UIFeedback.Purchase();
            Toast.Show(PassRewards.Level > before ? $"Pass level {PassRewards.Level}!" : $"+{m.passXp} XP");
        }

        void BuyPremium()
        {
            if (PassRewards.IsPremium) { Toast.Show("Premium is active"); return; }
            Purchases.Buy(PassRewards.PremiumProductId, "$4.99", () => { PassRewards.UnlockPremium(); Refresh(); });
        }

        void ScrollToLevel()
        {
            if (track == null) return;
            Canvas.ForceUpdateCanvases();
            float t = Mathf.Clamp01((PassRewards.Level - 3) / (float)(PassRewards.MaxLevel - 5));
            track.horizontalNormalizedPosition = t;
        }

        public void Refresh()
        {
            int today = DailyRewards.Today;
            PassRewards.EnsureSeason(today);
            PlayerProfile.RefreshMissionWindow(DateTime.UtcNow);
            Set(coinLabel, HomeScreen.Short(PlayerProfile.Coin));
            Set(gemLabel, HomeScreen.Short(PlayerProfile.Gem));
            int level = PassRewards.Level;
            Set(levelLabel, level.ToString());
            Set(seasonLabel, "SEASON " + PassRewards.Season);
            Set(daysLabel, $"{PassRewards.DaysLeft(today)} DAYS LEFT");
            bool max = level >= PassRewards.MaxLevel;
            UIBarClip.Set(xpBar, max ? 1f : PassRewards.XpIntoLevel / (float)PassRewards.XpPerLevel);
            Set(xpLabel, max ? "MAX LEVEL · XP COMES FROM MISSIONS" : $"{PassRewards.XpIntoLevel} / {PassRewards.XpPerLevel} XP · XP COMES FROM MISSIONS");

            for (int i = 0; i < columns.Length; i++)
            {
                var c = columns[i]; if (c == null) continue;
                int l = i + 1; bool reached = l <= level;
                bool fDone = PassRewards.IsClaimed(l, false), pDone = PassRewards.IsClaimed(l, true);
                ThemeTint.Set(c.freeBg, fDone ? ThemeRole.ClaimTint : ThemeRole.Card);
                Show(c.freeCheck, fDone); Show(c.freeRing, PassRewards.CanClaim(l, false));
                Show(c.premCheck, pDone); Show(c.premLock, !PassRewards.IsPremium); Show(c.premRing, PassRewards.CanClaim(l, true));
                ThemeTint.Set(c.node, reached ? ThemeRole.Claim : ThemeRole.Edge);
            }

            var active = PassMissions.ActiveFor(DateTime.UtcNow)
                .OrderBy(m => PlayerProfile.IsMissionClaimed(m.id) ? 2 : PlayerProfile.IsMissionComplete(m) ? 0 : 1)
                .ThenBy(m => m.scope).ToList();
            _missionIds = active.Select(m => m.id).ToArray();
            for (int i = 0; i < missions.Length; i++)
            {
                var card = missions[i]; if (card?.root == null) continue;
                bool has = i < active.Count; card.root.SetActive(has);
                if (!has) continue;
                var m = active[i];
                bool done = PlayerProfile.IsMissionComplete(m), claimed = PlayerProfile.IsMissionClaimed(m.id);
                Set(card.title, (m.scope == MissionScope.Weekly ? "WEEKLY · " : "") + m.title);
                UIBarClip.Set(card.bar, Mathf.Clamp01(PlayerProfile.GetMissionProgress(m.id) / (float)Mathf.Max(1, m.target)));
                ThemeTint.Set(card.barFill, done ? ThemeRole.Claim : m.scope == MissionScope.Weekly ? ThemeRole.Rarity3 : ThemeRole.Info);
                if (card.claimButton != null) card.claimButton.gameObject.SetActive(done && !claimed);
                Set(card.claimLabel, $"+{m.passXp} XP");
                if (card.xpLabel != null) { card.xpLabel.gameObject.SetActive(!done || claimed); card.xpLabel.text = claimed ? "DONE" : $"{m.passXp} XP"; }
            }
            Set(resetLabel, $"DAILY RESETS {24 - DateTime.UtcNow.Hour}H");
            if (premiumButton != null) premiumButton.gameObject.SetActive(!PassRewards.IsPremium);
            if (nav != null) nav.SetDot(4, PassRewards.ClaimableCount() > 0);
        }

        static void Show(GameObject g, bool on) { if (g != null) g.SetActive(on); }
        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }
    }
}
