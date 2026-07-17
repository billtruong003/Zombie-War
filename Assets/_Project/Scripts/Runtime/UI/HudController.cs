using BillGameCore;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// <summary>
    /// Screen-space HUD. Holds zero references to gameplay systems for wave/health (event bus only).
    /// The weapon slot is the one exception: it needs the live Weapon to switch + read reload progress,
    /// so it lazily resolves the runtime-spawned player. Wired up by SceneFlowBuilder; every field is
    /// optional so a missing widget never NREs.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("Wave")]
        [SerializeField] private Text waveLabel;          // "WAVE 3 / 5"
        [SerializeField] private Text zombieLabel;        // "ZOMBIES: 12"

        [Header("Health")]
        [SerializeField] private Image healthFill;        // horizontal fill (Filled image)
        [SerializeField] private Text healthLabel;        // "100 / 100"

        [Header("Weapon")]
        [SerializeField] private Button weaponButton;     // tap = switch to next weapon
        [SerializeField] private Image weaponIcon;        // placeholder icon; spins 1 full turn while reloading
        [SerializeField] private Text weaponLabel;        // "Rifle\n30/30" (name + ammo)

        [Header("Overlays")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;

        private int _totalWaves;
        private Weapon _weapon;

        private void OnEnable()
        {
            if (gameOverPanel) gameOverPanel.SetActive(false);
            if (victoryPanel) victoryPanel.SetActive(false);
            if (weaponButton) weaponButton.onClick.AddListener(OnWeaponPressed);

            var bus = Bill.Events;
            if (bus == null) return;
            bus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            bus.Subscribe<WaveClearedEvent>(OnWaveCleared);
            bus.Subscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            bus.Subscribe<ZombieCountChangedEvent>(OnZombieCountChanged);
            bus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            bus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        private void OnDisable()
        {
            if (weaponButton) weaponButton.onClick.RemoveListener(OnWeaponPressed);

            var bus = Bill.Events;
            if (bus == null) return;
            bus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            bus.Unsubscribe<WaveClearedEvent>(OnWaveCleared);
            bus.Unsubscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            bus.Unsubscribe<ZombieCountChangedEvent>(OnZombieCountChanged);
            bus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            bus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        // ------------------------------------------------------------------ weapon slot

        private void Update()
        {
            if (_weapon == null)
            {
                _weapon = FindFirstObjectByType<Weapon>();
                if (_weapon == null) return;
            }

            if (weaponIcon)
            {
                // Image-style reload: the icon rotates exactly one full turn over the reload duration.
                float z = _weapon.IsReloading ? -360f * _weapon.ReloadProgress : 0f;
                var e = weaponIcon.rectTransform.localEulerAngles;
                weaponIcon.rectTransform.localEulerAngles = new Vector3(e.x, e.y, z);
            }

            if (weaponLabel)
            {
                var d = _weapon.Current;
                weaponLabel.text = d != null ? $"{d.weaponName}\n{_weapon.AmmoInMag}/{_weapon.MagazineSize}" : "";
            }
        }

        private void OnWeaponPressed()
        {
            if (_weapon == null) _weapon = FindFirstObjectByType<Weapon>();
            if (_weapon != null) _weapon.SwitchWeapon();
        }

        // ------------------------------------------------------------------ handlers

        private void OnWaveStarted(WaveStartedEvent e)
        {
            _totalWaves = e.TotalWaves;
            if (waveLabel) waveLabel.text = $"WAVE {e.WaveNumber} / {e.TotalWaves}";
            if (zombieLabel) zombieLabel.text = $"ZOMBIES: {e.ZombiesInWave}";
        }

        private void OnWaveCleared(WaveClearedEvent e)
        {
            if (waveLabel) waveLabel.text = _totalWaves > 0
                ? $"WAVE {e.WaveNumber} CLEARED"
                : "WAVE CLEARED";
        }

        private void OnAllWavesCleared(AllWavesClearedEvent e)
        {
            if (waveLabel) waveLabel.text = "ALL WAVES CLEARED";
            if (victoryPanel) victoryPanel.SetActive(true);
        }

        private void OnZombieCountChanged(ZombieCountChangedEvent e)
        {
            if (zombieLabel) zombieLabel.text = $"ZOMBIES: {e.AliveCount}";
        }

        private void OnPlayerDamaged(PlayerDamagedEvent e)
        {
            if (healthFill) healthFill.fillAmount = e.Normalized;
            if (healthLabel) healthLabel.text = $"{Mathf.CeilToInt(e.Current)} / {Mathf.CeilToInt(e.Max)}";
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            if (healthFill) healthFill.fillAmount = 0f;
            if (healthLabel) healthLabel.text = "0";
            if (gameOverPanel) gameOverPanel.SetActive(true);
        }
    }
}
