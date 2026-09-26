using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// M8 skill pass: builds the placeholder drone and binds the skill-pass assets on Player.prefab.
    ///
    /// The drone is a small quadcopter made of primitives, laid out the way the real model must be:
    /// - "Muzzle": the gun tip (tracers start here),
    /// - "Rotor0..3": spun around their local Y,
    /// - every renderer under "Glow": takes the rank colour (cyan → green → gold, magenta Squadron).
    /// Replace the prefab with the real drone later; keep those names and it works unchanged.
    public static class SkillAssetsBuilder
    {
        const string DronePath = "Assets/_Project/Prefabs/Skills/Drone_Placeholder.prefab";
        const string MatDir = "Assets/_Project/Materials/FX/";
        const string Etfx = "Assets/ThirdParty/Epic Toon FX/Prefabs/";

        [MenuItem("ZombieWar/Skills/Build Skill Pass Assets (drone + bindings)")]
        public static void Build()
        {
            var body = Mat("M_DroneBody", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.17f, 0.19f, 0.23f));
                m.SetFloat("_Metallic", 0.55f);
                m.SetFloat("_Smoothness", 0.55f);
            });
            var trim = Mat("M_DroneTrim", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.42f, 0.45f, 0.5f));
                m.SetFloat("_Metallic", 0.3f);
                m.SetFloat("_Smoothness", 0.4f);
            });
            var glow = Mat("M_DroneGlow", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", Color.white));
            var mark = Mat("M_SkillMark", "Sprites/Default", m => { });

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DronePath));
            var root = new GameObject("Drone_Placeholder");
            try
            {
                Part(root.transform, "Body", PrimitiveType.Cube, new Vector3(0, 0, 0), Vector3.zero, new Vector3(0.34f, 0.1f, 0.34f), body);
                Part(root.transform, "Canopy", PrimitiveType.Sphere, new Vector3(0, 0.06f, 0.02f), Vector3.zero, new Vector3(0.22f, 0.11f, 0.24f), trim);
                for (int i = 0; i < 4; i++)
                {
                    float ang = 45f + 90f * i;
                    var dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                    Part(root.transform, "Arm" + i, PrimitiveType.Cube, dir * 0.18f, new Vector3(0, ang, 0), new Vector3(0.05f, 0.035f, 0.3f), body);
                    var motorPos = dir * 0.3f;
                    Part(root.transform, "Motor" + i, PrimitiveType.Cylinder, motorPos + Vector3.up * 0.02f, Vector3.zero, new Vector3(0.08f, 0.03f, 0.08f), trim);
                    var rotor = new GameObject("Rotor" + i).transform;
                    rotor.SetParent(root.transform, false);
                    rotor.localPosition = motorPos + Vector3.up * 0.06f;
                    Part(rotor, "BladeA", PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(0.26f, 0.006f, 0.035f), trim);
                    Part(rotor, "BladeB", PrimitiveType.Cube, Vector3.zero, new Vector3(0, 90, 0), new Vector3(0.26f, 0.006f, 0.035f), trim);
                }
                // Gun under the nose.
                Part(root.transform, "Gun", PrimitiveType.Cylinder, new Vector3(0, -0.07f, 0.13f), new Vector3(90, 0, 0), new Vector3(0.035f, 0.08f, 0.035f), body);
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = new Vector3(0, -0.07f, 0.23f);

                // Emissive parts: the rank colour lives here.
                var g = new GameObject("Glow").transform;
                g.SetParent(root.transform, false);
                Part(g, "Eye", PrimitiveType.Sphere, new Vector3(0, 0.0f, 0.175f), Vector3.zero, new Vector3(0.09f, 0.05f, 0.03f), glow);
                Part(g, "Belly", PrimitiveType.Cube, new Vector3(0, -0.052f, 0), Vector3.zero, new Vector3(0.24f, 0.012f, 0.24f), glow);
                Part(g, "Stripe", PrimitiveType.Cube, new Vector3(0, 0.052f, -0.02f), Vector3.zero, new Vector3(0.05f, 0.012f, 0.3f), glow);
                for (int i = 0; i < 4; i++)
                {
                    var dir = Quaternion.Euler(0, 45f + 90f * i, 0) * Vector3.forward;
                    Part(g, "Light" + i, PrimitiveType.Sphere, dir * 0.3f + Vector3.up * 0.015f, Vector3.zero, Vector3.one * 0.075f, glow);
                }
                PrefabUtility.SaveAsPrefabAsset(root, DronePath);
            }
            finally { Object.DestroyImmediate(root); }

            const string reticle = "Assets/_Project/Art/Textures/FX/tex_skill_reticle.png";
            AssetDatabase.ImportAsset(reticle);
            if (AssetImporter.GetAtPath(reticle) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            BindPlayer(AssetDatabase.LoadAssetAtPath<GameObject>(DronePath), mark);
            AssetDatabase.SaveAssets();
            Debug.Log("[Skills] Drone placeholder built and skill-pass assets bound.");
        }

        static void BindPlayer(GameObject drone, Material mark)
        {
            const string path = "Assets/_Project/Prefabs/Player.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ars = new SerializedObject(root.GetComponent<ZombieWar.Skills.SkillArsenal>());
                ars.FindProperty("droneModel").objectReferenceValue = drone;
                ars.FindProperty("droneModelScale").floatValue = 1.9f;
                ars.FindProperty("soulWispFx").objectReferenceValue = Fx("Combat/Missiles/Soul/SoulMissileGreen");
                ars.FindProperty("freezeBurstFx").objectReferenceValue = Fx("Combat/Explosions/SnowExplosion/SnowExplosion");
                ars.FindProperty("reaperFx").objectReferenceValue = Fx("Combat/Explosions/SoulExplosion/SoulExplosionPurple");
                ars.FindProperty("pathMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_SkillLineAlpha.mat");
                ars.FindProperty("boomerangScale").floatValue = 1.6f;
                ars.ApplyModifiedPropertiesWithoutUndo();

                var drv = new SerializedObject(root.GetComponent<ZombieWar.Skills.SkillCombatDriver>());
                drv.FindProperty("soulBurstFx").objectReferenceValue = Fx("Combat/Explosions/SoulExplosion/SoulExplosionGreen");
                drv.ApplyModifiedPropertiesWithoutUndo();

                var fx = new SerializedObject(root.GetComponent<ZombieWar.Skills.SkillFxDirector>());
                fx.FindProperty("markSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Textures/FX/tex_skill_reticle.png");
                fx.FindProperty("markMaterial").objectReferenceValue = mark;
                fx.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static ParticleSystem Fx(string rel)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Etfx + rel + ".prefab");
            if (go == null) throw new System.Exception("missing FX " + rel);
            return go.GetComponent<ParticleSystem>();
        }

        static Material Mat(string name, string shader, System.Action<Material> cfg)
        {
            string path = MatDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (sh == null) throw new System.Exception("missing shader " + shader);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            cfg(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
