using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Gun star pips, lit or not (QA 04/10): unlit stars were tinted with the theme's Edge colour,
    /// which multiplied onto the gold star sprite still read gold, so a 1-star gun looked like 3.
    /// An unlit star is a dark, half-clear silhouette of the same sprite: an empty slot.
    /// </summary>
    public static class StarPips
    {
        static readonly Color Unlit = new(0.10f, 0.11f, 0.16f, 0.55f);

        public static void Paint(Image star, bool lit)
        {
            if (star == null) return;
            ThemeTint.Clear(star, lit ? Color.white : Unlit);
        }
    }
}
