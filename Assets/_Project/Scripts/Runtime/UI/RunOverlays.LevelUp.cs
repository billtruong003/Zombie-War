using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar
{
    public partial class RunOverlays
    {
        // ------------------------------------------------------------ level-up
        // RunState.LevelsGained -> queue -> pause + 1-of-3 card offer -> SkillRuntime.Take -> resume.
        // Level-ups earned while paused or while another overlay is up stay queued and present as
        // soon as the screen is free again, so no earned choice is ever dropped. Widgets come from the
        // wired LevelUpView (RunOverlays.Views.cs).
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
            if (ChestOpen) return;   // one choice screen at a time; the chest hands back when claimed
            if (pauseRoot != null && pauseRoot.activeSelf) return;
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
                var card = Card(i);
                if (card == null) continue;
                BindOfferText(card.title, CardTitle(def, nextRank));
                BindOfferText(card.desc, ZombieWar.Skills.SkillDescriptions.Describe(def, nextRank));
                BindCardVisuals(card, def, nextRank);
            }
            ShowOfferButtons(_skillOffer.Count);
            // FTUE v2: the first level-up ever has no timer and points at one power card.
            _ftueCard = !Ftue.Done(Ftue.Card);
            var sub = levelUp.sub;
            if (sub != null) sub.text = _ftueCard ? $"Level {run.Level} · choose one · no timer" : $"Level {run.Level} · choose one";
            if (_ftueCard) ZombieWar.Audio.FtueVoice.CardOffered(); else ZombieWar.Audio.RadioDirector.CardOffered();
            // v2 coach widgets in the prefab stay hidden; the radio card marks the suggested card. The
            // auto-pick countdown (Hint) only shows when there is a timer.
            foreach (var w in levelUp.legacyWidgets) if (w != null) w.SetActive(false);
            if (levelUp.hint != null) levelUp.hint.gameObject.SetActive(!_ftueCard);
            if (_ftueCard) FtueV3.FirstCard(Card(SuggestedCard(_skillOffer))?.root);
            BindBuildStrip(skills);
            _shownAutoPickSeconds = -1;

            _levelUpShownAtRealtime = Time.unscaledTime;   // not realtime: a player back from the background must not find a card picked (07/10)
            Time.timeScale = 0f;
            Show(levelUpRoot, true);
            // M8: the world dims, the title pops, the cards deal in one after another.
            UIFeedback.LevelUp();
            UIFx.FadeIn(levelUp.dim, 0.2f);
            UIFx.PopIn(levelUp.title, 0f, 0.6f, 0.35f);
            for (int i = 0; i < _skillOffer.Count; i++) UIFx.PopIn(Card(i)?.root, 0.08f + 0.07f * i, 0.8f, 0.3f);
        }

        /// The <=30 s unscaled pause. On expiry it auto-picks a VALID card — never a broken or
        /// ineligible one — so a player who walks away never loses an earned choice or gets stuck on
        /// a frozen screen.
        private void Update()
        {
            HandleBack();
            // The level-up sheet shows the build itself; the HUD skill bar underneath overlapped it.
            bool picking = levelUpRoot != null && levelUpRoot.activeSelf;
            if (skillBar != null && skillBar.activeSelf == picking) skillBar.SetActive(!picking);

            TickChest();
            if (levelUpRoot == null || !levelUpRoot.activeSelf) return;
            if (_skillOffer == null || _skillOffer.Count == 0) return;
            if (_ftueCard) return;   // the first level-up ever waits for the player
            float waited = Time.unscaledTime - _levelUpShownAtRealtime;
            int left = Mathf.CeilToInt(LevelUpTimeoutSeconds - waited);
            if (left != _shownAutoPickSeconds)
            {
                _shownAutoPickSeconds = left;
                var hint = levelUp.hint;
                if (hint != null) hint.text = $"Auto-picks in {Mathf.Max(0, left)} s";
            }
            if (waited < LevelUpTimeoutSeconds) return;

            var skills = ZombieWar.Skills.SkillRuntime.Active;
            var auto = ZombieWar.Skills.SkillOfferBuilder.AutoPick(_skillOffer, skills);
            int slot = auto == null ? 0 : _skillOffer.IndexOf(auto);
            ZombieWar.Audio.RadioDirector.CardAutoPicked();
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

        private OfferCardView Card(int i) => levelUp.cards != null && i >= 0 && i < levelUp.cards.Length ? levelUp.cards[i] : null;

        private static void BindOfferText(TMP_Text t, string value)
        {
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
        private void OnCardOfferRequested(CardOfferRequestedEvent _) => ShowLevelUp();

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
        private void BindCardVisuals(OfferCardView card, ZombieWar.Skills.SkillDef def, int nextRank)
        {
            var color = ZombieWar.Skills.SkillDescriptions.LayerColor(def);
            if (card.border != null) card.border.color = color;
            if (card.bg != null) card.bg.color = def.IsEvolution ? EvolutionBg : CardBg;
            if (card.icon?.frame != null) card.icon.frame.color = Color.Lerp(CardBg, color, 0.35f);

            BindIcon(card.icon, def, 1f);

            if (card.pipsRoot != null)
            {
                bool showPips = !def.IsEvolution && !def.IsOverflow && def.maxRank > 1;
                card.pipsRoot.SetActive(showPips);
                for (int k = 0; k < card.pips.Length; k++)
                {
                    var pip = card.pips[k];
                    if (pip == null) continue;
                    pip.gameObject.SetActive(k < def.maxRank);
                    pip.color = k < nextRank ? color : new Color(0.06f, 0.07f, 0.1f, 0.8f);
                }
            }
        }

        /// Icon art when the set has it, otherwise the two-letter badge in the layer colour.
        private void BindIcon(SkillTileView holder, ZombieWar.Skills.SkillDef def, float alpha)
        {
            if (holder == null) return;
            var sprite = skillIcons != null ? skillIcons.For(def.id) : null;
            var art = holder.art;
            if (art != null) { art.enabled = sprite != null; if (sprite != null) art.sprite = sprite; }
            var badge = holder.badge;
            if (badge != null)
            {
                badge.enabled = sprite == null;
                badge.text = ZombieWar.UI.SkillIconSet.Abbreviation(def.displayName);
                var c = ZombieWar.Skills.SkillDescriptions.LayerColor(def); c.a = alpha;
                badge.color = Color.Lerp(c, Color.white, 0.35f);
            }
        }

        // Owner 2026-09-29: the strip is the build's slots — Item0-5 the 6 skill slots, Item6-9 the 4
        // stat slots — filled in pick order, empty ones shown as open "+" slots.
        private static readonly List<ZombieWar.Skills.SkillDef> SlotScratch = new(8);

        private void BindBuildStrip(ZombieWar.Skills.SkillRuntime skills)
        {
            if (levelUp.build == null || skills == null) return;
            levelUp.build.SetActive(true);

            int usedSkills = BindSlotGroup(skills, ZombieWar.Skills.SkillSlot.Skill, 0, ZombieWar.Skills.SkillCatalogDefs.MaxSkillSlots);
            int usedStats = BindSlotGroup(skills, ZombieWar.Skills.SkillSlot.Stat, ZombieWar.Skills.SkillCatalogDefs.MaxSkillSlots,
                                          ZombieWar.Skills.SkillCatalogDefs.MaxStatSlots);
            var caption = levelUp.buildCaption;
            if (caption != null)
                caption.text = $"SKILLS {usedSkills}/{ZombieWar.Skills.SkillCatalogDefs.MaxSkillSlots}   ·   STATS {usedStats}/{ZombieWar.Skills.SkillCatalogDefs.MaxStatSlots}";
        }

        private int BindSlotGroup(ZombieWar.Skills.SkillRuntime skills, ZombieWar.Skills.SkillSlot slot, int first, int count)
        {
            SlotScratch.Clear();
            foreach (var kv in skills.Ranks)
            {
                var def = ZombieWar.Skills.SkillCatalogDefs.ById(kv.Key);
                if (def != null && kv.Value > 0 && def.Slot == slot) SlotScratch.Add(def);
            }
            for (int k = 0; k < count; k++)
            {
                var it = first + k < levelUp.buildSlots.Length ? levelUp.buildSlots[first + k] : null;
                if (it?.frame == null) continue;
                it.frame.gameObject.SetActive(true);
                var rank = it.rank;
                var frame = it.frame;
                if (k < SlotScratch.Count)
                {
                    var def = SlotScratch[k];
                    var evo = ZombieWar.Skills.SkillCatalogDefs.EvolutionOf(def.id);
                    bool evolved = evo != null && skills.Has(evo.id);
                    BindIcon(it, evolved ? evo : def, 1f);
                    if (frame != null) frame.color = evolved ? EvolutionBg : Color.Lerp(CardBg, ZombieWar.Skills.SkillDescriptions.LayerColor(def), 0.3f);
                    // A word here covers half the icon art: max rank is its number in gold, and an
                    // evolution already reads from its gold frame and its own icon.
                    int r = skills.RankOf(def.id);
                    if (rank != null) rank.text = evolved ? "" : r >= def.maxRank ? $"<color=#FFC93C>{r}</color>" : r.ToString();
                }
                else
                {
                    // An open slot: no art, a quiet "+", a dim frame.
                    var art = it.art;
                    if (art != null) art.enabled = false;
                    var badge = it.badge;
                    if (badge != null) { badge.enabled = true; badge.text = "+"; badge.color = new Color(1f, 1f, 1f, 0.35f); }
                    if (frame != null) frame.color = new Color(CardBg.r, CardBg.g, CardBg.b, 0.45f);
                    if (rank != null) rank.text = "";
                }
            }
            return SlotScratch.Count;
        }

        /// Card title: the name in its layer colour plus a small NEW / Lv N / EVOLUTION tag. Rich text
        /// only (no glyphs the game font may lack), so the owner-authored card prefab is untouched.
        public static string CardTitle(ZombieWar.Skills.SkillDef def, int rank)
        {
            string hex = ColorUtility.ToHtmlStringRGB(ZombieWar.Skills.SkillDescriptions.LayerColor(def));
            string tag = def.IsEvolution ? "EVOLUTION" : def.IsOverflow ? "BONUS" : rank <= 1 ? "NEW" : $"Lv {rank}";
            return $"<color=#{hex}>{def.displayName}</color> <size=70%>{tag}</size>";
        }

        // ------------------------------------------------------------ ftue v2: first card
        private bool _ftueCard;

        /// The card to point at on the first level-up: a power (it does something you can see),
        /// else the first card.
        private static int SuggestedCard(List<ZombieWar.Skills.SkillDef> offer)
        {
            for (int i = 0; i < offer.Count; i++)
                if (offer[i].layer == ZombieWar.Skills.SkillLayer.Autonomous) return i;
            return 0;
        }

        /// A Max Health rank (card or chest) must act at pick time; the Health component owns the number.
        static void ApplyPendingMaxHealth(ZombieWar.Skills.SkillRuntime skills)
        {
            float bonus = skills.ConsumeMaxHealthBonus();
            if (bonus <= 0f) return;
            var player = PlayerMovement.Instance;
            if (player != null && player.TryGetComponent(out Health health)) health.IncreaseMax(1f + bonus);
        }

        private void PickPerk(int slot)
        {
            if (_ftueCard) { _ftueCard = false; Ftue.Complete(Ftue.Card); }
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (skills != null && _skillOffer != null && slot >= 0 && slot < _skillOffer.Count)
            {
                var taken = _skillOffer[slot];
                skills.Take(taken.id);
                UIFeedback.Confirm();
                // M8: a stat card has no power of its own to watch; a small Epic Toon nova in its layer colour marks the pick.
                if (taken.layer == ZombieWar.Skills.SkillLayer.Stat && PlayerMovement.Instance != null)
                    ZombieWar.Skills.SkillArsenal.Instance?.Shockwave(PlayerMovement.Instance.transform.position, 0.3f, 1.8f,
                        ZombieWar.Skills.SkillDescriptions.LayerColor(taken), 0.45f);
                UIFeedback.Haptic(UIFeedback.Buzz.Tick);
                MissionTracker.ReportCardChosen();
                if (taken.IsEvolution) ZombieWar.Skills.SkillCombatDriver.Instance?.OnEvolutionTaken();

                // Max Health is the one card that must act at pick time; the Health component owns
                // the number.
                ApplyPendingMaxHealth(skills);
            }

            _skillOffer = null;
            _pendingLevelUps = Mathf.Max(0, _pendingLevelUps - 1);
            Show(levelUpRoot, false);
            Time.timeScale = 1f;
            TryShowLevelUp();   // more queued level-ups present immediately, one choice each
            TryShowChest();
        }
    }
}
