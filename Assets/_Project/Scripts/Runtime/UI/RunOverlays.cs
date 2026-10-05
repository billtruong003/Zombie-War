using System.Collections;
using System.Collections.Generic;
using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar
{
    /// <summary>
    /// In-run overlays: Pause + Settings (RunOverlays.Pause.cs), Level-up (RunOverlays.LevelUp.cs),
    /// Chest (RunOverlays.Chest.cs), the hand-off to the result screen (RunEndV2) and the move FTUE.
    /// The widgets are authored in the HUD prefab; this class only wires and sequences them, and is
    /// the ONE place on the UI side that touches Time.timeScale.
    /// </summary>
    public partial class RunOverlays : MonoBehaviour
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

        [Header("Retired V1 revive (kept hidden; the revive is RunEndV2's)")]
        [SerializeField] private GameObject reviveRoot;

        [Header("Level-up (§4.7 — presentation-only)")]
        [SerializeField] private GameObject levelUpRoot;
        [SerializeField] private Button[] perkButtons;

        [Header("Result")]
        [FormerlySerializedAs("gameOverRoot")]
        [SerializeField] private GameObject resultRoot;
        [Tooltip("M10: the v2 revive + result panels. When set, the result shows there instead.")]
        [SerializeField] private ZombieWar.UI.RunEndV2 endV2;

        [Header("Retired (hidden until removed from the prefab)")]
        [SerializeField] private GameObject victoryRoot;

        [Header("FTUE (§4.12)")]
        [SerializeField] private GameObject ftueRoot;
        [SerializeField] private Button ftueSkipButton;

        private Coroutine _routine;

        private void Awake()
        {
            HideAll();
            Wire(resumeButton, ResumeWithCountdown);
            Wire(chest.claim, ClaimChest);
            // The quit question replaces the pause panel instead of sitting on top of it.
            Wire(exitButton, () => { Show(pauseRoot, false); Show(confirmRoot, true); UIFx.ModalIn(confirmRoot != null ? confirmRoot.transform : null); });
            Wire(confirmNoButton, () => { Show(confirmRoot, false); Show(pauseRoot, true); });
            Wire(confirmYesButton, EndRun);
            Wire(settingsButton, OpenSettings);
            Wire(settingsCloseButton, () => Show(settingsRoot, false));
            Wire(ftueSkipButton, CompleteFtue);
            if (perkButtons != null)
                for (int i = 0; i < perkButtons.Length; i++)
                {
                    int slot = i;
                    Wire(perkButtons[i], () => PickPerk(slot));
                }

            if (soundToggle != null)
            {
                soundToggle.SetIsOnWithoutNotify(!(Bill.Audio != null && Bill.Audio.GetVolume(AudioChannel.Master) <= 0f));
                soundToggle.onValueChanged.AddListener(on => Bill.Audio?.SetVolume(AudioChannel.Master, on ? 1f : 0f));
            }
            if (vibrateToggle != null)
            {
                vibrateToggle.SetIsOnWithoutNotify(GameSettings.Haptics);
                vibrateToggle.onValueChanged.AddListener(on => GameSettings.Haptics = on);
            }
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(GameSettings.Music);
                musicSlider.onValueChanged.AddListener(v => GameSettings.Music = v);
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(GameSettings.Sfx);
                sfxSlider.onValueChanged.AddListener(v => GameSettings.Sfx = v);
            }
            if (hapticToggle != null)
            {
                hapticToggle.SetIsOnWithoutNotify(GameSettings.Haptics);
                hapticToggle.onValueChanged.AddListener(on => GameSettings.Haptics = on);
            }

            var hud = GetComponent<HudController>();
            if (hud != null) hud.PauseRequested = ShowPause;
        }

        private void OnEnable()
        {
            // Terminal presentation subscribes ONLY to RunFinishedEvent. It is fired exactly once by
            // RunDirector after RunClosure's first-wins close, so the screen can never disagree with
            // the frozen ledger.
            Bill.Events?.Subscribe<RunFinishedEvent>(OnRunFinished);
            Bill.Events?.Subscribe<AppPauseEvent>(OnAppPause);
            Bill.Events?.Subscribe<CardOfferRequestedEvent>(OnCardOfferRequested);
            RunState.LevelsGained += OnLevelsGained;
            PickupManager.ChestCollected += OnChestCollected;
        }

        private void OnDisable()
        {
            Bill.Events?.Unsubscribe<RunFinishedEvent>(OnRunFinished);
            Bill.Events?.Unsubscribe<AppPauseEvent>(OnAppPause);
            Bill.Events?.Unsubscribe<CardOfferRequestedEvent>(OnCardOfferRequested);
            RunState.LevelsGained -= OnLevelsGained;
            PickupManager.ChestCollected -= OnChestCollected;
        }

        // ------------------------------------------------------------ result binding
        // Every value shown on the result screen comes from the closed run's RunClosure.Result, never
        // from the prefab's placeholder numbers or a live RunState that may already be cleared.
        private bool _terminalShown;

        private void OnRunFinished(RunFinishedEvent e)
        {
            // A replayed event must not re-run the terminal transition.
            if (_terminalShown) return;
            _terminalShown = true;

            ShowResult(e.Result);
        }

        // The single terminal transition: every other overlay yields, coroutines stop and the world
        // freezes behind the result. On a walk-away the player is still alive, and nothing may keep
        // hitting them while they read what they forfeited.
        private void ShowResult(RunClosure.Result result)
        {
            Restart(null);
            StopFtueWatch();
            Show(pauseRoot, false);
            Show(confirmRoot, false);
            Show(settingsRoot, false);
            Show(levelUpRoot, false);
            Show(ftueRoot, false);
            if (resumeCountText != null) resumeCountText.gameObject.SetActive(false);

            Show(resultRoot, false);   // the retired V1 result; RunEndV2 is the result screen
            if (endV2 != null) endV2.ShowResult(result);
            else { Time.timeScale = 0f; Debug.LogError("[RunOverlays] No result screen (endV2) wired."); }
        }

        private void Start()
        {
            // Radio subtitles hide while any of these is up (owner decision 04/10).
            foreach (var m in new[] { pauseRoot, confirmRoot, settingsRoot, levelUpRoot, ChestRoot }) FtueRadio.RegisterModal(m);
            foreach (var m in new[] { pauseRoot, confirmRoot, settingsRoot }) FtueRadio.RegisterBlocker(m);

            Show(ftueRoot, false);   // the v2 move overlay (prefab) stays hidden; the radio draws the step
            if (!Ftue.Done(Ftue.Move))
            {
                FtueV3.Move();
                _ftueWatch = StartCoroutine(CoFtueWatchJoystick());
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;   // scene unload giữa lúc pause không được để game đứng hình
        }

        // The result screen is up: nothing else may open over it (resultRoot is the retired V1 one,
        // always hidden, so it could not tell).
        private bool TerminalOverlayActive => _terminalShown;

        // ------------------------------------------------------------ ftue
        private Coroutine _ftueWatch;

        private void CompleteFtue()
        {
            Ftue.Complete(Ftue.Move);
            Show(ftueRoot, false);
            StopFtueWatch();
        }

        private void StopFtueWatch()
        {
            if (_ftueWatch == null) return;
            StopCoroutine(_ftueWatch);
            _ftueWatch = null;
        }

        // FTUE dạy "kéo để chạy" — làm đúng hành động đó là tutorial coi như xong, tự đóng.
        // Coroutine riêng, không dùng _routine: pause/revive gọi Restart() sẽ giết nhầm watcher.
        private IEnumerator CoFtueWatchJoystick()
        {
            var joy = GetComponentInChildren<VirtualJoystick>(true);
            float held = 0f;
            while (!Ftue.Done(Ftue.Move))
            {
                if (joy != null && joy.Direction.sqrMagnitude > 0.04f) held += Time.unscaledDeltaTime;
                else held = 0f;
                if (held >= 0.5f) { CompleteFtue(); yield break; }
                yield return null;
            }
            _ftueWatch = null;
        }

        // ------------------------------------------------------------ utils
        private void HideAll()
        {
            Show(pauseRoot, false);
            Show(confirmRoot, false);
            Show(settingsRoot, false);
            Show(reviveRoot, false);
            Show(levelUpRoot, false);
            Show(resultRoot, false);
            Show(victoryRoot, false);
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
