using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Pushes a <see cref="VatLookConfig"/> onto every baked VAT enemy material, and owns the two
    /// shared assets that go with it: the dissolve noise texture and the blob-shadow material.
    ///
    /// Applying is deliberately a one-shot editor action rather than a runtime lookup: these are
    /// material constants, so writing them once at author time costs nothing at runtime and keeps
    /// the shipped materials self-contained.
    ///
    /// Menu: Tools/ZombieWar/Apply VAT Look.
    /// </summary>
    public static class VatLookApplier
    {
        public const string ConfigPath = "Assets/_Project/Data/Art/VatLookConfig.asset";
        public const string NoisePath = "Assets/_Project/Art/Textures/T_DissolveNoise.png";
        public const string ShadowMatPath = "Assets/_Project/Art/Materials/M_BlobShadow.mat";
        public const string ShadowTexPath = "Assets/_Project/Art/Textures/T_BlobShadow.png";
        const string VatEnemyDir = "Assets/_Project/Art/VAT/Enemies";

        [MenuItem("Tools/ZombieWar/Apply VAT Look")]
        public static void ApplyMenu() => Apply(LoadOrCreateConfig());

        public static VatLookConfig LoadOrCreateConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<VatLookConfig>(ConfigPath);
            if (cfg == null)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(ConfigPath).Replace('\\', '/'));
                cfg = ScriptableObject.CreateInstance<VatLookConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
                AssetDatabase.SaveAssets();
            }
            if (cfg.dissolveNoise == null)
            {
                cfg.dissolveNoise = EnsureNoiseTexture();
                EditorUtility.SetDirty(cfg);
            }
            return cfg;
        }

        /// <summary>
        /// Writes the shared look onto every enemy material AND the two timing values onto every
        /// enemy prefab. Returns how many materials were updated.
        ///
        /// <paramref name="save"/> false is the live-preview path: material properties are written
        /// (so the Scene view updates on the same frame you drag a slider) but nothing is flushed to
        /// disk. Saving on every slider frame would stall the editor. Pass true on the explicit
        /// Apply press to persist.
        /// </summary>
        public static int Apply(VatLookConfig cfg, bool save = true)
        {
            if (cfg == null) return 0;

            int count = 0;
            foreach (var mat in EnemyMaterials())
            {
                mat.SetFloat("_SpecSteps", cfg.specSteps);
                mat.SetFloat("_SpecSize", cfg.specSize);
                mat.SetFloat("_SpecIntensity", cfg.specIntensity);

                mat.SetColor("_DissolveEdgeColor", cfg.dissolveEdgeColor);
                mat.SetFloat("_DissolveEdgeWidth", cfg.dissolveEdgeWidth);
                mat.SetVector("_DissolveNoiseTiling",
                    new Vector4(cfg.dissolveNoiseTiling.x, cfg.dissolveNoiseTiling.y, 0f, 0f));

                // Toggle rather than a blind assign: with no texture the shader must fall back to
                // procedural noise, otherwise it would sample white and dissolve as a hard pop.
                bool useTex = cfg.dissolveNoise != null;
                mat.SetTexture("_DissolveNoiseTex", cfg.dissolveNoise);
                mat.SetFloat("_UseNoiseTex", useTex ? 1f : 0f);

                mat.SetColor("_HitFlashColor", cfg.hitFlashColor);

                if (save) EditorUtility.SetDirty(mat);
                count++;
            }

            int prefabs = ApplyTimings(cfg, save);

            if (save)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[VatLook] Applied shared look to {count} materials and timings to {prefabs} prefabs " +
                          $"(specSteps={cfg.specSteps}, tiling={cfg.dissolveNoiseTiling}, " +
                          $"dissolve={cfg.dissolveDuration}s, flash={cfg.hitFlashDuration}s, " +
                          $"noise={(cfg.dissolveNoise != null ? cfg.dissolveNoise.name : "procedural")}).");
            }
            return count;
        }

        /// <summary>
        /// Pushes the dissolve/flash durations into every enemy prefab's ZombieBase.
        ///
        /// These live on the component rather than the material because they drive coroutines, not
        /// shader constants - but they are still part of the shared look, so the config owns them
        /// and this keeps all 15 prefabs from drifting apart.
        /// </summary>
        static int ApplyTimings(VatLookConfig cfg, bool save)
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemies" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ENM_")) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var zb = go != null ? go.GetComponent<ZombieBase>() : null;
                if (zb == null) continue;

                var so = new SerializedObject(zb);
                var dissolve = so.FindProperty("dissolveDuration");
                var flash = so.FindProperty("hitFlashDuration");
                if (dissolve != null) dissolve.floatValue = cfg.dissolveDuration;
                if (flash != null) flash.floatValue = cfg.hitFlashDuration;

                // returnToPoolDelay must outlast the dissolve or the corpse is pooled mid-fade.
                var poolDelay = so.FindProperty("returnToPoolDelay");
                if (poolDelay != null && poolDelay.floatValue < cfg.dissolveDuration + 0.3f)
                    poolDelay.floatValue = cfg.dissolveDuration + 0.8f;

                if (so.ApplyModifiedPropertiesWithoutUndo() && save) EditorUtility.SetDirty(go);
                count++;
            }
            return count;
        }

        /// <summary>Every material that lives as a sub-asset of a baked VAT data asset.</summary>
        public static IEnumerable<Material> EnemyMaterials()
        {
            if (!AssetDatabase.IsValidFolder(VatEnemyDir)) yield break;

            foreach (var guid in AssetDatabase.FindAssets("t:VAT_AnimationData", new[] { VatEnemyDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (obj is Material m) yield return m;
            }
        }

        // ── Generated shared textures ────────────────────────────────────────────────────────

        /// <summary>Fractal value noise baked to a tiling greyscale texture. Generated rather than
        /// imported so the project has a working default with no vendor dependency; the config field
        /// accepts any replacement.</summary>
        public static Texture2D EnsureNoiseTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
            if (existing != null) return existing;

            EnsureFolder(System.IO.Path.GetDirectoryName(NoisePath).Replace('\\', '/'));

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Three octaves of tiling Perlin. Tiling matters: the mesh UVs are tiled by
                // _DissolveNoiseScale, so a non-tiling texture would show hard seams mid-burn.
                float v = 0f, amp = 0.5f, freq = 4f;
                for (int o = 0; o < 3; o++)
                {
                    v += amp * TilingPerlin(x / (float)size, y / (float)size, freq);
                    amp *= 0.5f;
                    freq *= 2f;
                }
                v = Mathf.Clamp01(v + 0.25f);
                pixels[y * size + x] = new Color(v, v, v, 1f);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(NoisePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(NoisePath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(NoisePath);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = false;   // it is a mask, not colour
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
        }

        /// <summary>Perlin that wraps at the tile edge, by blending the four wrapped samples.</summary>
        static float TilingPerlin(float u, float v, float freq)
        {
            float x = u * freq, y = v * freq;
            float a = Mathf.PerlinNoise(x, y);
            float b = Mathf.PerlinNoise(x - freq, y);
            float c = Mathf.PerlinNoise(x, y - freq);
            float d = Mathf.PerlinNoise(x - freq, y - freq);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Soft radial blob used as a fake contact shadow under every enemy.</summary>
        public static Texture2D EnsureShadowTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(ShadowTexPath);
            if (existing != null) return existing;

            EnsureFolder(System.IO.Path.GetDirectoryName(ShadowTexPath).Replace('\\', '/'));

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                // smoothstep falloff, opaque core fading to nothing at the rim
                float a = 1f - Mathf.SmoothStep(0.25f, 1f, d);
                pixels[y * size + x] = new Color(0f, 0f, 0f, a);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(ShadowTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ShadowTexPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(ShadowTexPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(ShadowTexPath);
        }

        /// <summary>
        /// The ONE material every enemy's blob shadow shares.
        ///
        /// Unlit + transparent + ZWrite off so it never fights the ground depth, and
        /// GPU-instanced so a screen full of enemies still batches their shadows into few draws.
        /// </summary>
        public static Material EnsureShadowMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);
            if (mat == null)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(ShadowMatPath).Replace('\\', '/'));
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, ShadowMatPath);
            }

            var tex = EnsureShadowTexture();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.45f));

            // URP Unlit transparent setup
            mat.SetFloat("_Surface", 1f);            // 0 opaque, 1 transparent
            mat.SetFloat("_Blend", 0f);              // alpha
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.enableInstancing = true;

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
