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

        [Header("Victory")]
        [SerializeField] private GameObject victoryRoot;
        [SerializeField] private Button victoryHomeButton;

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
            Wire(victoryHomeButton, () => { Time.timeScale = 1f; GameFlow.ReturnToMenu(); });
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
            // Terminal presentation subscribes ONLY to RunFinishedEvent (M5.1.2 CP3). It is fired
            // exactly once by RunDirector after RunClosure's first-wins close, so the screen can
            // never disagree with the frozen ledger. Raw GameOverEvent/AllWavesClearedEvent used to
            // drive the roots directly - a late death after a locked Victory repainted the screen
            // as Defeat while the payout stayed Victory.
            Bill.Events?.Subscribe<RunFinishedEvent>(OnRunFinished);
            RunState.LevelsGained += OnLevelsGained;
        }

        private void OnDisable()
        {
            Bill.Events?.Unsubscribe<RunFinishedEvent>(OnRunFinished);
            RunState.LevelsGained -= OnLevelsGained;
        }

        // ------------------------------------------------------------ result binding
        // The terminal screens were installed with placeholder numbers; every value shown to the
        // player MUST come from the closed run's RunClosure.Result. Subscription order between this
        // component and RunDirector is not guaranteed, so binding happens from whichever side runs
        // last: the finished event (overlay may already be up) or the overlay show (result may
        // already be cached).
        private RunClosure.Result _result;
        private bool _hasResult;
        private bool _terminalShown;

        private void OnRunFinished(RunFinishedEvent e)
        {
            // A replayed event must not re-run the terminal transition (RunDirector fires once;
            // this guard makes the UI robust even if something replays it).
            if (_terminalShown) return;
            _terminalShown = true;

            _result = e.Result;
            _hasResult = true;
            ShowTerminal(e.Result.Summary.Outcome);
        }

        // The single terminal transition: every other overlay yields, coroutines stop, timeScale
        // returns to 1, and the root is chosen from the FROZEN summary - never from a raw event.
        private void ShowTerminal(RunOutcome outcome)
        {
            Restart(null);
            StopFtueWatch();
            Show(pauseRoot, false);
            Show(confirmRoot, false);
            Show(settingsRoot, false);
            Show(reviveRoot, false);
            Show(levelUpRoot, false);
            Show(ftueRoot, false);
            if (resumeCountText != null) resumeCountText.gameObject.SetActive(false);
            Time.timeScale = 1f;

            if (outcome == RunOutcome.Victory)
            {
                Show(gameOverRoot, false);
                Show(victoryRoot, true);
                BindVictory();
            }
            else
            {
                Show(victoryRoot, false);
                Show(gameOverRoot, true);
                BindGameOver();
            }
        }

        private void BindGameOver()
        {
            if (!_hasResult || gameOverRoot == null) return;
            var s = _result.Summary;
            long keptCoin = _result.BankedCoin;
            long firstClear = _result.FirstClearCoin;
            long gems = _result.BankedGem + _result.FirstClearGem;

            SetText("RecordPill/L", $"Reached  Wave {s.WaveReached}");
            SetText("PayoutCard/Row0L", "Collected in run");
            SetText("PayoutCard/Row0V", $"+{s.Coin:N0}");
            SetText("PayoutCard/Row1L", s.Outcome == RunOutcome.Defeat
                ? $"Kept ({RunClosure.DefeatCoinFraction:P0})" : "Kept");
            SetText("PayoutCard/Row1V", $"+{keptCoin:N0}");
            SetText("PayoutCard/Row2L", "First-clear bonus");
            SetText("PayoutCard/Row2V", $"+{firstClear:N0}");
            SetText("PayoutCard/TotalV", $"{keptCoin + firstClear:N0}");
            SetText("PayoutCard/KcRow", $"Gems kept  +{gems}");
            SetShown("PayoutCard/KcRow", gems > 0);

            // No live pass-XP value exists yet; an invented number is worse than nothing.
            SetShown("PassXpBar", false);
            SetShown("PassXpLabel", false);
        }

        private void BindVictory()
        {
            if (!_hasResult || victoryRoot == null) return;
            var s = _result.Summary;
            long total = _result.BankedCoin + _result.FirstClearCoin;
            var sub = victoryRoot.transform.Find("Subtitle")?.GetComponent<TMP_Text>();
            if (sub != null)
                sub.text = $"Banked +{total:N0} coin  ·  {s.Kills} kills  ·  wave {s.WaveReached}";
        }

        private void SetText(string path, string value)
        {
            var t = gameOverRoot.transform.Find(path)?.GetComponent<TMP_Text>();
            if (t != null) t.text = value;
        }

        private void SetShown(string path, bool shown)
        {
            var t = gameOverRoot.transform.Find(path);
            if (t != null) t.gameObject.SetActive(shown);
        }

        private void Start()
        {
            if (PlayerPrefs.GetInt("ftue_done", 0) == 0)
            {
                Show(ftueRoot, true);
                _ftueWatch = StartCoroutine(CoFtueWatchJoystick());
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;   // scene unload giữa lúc pause không được để game đứng hình
        }

        private bool TerminalOverlayActive =>
            (gameOverRoot != null && gameOverRoot.activeSelf) ||
            (victoryRoot != null && victoryRoot.activeSelf);

        // ------------------------------------------------------------ pause
        public void ShowPause()
        {
            if (TerminalOverlayActive) return;
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
            TryShowLevelUp();   // level-ups earned before/during the pause were held back
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
            if (TerminalOverlayActive) return;
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
            TryShowLevelUp();
        }

        // ------------------------------------------------------------ level-up
        // Real flow: RunState.LevelsGained -> queue -> pause + 1-of-3 offer -> AddPerk -> resume.
        // Level-ups earned while paused or while another overlay is up stay queued and present as
        // soon as the screen is free again, so no earned choice is ever dropped.
        private int _pendingLevelUps;
        private System.Collections.Generic.List<RunPerk> _offer;

        // M7.2 — the 23-card offer. It replaces the 7-perk draw as the source of the 1-of-3, but the
        // legacy `_offer` path is kept as a fallback so a run with no SkillRuntime still works
        // exactly as before. Binding reuses the SAME prefab paths (Perk{i}/Name, Perk{i}/Desc), which
        // is what lets this land without any UI prefab edit.
        private System.Collections.Generic.List<ZombieWar.Skills.SkillDef> _skillOffer;
        private float _levelUpShownAtRealtime;
        private const float LevelUpTimeoutSeconds = 30f;

        private void OnLevelsGained(int levels)
        {
            _pendingLevelUps += levels;
            TryShowLevelUp();
        }

        private void TryShowLevelUp()
        {
            if (_pendingLevelUps <= 0 || TerminalOverlayActive) return;
            if (levelUpRoot == null || levelUpRoot.activeSelf) return;
            if (pauseRoot != null && pauseRoot.activeSelf) return;
            if (reviveRoot != null && reviveRoot.activeSelf) return;
            var run = RunState.Current;
            if (run == null || run.IsOver) { _pendingLevelUps = 0; return; }

            var skills = ZombieWar.Skills.SkillRuntime.Active;
            _skillOffer = null;
            _offer = null;

            if (skills != null)
            {
                var weapon = PlayerMovement.Instance != null
                    ? PlayerMovement.Instance.GetComponentInChildren<Weapon>() : null;
                if (weapon != null && weapon.Current != null) skills.EquippedFamily = weapon.Current.weaponClass;

                _skillOffer = ZombieWar.Skills.SkillOfferBuilder.Build(
                    skills, skills.EquippedFamily, run.Seed, run.Level);

                if (_skillOffer.Count == 0)
                {
                    // Pool exhausted: there is no choice to make, so do not steal a pause for it.
                    _pendingLevelUps = Mathf.Max(0, _pendingLevelUps - 1);
                    return;
                }

                for (int i = 0; i < _skillOffer.Count; i++)
                {
                    var def = _skillOffer[i];
                    int nextRank = skills.RankOf(def.id) + 1;
                    BindOfferText($"Perk{i}/Name", nextRank > 1 ? $"{def.displayName}  {nextRank}" : def.displayName);
                    BindOfferText($"Perk{i}/Desc", DescribeCard(def, nextRank));
                }
            }
            else
            {
                _offer = RunPerkPool.Draw(perkButtons != null ? perkButtons.Length : 3);
                for (int i = 0; i < _offer.Count; i++)
                {
                    BindOfferText($"Perk{i}/Name", _offer[i].title);
                    BindOfferText($"Perk{i}/Desc", _offer[i].description);
                }
            }

            _levelUpShownAtRealtime = Time.realtimeSinceStartup;
            Time.timeScale = 0f;
            Show(levelUpRoot, true);
        }

        /// The <=30 s unscaled pause. On expiry it auto-picks a VALID card — never a broken or
        /// ineligible one — so a player who walks away never loses an earned choice or gets stuck on
        /// a frozen screen.
        private void Update()
        {
            if (levelUpRoot == null || !levelUpRoot.activeSelf) return;
            if (_skillOffer == null || _skillOffer.Count == 0) return;
            if (Time.realtimeSinceStartup - _levelUpShownAtRealtime < LevelUpTimeoutSeconds) return;

            var skills = ZombieWar.Skills.SkillRuntime.Active;
            var auto = ZombieWar.Skills.SkillOfferBuilder.AutoPick(_skillOffer, skills);
            int slot = auto == null ? 0 : _skillOffer.IndexOf(auto);
            PickPerk(Mathf.Max(0, slot));
        }

        private void BindOfferText(string path, string value)
        {
            var t = levelUpRoot.transform.Find(path)?.GetComponent<TMP_Text>();
            if (t != null) t.text = value;
        }

        /// <summary>Test hook: shows the overlay with a fresh offer without needing earned XP.</summary>
        public void ShowLevelUp()
        {
            if (TerminalOverlayActive) return;
            _pendingLevelUps = Mathf.Max(_pendingLevelUps, 1);
            TryShowLevelUp();
        }

        /// One-line card description, generated so no prefab text needs authoring per card.
        private static string DescribeCard(ZombieWar.Skills.SkillDef def, int rank)
        {
            float v = def.ValueAt(rank);
            string magnitude = Mathf.Abs(v) < 3f ? $"{v * 100f:0}%" : $"{v:0.#}";
            return def.layer switch
            {
                ZombieWar.Skills.SkillLayer.Stat => $"{magnitude} — always on",
                ZombieWar.Skills.SkillLayer.Signature => $"{def.family} signature · {magnitude}",
                ZombieWar.Skills.SkillLayer.Autonomous => $"Automatic power · {magnitude}",
                _ => $"Any weapon · {magnitude}",
            };
        }

        private void PickPerk(int slot)
        {
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (skills != null && _skillOffer != null)
            {
                if (slot >= 0 && slot < _skillOffer.Count)
                {
                    skills.Take(_skillOffer[slot].id);

                    // Max Health is the one card that must act at pick time; the Health component
                    // owns the number, exactly as the legacy MaxHealth perk did.
                    float bonus = skills.ConsumeMaxHealthBonus();
                    if (bonus > 0f) PlayerMovement.Instance?.GetComponent<Health>()?.IncreaseMax(1f + bonus);
                }

                _skillOffer = null;
                _pendingLevelUps = Mathf.Max(0, _pendingLevelUps - 1);
                Show(levelUpRoot, false);
                Time.timeScale = 1f;
                TryShowLevelUp();
                return;
            }

            var run = RunState.Current;
            if (_offer != null && slot < _offer.Count && run != null && !run.IsOver)
            {
                var perk = _offer[slot];
                run.AddPerk(perk);

                // MaxHealth is the one perk that must act at pick time: the multiplier has no
                // continuous consumer, the player's Health component owns the number.
                if (perk.kind == RunPerkKind.MaxHealth)
                    PlayerMovement.Instance?.GetComponent<Health>()?.IncreaseMax(perk.multiplier);
            }

            _offer = null;
            _pendingLevelUps = Mathf.Max(0, _pendingLevelUps - 1);
            Show(levelUpRoot, false);
            Time.timeScale = 1f;
            TryShowLevelUp();   // more queued level-ups present immediately, one choice each
        }

        // ------------------------------------------------------------ ftue
        private Coroutine _ftueWatch;

        private void CompleteFtue()
        {
            PlayerPrefs.SetInt("ftue_done", 1);
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
            while (ftueRoot != null && ftueRoot.activeSelf)
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
            Show(gameOverRoot, false);
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
