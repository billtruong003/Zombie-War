#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BillGameCore.Editor
{
    public class BillSetupWizard : EditorWindow
    {
        private string _name = "MyGame";
        private bool _sample = true;

        [MenuItem("BillGameCore/Setup Project %#g")]
        static void Open() => GetWindow<BillSetupWizard>("BillGameCore Setup").minSize = new Vector2(350, 380);

        [MenuItem("BillGameCore/Open Config")]
        static void OpenCfg()
        {
            var c = Resources.Load<BillBootstrapConfig>("BillBootstrapConfig");
            if (c) Selection.activeObject = c;
            else EditorUtility.DisplayDialog("BillGameCore", "Config not found. Run Setup first.", "OK");
        }

        [MenuItem("BillGameCore/Tools/Add Scene to Build")]
        static void AddScene()
        {
            var s = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(s.path)) { EditorUtility.DisplayDialog("BillGameCore", "Save scene first.", "OK"); return; }
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(x => x.path == s.path)) return;
            list.Add(new EditorBuildSettingsScene(s.path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            GUILayout.Label("BillGameCore Setup", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Creates folders, bootstrap scene, config, and build settings.", MessageType.Info);
            EditorGUILayout.Space(8);

            _name = EditorGUILayout.TextField("Project Name", _name);
            _sample = EditorGUILayout.Toggle("Create Sample Scene", _sample);

            bool hasFusion = System.AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.StartsWith("Fusion.Runtime") || a.FullName.StartsWith("Fusion.Unity"));
            EditorGUI.BeginDisabledGroup(true); EditorGUILayout.Toggle("Photon Fusion Detected", hasFusion); EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(12);
            GUI.backgroundColor = new Color(.3f, .85f, .3f);
            if (GUILayout.Button("Setup Project", GUILayout.Height(36))) RunSetup(hasFusion);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(16);
            GUILayout.Label("Status", EditorStyles.boldLabel);
            Status("Config", Resources.Load("BillBootstrapConfig") != null);
            Status("Bootstrap Scene", File.Exists("Assets/_Game/Scenes/00_Bootstrap.unity"));
            Status("PHOTON_FUSION", PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)).Contains("PHOTON_FUSION"));
        }

        void Status(string label, bool ok) { EditorGUILayout.BeginHorizontal(); GUILayout.Label(ok ? "  OK" : "  --", GUILayout.Width(30)); GUILayout.Label(label); EditorGUILayout.EndHorizontal(); }

        void RunSetup(bool hasFusion)
        {
            foreach (var d in new[] { "Assets/_Game", "Assets/_Game/Scenes", "Assets/_Game/Scripts", "Assets/_Game/Prefabs", "Assets/_Game/Audio", "Assets/Resources", "Assets/Resources/Pools", "Assets/Resources/Configs" })
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);

            if (!File.Exists("Assets/Resources/BillBootstrapConfig.asset"))
            {
                var cfg = ScriptableObject.CreateInstance<BillBootstrapConfig>();
                cfg.enforceBootstrapScene = true; cfg.returnToEditSceneInEditor = true; cfg.targetFrameRate = 60;
                AssetDatabase.CreateAsset(cfg, "Assets/Resources/BillBootstrapConfig.asset");
            }

            if (!File.Exists("Assets/_Game/Scenes/00_Bootstrap.unity"))
            {
                var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(s, "Assets/_Game/Scenes/00_Bootstrap.unity");
            }

            string samplePath = $"Assets/_Game/Scenes/01_{_name}_Main.unity";
            if (_sample && !File.Exists(samplePath))
            {
                var s = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(s, samplePath);
                EditorSceneManager.CloseScene(s, true);
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(x => x.path == "Assets/_Game/Scenes/00_Bootstrap.unity");
            scenes.Insert(0, new EditorBuildSettingsScene("Assets/_Game/Scenes/00_Bootstrap.unity", true));
            if (_sample && File.Exists(samplePath) && !scenes.Any(x => x.path == samplePath))
                scenes.Add(new EditorBuildSettingsScene(samplePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            if (hasFusion)
            {
                var g = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
                var d = PlayerSettings.GetScriptingDefineSymbolsForGroup(g);
                if (!d.Contains("PHOTON_FUSION")) PlayerSettings.SetScriptingDefineSymbolsForGroup(g, d + ";PHOTON_FUSION");
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("BillGameCore", "Setup complete! Hit Play to test.", "OK");
        }
    }
}
#endif
