using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ZombieWar.Bosses;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Builds the boss lab kit (backlog #39): the Titan's animator (Malbers humanoid idle/run/death +
    /// FreeFighter attacks re-imported as humanoid: axe kick = slam, charge fist = charge, super blast =
    /// roar), the Titan prefab (Synty Kaiju_04 ×3, ~6 m) and an arena wall piece, under
    /// Resources/Dev so the dev-only BossLab can spawn them in a run.
    /// </summary>
    public static class BossLabBuilder
    {
        const string ArtDir = "Assets/_Project/Art/Bosses/Titan";
        const string ResDir = "Assets/_Project/Resources/Dev";
        const string Kaiju = "Assets/Synty/PolygonKaiju/Prefabs/Characters/SM_Chr_Kaiju_04.prefab";
        const string Wall = "Assets/_Project/Art/EnvPalette/Prefabs/KD_barrier.prefab";
        const string FF = "Assets/EEJANAI_Team/FreeFighterAnimations/FBX/";
        const string Malbers = "Assets/ThirdParty/MalbersHumanAnims/";
        public const float TitanScale = 3f;

        [MenuItem("ZombieWar/Dev/Build Boss Lab")]
        public static void Build()
        {
            Directory.CreateDirectory(ArtDir); Directory.CreateDirectory(ResDir);
            var controller = BuildController();
            BuildTitan(controller);
            BuildWall();
            BuildTelegraphMaterial();
            AssetDatabase.SaveAssets();
            Debug.Log("[BossLab] kit built");
        }

        static AnimationClip ClipIn(string path)
        {
            if (path.EndsWith(".anim")) return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp != null && imp.animationType != ModelImporterAnimationType.Human)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.SaveAndReimport();
            }
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            return null;
        }

        static AnimatorController BuildController()
        {
            string path = ArtDir + "/Titan.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            foreach (var t in new[] { "Slam", "Charge", "Roar", "Die" }) ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;

            var idle = sm.AddState("Idle"); idle.motion = ClipIn(Malbers + "Locomotion/Idle.anim");
            var run = sm.AddState("Run"); run.motion = ClipIn(Malbers + "Locomotion/Run.anim"); run.speed = 0.7f;
            sm.defaultState = idle;
            var toRun = idle.AddTransition(run); toRun.hasExitTime = false; toRun.duration = 0.15f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            var toIdle = run.AddTransition(idle); toIdle.hasExitTime = false; toIdle.duration = 0.2f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");

            Attack(sm, idle, "Slam", ClipIn(FF + "axe kick.fbx"), 0.85f);
            Attack(sm, idle, "Charge", ClipIn(FF + "charge fist.fbx"), 0.9f);
            Attack(sm, idle, "Roar", ClipIn(FF + "super blast.fbx"), 1.1f);
            var death = sm.AddState("Death"); death.motion = ClipIn(Malbers + "Deaths/H_Death1.fbx");
            var toDeath = sm.AddAnyStateTransition(death); toDeath.hasExitTime = false; toDeath.duration = 0.1f;
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Die"); toDeath.canTransitionToSelf = false;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void Attack(AnimatorStateMachine sm, AnimatorState idle, string trigger, AnimationClip clip, float speed)
        {
            var s = sm.AddState(trigger); s.motion = clip; s.speed = speed;
            var t = sm.AddAnyStateTransition(s); t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            var back = s.AddTransition(idle); back.hasExitTime = true; back.exitTime = 0.92f; back.duration = 0.15f;
        }

        static void BuildTitan(AnimatorController controller)
        {
            var root = new GameObject("BOSS_Titan");
            try
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kaiju));
                PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                body.transform.SetParent(root.transform, false);
                body.transform.localScale = Vector3.one * TitanScale;
                // Synty's Generic_Basic shader draws nothing in this URP setup: the Titan wears the
                // game's character toon (same light and outline as the player) with the Kaiju texture.
                foreach (var n in new[] { "SM_Chr_Kaiju_01", "SM_Chr_Kaiju_02" })
                {
                    var spare = body.transform.Find(n);
                    if (spare != null) Object.DestroyImmediate(spare.gameObject);   // the shared rig's other kaiju
                }
                var mat = TitanMaterial();
                foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.sharedMaterial = mat;
                var anim = body.GetComponentInChildren<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var cap = root.AddComponent<CapsuleCollider>();
                cap.radius = 1.6f; cap.height = 6f; cap.center = new Vector3(0f, 3f, 0f);
                root.AddComponent<TitanBoss>();
                PrefabUtility.SaveAsPrefabAsset(root, ResDir + "/BOSS_Titan.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Material TitanMaterial()
        {
            string path = ArtDir + "/M_Titan_Kaiju04.mat";
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Character/M_Character_Toon.mat");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(src); AssetDatabase.CreateAsset(mat, path); }
            else mat.CopyPropertiesFromMaterial(src);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FindTexture("Kaiju_04_01_A"));
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static string FindTexture(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D", new[] { "Assets/Synty/PolygonKaiju" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name) return p;
            }
            return null;
        }

        /// <summary>The ground warning's transparent URP Unlit material, in Resources so it ships.</summary>
        static void BuildTelegraphMaterial()
        {
            Directory.CreateDirectory("Assets/_Project/Resources/FX");
            string path = "Assets/_Project/Resources/" + GroundTelegraph.MaterialPath + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3001;
            m.SetColor("_BaseColor", new Color(1f, 0.15f, 0.1f, 0.5f));
            EditorUtility.SetDirty(m);
        }

        static void BuildWall()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Wall);
            var root = new GameObject("BossWall");
            try
            {
                if (src != null)
                {
                    var w = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    PrefabUtility.UnpackPrefabInstance(w, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    w.transform.SetParent(root.transform, false);
                    var b = new Bounds(w.transform.position, Vector3.zero);
                    foreach (var r in w.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    float longest = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
                    w.transform.localScale *= BossArena.SegmentLength / longest;
                    if (b.size.x > b.size.z) w.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // length along z
                }
                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(0.7f, 2.2f, BossArena.SegmentLength); box.center = new Vector3(0f, 1.1f, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, ResDir + "/BossWall.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
