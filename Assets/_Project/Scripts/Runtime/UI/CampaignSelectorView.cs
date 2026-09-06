using System.Collections.Generic;
using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// The compact campaign selector that sits directly above PLAY:
    ///
    /// <code>
    ///              STAGE 2
    ///     ‹  ● ━ ● ─ ○ ─ • ─ •  ›
    ///               PLAY
    /// </code>
    ///
    /// Presentation only. Every rule it renders - which stage is reachable, where the arrows may go,
    /// what the fallback is - comes from <see cref="CampaignSelection"/> and
    /// <see cref="CampaignCatalog.Evaluate"/>, so the view and the gate cannot drift apart.
    ///
    /// Dots are cloned from an authored template rather than drawn in code: the count is whatever the
    /// catalog holds, so it cannot be authored as a fixed row, but the look still comes from the
    /// prefab instead of hardcoded colours in a runtime builder.
    /// </summary>
    public sealed class CampaignSelectorView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CampaignCatalog catalog;
        [Tooltip("Weapons used to compute Combat Power for the advisory warning. There is no shared " +
                 "runtime arsenal asset in this project (LoadoutScreen builds its own from authored " +
                 "cards), so it is referenced here. Leave empty to suppress the warning rather than " +
                 "show one computed from a power of zero.")]
        [SerializeField] private WeaponData[] arsenal = System.Array.Empty<WeaponData>();

        [Header("Refs")]
        [SerializeField] private TMP_Text stageLabel;
        [SerializeField] private TMP_Text warningLabel;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [Tooltip("Parent the dot row is built under. Cleared and refilled from the catalog.")]
        [SerializeField] private RectTransform dotRow;
        [Tooltip("Authored single dot (with its connector). Cloned once per catalog entry and kept " +
                 "inactive as the template.")]
        [SerializeField] private CampaignDotView dotTemplate;

        [Header("State colours")]
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.30f);
        [SerializeField] private Color completedColor = new Color(0.42f, 0.86f, 0.55f);
        [SerializeField] private Color availableColor = new Color(0.85f, 0.90f, 0.95f);
        [SerializeField] private Color lockedColor = new Color(0.34f, 0.37f, 0.42f, 0.65f);

        private readonly List<CampaignDotView> _dots = new();
        private int _selected = CampaignSelection.NoSelection;
        private bool _subscribed;

        /// <summary>Currently selected level, or null when the catalog has nothing playable.</summary>
        public CampaignLevel SelectedLevel =>
            catalog != null && _selected >= 0 ? catalog.Get(_selected) : null;

        private void OnEnable()
        {
            if (!_subscribed)
            {
                PlayerProfile.CampaignChanged += Refresh;
                _subscribed = true;
            }

            // Returning to the Hub must reflect a clear that happened during the run, so the initial
            // index is resolved here rather than once at Awake.
            _selected = CampaignSelection.ResolveInitialIndex(
                catalog, PlayerProfile.LastSelectedLevelId, CurrentPower());

            Build();
            Refresh();
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            PlayerProfile.CampaignChanged -= Refresh;
            _subscribed = false;
        }

        private void Awake()
        {
            if (leftButton != null) leftButton.onClick.AddListener(() => Step(-1));
            if (rightButton != null) rightButton.onClick.AddListener(() => Step(1));
            if (dotTemplate != null) dotTemplate.gameObject.SetActive(false);
        }

        /// <summary>True only when Combat Power can actually be computed. Without an arsenal the
        /// honest answer is "unknown", and a warning derived from a fabricated zero would be worse
        /// than no warning at all.</summary>
        /// A5: the serialized `arsenal` lives on a UI prefab this run may not edit, so when it is
        /// empty the catalog supplies the roster instead. Playable weapons only — an unauthored body
        /// has no stats worth scoring.
        private System.Collections.Generic.IReadOnlyList<WeaponData> ResolvedArsenal
        {
            get
            {
                if (arsenal != null && arsenal.Length > 0) return arsenal;
                var catalog = WeaponCatalog.Active;
                if (catalog == null) return System.Array.Empty<WeaponData>();
                var list = new System.Collections.Generic.List<WeaponData>();
                foreach (var d in catalog.AllData()) if (d != null && d.IsPlayable) list.Add(d);
                return list;
            }
        }

        private bool CanAssessPower => ResolvedArsenal.Count > 0;

        private int CurrentPower() => CanAssessPower ? CombatPower.Current(ResolvedArsenal) : 0;

        // One dot per catalog entry, never a fixed five.
        private void Build()
        {
            if (dotRow == null || dotTemplate == null) return;

            int want = catalog != null ? catalog.Count : 0;
            while (_dots.Count < want)
            {
                var dot = Instantiate(dotTemplate, dotRow);
                dot.name = $"Dot_{_dots.Count:00}";
                _dots.Add(dot);
            }
            for (int i = 0; i < _dots.Count; i++) _dots[i].gameObject.SetActive(i < want);
        }

        /// <summary>Re-reads progression and repaints. Cheap enough to call on every campaign change.</summary>
        private void Refresh()
        {
            if (catalog == null) return;

            int power = CurrentPower();
            // A clear during the run can move what is valid under a held selection.
            _selected = CampaignSelection.Revalidate(catalog, _selected, power);

            var level = SelectedLevel;
            // Same rule as the arrows and the dot row below: with a one-stage catalog the name of
            // the only stage says nothing PLAY does not already say, and at the 1080x1920 reference
            // resolution the label band sits inside the reward-card row, overlapping DAILY REWARD.
            // A second authored stage brings the label back with no code change.
            if (stageLabel != null)
            {
                stageLabel.gameObject.SetActive(catalog.Count > 1);
                stageLabel.text = level != null ? level.displayName.ToUpperInvariant() : "NO STAGE";
            }

            for (int i = 0; i < _dots.Count && i < catalog.Count; i++)
            {
                var state = CampaignSelection.StateFor(catalog, i, _selected, power);
                _dots[i].Apply(state, ColorFor(state), showConnector: i > 0);
            }

            // Arrows read as disabled at the boundaries instead of silently doing nothing.
            //
            // M4 closeout: with a ONE-STAGE catalog there is nowhere to step at all, and a permanently
            // greyed pair of arrows is just furniture that promises content the game no longer has.
            // Hide them entirely in that case rather than leaving two dead buttons on the card. The
            // moment a second stage is authored they come back with no code change - this reads the
            // catalog, it does not hardcode "one stage".
            bool canNavigate = catalog.Count > 1;
            if (leftButton != null)
            {
                leftButton.gameObject.SetActive(canNavigate);
                leftButton.interactable = canNavigate && CampaignSelection.CanStep(catalog, _selected, -1, power);
            }
            if (rightButton != null)
            {
                rightButton.gameObject.SetActive(canNavigate);
                rightButton.interactable = canNavigate && CampaignSelection.CanStep(catalog, _selected, 1, power);
            }

            // Một chấm trên hàng chấm cũng không nói lên điều gì — nó chỉ nói "có đúng một chặng".
            if (dotRow != null) dotRow.gameObject.SetActive(canNavigate);

            if (warningLabel != null)
            {
                // Power is advice, so this is the only place it shows up - it never disables PLAY.
                var gate = level != null ? catalog.Evaluate(_selected, power) : LevelGate.Locked("");
                bool warn = level != null && gate.HasWarning && CanAssessPower;
                warningLabel.gameObject.SetActive(warn);
                if (warn) warningLabel.text = gate.Reason;
            }
        }

        private Color ColorFor(CampaignSelection.DotState state) => state switch
        {
            CampaignSelection.DotState.Selected => selectedColor,
            CampaignSelection.DotState.Completed => completedColor,
            CampaignSelection.DotState.Available => availableColor,
            _ => lockedColor,
        };

        private void Step(int direction)
        {
            int next = CampaignSelection.Step(catalog, _selected, direction, CurrentPower());
            if (next == _selected) return;   // boundary or locked: the press does nothing

            _selected = next;
            Refresh();
        }

        /// <summary>
        /// Commits the selection and launches. Returns false when there is nothing playable, so the
        /// caller can leave the old behaviour intact rather than starting an undefined run.
        ///
        /// A locked stage can never reach here: it cannot become <see cref="_selected"/> in the first
        /// place, and the gate is re-checked at launch in case progression changed under the screen.
        /// </summary>
        public bool LaunchSelected()
        {
            var level = SelectedLevel;
            if (level == null) return false;
            if (!catalog.Evaluate(_selected, CurrentPower()).CanPlay) return false;

            GameFlow.SelectLevel(level);
            GameFlow.StartGameplay();
            return true;
        }
    }
}
