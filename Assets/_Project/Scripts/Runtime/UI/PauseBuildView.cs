using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZombieWar.Skills;

namespace ZombieWar.UI
{
    /// <summary>
    /// Pause build card (mockup U3, owner-approved 05/10): the six Skill slots with name and rank,
    /// the four Stat slots, and the evolution the build is heading for. The HUD shows only the six
    /// skill icons; everything else lives here, so the run screen stays clear.
    ///
    /// The card is authored in UI_Hud (PauseModal/BuildCard); this finds its parts by name.
    /// </summary>
    public sealed class PauseBuildView : MonoBehaviour
    {
        [SerializeField] private SkillIconSet icons;

        sealed class Cell
        {
            public GameObject root;
            public Image frame, icon, pill;
            public TMP_Text rank, name;
        }

        static readonly Color EmptyFrame = new(0.77f, 0.87f, 0.95f, 1f);   // edge #c4ddf2
        static readonly Color PillYellow = new(1f, 0.757f, 0.165f, 1f);

        readonly List<Cell> _skills = new(6), _stats = new(4);
        TMP_Text _skillsHeader, _statsHeader, _evoTag, _evoTitle, _evoSub;
        GameObject _evo;
        Image _evoIcon;
        readonly List<SkillDef> _skillDefs = new(6), _statDefs = new(4);

        void Awake()
        {
            for (int i = 0; i < SkillCatalogDefs.MaxSkillSlots; i++) _skills.Add(Find("Row" + i, true));
            for (int i = 0; i < SkillCatalogDefs.MaxStatSlots; i++) _stats.Add(Find("Stat" + i, false));
            _skillsHeader = Text(transform, "SkillsHeader");
            _statsHeader = Text(transform, "StatsHeader");
            var evo = transform.Find("Evo");
            _evo = evo != null ? evo.gameObject : null;
            if (evo != null)
            {
                _evoIcon = evo.Find("Icon")?.GetComponent<Image>();
                _evoTag = Text(evo, "Tag"); _evoTitle = Text(evo, "Title"); _evoSub = Text(evo, "Sub");
            }
        }

        void OnEnable() => Refresh(SkillRuntime.Active);

        static TMP_Text Text(Transform root, string name) => root.Find(name)?.GetComponent<TMP_Text>();

        Cell Find(string name, bool named)
        {
            var t = transform.Find(name);
            if (t == null) return null;
            var frame = t.Find("Frame");
            var pill = frame != null ? frame.Find("Rank") : null;
            return new Cell
            {
                root = t.gameObject,
                frame = frame != null ? frame.GetComponent<Image>() : null,
                icon = frame != null ? frame.Find("Icon")?.GetComponent<Image>() : null,
                pill = pill != null ? pill.GetComponent<Image>() : null,
                rank = pill != null ? pill.GetComponentInChildren<TMP_Text>(true) : null,
                name = named ? Text(t, "Name") : null,
            };
        }

        public void Refresh(SkillRuntime run)
        {
            _skillDefs.Clear(); _statDefs.Clear();
            if (run != null)
                foreach (var kv in run.Ranks)
                {
                    var def = SkillCatalogDefs.ById(kv.Key);
                    if (def == null || kv.Value <= 0) continue;
                    if (def.Slot == SkillSlot.Skill) _skillDefs.Add(def);
                    else if (def.Slot == SkillSlot.Stat) _statDefs.Add(def);
                }

            _skillDefs.Sort(CatalogOrder); _statDefs.Sort(CatalogOrder);
            if (_skillsHeader != null) _skillsHeader.text = $"SKILLS {_skillDefs.Count} / {SkillCatalogDefs.MaxSkillSlots}";
            if (_statsHeader != null) _statsHeader.text = $"STATS {_statDefs.Count} / {SkillCatalogDefs.MaxStatSlots}";
            for (int i = 0; i < _skills.Count; i++) Fill(_skills[i], i < _skillDefs.Count ? _skillDefs[i] : null, run);
            for (int i = 0; i < _stats.Count; i++) Fill(_stats[i], i < _statDefs.Count ? _statDefs[i] : null, run);

            var line = EvolutionLine(run);
            if (_evo != null) _evo.SetActive(line.evo != null);
            if (line.evo == null) return;
            if (_evoTag != null) _evoTag.text = line.tag;
            if (_evoTitle != null) _evoTitle.text = line.title;
            if (_evoSub != null) _evoSub.text = line.sub;
            if (_evoIcon != null)
            {
                var s = icons != null ? icons.For(line.evo.id) : null;
                _evoIcon.enabled = s != null;
                if (s != null) _evoIcon.sprite = s;
            }
        }

        void Fill(Cell c, SkillDef def, SkillRuntime run)
        {
            if (c == null) return;
            bool on = def != null;
            if (c.frame != null) c.frame.color = on ? SkillDescriptions.LayerColor(def) : EmptyFrame;
            if (c.icon != null)
            {
                var s = on && icons != null ? icons.For(run.IsEvolved(def.id) ? SkillCatalogDefs.EvolutionOf(def.id)?.id ?? def.id : def.id) : null;
                c.icon.enabled = s != null;
                if (s != null) c.icon.sprite = s;
            }
            if (c.pill != null)
            {
                c.pill.gameObject.SetActive(on);
                c.pill.color = PillYellow;
            }
            if (c.rank != null && on) c.rank.text = run.IsEvolved(def.id) ? "EVO" : run.RankOf(def.id).ToString();
            if (c.name != null) c.name.text = on ? def.displayName : string.Empty;
        }

        /// <summary>Catalog order, so the HUD grid and this card list a build the same way.</summary>
        public static int CatalogOrder(SkillDef a, SkillDef b) => IndexOf(a).CompareTo(IndexOf(b));

        static int IndexOf(SkillDef d)
        {
            var all = SkillCatalogDefs.All;
            for (int i = 0; i < all.Count; i++) if (all[i] == d) return i;
            return int.MaxValue;
        }

        public struct Line
        {
            public SkillDef evo;
            public string tag, title, sub;
        }

        /// <summary>
        /// What the card says about evolutions: one that is ready (power maxed + partner owned) wins;
        /// otherwise the owned power closest to its evolution, with what is still missing. Nothing
        /// when the build has no power that evolves.
        /// </summary>
        public static Line EvolutionLine(SkillRuntime run)
        {
            if (run == null) return default;
            SkillDef best = null; int bestScore = int.MinValue;
            var all = SkillCatalogDefs.All;
            for (int i = 0; i < all.Count; i++)
            {
                var evo = all[i];
                if (!evo.IsEvolution || run.Has(evo.id) || !run.Has(evo.evolvesFrom)) continue;
                if (run.CanEvolve(evo)) return Ready(evo);
                var from = SkillCatalogDefs.ById(evo.evolvesFrom);
                // Closest first: fewer ranks to go, then a partner already owned.
                int score = -(from.maxRank - run.RankOf(from.id)) * 2 + (run.Has(evo.partner) ? 1 : 0);
                if (score > bestScore) { bestScore = score; best = evo; }
            }
            if (best == null) return default;
            var power = SkillCatalogDefs.ById(best.evolvesFrom);
            var partner = SkillCatalogDefs.ById(best.partner);
            int rank = run.RankOf(power.id);
            string need = rank < power.maxRank
                ? $"{power.displayName} rank {rank}/{power.maxRank}"
                : $"{power.displayName} is maxed";
            need += run.Has(best.partner) ? $" · {partner?.displayName} owned" : $" · needs {partner?.displayName}";
            return new Line
            {
                evo = best,
                tag = "NEXT EVOLUTION",
                title = $"{power.displayName} {power.maxRank} + {partner?.displayName}: {best.displayName}",
                sub = need,
            };
        }

        static Line Ready(SkillDef evo)
        {
            var power = SkillCatalogDefs.ById(evo.evolvesFrom);
            var partner = SkillCatalogDefs.ById(evo.partner);
            return new Line
            {
                evo = evo,
                tag = "EVOLUTION READY",
                title = $"{power?.displayName} {power?.maxRank} + {partner?.displayName}: {evo.displayName}",
                sub = "Open a chest to evolve it.",
            };
        }
    }
}
