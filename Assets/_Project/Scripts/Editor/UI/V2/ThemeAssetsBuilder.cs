using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.EditorTools.V2
{
    /// <summary>
    /// Builds the four UI theme palettes and the ThemeSet (Resources/UI/ThemeSet) from
    /// Tools/themes.json, which holds the OKLCH-built tokens the owner reviewed on the mockup canvas.
    /// </summary>
    public static class ThemeAssetsBuilder
    {
        const string Json = "Tools/themes.json";
        const string Dir = "Assets/_Project/UI/Theme/";
        const string SetPath = "Assets/Resources/UI/ThemeSet.asset";
        const string BgMat = "Assets/_Project/Art/Materials/UI/MenuBg_sky.mat";

        [Serializable] class ColorEntry { public string role; public string hex; }
        [Serializable] class ThemeEntry
        {
            public string id, name; public bool dark; public List<ColorEntry> colors;
            public string bg_top, bg_bottom, bg_shape; public float bg_alpha, bg_rays, bg_vignette;
        }
        [Serializable] class Root { public List<ThemeEntry> themes; }

        static Color H(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

        [MenuItem("HordeCall/UI v2/Build Theme Assets")]
        public static string Build()
        {
            var root = JsonUtility.FromJson<Root>(File.ReadAllText(Json));
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(Path.GetDirectoryName(SetPath));
            var palettes = new List<ThemePalette>();
            foreach (var t in root.themes)
            {
                string path = Dir + "Theme_" + t.id + ".asset";
                var p = AssetDatabase.LoadAssetAtPath<ThemePalette>(path);
                if (p == null) { p = ScriptableObject.CreateInstance<ThemePalette>(); AssetDatabase.CreateAsset(p, path); }
                p.id = t.id; p.displayName = t.name; p.dark = t.dark;
                foreach (var c in t.colors)
                    if (Enum.TryParse<ThemeRole>(c.role, out var role)) p.Set(role, H(c.hex));
                p.bgTop = H(t.bg_top); p.bgBottom = H(t.bg_bottom); p.bgShape = H(t.bg_shape);
                p.bgShapeAlpha = t.bg_alpha; p.bgRays = t.bg_rays; p.bgVignette = t.bg_vignette;
                EditorUtility.SetDirty(p);
                palettes.Add(p);
            }
            var set = AssetDatabase.LoadAssetAtPath<ThemeSet>(SetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<ThemeSet>(); AssetDatabase.CreateAsset(set, SetPath); }
            set.themes = palettes.ToArray();
            set.backgroundMaterial = AssetDatabase.LoadAssetAtPath<Material>(BgMat);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            ThemeService.Reset();
            return $"{palettes.Count} themes -> {SetPath}";
        }

        /// <summary>Editor preview of a theme (Theme menu), used by the review renders.</summary>
        public static void Preview(string id) { ThemeService.Reset(); ThemeService.Use(id); }
    }
}
