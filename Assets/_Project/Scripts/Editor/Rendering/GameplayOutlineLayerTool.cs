using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Chọn nhân vật và quái vào mặt nạ viền bằng RENDERING LAYER (M4.6C.2R).
    ///
    /// Bản trước mượn `GameObject.layer` để chọn. Đó là kênh VẬT LÝ — ma trận va chạm, raycast ngắm
    /// bắn và `WalkableGround` đều lọc theo nó — nên muốn viền một mảnh hình thì phải đổi tầng vật lý
    /// của mảnh đó. Hệ quả: `Player/Plane` dùng chung GameObject với `MeshCollider` buộc phải bỏ qua,
    /// và bóng nhân vật bị thủng một mảng ngay trong mặt nạ.
    ///
    /// `Renderer.renderingLayerMask` là kênh thuần hình ảnh. Nó chọn được đúng 37/37 renderer, kể cả
    /// `Player/Plane`, mà không chạm một byte nào của vật lý.
    ///
    /// Bit được CỘNG THÊM chứ không ghi đè: mọi renderer giữ nguyên mask ánh sáng sẵn có của nó.
    /// </summary>
    public static class GameplayOutlineLayerTool
    {
        /// <summary>Bit 1 — nhân vật. Bit 0 (giá trị 1) là mặc định của mọi renderer, không được đụng.</summary>
        public const uint WeaponRenderingBit = 1u << 1;

        /// <summary>Bit 2 — quái.</summary>
        public const uint EnemyRenderingBit = 1u << 2;

        /// <summary>Bit 3 — skinned player body rendered by Toon Lit's material-owned mask pass.</summary>
        public const uint PlayerRenderingBit = 1u << 3;

        /// <summary>Mặt nạ chọn lọc của NHÂN VẬT. Test đọc lại đúng hằng số này.</summary>
        public const uint SelectionRenderingMask = WeaponRenderingBit | EnemyRenderingBit | PlayerRenderingBit;

        /// <summary>
        /// Bit 4 — viền cảnh vật môi trường (M4.6CD.1). Chỉ `SolidDecorMeshRenderer` mang bit này.
        ///
        /// Giá trị phải khớp <c>ZombieWar.WorldStreaming.ChunkInstance.EnvironmentOutlineRenderingBit</c>;
        /// hai hằng số nằm ở hai assembly khác nhau nên có test canh chúng bằng nhau.
        /// </summary>
        public const uint EnvironmentOutlineRenderingBit = 1u << 4;

        /// <summary>
        /// Mặt nạ ghi vào Volume của production: nhân vật CỘNG cảnh vật rắn = <c>14 | 16 = 30</c>.
        /// </summary>
        public const uint ProductionSelectionMask = SelectionRenderingMask | EnvironmentOutlineRenderingBit;

        private const string PrefabFolder = "Assets/_Project/Prefabs";

        /// <summary>Layer vật lý gốc của nhân vật/quái.</summary>
        private const int OriginalGameplayLayer = 0;

        /// <summary>Hai ID layer mà M4.6C.2 đã ghi vào và checkpoint này phải gỡ ra. Tên đã bị xoá
        /// khỏi TagManager nên chỉ còn so được bằng số.</summary>
        private const int LegacyOutlinePlayerLayer = 11;
        private const int LegacyOutlineEnemyLayer = 12;


        /// <summary>
        /// Tấm bóng đổ giả dưới chân nhân vật — KHÔNG được vào mặt nạ viền.
        ///
        /// Nó là một quad phẳng, nên đưa vào mặt nạ thì viền vẽ ra một HÌNH CHỮ NHẬT quanh chân mỗi
        /// nhân vật. Bằng chứng: ảnh EdgeOnly đầu tiên hiện rõ các khung chữ nhật quanh từng con quái.
        ///
        /// Nhận diện theo SHADER chứ không theo tên "Plane": tên là thứ đổi được mà không ai nhận ra
        /// hệ quả, còn tấm bóng luôn dùng vật liệu Unlit trong khi thân nhân vật dùng shader toon.
        /// </summary>
        private static bool IsShadowBlob(Renderer r)
        {
            Material m = r.sharedMaterial;
            return m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Unlit";
        }

        [MenuItem("ZombieWar/Rendering/Assign Gameplay Outline Rendering Layers", priority = 140)]
        public static void RunMenu()
        {
            string report = Run();
            Debug.Log("[M4.6C.2R] " + report);
            EditorUtility.DisplayDialog("Gameplay outline rendering layers", report, "OK");
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            int prefabs = 0, renderersTagged = 0, layersRestored = 0, legacyFixed = 0, shadowBlobsExcluded = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject probe = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (probe == null) continue;

                bool isPlayer = probe.GetComponent<ZombieWar.PlayerMovement>() != null;
                bool isEnemy = probe.GetComponent<ZombieWar.ZombieBase>() != null;
                if (!isPlayer && !isEnemy) continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                uint bit = isPlayer ? PlayerRenderingBit : EnemyRenderingBit;
                bool dirty = false;

                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    // Chỉ gỡ ĐÚNG hai ID mà M4.6C.2 đã ghi vào. Viết `!= 0` là quá tay cho một công cụ
                    // chạy lại nhiều lần: sau này ai đó gán một layer hình ảnh có chủ đích cho một
                    // prefab nhân vật mới, lần chạy kế tiếp sẽ âm thầm xoá mất.
                    //
                    // So sánh bằng số chứ không qua `LayerMask.NameToLayer`: tên "OutlinePlayer"/
                    // "OutlineEnemy" đã bị gỡ khỏi TagManager, nên tra theo tên bây giờ trả về -1.
                    if (r.gameObject.layer == LegacyOutlinePlayerLayer ||
                        r.gameObject.layer == LegacyOutlineEnemyLayer)
                    {
                        r.gameObject.layer = OriginalGameplayLayer;
                        layersRestored++;
                        dirty = true;
                    }

                    // Bỏ tấm bóng đổ ra khỏi mặt nạ, và GỠ bit nếu lần chạy trước đã lỡ gán.
                    if (IsShadowBlob(r))
                    {
                        shadowBlobsExcluded++;
                        uint cleared = r.renderingLayerMask & ~SelectionRenderingMask;
                        if (cleared == 0) cleared = 1u;
                        if (cleared != r.renderingLayerMask) { r.renderingLayerMask = cleared; dirty = true; }
                        continue;
                    }

                    uint mask = r.renderingLayerMask;

                    // Mask "tất cả bit" là di sản: nó khớp với MỌI bộ lọc rendering-layer, nên vật thể
                    // đó sẽ lọt vào mặt nạ nhân vật dù không phải nhân vật. Kéo về bit mặc định trước.
                    if (mask == uint.MaxValue)
                    {
                        mask = 1u;
                        legacyFixed++;
                        dirty = true;
                    }

                    // Keep exactly one character role bit. This migrates the old player bit away
                    // from skinned bodies so the generic bind-pose override cannot draw them.
                    uint updated = (mask & ~SelectionRenderingMask) | bit;
                    if (updated == 0) updated = 1u | bit;
                    if (updated != r.renderingLayerMask)
                    {
                        r.renderingLayerMask = updated;
                        renderersTagged++;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            // --- Vũ khí cầm tay ---
            // Ba prefab vũ khí mang mask `uint.MaxValue` (10 renderer). Mask đó khớp với MỌI bộ lọc
            // rendering-layer, kể cả bit của quái, nên phải sửa dù có muốn viền vũ khí hay không.
            //
            // Bằng chứng cho lựa chọn: chúng có `WeaponGripPoints` và được `WD_*.asset` tham chiếu,
            // tức là vũ khí do NGƯỜI CHƠI cầm. Vậy đưa chúng vào mặt nạ nhân vật là có chủ đích —
            // khẩu súng viền cùng người cầm — chứ không phải tai nạn. Giữ bit ánh sáng mặc định.
            int weaponRenderers = 0, weaponPrefabs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder + "/Weapons" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;
                bool dirty = false;

                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    uint mask = r.renderingLayerMask;
                    if (mask == uint.MaxValue) { mask = 1u; legacyFixed++; }
                    uint updated = (mask & ~SelectionRenderingMask) | WeaponRenderingBit;
                    if (updated == 0) updated = 1u | WeaponRenderingBit;
                    if (updated != r.renderingLayerMask)
                    {
                        r.renderingLayerMask = updated;
                        weaponRenderers++;
                        dirty = true;
                    }
                }

                if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, path); weaponPrefabs++; }
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            sb.Append("weaponPrefabsChanged=").Append(weaponPrefabs)
              .Append(" weaponRenderersTagged=").Append(weaponRenderers).Append(' ');
            sb.Append("prefabsChanged=").Append(prefabs)
              .Append(" renderersTagged=").Append(renderersTagged)
              .Append(" gameObjectLayersRestored=").Append(layersRestored)
              .Append(" shadowBlobsExcluded=").Append(shadowBlobsExcluded)
              .Append(" legacyAllBitsMasksFixed=").Append(legacyFixed)
              .Append(" selectionMask=").Append(SelectionRenderingMask);
            return sb.ToString();
        }

        /// <summary>Kiểm chứng hợp đồng, dùng bởi test và bởi lần chạy thủ công.</summary>
        public static string Verify()
        {
            var sb = new StringBuilder();
            int player = 0, enemy = 0, total = 0, wrongLayer = 0, physicsMoved = 0, allBits = 0, blobsSelected = 0, blobsExcluded = 0;
            var missing = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                bool isPlayer = go.GetComponent<ZombieWar.PlayerMovement>() != null;
                bool isEnemy = go.GetComponent<ZombieWar.ZombieBase>() != null;
                if (!isPlayer && !isEnemy) continue;

                uint bit = isPlayer ? PlayerRenderingBit : EnemyRenderingBit;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    total++;
                    bool blob = IsShadowBlob(r);
                    if ((r.renderingLayerMask & bit) != 0) { if (isPlayer) player++; else enemy++; if (blob) blobsSelected++; }
                    else if (blob) blobsExcluded++;
                    else missing.Add($"{go.name}/{r.gameObject.name}");
                    if (r.renderingLayerMask == uint.MaxValue) allBits++;
                    if (r.gameObject.layer == LegacyOutlinePlayerLayer ||
                        r.gameObject.layer == LegacyOutlineEnemyLayer) wrongLayer++;
                }
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                    if (c.gameObject.layer != OriginalGameplayLayer) physicsMoved++;
                foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
                    if (rb.gameObject.layer != OriginalGameplayLayer) physicsMoved++;
            }

            sb.Append("renderersTotal=").Append(total)
              .Append(" playerSelected=").Append(player)
              .Append(" enemySelected=").Append(enemy)
              .Append(" notSelected=").Append(missing.Count)
              .Append(" shadowBlobsExcluded=").Append(blobsExcluded)
              .Append(" shadowBlobsWronglySelected=").Append(blobsSelected)
              .Append(" legacyOutlineLayer11or12=").Append(wrongLayer)
              .Append(" physicsLayerDrift=").Append(physicsMoved)
              .Append(" legacyAllBits=").Append(allBits);
            foreach (string m in missing) sb.Append("\n  NOT SELECTED: ").Append(m);
            return sb.ToString();
        }
    }
}
