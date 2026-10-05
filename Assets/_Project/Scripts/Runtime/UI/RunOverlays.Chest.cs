using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.UI;

namespace ZombieWar
{
    /// <summary>
    /// A7 (owner-approved mockup SK_Chest, 2026-09-29): a chest picked up in a run opens over the
    /// frozen game — an evolution the build is ready for (the only way to get one), else +1 rank on a
    /// card already owned, else a bonus card. One choice screen at a time: a chest waits for a level-up
    /// to close and the other way round. The reward is applied the moment the chest opens, so walking
    /// away (auto-claim) never loses it.
    /// </summary>
    public partial class RunOverlays
    {
        const float ChestTimeoutSeconds = 10f;

        int _pendingChests, _chestsOpened, _chestShownLeft = -1;
        float _chestShownAt;
        ZombieWar.Skills.SkillRuntime.ChestReward _chest;

        GameObject ChestRoot => chest.root;

        bool ChestOpen => ChestRoot != null && ChestRoot.activeSelf;

        void OnChestCollected(Vector3 at)
        {
            _pendingChests++;
            TryShowChest();
        }

        void TryShowChest()
        {
            if (_pendingChests <= 0 || TerminalOverlayActive || ChestRoot == null || ChestOpen) return;
            if (levelUpRoot != null && levelUpRoot.activeSelf) return;
            if (pauseRoot != null && pauseRoot.activeSelf) return;
            var run = RunState.Current;
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (run == null || run.IsOver || skills == null) { _pendingChests = 0; return; }

            _pendingChests--;
            _chest = skills.OpenChest(run.Seed * 31 + ++_chestsOpened);
            BindChest(_chest, skills);
            _chestShownAt = Time.realtimeSinceStartup;
            _chestShownLeft = -1;
            // FTUE v2: the first chest ever has no timer and says how evolutions come about.
            _ftueChest = !Ftue.Done(Ftue.Chest);
            if (chest.ftueEvo != null) chest.ftueEvo.SetActive(false);   // v2 widget; the radio card explains
            if (_ftueChest) SetText(chest.hint, "No timer on your first chest");
            if (_ftueChest) ZombieWar.Audio.FtueVoice.ChestOpened();
            if (_ftueChest) ZombieWar.UI.FtueV3.Chest(chest.claim != null ? (RectTransform)chest.claim.transform : null);
            Time.timeScale = 0f;
            Show(ChestRoot, true);

            UIFeedback.LevelUp();
            UIFx.FadeIn(chest.dim, 0.2f);
            UIFx.PopIn(chest.chest, 0f, 0.5f, 0.4f);
            UIFx.PopIn(chest.card, 0.25f, 0.8f, 0.35f);
            // #37: the reward spins like a slot reel before it lands (not on the first, taught chest).
            if (_reel != null) StopCoroutine(_reel);
            _reel = _ftueChest ? null : StartCoroutine(ReelChest(_chest));
        }

        Coroutine _reel;

        /// <summary>Backlog #37 (slot-machine chest): the icon spins through other cards, slowing, with
        /// a tick per stop, then lands on the real reward with a punch. Unscaled (the run is frozen).
        /// The reward is already applied, so claiming mid-spin only skips the show.</summary>
        System.Collections.IEnumerator ReelChest(ZombieWar.Skills.SkillRuntime.ChestReward reward)
        {
            var all = ZombieWar.Skills.SkillCatalogDefs.All;
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (all == null || all.Count == 0 || reward.card == null) yield break;
            string title = chest.title != null ? chest.title.text : null, desc = chest.desc != null ? chest.desc.text : null;
            SetText(chest.title, "???");
            SetText(chest.desc, "");
            float delay = 0.045f;
            int steps = 14;
            for (int i = 0; i < steps; i++)
            {
                var d = all[Random.Range(0, all.Count)];
                if (d == null || d == reward.card) continue;
                BindTile(chest.icon, d, ZombieWar.Skills.SkillDescriptions.LayerColor(d));
                BillGameCore.Bill.Audio?.PlayPitched("sfx.ui.tap", 0.8f + 0.5f * i / steps, 0.5f);
                yield return new WaitForSecondsRealtime(delay);
                delay *= 1.17f;   // slows to ~0.4 s between the last stops
            }
            if (!ChestOpen) yield break;
            BindChest(reward, skills);
            SetText(chest.title, title);
            SetText(chest.desc, desc);
            if (chest.icon?.frame != null) UIFx.Punch(chest.icon.frame.transform);
            UIFeedback.Haptic(UIFeedback.Buzz.Medium);
            UIFeedback.LevelUp();
            _reel = null;
        }

        void BindChest(ZombieWar.Skills.SkillRuntime.ChestReward reward, ZombieWar.Skills.SkillRuntime skills)
        {
            var def = reward.card;
            bool evo = reward.kind == ZombieWar.Skills.SkillRuntime.ChestKind.Evolution;
            bool bonus = reward.kind == ZombieWar.Skills.SkillRuntime.ChestKind.Bonus;
            var colour = evo ? new Color(1f, 0.8f, 0.2f) : ZombieWar.Skills.SkillDescriptions.LayerColor(def);

            SetText(chest.tag, evo ? "EVOLUTION" : bonus ? "BONUS" : "RANK UP");
            if (chest.tagBg != null) chest.tagBg.color = colour;
            if (chest.frame != null) chest.frame.color = colour;
            BindTile(chest.icon, def, colour);
            SetText(chest.title, evo || bonus ? def.displayName : $"{def.displayName}  Lv {reward.rank}");
            SetText(chest.desc, ZombieWar.Skills.SkillDescriptions.Describe(def, Mathf.Max(1, reward.rank)));

            if (chest.recipe != null) chest.recipe.SetActive(evo);
            if (evo)
            {
                var a = ZombieWar.Skills.SkillCatalogDefs.ById(def.evolvesFrom);
                var b = ZombieWar.Skills.SkillCatalogDefs.ById(def.partner);
                BindTile(chest.recipeA, a, ZombieWar.Skills.SkillDescriptions.LayerColor(a));
                BindTile(chest.recipeB, b, ZombieWar.Skills.SkillDescriptions.LayerColor(b));
                BindTile(chest.recipeEvo, def, colour);
                SetText(chest.req, $"{a.displayName.ToUpperInvariant()} RANK 5 + {b.displayName.ToUpperInvariant()}");
            }
            else SetText(chest.req, bonus ? "Your build is full and maxed: a bonus instead"
                                              : "Not ready to evolve yet: +1 rank on a card you own");
        }

        void BindTile(SkillTileView tile, ZombieWar.Skills.SkillDef def, Color colour)
        {
            if (tile == null || def == null) return;
            if (tile.frame != null) tile.frame.color = Color.Lerp(CardBg, colour, 0.35f);
            var sprite = skillIcons != null ? skillIcons.For(def.id) : null;
            var art = tile.art;
            if (art != null) { art.enabled = sprite != null; art.sprite = sprite; }
            var badge = tile.badge;
            if (badge != null) { badge.enabled = sprite == null; badge.text = ZombieWar.UI.SkillIconSet.Abbreviation(def.displayName); }
        }

        static void SetText(TMP_Text t, string value) { if (t != null) t.text = value; }

        bool _ftueChest;

        void TickChest()
        {
            if (!ChestOpen || _ftueChest) return;
            float waited = Time.realtimeSinceStartup - _chestShownAt;
            int left = Mathf.CeilToInt(ChestTimeoutSeconds - waited);
            if (left != _chestShownLeft)
            {
                _chestShownLeft = left;
                SetText(chest.hint, $"Auto-claims in {Mathf.Max(0, left)} s");
            }
            if (waited >= ChestTimeoutSeconds) ClaimChest();
        }

        void ClaimChest()
        {
            if (!ChestOpen) return;
            if (_reel != null) { StopCoroutine(_reel); _reel = null; }
            UIFeedback.Confirm();
            UIFeedback.Haptic(UIFeedback.Buzz.Tick);
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (_chest.card != null && _chest.kind == ZombieWar.Skills.SkillRuntime.ChestKind.Evolution)
                ZombieWar.Skills.SkillCombatDriver.Instance?.OnEvolutionTaken();
            if (skills != null) ApplyPendingMaxHealth(skills);   // a chest can rank up Max Health
            if (_ftueChest) { _ftueChest = false; Ftue.Complete(Ftue.Chest); }
            if (chest.ftueEvo != null) chest.ftueEvo.SetActive(false);
            Show(ChestRoot, false);
            Time.timeScale = 1f;
            TryShowChest();
            TryShowLevelUp();
        }
    }
}
