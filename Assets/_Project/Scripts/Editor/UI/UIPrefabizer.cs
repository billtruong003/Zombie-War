using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Chuyển UI hierarchy đang sống trong scene thành prefab asset + scene instance
    /// (SaveAsPrefabAssetAndConnect) — source of truth chuyển về prefab, designer chỉnh trong
    /// Prefab Mode. Cross-screen references (Hub→Loadout...) giữ nguyên dưới dạng instance override.
    /// Idempotent: object đã là prefab instance thì bỏ qua.
    /// </summary>
    public static class UIPrefabizer
    {
        public const string ScreensDir = "Assets/_Project/UI/Prefabs/Screens";
        const string MapScene = "Assets/_Project/Scenes/Map_Level1.unity";

        [MenuItem("ZombieWar/UI/Authoring/Create Missing UI Prefabs")]
        public static void CreateMissing()
        {
            Directory.CreateDirectory(ScreensDir);

            // The menu screens are the V2 prefabs (UI_V2_*); only the HUD is connected here.
            var scene = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
            var hud = GameObject.Find("HUD");
            if (hud != null)
            {
                if (ConnectIfPlain(hud, $"{ScreensDir}/UI_Hud.prefab"))
                    EditorSceneManager.SaveScene(scene);
            }
            else Debug.LogWarning("[Prefabizer] Map không có HUD canvas.");

            Debug.Log("[Prefabizer] Create Missing UI Prefabs xong.");
        }

        /// <summary>Scene object chưa là prefab instance → save asset + connect. True nếu có thay đổi.</summary>
        public static bool ConnectIfPlain(GameObject sceneRoot, string prefabPath)
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(sceneRoot) != null) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath)!);
            PrefabUtility.SaveAsPrefabAssetAndConnect(sceneRoot, prefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[Prefabizer] {sceneRoot.name} → {prefabPath}");
            return true;
        }

        /// <summary>Sau destructive rebuild: ghi đè prefab asset từ scene root mới + reconnect.</summary>
        public static void ReconnectAfterRebuild(GameObject sceneRoot, string prefabPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath)!);
            PrefabUtility.SaveAsPrefabAssetAndConnect(sceneRoot, prefabPath, InteractionMode.AutomatedAction);
        }

        /// <summary>Dialog xác nhận cho mọi thao tác destructive. False = user huỷ.</summary>
        public static bool ConfirmDestructive(string what)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Prefabizer] Không chạy destructive rebuild trong Play Mode.");
                return false;
            }
            return EditorUtility.DisplayDialog("Destructive rebuild",
                $"Đập và dựng lại: {what}\n\nMọi manual edit / prefab override trên phần này sẽ MẤT.\nPrefab asset tương ứng sẽ bị ghi đè.\n\nTiếp tục?",
                "Rebuild", "Huỷ");
        }
    }
}
