using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Dựng lại scene <c>WorldStreamingLab</c> từ code.
    ///
    /// Lý do build bằng script chứ không dựng tay: scene lab phải tái tạo được y hệt sau mỗi lần đổi
    /// cấu trúc, và nó tuyệt đối không được đụng vào scene production đang mở. Hàm này dựng scene ở chế
    /// độ additive, chỉ save đúng scene mới, rồi đóng lại — scene đang mở của người dùng không bị chạm.
    /// </summary>
    public static class WorldStreamingLabBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/WorldStreamingLab.unity";
        public const string ConfigPath = "Assets/_Project/Data/World/WorldStreamingConfig.asset";
        public const string GroundMaterialPath = "Assets/_Project/Art/Materials/M_WorldStreamingGround.mat";
        public const string ProbeMaterialPath = "Assets/_Project/Art/Materials/M_LabProbe.mat";
        public const string SolidDecorMaterialPath = "Assets/_Project/Art/Materials/M_WorldStreamingSolidDecor.mat";
        public const string FoliageMaterialPath = "Assets/_Project/Art/Materials/M_WorldStreamingFoliage.mat";
        public const string OutlineProfilePath = "Assets/Settings/SampleSceneProfile.asset";

        /// <summary>
        /// Texture nguồn được chọn sau khi xem thật từng ảnh, không phải đoán qua tên file.
        ///
        /// Vendor texture chỉ dùng làm NGUỒN: material và shader đều là của project. Không đụng vào
        /// import setting của vendor — bốn normal map trong pack đã sẵn `textureType: NormalMap`.
        /// </summary>
        private const string DryAlbedo = "Assets/Cartoon_Texture_Pack/DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png";
        private const string DryNormal = "Assets/Cartoon_Texture_Pack/DIRT/Dirt_Path/Textures/Dirt_Path_Normal.png";
        private const string GrassAlbedo = "Assets/Cartoon_Texture_Pack/GRASS/GRASS_Dense/GRASS_Dense_Tint_02/Textures/Grass_Dense_Tint_02_Base_Basecolor_A.png";
        private const string GrassNormal = "Assets/Cartoon_Texture_Pack/GRASS/GRASS_Dense/GRASS_Dense_Tint_02/Textures/Grass_Dense_Tint_02_Base_Normal.png";
        private const string SandAlbedo = "Assets/Cartoon_Texture_Pack/SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png";
        private const string SandNormal = "Assets/Cartoon_Texture_Pack/SAND/SAND_Beach/Textures/Sand_Beach_Base_Normal.png";
        private const string RockAlbedo = "Assets/Cartoon_Texture_Pack/ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_B_Basecolor.png";
        private const string RockNormal = "Assets/Cartoon_Texture_Pack/ROCKS/ROCKS_Volcanic/Textures/Rock_Volcanic_B_Normal.png";
        private const string NoiseTexture = "Assets/_Project/Art/Textures/Noise/Noise_Perlin_01.png";

        [MenuItem("ZombieWar/World Streaming/Build Lab Scene", priority = 100)]
        public static void BuildMenu()
        {
            string path = Build();
            Debug.Log($"[WorldStreaming] Đã dựng lab scene: {path}");
            EditorUtility.DisplayDialog("World Streaming Lab", $"Đã dựng scene:\n{path}", "OK");
        }

        [MenuItem("ZombieWar/World Streaming/Open Lab Scene", priority = 101)]
        public static void OpenMenu()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning($"[WorldStreaming] Chưa có {ScenePath}. Chạy 'Build Lab Scene' trước.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>Dựng (hoặc dựng lại) scene lab và trả về đường dẫn asset.</summary>
        public static string Build()
        {
            // Tạo asset trước, nhưng KHÔNG giữ tham chiếu qua lời gọi NewScene: NewScene ở chế độ Single
            // có dọn asset không còn ai dùng, và tham chiếu lấy trước đó sẽ thành null.
            EnsureConfig();
            EnsureGroundMaterial();
            EnsureProbeMaterial();
            EnsureSolidDecorMaterial();
            EnsureFoliageMaterial();

            Scene previousActive = SceneManager.GetActiveScene();
            bool additive = CanBuildAdditively();

            Scene labScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                additive ? NewSceneMode.Additive : NewSceneMode.Single);
            labScene.name = "WorldStreamingLab";

            try
            {
                // Mọi GameObject tạo mới rơi vào active scene — chuyển active sang scene lab để không
                // có một object nào lọt vào scene người dùng đang mở.
                SceneManager.SetActiveScene(labScene);
                Populate(EnsureConfig(), EnsureGroundMaterial(), EnsureProbeMaterial(),
                    EnsureSolidDecorMaterial(), EnsureFoliageMaterial());
            }
            finally
            {
                if (additive && previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(labScene, ScenePath);
            if (additive) EditorSceneManager.CloseScene(labScene, true);
            AssetDatabase.Refresh();

            return ScenePath;
        }

        /// <summary>
        /// Chế độ additive là mặc định vì nó không đụng vào scene người dùng đang mở. Nhưng Unity từ
        /// chối tạo scene additive khi đang có scene untitled, và ta cũng không thể ghi đè lên chính
        /// scene lab đang mở — hai trường hợp đó phải dựng ở chế độ Single.
        /// </summary>
        private static bool CanBuildAdditively()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (string.IsNullOrEmpty(scene.path))
                {
                    if (scene.isDirty)
                        throw new System.InvalidOperationException(
                            "[WorldStreaming] Đang có scene untitled chưa lưu. Lưu hoặc đóng nó trước khi dựng lab.");

                    return false;
                }

                if (scene.path == ScenePath) return false;
            }

            return true;
        }

        private static void Populate(WorldStreamingConfig config, Material diagnosticMaterial, Material probeMaterial,
            Material solidDecorMaterial, Material foliageMaterial)
        {
            // --- PlayerProbe -----------------------------------------------------------------
            var probe = new GameObject("PlayerProbe");
            probe.transform.position = new Vector3(4f, 1f, 4f);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Sphere";
            sphere.transform.SetParent(probe.transform, false);
            sphere.transform.localScale = Vector3.one * 2f;
            sphere.GetComponent<MeshRenderer>().sharedMaterial = probeMaterial;

            // Probe di chuyển kinematic; bỏ collider để scene chỉ còn ĐÚNG MỘT collider gameplay.
            Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());

            // --- StreamingWorld --------------------------------------------------------------
            WorldStreamingRig.Rig rig = WorldStreamingRig.Build(config, probe.transform, diagnosticMaterial,
                solidDecorMaterial, foliageMaterial, initializeOnStart: true);

            var controller = probe.AddComponent<WorldStreamingLabController>();
            controller.SetManager(rig.Manager);

            // --- Camera ----------------------------------------------------------------------
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetParent(probe.transform, false);
            // M2B ha camera xuong: o do cao cu (110 m) ca ring nam gon trong khung nhung cay co
            // nho den muc khong danh gia duoc. Do cao nay doc duoc ca hoa tiet dat lan tan cay.
            cameraGO.transform.localPosition = new Vector3(0f, 42f, -34f);
            cameraGO.transform.localRotation = Quaternion.Euler(48f, 0f, 0f);

            var camera = cameraGO.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
            cameraGO.AddComponent<AudioListener>();

            // Fullscreen outline can only be judged when an active Volume feeds the renderer feature.
            // The RP assets already request depth; the lab previously omitted the Volume entirely.
            AddStylizedOutlineVolume();

            // --- Lighting --------------------------------------------------------------------
            var lighting = new GameObject("Lighting");
            var lightGO = new GameObject("Directional Light");
            lightGO.transform.SetParent(lighting.transform, false);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);
            RenderSettings.fog = false;

            // --- Debug -----------------------------------------------------------------------
            var debug = new GameObject("Debug");
            var debugViewGO = new GameObject("WorldStreamingDebugView");
            debugViewGO.transform.SetParent(debug.transform, false);
            debugViewGO.AddComponent<WorldStreamingDebugView>().SetManager(rig.Manager);
        }

        private static WorldStreamingConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<WorldStreamingConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                config = ScriptableObject.CreateInstance<WorldStreamingConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            // Palette do DecorationAuthoring bake ra; noi lai o day de scene khong bao gio thieu no.
            var palette = AssetDatabase.LoadAssetAtPath<DecorationPalette>(DecorationAuthoring.PalettePath);
            if (palette != null && config.DecorationPalette != palette)
            {
                config.SetDecorationPalette(palette);
                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>Material trang tri dac dung chung. Mot cho ca ring, khong instance theo chunk.</summary>
        public static Material EnsureSolidDecorMaterial() =>
            EnsureDecorationMaterial(SolidDecorMaterialPath, ChunkDiagnosticAssets.SolidDecorShaderName,
                DecorationAuthoring.SolidAtlasPath, cutoff: -1f);

        /// <summary>Material foliage dung chung. Alpha clipping, khong phai alpha blending.</summary>
        public static Material EnsureFoliageMaterial() =>
            EnsureDecorationMaterial(FoliageMaterialPath, ChunkDiagnosticAssets.FoliageShaderName,
                DecorationAuthoring.FoliageAtlasPath, cutoff: 0.45f);

        private static Material EnsureDecorationMaterial(string materialPath, string shaderName, string atlasPath, float cutoff)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[WorldStreaming] Khong tim thay shader '{shaderName}'.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            if (atlas == null) Debug.LogError($"[WorldStreaming] Thieu atlas '{atlasPath}'. Chay 'Rebuild Decoration Assets' truoc.");
            else material.SetTexture("_BaseMap", atlas);

            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_TintScale", 2f);
            if (cutoff >= 0f) material.SetFloat("_Cutoff", cutoff);
            material.SetFloat("_ToonSteps", 3f);
            material.SetFloat("_ToonSoftness", 0.035f);
            material.SetFloat("_AmbientBoost", 0.45f);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Material nền dùng chung cho cả 25 chunk. Nếu asset đã tồn tại thì sửa tại chỗ để scene và
        /// mọi tham chiếu cũ không bị đứt; nếu chưa có thì tạo mới. Cả hai đường đều nạp lại đầy đủ
        /// texture và thông số, nên material trên đĩa luôn khớp với ý định trong code.
        /// </summary>
        public static Material EnsureGroundMaterial()
        {
            Shader shader = Shader.Find(ChunkDiagnosticAssets.GroundShaderName);
            if (shader == null)
            {
                Debug.LogError($"[WorldStreaming] Không tìm thấy shader '{ChunkDiagnosticAssets.GroundShaderName}'.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GroundMaterialPath));
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, GroundMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            ConfigureGroundMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void ConfigureGroundMaterial(Material material)
        {
            ChunkDiagnosticAssets.ApplyDiagnosticPalette(material);

            // Canonical cartoon palette: darker midtones leave headroom for the main light and keep
            // biome regions readable as color blocks instead of washed PBR albedo.
            material.SetColor("_DryDebugColor",   new Color(0.46f, 0.29f, 0.14f));
            material.SetColor("_GrassDebugColor", new Color(0.22f, 0.48f, 0.15f));
            material.SetColor("_SandDebugColor",  new Color(0.72f, 0.55f, 0.24f));
            material.SetColor("_RockDebugColor",  new Color(0.32f, 0.38f, 0.46f));

            AssignTexture(material, "_DryTex", DryAlbedo);
            AssignTexture(material, "_GrassTex", GrassAlbedo);
            AssignTexture(material, "_SandTex", SandAlbedo);
            AssignTexture(material, "_RockTex", RockAlbedo);
            AssignTexture(material, "_DryNormal", DryNormal);
            AssignTexture(material, "_GrassNormal", GrassNormal);
            AssignTexture(material, "_SandNormal", SandNormal);
            AssignTexture(material, "_RockNormal", RockNormal);
            AssignTexture(material, "_NoiseTex", NoiseTexture);

            // Tiling tính bằng mét cho một lần lặp, chốt sau khi soi ảnh chụp thật.
            // Bộ số đầu tiên (6.5 / 4.5 / 9 / 5.5) cho ra một lưới lặp lại nhìn thấy rõ ở độ cao
            // camera của lab — phải nới rộng ra mới hết đọc thành ô bàn cờ.
            material.SetFloat("_DryTiling", 13f);
            material.SetFloat("_GrassTiling", 9f);
            material.SetFloat("_SandTiling", 17f);
            material.SetFloat("_RockTiling", 11f);

            // Tint kéo bốn lớp về cùng một dải giá trị, tránh việc một lớp trông như lấy từ game khác.
            material.SetColor("_DryTint", new Color(1.00f, 0.96f, 0.90f));
            material.SetColor("_GrassTint", new Color(0.95f, 1.00f, 0.88f));
            material.SetColor("_SandTint", new Color(1.00f, 0.97f, 0.88f));
            material.SetColor("_RockTint", new Color(0.94f, 0.95f, 0.97f));

            material.SetFloat("_BlendSharpness", 4.0f);
            material.SetFloat("_WeightJitter", 0.24f);
            material.SetFloat("_MacroScale", 190f);
            material.SetFloat("_MacroStrength", 0.06f);
            material.SetFloat("_DetailScale", 23f);
            material.SetFloat("_TextureStrength", 0.28f);
            material.SetFloat("_ToonSteps", 3f);
            material.SetFloat("_ToonSoftness", 0.035f);
            material.SetFloat("_LightWrap", 0.55f);
            material.SetFloat("_AmbientBoost", 0.35f);
            material.SetFloat("_NormalStrength", 0f);
            material.SetVector("_WorldOriginOffset", Vector4.zero);

            // Normal-map relief was technically correct but pushed the flat stylized plane toward PBR.
            // Keep source maps assigned for later A/B, but the canonical lab presentation starts flat.
            SetNormalMapEnabled(material, false);
        }

        /// <summary>Bật/tắt normal map qua đúng một shader keyword — hai biến thể, không hơn.</summary>
        public static void SetNormalMapEnabled(Material material, bool enabled)
        {
            if (material == null) return;

            material.SetFloat("_UseNormalMap", enabled ? 1f : 0f);
            if (enabled) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
        }

        private static void AssignTexture(Material material, string property, string assetPath)
        {
            if (!material.HasProperty(property))
            {
                Debug.LogError($"[WorldStreaming] Shader thiếu property '{property}'.");
                return;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                Debug.LogError($"[WorldStreaming] Không tìm thấy texture '{assetPath}' cho '{property}'.");
                return;
            }

            material.SetTexture(property, texture);
        }

        private static Material EnsureProbeMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(ProbeMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[WorldStreaming] Không tìm thấy shader URP Lit. Project có đúng đang dùng URP không?");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ProbeMaterialPath));
            var material = new Material(shader) { enableInstancing = true };
            material.SetColor("_BaseColor", new Color(1f, 0.25f, 0.1f));

            AssetDatabase.CreateAsset(material, ProbeMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void AddStylizedOutlineVolume()
        {
            // _Project.Editor intentionally has no hard dependency on Core RP. Resolve the installed
            // URP Volume component by its assembly-qualified name and author it through SerializedObject.
            System.Type volumeType = System.Type.GetType(
                "UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            if (volumeType == null)
            {
                Debug.LogError("[WorldStreaming] Khong tim thay URP Volume component.");
                return;
            }

            var volumeGO = new GameObject("StylizedOutlineVolume");
            Component volume = volumeGO.AddComponent(volumeType);
            var serialized = new SerializedObject(volume);
            serialized.FindProperty("m_IsGlobal").boolValue = true;
            serialized.FindProperty("priority").floatValue = 100f;
            serialized.FindProperty("sharedProfile").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Object>(OutlineProfilePath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (serialized.FindProperty("sharedProfile").objectReferenceValue == null)
                Debug.LogError($"[WorldStreaming] Thieu outline profile '{OutlineProfilePath}'.");
        }
    }
}
