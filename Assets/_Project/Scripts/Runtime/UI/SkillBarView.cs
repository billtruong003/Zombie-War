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
            }

            if (slots != null)
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    if (i >= _powers.Count) { slots[i].ShowEmpty(); continue; }
                    var def = _powers[i];
                    bool evolved = run.IsEvolved(def.id);
                    slots[i].Show(icons != null ? icons.For(def.id) : null, SkillIconSet.Abbreviation(def.displayName),
                        evolved ? EvolutionGold : SkillDescriptions.LayerColor(def),
                        evolved ? "EVO" : run.RankOf(def.id).ToString(),
                        1f - run.ReadinessOf(def.id));
                }

            if (passivePips != null)
                for (int i = 0; i < passivePips.Length; i++)
                {
                    var pip = passivePips[i];
                    if (pip == null) continue;
                    bool on = i < _passives.Count;
                    if (pip.transform.parent.gameObject.activeSelf != on) pip.transform.parent.gameObject.SetActive(on);
                    if (!on) continue;
                    var def = _passives[i];
                    pip.text = SkillIconSet.Abbreviation(def.displayName) + run.RankOf(def.id);
                    pip.color = SkillDescriptions.LayerColor(def);
                }
        }
    }
}
