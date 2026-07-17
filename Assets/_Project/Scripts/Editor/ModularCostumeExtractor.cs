#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Scans the Layer Lab "3D Casual Character Pro - Fantasy" Parts prefab folder, groups the
    /// ~2000 part prefabs into logical costume slots, and bakes a ModularCostumeCatalog asset
    /// (+ a JSON report for eyeballing). Held items are skipped — see EXCLUDED.
    ///
    /// Run: ZombieWar/Extract Modular Costume Catalog
    /// </summary>
    public static class ModularCostumeExtractor
    {
        private const string PartsFolder =
            "Assets/ThirdParty/Layer Lab/3D Casual Character/3D Characters Pro - Fantasy/Prefabs/Parts";
        private const string OutAsset  = "Assets/_Project/Data/Character/ModularCostumeCatalog.asset";
        private const string OutJson   = "Assets/_Project/Data/Character/ModularCostumeCatalog.json";

        // Held items we never swap — the player carries guns from the weapon pack instead.
        private static readonly string[] EXCLUDED = { "Wield_Gear_Left", "Wield_Gear_Right", "Wield_Gear" };

        // Order matters: longer / more-specific prefixes first (Eyewear before Eye, Body before none).
        private static readonly string[] SLOT_PREFIXES =
        {
            "Hair", "Beard", "Brow", "Mouth", "Eyewear", "Eye", "Earring",
            "Head", "Chest", "Legs", "Feet", "Hands", "Back", "Body",
        };

        private const string BASE_BODY_SLOT = "Body";

        [MenuItem("ZombieWar/Extract Modular Costume Catalog")]
        public static void Extract()
        {
            if (!AssetDatabase.IsValidFolder(PartsFolder))
            {
                Debug.LogError($"[CostumeExtractor] Parts folder not found: {PartsFolder}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { PartsFolder });
            var slots = new Dictionary<string, ModularCostumeCatalog.Slot>(System.StringComparer.OrdinalIgnoreCase);
            int excluded = 0, unresolved = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);

                if (EXCLUDED.Any(x => name.StartsWith(x, System.StringComparison.OrdinalIgnoreCase)))
                {
                    excluded++;
                    continue;
                }

                string slotName = ResolveSlot(name);
                if (slotName == null) { unresolved++; slotName = "Other"; }

                if (!slots.TryGetValue(slotName, out var slot))
                {
                    slot = new ModularCostumeCatalog.Slot
                    {
                        slot = slotName,
                        isBaseBody = slotName == BASE_BODY_SLOT,
                    };
                    slots[slotName] = slot;
                }

                slot.parts.Add(new ModularCostumeCatalog.PartEntry
                {
                    name = name,
                    assetPath = path,
                    guid = guid,
                });
            }

            // Deterministic ordering so the asset diffs cleanly.
            var ordered = slots.Values
                .OrderBy(s => System.Array.IndexOf(SLOT_PREFIXES, s.slot) is var i && i >= 0 ? i : int.MaxValue)
                .ThenBy(s => s.slot)
                .ToList();
            foreach (var s in ordered)
                s.parts = s.parts.OrderBy(p => p.name, new NaturalComparer()).ToList();

            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(OutAsset);
            bool isNew = catalog == null;
            if (isNew) catalog = ScriptableObject.CreateInstance<ModularCostumeCatalog>();

            catalog.sourceFolder = PartsFolder;
            catalog.excludedCategories = EXCLUDED.ToList();
            catalog.slots = ordered;

            Directory.CreateDirectory(Path.GetDirectoryName(OutAsset));
            if (isNew) AssetDatabase.CreateAsset(catalog, OutAsset);
            else EditorUtility.SetDirty(catalog);

            File.WriteAllText(OutJson, BuildJson(catalog, excluded, unresolved));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sb = new StringBuilder();
            sb.AppendLine($"[CostumeExtractor] Catalog baked: {catalog.TotalParts} parts across {ordered.Count} slots " +
                          $"(scanned {guids.Length}, excluded held items {excluded}, unresolved {unresolved}).");
            foreach (var s in ordered)
                sb.AppendLine($"  - {s.slot,-10} {s.parts.Count}{(s.isBaseBody ? "  (base body)" : "")}");
            Debug.Log(sb.ToString());
            Selection.activeObject = catalog;
        }

        private static string ResolveSlot(string name)
        {
            foreach (string prefix in SLOT_PREFIXES)
                if (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    return prefix;
            return null;
        }

        private static string BuildJson(ModularCostumeCatalog c, int excluded, int unresolved)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"sourceFolder\": \"{Esc(c.sourceFolder)}\",");
            sb.AppendLine($"  \"totalParts\": {c.TotalParts},");
            sb.AppendLine($"  \"excludedHeldItems\": {excluded},");
            sb.AppendLine($"  \"unresolved\": {unresolved},");
            sb.AppendLine($"  \"excludedCategories\": [{string.Join(", ", c.excludedCategories.Select(x => $"\"{Esc(x)}\""))}],");
            sb.AppendLine("  \"slots\": [");
            for (int i = 0; i < c.slots.Count; i++)
            {
                var s = c.slots[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"slot\": \"{Esc(s.slot)}\",");
                sb.AppendLine($"      \"isBaseBody\": {(s.isBaseBody ? "true" : "false")},");
                sb.AppendLine($"      \"count\": {s.parts.Count},");
                sb.AppendLine($"      \"parts\": [{string.Join(", ", s.parts.Select(p => $"\"{Esc(p.name)}\""))}]");
                sb.AppendLine("    }" + (i < c.slots.Count - 1 ? "," : ""));
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string Esc(string s) => (s ?? "").Replace("\\", "/").Replace("\"", "\\\"");

        /// Sorts Hair_Black_2 before Hair_Black_10 (numeric-aware).
        private class NaturalComparer : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                if (a == null || b == null) return string.CompareOrdinal(a, b);
                int ia = 0, ib = 0;
                while (ia < a.Length && ib < b.Length)
                {
                    if (char.IsDigit(a[ia]) && char.IsDigit(b[ib]))
                    {
                        int na = 0, nb = 0;
                        while (ia < a.Length && char.IsDigit(a[ia])) na = na * 10 + (a[ia++] - '0');
                        while (ib < b.Length && char.IsDigit(b[ib])) nb = nb * 10 + (b[ib++] - '0');
                        if (na != nb) return na - nb;
                    }
                    else
                    {
                        int cmp = a[ia].CompareTo(b[ib]);
                        if (cmp != 0) return cmp;
                        ia++; ib++;
                    }
                }
                return (a.Length - ia) - (b.Length - ib);
            }
        }
    }
}
#endif
