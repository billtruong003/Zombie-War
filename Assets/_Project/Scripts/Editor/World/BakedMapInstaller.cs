using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Puts the baked-map streamer into the gameplay scene (2026-10-01). The procedural world stays in
    /// the scene, inactive, as the fallback the streamer re-enables when no baked theme can be loaded.
    /// Running it twice changes nothing.
    /// </summary>
    public static class BakedMapInstaller
    {
        const string ScenePath = "Assets/_Project/Scenes/Map_Level1.unity";

        [MenuItem("HordeCall/World/Install Baked Map Streaming (Map_Level1)")]
        public static string Install()
        {
            var prev = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject procedural = null, baked = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "ProceduralWorld") procedural = root;
                if (root.name == "BakedWorld") baked = root;
            }
            if (baked == null) baked = new GameObject("BakedWorld");
            var streamer = baked.GetComponent<ZombieWar.World.BakedMapStreamer>();
            if (streamer == null) streamer = baked.AddComponent<ZombieWar.World.BakedMapStreamer>();
            var so = new SerializedObject(streamer);
            so.FindProperty("proceduralWorld").objectReferenceValue = procedural;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (procedural != null) procedural.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(prev) && prev != ScenePath) EditorSceneManager.OpenScene(prev, OpenSceneMode.Single);
            return procedural != null ? "installed; ProceduralWorld kept inactive as fallback" : "installed; no ProceduralWorld found";
        }
    }
}
