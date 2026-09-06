using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Đưa thế giới thủ tục vào một map gameplay thật (M4.1).
    ///
    /// Chỉ có MỘT thành phần này cho mỗi map, và nó gọi đúng <see cref="WorldStreamingRig.Build"/> mà
    /// lab và toàn bộ test PlayMode đang dùng. Không có bản sao thuật toán streaming nào cho production
    /// — thứ được kiểm chứng ở M0–M3 phải là đúng thứ chạy trong trận.
    ///
    /// Việc gắn mục tiêu streaming phải hoãn lại: người chơi không nằm sẵn trong scene mà do
    /// <see cref="PlayerSpawner"/> sinh ra trong <c>Start()</c>, và Unity không bảo đảm thứ tự
    /// <c>Start()</c> giữa hai thành phần. Nên rig được dựng ngay nhưng CHƯA khởi tạo, rồi khởi tạo ở
    /// frame đầu tiên mà người chơi thật sự tồn tại.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionWorldStreaming : MonoBehaviour
    {
        [Header("Streaming")]
        [SerializeField] private WorldStreamingConfig config;

        [Tooltip("Bo trong = tu tim trong scene.")]
        [SerializeField] private PlayerSpawner playerSpawner;

        [Header("Shared materials (dung chung ca 25 chunk, khong instance theo map)")]
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material solidDecorMaterial;
        [SerializeField] private Material foliageMaterial;

        [Header("Stage identity")]
        [Tooltip("Hat giong the gioi cua man nay. Hai man khac hat giong = hai vung dat khac han.")]
        [SerializeField] private int worldSeed = 20260809;

        // Không có `startChunk`. Bản đầu có, và nó đã tạo ra đúng một lỗi: mặt gameplay dùng chung
        // được đặt ở ô khởi điểm trong khi người chơi vẫn sinh ra ở PlayerSpawnPoint của map, nên
        // người chơi rơi xuyên thế giới và chết trước khi ring kịp dựng.
        //
        // Nó cũng thừa: `worldSeed` đã cho mỗi màn một vùng đất hoàn toàn khác. Dời thêm toạ độ khởi
        // điểm không tạo ra sự khác biệt nào mà người chơi cảm nhận được, chỉ thêm một thứ phải đồng
        // bộ với điểm sinh của tác giả.

        private WorldStreamingRig.Rig _rig;
        private bool _built;
        private bool _initialized;

        public WorldStreamManager Manager => _rig.Manager;
        public ChunkPool Pool => _rig.Pool;
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Dựng rig NGAY Ở AWAKE, trước khi <see cref="PlayerSpawner"/> chạy <c>Start()</c>.
        ///
        /// Thứ tự này là bắt buộc chứ không phải tối ưu. Map cũ có 121 ô sàn mang collider; chuyển
        /// sang thế giới thủ tục thì chúng bị tắt, và mặt gameplay dùng chung trở thành thứ DUY NHẤT
        /// đỡ người chơi. Nếu rig chỉ dựng ở `Start()`, người chơi có thể được sinh ra trước khi mặt
        /// đó tồn tại và rơi xuyên thế giới — đúng lỗi đã bắt được khi kiểm chứng Map_Level1.
        ///
        /// Mặt dùng chung không phụ thuộc người chơi, chỉ vòng ring mới phụ thuộc — nên nó được đặt
        /// vào đúng ô khởi điểm của màn ngay tại đây, còn ring thì chờ.
        /// </summary>
        private void Awake()
        {
            if (playerSpawner == null) playerSpawner = FindFirstObjectByType<PlayerSpawner>();

            BuildRig();
            if (!_built || _rig.Surface == null) return;

            // Đặt mặt dùng chung ngay dưới ĐIỂM SINH của map — chỗ người chơi sắp xuất hiện — chứ
            // không phải gốc toạ độ. Điểm sinh đọc được từ scene trước khi người chơi tồn tại.
            float chunkSize = _rig.Manager.Config.ChunkSize;
            Vector3 spawn = ResolveSpawnPosition();
            _rig.Surface.RecenterOn(ChunkCoord.FromWorld(spawn, chunkSize), chunkSize);
        }

        /// <summary>Vị trí người chơi sẽ xuất hiện, đọc trước khi người chơi được sinh ra.</summary>
        private Vector3 ResolveSpawnPosition()
        {
            var point = FindFirstObjectByType<PlayerSpawnPoint>();
            if (point != null) return point.transform.position;
            return playerSpawner != null ? playerSpawner.transform.position : Vector3.zero;
        }

        private void BuildRig()
        {
            if (_built) return;

            if (config == null)
            {
                Debug.LogError("[ProductionWorldStreaming] Chưa gán WorldStreamingConfig — map sẽ không có mặt đất.", this);
                enabled = false;
                return;
            }

            // Hạt giống của màn ghi đè hạt giống mặc định của config. Config là asset DÙNG CHUNG cho
            // cả 5 map, nên ghi thẳng vào nó sẽ khiến map nạp sau đổi thế giới của map nạp trước —
            // vì vậy dùng một bản sao runtime.
            WorldStreamingConfig runtimeConfig = Instantiate(config);
            runtimeConfig.name = $"{config.name}_Runtime";
            runtimeConfig.SetWorldSeed(worldSeed);

            _rig = WorldStreamingRig.Build(runtimeConfig, null, groundMaterial,
                solidDecorMaterial, foliageMaterial, initializeOnStart: false);
            _rig.Root.transform.SetParent(transform, false);
            _built = true;
        }

        private void LateUpdate()
        {
            if (_initialized || !_built) return;

            Transform player = ResolvePlayer();
            if (player == null) return;

            _rig.Manager.SetTarget(player);
            _rig.Manager.Initialize();
            _initialized = true;
        }

        private Transform ResolvePlayer()
        {
            if (playerSpawner != null && playerSpawner.Current != null)
                return playerSpawner.Current.transform;

            // Dự phòng cho map được mở thẳng để test, khi PlayerSpawner không chạy.
            var movement = PlayerMovement.Instance;
            return movement != null ? movement.transform : null;
        }

        /// <summary>
        /// Dọn trạng thái TĨNH khi rời scene.
        ///
        /// Pool tự dọn chunk root của nó, nhưng <see cref="PlanarSteeringWorld"/> là registry tĩnh:
        /// enemy của map cũ sẽ nằm lại trong đó và làm mọi truy vấn hàng xóm của map mới chậm dần nếu
        /// không xoá. Đây đúng là kiểu rò rỉ chỉ lộ ra sau vài lần đổi map.
        /// </summary>
        private void OnDestroy()
        {
            PlanarSteeringWorld.Clear();
            if (_rig.Pool != null) _rig.Pool.BeginTeardown();
        }
    }
}
