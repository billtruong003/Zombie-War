using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Chuyển một map gameplay sang thế giới thủ tục (M4.1).
    ///
    /// Một hàm chuyển đổi duy nhất chạy cho cả năm map. Cả năm map có cấu trúc giống hệt nhau (đã
    /// khảo sát trước khi viết), nên viết năm biến thể sẽ chỉ tạo ra năm chỗ để lệch nhau.
    ///
    /// Phép chuyển đổi là CỘNG THÊM và TẮT BỚT, không xoá: mọi thứ bị loại bỏ đều chỉ bị
    /// <c>SetActive(false)</c>. Nhờ vậy thay đổi có thể đảo ngược bằng tay trong Editor, và không có
    /// nội dung tác giả nào bị huỷ vĩnh viễn vì một phán đoán tự động.
    /// </summary>
    public static class ProductionMapStreamingConverter
    {
        public const string StreamingRootName = "ProceduralWorld";

        private static readonly string[] ProductionMaps =
        {
            "Map_Level1", "Map_Level2", "Map_Level3", "Map_Level4", "Map_Level5",
        };

        /// <summary>
        /// Những nhánh bị thế giới thủ tục thay thế.
        ///
        /// - `NavMesh`: bề mặt điều hướng đã bake. Không còn gì đọc nó từ M4.2.
        /// - `Environment/Ground`: 121 ô sàn 5×5 m tạo thành đấu trường 55 m. Mặt đất thủ tục thay
        ///   nó, và để cả hai cùng bật sẽ z-fighting ngay tại Y=0.
        /// - `Environment/Boundary`: tường vách đá quây đúng đấu trường 55 m đó. Giữ lại thì người
        ///   chơi bị nhốt trong chưa đầy hai chunk và toàn bộ điểm của streaming biến mất.
        ///
        /// - `Environment/Props`: đá, vách, xương rồng, cây low-poly cũ. Ở M4 chúng được GIỮ để làm
        ///   vật cản; M4.5 đảo lại quyết định đó vì lý do thị giác: thế giới thủ tục không được pha
        ///   trộn với bộ môi trường vẽ tay cũ. Chúng bị tắt cả renderer lẫn collider.
        ///
        /// `Environment/Interactive` rỗng ở cả năm map nhưng vẫn được giữ nguyên — nó là chỗ dành cho
        /// nội dung gameplay, không phải trang trí.
        /// </summary>
        private static readonly string[] RetiredPaths =
        {
            "NavMesh",
            "Environment/Ground",
            "Environment/Boundary",
            "Environment/Props",
        };

        [MenuItem("ZombieWar/World Streaming/Convert Production Maps", priority = 120)]
        public static void ConvertAllMenu()
        {
            string report = ConvertAll();
            Debug.Log("[M4] " + report);
            EditorUtility.DisplayDialog("Production map conversion", report, "OK");
        }

        public static string ConvertAll()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ProductionMaps.Length; i++)
                sb.Append(Convert(ProductionMaps[i])).Append('\n');
            return sb.ToString();
        }

        /// <summary>Chuyển đổi một map và lưu lại. Trả về một dòng báo cáo.</summary>
        public static string Convert(string mapName)
        {
            string path = $"Assets/_Project/Scenes/{mapName}.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.Append(mapName.PadRight(12));

            int retired = 0;
            foreach (string target in RetiredPaths)
            {
                GameObject go = FindByPath(scene, target);
                if (go == null) { sb.Append(" [missing:").Append(target).Append(']'); continue; }
                if (!go.activeSelf) continue;

                Undo.RecordObject(go, "M4 retire legacy environment");
                go.SetActive(false);
                retired++;
            }

            // Một map chỉ được có đúng một thế giới thủ tục.
            GameObject root = FindByPath(scene, StreamingRootName);
            bool created = root == null;
            if (created)
            {
                root = new GameObject(StreamingRootName);
                root.transform.SetParent(null);
            }

            var streaming = root.GetComponent<ProductionWorldStreaming>();
            if (streaming == null) streaming = root.AddComponent<ProductionWorldStreaming>();

            // Hạt giống suy từ TÊN MÀN chứ không phải một bảng switch: mỗi màn nhận một hạt giống ổn
            // định, khác nhau, và thêm map thứ sáu không phải sửa code. Giá trị được ghi vào trường
            // serialize nên nó là dữ liệu của scene, tác giả sửa lại được bất cứ lúc nào.
            int index = System.Array.IndexOf(ProductionMaps, mapName);
            int seed = 20260809 + (index + 1) * 7919;

            var so = new SerializedObject(streaming);
            so.FindProperty("config").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WorldStreamingConfig>(WorldStreamingLabBuilder.ConfigPath);
            so.FindProperty("groundMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(WorldStreamingLabBuilder.GroundMaterialPath);
            so.FindProperty("solidDecorMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(WorldStreamingLabBuilder.SolidDecorMaterialPath);
            so.FindProperty("foliageMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(WorldStreamingLabBuilder.FoliageMaterialPath);
            so.FindProperty("worldSeed").intValue = seed;
            so.FindProperty("playerSpawner").objectReferenceValue = FindFirst<PlayerSpawner>(scene);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            sb.Append(created ? " +rig" : " rig=kept")
              .Append("  retired=").Append(retired)
              .Append("  seed=").Append(seed);
            return sb.ToString();
        }

        private static GameObject FindByPath(UnityEngine.SceneManagement.Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                Transform t = root.transform;
                for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                return t != null ? t.gameObject : null;
            }

            return null;
        }

        private static T FindFirst<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }
    }
}
