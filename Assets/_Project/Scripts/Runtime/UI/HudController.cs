using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// <summary>
    /// HUD in-run (spec §4.6, Sheet B): HP pill TL + Wave pill TC + Coin pill TR + Pause TR,
    /// Bomb + Weapon slot bên phải trong thumb-zone. Auto-aim/auto-fire — KHÔNG có nút bắn/reload.
    /// Wave/health qua Bill.Events (không tham chiếu gameplay trực tiếp); riêng weapon slot cần
    /// Weapon sống để switch + đọc đạn nên lazily resolve player spawn lúc runtime.
    /// Mọi field optional — thiếu widget không NRE. Widgets do HudInstaller dựng sẵn trong scene.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("Top pills")]
        [SerializeField] private RectTransform healthFillRect;  // fill pill — scale theo anchorMax.x
        [SerializeField] private TMP_Text healthLabel;          // "100"
        [SerializeField] private TMP_Text wavePill;             // "Wave 3 — 12"
        [SerializeField] private TMP_Text coinPill;             // coin in-run (backend chưa có — giữ 0)
        [SerializeField] private Image healthFillImage;         // đổi màu khi <30%

        [Header("Buttons")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button bombButton;
        [SerializeField] private TMP_Text bombLabel;            // "x3" / cooldown
        [SerializeField] private Button weaponButton;           // tap = switch weapon
        [SerializeField] private Image weaponIcon;              // xoay 1 vòng khi reload
        [SerializeField] private TMP_Text weaponLabel;          // ten sung dang cam (M4: khong con dan)
        [SerializeField] private Image ammoRing;                // M4: khong con y nghia, bi tat luc chay

        [Header("Overlays (legacy — thay ở đợt overlay screens)")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;

        [Header("Prototype data (icon súng cho weapon slot)")]
        [SerializeField] private ZombieWar.UI.UIPrototypeCatalog prototypeCatalog;

        private int _wave, _alive;
        private Weapon _weapon;
        private BombThrower _bomb;
        private PlayerMovement _player;
        private WeaponData _iconBound;
        private float _hp01 = 1f;

        // Cache giá trị đã hiển thị — TMP .text set mỗi frame là nguồn GC string chính của HUD.
        private WeaponData _labelBound;
        private int _shownBombs = int.MinValue;
        private int _shownCooldownTenths = int.MinValue;
        private long _shownRunCoin = long.MinValue;
        private int _shownLevel = 1;

        /// <summary>Phase Pause/GameOver wire vào đây (PauseOverlay). Null → nút pause log fail-safe.</summary>
        public System.Action PauseRequested;

        private void OnEnable()
        {
            if (gameOverPanel) gameOverPanel.SetActive(false);
            if (victoryPanel) victoryPanel.SetActive(false);
            if (weaponButton) weaponButton.onClick.AddListener(OnWeaponPressed);
            if (bombButton) bombButton.onClick.AddListener(OnBombPressed);
            if (pauseButton) pauseButton.onClick.AddListener(OnPausePressed);

            RunState.Changed += OnRunChanged;
            OnRunChanged();

            var bus = Bill.Events;
            if (bus == null) return;
            bus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            bus.Subscribe<WaveClearedEvent>(OnWaveCleared);
            bus.Subscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            bus.Subscribe<ZombieCountChangedEvent>(OnZombieCountChanged);
            bus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            bus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            bus.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnDisable()
        {
            RunState.Changed -= OnRunChanged;
            if (weaponButton) weaponButton.onClick.RemoveListener(OnWeaponPressed);
            if (bombButton) bombButton.onClick.RemoveListener(OnBombPressed);
            if (pauseButton) pauseButton.onClick.RemoveListener(OnPausePressed);

            var bus = Bill.Events;
            if (bus == null) return;
            bus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            bus.Unsubscribe<WaveClearedEvent>(OnWaveCleared);
            bus.Unsubscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            bus.Unsubscribe<ZombieCountChangedEvent>(OnZombieCountChanged);
            bus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            bus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            bus.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        // ------------------------------------------------------------------ weapon/bomb slot

        private void Update()
        {
            if (_weapon == null)
            {
                _weapon = FindFirstObjectByType<Weapon>();
                if (_weapon == null) return;
            }
            if (_bomb == null) _bomb = _weapon.GetComponent<BombThrower>();
            if (_player == null) _player = PlayerMovement.Instance;

            if (bombLabel != null && _bomb != null)
            {
                // Chỉ đổi text khi giá trị hiển thị đổi thật — không alloc string mỗi frame.
                if (_bomb.CooldownRemaining > 0.05f)
                {
                    int tenths = Mathf.CeilToInt(_bomb.CooldownRemaining * 10f);
                    if (tenths != _shownCooldownTenths)
                    {
                        _shownCooldownTenths = tenths;
                        _shownBombs = int.MinValue;
                        bombLabel.text = $"{tenths / 10f:0.0}s";
                    }
                }
                else if (_bomb.BombsRemaining != _shownBombs)
                {
                    _shownBombs = _bomb.BombsRemaining;
                    _shownCooldownTenths = int.MinValue;
                    bombLabel.text = $"x{_shownBombs}";
                }
            }

            if (weaponIcon)
            {
                if (_weapon.Current != _iconBound && prototypeCatalog != null)
                {
                    _iconBound = _weapon.Current;
                    var s = prototypeCatalog.GetWeaponIcon(_iconBound);
                    if (s != null) { weaponIcon.sprite = s; weaponIcon.color = Color.white; }
                }
            }

            // M4: weapons have no magazine, so the ring has nothing to report. It is HIDDEN rather
            // than left full - a permanently complete gauge tells the player ammunition still exists
            // and is merely topped up, which is exactly the wrong reading.
            //
            // The Image is disabled from code instead of deleted from the prefab: HUD prefabs are
            // owner-authored, and silently restructuring one to satisfy a gameplay change is not
            // this milestone's call.
            if (ammoRing && ammoRing.enabled) ammoRing.enabled = false;

            // The label now carries weapon IDENTITY instead of a round count - the thing that still
            // varies and that the player actually chooses between.
            if (weaponLabel && !ReferenceEquals(_weapon.Current, _labelBound))
            {
                _labelBound = _weapon.Current;
                weaponLabel.text = _labelBound != null ? _labelBound.weaponName : "";
            }
        }

        // Coin pill bind RunState thật: nhặt vàng/nổ thùng/giết quái → số nhảy ngay.
        private void OnRunChanged()
        {
            int level = RunState.Current?.Level ?? 1;
            if (level != _shownLevel)
            {
                _shownLevel = level;
                RefreshWavePill();
            }

            if (coinPill == null) return;
            long coin = RunState.Current?.Coin ?? 0;
            if (coin == _shownRunCoin) return;
            _shownRunCoin = coin;
            coinPill.text = ZombieWar.UI.CurrencyClusterWidget.Format(coin);
            ZombieWar.UI.UIFx.Punch(coinPill.transform);
        }

        private void OnWeaponPressed()
        {
            if (_weapon == null) _weapon = FindFirstObjectByType<Weapon>();
            if (_weapon != null) _weapon.SwitchWeapon();
        }

        private void OnBombPressed()
        {
            if (_bomb == null) return;
            Vector3 aim = _player != null ? _player.AimDirection : _bomb.transform.forward;
            _bomb.TryThrow(aim);
        }

        private void OnPausePressed()
        {
            if (PauseRequested != null) PauseRequested();
            else Debug.Log("[HudController] Pause: overlay chưa wire (đợt overlay screens).");
        }

        // ------------------------------------------------------------------ handlers

        private void OnWaveStarted(WaveStartedEvent e)
        {
            _wave = e.WaveNumber; _alive = e.ZombiesInWave;
            RefreshWavePill();
        }

        private void OnWaveCleared(WaveClearedEvent e)
        {
            // U+2713 CHECK MARK is not in LiberationSans SDF, so TMP substituted U+25A1 (a box) and
            // warned on every wave clear. Replaced with a character the shipped font actually has.
            if (wavePill) wavePill.text = $"Wave {e.WaveNumber} OK";
        }

        // Terminal presentation is owned by RunOverlays via RunFinishedEvent (first-wins,
        // M5.1.2 CP3). Raw wave/game-over events may only touch non-terminal HUD text here -
        // toggling the result roots from them let a late GameOverEvent repaint a locked Victory.
        private void OnAllWavesCleared(AllWavesClearedEvent e)
        {
            if (wavePill) wavePill.text = "ALL CLEAR";
        }

        private void OnZombieCountChanged(ZombieCountChangedEvent e)
        {
            _alive = e.AliveCount;
            RefreshWavePill();
        }

        private void RefreshWavePill()
        {
            // Level rides on the wave pill so the silent-level-up gap (M5 audit S6) is closed
            // without new HUD geometry; a real XP bar is redesign-phase work.
            if (wavePill) wavePill.text = $"Wave {_wave} — {_alive} · Lv {_shownLevel}";
        }

        private void OnPlayerDamaged(PlayerDamagedEvent e)
        {
            SetHp(e.Normalized, e.Current);
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            // HP về 0 ngay; màn thua đợi GameOverEvent cho death anim/FX kịp diễn.
            SetHp(0f, 0f);
        }

        private void SetHp(float normalized, float current)
        {
            _hp01 = normalized;
            if (healthFillRect)
                healthFillRect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            // Compact format (12.3K) — label nằm TRONG vùng HP bar, số dài không được tràn sang Wave pill
            if (healthLabel) healthLabel.text = ZombieWar.UI.CurrencyClusterWidget.Format(Mathf.CeilToInt(current));
            if (healthFillImage)
                healthFillImage.color = normalized < 0.3f
                    ? new Color(0.898f, 0.282f, 0.302f)   // danger khi <30% (Sheet C "Low HP")
                    : new Color(0.298f, 0.686f, 0.431f);
        }

        private void OnGameOver(GameOverEvent e)
        {
            // Intentionally empty for terminal roots - see the note on OnAllWavesCleared.
        }
    }
}
