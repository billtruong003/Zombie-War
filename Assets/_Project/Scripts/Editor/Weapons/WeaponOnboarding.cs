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
    /// M7.1 — onboards vendor weapon bodies into project ownership.
    ///
    /// What it does: copies the prefab, copies and converts its materials (the vendor originals are
    /// NEVER edited — they are duplicated and the copy is converted), creates a `WeaponData` with a
    /// stable invented `weaponId`, and registers a catalog entry.
    ///
    /// What it deliberately does NOT do: place a grip, a muzzle or a hand transform. Those are
    /// hand-authored by the owner, permanently. Every weapon onboarded here lands as
    /// <see cref="WeaponData.AuthoringStatus.PendingOwnerAuthoring"/> and is refused by
    /// `Weapon.EquipToSlot`, `LoadoutState.TryEquip` and `WeaponCatalog.DisplayEntries` until the
    /// owner has authored it.
    /// </summary>
    public static class WeaponOnboarding
    {
        const string PrefabDir = "Assets/_Project/Prefabs/Weapons";
        const string DataDir = "Assets/_Project/Data/Weapons";
        const string MatDir = "Assets/_Project/Materials/Weapons";
        const string ToonShader = "StylizedToonWorldKit/Toon/Toon Lit";

        /// <summary>
        /// One row per body to onboard. `assetName`/`weaponId` are INVENTED where the vendor name
        /// would collide with something already shipped — see `reason`.
        /// </summary>
        class Row
        {
            public string source;       // vendor prefab path
            public string assetName;    // WD_<Family>_<Name>
            public string weaponId;     // weapon.<family>.<name>
            public WeaponClass family;
            public bool twoHanded;
            public string variantOf;    // weaponId of the base, when this body duplicates one
            public bool blockedProjectile;
            public string reason;       // why this name, recorded for the report
        }

        // ── The manifest. Naming rules in force:
        //    * weaponId is save identity: unique, never reused, never colliding with the shipped 25.
        //    * A variant reserves the shape `<baseId>__<variant>` (double underscore) so a later
        //      recolour of the same body never has to rename its parent.
        static List<Row> Manifest()
        {
            const string V4New = "Assets/Low Poly Weapon Series V 4/Prefabs/New/Weapons";
            const string V4WWII = "Assets/Low Poly Weapon Series V 4/Prefabs/Low Poly Series V 4 New/Weapons";
            const string SG2 = "Assets/Low Poly ShotGun Weapon Pack 2/Prefabs/Weapons";
            const string SMG2 = "Assets/Low Poly SMG Weapon Pack 2/Prefabs/Weapons";
            const string SG1 = "Assets/ThirdParty/Low Poly ShotGun Weapon Pack 1/Prefabs/Weapons";
            const string MW4 = "Assets/ThirdParty/Low Poly Weapon Pack 4_MW_1/Prefabs/Weapons";
            const string VOL1 = "Assets/ThirdParty/Low Poly Weapons VOL.1/Prefabs";

            var r = new List<Row>();

            void Add(string src, string name, string id, WeaponClass fam, bool two,
                     string reason, string variantOf = null, bool blocked = false)
                => r.Add(new Row
                {
                    source = src, assetName = name, weaponId = id, family = fam, twoHanded = two,
                    reason = reason, variantOf = variantOf, blockedProjectile = blocked
                });

            // ── STARVED FAMILIES FIRST (owner priority): SMG, Marksman, LMG ──────────────
            Add($"{SMG2}/SMG_F.prefab", "WD_SMG_PackF", "weapon.smg.pack_f", WeaponClass.SMG, false,
                "New body, no collision. 'PackF' keeps the vendor letter without implying a real-world model.");
            Add($"{SMG2}/SMG_G.prefab", "WD_SMG_PackG", "weapon.smg.pack_g", WeaponClass.SMG, false, "As PackF.");
            Add($"{SMG2}/SMG_H.prefab", "WD_SMG_PackH", "weapon.smg.pack_h", WeaponClass.SMG, false, "As PackF.");
            Add($"{SMG2}/SMG_I.prefab", "WD_SMG_PackI", "weapon.smg.pack_i", WeaponClass.SMG, false, "As PackF.");
            Add($"{SMG2}/SMG_J.prefab", "WD_SMG_PackJ", "weapon.smg.pack_j", WeaponClass.SMG, false, "As PackF.");
            Add($"{V4WWII}/WWII_SMG_B.prefab", "WD_SMG_VintageB", "weapon.smg.vintage_b", WeaponClass.SMG, false,
                "'Vintage' marks the WWII visual line so it reads as a distinct sub-family in the shop.");
            Add($"{MW4}/SMG_P.prefab", "WD_SMG_ModernP", "weapon.smg.modern_p", WeaponClass.SMG, false,
                "MW4 body. 'Modern' distinguishes the MW4 visual line from the Vintage/Pack lines.");

            Add($"{V4WWII}/WWII_Recon_B.prefab", "WD_Marksman_VintageReconB", "weapon.marksman.vintage_recon_b",
                WeaponClass.Marksman, true, "Vintage line, marksman family.");
            Add($"{MW4}/Recon_P.prefab", "WD_Marksman_ReconP", "weapon.marksman.recon_p",
                WeaponClass.Marksman, true, "MW4 marksman body; the vision review rated it the best marksman asset available.");

            Add($"{V4WWII}/WWII_LMG_B.prefab", "WD_LMG_VintageB", "weapon.lmg.vintage_b", WeaponClass.LMG, true,
                "Second LMG body — the family had exactly one.");

            // ── REMAINING FAMILIES ───────────────────────────────────────────────────────
            Add($"{SG2}/ShotGun_F.prefab", "WD_Shotgun_PackF", "weapon.shotgun.pack_f", WeaponClass.Shotgun, true, "New body.");
            Add($"{SG2}/ShotGun_G.prefab", "WD_Shotgun_PackG", "weapon.shotgun.pack_g", WeaponClass.Shotgun, true, "New body.");
            Add($"{SG2}/ShotGun_H.prefab", "WD_Shotgun_PackH", "weapon.shotgun.pack_h", WeaponClass.Shotgun, true, "New body.");
            Add($"{SG2}/ShotGun_I.prefab", "WD_Shotgun_PackI", "weapon.shotgun.pack_i", WeaponClass.Shotgun, true, "New body.");
            Add($"{SG2}/ShotGun_J.prefab", "WD_Shotgun_PackJ", "weapon.shotgun.pack_j", WeaponClass.Shotgun, true, "New body.");
            Add($"{V4New}/ShotGun_P.prefab", "WD_Shotgun_ModernP", "weapon.shotgun.modern_p", WeaponClass.Shotgun, true, "New body.");

            Add($"{V4New}/Pistol_R.prefab", "WD_Sidearm_ModernR", "weapon.sidearm.modern_r", WeaponClass.Sidearm, false, "New body.");
            Add($"{MW4}/Pistol_P.prefab", "WD_Sidearm_MachinePistolP", "weapon.sidearm.machine_pistol_p",
                WeaponClass.Sidearm, false,
                "The vision review called this the most distinct sidearm silhouette in the project — a machine " +
                "pistol. The name states the shape rather than the vendor letter.");
            Add($"{VOL1}/M1911.prefab", "WD_Sidearm_M1911Vol1", "weapon.sidearm.m1911_vol1", WeaponClass.Sidearm, false,
                "NAME COLLISION: WD_Sidearm_M1911 / weapon.sidearm.m1911 already ships and is a DIFFERENT mesh. " +
                "Suffixed with its pack of origin (Vol1) — invented per the owner's delegation.");

            Add($"{V4New}/AR_W.prefab", "WD_AssaultRifle_ModernW", "weapon.assault_rifle.modern_w", WeaponClass.AssaultRifle, true, "New body.");
            Add($"{V4New}/AR_X.prefab", "WD_AssaultRifle_ModernX", "weapon.assault_rifle.modern_x", WeaponClass.AssaultRifle, true, "New body.");
            Add($"{V4WWII}/WWII_Rifle_A.prefab", "WD_AssaultRifle_VintageA", "weapon.assault_rifle.vintage_a", WeaponClass.AssaultRifle, true, "Vintage line.");
            Add($"{V4WWII}/WWII_Rifle_B.prefab", "WD_AssaultRifle_VintageB", "weapon.assault_rifle.vintage_b", WeaponClass.AssaultRifle, true, "Vintage line.");
            Add($"{SG1}/AR_A_2.prefab", "WD_AssaultRifle_LegacyA2", "weapon.assault_rifle.legacy_a2", WeaponClass.AssaultRifle, true,
                "Already toon-shaded and unused since M6.1. 'Legacy' marks the original ShotgunPack1 visual line.");
            Add($"{VOL1}/M4_8.prefab", "WD_AssaultRifle_LegacyM4", "weapon.assault_rifle.legacy_m4", WeaponClass.AssaultRifle, true,
                "Toon-ready and unused since M6.1.");
            Add($"{MW4}/AR_T.prefab", "WD_AssaultRifle_ModernT", "weapon.assault_rifle.modern_t", WeaponClass.AssaultRifle, true, "MW4 line.");
            Add($"{MW4}/AR_U.prefab", "WD_AssaultRifle_ModernU", "weapon.assault_rifle.modern_u", WeaponClass.AssaultRifle, true, "MW4 line.");

            // ShotGun_D is NOT a shotgun. The M7.0 roster notes record that its mesh is an AK-pattern
            // RIFLE, which is why it was excluded from the shotgun roster in the first place. It is
            // onboarded here into the family its geometry actually belongs to.
            Add($"{SG1}/ShotGun_D.prefab", "WD_AssaultRifle_LegacyD", "weapon.assault_rifle.legacy_d", WeaponClass.AssaultRifle, true,
                "MISLEADING VENDOR NAME: 'ShotGun_D' is an AK-pattern RIFLE mesh (recorded in the M6.1 " +
                "migration notes). Onboarded as AssaultRifle, not Shotgun, so the family count stays truthful.");

            // Launcher: data only, permanently blocked until FireMode.Projectile exists.
            Add($"{V4New}/Launcher_G.prefab", "WD_Launcher_ModernG", "weapon.launcher.modern_g", WeaponClass.AssaultRifle, true,
                "Launcher body. Onboarded as data ONLY and blocked: FireMode.Projectile does not exist.",
                blocked: true);

            return r;
        }

        [MenuItem("ZombieWar/Weapons/Factory/Onboard Vendor Bodies (M7.1)")]
        public static void Run()
        {
            Directory.CreateDirectory(MatDir);
            if (!AssetDatabase.IsValidFolder(MatDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Materials");
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Weapons");
            }

            var existing = AssetDatabase.FindAssets("t:WeaponData", new[] { DataDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null).ToList();
            var takenIds = new HashSet<string>(existing.Select(w => w.WeaponId), StringComparer.Ordinal);
            int nextOrder = existing.Count == 0 ? 0 : existing.Max(w => w.CatalogOrder) + 1;

            var log = new StringBuilder();
            log.AppendLine("assetName,weaponId,family,sourcePrefab,tris,longestAxis,authoringStatus,materialsOwned,note");
            int created = 0, skipped = 0;

            foreach (var row in Manifest())
            {
                if (takenIds.Contains(row.weaponId))
                {
                    // Idempotent: re-running must never mint a second copy of the same identity.
                    skipped++;
                    continue;
                }
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(row.source);
                if (src == null)
                {
                    Debug.LogError($"[Onboard] Source missing: {row.source}");
                    log.AppendLine($"{row.assetName},{row.weaponId},{row.family},{row.source},,,,,SOURCE MISSING");
                    continue;
                }

                // ---- 1. copy the prefab into project ownership
                string prefabPath = $"{PrefabDir}/WPN_{row.assetName.Substring(3)}.prefab";
                if (!AssetDatabase.CopyAsset(row.source, prefabPath))
                {
                    Debug.LogError($"[Onboard] Copy failed: {row.source} -> {prefabPath}");
                    continue;
                }

                // ---- 2. own + convert the materials (vendor originals untouched)
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                int owned = 0;
                foreach (var rend in contents.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = rend.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        mats[i] = OwnAndConvert(mats[i], ref owned);
                    }
                    rend.sharedMaterials = mats;
                }
                // Strip any grip anchors the VENDOR prefab shipped with. ShotGun_D is one such body.
                // Inheriting them would smuggle an unverified anchor past the authoring gate and let
                // a weapon look authored when no one authored it. The owner starts from nothing.
                foreach (var inherited in contents.GetComponentsInChildren<WeaponGripPoints>(true))
                    UnityEngine.Object.DestroyImmediate(inherited, true);

                // Name the root after the file so the roster validator's naming check passes.
                contents.name = Path.GetFileNameWithoutExtension(prefabPath);

                int tris = 0; var b = new Bounds(); bool first = true;
                foreach (var mf in contents.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    tris += mf.sharedMesh.triangles.Length / 3;
                    if (first) { b = mf.sharedMesh.bounds; first = false; } else b.Encapsulate(mf.sharedMesh.bounds);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                PrefabUtility.UnloadPrefabContents(contents);

                // ---- 3. the WeaponData, minus every anchor
                var data = ScriptableObject.CreateInstance<WeaponData>();
                data.weaponName = Prettify(row.assetName);
                data.weaponClass = row.family;
                data.twoHanded = row.twoHanded;
                data.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                // Anchors are the owner's to author. Never inferred, so authored positions stay off.
                data.useAuthoredGripPositions = false;

                string dataPath = $"{DataDir}/{row.assetName}.asset";
                AssetDatabase.CreateAsset(data, dataPath);

                var so = new SerializedObject(data);
                so.FindProperty("weaponId").stringValue = row.weaponId;
                so.FindProperty("catalogOrder").intValue = nextOrder++;
                so.FindProperty("authoringStatus").enumValueIndex = row.blockedProjectile
                    ? (int)WeaponData.AuthoringStatus.BlockedNeedsProjectileFireMode
                    : (int)WeaponData.AuthoringStatus.PendingOwnerAuthoring;
                // tier stays Common: the measured power band is a function of authored stats, and
                // this weapon has none yet. Inventing a tier would be inventing a balance claim.
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);

                takenIds.Add(row.weaponId);
                created++;

                float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                string note = row.blockedProjectile ? "BLOCKED_NEEDS_PROJECTILE_FIREMODE" : "";
                if (tris > 11614) note += (note.Length > 0 ? "; " : "") + $"G6 tris {tris} > 11614";
                if (longest > 1.465f) note += (note.Length > 0 ? "; " : "") + $"size {longest:F3} m > 1.465";
                log.AppendLine($"{row.assetName},{row.weaponId},{row.family},{row.source},{tris}," +
                               $"{longest:F3},{(row.blockedProjectile ? "BlockedNeedsProjectileFireMode" : "PendingOwnerAuthoring")}," +
                               $"{owned},\"{note}\"");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Review", "M7_1_Packs", "onboarded.csv"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, log.ToString());
            Debug.Log($"[Onboard] created {created}, skipped {skipped} (already present). -> Review/M7_1_Packs/onboarded.csv");
        }

        /// <summary>
        /// Copies a vendor-owned material into the project and converts the COPY to the toon
        /// contract. The vendor asset is never written to — that is a standing project rule.
        /// A material already inside the project is returned unchanged.
        /// </summary>
        static Material OwnAndConvert(Material m, ref int owned)
        {
            string path = AssetDatabase.GetAssetPath(m);
            if (string.IsNullOrEmpty(path) || !IsVendorOwned(path))
                return m; // already project-owned

            string dst = $"{MatDir}/{SanitizeFile(m.name)}.mat";
            var already = AssetDatabase.LoadAssetAtPath<Material>(dst);
            if (already != null) { owned++; return already; }

            if (!AssetDatabase.CopyAsset(path, dst)) return m;
            var copy = AssetDatabase.LoadAssetAtPath<Material>(dst);
            if (copy == null) return m;

            var toon = Shader.Find(ToonShader);
            if (toon != null && copy.shader != toon)
            {
                var baseColor = copy.HasProperty("_BaseColor") ? copy.GetColor("_BaseColor")
                              : copy.HasProperty("_Color") ? copy.GetColor("_Color") : Color.white;
                var baseMap = copy.HasProperty("_BaseMap") ? copy.GetTexture("_BaseMap")
                            : copy.HasProperty("_MainTex") ? copy.GetTexture("_MainTex") : null;
                copy.shader = toon;
                if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", baseColor);
                if (copy.HasProperty("_BaseMap")) copy.SetTexture("_BaseMap", baseMap);
                EditorUtility.SetDirty(copy);
            }
            owned++;
            return copy;
        }

        static bool IsVendorOwned(string path) =>
            path.StartsWith("Assets/ThirdParty/") || path.StartsWith("Assets/Low Poly ") ||
            path.StartsWith("Assets/Synty/");

        static string SanitizeFile(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        static string Prettify(string assetName)
        {
            var n = assetName.StartsWith("WD_") ? assetName.Substring(3) : assetName;
            int us = n.IndexOf('_');
            return us >= 0 ? n.Substring(us + 1) : n;
        }

        // ────────────────────────────────────────────────────────────── A4: own the shipped 25

        /// <summary>
        /// A4 / gate G1b — the 25 already-shipped weapons reference `.mat` files that live inside
        /// vendor pack folders, so a pack reimport could restyle the whole arsenal. This copies each
        /// into project ownership and repoints the prefab. Vendor files stay byte-identical.
        /// </summary>
        [MenuItem("ZombieWar/Weapons/Factory/Own Shipped Weapon Materials (G1b)")]
        public static void OwnShippedMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Materials");
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Weapons");
            }

            var all = AssetDatabase.FindAssets("t:WeaponData", new[] { DataDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null && w.weaponPrefab != null).ToList();

            int prefabsTouched = 0, matsOwned = 0;
            foreach (var w in all)
            {
                string path = AssetDatabase.GetAssetPath(w.weaponPrefab);
                var contents = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (var rend in contents.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = rend.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        var repl = OwnAndConvert(mats[i], ref matsOwned);
                        if (repl != mats[i]) { mats[i] = repl; changed = true; }
                    }
                    if (changed) rend.sharedMaterials = mats;
                }
                if (changed) { PrefabUtility.SaveAsPrefabAsset(contents, path); prefabsTouched++; }
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[G1b] repointed {prefabsTouched} prefab(s); {matsOwned} material reference(s) now project-owned.");
        }
    }
}
