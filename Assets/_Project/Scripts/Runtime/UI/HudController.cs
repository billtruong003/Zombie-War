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
        [SerializeField] private TMP_Text weaponLabel;          // "Rifle 30/30"
        [SerializeField] private Image ammoRing;                // ring radial fill = đạn còn

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

        /// <summary>Phase Pause/GameOver wire vào đây (PauseOverlay). Null → nút pause log fail-safe.</summary>
        public System.Action PauseRequested;

        private void OnEnable()
        {
            if (gameOverPanel) gameOverPanel.SetActive(false);
            if (victoryPanel) victoryPanel.SetActive(false);
            if (weaponButton) weaponButton.onClick.AddListener(OnWeaponPressed);
            if (bombButton) bombButton.onClick.AddListener(OnBombPressed);
            if (pauseButton) pauseButton.onClick.AddListener(OnPausePressed);

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
                bombLabel.text = _bomb.CooldownRemaining > 0.05f
                    ? $"{_bomb.CooldownRemaining:0.0}s"
                    : $"x{_bomb.BombsRemaining}";

            if (weaponIcon)
            {
                // reload đọc bằng icon xoay đúng 1 vòng — không có nút reload
                float z = _weapon.IsReloading ? -360f * _weapon.ReloadProgress : 0f;
                var e = weaponIcon.rectTransform.localEulerAngles;
                weaponIcon.rectTransform.localEulerAngles = new Vector3(e.x, e.y, z);

                if (_weapon.Current != _iconBound && prototypeCatalog != null)
                {
                    _iconBound = _weapon.Current;
                    var s = prototypeCatalog.GetWeaponIcon(_iconBound);
                    if (s != null) { weaponIcon.sprite = s; weaponIcon.color = Color.white; }
                }
            }

            if (ammoRing && _weapon.MagazineSize > 0)
                ammoRing.fillAmount = (float)_weapon.AmmoInMag / _weapon.MagazineSize;

            if (weaponLabel)
            {
                var d = _weapon.Current;
                weaponLabel.text = d != null ? $"{_weapon.AmmoInMag}" : "";
            }
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
            if (wavePill) wavePill.text = $"Wave {e.WaveNumber} ✓";
        }

        private void OnAllWavesCleared(AllWavesClearedEvent e)
        {
            if (wavePill) wavePill.text = "ALL CLEAR";
            if (victoryPanel) victoryPanel.SetActive(true);
        }

        private void OnZombieCountChanged(ZombieCountChangedEvent e)
        {
            _alive = e.AliveCount;
            RefreshWavePill();
        }

        private void RefreshWavePill()
        {
            if (wavePill) wavePill.text = $"Wave {_wave} — {_alive}";
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
            if (gameOverPanel) gameOverPanel.SetActive(true);
        }
    }
}
