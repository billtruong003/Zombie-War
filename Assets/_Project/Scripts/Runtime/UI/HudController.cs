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
        private BombThrower _bomb;
        private PlayerMovement _player;

        [Header("Bomb / Weapon roster (built at runtime)")]
        [SerializeField] private bool buildBombButton = true;
        [SerializeField] private bool buildWeaponRoster = true;   // data-driven from Weapon.Weapons
        private Button _bombButton;
        private Text _bombLabel;
        private RectTransform _rosterBar;
        private readonly System.Collections.Generic.List<Button> _rosterButtons = new();
        private int _rosterBuiltCount = -1;

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

        private void Start()
        {
            if (buildBombButton) BuildBombButton();
        }

        // ------------------------------------------------------------------ weapon slot

        private void Update()
        {
            if (_weapon == null)
            {
                _weapon = FindFirstObjectByType<Weapon>();
                if (_weapon == null) return;
            }

            if (_bomb == null) _bomb = _weapon.GetComponent<BombThrower>();
            if (_player == null) _player = PlayerMovement.Instance;
            EnsureRoster();
            RefreshRosterHighlight();
            if (_bombLabel != null && _bomb != null)
                _bombLabel.text = _bomb.CooldownRemaining > 0.05f
                    ? $"BOMB\n{_bomb.CooldownRemaining:0.0}s"
                    : $"BOMB x{_bomb.BombsRemaining}";

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

        // ------------------------------------------------------------------ runtime-built UI

        private void OnBombPressed()
        {
            if (_bomb == null) return;
            Vector3 aim = _player != null ? _player.AimDirection : _bomb.transform.forward;
            _bomb.TryThrow(aim);
        }

        private void BuildBombButton()
        {
            _bombButton = MakeButton("BombButton", transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 190f),
                new Vector2(220f, 110f), "BOMB", out _bombLabel);
            _bombButton.onClick.AddListener(OnBombPressed);
        }

        // Rebuilds only when roster size changes, so shrinking Weapon.weapons later just works.
        private void EnsureRoster()
        {
            if (!buildWeaponRoster || _weapon == null) return;
            int count = _weapon.Weapons != null ? _weapon.Weapons.Count : 0;
            if (count == _rosterBuiltCount) return;
            _rosterBuiltCount = count;

            if (_rosterBar != null) Destroy(_rosterBar.gameObject);
            _rosterButtons.Clear();
            if (count == 0) return;

            const float bw = 150f, bh = 70f, gap = 10f;
            float totalW = count * bw + (count - 1) * gap;

            var barGo = new GameObject("WeaponRoster", typeof(RectTransform));
            _rosterBar = barGo.GetComponent<RectTransform>();
            _rosterBar.SetParent(transform, false);
            _rosterBar.anchorMin = _rosterBar.anchorMax = _rosterBar.pivot = new Vector2(0.5f, 0f);
            _rosterBar.anchoredPosition = new Vector2(0f, 40f);
            _rosterBar.sizeDelta = new Vector2(totalW, bh);

            for (int i = 0; i < count; i++)
            {
                int idx = i;
                var wd = _weapon.Weapons[i];
                string nm = wd != null ? wd.weaponName : ("#" + i);
                float x = -totalW / 2f + bw / 2f + i * (bw + gap);
                var b = MakeButton("W" + i, _rosterBar,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                    new Vector2(bw, bh), nm, out _);
                b.onClick.AddListener(() => { if (_weapon != null) _weapon.EquipIndex(idx); });
                _rosterButtons.Add(b);
            }
        }

        private void RefreshRosterHighlight()
        {
            if (_weapon == null || _rosterButtons.Count == 0) return;
            int cur = _weapon.CurrentIndex;
            for (int i = 0; i < _rosterButtons.Count; i++)
            {
                var img = _rosterButtons[i].targetGraphic as Image;
                if (img != null)
                    img.color = i == cur ? new Color(0.85f, 0.55f, 0.1f, 0.9f)
                                         : new Color(0f, 0f, 0f, 0.55f);
            }
        }

        private static Button MakeButton(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, string text, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var trt = txtGo.GetComponent<RectTransform>();
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            label = txtGo.GetComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = 40;
            return go.GetComponent<Button>();
        }
    }
}
