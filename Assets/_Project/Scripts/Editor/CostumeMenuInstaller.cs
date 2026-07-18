using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// Cai CostumeMenuController + preview character vao Menu scene, va gan
    /// CharacterModularApplier vao Player prefab (task #65).
    public static class CostumeMenuInstaller
    {
        private const string CatalogPath = "Assets/_Project/Data/Character/ModularCostumeCatalog.asset";

        [MenuItem("Tools/ZombieWar/Scenes/Install Costume Menu UI")]
        public static void InstallMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[CostumeMenuInstaller] Khong thay catalog o {CatalogPath} — chay extractor truoc.");
                return;
            }

            string scenePath = EditorBuildSettings.scenes
                .Select(s => s.path)
                .FirstOrDefault(p => p.Contains("Menu"));
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("[CostumeMenuInstaller] Khong tim thay Menu scene trong Build Settings.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath);

            // ---- Preview character (skeleton QuickRig + base body) ----
            var preview = GameObject.Find("CostumePreview");
            if (preview == null)
            {
                var basicGuid = AssetDatabase.FindAssets("Character_Basic t:Prefab")
                    .FirstOrDefault(g =>
                        System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == "Character_Basic");
                if (string.IsNullOrEmpty(basicGuid))
                {
                    Debug.LogError("[CostumeMenuInstaller] Khong thay prefab Character_Basic de lam preview.");
                    return;
                }
                var basicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(basicGuid));
                preview = (GameObject)PrefabUtility.InstantiatePrefab(basicPrefab, scene);
                preview.name = "CostumePreview";

                // Dat truoc camera menu, quay mat ve phia camera.
                var cam = Object.FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    var t = cam.transform;
                    Vector3 pos = t.position + t.forward * 3.5f;
                    pos.y = t.position.y - 1.2f;
                    preview.transform.position = pos;
                    Vector3 look = t.position; look.y = pos.y;
                    preview.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
                }
            }

            var applier = preview.GetComponent<CharacterModularApplier>();
            if (applier == null) applier = preview.AddComponent<CharacterModularApplier>();
            var soA = new SerializedObject(applier);
            soA.FindProperty("catalog").objectReferenceValue = catalog;
            soA.ApplyModifiedPropertiesWithoutUndo();

            // ---- Controller ----
            var ctrlGo = GameObject.Find("CostumeMenu");
            if (ctrlGo == null) ctrlGo = new GameObject("CostumeMenu");
            var ctrl = ctrlGo.GetComponent<CostumeMenuController>();
            if (ctrl == null) ctrl = ctrlGo.AddComponent<CostumeMenuController>();
            ctrl.catalog = catalog;
            ctrl.previewApplier = applier;
            EditorUtility.SetDirty(ctrl);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CostumeMenuInstaller] OK — controller + preview -> {scenePath}");
        }

        [MenuItem("Tools/ZombieWar/Prefabs/Install Player Costume Applier")]
        public static void InstallPlayerApplier()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[CostumeMenuInstaller] Khong thay catalog o {CatalogPath}.");
                return;
            }

            string prefabPath = AssetDatabase.FindAssets("Player t:Prefab", new[] { "Assets/_Project/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == "Player");
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError("[CostumeMenuInstaller] Khong thay Player.prefab trong Assets/_Project/Prefabs.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var applier = root.GetComponentInChildren<CharacterModularApplier>();
                if (applier == null) applier = root.AddComponent<CharacterModularApplier>();
                var so = new SerializedObject(applier);
                so.FindProperty("catalog").objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[CostumeMenuInstaller] OK — applier + catalog -> {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
