using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Ghi dữ liệu VAT BẤT BIẾN của archetype lên material asset — ở THỜI ĐIỂM AUTHORING.
    ///
    /// Trước đây <c>VAT_Animator</c> ghi mấy giá trị này lên <c>sharedMaterial</c> lúc chạy. Nó hoạt
    /// động, nhưng sai chỗ: material asset đi vào bản build mà thiếu texture, và mọi thứ chỉ đúng lên
    /// sau khi một MonoBehaviour kịp chạy — nghĩa là frame đầu tiên có thể vẽ sai, và một bản build
    /// không có Editor thì không ai sửa hộ. Dữ liệu hằng số của archetype phải nằm sẵn trong asset.
    ///
    /// Đây là nơi DUY NHẤT được ghi. Bake lại gọi vào đây, nên một lần bake mới không thể trả material
    /// về trạng thái thiếu texture.
    /// </summary>
    public static class VatMaterialAuthoring
    {
        private static readonly int PositionTexID = Shader.PropertyToID("_PositionTexture");
        private static readonly int PositionMinID = Shader.PropertyToID("_PositionMin");
        private static readonly int PositionMaxID = Shader.PropertyToID("_PositionMax");

        /// <summary>Ghi hằng số archetype lên material. Trả về true nếu asset thay đổi.</summary>
        public static bool Apply(Material material, VAT_AnimationData data)
        {
            if (material == null || data == null || !data.IsValid()) return false;

            bool changed = false;

            if (material.HasProperty(PositionTexID) && material.GetTexture(PositionTexID) != data.positionTexture)
            {
                material.SetTexture(PositionTexID, data.positionTexture);
                changed = true;
            }

            if (material.HasProperty(PositionMinID) &&
                (Vector3)material.GetVector(PositionMinID) != data.positionMinBounds)
            {
                material.SetVector(PositionMinID, data.positionMinBounds);
                changed = true;
            }

            if (material.HasProperty(PositionMaxID) &&
                (Vector3)material.GetVector(PositionMaxID) != data.positionMaxBounds)
            {
                material.SetVector(PositionMaxID, data.positionMaxBounds);
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(material);
            return changed;
        }

        /// <summary>
        /// Quét mọi prefab quái đã bake và ghi hằng số lên material của chúng.
        ///
        /// Chạy sau khi bake, và chạy được thủ công khi cần vá dữ liệu cũ.
        /// </summary>
        [MenuItem("ZombieWar/VAT/Author VAT constants onto materials")]
        public static void ApplyToAllProductionArchetypes()
        {
            int checkedCount = 0, changedCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:GameObject ENM_"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab")) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                var animator = go.GetComponentInChildren<VAT_Animator>(true);
                if (animator == null || animator.animationData == null) continue;

                var renderer = animator.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterial == null) continue;

                checkedCount++;
                if (Apply(renderer.sharedMaterial, animator.animationData)) changedCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[VatMaterialAuthoring] archetypes checked={checkedCount} materials updated={changedCount}");
        }
    }
}
