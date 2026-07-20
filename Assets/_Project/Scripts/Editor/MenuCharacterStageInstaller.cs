using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Author hoá character preview: dựng MenuCharacterPreviewStage.prefab (PreviewPivot +
    /// PreviewCharacter + PreviewCamera + PreviewKeyLight + CameraTarget) + RenderTexture ASSET,
    /// đặt instance vào Menu.unity. Sau bước này KHÔNG còn gì được dựng lúc runtime —
    /// designer chỉnh framing (camera/character/light) trực tiếp trong Scene View/Inspector.
    /// Idempotent: instance/asset đã có thì giữ nguyên (không phá manual edit).
    /// </summary>
    public static class MenuCharacterStageInstaller
    {
        const string ScenePath = "Assets/_Project/Scenes/Menu.unity";
        const string CatalogPath = "Assets/_Project/Data/Character/ModularCostumeCatalog.asset";
        const string BasePrefabName = "Character_Basic";
        const string IdleFbx = "Assets/ThirdParty/Layer Lab/3D Casual Character/Demo_v2/Animation/Anim@Stand_Idle1.FBX";
        const string CtrlPath = "Assets/_Project/Animation/MenuIdle.controller";

        public const string RtPath = "Assets/_Project/UI/RenderTextures/MenuCharacterPreview.renderTexture";
        public const string StagePrefabPath = "Assets/_Project/UI/Prefabs/Preview/MenuCharacterPreviewStage.prefab";
        public const string StageName = "MenuCharacterPreviewStage";

        static readonly Vector3 StageOrigin = new Vector3(600f, 600f, 600f);

        [MenuItem("ZombieWar/UI/Authoring/Ensure Character Preview Stage")]
        public static void Ensure()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (EnsureInOpenScene() == null) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PreviewStage] OK — preview stage authored, RT asset: " + RtPath);
        }

        /// <summary>Ensure stage instance trong scene ĐANG mở (Menu). Không destroy manual edit — chỉ tạo thiếu.</summary>
        public static MenuCharacterStage EnsureInOpenScene()
        {
            var rt = EnsureRenderTexture();
            var prefab = EnsureStagePrefab(rt);
            if (prefab == null) return null;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // dọn stage runtime-build cũ (không có MenuCharacterStage prefab link) — một lần
            foreach (var go in scene.GetRootGameObjects())
            {
                if ((go.name == "MenuCharacterStage" || go.name == "CostumePreview")
                    && PrefabUtility.GetCorrespondingObjectFromSource(go) == null)
                {
                    Debug.Log($"[PreviewStage] Xoá stage runtime cũ '{go.name}'.");
                    Object.DestroyImmediate(go);
                }
            }

            var existing = Object.FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = StageOrigin;
            Debug.Log("[PreviewStage] Đặt instance MenuCharacterPreviewStage vào Menu scene.");
            return instance.GetComponent<MenuCharacterStage>();
        }

        public static RenderTexture EnsureRenderTexture()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (rt != null) return rt;
            Directory.CreateDirectory(Path.GetDirectoryName(RtPath)!);
            rt = new RenderTexture(512, 900, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2,
                name = "MenuCharacterPreview"
            };
            AssetDatabase.CreateAsset(rt, RtPath);
            Debug.Log("[PreviewStage] Tạo RenderTexture asset: " + RtPath);
            return rt;
        }

        static GameObject EnsureStagePrefab(RenderTexture rt)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefabPath);
            if (existing != null) return existing;

            int layer = LayerMask.NameToLayer("CharacterPreview");
            if (layer < 0) { Debug.LogError("[PreviewStage] Layer 'CharacterPreview' không tồn tại."); return null; }

            var ctrl = EnsureIdleController();
            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CatalogPath);
            var baseGuid = AssetDatabase.FindAssets($"{BasePrefabName} t:Prefab")
                .FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == BasePrefabName);
            var basePrefab = string.IsNullOrEmpty(baseGuid) ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(baseGuid));
            if (basePrefab == null) { Debug.LogError($"[PreviewStage] Không thấy prefab {BasePrefabName}"); return null; }

            var root = new GameObject(StageName);
            try
            {
                var pivot = new GameObject("PreviewPivot").transform;
                pivot.SetParent(root.transform, false);

                var character = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                character.name = "PreviewCharacter";
                character.transform.SetParent(pivot, false);
                character.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // quay mặt về camera
                foreach (var t in character.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = layer;   // bake layer trong Editor — không SetLayerRecursive runtime

                var animator = character.GetComponent<Animator>();
                if (animator == null) animator = character.AddComponent<Animator>();
                if (ctrl != null) animator.runtimeAnimatorController = ctrl;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var applier = character.GetComponent<CharacterModularApplier>();
                if (applier == null) applier = character.AddComponent<CharacterModularApplier>();
                var soApplier = new SerializedObject(applier);
                soApplier.FindProperty("catalog").objectReferenceValue = catalog;
                soApplier.ApplyModifiedPropertiesWithoutUndo();

                var camTarget = new GameObject("CameraTarget").transform;
                camTarget.SetParent(root.transform, false);
                camTarget.localPosition = new Vector3(0f, 0.9f, 0f);

                var camGo = new GameObject("PreviewCamera");
                camGo.transform.SetParent(root.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 0.95f, -3.4f);
                camGo.transform.LookAt(root.transform.TransformPoint(camTarget.localPosition));
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = 1 << layer;
                cam.fieldOfView = 24f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 50f;
                cam.allowHDR = false;
                cam.targetTexture = rt;

                var lightGo = new GameObject("PreviewKeyLight");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(1.5f, 3f, -2f);
                lightGo.transform.LookAt(root.transform.TransformPoint(new Vector3(0f, 0.9f, 0f)));
                var lit = lightGo.AddComponent<Light>();
                lit.type = LightType.Directional;
                lit.color = new Color(1f, 0.97f, 0.9f);
                lit.intensity = 1.1f;
                lit.cullingMask = 1 << layer;

                var stage = root.AddComponent<MenuCharacterStage>();
                var so = new SerializedObject(stage);
                so.FindProperty("previewCamera").objectReferenceValue = cam;
                so.FindProperty("previewLight").objectReferenceValue = lit;
                so.FindProperty("characterRoot").objectReferenceValue = character.transform;
                so.FindProperty("modularApplier").objectReferenceValue = applier;
                so.FindProperty("previewTexture").objectReferenceValue = rt;
                so.FindProperty("animator").objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();

                Directory.CreateDirectory(Path.GetDirectoryName(StagePrefabPath)!);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, StagePrefabPath);
                Debug.Log("[PreviewStage] Tạo prefab: " + StagePrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static AnimatorController EnsureIdleController()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (ctrl != null) return ctrl;
            var idleClip = AssetDatabase.LoadAllAssetRepresentationsAtPath(IdleFbx)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (idleClip == null) { Debug.LogWarning($"[PreviewStage] Không thấy idle clip trong {IdleFbx}"); return null; }
            Directory.CreateDirectory("Assets/_Project/Animation");
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(CtrlPath, idleClip);
        }
    }
}
