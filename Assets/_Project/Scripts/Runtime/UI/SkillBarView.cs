using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.Skills;
using ZombieWar.UI;

namespace ZombieWar
{
    /// <summary>
    /// M8 HUD skill bar (owner-approved mockup): the run's autonomous powers as icons with a
    /// cooldown sweep and rank badge, and the passive cards as small pips underneath. The player
    /// could not see their build during play before; now it is on screen the whole run.
    ///
    /// Reads <see cref="SkillRuntime.Active"/> ten times a second. Slots are authored in the prefab
    /// so the layout stays editable; a run with more powers than slots shows the first ones.
    /// </summary>
    public class SkillBarView : MonoBehaviour
    {
        [SerializeField] private SkillSlotView[] slots;
        [SerializeField] private TMP_Text[] passivePips;
        [SerializeField] private SkillIconSet icons;

        static readonly Color EvolutionGold = new(1f, 0.8f, 0.2f);
        const int MaxChips = 4;
        readonly List<SkillDef> _powers = new(8);
        readonly List<SkillDef> _passives = new(8);
        float _nextRefresh;

        void OnEnable() => _nextRefresh = 0f;

        void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            Refresh(SkillRuntime.Active);
        }

        public void Refresh(SkillRuntime run)
        {
            _powers.Clear();
            _passives.Clear();
            if (run != null)
            {
                foreach (var kv in run.Ranks)
                {
                    var def = SkillCatalogDefs.ById(kv.Key);
                    if (def == null || kv.Value <= 0 || def.IsEvolution) continue;
                    (def.layer == SkillLayer.Autonomous ? _powers : _passives).Add(def);
                }
                // Stats first: they are the four fixed slots. Gun cards follow.
                _passives.Sort((a, b) => (a.layer == SkillLayer.Stat ? 0 : 1).CompareTo(b.layer == SkillLayer.Stat ? 0 : 1));
            }

            if (slots != null)
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    if (i >= _powers.Count) { slots[i].ShowEmpty(); continue; }
                    var def = _powers[i];
                    bool evolved = run.IsEvolved(def.id);
                    // An evolved slot shows the evolution (Buzzsaw Halo), not the power it came from.
                    var shown = evolved ? SkillCatalogDefs.EvolutionOf(def.id) ?? def : def;
                    slots[i].Show(icons != null ? icons.For(shown.id) : null, SkillIconSet.Abbreviation(shown.displayName),
                        evolved ? EvolutionGold : SkillDescriptions.LayerColor(def),
                        evolved ? "EVO" : Num(run.RankOf(def.id)),
                        1f - run.ReadinessOf(def.id), evolved);
                }

            if (passivePips != null)
                for (int i = 0; i < passivePips.Length; i++)
                {
                    var pip = passivePips[i];
                    if (pip == null) continue;
                    // The row is as wide as the skill bar: four chips fit. With more cards the fourth
                    // chip says how many more, instead of chips running off the screen (2026-09-30).
                    int visible = _passives.Count > MaxChips ? MaxChips - 1 : _passives.Count;
                    bool more = _passives.Count > MaxChips && i == MaxChips - 1;
                    bool on = i < visible || more;
                    if (pip.transform.parent.gameObject.activeSelf != on) pip.transform.parent.gameObject.SetActive(on);
                    if (!on) continue;
                    if (more)
                    {
                        PipIcon(pip, false);
                        pip.text = Plus(_passives.Count - visible);
                        pip.alignment = TextAlignmentOptions.Center;
                        pip.color = Color.white;
                        continue;
                    }
                    var def = _passives[i];
                    var sprite = icons != null ? icons.For(def.id) : null;
                    var icon = PipIcon(pip, sprite != null);
                    if (icon != null) icon.sprite = sprite;
                    // With art the chip shows the icon and the rank; without, the old abbreviation.
                    pip.text = sprite != null ? Num(run.RankOf(def.id)) : SkillIconSet.Abbreviation(def.displayName) + Num(run.RankOf(def.id));
                    pip.alignment = sprite != null ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Center;
                    pip.color = SkillDescriptions.LayerColor(def);
                }
        }

        readonly Dictionary<TMP_Text, Image> _pipIcons = new();

        /// A square icon on the left of the pip's chip, made once per chip.
        Image PipIcon(TMP_Text pip, bool show)
        {
            if (!_pipIcons.TryGetValue(pip, out var img) || img == null)
            {
                if (!show) return null;
                var chip = (RectTransform)pip.transform.parent;
                var rt = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rt.SetParent(chip, false);
                rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
                float h = chip.rect.height * 0.9f;
                rt.sizeDelta = new Vector2(h, h); rt.anchoredPosition = new Vector2(chip.rect.height * 0.08f, 0f);
                img = rt.GetComponent<Image>(); img.preserveAspect = true; img.raycastTarget = false;
                _pipIcons[pip] = img;
            }
            img.enabled = show;
            return img;
        }

        // Cached labels: this view refreshes ten times a second and ToString/concat made new strings
        // every time (TMP also skips the rebuild when it gets the same string back).
        static readonly string[] Nums = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
        static readonly string[] Pluses = { "+0", "+1", "+2", "+3", "+4", "+5", "+6", "+7", "+8", "+9" };
        static string Num(int n) => n >= 0 && n < Nums.Length ? Nums[n] : n.ToString();
        static string Plus(int n) => n >= 0 && n < Pluses.Length ? Pluses[n] : "+" + n;
    }
}
