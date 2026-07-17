#if UNITY_EDITOR
using System.IO;
using System.Linq;
using BillGameCore;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZombieWar.Editor
{
    /// One-shot builder for the playable skeleton: Bill bootstrap config + the three-scene additive
    /// flow (Bootstrap -> Menu -> Map_Level1) + a controllable player prefab on a baked NavMesh plane.
    /// Run from the menu or headless via: -executeMethod ZombieWar.Editor.SceneFlowBuilder.BuildAll
    public static class SceneFlowBuilder
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string MatDir = "Assets/_Project/Art/Materials";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";
        private const string CharacterFbxPath = "Assets/ThirdParty/Layer Lab/3D Casual Character/3D Characters Pro - Fantasy/FBX/Character/Character_Basic.fbx";
        private const string LocomotionControllerPath = "Assets/ThirdParty/Layer Lab/3D Casual Character/Demo_v2/AnimationController/AnimationController_Demo.controller";
        private const string BootstrapScene = ScenesDir + "/Bootstrap.unity";
        private const string MenuScene = ScenesDir + "/Menu.unity";
        private const string MapScene = ScenesDir + "/Map_Level1.unity";

        [MenuItem("ZombieWar/Build Scene Flow")]
        public static void BuildAll()
        {
            Log("=== SceneFlowBuilder START ===");

            // We replace the open scene 3x below; give the user a chance to save first so nothing
            // in their currently-open scene is silently lost when this runs from the menu.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Log("Aborted by user (unsaved scenes).");
                return;
            }

            EnsureDir(ScenesDir); EnsureDir(PrefabsDir); EnsureDir(MatDir);

            EnsureConfig();
            Material groundMat = MakeMaterial("GroundMat", new Color(0.22f, 0.24f, 0.26f));
            GameObject playerPrefab = BuildPlayerPrefab();

            BuildBootstrapScene();
            BuildMenuScene();
            BuildMapScene(playerPrefab, groundMat);

            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("=== SceneFlowBuilder DONE ===");
        }

        // ---------------------------------------------------------------- Config

        private static void EnsureConfig()
        {
            const string path = "Assets/Resources/BillBootstrapConfig.asset";
            if (File.Exists(path)) { Log("Config already exists."); return; }
            EnsureDir("Assets/Resources");
            var cfg = ScriptableObject.CreateInstance<BillBootstrapConfig>();
            cfg.enforceBootstrapScene = false;      // BootstrapEntry drives the flow, don't hard-gate
            cfg.returnToEditSceneInEditor = true;
            cfg.defaultGameScene = "";              // empty -> BootstrapEntry handles entry additively
            cfg.targetFrameRate = 60;
            cfg.includeDebugOverlay = true;
            cfg.includeCheatConsole = true;
            AssetDatabase.CreateAsset(cfg, path);
            Log("Created BillBootstrapConfig.asset");
        }

        // ---------------------------------------------------------------- Player prefab

        private static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");
            root.tag = "Player";
            root.transform.position = Vector3.zero;

            var rb = root.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var col = root.AddComponent<CapsuleCollider>();
            col.height = 2f; col.radius = 0.5f; col.center = new Vector3(0f, 1f, 0f);

            root.AddComponent<PlayerMovement>();
            root.AddComponent<Health>();
            root.AddComponent<Weapon>();
            root.AddComponent<BombThrower>();

            // Character model: Layer Lab humanoid. Animator + Avatar go on the ROOT (alongside
            // RigBuilder) so the Animation Rigging constraints resolve humanoid bones under the nested
            // model with a single Animator/RigBuilder pair (required by Animation Rigging).
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbxPath);
            if (fbx == null)
            {
                LogErr("Character FBX not found: " + CharacterFbxPath);
            }
            else
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                model.name = "CharacterModel";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;

                // Lift the humanoid Avatar onto a root Animator; drop the model's own Animator.
                var modelAnimator = model.GetComponent<Animator>();
                Avatar avatar = modelAnimator != null ? modelAnimator.avatar : null;
                if (modelAnimator != null) Object.DestroyImmediate(modelAnimator);

                var animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(LocomotionControllerPath);
                if (controller != null) animator.runtimeAnimatorController = controller;

                SetRef(root.GetComponent<PlayerMovement>(), "animator", animator);

                // Multi-Aim chest + Two-Bone IK hands + weapon socket; wires Weapon + WeaponIKController.
                ZombieWar.EditorTools.PlayerRigBuilder.BuildWeaponRig(root);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            Log("Built Player prefab -> " + PlayerPrefabPath);
            return prefab;
        }

        // ---------------------------------------------------------------- Bootstrap scene

        private static void BuildBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("BootstrapEntry");
            go.AddComponent<BootstrapEntry>();
            EditorSceneManager.SaveScene(scene, BootstrapScene);
            Log("Built Bootstrap scene.");
        }

        // ---------------------------------------------------------------- Menu scene

        private static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Menu Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.gameObject.AddComponent<AudioListener>();

            var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);

            MakeText(canvasGo.transform, "Title", "ZOMBIE WAR", 72, new Vector2(0f, 400f), new Vector2(900f, 200f));
            var play = MakeButton(canvasGo.transform, "PlayButton", "PLAY", new Vector2(0f, 0f), new Color(0.25f, 0.7f, 0.3f));
            var quit = MakeButton(canvasGo.transform, "QuitButton", "QUIT", new Vector2(0f, -220f), new Color(0.6f, 0.25f, 0.25f));

            var controller = canvasGo.AddComponent<MainMenuController>();
            SetRef(controller, "playButton", play);
            SetRef(controller, "quitButton", quit);

            EnsureEventSystem();
            EditorSceneManager.SaveScene(scene, MenuScene);
            Log("Built Menu scene.");
        }

        // ---------------------------------------------------------------- Map scene

        private static void BuildMapScene(GameObject playerPrefab, Material groundMat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ground plane: default plane is 10x10 units -> scale 10 = 100x100.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            if (groundMat != null) ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);

            // NavMesh bake over the whole scene.
            var navRoot = new GameObject("NavMesh");
            var surface = navRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();
            Log("Baked NavMesh.");

            // Player spawn marker - the player is instantiated at runtime by PlayerSpawner, keeping the
            // map authorable as pure environment + markers (see PlayerSpawner.cs).
            var spawnPointGo = new GameObject("PlayerSpawnPoint");
            spawnPointGo.transform.position = new Vector3(0f, 0f, 0f);
            spawnPointGo.AddComponent<PlayerSpawnPoint>(); // isPrimary defaults to true

            // Wave system: ZombieManager (3-tier distance authority, see GAMEPLAY_DESIGN.md mục 4) +
            // WaveDirector (core-loop driver; RequireComponent auto-adds ZombieSpawner). The level's
            // waves are authored as the WD_Level1 asset - designer data, no spawn logic in scene.
            new GameObject("ZombieManager").AddComponent<ZombieManager>();

            var waveGo = new GameObject("WaveDirector");
            var director = waveGo.AddComponent<WaveDirector>();
            var waveData = AssetDatabase.LoadAssetAtPath<WaveData>("Assets/_Project/Data/Waves/WD_Level1.asset");
            if (waveData != null) SetRef(director, "waveData", waveData);
            else LogErr("WD_Level1.asset not found - WaveDirector will not run.");

            // Camera follow
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 12f, -8f);
            cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            cam.gameObject.AddComponent<AudioListener>();
            var follow = cam.gameObject.AddComponent<CameraFollow>();
            // follow.target is wired at runtime by PlayerSpawner to the spawned player.

            // HUD + mobile joystick. The joystick is wired into the spawned player at runtime.
            var hud = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hud.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            hud.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
            var joystick = BuildJoystick(hud.transform);
            BuildHud(hud.transform);

            // PlayerSpawner instantiates the player prefab at the spawn point at runtime and wires the
            // persistent scene objects (camera follow + joystick) into it.
            var spawnerGo = new GameObject("PlayerSpawner");
            var spawner = spawnerGo.AddComponent<PlayerSpawner>();
            SetRef(spawner, "playerPrefab", playerPrefab);
            SetRef(spawner, "cameraFollow", follow);
            SetRef(spawner, "joystick", joystick);

            EnsureEventSystem();
            EditorSceneManager.SaveScene(scene, MapScene);
            Log("Built Map_Level1 scene.");
        }

        private static VirtualJoystick BuildJoystick(Transform parent)
        {
            var bgGo = new GameObject("Joystick_BG", typeof(Image), typeof(VirtualJoystick));
            bgGo.transform.SetParent(parent, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0f, 0f);
            bgRt.anchoredPosition = new Vector2(280f, 280f);
            bgRt.sizeDelta = new Vector2(320f, 320f);
            bgGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            var handleGo = new GameObject("Handle", typeof(Image));
            handleGo.transform.SetParent(bgGo.transform, false);
            var hRt = handleGo.GetComponent<RectTransform>();
            hRt.anchorMin = hRt.anchorMax = new Vector2(0.5f, 0.5f);
            hRt.sizeDelta = new Vector2(140f, 140f);
            handleGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);

            var joy = bgGo.GetComponent<VirtualJoystick>();
            SetRef(joy, "background", bgRt);
            SetRef(joy, "handle", hRt);
            return joy;
        }

        // HUD widgets (wave label, zombie count, health bar, game-over) driven by HudController
        // through the decoupled Bill.Events bus — no direct gameplay references.
        private static void BuildHud(Transform parent)
        {
            var waveLabel = MakeAnchoredText(parent, "WaveLabel", "WAVE 1 / 5", 52,
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(700f, 90f));
            var zombieLabel = MakeAnchoredText(parent, "ZombieLabel", "ZOMBIES: 0", 40,
                new Vector2(1f, 1f), new Vector2(-40f, -160f), new Vector2(520f, 70f), TextAnchor.MiddleRight);

            // Health bar: dark frame + red fill (left-anchored, Filled horizontal) + label.
            var frameGo = new GameObject("HealthFrame", typeof(Image));
            frameGo.transform.SetParent(parent, false);
            var frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = frameRt.anchorMax = new Vector2(0f, 1f);
            frameRt.pivot = new Vector2(0f, 1f);
            frameRt.anchoredPosition = new Vector2(40f, -160f);
            frameRt.sizeDelta = new Vector2(520f, 56f);
            frameGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var fillGo = new GameObject("HealthFill", typeof(Image));
            fillGo.transform.SetParent(frameGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(6f, 6f);
            fillRt.offsetMax = new Vector2(-6f, -6f);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = new Color(0.85f, 0.2f, 0.2f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            var healthLabel = MakeText(fillGo.transform, "HealthLabel", "100 / 100", 32,
                Vector2.zero, new Vector2(520f, 56f));

            var gameOver = BuildOverlay(parent, "GameOverPanel", "GAME OVER", new Color(0.5f, 0f, 0f, 0.6f));
            var victory = BuildOverlay(parent, "VictoryPanel", "YOU SURVIVED", new Color(0f, 0.4f, 0.1f, 0.6f));

            // Weapon slot (bottom-right): tap = switch weapon; inner icon spins 1 full turn while reloading.
            var wSlot = new GameObject("WeaponButton", typeof(Image), typeof(Button));
            wSlot.transform.SetParent(parent, false);
            var wSlotRt = wSlot.GetComponent<RectTransform>();
            wSlotRt.anchorMin = wSlotRt.anchorMax = wSlotRt.pivot = new Vector2(1f, 0f);
            wSlotRt.anchoredPosition = new Vector2(-70f, 340f);
            wSlotRt.sizeDelta = new Vector2(190f, 190f);
            wSlot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);

            var wIconGo = new GameObject("Icon", typeof(Image));
            wIconGo.transform.SetParent(wSlot.transform, false);
            var wIconRt = wIconGo.GetComponent<RectTransform>();
            wIconRt.anchorMin = wIconRt.anchorMax = wIconRt.pivot = new Vector2(0.5f, 0.5f);
            wIconRt.anchoredPosition = Vector2.zero;
            wIconRt.sizeDelta = new Vector2(96f, 150f); // portrait rect so the spin reads clearly
            var wIcon = wIconGo.GetComponent<Image>();
            wIcon.color = new Color(0.95f, 0.82f, 0.28f, 1f); // placeholder tint
            wIcon.raycastTarget = false;

            var wLabelGo = new GameObject("WeaponLabel", typeof(Text));
            wLabelGo.transform.SetParent(wSlot.transform, false);
            var wLabelRt = wLabelGo.GetComponent<RectTransform>();
            wLabelRt.anchorMin = new Vector2(0f, 0f);
            wLabelRt.anchorMax = new Vector2(1f, 0f);
            wLabelRt.pivot = new Vector2(0.5f, 1f);
            wLabelRt.anchoredPosition = new Vector2(0f, -6f);
            wLabelRt.sizeDelta = new Vector2(0f, 90f);
            var wLabel = wLabelGo.GetComponent<Text>();
            wLabel.text = "";
            wLabel.fontSize = 34;
            wLabel.alignment = TextAnchor.UpperCenter;
            wLabel.color = Color.white;
            wLabel.raycastTarget = false;
            wLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var ctrl = parent.gameObject.AddComponent<HudController>();
            SetRef(ctrl, "weaponButton", wSlot.GetComponent<Button>());
            SetRef(ctrl, "weaponIcon", wIcon);
            SetRef(ctrl, "weaponLabel", wLabel);
            SetRef(ctrl, "waveLabel", waveLabel);
            SetRef(ctrl, "zombieLabel", zombieLabel);
            SetRef(ctrl, "healthFill", fillImg);
            SetRef(ctrl, "healthLabel", healthLabel);
            SetRef(ctrl, "gameOverPanel", gameOver);
            SetRef(ctrl, "victoryPanel", victory);
        }

        private static GameObject BuildOverlay(Transform parent, string name, string label, Color tint)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = tint;
            MakeText(go.transform, "Label", label, 96, new Vector2(0f, 120f), new Vector2(900f, 200f));
            go.SetActive(false);
            return go;
        }

        private static Text MakeAnchoredText(Transform parent, string name, string content, int size,
            Vector2 anchor, Vector2 pos, Vector2 dim, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var txt = MakeText(parent, name, content, size, Vector2.zero, dim);
            var rt = txt.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            txt.alignment = align;
            return txt;
        }

        // ---------------------------------------------------------------- Build settings

        private static void RegisterBuildSettings()
        {
            var wanted = new[] { BootstrapScene, MenuScene, MapScene };
            var list = EditorBuildSettings.scenes.Where(s => !wanted.Contains(s.path)).ToList();
            for (int i = 0; i < wanted.Length; i++)
                list.Insert(i, new EditorBuildSettingsScene(wanted[i], true));
            EditorBuildSettings.scenes = list.ToArray();
            Log("Registered build settings (Bootstrap=0, Menu=1, Map=2).");
        }

        // ---------------------------------------------------------------- Helpers

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(520f, 160f);
            go.GetComponent<Image>().color = color;
            MakeText(go.transform, "Label", label, 48, Vector2.zero, new Vector2(520f, 160f));
            return go.GetComponent<Button>();
        }

        private static Text MakeText(Transform parent, string name, string content, int size, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = dim;
            var txt = go.GetComponent<Text>();
            txt.text = content;
            txt.fontSize = size;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            return txt;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void SetRef(Object comp, string prop, Object value)
        {
            var so = new SerializedObject(comp);
            var p = so.FindProperty(prop);
            if (p == null) { LogErr($"Field '{prop}' not found on {comp.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        private static void Log(string m) => Debug.Log("[SceneFlowBuilder] " + m);
        private static void LogErr(string m) => Debug.LogError("[SceneFlowBuilder] " + m);
    }
}
#endif
