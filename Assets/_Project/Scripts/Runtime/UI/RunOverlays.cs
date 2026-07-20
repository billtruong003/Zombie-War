using System.Collections;
using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// <summary>
    /// Overlay in-run (spec §4.7–§4.12): Pause / Revive / Level-up / Game Over / Settings / FTUE.
    /// Widgets do HudInstaller dựng sẵn trong Map_Level1; class này chỉ wire + lifecycle.
    /// Là NƠI DUY NHẤT đụng Time.timeScale phía UI — không rải mutation ra chỗ khác.
    /// Backend chưa có: revive/perk/ad là presentation-only (test hook ShowRevive/ShowLevelUp);
    /// KHÔNG tự bật theo PlayerDiedEvent để không chặn death→GameOver flow thật.
    /// </summary>
    public class RunOverlays : MonoBehaviour
    {
        [Header("Pause (§4.8)")]
        [SerializeField] private GameObject pauseRoot;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Toggle soundToggle;
        [SerializeField] private Toggle vibrateToggle;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject confirmRoot;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;
        [SerializeField] private TMP_Text resumeCountText;

        [Header("Settings (§4.11)")]
        [SerializeField] private GameObject settingsRoot;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle hapticToggle;
        [SerializeField] private Button settingsCloseButton;

        [Header("Revive (§4.9 — presentation-only)")]
        [SerializeField] private GameObject reviveRoot;
        [SerializeField] private TMP_Text reviveCountText;
        [SerializeField] private Button reviveAdButton;
        [SerializeField] private Button reviveSkipButton;

        [Header("Level-up (§4.7 — presentation-only)")]
        [SerializeField] private GameObject levelUpRoot;
        [SerializeField] private Button[] perkButtons;

        [Header("Game Over (§4.10)")]
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button homeButton;

        [Header("FTUE (§4.12)")]
        [SerializeField] private GameObject ftueRoot;
        [SerializeField] private Button ftueSkipButton;

        private Coroutine _routine;

        private void Awake()
        {
            HideAll();
            Wire(resumeButton, ResumeWithCountdown);
            Wire(exitButton, () => Show(confirmRoot, true));
            Wire(confirmNoButton, () => Show(confirmRoot, false));
            Wire(confirmYesButton, ExitRun);
            Wire(settingsButton, OpenSettings);
            Wire(settingsCloseButton, () => Show(settingsRoot, false));
            Wire(reviveAdButton, () =>
            {
                Debug.Log("[RunOverlays] Revive ad: chưa có ad SDK/economy (placeholder).");
                CloseRevive();
            });
            Wire(reviveSkipButton, CloseRevive);
            Wire(replayButton, () => { Time.timeScale = 1f; GameFlow.RestartGameplay(); });
            Wire(homeButton, () => { Time.timeScale = 1f; GameFlow.ReturnToMenu(); });
            Wire(ftueSkipButton, CompleteFtue);
            if (perkButtons != null)
                foreach (var b in perkButtons)
                    Wire(b, PickPerk);

            if (soundToggle != null)
            {
                soundToggle.SetIsOnWithoutNotify(!(Bill.Audio != null && Bill.Audio.GetVolume(AudioChannel.Master) <= 0f));
                soundToggle.onValueChanged.AddListener(on => Bill.Audio?.SetVolume(AudioChannel.Master, on ? 1f : 0f));
            }
            if (vibrateToggle != null)
            {
                vibrateToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("haptics", 1) == 1);
                vibrateToggle.onValueChanged.AddListener(on => PlayerPrefs.SetInt("haptics", on ? 1 : 0));
            }
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(Bill.Audio != null ? Bill.Audio.GetVolume(AudioChannel.Music) : 1f);
                musicSlider.onValueChanged.AddListener(v => Bill.Audio?.SetVolume(AudioChannel.Music, v));
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(Bill.Audio != null ? Bill.Audio.GetVolume(AudioChannel.SFX) : 1f);
                sfxSlider.onValueChanged.AddListener(v => Bill.Audio?.SetVolume(AudioChannel.SFX, v));
            }
            if (hapticToggle != null)
            {
                hapticToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("haptics", 1) == 1);
                hapticToggle.onValueChanged.AddListener(on => PlayerPrefs.SetInt("haptics", on ? 1 : 0));
            }

            var hud = GetComponent<HudController>();
            if (hud != null) hud.PauseRequested = ShowPause;
        }

        private void OnEnable()
        {
            Bill.Events?.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnDisable()
        {
            Bill.Events?.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        private void Start()
        {
            if (PlayerPrefs.GetInt("ftue_done", 0) == 0) Show(ftueRoot, true);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;   // scene unload giữa lúc pause không được để game đứng hình
        }

        private bool GameOverActive => gameOverRoot != null && gameOverRoot.activeSelf;

        // ------------------------------------------------------------ pause
        public void ShowPause()
        {
            if (GameOverActive) return;
            Time.timeScale = 0f;
            Show(pauseRoot, true);
        }

        private void ResumeWithCountdown()
        {
            Show(pauseRoot, false);
            Restart(CoResume());
        }

        private IEnumerator CoResume()
        {
            if (resumeCountText != null)
            {
                resumeCountText.gameObject.SetActive(true);
                for (int i = 3; i >= 1; i--)
                {
                    resumeCountText.text = i.ToString();
                    yield return new WaitForSecondsRealtime(0.6f);
                }
                resumeCountText.gameObject.SetActive(false);
            }
            Time.timeScale = 1f;
        }

        private void ExitRun()
        {
            Time.timeScale = 1f;
            Show(confirmRoot, false);
            Show(pauseRoot, false);
            GameFlow.ReturnToMenu();
        }

        private void OpenSettings() => Show(settingsRoot, true);

        // ------------------------------------------------------------ revive (test hook)
        public void ShowRevive()
        {
            if (GameOverActive) return;
            Time.timeScale = 0f;
            Show(reviveRoot, true);
            Restart(CoReviveCountdown());
        }

        private IEnumerator CoReviveCountdown()
        {
            for (int i = 5; i >= 0; i--)
            {
                if (reviveCountText != null) reviveCountText.text = i.ToString();
                if (i > 0) yield return new WaitForSecondsRealtime(1f);
            }
            CloseRevive();
        }

        private void CloseRevive()
        {
            Restart(null);
            Show(reviveRoot, false);
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------ level-up (test hook)
        public void ShowLevelUp()
        {
            if (GameOverActive) return;
            Time.timeScale = 0f;
            Show(levelUpRoot, true);
        }

        private void PickPerk()
        {
            // Perk backend (roadmap #48) chưa có — chọn chỉ đóng overlay.
            Show(levelUpRoot, false);
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------ game over
        // Terminal state: mutually exclusive với MỌI overlay khác (FTUE/settings/confirm gồm luôn),
        // dừng coroutine đang chạy (resume/revive countdown) để không giành timeScale sau đó.
        private void OnGameOver(GameOverEvent e)
        {
            Restart(null);
            Show(pauseRoot, false);
            Show(confirmRoot, false);
            Show(settingsRoot, false);
            Show(reviveRoot, false);
            Show(levelUpRoot, false);
            Show(ftueRoot, false);
            if (resumeCountText != null) resumeCountText.gameObject.SetActive(false);
            Time.timeScale = 1f;
            Show(gameOverRoot, true);
        }

        // ------------------------------------------------------------ ftue
        private void CompleteFtue()
        {
            PlayerPrefs.SetInt("ftue_done", 1);
            Show(ftueRoot, false);
        }

        // ------------------------------------------------------------ utils
        private void HideAll()
        {
            Show(pauseRoot, false);
            Show(confirmRoot, false);
            Show(settingsRoot, false);
            Show(reviveRoot, false);
            Show(levelUpRoot, false);
            Show(gameOverRoot, false);
            Show(ftueRoot, false);
            if (resumeCountText != null) resumeCountText.gameObject.SetActive(false);
        }

        private static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        private void Restart(IEnumerator routine)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = routine != null ? StartCoroutine(routine) : null;
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction fn)
        {
            if (b != null) b.onClick.AddListener(fn);
        }
    }
}
