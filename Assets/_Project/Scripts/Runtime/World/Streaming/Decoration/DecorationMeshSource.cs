using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Dữ liệu hình học của MỘT submesh nguồn, đã được sao chép sang asset của project ở Editor.
    ///
    /// Lý do tồn tại: mesh của vendor có <c>isReadable = false</c>. Runtime không được phép phụ thuộc
    /// vào việc đọc mesh vendor, và cũng không được phép phát hiện chuyện đó giữa lúc đang traverse.
    /// Bake một lần ở Editor rồi runtime chỉ đọc mảng ở đây.
    ///
    /// Không đụng vào import setting của vendor: baker đọc geometry qua API Editor, không bật
    /// <c>isReadable</c> trên model nào cả.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/World Streaming/Decoration Mesh Source", fileName = "DMS_")]
    public class DecorationMeshSource : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("ID ổn định. Không bao giờ được đổi sau khi thế giới đã sinh ra dựa trên nó.")]
        [SerializeField] private string stableId;

        [Tooltip("Nguồn vendor đã bake, chỉ để truy vết — runtime không đụng tới.")]
        [SerializeField] private string sourceAssetPath;

        [SerializeField] private int sourceSubMesh;
        [SerializeField] private DecorationCategory category = DecorationCategory.Foliage;

        [Header("Atlas")]
        [Tooltip("Ô mà UV của nguồn này được ánh xạ vào: (x, y, width, height) trong [0,1].")]
        [SerializeField] private Rect atlasRect = new Rect(0f, 0f, 1f, 1f);

        [Tooltip("UV gốc chạy ra ngoài [0,1] (texture lặp). Nguồn kiểu này phải chiếm trọn atlas.")]
        [SerializeField] private bool tilesUv;

        [Header("Geometry (baked)")]
        [SerializeField] private Vector3[] positions;
        [SerializeField] private Vector3[] normals;
        [SerializeField] private Vector2[] uv;
        [SerializeField] private int[] indices;
        [SerializeField] private Bounds localBounds;

        public string StableId => stableId;
        public string SourceAssetPath => sourceAssetPath;
        public int SourceSubMesh => sourceSubMesh;
        public DecorationCategory Category => category;
        public Rect AtlasRect => atlasRect;

        /// <summary>
        /// UV của nguồn này lặp ra ngoài <c>[0,1]</c> — ví dụ vỏ thân cây quấn nhiều vòng quanh trụ.
        ///
        /// Nguồn như vậy KHÔNG thể chia ô với nguồn khác: kẹp UV về <c>[0,1]</c> sẽ bôi bẹt hoa văn,
        /// còn ánh xạ vào một ô con sẽ khiến phần lặp tràn sang ô hàng xóm. Nó phải chiếm trọn atlas
        /// và dùng wrap mode Repeat.
        /// </summary>
        public bool TilesUv => tilesUv;

        /// <summary>Ô atlas có phải là toàn bộ texture hay không.</summary>
        public bool OccupiesWholeAtlas =>
            Mathf.Abs(atlasRect.x) < 1e-4f && Mathf.Abs(atlasRect.y) < 1e-4f &&
            Mathf.Abs(atlasRect.width - 1f) < 1e-4f && Mathf.Abs(atlasRect.height - 1f) < 1e-4f;

        public Vector3[] Positions => positions;
        public Vector3[] Normals => normals;
        public Vector2[] Uv => uv;
        public int[] Indices => indices;
        public Bounds LocalBounds => localBounds;

        public int VertexCount => positions != null ? positions.Length : 0;
        public int TriangleCount => indices != null ? indices.Length / 3 : 0;

        /// <summary>Bán kính ngang, dùng cho luật giãn cách và biên bounds.</summary>
        public float HorizontalRadius =>
            Mathf.Max(localBounds.extents.x, localBounds.extents.z);

        /// <summary>Muối ổn định suy từ <see cref="StableId"/>, không phụ thuộc thứ tự danh sách.</summary>
        public int Salt => DecorationHash.SaltFromId(stableId);

        /// <summary>Baker ở Editor gọi hàm này. Runtime chỉ đọc.</summary>
        public void Bake(
            string id, string assetPath, int subMesh, DecorationCategory decorationCategory, Rect rect,
            Vector3[] bakedPositions, Vector3[] bakedNormals, Vector2[] bakedUv, int[] bakedIndices)
        {
            stableId = id;
            sourceAssetPath = assetPath;
            sourceSubMesh = subMesh;
            category = decorationCategory;
            atlasRect = rect;
            positions = bakedPositions;
            normals = bakedNormals;
            uv = bakedUv;
            indices = bakedIndices;

            tilesUv = false;
            if (bakedUv != null)
            {
                for (int i = 0; i < bakedUv.Length; i++)
                {
                    Vector2 t = bakedUv[i];
                    if (t.x < -1e-3f || t.x > 1f + 1e-3f || t.y < -1e-3f || t.y > 1f + 1e-3f)
                    {
                        tilesUv = true;
                        break;
                    }
                }
            }

            localBounds = new Bounds();
            if (bakedPositions != null && bakedPositions.Length > 0)
            {
                localBounds = new Bounds(bakedPositions[0], Vector3.zero);
                for (int i = 1; i < bakedPositions.Length; i++) localBounds.Encapsulate(bakedPositions[i]);
            }
        }

        /// <summary>Fail rõ ràng thay vì để mesh builder sinh ra rác.</summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = $"{name}: thiếu stableId.";
                return false;
            }

            if (positions == null || positions.Length == 0)
            {
                error = $"{stableId}: không có đỉnh nào.";
                return false;
            }

            if (normals == null || normals.Length != positions.Length)
            {
                error = $"{stableId}: số normal ({normals?.Length ?? 0}) khác số đỉnh ({positions.Length}).";
                return false;
            }

            if (uv == null || uv.Length != positions.Length)
            {
                error = $"{stableId}: số UV ({uv?.Length ?? 0}) khác số đỉnh ({positions.Length}).";
                return false;
            }

            if (indices == null || indices.Length == 0 || indices.Length % 3 != 0)
            {
                error = $"{stableId}: chỉ số tam giác không hợp lệ ({indices?.Length ?? 0}).";
                return false;
            }

            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] < 0 || indices[i] >= positions.Length)
                {
                    error = $"{stableId}: chỉ số {indices[i]} nằm ngoài {positions.Length} đỉnh.";
                    return false;
                }
            }

            if (atlasRect.width <= 0f || atlasRect.height <= 0f)
            {
                error = $"{stableId}: atlasRect rỗng ({atlasRect}).";
                return false;
            }

            if (atlasRect.xMin < -0.0001f || atlasRect.yMin < -0.0001f ||
                atlasRect.xMax > 1.0001f || atlasRect.yMax > 1.0001f)
            {
                error = $"{stableId}: atlasRect ra ngoài [0,1] ({atlasRect}).";
                return false;
            }

            if (tilesUv && !OccupiesWholeAtlas)
            {
                error = $"{stableId}: UV lặp ra ngoài [0,1] nhưng lại được xếp vào ô con {atlasRect} — " +
                        "nguồn lặp phải chiếm trọn atlas riêng của nó.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
