using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieWar.Skills;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Builds <see cref="SkillIconSet"/> from <c>Assets/_Project/UI/Icons/Skills/&lt;card id&gt;.png</c>.
    /// Run after dropping in the icons generated from <c>Review/M8/skill_icon_prompts.md</c>; cards
    /// without a file keep their fallback badge. Also imports each PNG as a transparent sprite.
    /// </summary>
    public static class SkillIconSetBuilder
    {
        public const string IconDir = "Assets/_Project/UI/Icons/Skills";
        public const string AssetPath = "Assets/_Project/UI/Data/SkillIconSet.asset";

        [MenuItem("ZombieWar/UI/Authoring/Refresh Skill Icons")]
        public static SkillIconSet Refresh()
        {
            Directory.CreateDirectory(IconDir);
            var set = AssetDatabase.LoadAssetAtPath<SkillIconSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SkillIconSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }

            var entries = new List<SkillIconSet.Entry>();
            int found = 0;
            var defs = new List<SkillDef>(SkillCatalogDefs.All);
            defs.AddRange(SkillCatalogDefs.Overflow);   // the BONUS cards need art too
            foreach (var def in defs)
            {
                string path = $"{IconDir}/{def.id}.png";
                Sprite sprite = null;
                if (File.Exists(path))
                {
                    var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                    if (imp != null && (imp.textureType != TextureImporterType.Sprite || !imp.alphaIsTransparency))
                    {
                        imp.textureType = TextureImporterType.Sprite;
                        imp.spriteImportMode = SpriteImportMode.Single;
                        imp.alphaIsTransparency = true;
                        imp.mipmapEnabled = false;
                        imp.maxTextureSize = 256;
                        imp.SaveAndReimport();
                    }
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null) found++;
                }
                entries.Add(new SkillIconSet.Entry { id = def.id, icon = sprite });
            }
            set.SetEntries(entries);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillIcons] {found}/{defs.Count} cards have an icon; the rest show their badge.");
            return set;
        }
    }
}
