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

        GameObject _chestRoot;
        int _pendingChests, _chestsOpened, _chestShownLeft = -1;
        float _chestShownAt;
        ZombieWar.Skills.SkillRuntime.ChestReward _chest;

        GameObject ChestRoot
        {
            get
            {
                if (_chestRoot == null && levelUpRoot != null)
                {
                    var t = levelUpRoot.transform.parent != null ? levelUpRoot.transform.parent.Find("ChestOverlay") : null;
                    if (t != null)
                    {
                        _chestRoot = t.gameObject;
                        var claim = t.Find("Claim")?.GetComponent<Button>();
                        if (claim != null) claim.onClick.AddListener(ClaimChest);
                    }
                }
                return _chestRoot;
            }
        }

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
            if (reviveRoot != null && reviveRoot.activeSelf) return;
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
            ChestRoot.transform.Find("Card/FtueEvo")?.gameObject.SetActive(_ftueChest);
            if (_ftueChest) SetText(ChestRoot.transform, "Hint", "No timer on your first chest");
            Time.timeScale = 0f;
            Show(ChestRoot, true);

            var t = ChestRoot.transform;
            UIFeedback.LevelUp();
            UIFx.FadeIn(t.Find("Dim")?.GetComponent<Graphic>(), 0.2f);
            UIFx.PopIn(t.Find("Chest"), 0f, 0.5f, 0.4f);
            UIFx.PopIn(t.Find("Card"), 0.25f, 0.8f, 0.35f);
        }

        void BindChest(ZombieWar.Skills.SkillRuntime.ChestReward reward, ZombieWar.Skills.SkillRuntime skills)
        {
            var t = ChestRoot.transform;
            var def = reward.card;
            bool evo = reward.kind == ZombieWar.Skills.SkillRuntime.ChestKind.Evolution;
            bool bonus = reward.kind == ZombieWar.Skills.SkillRuntime.ChestKind.Bonus;
            var colour = evo ? new Color(1f, 0.8f, 0.2f) : ZombieWar.Skills.SkillDescriptions.LayerColor(def);

            SetText(t, "Card/Tag/Label", evo ? "EVOLUTION" : bonus ? "BONUS" : "RANK UP");
            var tagBg = t.Find("Card/Tag")?.GetComponent<Image>();
            if (tagBg != null) tagBg.color = colour;
            var frame = t.Find("Card/Frame")?.GetComponent<Image>();
            if (frame != null) frame.color = colour;
            BindTile(t.Find("Card/Icon"), def, colour);
            SetText(t, "Card/Name", evo || bonus ? def.displayName : $"{def.displayName}  Lv {reward.rank}");
            SetText(t, "Card/Desc", ZombieWar.Skills.SkillDescriptions.Describe(def, Mathf.Max(1, reward.rank)));

            var recipe = t.Find("Card/Recipe");
            if (recipe != null) recipe.gameObject.SetActive(evo);
            if (evo)
            {
                var a = ZombieWar.Skills.SkillCatalogDefs.ById(def.evolvesFrom);
                var b = ZombieWar.Skills.SkillCatalogDefs.ById(def.partner);
                BindTile(recipe.Find("A"), a, ZombieWar.Skills.SkillDescriptions.LayerColor(a));
                BindTile(recipe.Find("B"), b, ZombieWar.Skills.SkillDescriptions.LayerColor(b));
                BindTile(recipe.Find("Evo"), def, colour);
                SetText(t, "Card/Req", $"{a.displayName.ToUpperInvariant()} RANK 5 + {b.displayName.ToUpperInvariant()}");
            }
            else SetText(t, "Card/Req", bonus ? "Your build is full and maxed: a bonus instead"
                                              : "Not ready to evolve yet: +1 rank on a card you own");
        }

        void BindTile(Transform tile, ZombieWar.Skills.SkillDef def, Color colour)
        {
            if (tile == null || def == null) return;
            var bg = tile.GetComponent<Image>();
            if (bg != null) bg.color = Color.Lerp(CardBg, colour, 0.35f);
            var sprite = skillIcons != null ? skillIcons.For(def.id) : null;
            var art = tile.Find("Art")?.GetComponent<Image>();
            if (art != null) { art.enabled = sprite != null; art.sprite = sprite; }
            var badge = tile.Find("Badge")?.GetComponent<TMP_Text>();
            if (badge != null) { badge.enabled = sprite == null; badge.text = ZombieWar.UI.SkillIconSet.Abbreviation(def.displayName); }
        }

        static void SetText(Transform root, string path, string value)
        {
            var t = root.Find(path)?.GetComponent<TMP_Text>();
            if (t != null) t.text = value;
        }

        bool _ftueChest;

        void TickChest()
        {
            if (!ChestOpen || _ftueChest) return;
            float waited = Time.realtimeSinceStartup - _chestShownAt;
            int left = Mathf.CeilToInt(ChestTimeoutSeconds - waited);
            if (left != _chestShownLeft)
            {
                _chestShownLeft = left;
                SetText(ChestRoot.transform, "Hint", $"Auto-claims in {Mathf.Max(0, left)} s");
            }
            if (waited >= ChestTimeoutSeconds) ClaimChest();
        }

        void ClaimChest()
        {
            if (!ChestOpen) return;
            UIFeedback.Confirm();
            UIFeedback.Haptic(UIFeedback.Buzz.Tick);
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (_chest.card != null && _chest.kind == ZombieWar.Skills.SkillRuntime.ChestKind.Evolution)
                ZombieWar.Skills.SkillCombatDriver.Instance?.OnEvolutionTaken();
            if (skills != null)
            {
                float bonus = skills.ConsumeMaxHealthBonus();   // a chest can rank up Max Health
                if (bonus > 0f) PlayerMovement.Instance?.GetComponent<Health>()?.IncreaseMax(1f + bonus);
            }
            if (_ftueChest) { _ftueChest = false; Ftue.Complete(Ftue.Chest); }
            ChestRoot.transform.Find("Card/FtueEvo")?.gameObject.SetActive(false);
            Show(ChestRoot, false);
            Time.timeScale = 1f;
            TryShowChest();
            TryShowLevelUp();
        }
    }
}
