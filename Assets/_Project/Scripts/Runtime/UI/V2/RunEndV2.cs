using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 run ending (owner-approved V2_Revive and V2_Result mockups), living in the HUD next to
    /// <see cref="RunOverlays"/>. Revive: a lethal hit is held (<see cref="Health.ReviveGate"/>), the
    /// world freezes and the player picks a free ad revive, a coin revive (<see cref="ReviveRules"/>)
    /// or no thanks; seven seconds of silence counts as no thanks. Result: time, record, stats,
    /// coins kept with an ad to double them, missions finished this run, the gun that became
    /// affordable (with a Shop link), Play again and Home.
    /// Built by HordeCall/UI v2/Build Run End (into UI_Hud).
    /// </summary>
    public sealed class RunEndV2 : MonoBehaviour
    {
        [Header("Revive")]
        [SerializeField] private GameObject reviveRoot;
        [SerializeField] private Image ring;
        [SerializeField] private TMP_Text seconds;
        [SerializeField] private TMP_Text carried;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text costValue;
        [SerializeField] private TMP_Text costNote;
        [SerializeField] private Button adButton;
        [SerializeField] private Button coinButton;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private Button noButton;

        [Header("Result")]
        [SerializeField] private GameObject resultRoot;
        [SerializeField] private TMP_Text banner;
        [SerializeField] private TMP_Text time;
        [SerializeField] private GameObject newBest;
        [SerializeField] private TMP_Text bestLabel;
        [SerializeField] private TMP_Text kills;
        [SerializeField] private TMP_Text level;
        [SerializeField] private TMP_Text threat;
        [SerializeField] private TMP_Text coins;
        [SerializeField] private Button doubleButton;
        [SerializeField] private TMP_Text doubleLabel;
        [SerializeField] private TMP_Text gems;
        [SerializeField] private GameObject[] progressRows = new GameObject[3];
        [SerializeField] private TMP_Text[] progressText = new TMP_Text[3];
        [SerializeField] private TMP_Text[] progressTag = new TMP_Text[3];
        [SerializeField] private Button shopLink;
        [SerializeField] private GameObject progressCard;
        [SerializeField] private Button playAgain;
        [SerializeField] private Button home;

        Health _player;
        Coroutine _count;
        long _banked;
        bool _doubled;

        void Awake()
        {
            if (reviveRoot != null) reviveRoot.SetActive(false);
            if (resultRoot != null) resultRoot.SetActive(false);
            On(adButton, () => RewardedAds.Show("revive", () => { ReviveRules.UseAd(); GetUp(); }));
            On(coinButton, () =>
            {
                if (ReviveRules.TryPayCoin()) GetUp();
                else { UIFeedback.Error(); Toast.Show("Not enough coins"); }
            });
            On(noButton, GiveUp);
            On(doubleButton, () => RewardedAds.Show("double_coins", DoubleCoins));
            On(shopLink, () => { MenuIntent.Next = MenuIntent.Shop; Leave(GameFlow.ReturnToMenu); });
            On(playAgain, () => Leave(GameFlow.RestartGameplay));
            On(home, () => Leave(GameFlow.ReturnToMenu));
        }

        void Start() => HookPlayer();

        void Update() { if (_player == null) HookPlayer(); }

        void HookPlayer()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) return;
            _player = pc.GetComponent<Health>();
            if (_player != null) _player.ReviveGate = OfferRevive;
        }

        void OnDestroy() { if (_player != null && _player.ReviveGate == (Func<bool>)OfferRevive) _player.ReviveGate = null; }

        // ------------------------------------------------------------ revive
        bool OfferRevive()
        {
            if (!ReviveRules.CanOffer || reviveRoot == null) return false;
            Time.timeScale = 0f;
            reviveRoot.SetActive(true);
            reviveRoot.transform.SetAsLastSibling();
            long cost = ReviveRules.NextCoinCost, carry = ReviveRules.Carried;
            Set(carried, carry.ToString("N0"));
            Set(costLabel, $"Revive {ReviveRules.Used + 1} of {ReviveRules.MaxRevives} costs");
            Set(costValue, cost.ToString("N0"));
            Set(costNote, cost > carry ? "Price doubles each time. This one costs more than you carry." : "Price doubles each time.");
            Set(coinLabel, $"REVIVE · {cost:N0}");
            if (adButton != null) adButton.gameObject.SetActive(ReviveRules.AdAvailable);
            if (coinButton != null) coinButton.interactable = PlayerProfile.Coin >= cost;
            if (_count != null) StopCoroutine(_count);
            _count = StartCoroutine(Countdown());
            return true;
        }

        IEnumerator Countdown()
        {
            for (float t = ReviveRules.OfferSeconds; t > 0f; t -= Time.unscaledDeltaTime)
            {
                Set(seconds, Mathf.CeilToInt(t).ToString());
                if (ring != null) ring.fillAmount = t / ReviveRules.OfferSeconds;
                yield return null;
            }
            GiveUp();
        }

        void GetUp()
        {
            if (_count != null) StopCoroutine(_count);
            reviveRoot.SetActive(false);
            Time.timeScale = 1f;
            UIFeedback.Confirm();
            _player?.Revive();
        }

        void GiveUp()
        {
            if (_count != null) StopCoroutine(_count);
            if (reviveRoot != null) reviveRoot.SetActive(false);
            Time.timeScale = 1f;
            _player?.ConfirmDeath();
        }

        // ------------------------------------------------------------ result
        public void ShowResult(RunClosure.Result result)
        {
            if (resultRoot == null) return;
            var s = result.Summary;
            _banked = result.BankedCoin; _doubled = false;
            resultRoot.SetActive(true);
            resultRoot.transform.SetAsLastSibling();
            Set(banner, s.Outcome == RunOutcome.Died ? "THE HORDE GOT YOU" : "YOU WALKED AWAY");
            UIFx.CountUp(time, Mathf.FloorToInt(s.Duration), 0.7f, v => HudController.FormatClock((int)v));
            if (newBest != null)
            {
                newBest.SetActive(true);
                var img = newBest.GetComponent<Image>();
                ThemeTint.Set(img, result.NewSurvivalRecord ? ThemeRole.Primary : ThemeRole.Card);
                ThemeTint.Set(bestLabel, result.NewSurvivalRecord ? ThemeRole.PrimaryOn : ThemeRole.TextOnSurface);
            }
            Set(bestLabel, result.NewSurvivalRecord ? "NEW BEST" : "BEST " + HudController.FormatClock(Mathf.FloorToInt(PlayerProfile.BestSurvivalSeconds)));
            UIFx.CountUp(kills, s.Kills, 0.6f, v => $"{v:N0}", 0.25f);
            Set(level, s.Level.ToString());
            Set(threat, s.PeakThreatTier.ToString());
            UIFx.CountUp(coins, _banked, 0.8f, v => $"{v:N0}", 0.4f);
            Set(gems, $"+{s.Gem}");
            if (doubleButton != null) doubleButton.gameObject.SetActive(_banked > 0);
            Set(doubleLabel, $"WATCH AD · DOUBLE TO {_banked * 2:N0}");

            // Missions this run finished (claimable in the Pass), then the gun it made affordable.
            var done = PassMissions.ActiveFor(DateTime.UtcNow)
                .Where(m => PlayerProfile.IsMissionComplete(m) && !PlayerProfile.IsMissionClaimed(m.id)).Take(2).ToList();
            for (int i = 0; i < progressRows.Length; i++) if (progressRows[i] != null) progressRows[i].SetActive(false);
            int row = 0;
            foreach (var m in done) SetRow(row++, m.title, $"DONE +{m.passXp} XP");
            var guns = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
            var next = guns?.Where(w => w.price > 0 && !PlayerProfile.IsWeaponOwned(w.WeaponId) && w.price <= PlayerProfile.Coin)
                            .OrderByDescending(w => w.price).FirstOrDefault();
            // The last row is the next-buy row; it carries the Shop link.
            if (next != null) SetRow(progressRows.Length - 1, $"{next.weaponName} now affordable", "");
            if (progressCard != null) progressCard.SetActive(done.Count > 0 || next != null);
            Time.timeScale = 0f;
        }

        void SetRow(int i, string text, string tag)
        {
            if (i >= progressRows.Length) return;
            if (progressRows[i] != null) progressRows[i].SetActive(true);
            if (i < progressText.Length) Set(progressText[i], text);
            if (i < progressTag.Length && progressTag[i] != null)
            {
                progressTag[i].transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(tag));
                progressTag[i].text = tag;
            }
        }

        void DoubleCoins()
        {
            if (_doubled || _banked <= 0) return;
            _doubled = true;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, _banked);
            UIFeedback.Purchase();
            Set(coins, (_banked * 2).ToString("N0"));
            if (doubleButton != null) doubleButton.gameObject.SetActive(false);
            Toast.Show($"+{_banked:N0} coins");
        }

        static void Leave(Action go) { Time.timeScale = 1f; go(); }
        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }
        static void On(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }
    }

    /// <summary>Where the menu should go right after it opens (e.g. the result's Shop link).</summary>
    public static class MenuIntent
    {
        public const string Shop = "shop";
        public static string Next;
        public static string Take() { var n = Next; Next = null; return n; }
    }
}
