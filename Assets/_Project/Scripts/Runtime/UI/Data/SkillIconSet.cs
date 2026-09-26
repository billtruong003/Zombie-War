using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Card id → icon sprite for the level-up cards, the HUD skill bar and the result build recap.
    ///
    /// Filled by the editor command "ZombieWar/UI/Authoring/Refresh Skill Icons", which scans
    /// <c>Assets/_Project/UI/Icons/Skills/&lt;card id&gt;.png</c> (the files generated from
    /// <c>Review/M8/skill_icon_prompts.md</c>). A card without an icon falls back to a badge: its
    /// layer colour and a two-letter abbreviation, so the UI is complete before the art lands.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/UI/Skill Icon Set", fileName = "SkillIconSet")]
    public class SkillIconSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public Sprite icon;
        }

        [SerializeField] private List<Entry> entries = new();

        Dictionary<string, Sprite> _map;

        public IReadOnlyList<Entry> Entries => entries;

        public Sprite For(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_map == null || _map.Count != entries.Count)
            {
                _map = new Dictionary<string, Sprite>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                    if (!string.IsNullOrEmpty(entries[i].id)) _map[entries[i].id] = entries[i].icon;
            }
            return _map.TryGetValue(id, out var s) ? s : null;
        }

        public void SetEntries(List<Entry> list)
        {
            entries = list ?? new List<Entry>();
            _map = null;
        }

        /// <summary>"Orbit Blades" → "OB", "Airstrike" → "AI". Shown when a card has no icon yet.</summary>
        public static string Abbreviation(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "?";
            var words = displayName.Split(new[] { ' ', '-', '&' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2) return (char.ToUpperInvariant(words[0][0]).ToString() + char.ToUpperInvariant(words[1][0]));
            string w = words[0];
            return w.Length >= 2 ? char.ToUpperInvariant(w[0]) + w.Substring(1, 1).ToUpperInvariant() : w.ToUpperInvariant();
        }
    }
}
