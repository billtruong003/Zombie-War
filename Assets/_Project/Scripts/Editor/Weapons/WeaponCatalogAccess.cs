using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// M7.1 A5 — the one way editor tooling is allowed to enumerate the arsenal.
    ///
    /// Every consumer used to run its own <c>FindAssets("t:WeaponData")</c> folder scan. That is what
    /// made "add weapon 26" cost a consumer edit: each scan silently picked up the new asset with its
    /// own assumptions, and nothing checked that the catalog, the folder and the audio tables agreed.
    ///
    /// This accessor reads the catalog and <b>fails loudly</b> when the folder and the catalog
    /// disagree, so a weapon added without a catalog entry breaks the build instead of shipping
    /// half-registered and silently losing its audio at runtime.
    /// </summary>
    public static class WeaponCatalogAccess
    {
        public const string CatalogPath = "Assets/_Project/Resources/WeaponCatalog.asset";
        public const string DataDir = "Assets/_Project/Data/Weapons";

        public static WeaponCatalog LoadCatalog() =>
            AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);

        /// <summary>Every WeaponData asset on disk, regardless of catalog state.</summary>
        public static List<WeaponData> ScanFolder() =>
            AssetDatabase.FindAssets("t:WeaponData", new[] { DataDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null)
                .OrderBy(w => w.CatalogOrder)
                .ToList();

        /// <summary>
        /// The arsenal, from the catalog, verified against the folder. Throws when they disagree —
        /// this is the check that turns "weapon 55 was added without a catalog entry" from a silent
        /// runtime defect into a build failure.
        /// </summary>
        public static List<WeaponData> AllWeaponsForBuild()
        {
            var catalog = LoadCatalog();
            if (catalog == null)
                throw new InvalidDataException(
                    $"WeaponCatalog is missing at {CatalogPath}. Run ZombieWar/Weapons/Factory/Build Weapon Catalog.");

            var problems = Reconcile(catalog);
            if (problems.Count > 0)
                throw new InvalidDataException(
                    "WeaponCatalog and the weapon folder disagree — every weapon must have exactly one " +
                    "catalog entry:\n  " + string.Join("\n  ", problems));

            return catalog.AllData();
        }

        /// <summary>
        /// Non-throwing form. Returns a human-readable list of disagreements between the catalog and
        /// the folder; empty means they match.
        /// </summary>
        public static List<string> Reconcile(WeaponCatalog catalog)
        {
            var problems = new List<string>();
            if (catalog == null) { problems.Add("catalog asset is missing"); return problems; }

            var onDisk = ScanFolder();
            var byId = new Dictionary<string, WeaponData>();
            foreach (var w in onDisk)
            {
                if (string.IsNullOrEmpty(w.WeaponId)) { problems.Add($"{w.name}: empty weaponId"); continue; }
                if (byId.ContainsKey(w.WeaponId)) { problems.Add($"{w.name}: duplicate weaponId '{w.WeaponId}'"); continue; }
                byId[w.WeaponId] = w;
            }

            foreach (var w in byId.Values)
                if (catalog.ById(w.WeaponId) == null)
                    problems.Add($"'{w.name}' ({w.WeaponId}) exists on disk but has NO catalog entry — " +
                                 "add one entry and nothing else");

            foreach (var e in catalog.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.weaponId)) continue;
                if (!byId.ContainsKey(e.weaponId))
                    problems.Add($"catalog entry '{e.weaponId}' has no WeaponData on disk");
            }

            problems.AddRange(catalog.Validate());
            return problems;
        }

        /// <summary>
        /// Weapons that must carry working audio. Non-playable weapons are excluded on purpose: a
        /// body onboarded as data with no authored anchors cannot fire, so demanding an audio cue
        /// for it would block the build on a weapon nobody can shoot.
        /// </summary>
        public static List<WeaponData> WeaponsRequiringAudio() =>
            AllWeaponsForBuild().Where(w => w != null && w.IsPlayable).ToList();

        [MenuItem("ZombieWar/Weapons/Factory/Verify Catalog Covers The Arsenal")]
        public static void Verify()
        {
            var problems = Reconcile(LoadCatalog());
            if (problems.Count == 0)
                Debug.Log($"[Catalog] OK — {ScanFolder().Count} weapons, all present in the catalog exactly once.");
            else
                Debug.LogError("[Catalog] " + problems.Count + " problem(s):\n  " + string.Join("\n  ", problems));
        }
    }
}
