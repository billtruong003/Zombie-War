using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ZombieWar
{
    /// <summary>
    /// In-run HUD (M8 layout, owner-approved mockup): XP bar across the top, HP bar with level and
    /// kill chips, a big survival clock with the threat chip under it, coin pill, pause, the horde
    /// banner, and the skill bar (<see cref="SkillBarView"/>).
    ///
    /// Input is movement and the level-up card only (GDD §1): there is no fire, reload, bomb or
    /// weapon-switch button. Every field is optional - a missing widget never throws.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("Top pills")]
        [SerializeField] private RectTransform healthFillRect;  // fill pill - scaled through anchorMax.x
        [SerializeField] private TMP_Text healthLabel;          // "100"
        [FormerlySerializedAs("wavePill")]
        [SerializeField] private TMP_Text runPill;              // "3:42 · Threat 2 · Lv 7"
        [SerializeField] private TMP_Text coinPill;
        [SerializeField] private Image healthFillImage;         // turns red under 30%

        [Header("M8 run readout")]
        [SerializeField] private TMP_Text clockLabel;           // "3:42"
        [SerializeField] private Image threatChip;
        [SerializeField] private TMP_Text threatLabel;          // "THREAT 2"
        [SerializeField] private TMP_Text levelLabel;           // "Lv 7"
        [SerializeField] private TMP_Text killLabel;            // "287"
        [SerializeField] private RectTransform xpFillRect;      // scaled through anchorMax.x
        [SerializeField] private GameObject hordeBanner;
        [SerializeField] private TMP_Text hordeLabel;

        [Header("Buttons")]
        [SerializeField] private Button pauseButton;

        [Tooltip("Seconds before a surge that the banner starts warning.")]
        [SerializeField] private float hordeWarningSeconds = 3f;

        // Cached shown values - setting TMP text every frame is the HUD's main source of GC.
        private long _shownRunCoin = long.MinValue;
        private int _shownSeconds = -1, _shownTier = -1, _shownLevel = -1, _shownKills = -1, _shownBanner = -2;
        private bool _shownSurge;
        private float _shownXp = -1f;

        static readonly Color ThreatCalm = new(1f, 0.61f, 0.24f);
        static readonly Color ThreatHorde = new(0.84f, 0.23f, 0.23f);

        /// <summary>RunOverlays wires its pause screen here. Null -> the button only logs.</summary>
        public System.Action PauseRequested;

        private void OnEnable()
        {
            if (pauseButton) pauseButton.onClick.AddListener(OnPausePressed);

            RunState.Changed += OnRunChanged;
            OnRunChanged();

            var bus = Bill.Events;
            if (bus == null) return;
            bus.Subscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            bus.Subscribe<PlayerDiedEvent>(OnPlayerDied);

            // The bar starts from the live value, not the prefab's placeholder fill.
            var health = PlayerMovement.Instance != null ? PlayerMovement.Instance.GetComponent<Health>() : null;
            if (health != null) SetHp(health.Max > 0f ? health.Current / health.Max : 0f, health.Current);
        }

        private void OnDisable()
        {
            RunState.Changed -= OnRunChanged;
            if (pauseButton) pauseButton.onClick.RemoveListener(OnPausePressed);

            var bus = Bill.Events;
            if (bus == null) return;
            bus.Unsubscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            bus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        // Duration ticks every frame without raising RunState.Changed, so the clock polls - but only
        // rebuilds the string when the whole second, tier or level actually changes.
        private void Update() => RefreshRunPill();

        private void RefreshRunPill()
        {
            var run = RunState.Current;
            float duration = run != null ? run.Duration : 0f;
            int seconds = Mathf.FloorToInt(duration);
            var director = Threat.ThreatDirector.Instance;
            int tier = director != null ? director.CurrentTier : 0;
            bool surge = director != null && director.Surging;
            int level = run?.Level ?? 1;
            int kills = run?.Kills ?? 0;

            // XP bar: continuous, cheap (a RectTransform anchor), only touched when it moves.
            float xp = run != null && run.XpForNextLevel > 0 ? Mathf.Clamp01((float)run.Xp / run.XpForNextLevel) : 0f;
            if (xpFillRect != null && Mathf.Abs(xp - _shownXp) > 0.002f)
            {
                _shownXp = xp;
                xpFillRect.anchorMax = new Vector2(xp, 1f);
            }

            if (kills != _shownKills && killLabel != null) { _shownKills = kills; killLabel.text = kills.ToString(); }

            RefreshHordeBanner(director, duration);

            if (seconds == _shownSeconds && tier == _shownTier && level == _shownLevel && surge == _shownSurge) return;
            _shownSeconds = seconds; _shownTier = tier; _shownLevel = level; _shownSurge = surge;

            if (runPill != null) runPill.text = FormatRunPill(seconds, tier, level, surge);
            if (clockLabel != null) clockLabel.text = FormatClock(seconds);
            if (levelLabel != null) levelLabel.text = $"Lv {level}";
            if (threatLabel != null) threatLabel.text = surge ? "HORDE" : $"THREAT {tier}";
            if (threatChip != null) threatChip.color = surge ? ThreatHorde : ThreatCalm;
        }

        // The banner warns a few seconds BEFORE a surge (anticipation), then counts it down.
        private void RefreshHordeBanner(Threat.ThreatDirector director, float duration)
        {
            if (hordeBanner == null) return;
            int state;   // -1 hidden, 0..n = seconds shown (warning = n, running = 100+n)
            if (director == null) state = -1;
            else if (director.Surging) state = 100 + Mathf.CeilToInt(director.SurgeSecondsLeftNow(duration));
            else
            {
                float until = director.SecondsUntilSurgeNow(duration);
                // A surge that is due but has not started (director paused, surge held back) used to
                // leave "HORDE INCOMING 0" on screen for good: the warning is only for a real countdown.
                state = director.isActiveAndEnabled && until > 0f && until <= hordeWarningSeconds ? Mathf.CeilToInt(until) : -1;
            }
            if (state == _shownBanner) return;
            _shownBanner = state;

            bool show = state >= 0;
            if (hordeBanner.activeSelf != show)
            {
                hordeBanner.SetActive(show);
                if (show) ZombieWar.UI.UIFx.Punch(hordeBanner.transform);
            }
            if (!show || hordeLabel == null) return;
            hordeLabel.text = state >= 100 ? $"HORDE!  0:{state - 100:00}" : $"HORDE INCOMING  {state}";
        }

        public static string FormatClock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        /// <summary>"3:42 · Threat 2 · Lv 7". During a surge the whole pill turns red and HORDE takes
        /// the threat slot, so the text never grows past the owner-authored pill width.</summary>
        public static string FormatRunPill(int seconds, int tier, int level, bool surge) =>
            surge
                ? $"<color=#FF4A3D>{FormatClock(seconds)} · HORDE · Lv {level}</color>"
                : $"{FormatClock(seconds)} · Threat {tier} · Lv {level}";

        // Coin pill binds the live ledger: a pickup, a crate or a kill moves the number at once.
        private void OnRunChanged()
        {
            RefreshRunPill();

            if (coinPill == null) return;
            long coin = RunState.Current?.Coin ?? 0;
            if (coin == _shownRunCoin) return;
            _shownRunCoin = coin;
            coinPill.text = ZombieWar.UI.CurrencyClusterWidget.Format(coin);
            ZombieWar.UI.UIFx.Punch(coinPill.transform);
        }

        private void OnPausePressed()
        {
            if (PauseRequested != null) PauseRequested();
            else Debug.Log("[HudController] Pause: no overlay wired.");
        }

        // Every health change (hit, heal, revive, Max Health card), not only damage: following the
        // damage event alone left the bar empty after a revive (QA 04/10).
        private void OnPlayerHealthChanged(PlayerHealthChangedEvent e) => SetHp(e.Normalized, e.Current);

        // HP drops to 0 at once; the result screen waits for GameOverEvent so the death plays out.
        private void OnPlayerDied(PlayerDiedEvent e) => SetHp(0f, 0f);

        private ZombieWar.UI.UIBarClip _clip;
        private bool _clipResolved;

        private void SetHp(float normalized, float current)
        {
            if (healthFillRect)
            {
                // The pill shrinks instead of being clipped, so both ends stay round inside the round
                // track at any value (owner: a flat cut looked off). Never thinner than it is tall.
                if (!_clipResolved) { _clip = healthFillRect.GetComponent<ZombieWar.UI.UIBarClip>(); _clipResolved = true; }
                var clip = _clip;
                if (clip != null && clip.Graphic != null)
                {
                    healthFillRect.anchorMax = new Vector2(1f, 1f);
                    float inner = healthFillRect.rect.width, h = healthFillRect.rect.height, v = Mathf.Clamp01(normalized);
                    clip.Graphic.sizeDelta = new Vector2(v <= 0f ? 0f : Mathf.Lerp(Mathf.Min(h, inner), inner, v), 0f);
                }
                else healthFillRect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            }
            // Compact format (12.3K) - the label sits INSIDE the bar and must never overflow it.
            if (healthLabel) healthLabel.text = ZombieWar.UI.CurrencyClusterWidget.Format(Mathf.CeilToInt(current));
            if (healthFillImage)
                healthFillImage.color = normalized < 0.3f
                    ? new Color(0.898f, 0.282f, 0.302f)
                    : new Color(0.298f, 0.686f, 0.431f);
        }
    }
}
