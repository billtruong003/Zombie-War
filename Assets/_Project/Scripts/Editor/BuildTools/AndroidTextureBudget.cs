using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Caps texture sizes on Android only (06/10, build size): the editor, PC and WebGL keep their
    /// settings. Measured before: the Layer Lab costume icons were 1,107 textures at 1024 px
    /// (~468 MB of texture data) though the UI shows them small; 3D textures above 1024 px added
    /// ~112 MB. Rules, applied to textures that end up in the player (build scenes, Resources):
    ///  - Layer Lab costume / item icons (sprites): max <see cref="IconMax"/>;
    ///  - normal maps: max 1024;
    ///  - other 3D textures: max 1024, flip-book / atlas sheets max 2048;
    ///  - the game's own UI sprites (Assets/_Project/UI, Resources/UI) are left alone.
    /// Only lowers a size, never raises one, and keeps each texture's format. Re-running is safe.
    /// </summary>
    public static class AndroidTextureBudget
    {
        public const int IconMax = 512, TextureMax = 1024, SheetMax = 2048;
        const string Platform = "Android";

        [MenuItem("HordeCall/Build/Android Texture Budget (dry run)")]
        static void DryRun() => Debug.Log(Run(apply: false));

        [MenuItem("HordeCall/Build/Android Texture Budget (apply)")]
        static void Apply() => Debug.Log(Run(apply: true));

        public static string Run(bool apply)
        {
            var changes = new List<(string path, int from, int to)>();
            foreach (var path in TexturesInPlayer())
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
                int cap = CapFor(path, imp);
                if (cap <= 0) continue;
                var ps = imp.GetPlatformTextureSettings(Platform);
                int current = ps.overridden ? ps.maxTextureSize : imp.maxTextureSize;
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (Mathf.Max(w, h) <= cap || current <= cap) continue;
                changes.Add((path, current, cap));
                if (!apply) continue;
                if (!ps.overridden)
                {
                    var def = imp.GetDefaultPlatformTextureSettings();
                    ps.overridden = true;
                    ps.format = imp.GetAutomaticFormat(Platform);
                    ps.compressionQuality = def.compressionQuality;
                    ps.crunchedCompression = def.crunchedCompression;
                }
                ps.maxTextureSize = cap;
                imp.SetPlatformTextureSettings(ps);
            }
            if (apply && changes.Count > 0)
            {
                AssetDatabase.StartAssetEditing();
                try { foreach (var c in changes) AssetDatabase.ImportAsset(c.path, ImportAssetOptions.ForceUpdate); }
                finally { AssetDatabase.StopAssetEditing(); }
            }
            var byCap = changes.GroupBy(c => c.to).Select(g => $"{g.Count()} -> {g.Key}");
            return $"[TextureBudget] {(apply ? "applied" : "dry run")}: {changes.Count} textures ({string.Join(", ", byCap)})";
        }

        static int CapFor(string path, TextureImporter imp)
        {
            if (path.Contains("/_Project/UI/") || path.Contains("/Resources/UI/")) return 0;
            if (path.Contains("/Layer Lab/") && imp.textureType == TextureImporterType.Sprite) return IconMax;
            if (imp.textureType == TextureImporterType.Sprite || imp.textureType == TextureImporterType.GUI) return 0;
            if (imp.textureType == TextureImporterType.NormalMap) return TextureMax;
            string file = Path.GetFileNameWithoutExtension(path);
            bool sheet = path.Contains("/Epic Toon FX/") || file.Contains("Atlas") || file.EndsWith("_Texture_01_A");
            return sheet ? SheetMax : TextureMax;
        }

        /// <summary>Textures the Android player loads: build scenes, Resources and Addressables content.</summary>
        static IEnumerable<string> TexturesInPlayer()
        {
            var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            roots.AddRange(AssetDatabase.FindAssets("", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)));
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
                foreach (var g in settings.groups.Where(g => g != null))
                    roots.AddRange(g.entries.Select(e => e.AssetPath).Where(p => !string.IsNullOrEmpty(p)));
            return AssetDatabase.GetDependencies(roots.ToArray(), true)
                .Where(p => p.StartsWith("Assets/") && AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(Texture2D))
                .Distinct();
        }
    }
}
