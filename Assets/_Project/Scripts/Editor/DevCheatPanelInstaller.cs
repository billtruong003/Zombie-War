using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieWar.Editor
{
    /// Adds (or re-wires) the dev cheat panel on Bootstrap's BootstrapEntry: every WeaponData plus the
    /// Casual costume catalog. Touches Bootstrap.unity only.
    public static class DevCheatPanelInstaller
    {
        private const string BootstrapScene = "Assets/_Project/Scenes/Bootstrap.unity";

        [MenuItem("ZombieWar/Dev/Install Cheat Panel In Bootstrap")]
        public static void InstallDevCheatPanel()
        {
            var scene = SceneManager.GetSceneByPath(BootstrapScene);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Additive);

            var root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "BootstrapEntry");
            if (root == null)
            {
                root = new GameObject("BootstrapEntry");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<BootstrapEntry>();
            }

            var panel = root.GetComponent<ZombieWarCheatPanel>() ?? root.AddComponent<ZombieWarCheatPanel>();
            WireCheatAssets(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            Debug.Log("[DevCheatPanelInstaller] Installed dev cheat panel in Bootstrap scene.");
        }

        private static void WireCheatAssets(ZombieWarCheatPanel panel)
        {
            var so = new SerializedObject(panel);
            var weaponAssets = AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Project/Data/Weapons" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(x => x)
                .Select(AssetDatabase.LoadAssetAtPath<WeaponData>)
                .ToArray();
            var weaponProp = so.FindProperty("weapons");
            weaponProp.arraySize = weaponAssets.Length;
            for (int i = 0; i < weaponAssets.Length; i++)
                weaponProp.GetArrayElementAtIndex(i).objectReferenceValue = weaponAssets[i];

            var costumeAssets = new[]
            {
                // Casual only: the Fantasy catalog is rollback data and must not ship in builds.
                AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(
                    "Assets/_Project/Data/Character/CasualCostumeCatalog.asset")
            };
            var costumeProp = so.FindProperty("costumeCatalogs");
            costumeProp.arraySize = costumeAssets.Length;
            for (int i = 0; i < costumeAssets.Length; i++)
                costumeProp.GetArrayElementAtIndex(i).objectReferenceValue = costumeAssets[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
