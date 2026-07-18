using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// Cai LoadoutMenuController vao Menu scene + gan toan bo WeaponData (task #64).
    public static class LoadoutMenuInstaller
    {
        [MenuItem("Tools/ZombieWar/Scenes/Install Loadout Menu UI")]
        public static void Install()
        {
            string scenePath = EditorBuildSettings.scenes
                .Select(s => s.path)
                .FirstOrDefault(p => p.Contains("Menu"));
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("[LoadoutMenuInstaller] Khong tim thay Menu scene trong Build Settings.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath);

            var ctrl = Object.FindFirstObjectByType<LoadoutMenuController>();
            if (ctrl == null)
            {
                var go = new GameObject("LoadoutMenu");
                ctrl = go.AddComponent<LoadoutMenuController>();
            }

            ctrl.weapons = AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Project/Data/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null)
                .OrderBy(w => w.tier)
                .ThenBy(w => w.name)
                .ToList();

            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[LoadoutMenuInstaller] OK — {ctrl.weapons.Count} weapons -> {scenePath}");
        }
    }
}
