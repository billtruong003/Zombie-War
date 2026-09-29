using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Turns the skill models made in Blender (Review/M8/skill_models.blend, exported to
    /// Art/Models/Skills, one merged mesh each) into the prefabs SkillArsenal uses: the Drone Buddy
    /// drone, the Orbit Blades saw and the Boomerang.
    ///
    /// Every model shares one low-poly palette texture (T_HC_Palette: 8 x 4 gradient cells, UVs laid
    /// on the cells) through the weapons' toon shader. The drone's second material is the
    /// procedural rotor smear (HordeCall/RotorSmear) on its flat 12-triangle rotor discs;
    /// SkillArsenal tints that material per rank. Re-run after re-exporting from Blender.
    /// </summary>
    public static class SkillModelsBuilder
    {
        const string ModelDir = "Assets/_Project/Art/Models/Skills/";
        const string MatDir = "Assets/_Project/Materials/Skills/";
        const string PrefabDir = "Assets/_Project/Prefabs/Skills/";
        const string Palette = "Assets/_Project/Art/Textures/T_HC_Palette.png";
        const string ToonTemplate = "Assets/_Project/Materials/Weapons/Low Poly Weapon.mat";
        const string RotorShader = "Assets/_Project/Art/Shaders/RotorSmear.shader";
        const string RotorMasks = "Assets/_Project/Art/Textures/T_HC_RotorSmear.png";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";

        [MenuItem("HordeCall/Skills/Build Skill Models")]
        public static string Build()
        {
            System.IO.Directory.CreateDirectory(MatDir);
            ImportSettings();

            var palTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Palette);
            var pal = LoadOrCreate(MatDir + "M_SK_Palette.mat", () => new Material(AssetDatabase.LoadAssetAtPath<Material>(ToonTemplate)));
            pal.SetTexture("_BaseMap", palTex);
            pal.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(pal);

            // Colours are set once, on creation: re-running the build keeps the owner's tuning.
            var rotor = LoadOrCreate(MatDir + "M_SK_RotorSmear.mat", () =>
            {
                var m = new Material(AssetDatabase.LoadAssetAtPath<Shader>(RotorShader));
                m.SetFloat("_Speed", 4f);
                return m;
            });
            rotor.shader = AssetDatabase.LoadAssetAtPath<Shader>(RotorShader);
            rotor.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RotorMasks));
            EditorUtility.SetDirty(rotor);

            // Plain white alpha-faded trails for saws, boomerang tips and drones. They used the opaque
            // chain-lightning material, whose streaky texture smeared black through a long ribbon.
            var trail = LoadOrCreate(MatDir + "M_SK_Trail.mat", () => new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            trail.SetTexture("_BaseMap", null);
            trail.SetColor("_BaseColor", Color.white);
            trail.SetFloat("_Surface", 1f); trail.SetFloat("_Blend", 0f);
            trail.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            trail.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            trail.SetFloat("_SrcBlendAlpha", 1f);
            trail.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            trail.SetFloat("_ZWrite", 0f);
            trail.SetFloat("_Cull", 0f);   // both sides: a flat ribbon reads from any camera tilt
            trail.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            trail.SetOverrideTag("RenderType", "Transparent");
            trail.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(trail);

            var drone = MakePrefab("SK_Drone", new[] { pal, rotor });
            var saw = MakePrefab("SK_SawBlade", new[] { pal });
            var boom = MakePrefab("SK_Boomerang", new[] { pal });
            AssetDatabase.SaveAssets();

            // Phase A2: power assets live in the FX library, not on the player prefab.
            var lib = AssetDatabase.LoadAssetAtPath<ZombieWar.Skills.Powers.SkillFxLibrary>(SkillFxLibraryMigration.LibraryPath);
            if (lib == null) return "missing " + SkillFxLibraryMigration.LibraryPath;
            lib.drone.model = drone;
            lib.orbit.bladeModel = saw;
            lib.boomerang.model = boom;
            lib.shared.trailMaterial = trail;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return "built SK_Drone, SK_SawBlade, SK_Boomerang";
        }

        static void ImportSettings()
        {
            if (AssetImporter.GetAtPath(Palette) is TextureImporter t)
            {
                // No mipmaps: smaller mips would bleed neighbouring palette cells into each other.
                t.mipmapEnabled = false; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
                t.textureCompression = TextureImporterCompression.CompressedHQ; t.maxTextureSize = 1024;
                t.SaveAndReimport();
            }
            if (AssetImporter.GetAtPath(RotorMasks) is TextureImporter mk)
            {
                // Data masks, not colour: linear, clamped so the rotated square never wraps.
                mk.sRGBTexture = false; mk.wrapMode = TextureWrapMode.Clamp; mk.alphaSource = TextureImporterAlphaSource.None;
                mk.maxTextureSize = 256; mk.textureCompression = TextureImporterCompression.CompressedHQ;
                mk.SaveAndReimport();
            }
            foreach (var n in new[] { "SK_Drone", "SK_SawBlade", "SK_Boomerang" })
                if (AssetImporter.GetAtPath(ModelDir + n + ".fbx") is ModelImporter m)
                {
                    m.materialImportMode = ModelImporterMaterialImportMode.None;
                    m.importNormals = ModelImporterNormals.Import;   // keep Blender's sharp creases
                    m.importAnimation = false; m.animationType = ModelImporterAnimationType.None;
                    // SkillArsenal reads the boomerang's vertices to put its trail on the tip.
                    m.isReadable = n == "SK_Boomerang";
                    m.SaveAndReimport();
                }
        }

        /// <summary>Phase A5: the Sentry Turret and the Meteor (Review/M8/skill_models.blend, same palette).
        /// The meteor becomes a falling effect: the pack's fire missile with the rock inside it, so the
        /// shared blast system can fly it into its mark like a bomb.</summary>
        [MenuItem("HordeCall/Skills/Build A5 Models (turret + meteor)")]
        public static string BuildA5()
        {
            foreach (var n in new[] { "SK_Turret", "SK_Meteor" })
                if (AssetImporter.GetAtPath(ModelDir + n + ".fbx") is ModelImporter m)
                {
                    m.materialImportMode = ModelImporterMaterialImportMode.None;
                    m.importNormals = ModelImporterNormals.Import;
                    m.importAnimation = false; m.animationType = ModelImporterAnimationType.None;
                    m.SaveAndReimport();
                }
            var pal = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_SK_Palette.mat");
            if (pal == null) return "run Build Skill Models first (palette material)";
            var turret = MakePrefab("SK_Turret", new[] { pal });
            var rock = MakePrefab("SK_Meteor", new[] { pal });

            const string fire = "Assets/ThirdParty/Epic Toon FX/Prefabs/Combat/Missiles/Fireball/FireballMissileFire.prefab";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fire);
            if (src == null) return "missing " + fire;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            GameObject fall;
            try
            {
                go.name = "SK_MeteorFall";
                // The pack's missile flies along +Z; its own projectile scripts would fight the blast system.
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
                var inner = (GameObject)PrefabUtility.InstantiatePrefab(rock, go.transform);
                inner.name = "Rock";
                inner.transform.localPosition = Vector3.zero;
                inner.transform.localScale = Vector3.one * 1.3f;
                inner.AddComponent<ZombieWar.Skills.Powers.SpinWhileAlive>();
                go.transform.localScale = Vector3.one * 1.4f;
                fall = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "SK_MeteorFall.prefab");
            }
            finally { Object.DestroyImmediate(go); }
            AssetDatabase.SaveAssets();
            return $"built {turret.name}, {rock.name}, {fall.name}";
        }

        /// <summary>Phase A6: mine, axe and storm cloud (skill_models.blend), the ice shard (the pack's
        /// frost missile without its projectile script) and the War Dog (the DogPup's VAT body, recoloured
        /// steel blue so it never reads as an enemy pup).</summary>
        [MenuItem("HordeCall/Skills/Build A6 Models (mine, axe, cloud, shard, dog)")]
        public static string BuildA6()
        {
            foreach (var n in new[] { "SK_Mine", "SK_Axe", "SK_StormCloud" })
                if (AssetImporter.GetAtPath(ModelDir + n + ".fbx") is ModelImporter m)
                {
                    m.materialImportMode = ModelImporterMaterialImportMode.None;
                    m.importNormals = ModelImporterNormals.Import;
                    m.importAnimation = false; m.animationType = ModelImporterAnimationType.None;
                    m.isReadable = n == "SK_Axe";   // the tip trail reads the axe's vertices
                    m.SaveAndReimport();
                }
            var pal = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_SK_Palette.mat");
            if (pal == null) return "run Build Skill Models first (palette material)";
            MakePrefab("SK_Mine", new[] { pal });
            MakePrefab("SK_Axe", new[] { pal });
            MakePrefab("SK_StormCloud", new[] { pal });

            // Ice shard: the frost missile, stripped of its own movement.
            const string frost = "Assets/ThirdParty/Epic Toon FX/Prefabs/Combat/Missiles/Frost/FrostMissile.prefab";
            var fgo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(frost));
            try
            {
                fgo.name = "SK_IceShard";
                foreach (var mb in fgo.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                foreach (var col in fgo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                foreach (var rb in fgo.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
                PrefabUtility.SaveAsPrefabAsset(fgo, PrefabDir + "SK_IceShard.prefab");
            }
            finally { Object.DestroyImmediate(fgo); }

            // War Dog: the pup's VAT Visual with a steel-blue coat.
            var pup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/ENM_DogPup_VAT.prefab");
            var visual = pup != null ? pup.transform.Find("Visual") : null;
            if (visual == null) return "missing DogPup Visual";
            var srcMat = visual.GetComponent<MeshRenderer>().sharedMaterial;
            var coat = RecolourCoat((Texture2D)srcMat.GetTexture("_MainTex"), "Assets/_Project/Art/Textures/T_WarDog.png");
            var dogMat = LoadOrCreate(MatDir + "M_WarDog.mat", () => new Material(srcMat));
            dogMat.CopyPropertiesFromMaterial(srcMat);
            dogMat.SetTexture("_MainTex", coat);
            EditorUtility.SetDirty(dogMat);
            var dog = Object.Instantiate(visual.gameObject);
            try
            {
                dog.name = "SK_WarDogBody";
                dog.GetComponent<MeshRenderer>().sharedMaterial = dogMat;
                PrefabUtility.SaveAsPrefabAsset(dog, PrefabDir + "SK_WarDogBody.prefab");
            }
            finally { Object.DestroyImmediate(dog); }
            AssetDatabase.SaveAssets();
            return "built SK_Mine, SK_Axe, SK_StormCloud, SK_IceShard, SK_WarDogBody";
        }

        /// The pup's warm coat turned steel blue: hue rotated, saturation eased, value kept.
        static Texture2D RecolourCoat(Texture2D src, string path)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                if (s > 0.12f) { h = Mathf.Repeat(h + 0.5f, 1f); s *= 0.65f; }
                var c = Color.HSVToRGB(h, s, v); c.a = px[i].a; px[i] = c;
            }
            tex.SetPixels(px);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material LoadOrCreate(string path, System.Func<Material> make)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = make();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static GameObject MakePrefab(string name, Material[] mats)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + name + ".fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = name;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    int subs = r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : mats.Length;
                    var use = new Material[Mathf.Min(subs, mats.Length)];
                    System.Array.Copy(mats, use, use.Length);
                    r.sharedMaterials = use;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                return PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + name + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
