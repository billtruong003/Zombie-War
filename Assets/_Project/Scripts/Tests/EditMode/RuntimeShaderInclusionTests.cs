using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>
    /// 05/10 device playtest: a shader the game only reaches through Shader.Find is stripped from
    /// a player build unless something includes it (ToonBloom and ItemTileFx were missing on the
    /// phone). Every name game code looks up must be in Always Included Shaders.
    /// </summary>
    public class RuntimeShaderInclusionTests
    {
        static readonly string[] Roots = { "Assets/_Project/Scripts/Runtime", "Assets/_Project/Art/Rendering" };
        // Development-only tools and demos that never run in a player.
        static readonly string[] Skip = { "/Dev/", "EnvNavDemo.cs", "ChunkDiagnosticAssets.cs" };

        [Test]
        public void EveryShaderFoundByNameShipsInTheBuild()
        {
            var always = new HashSet<string>();
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue is Shader s) always.Add(s.name);

            var missing = new List<string>();
            foreach (var root in Roots)
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                bool skip = false;
                foreach (var s in Skip) skip |= path.Contains(s);
                if (skip) continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "Shader\\.Find\\(\"([^\"]+)\"\\)"))
                {
                    string name = m.Groups[1].Value;
                    if (!always.Contains(name)) missing.Add($"{name}  ({Path.GetFileName(file)})");
                }
            }
            Assert.IsEmpty(missing, "add these to Graphics Settings > Always Included Shaders:\n" + string.Join("\n", missing));
        }
    }
}
