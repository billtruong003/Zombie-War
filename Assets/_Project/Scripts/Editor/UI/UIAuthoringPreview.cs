using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// Authoring preview: bật 1 screen (tắt siblings) để designer xem/chỉnh NGOÀI Play Mode.
    /// Chỉ đổi active state — không rebuild, không overwrite layout, không chạy runtime service.
    /// Restore Runtime Defaults = Hub active, các screen khác inactive (UIManager tự quản khi Play).
    /// KHÔNG tự save scene — designer tự quyết (active state là thay đổi scene bình thường).
    /// </summary>
    public static class UIAuthoringPreview
    {
        [MenuItem("ZombieWar/UI/Authoring/Preview/Hub")] public static void Hub() => Show("HubScreen");
        [MenuItem("ZombieWar/UI/Authoring/Preview/Loadout")] public static void Loadout() => Show("LoadoutScreen");
        [MenuItem("ZombieWar/UI/Authoring/Preview/Costume")] public static void Costume() => Show("CostumeScreen");
        [MenuItem("ZombieWar/UI/Authoring/Preview/Shop")] public static void Shop() => Show("ShopScreen");
        [MenuItem("ZombieWar/UI/Authoring/Preview/Pass")] public static void Pass() => Show("PassScreen");

        [MenuItem("ZombieWar/UI/Authoring/Preview/Restore Runtime Defaults")]
        public static void Restore() => Show("HubScreen");

        [MenuItem("ZombieWar/UI/Authoring/Preview/HUD (open Map)")]
        public static void Hud()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Preview] Không dùng trong Play Mode."); return; }
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Map_Level1.unity", OpenSceneMode.Single);
            var hud = GameObject.Find("HUD");
            if (hud != null) Selection.activeGameObject = hud;
        }

        static readonly string[] Screens = { "HubScreen", "LoadoutScreen", "CostumeScreen", "ShopScreen", "PassScreen" };

        static void Show(string target)
        {
            if (Application.isPlaying) { Debug.LogWarning("[Preview] Không dùng trong Play Mode."); return; }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Menu.unity"))
                scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Menu.unity", OpenSceneMode.Single);

            var canvas = GameObject.Find("UIRoot");
            if (canvas == null) { Debug.LogError("[Preview] Menu không có UIRoot."); return; }

            foreach (var name in Screens)
            {
                var t = canvas.transform.Find(name);
                if (t == null) continue;
                bool on = name == target;
                if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
                // CanvasGroup alpha 0 lúc edit làm screen tàng hình — preview cần thấy
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null && on) cg.alpha = 1f;
            }
            var targetT = canvas.transform.Find(target);
            if (targetT != null) Selection.activeGameObject = targetT.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Preview] Đang hiển thị {target} (edit mode). Restore Runtime Defaults khi xong.");
        }
    }
}
