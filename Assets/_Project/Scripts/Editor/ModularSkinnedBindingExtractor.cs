#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Doc scene Demo cua Layer Lab (noi duy nhat chua mapping mesh skinned ↔ bone names)
    /// va dien skinnedMesh/materials/boneNames/rootBoneName vao ModularCostumeCatalog.
    ///
    /// Cau truc scene Demo:
    ///   Character/Characters/Body/&lt;Body_X&gt;                 (base body, SMR)
    ///   Character/Characters/Parts/PartA/&lt;Slot&gt;/&lt;Part_N&gt;  (SMR)
    ///   Character/Characters/Parts/PartB/&lt;Slot&gt;/&lt;Part_N&gt;  (SMR)
    /// Duyet THEO CAU TRUC NODE (khong reference script demo de tranh phu thuoc assembly).
    /// </summary>
    public static class ModularSkinnedBindingExtractor
    {
        private const string DemoScenePath =
            "Assets/ThirdParty/Layer Lab/3D Casual Character/3D Characters Pro - Fantasy/Scenes/Demo.unity";
        private const string CatalogPath =
            "Assets/_Project/Data/Character/ModularCostumeCatalog.asset";

        private struct SkinnedInfo
        {
            public Mesh mesh;
            public Material[] materials;
            public string[] boneNames;
            public string rootBoneName;
        }

        [MenuItem("ZombieWar/Extract Skinned Bindings (Demo Scene)")]
        public static void Extract()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[SkinnedExtract] Khong thay catalog tai {CatalogPath}");
                return;
            }

            // (slotName, partName) -> skinned info
            var lookup = new Dictionary<(string, string), SkinnedInfo>();

            var scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Additive);
            try
            {
                Transform charactersNode = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var found = FindDeep(root.transform, "Characters");
                    if (found != null && found.Find("Parts") != null) { charactersNode = found; break; }
                }

                if (charactersNode == null)
                {
                    Debug.LogError("[SkinnedExtract] Khong tim thay node 'Characters' co con 'Parts' trong scene Demo");
                    return;
                }

                // Base body: Characters/Body/<Body_Color>/<Body_Color_Part_N> (long them 1 cap nhom mau)
                var bodyNode = charactersNode.Find("Body");
                if (bodyNode != null)
                    foreach (Transform colorGroup in bodyNode)
                        CollectSlot("Body", colorGroup, lookup);

                // Parts: Characters/Parts/PartA|PartB/<Slot>/<Part_N>
                var partsNode = charactersNode.Find("Parts");
                foreach (Transform group in partsNode)          // PartA, PartB
                    foreach (Transform slotNode in group)       // Beard, Brow, ... Chest, Leg ...
                        CollectSlot(slotNode.name, slotNode, lookup);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            // Dien vao catalog, match theo (slot, ten part)
            int matched = 0, missing = 0;
            var missingReport = new StringBuilder();
            foreach (var slot in catalog.slots)
            {
                // Catalog slot "Body" (isBaseBody) map voi node "Body"
                // Catalog dung ten so nhieu (Legs/Feet/Hands) nhung node scene la so it (Leg/Foot/Hand)
                var slotKey = slot.isBaseBody ? "Body" : slot.slot;
                switch (slotKey)
                {
                    case "Legs": slotKey = "Leg"; break;
                    case "Feet": slotKey = "Foot"; break;
                    case "Hands": slotKey = "Hand"; break;
                }
                for (int i = 0; i < slot.parts.Count; i++)
                {
                    var e = slot.parts[i];
                    if (lookup.TryGetValue((slotKey, e.name), out var info))
                    {
                        e.skinnedMesh = info.mesh;
                        e.materials = info.materials;
                        e.boneNames = info.boneNames;
                        e.rootBoneName = info.rootBoneName;
                        slot.parts[i] = e;
                        matched++;
                    }
                    else
                    {
                        missing++;
                        if (missingReport.Length < 2000)
                            missingReport.AppendLine($"  {slotKey}/{e.name}");
                    }
                }
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SkinnedExtract] matched={matched} missing={missing} (demo co {lookup.Count} part skinned)"
                      + (missing > 0 ? "\nMissing:\n" + missingReport : ""));
        }

        private static void CollectSlot(string slotName, Transform slotNode,
            Dictionary<(string, string), SkinnedInfo> lookup)
        {
            foreach (Transform part in slotNode)
            {
                var smr = part.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr == null || smr.sharedMesh == null) continue;

                var bones = smr.bones;
                var names = new string[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    names[i] = bones[i] != null ? bones[i].name : string.Empty;

                lookup[(slotName, part.name)] = new SkinnedInfo
                {
                    mesh = smr.sharedMesh,
                    materials = smr.sharedMaterials,
                    boneNames = names,
                    rootBoneName = smr.rootBone != null ? smr.rootBone.name : string.Empty,
                };
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
#endif
