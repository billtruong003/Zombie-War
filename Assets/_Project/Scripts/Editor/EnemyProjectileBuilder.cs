using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Builds the pooled enemy projectiles in Resources/Pools, where Bill.Pool loads them by key:
    /// ZombieSpit (green glob, cactus spitters) and BoneBolt (pink bolt, Skeleton Mage). Each is a
    /// ZombieSpitProjectile root with an Epic Toon FX missile as its body and the matching fireball
    /// burst as its impact. Also points the Skeleton Mage at BoneBolt.
    /// </summary>
    public static class EnemyProjectileBuilder
    {
        const string Etfx = "Assets/ThirdParty/Epic Toon FX/Prefabs/Combat/";
        const string PoolDir = "Assets/_Project/Resources/Pools/";
        const string MagePrefab = "Assets/_Project/Prefabs/Enemies/ENM_SkeletonMage_VAT.prefab";

        [MenuItem("HordeCall/Enemies/Build Enemy Projectiles")]
        public static string Build()
        {
            Make("ZombieSpit", Etfx + "Missiles/Energy/EnergyMissileGreen.prefab",
                 Etfx + "Explosions/FireballRoundExplosion/ExplosionFireballGreen.prefab", 0.55f);
            Make("BoneBolt", Etfx + "Missiles/Energy/EnergyMissilePink.prefab",
                 Etfx + "Explosions/FireballRoundExplosion/ExplosionFireballPink.prefab", 0.55f);

            var mage = PrefabUtility.LoadPrefabContents(MagePrefab);
            try
            {
                var ranged = mage.GetComponentInChildren<ZombieRanged>(true);
                var so = new SerializedObject(ranged);
                so.FindProperty("projectilePoolKey").stringValue = "BoneBolt";
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(mage, MagePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(mage); }
            AssetDatabase.SaveAssets();
            return "built ZombieSpit, BoneBolt";
        }

        static void Make(string key, string missilePath, string impactPath, float bodyScale)
        {
            var missile = AssetDatabase.LoadAssetAtPath<GameObject>(missilePath);
            var impact = AssetDatabase.LoadAssetAtPath<ParticleSystem>(impactPath);
            if (missile == null || impact == null) throw new System.Exception($"{key}: missing {missilePath} or {impactPath}");

            var root = new GameObject(key);
            try
            {
                var projectile = root.AddComponent<ZombieSpitProjectile>();
                var body = (GameObject)PrefabUtility.InstantiatePrefab(missile, root.transform);
                PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                body.name = "Body";
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                body.transform.localScale = Vector3.one * bodyScale;
                foreach (var ps in body.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.playOnAwake = true;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // honour bodyScale
                }

                var so = new SerializedObject(projectile);
                so.FindProperty("impactFx").objectReferenceValue = impact;
                so.ApplyModifiedPropertiesWithoutUndo();

                System.IO.Directory.CreateDirectory(PoolDir);
                PrefabUtility.SaveAsPrefabAsset(root, PoolDir + key + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
