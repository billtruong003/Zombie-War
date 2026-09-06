using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.3c Item 2 — gate 7, unmet for three milestones.
    ///
    /// Station bodies were attached but never converted, so raw Synty materials shipped into a toon
    /// world. These assert the same contract the weapons carry, and that the vendor originals were
    /// duplicated rather than edited.
    /// </summary>
    public class SyntyPropContractTests
    {
        static readonly string[] Props =
        {
            "Assets/_Project/Prefabs/Stations/SM_Bld_Base_Pillar_01.prefab",
            "Assets/_Project/Prefabs/Stations/SM_Prop_Chest_01.prefab",
            "Assets/_Project/Prefabs/Stations/SM_Prop_Altar_Table_01.prefab",
        };

        [Test]
        public void StationPropsExistInProjectOwnership()
        {
            foreach (var p in Props)
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(p),
                    $"{p} missing — run ZombieWar/Stations/Convert Synty Props To Toon");
        }

        [Test]
        public void EveryStationPropMaterialIsOnTheToonContract()
        {
            foreach (var p in Props)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        Assert.IsNotNull(m.shader, $"{go.name}: material '{m.name}' has no shader");
                        StringAssert.Contains("Toon", m.shader.name,
                            $"{go.name}: '{m.name}' is still on {m.shader.name} — raw Synty in a toon world");
                    }
            }
        }

        [Test]
        public void NoStationPropReferencesAVendorMaterial()
        {
            foreach (var p in Props)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        string mp = AssetDatabase.GetAssetPath(m);
                        Assert.IsFalse(mp.StartsWith("Assets/Synty/") || mp.StartsWith("Assets/ThirdParty/"),
                            $"{go.name} still points at vendor material {mp} — a pack reimport would restyle it");
                    }
            }
        }

        [Test]
        public void VendorOriginalsWereNotEditedInPlace()
        {
            // The originals must still be on their stock URP shader: the converter copies, it does
            // not mutate a vendor asset.
            foreach (var src in new[]
            {
                "Assets/Synty/PolygonDarkFantasy/Prefabs/Base/SM_Bld_Base_Pillar_01.prefab",
                "Assets/Synty/PolygonDarkFantasy/Prefabs/Props/SM_Prop_Chest_01.prefab",
            })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(src);
                if (go == null) continue;
                bool anyVendorMaterialConverted = go.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null && m.shader != null)
                    .Any(m => m.shader.name.Contains("Toon"));

                Assert.IsFalse(anyVendorMaterialConverted,
                    $"{src} was converted IN PLACE — vendor assets must stay untouched");
            }
        }

        [Test]
        public void ConverterAuditReportsNoProblems()
        {
            var problems = EditorTools.SyntyPropToonConverter.Audit();
            CollectionAssert.IsEmpty(problems);
        }
    }
}
