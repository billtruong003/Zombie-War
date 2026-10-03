#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BillGameCore.BillSceneSwitcher
{
    /// <summary>
    /// Play from the bootstrap scene, then come back: remembers the open scene setup, opens the
    /// bootstrap scene, enters Play, and restores the setup when Play stops. The setup survives the
    /// domain reload through SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class BillScenePlayFromBootstrap
    {
        const string Key = "BillSceneSwitcher.ReturnSetup";

        static BillScenePlayFromBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var data = BillSceneSwitcherData.Instance;
            var bootstrap = data.BootstrapScenePath;
            if (string.IsNullOrEmpty(bootstrap) && EditorBuildSettings.scenes.Length > 0) bootstrap = EditorBuildSettings.scenes[0].path;
            if (string.IsNullOrEmpty(bootstrap)) { Debug.LogWarning("[SceneSwitcher] No bootstrap scene set."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var saved = new Setup { scenes = new Entry[setup.Length] };
            for (int i = 0; i < setup.Length; i++)
                saved.scenes[i] = new Entry { path = setup[i].path, isActive = setup[i].isActive, isLoaded = setup[i].isLoaded };
            SessionState.SetString(Key, JsonUtility.ToJson(saved));
            EditorSceneManager.OpenScene(bootstrap, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            var json = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return;
            SessionState.EraseString(Key);
            var setup = JsonUtility.FromJson<Setup>(json);
            if (setup?.scenes == null || setup.scenes.Length == 0) return;
            var restore = new SceneSetup[setup.scenes.Length];
            for (int i = 0; i < restore.Length; i++)
            {
                var e = setup.scenes[i];
                if (string.IsNullOrEmpty(e.path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(e.path) == null) return;
                restore[i] = new SceneSetup { path = e.path, isActive = e.isActive, isLoaded = e.isLoaded };
            }
            EditorApplication.delayCall += () => EditorSceneManager.RestoreSceneManagerSetup(restore);
        }

        [System.Serializable]
        class Setup { public Entry[] scenes; }

        [System.Serializable]
        struct Entry { public string path; public bool isActive, isLoaded; }
    }
}
#endif
