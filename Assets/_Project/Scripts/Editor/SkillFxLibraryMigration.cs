using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieWar.Skills;
using ZombieWar.Skills.Powers;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Phase A2 (2026-09-30): the powers left SkillArsenal / SkillCombatDriver for one module each,
    /// and their assets left the player prefab for <see cref="SkillFxLibrary"/>. This copies the
    /// values the prefab held into the library, field by field. Run once, before the old fields go.
    /// </summary>
    public static class SkillFxLibraryMigration
    {
        public const string LibraryPath = "Assets/_Project/Data/Skills/SkillFxLibrary.asset";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";

        static readonly (string from, string to)[] ArsenalMap =
        {
            ("enemyMask", "shared.enemyMask"),
            ("trailMaterial", "shared.trailMaterial"),
            ("discMaterial", "shared.discMaterial"),
            ("strikeMissileFx", "shared.bombFx"),
            ("bombFallHeight", "shared.bombFallHeight"),
            ("bombDrift", "shared.bombDrift"),
            ("missileFallSeconds", "shared.bombFallSeconds"),
            ("soulWispFx", "shared.soulWispFx"),
            ("evolveFx", "shared.evolveFx"),

            ("bladeModel", "orbit.bladeModel"),
            ("bladeMaterial", "orbit.bladeMaterial"),
            ("bladeHitFx", "orbit.hitFx"),
            ("orbitBaseDamage", "orbit.baseDamage"),
            ("orbitHitInterval", "orbit.hitInterval"),
            ("bladeScale", "orbit.bladeScale"),
            ("orbitTrailColor", "orbit.trailColor"),
            ("pathMaterial", "orbit.pathMaterial"),
            ("showOrbitPath", "orbit.showPath"),

            ("droneModel", "drone.model"),
            ("droneModelScale", "drone.modelScale"),
            ("droneBodyFx", "drone.bodyFx"),
            ("droneBodyScale", "drone.bodyScale"),
            ("droneTracer", "drone.tracer"),
            ("droneMuzzleFx", "drone.muzzleFx"),
            ("droneHitFx", "drone.hitFx"),
            ("droneBaseDamage", "drone.baseDamage"),
            ("droneRange", "drone.range"),

            ("frostNovaFx", "frost.fx"),
            ("frostNovaNativeRadius", "frost.nativeRadius"),
            ("frostBaseDamage", "frost.baseDamage"),
            ("frostTint", "frost.slowTint"),
            ("frozenTint", "frost.frozenTint"),
            ("freezeBurstFx", "frost.freezeBurstFx"),

            ("firePatchFx", "fireTrail.patchFx"),
            ("firePatchNativeRadius", "fireTrail.nativeRadius"),
            ("firePatchRadius", "fireTrail.radius"),
            ("firePatchSeconds", "fireTrail.seconds"),
            ("burnTint", "fireTrail.burnTint"),

            ("boomerangHitFx", "boomerang.hitFx"),
            ("boomerangBaseDamage", "boomerang.baseDamage"),
            ("boomerangRange", "boomerang.range"),
            ("boomerangOutSeconds", "boomerang.outSeconds"),
            ("boomerangModel", "boomerang.model"),
            ("boomerangScale", "boomerang.scale"),
            ("boomerangTint", "boomerang.tint"),
            ("boomerangTrailColor", "boomerang.trailColor"),

            ("strikeBlastFx", "airstrike.blastFx"),
            ("strikeBlastNativeRadius", "airstrike.blastNativeRadius"),
            ("strikeDecalFx", "airstrike.decalFx"),
            ("airstrikeBaseDamage", "airstrike.baseDamage"),
            ("airstrikeDelay", "airstrike.delay"),

            ("thunderStrikeFx", "chain.skyStrikeFx"),
            ("reaperFx", "selfBurst.reaperFx"),
            ("shieldMaterial", "shield.material"),
            ("shieldAuraFx", "shield.auraFx"),
        };

        static readonly (string from, string to)[] DriverMap =
        {
            ("chainArcFx", "chain.sparkFx"),
            ("chainDamage", "chain.damage"),
            ("chainJumpRange", "chain.jumpRange"),
            ("chainFirstReach", "chain.firstReach"),
            ("scanRadius", "chain.scanRadius"),

            ("explosionFx", "ordnance.blastFx"),
            ("explosionNativeRadius", "ordnance.blastNativeRadius"),
            ("ordnanceDelay", "ordnance.delay"),
            ("blastDamage", "ordnance.damage"),
            ("scanRadius", "ordnance.scanRadius"),

            ("soulBurstFx", "selfBurst.soulBurstFx"),
            ("soulBurstNativeRadius", "selfBurst.soulBurstNativeRadius"),
            ("emergencyFx", "selfBurst.emergencyFx"),
            ("emergencyNativeRadius", "selfBurst.emergencyNativeRadius"),
            ("blastDamage", "selfBurst.damage"),
            ("scanRadius", "selfBurst.scanRadius"),

            ("shieldBreakFx", "shield.breakFx"),
        };

        [MenuItem("HordeCall/Skills/Migrate Player FX To Library")]
        public static string Run()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(LibraryPath);
            if (lib == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LibraryPath));
                lib = ScriptableObject.CreateInstance<SkillFxLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            var log = new StringBuilder();
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var dst = new SerializedObject(lib);
                var arsenal = root.GetComponent<SkillArsenal>();
                var driver = root.GetComponent<SkillCombatDriver>();
                if (arsenal == null || driver == null) return "Player prefab has no SkillArsenal / SkillCombatDriver";
                Copy(new SerializedObject(arsenal), dst, ArsenalMap, log);
                Copy(new SerializedObject(driver), dst, DriverMap, log);
                dst.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lib);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[FX Library] migrated\n" + log);
            return log.ToString();
        }

        static void Copy(SerializedObject src, SerializedObject dst, (string from, string to)[] map, StringBuilder log)
        {
            foreach (var (from, to) in map)
            {
                var a = src.FindProperty(from);
                var b = dst.FindProperty(to);
                if (a == null) { log.AppendLine($"MISSING source {from}"); continue; }
                if (b == null) { log.AppendLine($"MISSING target {to}"); continue; }
                switch (a.propertyType)
                {
                    case SerializedPropertyType.ObjectReference: b.objectReferenceValue = a.objectReferenceValue; break;
                    case SerializedPropertyType.Float: b.floatValue = a.floatValue; break;
                    case SerializedPropertyType.Integer: b.intValue = a.intValue; break;
                    case SerializedPropertyType.Boolean: b.boolValue = a.boolValue; break;
                    case SerializedPropertyType.Color: b.colorValue = a.colorValue; break;
                    case SerializedPropertyType.LayerMask: b.intValue = a.intValue; break;
                    default: log.AppendLine($"UNHANDLED {from} ({a.propertyType})"); continue;
                }
                string v = a.propertyType == SerializedPropertyType.ObjectReference
                    ? (a.objectReferenceValue != null ? a.objectReferenceValue.name : "null")
                    : a.propertyType == SerializedPropertyType.Float ? a.floatValue.ToString("0.###")
                    : a.propertyType == SerializedPropertyType.Color ? a.colorValue.ToString()
                    : a.propertyType == SerializedPropertyType.Boolean ? a.boolValue.ToString() : a.intValue.ToString();
                log.AppendLine($"{from} -> {to} = {v}");
            }
        }

        /// <summary>Points the player's SkillArsenal at the library (after the new arsenal compiles).</summary>
        [MenuItem("HordeCall/Skills/Wire FX Library On Player")]
        public static string Wire()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SkillFxLibrary>(LibraryPath);
            if (lib == null) return "no library";
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var so = new SerializedObject(root.GetComponent<SkillArsenal>());
                var p = so.FindProperty("library");
                if (p == null) return "SkillArsenal has no library field yet";
                p.objectReferenceValue = lib;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return "wired";
        }
    }
}
