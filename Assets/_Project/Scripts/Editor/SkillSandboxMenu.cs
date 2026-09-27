using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// Skill test bench (M8 review). The sandbox is a copy of the world scene with a SkillSandbox
    /// object added; it still starts from Bootstrap so every service is up, then skips the menu.
    public static class SkillSandboxMenu
    {
        const string World = "Assets/_Project/Scenes/Map_Level1.unity";
        const string Bootstrap = "Assets/_Project/Scenes/Bootstrap.unity";

        [MenuItem("ZombieWar/Dev/Play Skill Sandbox")]
        public static void Play()
        {
            if (EditorApplication.isPlaying) return;
            if (!System.IO.File.Exists(GameFlow.SandboxScenePath)) Build();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool("zw.sandbox", true);
            EditorSceneManager.OpenScene(Bootstrap);
            EditorApplication.EnterPlaymode();
        }

        /// Gun-skin review: the sandbox with the skin turntable (angles, gun and set pickers).
        [MenuItem("ZombieWar/Dev/Play Skin Viewer")]
        public static void PlaySkinViewer()
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetBool("zw.skinviewer", true);
            Play();
        }

        /// (Re)creates the sandbox from the current world scene. Map_Level1 itself is not touched.
        [MenuItem("ZombieWar/Dev/Rebuild Skill Sandbox Scene")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(GameFlow.SandboxScenePath));
            AssetDatabase.DeleteAsset(GameFlow.SandboxScenePath);
            if (!AssetDatabase.CopyAsset(World, GameFlow.SandboxScenePath)) { Debug.LogError("[Sandbox] copy failed"); return; }
            var scene = EditorSceneManager.OpenScene(GameFlow.SandboxScenePath, OpenSceneMode.Additive);
            try
            {
                var go = new GameObject("SkillSandbox", typeof(ZombieWar.Dev.SkillSandbox));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            Debug.Log("[Sandbox] built " + GameFlow.SandboxScenePath);
        }
    }
}
