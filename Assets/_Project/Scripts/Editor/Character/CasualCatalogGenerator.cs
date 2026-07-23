using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Deterministic generator for the Casual costume catalog. Scans the authoritative skinned
    /// source (Layer Lab Character.fbx — every part is a SkinnedMeshRenderer on the shared
    /// QuickRigCharacter2_* skeleton) and produces a <see cref="ModularCostumeCatalog"/> asset the
    /// existing <see cref="CharacterModularApplier"/> can rebind onto the Player skeleton by bone name.
    ///
    /// Idempotent: running twice on an unchanged source produces the same asset. Never modifies the
    /// vendor pack. Validates every referenced bone against the real Player skeleton and fails loudly.
    /// </summary>
    public static class CasualCatalogGenerator
    {
        const string CharacterFbx =
            "Assets/ThirdParty/Layer Lab/3D CharactersCasual/3D Characters Pro-Casual/FBX/Character/Characters.fbx";
        const string FreeMaterialDir =
            "Assets/ThirdParty/Layer Lab/3D Casual Character/3D Casual Character/Material";
        const string ProMaterialDir =
            "Assets/ThirdParty/Layer Lab/3D CharactersCasual/3D Characters Pro-Casual/Materials";
        const string ProAtlas =
            "Assets/ThirdParty/Layer Lab/3D CharactersCasual/3D Characters Pro-Casual/Textures/Casual_Character.png";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        const string OutCatalog = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        const string OutReport = "Assets/Screenshots/CasualMigration/casual_catalog_audit.txt";
        const string ProAuditReport = "Assets/Screenshots/CasualMigration/pro_casual_source_audit.txt";

        static readonly string[] ProMaterialNames = { "ColorA", "ColorB", "ColorC", "ColorD" };

        // Vendor mesh prefix -> logical slot. Body is special (digits-only = player-facing, rest = assembly).
        static readonly Dictionary<string, string> PrefixToSlot = new()
        {
            { "Hair_", "Hair" }, { "Eye_", "Eye" }, { "Eyebrow_", "Brow" }, { "lips_", "Mouth" },
            { "Mustache_", "Beard" }, { "Mask_", "Mask" }, { "Earring_", "Earring" },
            { "HairAcc_", "HairAccessory" }, { "Headgear_", "Head" }, { "Eyewear_", "Eyewear" },
            { "Top_", "Chest" }, { "Glove_", "Hands" }, { "Bracelet_", "Bracelet" },
            { "HandAcc_", "HandAccessory" }, { "Watch_", "Watch" }, { "Bag_", "Back" },
            { "Body_", "Body" }, { "Bottom_", "Legs" }, { "Shoes_", "Feet" },
        };

        [MenuItem("ZombieWar/Costume/Prepare Pro Casual Materials")]
        public static void PrepareProMaterials()
        {
            EnsureAssetFolder(ProMaterialDir);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(ProAtlas);
            if (atlas == null) { Debug.LogError($"[ProCasualMaterial] atlas not found: {ProAtlas}"); return; }

            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string materialName in ProMaterialNames)
            {
                string sourcePath = $"{FreeMaterialDir}/{materialName}.mat";
                string destinationPath = $"{ProMaterialDir}/{materialName}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(destinationPath) == null
                    && !AssetDatabase.CopyAsset(sourcePath, destinationPath))
                {
                    Debug.LogError($"[ProCasualMaterial] cannot copy {sourcePath} -> {destinationPath}");
                    return;
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(destinationPath);
                if (material == null) { Debug.LogError($"[ProCasualMaterial] cannot load {destinationPath}"); return; }
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", atlas);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", atlas);
                if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", atlas);
                EditorUtility.SetDirty(material);
                materials[materialName] = material;
            }

            var importer = AssetImporter.GetAtPath(CharacterFbx) as ModelImporter;
            if (importer == null) { Debug.LogError($"[ProCasualMaterial] ModelImporter not found: {CharacterFbx}"); return; }
            foreach (var pair in materials)
            {
                var identifier = new AssetImporter.SourceAssetIdentifier
                {
                    type = typeof(Material),
                    name = pair.Key,
                };
                importer.AddRemap(identifier, pair.Value);
            }
            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();
            Debug.Log($"[ProCasualMaterial] Prepared {materials.Count} URP materials with Pro atlas and remapped {CharacterFbx}.");
        }

        [MenuItem("ZombieWar/Costume/Audit Pro Casual Source")]
        public static void AuditProSource()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbx);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (source == null) { Debug.LogError($"[ProCasualAudit] source not found: {CharacterFbx}"); return; }
            if (player == null) { Debug.LogError($"[ProCasualAudit] Player not found: {PlayerPrefab}"); return; }

            var sourceBones = source.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("QuickRigCharacter2_", StringComparison.Ordinal))
                .GroupBy(t => t.name)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var playerBones = player.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("QuickRigCharacter2_", StringComparison.Ordinal))
                .GroupBy(t => t.name)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            float maxPositionDelta = 0f;
            float maxRotationDelta = 0f;
            float maxScaleDelta = 0f;
            var missingPlayerBones = new List<string>();
            foreach (var pair in sourceBones)
            {
                if (!playerBones.TryGetValue(pair.Key, out var playerBone))
                {
                    missingPlayerBones.Add(pair.Key);
                    continue;
                }

                maxPositionDelta = Mathf.Max(maxPositionDelta,
                    Vector3.Distance(pair.Value.localPosition, playerBone.localPosition));
                maxRotationDelta = Mathf.Max(maxRotationDelta,
                    Quaternion.Angle(pair.Value.localRotation, playerBone.localRotation));
                maxScaleDelta = Mathf.Max(maxScaleDelta,
                    Vector3.Distance(pair.Value.localScale, playerBone.localScale));
            }

            var categoryCounts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var bindingErrors = new List<string>();
            var renderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(smr => smr.sharedMesh != null)
                .ToArray();
            foreach (var smr in renderers)
            {
                string category = SourceCategory(smr.sharedMesh.name);
                categoryCounts.TryGetValue(category, out int count);
                categoryCounts[category] = count + 1;

                if (smr.bones.Length != smr.sharedMesh.bindposeCount)
                    bindingErrors.Add($"{smr.sharedMesh.name}: bones={smr.bones.Length}, bindposes={smr.sharedMesh.bindposeCount}");
                foreach (var bone in smr.bones)
                    if (bone == null || !playerBones.ContainsKey(bone.name))
                        bindingErrors.Add($"{smr.sharedMesh.name}: missing Player bone '{(bone != null ? bone.name : "<null>")}'");
                if (smr.rootBone == null || !playerBones.ContainsKey(smr.rootBone.name))
                    bindingErrors.Add($"{smr.sharedMesh.name}: missing Player root bone '{(smr.rootBone != null ? smr.rootBone.name : "<null>")}'");
            }

            var report = new StringBuilder();
            report.AppendLine("=== Pro Casual source audit ===");
            report.AppendLine("source: " + CharacterFbx);
            report.AppendLine($"renderers: {renderers.Length}");
            report.AppendLine($"source QuickRig bones: {sourceBones.Count}");
            report.AppendLine($"missing Player bones: {missingPlayerBones.Count}");
            report.AppendLine($"max local position delta: {maxPositionDelta:F6}");
            report.AppendLine($"max local rotation delta: {maxRotationDelta:F6} deg");
            report.AppendLine($"max local scale delta: {maxScaleDelta:F6}");
            report.AppendLine($"binding errors: {bindingErrors.Count}");
            report.AppendLine();
            report.AppendLine("Categories:");
            foreach (var pair in categoryCounts) report.AppendLine($"  {pair.Key,-16} {pair.Value}");
            if (missingPlayerBones.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Missing Player bones:");
                foreach (var bone in missingPlayerBones.OrderBy(x => x, StringComparer.Ordinal)) report.AppendLine("  " + bone);
            }
            if (bindingErrors.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Binding errors:");
                foreach (var error in bindingErrors.Distinct().OrderBy(x => x, StringComparer.Ordinal)) report.AppendLine("  " + error);
            }

            WriteReport(ProAuditReport, report.ToString());
            if (missingPlayerBones.Count == 0 && bindingErrors.Count == 0)
                Debug.Log("[ProCasualAudit] PASS\n" + report);
            else
                Debug.LogError("[ProCasualAudit] FAILED\n" + report);
        }

        static string SourceCategory(string meshName)
        {
            int separator = meshName.IndexOf('_');
            return separator > 0 ? meshName.Substring(0, separator) : meshName;
        }

        [MenuItem("ZombieWar/Costume/Generate Casual Catalog")]
        public static void Generate()
        {
            PrepareProMaterials();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbx);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (source == null) { Debug.LogError($"[CasualCatalog] source not found: {CharacterFbx}"); return; }
            if (player == null) { Debug.LogError($"[CasualCatalog] Player not found: {PlayerPrefab}"); return; }

            var playerBones = new HashSet<string>();
            foreach (var t in player.GetComponentsInChildren<Transform>(true)) playerBones.Add(t.name);

            var sourceGuid = AssetDatabase.AssetPathToGUID(CharacterFbx);
            var slots = new Dictionary<string, ModularCostumeCatalog.Slot>(StringComparer.OrdinalIgnoreCase);
            var seenItemIds = new HashSet<string>();
            var report = new StringBuilder();
            var categoryCounts = new SortedDictionary<string, int>();
            var excludedAssembly = new List<string>();
            var errors = new List<string>();

            foreach (var smr in source.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                         .OrderBy(s => s.sharedMesh != null ? s.sharedMesh.name : "", StringComparer.Ordinal))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                string meshName = mesh.name;

                if (!TryClassify(meshName, out string slotId, out string sourceCategory, out bool isAssembly))
                    continue; // unknown category — skip silently (nothing player-facing)

                if (isAssembly) { excludedAssembly.Add(meshName); continue; }

                string itemId = MakeItemId(meshName, slotId, sourceCategory);
                if (!seenItemIds.Add(itemId))
                    errors.Add($"DUPLICATE itemId '{itemId}' (mesh {meshName}) — generation aborted");

                // Extract skinned binding, remap-by-name against Player.
                var boneNames = smr.bones.Select(b => b != null ? b.name : "<null>").ToArray();
                string rootName = smr.rootBone != null ? smr.rootBone.name : null;

                foreach (var bn in boneNames)
                    if (!playerBones.Contains(bn))
                        errors.Add($"{itemId} ({meshName}): bone '{bn}' missing on Player skeleton");
                if (string.IsNullOrEmpty(rootName) || !playerBones.Contains(rootName))
                    errors.Add($"{itemId} ({meshName}): rootBone '{rootName}' missing on Player skeleton");
                if (boneNames.Length != mesh.bindposeCount)
                    errors.Add($"{itemId} ({meshName}): boneNames={boneNames.Length} != bindposes={mesh.bindposeCount}");

                var entry = new ModularCostumeCatalog.PartEntry
                {
                    name = meshName,
                    assetPath = CharacterFbx,
                    guid = sourceGuid,
                    itemId = itemId,
                    prefab = null, // applier rebinds skinnedMesh directly; no prefab instantiation
                    skinnedMesh = mesh,
                    materials = smr.sharedMaterials,
                    boneNames = boneNames,
                    rootBoneName = rootName,
                };

                if (!slots.TryGetValue(slotId, out var slot))
                {
                    slot = new ModularCostumeCatalog.Slot { slot = slotId, isBaseBody = slotId == "Body" };
                    slots[slotId] = slot;
                }
                slot.parts.Add(entry);
                categoryCounts.TryGetValue(sourceCategory, out int c);
                categoryCounts[sourceCategory] = c + 1;
            }

            if (errors.Count > 0)
            {
                report.AppendLine("=== GENERATION FAILED — " + errors.Count + " error(s) ===");
                foreach (var e in errors) report.AppendLine("  " + e);
                WriteReport(report.ToString());
                Debug.LogError($"[CasualCatalog] {errors.Count} validation error(s) — see {OutReport}. Catalog NOT written.");
                return;
            }

            var catalog = ScriptableObject.CreateInstance<ModularCostumeCatalog>();
            catalog.compositeBody = false;
            catalog.sourceFolder = CharacterFbx;
            catalog.excludedCategories = new List<string>
            {
                "Body assembly meshes (Arm/Leg/Top/Bottom/Neck/Hand)",
                "Vendor held weapons (Axe/Sword/Spear/Shield)",
            };
            catalog.slotDefinitions = BuildSlotDefinitions();
            catalog.slots = SlotOrder
                .Where(slots.ContainsKey)
                .Select(id => slots[id])
                .ToList();

            // Order parts within each slot by numeric index for stable diffs.
            foreach (var slot in catalog.slots)
                slot.parts.Sort((a, b) => string.CompareOrdinal(a.itemId, b.itemId));

            // Author provisional starter defaults (Phase 3 refines visually). Required slots must resolve.
            ApplyProvisionalDefaults(catalog);

            var existing = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(OutCatalog);
            if (existing != null)
            {
                // Preserve icons bound by the Casual icon generator (keyed by stable itemId) so a
                // catalog rebuild does not wipe them.
                var iconByItem = new Dictionary<string, Sprite>();
                foreach (var s in existing.slots)
                {
                    if (existing.IsTechnicalCasualSlot(s.slot)) continue;
                    foreach (var p in s.parts)
                        if (p.icon != null && !string.IsNullOrEmpty(p.itemId)) iconByItem[p.itemId] = p.icon;
                }
                foreach (var s in catalog.slots)
                    for (int i = 0; i < s.parts.Count; i++)
                        if (iconByItem.TryGetValue(s.parts[i].itemId, out var icon))
                        { var e = s.parts[i]; e.icon = icon; s.parts[i] = e; }

                EditorUtility.CopySerialized(catalog, existing); catalog = existing;
            }
            else AssetDatabase.CreateAsset(catalog, OutCatalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            // Audit report.
            report.AppendLine("=== Pro Casual Costume Catalog — audit ===");
            report.AppendLine("source: " + CharacterFbx);
            int playerFacingParts = catalog.slots
                .Where(s => !catalog.IsTechnicalCasualSlot(s.slot))
                .Sum(s => s.parts.Count);
            report.AppendLine("internal source parts: " + catalog.TotalParts);
            report.AppendLine("player-facing parts: " + playerFacingParts);
            report.AppendLine("technical renderer parts: " + (catalog.TotalParts - playerFacingParts) + " (Head + Body_1..4)");
            report.AppendLine();
            report.AppendLine("Source mesh category counts (includes technical Head/Body):");
            foreach (var kv in categoryCounts) report.AppendLine($"  {kv.Key,-10} {kv.Value}");
            report.AppendLine();
            report.AppendLine("Logical slots:");
            foreach (var def in catalog.slotDefinitions)
            {
                var slot = catalog.GetSlot(def.id);
                int n = slot != null ? slot.parts.Count : 0;
                report.AppendLine($"  {def.id,-9} group={def.group,-5} parts={n,-3} required={def.required} allowNone={def.allowNone} default={def.defaultItemId}");
            }
            report.AppendLine();
            report.AppendLine($"Excluded Body assembly meshes ({excludedAssembly.Count}):");
            report.AppendLine("  " + string.Join(", ", excludedAssembly.OrderBy(x => x, StringComparer.Ordinal)));
            WriteReport(report.ToString());

            Debug.Log($"[CasualCatalog] OK — {playerFacingParts} player-facing parts, "
                + $"{catalog.TotalParts - playerFacingParts} technical parts, {catalog.slotDefinitions.Count} UI slots -> {OutCatalog}\n{report}");
        }

        // -------- classification --------

        static bool TryClassify(string meshName, out string slotId, out string sourceCategory, out bool isAssembly)
        {
            slotId = null; sourceCategory = null; isAssembly = false;
            if (string.Equals(meshName, "Head", StringComparison.Ordinal))
            {
                slotId = "Face";
                sourceCategory = "Head";
                return true;
            }
            foreach (var kv in PrefixToSlot)
            {
                if (!meshName.StartsWith(kv.Key, StringComparison.Ordinal)) continue;
                sourceCategory = kv.Key.TrimEnd('_');
                slotId = kv.Value;
                if (slotId == "Body")
                {
                    // Only Body_<digits> (Body_1..4) are runtime technical full bodies; the rest are
                    // unused assembly meshes (Body_ArmA_1, Body_Top_1, Body_Hand, Body_Neck, ...).
                    string tail = meshName.Substring("Body_".Length);
                    isAssembly = !tail.All(char.IsDigit);
                }
                return true;
            }
            return false;
        }

        // casual.pro.body.001 / casual.pro.face.base / casual.pro.chest.top.024 ...
        static string MakeItemId(string meshName, string slotId, string sourceCategory)
        {
            string cat = sourceCategory.ToLowerInvariant();
            switch (slotId)
            {
                case "Face": return "casual.pro.face.base";
                case "Chest": return $"casual.pro.chest.top.{Num(meshName):000}";
                case "Hands": return $"casual.pro.hands.glove.{Num(meshName):000}";
                case "Back": return $"casual.pro.back.bag.{Num(meshName):000}";
                case "Legs": return $"casual.pro.legs.bottom.{Num(meshName):000}";
                case "Feet": return $"casual.pro.feet.shoes.{Num(meshName):000}";
                default: return $"casual.pro.{cat}.{Num(meshName):000}";
            }
        }

        static int Num(string meshName)
        {
            int i = meshName.Length; while (i > 0 && char.IsDigit(meshName[i - 1])) i--;
            return int.TryParse(meshName.Substring(i), out int n) ? n : 0;
        }

        // -------- slot presentation (authored design) --------

        static readonly string[] SlotOrder =
        {
            "Face", "Eye", "Brow", "Mouth", "Hair", "Beard", "Mask", "HairAccessory", "Head", "Eyewear", "Earring",
            "Body", "Chest", "Hands", "Bracelet", "HandAccessory", "Watch", "Back", "Legs", "Feet",
        };

        static List<ModularCostumeCatalog.SlotDefinition> BuildSlotDefinitions()
        {
            var G = ModularCostumeCatalog.CostumeGroup.Head;
            var B = ModularCostumeCatalog.CostumeGroup.Body;
            var L = ModularCostumeCatalog.CostumeGroup.Legs;
            var list = new List<ModularCostumeCatalog.SlotDefinition>
            {
                Def("Eye",           "Eyes",           G, 1,  true,  false),
                Def("Brow",          "Brows",          G, 2,  true,  false),
                Def("Mouth",         "Mouth",          G, 3,  true,  false),
                Def("Hair",          "Hair",           G, 4,  true,  false),
                Def("Beard",         "Beard",          G, 5,  false, true),
                Def("Mask",          "Mask",           G, 6,  false, true),
                Def("HairAccessory", "Hair Accessory", G, 7,  false, true),
                Def("Head",          "Hat",            G, 8,  false, true),
                Def("Eyewear",       "Glasses",        G, 9,  false, true),
                Def("Earring",       "Earring",        G, 10, false, true),
                Def("Chest",         "Top",            B, 12, true,  false),
                Def("Hands",         "Gloves",         B, 13, false, true),
                Def("Bracelet",      "Bracelet",       B, 14, false, true),
                Def("HandAccessory", "Hand Accessory", B, 15, false, true),
                Def("Watch",         "Watch",          B, 16, false, true),
                Def("Back",          "Backpack",       B, 17, false, true),
                Def("Legs",          "Pants",          L, 18, true,  false),
                Def("Feet",          "Shoes",          L, 19, false, true),
            };
            return list;
        }

        static ModularCostumeCatalog.SlotDefinition Def(string id, string vn, ModularCostumeCatalog.CostumeGroup g,
            int order, bool required, bool allowNone) =>
            new() { id = id, displayName = vn, group = g, sortOrder = order, required = required, allowNone = allowNone };

        // Provisional coherent starter (proven-good combo from the bind-pose spike). Phase 3 finalizes
        // after visual inspection. Required slots MUST resolve to a real item present in the catalog.
        static readonly (string slot, string itemId)[] ProvisionalDefaults =
        {
            ("Eye",   "casual.pro.eye.001"),
            ("Brow",  "casual.pro.eyebrow.001"),
            ("Mouth", "casual.pro.lips.001"),
            ("Hair",  "casual.pro.hair.001"),
            ("Chest", "casual.pro.chest.top.001"),
            ("Legs",  "casual.pro.legs.bottom.001"),
            ("Feet",  "casual.pro.feet.shoes.001"),
        };

        static void ApplyProvisionalDefaults(ModularCostumeCatalog catalog)
        {
            foreach (var (slotId, itemId) in ProvisionalDefaults)
            {
                var def = catalog.GetSlotDefinition(slotId);
                if (def == null) continue;
                bool exists = catalog.TryFindByItemId(itemId, out _, out _);
                if (!exists)
                {
                    // Fall back to the first item in the slot so a required slot always resolves.
                    var slot = catalog.GetSlot(slotId);
                    if (slot != null && slot.parts.Count > 0) def.defaultItemId = slot.parts[0].itemId;
                    Debug.LogWarning($"[CasualCatalog] default {itemId} for slot {slotId} not found; fell back to {def.defaultItemId}");
                }
                else def.defaultItemId = itemId;
            }
        }

        static void WriteReport(string text)
        {
            WriteReport(OutReport, text);
        }

        static void WriteReport(string assetPath, string text)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath), assetPath), text);
            AssetDatabase.ImportAsset(assetPath);
        }

        static void EnsureAssetFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
