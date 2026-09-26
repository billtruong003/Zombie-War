using System.Collections;
using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar
{
    /// <summary>
    /// In-run overlays: Pause / Revive / Level-up / Result / Settings / FTUE.
    /// The widgets are authored in the HUD prefab; this class only wires and sequences them, and is
    /// the ONE place on the UI side that touches Time.timeScale.
    /// Revive has no ad backend yet, so it is presentation-only (test hook ShowRevive) and never
    /// opens on PlayerDiedEvent, which would block the real death -> result flow.
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

        [Header("Result")]
        [FormerlySerializedAs("gameOverRoot")]
        [SerializeField] private GameObject resultRoot;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button homeButton;

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
            Wire(exitButton, () => { Show(confirmRoot, true); UIFx.ModalIn(confirmRoot != null ? confirmRoot.transform : null); });
            Wire(confirmNoButton, () => Show(confirmRoot, false));
            Wire(confirmYesButton, EndRun);
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
            // Terminal presentation subscribes ONLY to RunFinishedEvent. It is fired exactly once by
            // RunDirector after RunClosure's first-wins close, so the screen can never disagree with
            // the frozen ledger.
            Bill.Events?.Subscribe<RunFinishedEvent>(OnRunFinished);
            RunState.LevelsGained += OnLevelsGained;
        }

        private void OnDisable()
        {
            Bill.Events?.Unsubscribe<RunFinishedEvent>(OnRunFinished);
            RunState.LevelsGained -= OnLevelsGained;
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
            Show(reviveRoot, false);
            Show(levelUpRoot, false);
            Show(ftueRoot, false);
            if (resumeCountText != null) resumeCountText.gameObject.SetActive(false);

            Show(resultRoot, true);
            BindResult(result);
            UIFx.PopIn(resultRoot.transform.Find("Time"), 0f, 0.7f, 0.35f);
            UIFx.PopIn(resultRoot.transform.Find("ReplayBtn"), 0.45f, 0.8f, 0.3f);
            Time.timeScale = 0f;
        }

        private void BindResult(RunClosure.Result result)
        {
            if (resultRoot == null) return;
            var s = result.Summary;
            bool died = s.Outcome == RunOutcome.Died;
            string clock = HudController.FormatClock(Mathf.FloorToInt(s.Duration));

            SetText("Banner", died ? "THE HORDE GOT YOU" : "YOU WALKED AWAY");
            // M8: the result counts up instead of appearing, so the run's numbers land one by one.
            var timeLabel = resultRoot.transform.Find("Time/Label")?.GetComponent<TMP_Text>();
            UIFx.CountUp(timeLabel, Mathf.FloorToInt(s.Duration), 0.7f, v => HudController.FormatClock((int)v));
            int best = Mathf.FloorToInt(PlayerProfile.BestSurvivalSeconds);
            SetText("RecordPill/L", result.NewSurvivalRecord ? "NEW BEST!" : $"Best  {HudController.FormatClock(best)}");
            UIFx.CountUp(resultRoot.transform.Find("Stats/Stat0/Value/Label")?.GetComponent<TMP_Text>(), s.Kills, 0.6f, v => $"{v:N0}", 0.25f);
            SetText("Stats/Stat1/Value/Label", $"{s.Level}");
            SetText("Stats/Stat2/Value/Label", $"{s.PeakThreatTier}");
            SetText("PayoutCard/Row0L", "Coins collected");
            SetText("PayoutCard/Row0V", $"+{s.Coin:N0}");
            SetText("PayoutCard/Row1L", died
                ? $"You keep {RunClosure.DiedCoinFraction:P0} on death"
                : "Walked away: coins lost");
            // Walking away keeps nothing: show what was lost, not the (zero) amount kept.
            long lostCoin = s.Coin - result.BankedCoin;
            SetText("PayoutCard/Row1V", died ? $"+{result.BankedCoin:N0}" : lostCoin > 0 ? $"-{lostCoin:N0}" : "0");
            var lost = resultRoot.transform.Find("PayoutCard/Row1V")?.GetComponent<TMP_Text>();
            if (lost != null) lost.color = died || s.Coin == 0 ? UITheme.M8Yellow : UITheme.M8Red;
            SetText("PayoutCard/Row2L", "Gems (always kept)");
            SetText("PayoutCard/Row2V", $"+{s.Gem}");
            SetText("PayoutCard/TotalL", "Banked");
            UIFx.CountUp(resultRoot.transform.Find("PayoutCard/TotalV")?.GetComponent<TMP_Text>(), result.BankedCoin, 0.8f, v => $"{v:N0}", 0.5f);
            SetShown("PayoutCard/KcRow", false);
            BindResultBuild();

            // No live pass-XP value exists for a run; an invented number is worse than nothing.
            SetShown("PassXpBar", false);
            SetShown("PassXpLabel", false);
        }

        // Build recap on the result screen, same tiles as the level-up strip plus a rank badge.
        private void BindResultBuild()
        {
            var items = resultRoot.transform.Find("Build/Items");
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (items == null) return;
            int k = 0;
            if (skills != null)
                for (int pass = 0; pass < 2; pass++)
                    foreach (var kv in skills.Ranks)
                    {
                        var def = ZombieWar.Skills.SkillCatalogDefs.ById(kv.Key);
                        if (def == null || def.IsEvolution || kv.Value <= 0) continue;
                        if ((pass == 0) != (def.layer == ZombieWar.Skills.SkillLayer.Autonomous)) continue;
                        if (k >= items.childCount) break;
                        var it = items.GetChild(k++);
                        it.gameObject.SetActive(true);
                        BindIcon(it, def, 1f);
                        var rank = it.Find("Rank/Label")?.GetComponent<TMP_Text>();
                        if (rank != null) rank.text = skills.IsEvolved(def.id) ? "EVO" : kv.Value.ToString();
                    }
            for (; k < items.childCount; k++) items.GetChild(k).gameObject.SetActive(false);
            SetShown("Build", skills != null && skills.Ranks.Count > 0);
        }

        private void SetText(string path, string value)
        {
            var t = resultRoot.transform.Find(path)?.GetComponent<TMP_Text>();
            if (t != null) t.text = value;
        }

        private void SetShown(string path, bool shown)
        {
            var t = resultRoot.transform.Find(path);
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

        private bool TerminalOverlayActive => resultRoot != null && resultRoot.activeSelf;

        // ------------------------------------------------------------ pause
        public void ShowPause()
        {
            if (TerminalOverlayActive) return;
            Time.timeScale = 0f;
            Show(pauseRoot, true);
            UIFx.ModalIn(pauseRoot != null ? pauseRoot.transform : null);
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
                    UIFx.PopIn(resumeCountText.transform, 0f, 1.4f, 0.25f);
                    yield return new WaitForSecondsRealtime(0.6f);
                }
                resumeCountText.gameObject.SetActive(false);
            }
            Time.timeScale = 1f;
            TryShowLevelUp();   // level-ups earned before/during the pause were held back
        }

        // "End run" from the pause menu. The run closes as a walk-away and the result screen shows
        // what was forfeited; Home on that screen is what actually leaves the world.
        private void EndRun()
        {
            Show(confirmRoot, false);
            Show(pauseRoot, false);
            if (RunState.Current == null || RunState.Current.IsOver)
            {
                Time.timeScale = 1f;
                GameFlow.ReturnToMenu();
                return;
            }
            Bill.Events?.Fire(new RunAbandonRequestedEvent());
        }

        private void OpenSettings()
        {
            Show(settingsRoot, true);
            UIFx.ModalIn(settingsRoot != null ? settingsRoot.transform : null);
        }

        // ------------------------------------------------------------ revive (test hook)
        public void ShowRevive()
        {
            if (TerminalOverlayActive) return;
            Time.timeScale = 0f;
            Show(reviveRoot, true);
            UIFx.ModalIn(reviveRoot != null ? reviveRoot.transform : null);
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
        // RunState.LevelsGained -> queue -> pause + 1-of-3 card offer -> SkillRuntime.Take -> resume.
        // Level-ups earned while paused or while another overlay is up stay queued and present as
        // soon as the screen is free again, so no earned choice is ever dropped. Binding reuses the
        // prefab's Perk{i}/Name and Perk{i}/Desc paths, so no UI prefab edit is needed.
        private int _pendingLevelUps;
        private System.Collections.Generic.List<ZombieWar.Skills.SkillDef> _skillOffer;

        [Tooltip("M8: card id -> icon. Cards without art show a two-letter badge in their layer colour.")]
        [SerializeField] private ZombieWar.UI.SkillIconSet skillIcons;
        private int _shownAutoPickSeconds = -1;
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
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (run == null || run.IsOver || skills == null) { _pendingLevelUps = 0; return; }

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
                BindOfferText($"Perk{i}/Name", CardTitle(def, nextRank));
                BindOfferText($"Perk{i}/Desc", ZombieWar.Skills.SkillDescriptions.Describe(def, nextRank));
                BindCardVisuals(i, def, nextRank);
            }
            ShowOfferButtons(_skillOffer.Count);
            var sub = levelUpRoot.transform.Find("Sub/Label")?.GetComponent<TMP_Text>();
            if (sub != null) sub.text = $"Level {run.Level} · choose one";
            BindBuildStrip(skills);
            _shownAutoPickSeconds = -1;

            _levelUpShownAtRealtime = Time.realtimeSinceStartup;
            Time.timeScale = 0f;
            Show(levelUpRoot, true);
            // M8: the world dims, the title pops, the cards deal in one after another.
            UIFeedback.LevelUp();
            var lu = levelUpRoot.transform;
            UIFx.FadeIn(lu.Find("Dim")?.GetComponent<Graphic>(), 0.2f);
            UIFx.PopIn(lu.Find("Title"), 0f, 0.6f, 0.35f);
            for (int i = 0; i < _skillOffer.Count; i++) UIFx.PopIn(lu.Find($"Perk{i}"), 0.08f + 0.07f * i, 0.8f, 0.3f);
        }

        /// The <=30 s unscaled pause. On expiry it auto-picks a VALID card — never a broken or
        /// ineligible one — so a player who walks away never loses an earned choice or gets stuck on
        /// a frozen screen.
        private void Update()
        {
            if (levelUpRoot == null || !levelUpRoot.activeSelf) return;
            if (_skillOffer == null || _skillOffer.Count == 0) return;
            float waited = Time.realtimeSinceStartup - _levelUpShownAtRealtime;
            int left = Mathf.CeilToInt(LevelUpTimeoutSeconds - waited);
            if (left != _shownAutoPickSeconds)
            {
                _shownAutoPickSeconds = left;
                var hint = levelUpRoot.transform.Find("Hint")?.GetComponent<TMP_Text>();
                if (hint != null) hint.text = $"Auto-picks in {Mathf.Max(0, left)} s";
            }
            if (waited < LevelUpTimeoutSeconds) return;

            var skills = ZombieWar.Skills.SkillRuntime.Active;
            var auto = ZombieWar.Skills.SkillOfferBuilder.AutoPick(_skillOffer, skills);
            int slot = auto == null ? 0 : _skillOffer.IndexOf(auto);
            PickPerk(Mathf.Max(0, slot));
        }

        // A near-exhausted pool can offer fewer than three cards. The spare buttons would otherwise
        // keep the prefab's placeholder text and burn the level-up on a card that does nothing.
        private void ShowOfferButtons(int count)
        {
            if (perkButtons == null) return;
            for (int i = 0; i < perkButtons.Length; i++)
                if (perkButtons[i] != null) perkButtons[i].gameObject.SetActive(i < count);
        }

        private void BindOfferText(string path, string value)
        {
            var t = levelUpRoot.transform.Find(path)?.GetComponent<TMP_Text>();
            if (t == null) return;
            // The generated text is longer than the placeholder the card was laid out for
            // ("Emergency Detonation NEW" ran past the card edge). Shrink to fit instead of
            // overflowing; the authored size stays the ceiling, so short names look as designed.
            if (!t.enableAutoSizing)
            {
                t.fontSizeMax = t.fontSize;
                t.fontSizeMin = Mathf.Max(8f, t.fontSize * 0.6f);
                t.enableAutoSizing = true;
            }
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.text = value;
        }

        /// <summary>Test hook: shows the overlay with a fresh offer without needing earned XP.</summary>
        public void ShowLevelUp()
        {
            if (TerminalOverlayActive) return;
            _pendingLevelUps = Mathf.Max(_pendingLevelUps, 1);
            TryShowLevelUp();
        }

        static readonly Color CardBg = UITheme.M8Card;                       // M8 mockup card
        static readonly Color EvolutionBg = new(0.227f, 0.2f, 0.122f);        // mockup #3A331F

        // Border in the layer colour, icon tile tinted from it, gold background for an evolution,
        // and rank pips: the card reads (what kind, how far along) before the text does.
        private void BindCardVisuals(int slot, ZombieWar.Skills.SkillDef def, int nextRank)
        {
            var card = levelUpRoot.transform.Find($"Perk{slot}");
            if (card == null) return;
            var color = ZombieWar.Skills.SkillDescriptions.LayerColor(def);

            var border = card.Find("Border")?.GetComponent<Image>();
            if (border != null) border.color = color;
            var bg = card.Find("Bg")?.GetComponent<Image>();
            if (bg != null) bg.color = def.IsEvolution ? EvolutionBg : CardBg;
            var tile = card.Find("Icon")?.GetComponent<Image>();
            if (tile != null) tile.color = Color.Lerp(CardBg, color, 0.35f);

            BindIcon(card.Find("Icon"), def, 1f);

            var pips = card.Find("Pips");
            if (pips != null)
            {
                bool showPips = !def.IsEvolution && def.maxRank > 1;
                pips.gameObject.SetActive(showPips);
                for (int k = 0; k < pips.childCount; k++)
                {
                    var pip = pips.GetChild(k);
                    bool used = k < def.maxRank;
                    pip.gameObject.SetActive(used);
                    var img = pip.GetComponent<Image>();
                    if (img != null) img.color = k < nextRank ? color : new Color(0.06f, 0.07f, 0.1f, 0.8f);
                }
            }
        }

        /// Icon art when the set has it, otherwise the two-letter badge in the layer colour.
        private void BindIcon(Transform holder, ZombieWar.Skills.SkillDef def, float alpha)
        {
            if (holder == null) return;
            var sprite = skillIcons != null ? skillIcons.For(def.id) : null;
            var art = holder.Find("Art")?.GetComponent<Image>();
            if (art != null) { art.enabled = sprite != null; if (sprite != null) art.sprite = sprite; }
            var badge = holder.Find("Badge")?.GetComponent<TMP_Text>();
            if (badge != null)
            {
                badge.enabled = sprite == null;
                badge.text = ZombieWar.UI.SkillIconSet.Abbreviation(def.displayName);
                var c = ZombieWar.Skills.SkillDescriptions.LayerColor(def); c.a = alpha;
                badge.color = Color.Lerp(c, Color.white, 0.35f);
            }
        }

        // The build so far under the cards: powers first, then everything else, one tile each.
        private void BindBuildStrip(ZombieWar.Skills.SkillRuntime skills)
        {
            var items = levelUpRoot.transform.Find("Build/Items");
            if (items == null || skills == null) return;
            int k = 0;
            for (int pass = 0; pass < 2; pass++)
                foreach (var kv in skills.Ranks)
                {
                    var def = ZombieWar.Skills.SkillCatalogDefs.ById(kv.Key);
                    if (def == null || def.IsEvolution || kv.Value <= 0) continue;
                    bool power = def.layer == ZombieWar.Skills.SkillLayer.Autonomous;
                    if ((pass == 0) != power) continue;
                    if (k >= items.childCount) break;
                    var it = items.GetChild(k++);
                    it.gameObject.SetActive(true);
                    BindIcon(it, def, 1f);
                }
            for (; k < items.childCount; k++) items.GetChild(k).gameObject.SetActive(false);
            var build = levelUpRoot.transform.Find("Build");
            if (build != null) build.gameObject.SetActive(skills.Ranks.Count > 0);
        }

        /// Card title: the name in its layer colour plus a small NEW / Lv N / EVOLUTION tag. Rich text
        /// only (no glyphs the game font may lack), so the owner-authored card prefab is untouched.
        public static string CardTitle(ZombieWar.Skills.SkillDef def, int rank)
        {
            string hex = ColorUtility.ToHtmlStringRGB(ZombieWar.Skills.SkillDescriptions.LayerColor(def));
            string tag = def.IsEvolution ? "EVOLUTION" : rank <= 1 ? "NEW" : $"Lv {rank}";
            return $"<color=#{hex}>{def.displayName}</color> <size=70%>{tag}</size>";
        }

        private void PickPerk(int slot)
        {
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (skills != null && _skillOffer != null && slot >= 0 && slot < _skillOffer.Count)
            {
                var taken = _skillOffer[slot];
                skills.Take(taken.id);
                UIFeedback.Confirm();
                // M8: a stat card has no power of its own to watch; a pulse in its layer colour marks the pick.
                if (taken.layer == ZombieWar.Skills.SkillLayer.Stat && PlayerMovement.Instance != null)
                    ZombieWar.Skills.SkillFxDirector.Instance?.Pulse(PlayerMovement.Instance.transform.position, 1.8f,
                        ZombieWar.Skills.SkillDescriptions.LayerColor(taken), 0.45f, 0.25f);
                UIFeedback.Haptic(UIFeedback.Buzz.Tick);
                MissionTracker.ReportCardChosen();
                if (taken.IsEvolution) ZombieWar.Skills.SkillCombatDriver.Instance?.OnEvolutionTaken();

                // Max Health is the one card that must act at pick time; the Health component owns
                // the number.
                float bonus = skills.ConsumeMaxHealthBonus();
                if (bonus > 0f) PlayerMovement.Instance?.GetComponent<Health>()?.IncreaseMax(1f + bonus);
            }

            _skillOffer = null;
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
            PlayerPrefs.Save();   // WebGL and a killed app both lose unsaved prefs
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
