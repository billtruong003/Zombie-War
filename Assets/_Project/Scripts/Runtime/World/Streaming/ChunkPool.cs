using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Pool chunk root cố định. Prewarm một lần rồi cho thuê / thu hồi mãi mãi — sau warmup,
    /// di chuyển và teleport phải tạo và huỷ ĐÚNG 0 chunk root.
    ///
    /// Bộ đếm ở đây đo vòng đời chunk root do pool này quản lý. Chúng không tuyên bố chặn được
    /// mọi cấp phát của Unity; phạm vi đúng bằng phạm vi mà M0 cần chứng minh.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChunkPool : MonoBehaviour
    {
        [Tooltip("Material nền dùng chung cho cả 25 chunk. Bỏ trống thì pool tự tạo một material dự phòng.")]
        [SerializeField] private Material diagnosticMaterial;

        [Tooltip("Material trang tri dac dung chung cho ca 25 chunk.")]
        [SerializeField] private Material solidDecorMaterial;

        [Tooltip("Material foliage dung chung cho ca 25 chunk.")]
        [SerializeField] private Material foliageMaterial;

        [Tooltip("Node cha chứa toàn bộ chunk root. Bỏ trống thì dùng chính transform này.")]
        [SerializeField] private Transform rootsParent;

        private readonly List<ChunkInstance> _all = new List<ChunkInstance>();
        private readonly List<ChunkInstance> _free = new List<ChunkInstance>();

        private int _warmupBaseline;
        private int _groundMeshBaseline;
        private int _decorationMeshBaseline;
        private bool _tearingDown;

        public bool IsPrewarmed { get; private set; }
        public int Capacity => _all.Count;
        public IReadOnlyList<ChunkInstance> All => _all;

        /// <summary>Tổng số chunk root đã tạo trong cả vòng đời pool.</summary>
        public int TotalRootsCreated { get; private set; }

        /// <summary>Số chunk root tạo thêm SAU khi prewarm xong. Yêu cầu M0: luôn bằng 0.</summary>
        public int RootsCreatedSinceWarmup => TotalRootsCreated - _warmupBaseline;

        /// <summary>Số Mesh nền tạo thêm SAU khi prewarm xong. Yêu cầu M1: luôn bằng 0.</summary>
        public int GroundMeshesCreatedSinceWarmup => GroundMeshBuilder.MeshesCreated - _groundMeshBaseline;

        /// <summary>Số chunk root bị huỷ trong lúc chạy bình thường. Yêu cầu M0: luôn bằng 0.</summary>
        public int RootsDestroyedDuringTraversal { get; private set; }

        /// <summary>
        /// Số chunk root bị huỷ khi dọn dẹp (thoát Play Mode, teardown của test).
        /// Tách riêng vì đây KHÔNG phải hành vi traversal và không được tính vào kết luận pooling.
        /// </summary>
        public int RootsDestroyedDuringTeardown { get; private set; }

        public int ActiveLeaseCount => _all.Count - _free.Count;
        public int FreeCount => _free.Count;

        /// <summary>Tổng số lần cho thuê.</summary>
        public int LeaseCount { get; private set; }

        /// <summary>Số lần một slot ĐÃ từng dùng được cấp lại cho toạ độ khác — số lần tái sử dụng thật.</summary>
        public int RecycleCount { get; private set; }

        /// <summary>Gán material chẩn đoán dùng chung. Phải gọi trước <see cref="Prewarm"/>.</summary>
        public void SetDiagnosticMaterial(Material value)
        {
            if (IsPrewarmed)
                throw new System.InvalidOperationException("[WorldStreaming] Không đổi material sau khi đã prewarm.");

            diagnosticMaterial = value;
        }

        /// <summary>Gan material trang tri. Phai goi truoc <see cref="Prewarm"/>.</summary>
        public void SetDecorationMaterials(Material solid, Material foliage)
        {
            if (IsPrewarmed)
                throw new System.InvalidOperationException("[WorldStreaming] Khong doi material sau khi da prewarm.");

            solidDecorMaterial = solid;
            foliageMaterial = foliage;
        }

        /// <summary>So Mesh trang tri tao them SAU khi prewarm xong. Yeu cau M2B: luon bang 0.</summary>
        public int DecorationMeshesCreatedSinceWarmup => DecorationMeshBuilder.MeshesCreated - _decorationMeshBaseline;

        /// <summary>Gán node cha chứa chunk root. Phải gọi trước <see cref="Prewarm"/>.</summary>
        public void SetRootsParent(Transform value)
        {
            if (IsPrewarmed)
                throw new System.InvalidOperationException("[WorldStreaming] Không đổi node cha sau khi đã prewarm.");

            rootsParent = value;
        }

        /// <summary>Tạo trước toàn bộ chunk root. Gọi nhiều lần là no-op.</summary>
        public void Prewarm(WorldStreamingConfig config)
        {
            if (IsPrewarmed) return;
            if (config == null) throw new System.ArgumentNullException(nameof(config));

            if (!config.Validate(out string error))
                throw new System.InvalidOperationException($"[WorldStreaming] Cấu hình không hợp lệ: {error}");

            Transform parent = rootsParent != null ? rootsParent : transform;

            for (int i = 0; i < config.PoolCapacity; i++)
            {
                var instance = ChunkInstance.Create(i, parent, config, diagnosticMaterial, solidDecorMaterial, foliageMaterial);
                instance.Destroyed += OnInstanceDestroyed;
                TotalRootsCreated++;
                _all.Add(instance);
                _free.Add(instance);
            }

            _warmupBaseline = TotalRootsCreated;
            _groundMeshBaseline = GroundMeshBuilder.MeshesCreated;
            _decorationMeshBaseline = DecorationMeshBuilder.MeshesCreated;
            IsPrewarmed = true;
        }

        /// <summary>
        /// Cho thuê slot rảnh có <see cref="ChunkInstance.SlotId"/> nhỏ nhất.
        /// Chọn theo slot id (chứ không theo thứ tự trả về) để hành vi tái lập được giữa các lần chạy.
        /// </summary>
        public ChunkInstance Lease()
        {
            if (!IsPrewarmed)
                throw new System.InvalidOperationException("[WorldStreaming] Pool chưa prewarm.");

            if (_free.Count == 0)
                throw new System.InvalidOperationException(
                    $"[WorldStreaming] Pool cạn slot (capacity {Capacity}). Ring đòi nhiều chunk hơn pool có.");

            ChunkInstance instance = _free[0];
            _free.RemoveAt(0);

            LeaseCount++;
            if (instance.AssignmentCount > 0) RecycleCount++;

            return instance;
        }

        public void Release(ChunkInstance instance)
        {
            if (instance == null) return;

            instance.Release();

            // Giữ danh sách rảnh sắp xếp tăng dần theo slot id: thứ tự trả về không được ảnh hưởng
            // thứ tự cho thuê lần sau.
            int index = _free.Count;
            for (int i = 0; i < _free.Count; i++)
            {
                if (_free[i].SlotId > instance.SlotId)
                {
                    index = i;
                    break;
                }
            }

            _free.Insert(index, instance);
        }

        private void OnInstanceDestroyed(ChunkInstance instance)
        {
            if (_tearingDown) RootsDestroyedDuringTeardown++;
            else RootsDestroyedDuringTraversal++;
        }

        /// <summary>Đánh dấu mọi lần huỷ sau đây là dọn dẹp, không phải traversal.</summary>
        public void BeginTeardown() => _tearingDown = true;

        private void OnDestroy() => BeginTeardown();

        private void OnApplicationQuit() => BeginTeardown();
    }
}
