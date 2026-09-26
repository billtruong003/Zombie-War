using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ZombieWar
{
    /// <summary>
    /// In-run HUD: HP pill, run pill (time survived · threat tier · level), coin pill, pause.
    ///
    /// Input is movement and the level-up card only (GDD §1): there is no fire, reload, bomb or
    /// weapon-switch button. The HUD prefab is owner-authored and still carries the retired bomb and
    /// weapon buttons, so they are hidden here rather than restructured in the prefab.
    /// Every field is optional - a missing widget never throws.
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

        [Header("Buttons")]
        [SerializeField] private Button pauseButton;

        [Header("Retired (hidden at runtime until removed from the prefab)")]
        [SerializeField] private Button bombButton;
        [SerializeField] private Button weaponButton;

        // Cached shown values - setting TMP text every frame is the HUD's main source of GC.
        private long _shownRunCoin = long.MinValue;
        private int _shownSeconds = -1, _shownTier = -1, _shownLevel = -1;
        private bool _shownSurge;

        /// <summary>RunOverlays wires its pause screen here. Null -> the button only logs.</summary>
        public System.Action PauseRequested;

        private void Awake()
        {
            if (bombButton) bombButton.gameObject.SetActive(false);
            if (weaponButton) weaponButton.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (pauseButton) pauseButton.onClick.AddListener(OnPausePressed);

            RunState.Changed += OnRunChanged;
            OnRunChanged();

            var bus = Bill.Events;
            if (bus == null) return;
            bus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            bus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        private void OnDisable()
        {
            RunState.Changed -= OnRunChanged;
            if (pauseButton) pauseButton.onClick.RemoveListener(OnPausePressed);

            var bus = Bill.Events;
            if (bus == null) return;
            bus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            bus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        // Duration ticks every frame without raising RunState.Changed, so the clock polls - but only
        // rebuilds the string when the whole second, tier or level actually changes.
        private void Update() => RefreshRunPill();

        private void RefreshRunPill()
        {
            if (runPill == null) return;
            var run = RunState.Current;
            int seconds = run != null ? Mathf.FloorToInt(run.Duration) : 0;
            var director = Threat.ThreatDirector.Instance;
            int tier = director != null ? director.CurrentTier : 0;
            bool surge = director != null && director.Surging;
            int level = run?.Level ?? 1;
            if (seconds == _shownSeconds && tier == _shownTier && level == _shownLevel && surge == _shownSurge) return;

            _shownSeconds = seconds; _shownTier = tier; _shownLevel = level; _shownSurge = surge;
            runPill.text = FormatRunPill(seconds, tier, level, surge);
        }

        public static string FormatClock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        /// <summary>"3:42 · Threat 2 · Lv 7", or a red HORDE! lead during a surge.</summary>
        public static string FormatRunPill(int seconds, int tier, int level, bool surge) =>
            surge
                ? $"<color=#FF4A3D>HORDE!</color> {FormatClock(seconds)} · Threat {tier} · Lv {level}"
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

        private void OnPlayerDamaged(PlayerDamagedEvent e) => SetHp(e.Normalized, e.Current);

        // HP drops to 0 at once; the result screen waits for GameOverEvent so the death plays out.
        private void OnPlayerDied(PlayerDiedEvent e) => SetHp(0f, 0f);

        private void SetHp(float normalized, float current)
        {
            if (healthFillRect)
                healthFillRect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            // Compact format (12.3K) - the label sits INSIDE the bar and must never overflow it.
            if (healthLabel) healthLabel.text = ZombieWar.UI.CurrencyClusterWidget.Format(Mathf.CeilToInt(current));
            if (healthFillImage)
                healthFillImage.color = normalized < 0.3f
                    ? new Color(0.898f, 0.282f, 0.302f)
                    : new Color(0.298f, 0.686f, 0.431f);
        }
    }
}
