using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// M7.3c Item 2 — brings Synty station props onto the project's toon material contract.
    ///
    /// Gate 7 went unmet for three milestones: the station bodies were attached but never converted,
    /// so they shipped raw Synty materials into a toon world.
    ///
    /// This mirrors exactly what <see cref="WeaponOnboarding"/> does for weapons:
    /// <list type="bullet">
    /// <item>copy the prop into project ownership — a vendor prefab is never edited</item>
    /// <item>copy each vendor material into the project, then convert the COPY</item>
    /// <item>never write to a vendor <c>.mat</c> in place</item>
    /// </list>
    /// </summary>
    public static class SyntyPropToonConverter
    {
        const string PropDir = "Assets/_Project/Prefabs/Stations";
        const string MatDir = "Assets/_Project/Materials/Stations";
        const string ToonShader = "StylizedToonWorldKit/Toon/Toon Lit";

        static readonly string[] Sources =
        {
            "Assets/Synty/PolygonDarkFantasy/Prefabs/Base/SM_Bld_Base_Pillar_01.prefab",
            "Assets/Synty/PolygonDarkFantasy/Prefabs/Props/SM_Prop_Chest_01.prefab",
            "Assets/Synty/PolygonDarkFantasy/Prefabs/Props/SM_Prop_Altar_Table_01.prefab",
            "Assets/Synty/PolygonDarkFantasy/Prefabs/Props/SM_Prop_Barrel_01.prefab",
            "Assets/Synty/PolygonDarkFantasy/Prefabs/Props/SM_Prop_Crate_01.prefab",
        };

        [MenuItem("ZombieWar/Stations/Convert Synty Props To Toon")]
        public static void Run()
        {
            EnsureFolder("Assets/_Project/Prefabs", "Stations");
            EnsureFolder("Assets/_Project/Materials", "Stations");

            var report = new List<string>();
            int converted = 0, matsOwned = 0;

            foreach (var src in Sources)
            {
                var srcGo = AssetDatabase.LoadAssetAtPath<GameObject>(src);
                if (srcGo == null) { report.Add($"MISSING {src}"); continue; }

                string dst = $"{PropDir}/{Path.GetFileNameWithoutExtension(src)}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) == null &&
                    !AssetDatabase.CopyAsset(src, dst))
                {
                    report.Add($"COPY FAILED {src}");
                    continue;
                }

                var contents = PrefabUtility.LoadPrefabContents(dst);
                int before = 0, after = 0;

                foreach (var rend in contents.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = rend.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        if (IsToon(mats[i])) { after++; continue; }
                        before++;
                        mats[i] = OwnAndConvert(mats[i], ref matsOwned);
                        if (IsToon(mats[i])) after++;
                    }
                    rend.sharedMaterials = mats;

                    // Same outline/selection contract the weapons carry: props are world dressing,
                    // so they stay off the player and enemy mask bits.
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, dst);
                PrefabUtility.UnloadPrefabContents(contents);
                converted++;
                report.Add($"{Path.GetFileNameWithoutExtension(src)}: {before} vendor material(s) -> toon, {after} toon total");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SyntyToon] converted {converted} prop(s), {matsOwned} material(s) now project-owned:\n  " +
                      string.Join("\n  ", report));
        }

        static bool IsToon(Material m) => m != null && m.shader != null && m.shader.name.Contains("Toon");

        static void EnsureFolder(string parent, string leaf)
        {
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
            if (!AssetDatabase.IsValidFolder($"{parent}/{leaf}"))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Duplicates a vendor material into the project and converts the COPY. The vendor asset is
        /// never written to — a standing project rule.
        /// </summary>
        static Material OwnAndConvert(Material m, ref int owned)
        {
            string path = AssetDatabase.GetAssetPath(m);
            if (string.IsNullOrEmpty(path)) return m;
            if (path.StartsWith("Assets/_Project/")) return m;   // already ours

            string dst = $"{MatDir}/{Sanitize(m.name)}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(dst);
            if (existing != null) { owned++; return existing; }

            if (!AssetDatabase.CopyAsset(path, dst)) return m;
            var copy = AssetDatabase.LoadAssetAtPath<Material>(dst);
            if (copy == null) return m;

            var toon = Shader.Find(ToonShader);
            if (toon != null)
            {
                // Carry the vendor's colour and albedo across so the prop keeps its identity; only
                // the lighting model changes.
                Color baseColor = copy.HasProperty("_BaseColor") ? copy.GetColor("_BaseColor")
                                : copy.HasProperty("_Color") ? copy.GetColor("_Color") : Color.white;
                Texture baseMap = copy.HasProperty("_BaseMap") ? copy.GetTexture("_BaseMap")
                                : copy.HasProperty("_MainTex") ? copy.GetTexture("_MainTex") : null;

                copy.shader = toon;
                if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", baseColor);
                if (copy.HasProperty("_BaseMap")) copy.SetTexture("_BaseMap", baseMap);
                EditorUtility.SetDirty(copy);
            }
            owned++;
            return copy;
        }

        static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        /// <summary>Read-only audit used by the test: any station prop still on a vendor material.</summary>
        public static List<string> Audit()
        {
            var problems = new List<string>();
            foreach (var src in Sources)
            {
                string dst = $"{PropDir}/{Path.GetFileNameWithoutExtension(src)}.prefab";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(dst);
                if (go == null) { problems.Add($"{dst} not created"); continue; }

                foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in rend.sharedMaterials)
                    {
                        if (m == null) continue;
                        string mp = AssetDatabase.GetAssetPath(m);
                        if (mp.StartsWith("Assets/Synty/") || mp.StartsWith("Assets/ThirdParty/"))
                            problems.Add($"{go.name} still uses vendor material {mp}");
                        else if (!IsToon(m))
                            problems.Add($"{go.name} material '{m.name}' is not on the toon contract");
                    }
            }
            return problems;
        }
    }
}
