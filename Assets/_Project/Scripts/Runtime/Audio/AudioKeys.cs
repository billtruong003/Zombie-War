using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Audio
{
    /// <summary>
    /// Which curated cue keys exist (the AddressableAudioCatalog in Resources), so code can derive a
    /// key ("…rifle.fire" → "…rifle.tail") and play it only when the library has it.
    /// </summary>
    public static class AudioKeys
    {
        static HashSet<string> _keys;

        public static bool Has(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (_keys == null)
            {
                _keys = new HashSet<string>();
                var cat = Resources.Load<AddressableAudioCatalog>("Audio/AddressableAudioCatalog");
                if (cat != null && cat.Variants != null)
                    foreach (var v in cat.Variants) if (v != null && !string.IsNullOrEmpty(v.cueKey)) _keys.Add(v.cueKey);
            }
            return _keys.Contains(key);
        }

        /// <summary>The sibling of a weapon's fire key ("sfx.weapon.rifle.fire" + "tail"), or null.</summary>
        public static string Sibling(string fireKey, string part)
        {
            if (string.IsNullOrEmpty(fireKey) || !fireKey.EndsWith(".fire")) return null;
            string k = fireKey.Substring(0, fireKey.Length - 4) + part;
            return Has(k) ? k : null;
        }
    }
}
