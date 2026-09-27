using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>All themes the player can pick, in Settings order. Lives at Resources/UI/ThemeSet.</summary>
    [CreateAssetMenu(menuName = "HordeCall/UI Theme Set")]
    public sealed class ThemeSet : ScriptableObject
    {
        public ThemePalette[] themes = new ThemePalette[0];
        public Material backgroundMaterial;

        public ThemePalette Find(string id)
        {
            if (themes == null) return null;
            foreach (var t in themes) if (t != null && t.id == id) return t;
            return null;
        }
    }
}
