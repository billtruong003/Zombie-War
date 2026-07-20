using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// One-shot roster migration: moves every WeaponData off ThirdParty vendor prefabs onto a clean
    /// project-owned roster under Assets/_Project/Prefabs/Weapons, assigns stable weaponId/catalogOrder,
    /// and corrects 5 confirmed model↔identity mismatches found by visual audit (see Docs/WeaponRosterMapping.json
    /// for the before/after table and evidence).
    ///
    /// Audit Migration  = read-only, prints the plan table.
    /// Execute Migration = performs it. Idempotent — re-running produces NO_CHANGE for already-migrated entries.
    /// Validate Roster   = read-only, checks the 25-weapon contract described in the task brief.
    ///
    /// Each RosterEntry's "old asset" is resolved by WeaponId first (already migrated) then by its
    /// original filename (not yet migrated) — this is what makes re-runs safe regardless of file state.
    /// </summary>
    public static class WeaponRosterMigration
    {
        const string WeaponsDataDir = "Assets/_Project/Data/Weapons";
        const string WeaponsPrefabDir = "Assets/_Project/Prefabs/Weapons";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string UIPrototypeCatalogPath = "Assets/_Project/UI/Data/UIPrototypeCatalog.asset";
        const string ReportPath = "Docs/WeaponRosterMapping.json";
        const string ContactSheetPath = "Assets/Screenshots/weapon_roster_after_migration.png";

        enum Source { Vendor, ProjectExisting }
        enum Action { CopyVendor, MoveProject, NoChange }

        class RosterEntry
        {
            public int order;
            public string oldAssetName;   // WeaponData filename BEFORE migration (asset kept, GUID preserved)
            public string newAssetName;   // WeaponData filename AFTER migration (no .asset)
            public string weaponId;
            public string displayName;    // null = keep existing weaponName untouched
            public WeaponClass weaponClass;
            public bool twoHanded;
            public string newPrefabName;  // no .prefab
            public Source source;
            public string sourcePath;     // vendor prefab path OR current project prefab path
            public string correctionNote; // non-null = this entry deviates from a naive letter-for-letter guess
        }

        static List<RosterEntry> BuildRoster()
        {
            const string PistolPack = "Assets/ThirdParty/Low Poly Pistol Weapon Pack 1/Prefabs/Weapons";
            const string ShotgunPack = "Assets/ThirdParty/Low Poly ShotGun Weapon Pack 1/Prefabs/Weapons"; // also holds AR_*
            const string Vol1Pack = "Assets/ThirdParty/Low Poly Weapons VOL.1/Prefabs";

            return new List<RosterEntry>
            {
                E(0, "WD_Pistol", "WD_Sidearm_PistolA", "weapon.sidearm.pistol_a", null, WeaponClass.Sidearm, false, "WPN_Sidearm_PistolA", Source.Vendor, $"{PistolPack}/Pistol_A.prefab"),
                E(1, "WD_SMG", "WD_SMG_Generic", "weapon.smg.generic", null, WeaponClass.SMG, false, "WPN_SMG_Generic", Source.ProjectExisting, $"{WeaponsPrefabDir}/WP_SMG.prefab"),
                E(2, "WD_Rifle", "WD_AssaultRifle_Generic", "weapon.assault_rifle.generic", null, WeaponClass.AssaultRifle, true, "WPN_AssaultRifle_Generic", Source.ProjectExisting, $"{WeaponsPrefabDir}/WP_Rifle.prefab"),
                E(3, "WD_Shotgun", "WD_Shotgun_Generic", "weapon.shotgun.generic", null, WeaponClass.Shotgun, true, "WPN_Shotgun_Generic", Source.ProjectExisting, $"{WeaponsPrefabDir}/WP_Shotgun.prefab"),
                E(4, "WD_Sniper", "WD_Marksman_SniperGeneric", "weapon.marksman.sniper_generic", null, WeaponClass.Marksman, true, "WPN_Marksman_SniperGeneric", Source.ProjectExisting, $"{WeaponsPrefabDir}/WP_Sniper.prefab"),
                E(5, "WD_LMG", "WD_LMG_Generic", "weapon.lmg.generic", null, WeaponClass.LMG, true, "WPN_LMG_Generic", Source.ProjectExisting, $"{WeaponsPrefabDir}/WP_LMG.prefab"),

                E(6, "WD_Pistol_B", "WD_Sidearm_Glock19", "weapon.sidearm.glock_19", null, WeaponClass.Sidearm, false, "WPN_Sidearm_Glock19", Source.Vendor, $"{PistolPack}/Pistol_B.prefab"),
                E(7, "WD_Pistol_C", "WD_Sidearm_P226", "weapon.sidearm.p226", null, WeaponClass.Sidearm, false, "WPN_Sidearm_P226", Source.Vendor, $"{PistolPack}/Pistol_C.prefab"),
                E(8, "WD_Pistol_D", "WD_Sidearm_M1911", "weapon.sidearm.m1911", null, WeaponClass.Sidearm, false, "WPN_Sidearm_M1911", Source.Vendor, $"{PistolPack}/Pistol_D.prefab"),
                E(9, "WD_Pistol_E", "WD_Sidearm_BerettaM9", "weapon.sidearm.beretta_m9", null, WeaponClass.Sidearm, false, "WPN_Sidearm_BerettaM9", Source.Vendor, $"{PistolPack}/Pistol_E.prefab"),
                E(10, "WD_Pistol_F", "WD_Sidearm_USP45", "weapon.sidearm.usp_45", null, WeaponClass.Sidearm, false, "WPN_Sidearm_USP45", Source.Vendor, $"{PistolPack}/Pistol_F.prefab"),
                E(11, "WD_Pistol_G", "WD_Sidearm_DesertEagle", "weapon.sidearm.desert_eagle", null, WeaponClass.Sidearm, false, "WPN_Sidearm_DesertEagle", Source.Vendor, $"{PistolPack}/Pistol_G.prefab"),
                E(12, "WD_Pistol_H", "WD_Sidearm_FiveSeven", "weapon.sidearm.five_seven", null, WeaponClass.Sidearm, false, "WPN_Sidearm_FiveSeven", Source.Vendor, $"{PistolPack}/Pistol_H.prefab"),
                E(13, "WD_Pistol_I", "WD_Sidearm_Makarov", "weapon.sidearm.makarov", null, WeaponClass.Sidearm, false, "WPN_Sidearm_Makarov", Source.Vendor, $"{PistolPack}/Pistol_I.prefab"),
                E(14, "WD_Pistol_J", "WD_Sidearm_Python357", "weapon.sidearm.python_357", null, WeaponClass.Sidearm, false, "WPN_Sidearm_Python357", Source.Vendor, $"{PistolPack}/Pistol_J.prefab"),

                // ---- Shotgun family: 3 of 5 vendor letters were confirmed CORRECT, 2 were wrong model,
                // and vendor "ShotGun_D" turned out to be an AK-pattern rifle mesh (not a shotgun at all —
                // excluded from the roster entirely). See Docs/WeaponRosterMapping.json §evidence.
                E(15, "WD_ShotGun_A", "WD_Shotgun_BenelliM4", "weapon.shotgun.benelli_m4", "Benelli M4", WeaponClass.Shotgun, true, "WPN_Shotgun_BenelliM4", Source.Vendor, $"{Vol1Pack}/Bennelli_M4.prefab",
                    "Was 'Remington 870' -> vendor ShotGun_A.prefab. Visual audit: ShotGun_A is actually a box-mag/rail combat shotgun (AA-12 identity, see order 19). No vendor asset matches Remington 870; ShotGun_D (only remaining letter) is an AK-pattern RIFLE mesh, not a shotgun. Substituted the closest verified real shotgun asset in the project (Bennelli_M4, Low Poly Weapons VOL.1) and renamed the identity accordingly rather than mislabeling the wrong mesh."),
                E(16, "WD_ShotGun_B", "WD_Shotgun_Mossberg500", "weapon.shotgun.mossberg_500", null, WeaponClass.Shotgun, true, "WPN_Shotgun_Mossberg500", Source.Vendor, $"{ShotgunPack}/ShotGun_B.prefab"),
                E(17, "WD_ShotGun_C", "WD_Shotgun_SPAS12", "weapon.shotgun.spas_12", null, WeaponClass.Shotgun, true, "WPN_Shotgun_SPAS12", Source.Vendor, $"{ShotgunPack}/ShotGun_C.prefab"),
                E(18, "WD_ShotGun_D", "WD_Shotgun_DoubleBarrel", "weapon.shotgun.double_barrel", null, WeaponClass.Shotgun, true, "WPN_Shotgun_DoubleBarrel", Source.Vendor, $"{ShotgunPack}/ShotGun_E.prefab",
                    "weaponName 'Double Barrel' was already correct text, but its prefab (vendor ShotGun_D.prefab) is visually an AK-pattern rifle. Repointed to vendor ShotGun_E.prefab, which is visually a genuine wood over/under double-barrel shotgun (two parallel barrels, break-action styling)."),
                E(19, "WD_ShotGun_E", "WD_Shotgun_AA12", "weapon.shotgun.aa_12", null, WeaponClass.Shotgun, true, "WPN_Shotgun_AA12", Source.Vendor, $"{ShotgunPack}/ShotGun_A.prefab",
                    "weaponName 'AA-12' was already correct text, but its prefab (vendor ShotGun_E.prefab) is visually the double-barrel model (see order 18). Repointed to vendor ShotGun_A.prefab, which is visually a box-magazine/top-rail/barrel-shroud combat shotgun matching AA-12."),

                E(20, "WD_AR_A_1", "WD_AssaultRifle_M4A1", "weapon.assault_rifle.m4a1", null, WeaponClass.AssaultRifle, true, "WPN_AssaultRifle_M4A1", Source.Vendor, $"{ShotgunPack}/AR_A_1.prefab"),
                E(21, "WD_AR_B", "WD_AssaultRifle_AK47", "weapon.assault_rifle.ak_47", null, WeaponClass.AssaultRifle, true, "WPN_AssaultRifle_AK47", Source.Vendor, $"{ShotgunPack}/AR_B.prefab"),
                E(22, "WD_AR_C", "WD_AssaultRifle_SCARL", "weapon.assault_rifle.scar_l", null, WeaponClass.AssaultRifle, true, "WPN_AssaultRifle_SCARL", Source.Vendor, $"{ShotgunPack}/AR_C.prefab"),
                E(23, "WD_AR_D", "WD_AssaultRifle_FAMAS", "weapon.assault_rifle.famas", null, WeaponClass.AssaultRifle, true, "WPN_AssaultRifle_FAMAS", Source.Vendor, $"{ShotgunPack}/AR_E.prefab",
                    "weaponName 'FAMAS' was already correct text, but its prefab (vendor AR_D.prefab) is visually a G36/G36C (top rail + integrated carry-handle scope, standard-length receiver with separate telescoping stock). Repointed to vendor AR_E.prefab, which is visually a genuine FAMAS bullpup (stock immediately behind grip, dominant sight-tunnel spine)."),
                E(24, "WD_AR_E", "WD_AssaultRifle_G36C", "weapon.assault_rifle.g36c", null, WeaponClass.AssaultRifle, true, "WPN_AssaultRifle_G36C", Source.Vendor, $"{ShotgunPack}/AR_D.prefab",
                    "weaponName 'G36C' was already correct text, but its prefab (vendor AR_E.prefab) is visually the FAMAS bullpup (see order 23). Repointed to vendor AR_D.prefab, which is visually a genuine G36/G36C (carry-handle scope hump, standard-length receiver, separate stock)."),
            };
        }

        static RosterEntry E(int order, string oldAssetName, string newAssetName, string weaponId,
            string displayName, WeaponClass weaponClass, bool twoHanded, string newPrefabName,
            Source source, string sourcePath, string correctionNote = null) => new RosterEntry
        {
            order = order, oldAssetName = oldAssetName, newAssetName = newAssetName, weaponId = weaponId,
            displayName = displayName, weaponClass = weaponClass, twoHanded = twoHanded,
            newPrefabName = newPrefabName, source = source, sourcePath = sourcePath, correctionNote = correctionNote
        };

        // ================================================================ AUDIT (read-only)

        [MenuItem("ZombieWar/Weapons/Roster/Audit Migration")]
        public static void Audit()
        {
            var roster = BuildRoster();
            var report = new StringBuilder();
            report.AppendLine("=== Weapon Roster Migration — AUDIT (read-only) ===");
            report.AppendLine($"{"Ord",3} {"OldAsset",-16} {"WeaponName",-16} {"WeaponId",-32} {"Class",-12} {"2H",-3} {"CurPrefab",-40} {"NewPrefab",-32} {"Action",-12} {"Status"}");

            int errors = 0;
            foreach (var e in roster)
            {
                var wd = FindExistingWeaponData(e);
                string weaponName = wd != null ? wd.weaponName : "<missing>";
                var (action, status, isError) = PlanAction(e, wd);
                if (isError) errors++;
                string curPrefabPath = wd != null && wd.weaponPrefab != null ? AssetDatabase.GetAssetPath(wd.weaponPrefab) : "<none>";
                report.AppendLine($"{e.order,3} {e.oldAssetName,-16} {weaponName,-16} {e.weaponId,-32} {e.weaponClass,-12} {(e.twoHanded ? "Y" : "N"),-3} {curPrefabPath,-40} {e.newPrefabName,-32} {action,-12} {status}");
                if (e.correctionNote != null) report.AppendLine($"     CORRECTION: {e.correctionNote}");
            }

            var collisionErrors = CheckCollisions(roster);
            foreach (var c in collisionErrors) { report.AppendLine("COLLISION: " + c); errors++; }

            report.AppendLine($"\n{roster.Count} entries planned. {errors} blocking error(s).");
            Debug.Log(report.ToString());
        }

        // ================================================================ EXECUTE

        [MenuItem("ZombieWar/Weapons/Roster/Execute Migration")]
        public static void Execute()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[RosterMigration] Refusing to run in Play Mode.");
                return;
            }

            var roster = BuildRoster();
            var collisionErrors = CheckCollisions(roster);
            if (collisionErrors.Count > 0)
            {
                Debug.LogError("[RosterMigration] Aborting — collisions found:\n" + string.Join("\n", collisionErrors));
                return;
            }

            // Pre-flight: every entry must resolve to an existing WeaponData and (for not-yet-migrated
            // entries) an existing source prefab, and no target must collide with an unrelated asset.
            var preflightErrors = new List<string>();
            foreach (var e in roster)
            {
                var wd = FindExistingWeaponData(e);
                if (wd == null) preflightErrors.Add($"order {e.order}: no WeaponData found (tried weaponId '{e.weaponId}' and old name '{e.oldAssetName}').");

                bool alreadyMigrated = wd != null && wd.WeaponId == e.weaponId
                    && wd.weaponPrefab != null && AssetDatabase.GetAssetPath(wd.weaponPrefab) == $"{WeaponsPrefabDir}/{e.newPrefabName}.prefab";
                if (!alreadyMigrated)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(e.sourcePath) == null)
                        preflightErrors.Add($"order {e.order}: source prefab missing at '{e.sourcePath}'.");
                }

                string targetPrefabPath = $"{WeaponsPrefabDir}/{e.newPrefabName}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath) != null && e.source == Source.Vendor && !alreadyMigrated)
                {
                    // target already exists but doesn't match this entry's expected source -> would overwrite unrelated asset
                    preflightErrors.Add($"order {e.order}: target prefab '{targetPrefabPath}' already exists and is not this entry's own migrated copy.");
                }
            }
            if (preflightErrors.Count > 0)
            {
                Debug.LogError("[RosterMigration] Aborting — preflight failed:\n" + string.Join("\n", preflightErrors));
                return;
            }

            if (!EditorUtility.DisplayDialog("Execute Weapon Roster Migration",
                    $"This will rename/move {roster.Count} WeaponData assets, copy {roster.Count(r => r.source == Source.Vendor)} vendor prefabs into " +
                    $"{WeaponsPrefabDir}, and move {roster.Count(r => r.source == Source.ProjectExisting)} existing project prefabs.\n\n" +
                    "ThirdParty source assets are never modified. Already-migrated entries are skipped (NO_CHANGE).\n\nContinue?",
                    "Execute", "Cancel"))
            {
                Debug.Log("[RosterMigration] Cancelled by user.");
                return;
            }

            Directory.CreateDirectory(WeaponsPrefabDir);
            var log = new StringBuilder();
            int copied = 0, moved = 0, noChange = 0;

            try
            {
                EditorUtility.DisplayProgressBar("Weapon Roster Migration", "Starting…", 0f);

                foreach (var e in roster)
                {
                    EditorUtility.DisplayProgressBar("Weapon Roster Migration", $"order {e.order}: {e.newAssetName}", e.order / (float)roster.Count);

                    var wd = FindExistingWeaponData(e);
                    string targetPrefabPath = $"{WeaponsPrefabDir}/{e.newPrefabName}.prefab";
                    string targetDataPath = $"{WeaponsDataDir}/{e.newAssetName}.asset";

                    GameObject targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath);
                    if (targetPrefab != null)
                    {
                        noChange++;
                        log.AppendLine($"order {e.order}: prefab NO_CHANGE ({targetPrefabPath})");
                    }
                    else if (e.source == Source.Vendor)
                    {
                        if (!AssetDatabase.CopyAsset(e.sourcePath, targetPrefabPath))
                        {
                            Debug.LogError($"[RosterMigration] CopyAsset failed for order {e.order}: {e.sourcePath} -> {targetPrefabPath}");
                            continue;
                        }
                        RenamePrefabRoot(targetPrefabPath, e.newPrefabName);
                        copied++;
                        log.AppendLine($"order {e.order}: COPY_VENDOR {e.sourcePath} -> {targetPrefabPath}");
                    }
                    else // ProjectExisting: move+rename, preserves GUID
                    {
                        string moveErr = AssetDatabase.MoveAsset(e.sourcePath, targetPrefabPath);
                        if (!string.IsNullOrEmpty(moveErr))
                        {
                            Debug.LogError($"[RosterMigration] MoveAsset failed for order {e.order}: {moveErr}");
                            continue;
                        }
                        RenamePrefabRoot(targetPrefabPath, e.newPrefabName);
                        moved++;
                        log.AppendLine($"order {e.order}: MOVE_PROJECT {e.sourcePath} -> {targetPrefabPath}");
                    }

                    // ---- WeaponData: rename (MoveAsset preserves GUID) + edit fields ----
                    // Legacy alias = e.oldAssetName from the static roster table, NOT wd.name — on a
                    // re-run after the asset is already renamed, wd.name is already the NEW name, so
                    // reading it here would silently lose the original identity old saves depend on.
                    string oldName = e.oldAssetName;
                    string currentDataPath = AssetDatabase.GetAssetPath(wd);
                    if (currentDataPath != targetDataPath)
                    {
                        string moveErr = AssetDatabase.MoveAsset(currentDataPath, targetDataPath);
                        if (!string.IsNullOrEmpty(moveErr))
                            Debug.LogError($"[RosterMigration] WeaponData MoveAsset failed for order {e.order}: {moveErr}");
                    }

                    var newPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath);
                    var so = new SerializedObject(wd);
                    so.FindProperty("weaponId").stringValue = e.weaponId;
                    so.FindProperty("catalogOrder").intValue = e.order;
                    var aliasProp = so.FindProperty("legacyAliases");
                    bool hasAlias = false;
                    for (int i = 0; i < aliasProp.arraySize; i++)
                        if (aliasProp.GetArrayElementAtIndex(i).stringValue == oldName) { hasAlias = true; break; }
                    if (!hasAlias && oldName != e.newAssetName)
                    {
                        aliasProp.InsertArrayElementAtIndex(aliasProp.arraySize);
                        aliasProp.GetArrayElementAtIndex(aliasProp.arraySize - 1).stringValue = oldName;
                    }
                    var classProp = so.FindProperty("weaponClass");
                    classProp.enumValueIndex = (int)e.weaponClass;
                    so.FindProperty("twoHanded").boolValue = e.twoHanded;
                    var prefabProp = so.FindProperty("weaponPrefab");
                    prefabProp.objectReferenceValue = newPrefabAsset;
                    if (e.displayName != null)
                        so.FindProperty("weaponName").stringValue = e.displayName;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(wd);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                NormalizeUIPrototypeCatalogOrder(roster);
                WriteMappingReport(roster);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[RosterMigration] DONE — copied={copied} moved={moved} noChange={noChange}\n{log}");
        }

        static void RenamePrefabRoot(string prefabPath, string newRootName)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.name == newRootName) return;
                root.name = newRootName;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// UIPrototypeCatalog.weapons entries reference WeaponData by GUID (survives rename/move
        /// untouched) but were originally appended in whatever order UIThumbnailGenerator's OLD
        /// (weaponClass/tier/name) sort produced — scrambled relative to catalogOrder. Reorder in
        /// place to match catalogOrder 0..N so "UIPrototypeCatalog order matches catalogOrder" holds.
        /// </summary>
        static void NormalizeUIPrototypeCatalogOrder(List<RosterEntry> roster)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ZombieWar.UI.UIPrototypeCatalog>(UIPrototypeCatalogPath);
            if (catalog == null) { Debug.LogWarning("[RosterMigration] UIPrototypeCatalog not found — skip reorder."); return; }

            var so = new SerializedObject(catalog);
            var weaponsProp = so.FindProperty("weapons");

            var byGuid = new Dictionary<string, int>(); // WeaponData guid -> current index in list
            for (int i = 0; i < weaponsProp.arraySize; i++)
            {
                var data = weaponsProp.GetArrayElementAtIndex(i).FindPropertyRelative("data").objectReferenceValue;
                if (data == null) continue;
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
                byGuid[guid] = i;
            }

            // Desired order = catalogOrder ascending. Missing entries (data new to the catalog) are
            // appended at the end via UIThumbnailGenerator elsewhere — here we only reorder what exists.
            var desiredGuidOrder = roster
                .Select(e => AssetDatabase.AssetPathToGUID($"{WeaponsDataDir}/{e.newAssetName}.asset"))
                .Where(g => byGuid.ContainsKey(g))
                .ToList();

            for (int target = 0; target < desiredGuidOrder.Count; target++)
            {
                string guid = desiredGuidOrder[target];
                int current = -1;
                for (int i = target; i < weaponsProp.arraySize; i++)
                {
                    var data = weaponsProp.GetArrayElementAtIndex(i).FindPropertyRelative("data").objectReferenceValue;
                    if (data != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data)) == guid) { current = i; break; }
                }
                if (current < 0 || current == target) continue;
                weaponsProp.MoveArrayElement(current, target);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            Debug.Log("[RosterMigration] UIPrototypeCatalog.weapons reordered to catalogOrder.");
        }

        static void WriteMappingReport(List<RosterEntry> roster)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"roster\": [");
            for (int i = 0; i < roster.Count; i++)
            {
                var e = roster[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"catalogOrder\": {e.order},");
                sb.AppendLine($"      \"weaponId\": \"{e.weaponId}\",");
                sb.AppendLine($"      \"weaponDataAsset\": \"{WeaponsDataDir}/{e.newAssetName}.asset\",");
                sb.AppendLine($"      \"prefab\": \"{WeaponsPrefabDir}/{e.newPrefabName}.prefab\",");
                sb.AppendLine($"      \"weaponClass\": \"{e.weaponClass}\",");
                sb.AppendLine($"      \"twoHanded\": {(e.twoHanded ? "true" : "false")},");
                sb.AppendLine($"      \"sourceKind\": \"{e.source}\",");
                sb.AppendLine($"      \"sourcePath\": \"{e.sourcePath.Replace("\\", "/")}\",");
                sb.AppendLine($"      \"oldAssetName\": \"{e.oldAssetName}\",");
                sb.AppendLine($"      \"correctionNote\": {(e.correctionNote != null ? "\"" + e.correctionNote.Replace("\"", "'") + "\"" : "null")}");
                sb.AppendLine(i == roster.Count - 1 ? "    }" : "    },");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
            File.WriteAllText(ReportPath, sb.ToString());
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log("[RosterMigration] Mapping report written: " + ReportPath);
        }

        // ================================================================ helpers (shared audit/execute)

        static (string action, string status, bool isError) PlanAction(RosterEntry e, WeaponData wd)
        {
            if (wd == null) return ("ERROR", "WeaponData not found", true);
            string targetPrefabPath = $"{WeaponsPrefabDir}/{e.newPrefabName}.prefab";
            bool alreadyMigrated = wd.WeaponId == e.weaponId
                && wd.weaponPrefab != null && AssetDatabase.GetAssetPath(wd.weaponPrefab) == targetPrefabPath;
            if (alreadyMigrated) return ("NO_CHANGE", "OK", false);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath) != null)
                return (e.source == Source.Vendor ? "COPY_VENDOR" : "MOVE_PROJECT", "TARGET EXISTS (foreign) — will error on execute", true);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(e.sourcePath) == null)
                return (e.source == Source.Vendor ? "COPY_VENDOR" : "MOVE_PROJECT", "SOURCE MISSING", true);
            return (e.source == Source.Vendor ? "COPY_VENDOR" : "MOVE_PROJECT", "OK", false);
        }

        static List<string> CheckCollisions(List<RosterEntry> roster)
        {
            var errors = new List<string>();
            foreach (var g in roster.GroupBy(r => r.weaponId))
                if (g.Count() > 1) errors.Add($"duplicate weaponId '{g.Key}' at orders {string.Join(",", g.Select(r => r.order))}");
            foreach (var g in roster.GroupBy(r => r.order))
                if (g.Count() > 1) errors.Add($"duplicate catalogOrder {g.Key}");
            foreach (var g in roster.GroupBy(r => r.newAssetName))
                if (g.Count() > 1) errors.Add($"duplicate target WeaponData filename '{g.Key}'");
            foreach (var g in roster.GroupBy(r => r.newPrefabName))
                if (g.Count() > 1) errors.Add($"duplicate target prefab filename '{g.Key}'");
            return errors;
        }

        /// Resolve the WeaponData for a roster slot: prefer weaponId (already migrated), else fall
        /// back to the original filename (not yet migrated). Makes Audit/Execute idempotent.
        static WeaponData FindExistingWeaponData(RosterEntry e)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponsDataDir }))
            {
                var wd = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (wd != null && !string.IsNullOrEmpty(wd.WeaponId) && wd.WeaponId == e.weaponId) return wd;
            }
            return AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponsDataDir}/{e.oldAssetName}.asset");
        }

        // ================================================================ VALIDATE (read-only)

        [MenuItem("ZombieWar/Weapons/Roster/Validate Roster")]
        public static void Validate()
        {
            var report = new StringBuilder();
            int fails = 0;
            void Fail(string m) { report.AppendLine("  ✗ " + m); fails++; }
            void Ok(string m) => report.AppendLine("  ✓ " + m);

            var all = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponsDataDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null).ToList();

            if (all.Count == 25) Ok($"25 WeaponData assets ({all.Count})"); else Fail($"expected 25 WeaponData assets, found {all.Count}");

            var ids = all.Select(w => w.WeaponId).ToList();
            if (ids.Distinct().Count() == ids.Count && ids.All(i => !string.IsNullOrEmpty(i))) Ok("all weaponId non-empty and unique");
            else Fail($"weaponId not all unique/non-empty ({ids.Count(string.IsNullOrEmpty)} empty, {ids.Count - ids.Distinct().Count()} dupes)");

            var orders = all.Select(w => w.CatalogOrder).OrderBy(x => x).ToList();
            bool ordersOk = orders.Count == 25 && orders.Distinct().Count() == 25 && orders.First() == 0 && orders.Last() == 24;
            if (ordersOk) Ok("catalogOrder covers 0-24 with no gaps/dupes"); else Fail($"catalogOrder invalid: [{string.Join(",", orders)}]");

            foreach (var w in all)
            {
                if (w.weaponPrefab == null) { Fail($"{w.name}: weaponPrefab is null"); continue; }
                string path = AssetDatabase.GetAssetPath(w.weaponPrefab);
                if (!path.StartsWith(WeaponsPrefabDir + "/"))
                    Fail($"{w.name}: prefab path '{path}' not under {WeaponsPrefabDir}/");
                if (path.StartsWith("Assets/ThirdParty"))
                    Fail($"{w.name}: still references a ThirdParty prefab directly ({path})");
                string expectedRoot = Path.GetFileNameWithoutExtension(path);
                if (w.weaponPrefab.name != expectedRoot)
                    Fail($"{w.name}: prefab root GO name '{w.weaponPrefab.name}' != filename '{expectedRoot}'");
                var comp = PrefabUtility.LoadPrefabContents(path);
                bool missing = comp.GetComponentsInChildren<Component>(true).Any(c => c == null);
                PrefabUtility.UnloadPrefabContents(comp);
                if (missing) Fail($"{w.name}: prefab '{path}' has a missing script");

                if (w.weaponClass == WeaponClass.Sidearm && w.twoHanded) Fail($"{w.name}: Sidearm must be one-handed (twoHanded=false)");
                if (w.weaponClass != WeaponClass.Sidearm && w.weaponClass != WeaponClass.SMG && !w.twoHanded)
                    Fail($"{w.name}: non-sidearm/SMG long gun must be twoHanded=true");
            }
            if (fails == 0) Ok("all prefab references / naming / handedness checks passed (see above dupes if any)");

            ValidateWeaponsList(PlayerPrefabPath, "Player.prefab arsenal", all, report, ref fails);
            ValidateUIPrototypeCatalog(all, report, ref fails);

            if (fails == 0) Debug.Log("[RosterMigration] VALIDATE PASS — roster contract OK.\n" + report);
            else Debug.LogError($"[RosterMigration] VALIDATE FAIL — {fails} issue(s):\n" + report);
        }

        static void ValidateWeaponsList(string prefabPath, string label, List<WeaponData> all, StringBuilder report, ref int fails)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (go == null) { report.AppendLine($"  ✗ {label}: prefab not found at {prefabPath}"); fails++; return; }
            var weaponComp = go.GetComponentInChildren<Weapon>(true);
            if (weaponComp == null) { report.AppendLine($"  ✗ {label}: no Weapon component"); fails++; return; }
            var so = new SerializedObject(weaponComp);
            var prop = so.FindProperty("weapons");
            var list = new List<WeaponData>();
            for (int i = 0; i < prop.arraySize; i++)
                list.Add(prop.GetArrayElementAtIndex(i).objectReferenceValue as WeaponData);

            if (list.Count == 25 && list.Distinct().Count() == 25 && !list.Contains(null))
                report.AppendLine($"  ✓ {label}: exactly 25 unique entries");
            else { report.AppendLine($"  ✗ {label}: {list.Count} entries ({list.Distinct().Count()} unique, {list.Count(x => x == null)} null)"); fails++; }

            bool orderOk = true;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == null || list[i].CatalogOrder != i) { orderOk = false; break; }
            if (orderOk) report.AppendLine($"  ✓ {label}: list order matches catalogOrder");
            else { report.AppendLine($"  ✗ {label}: list order does NOT match catalogOrder"); fails++; }
        }

        static void ValidateUIPrototypeCatalog(List<WeaponData> all, StringBuilder report, ref int fails)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ZombieWar.UI.UIPrototypeCatalog>(UIPrototypeCatalogPath);
            if (catalog == null) { report.AppendLine("  ✗ UIPrototypeCatalog: asset not found"); fails++; return; }
            var list = catalog.weapons.Select(w => w.data).ToList();
            if (list.Count == 25 && list.Distinct().Count() == 25 && !list.Contains(null))
                report.AppendLine("  ✓ UIPrototypeCatalog: exactly 25 unique entries");
            else { report.AppendLine($"  ✗ UIPrototypeCatalog: {list.Count} entries ({list.Distinct().Count()} unique, {list.Count(x => x == null)} null)"); fails++; }

            bool orderOk = true;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == null || list[i].CatalogOrder != i) { orderOk = false; break; }
            if (orderOk) report.AppendLine("  ✓ UIPrototypeCatalog: order matches catalogOrder");
            else { report.AppendLine("  ✗ UIPrototypeCatalog: order does NOT match catalogOrder"); fails++; }
        }

        // ================================================================ CONTACT SHEET

        [MenuItem("ZombieWar/Weapons/Roster/Generate Referenced Contact Sheet")]
        public static void GenerateReferencedContactSheet()
        {
            var all = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponsDataDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null && w.weaponPrefab != null)
                .OrderBy(w => w.CatalogOrder)
                .ToList();

            if (all.Count == 0) { Debug.LogError("[RosterMigration] No WeaponData with a prefab found."); return; }

            GameObject root = null; Camera cam = null; RenderTexture rt = null; Texture2D outTex = null;
            const float Y = 6000f;
            const int cols = 5;
            const float spacing = 3.6f;
            const float target = 0.85f;
            int rows = Mathf.CeilToInt(all.Count / (float)cols);

            try
            {
                root = new GameObject("___ROSTER_SHEET___");
                for (int i = 0; i < all.Count; i++)
                {
                    var wd = all[i];
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(wd.weaponPrefab, root.transform);
                    int col = i % cols, row = i / cols;
                    var cellPos = new Vector3(col * spacing, Y, -row * spacing);

                    var b = Encapsulate(inst);
                    float ext = Mathf.Max(0.001f, Mathf.Max(b.extents.x, b.extents.y, b.extents.z));
                    inst.transform.localScale = Vector3.one * (target / ext);
                    // side-profile: long axis is usually local Z -> face the grid camera along +X-ish
                    inst.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    b = Encapsulate(inst);
                    inst.transform.position += cellPos - b.center;

                    var lab = new GameObject("lbl");
                    lab.transform.SetParent(root.transform);
                    lab.transform.position = cellPos + new Vector3(0, 0, 1.55f);
                    var tmp = lab.AddComponent<TMPro.TextMeshPro>();
                    tmp.text = $"<b>{wd.CatalogOrder:00}</b>  {wd.weaponName}\n{wd.WeaponId}\n{wd.weaponPrefab.name}";
                    tmp.fontSize = 2.6f;
                    tmp.lineSpacing = -25f;
                    tmp.alignment = TMPro.TextAlignmentOptions.Top;
                    tmp.color = Color.white;
                    tmp.rectTransform.sizeDelta = new Vector2(spacing * 0.98f, 1.6f);
                    tmp.enableWordWrapping = true;
                }

                MakeLight(root, Quaternion.Euler(90, 0, 0), 1.25f);
                MakeLight(root, Quaternion.Euler(45, 180, 0), 0.5f);

                var cg = new GameObject("cam"); cg.transform.SetParent(root.transform);
                cam = cg.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.13f, 0.14f, 0.17f);
                cam.orthographic = true;
                var gridCenter = new Vector3((cols - 1) * spacing / 2f, Y, -(rows - 1) * spacing / 2f);
                cam.transform.position = gridCenter + new Vector3(0, 120, 0);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                cam.orthographicSize = rows * spacing / 2f * 1.28f;
                cam.nearClipPlane = 0.01f; cam.farClipPlane = 10000f;

                foreach (var t in root.GetComponentsInChildren<TMPro.TextMeshPro>())
                    t.transform.rotation = cam.transform.rotation;

                int W = cols * 420, H = rows * 480;
                rt = new RenderTexture(W, H, 24);
                cam.targetTexture = rt;
                var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req))
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();

                RenderTexture.active = rt;
                outTex = new Texture2D(W, H, TextureFormat.RGB24, false);
                outTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                outTex.Apply();
                RenderTexture.active = null;

                Directory.CreateDirectory(Path.GetDirectoryName(ContactSheetPath)!);
                File.WriteAllBytes(ContactSheetPath, outTex.EncodeToPNG());
                Debug.Log($"[RosterMigration] Contact sheet ({all.Count} weapons, {cols}x{rows}) -> {ContactSheetPath}");
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (outTex != null) UnityEngine.Object.DestroyImmediate(outTex);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.Refresh();
        }

        static Bounds Encapsulate(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        static void MakeLight(GameObject parent, Quaternion rot, float intensity)
        {
            var lg = new GameObject("lgt"); lg.transform.SetParent(parent.transform);
            var lt = lg.AddComponent<Light>();
            lt.type = LightType.Directional; lt.intensity = intensity;
            lg.transform.rotation = rot;
        }
    }
}
