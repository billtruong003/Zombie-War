using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Cấu hình duy nhất cho world streaming — gom hết hằng số vào một chỗ thay vì rải magic number
    /// khắp manager/pool/debug view.
    ///
    /// Giá trị khởi điểm là GIẢ THUYẾT KIỂM THỬ (32 m / radius 2 / 25 chunk), không phải hằng số vĩnh viễn.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/World Streaming Config", fileName = "WorldStreamingConfig")]
    public class WorldStreamingConfig : ScriptableObject
    {
        [Header("World")]
        [Tooltip("Hạt giống thế giới. Cùng hạt giống + cùng toạ độ toàn cục = cùng kết quả, mãi mãi.")]
        [SerializeField] private int worldSeed = 20260809;

        [Header("Chunk")]
        [Tooltip("Cạnh của một chunk logic, mét.")]
        [SerializeField] private float chunkSize = 32f;

        [Tooltip("Số đỉnh trên mỗi cạnh của mesh nền. 17 → 289 đỉnh, 512 tam giác.")]
        [SerializeField] private int groundResolution = GroundMeshBuilder.DefaultResolution;

        [Tooltip("Bán kính ring theo chunk. 2 → đường kính 5 → 25 chunk active.")]
        [SerializeField] private int renderRadius = 2;

        [Tooltip("Số chunk root dự phòng ngoài ring bắt buộc. M0 dùng 0 để pool đúng bằng 25.")]
        [SerializeField] private int poolSpareCount = 0;

        [Header("Decoration")]
        [Tooltip("Palette trang tri. De trong = the gioi khong co cay co, phan con lai van chay.")]
        [SerializeField] private DecorationPalette decorationPalette;

        [Tooltip("Tat de so sanh hieu nang va de soi rieng mat dat.")]
        [SerializeField] private bool decorationEnabled = true;

        [Tooltip("So chunk duoc sinh trang tri toi da trong MOT frame. 1 = trai deu nhat, hitch thap nhat.")]
        [SerializeField] private int decorationJobsPerFrame = 1;

        [Header("Distance density (M3B.2D)")]
        [Tooltip("Bat giam mat do cay co nho o vong ngoai.")]
        [SerializeField] private bool distanceDensityEnabled = true;

        [Tooltip("Ban kinh Chebyshev cua vung GAN. 1 -> 3x3 chunk giu mat do day du.")]
        [SerializeField] private int nearDensityRadius = 1;

        [Tooltip("Ti le cay co nho giu lai trong vung gan.")]
        [Range(0f, 1f)]
        [SerializeField] private float nearFoliageDensity = 1f;

        [Tooltip("Ti le cay co nho giu lai o vong ngoai. Phai <= nearFoliageDensity.")]
        [Range(0f, 1f)]
        [SerializeField] private float outerFoliageDensity = 0.4f;


        [Header("Shared gameplay surface")]
        [Tooltip("Cạnh của mặt collider dùng chung, mét. Phải lớn hơn ring vật lý cộng biên di chuyển.")]
        [SerializeField] private float surfaceFootprint = 256f;

        [Tooltip("Độ dày collider — đủ dày để vật thể di chuyển nhanh không xuyên qua.")]
        [SerializeField] private float surfaceThickness = 4f;

        public DecorationPalette DecorationPalette => decorationPalette;
        public bool DecorationEnabled => decorationEnabled;

        /// <summary>
        /// Ngân sách đếm theo SỐ VIỆC, không theo thời gian.
        ///
        /// Chọn đếm việc vì nó tái lập được: cùng một tuyến chạy trên cùng một máy luôn cho cùng số
        /// frame để hội tụ, nên test khẳng định được cận trên. Ngân sách theo đồng hồ thì phải đọc
        /// Stopwatch trên đường chạy nóng ngay cả khi không đo, đúng thứ M3A.1 vừa gỡ bỏ.
        /// </summary>
        public int DecorationJobsPerFrame => decorationJobsPerFrame;

        public bool DistanceDensityEnabled => distanceDensityEnabled;
        public int NearDensityRadius => nearDensityRadius;
        public float NearFoliageDensity => nearFoliageDensity;
        public float OuterFoliageDensity => outerFoliageDensity;

        /// <summary>Số chunk thuộc vùng gần với cấu hình hiện tại.</summary>
        public int NearChunkCount => distanceDensityEnabled
            ? ChunkCoord.RingCount(Mathf.Min(nearDensityRadius, renderRadius))
            : ActiveChunkCount;

        public int OuterChunkCount => ActiveChunkCount - NearChunkCount;

        /// <summary>
        /// Bậc mật độ của <paramref name="coord"/> khi gốc streaming ở <paramref name="origin"/>.
        ///
        /// Dùng khoảng cách Chebyshev cho thống nhất với chính hình dạng ring: ring là hình vuông, nên
        /// "vòng thứ mấy" và "xa bao nhiêu" phải là cùng một phép đo. Dùng Euclid ở đây sẽ khiến bốn
        /// chunk góc của vòng 1 rơi sang bậc ngoài trong khi chúng vẫn là hàng xóm trực tiếp.
        /// </summary>
        public DecorationDensityTier TierFor(ChunkCoord coord, ChunkCoord origin)
        {
            if (!distanceDensityEnabled) return DecorationDensityTier.Near;

            int ring = Mathf.Max(Mathf.Abs(coord.X - origin.X), Mathf.Abs(coord.Z - origin.Z));
            return ring <= nearDensityRadius ? DecorationDensityTier.Near : DecorationDensityTier.Outer;
        }

        /// <summary>Tỉ lệ cây cỏ nhỏ giữ lại ở một bậc.</summary>
        public float FoliageDensityFor(DecorationDensityTier tier)
        {
            if (!distanceDensityEnabled) return DecorationSampler.FullDensity;

            return tier == DecorationDensityTier.Near ? nearFoliageDensity : outerFoliageDensity;
        }
        public int WorldSeed => worldSeed;
        public int GroundResolution => groundResolution;
        public int GroundVertexCount => GroundMeshBuilder.VertexCount(groundResolution);
        public int GroundTriangleCount => GroundMeshBuilder.TriangleCount(groundResolution);
        public float ChunkSize => chunkSize;
        public int RenderRadius => renderRadius;
        public int ActiveDiameter => renderRadius * 2 + 1;
        public int ActiveChunkCount => ChunkCoord.RingCount(renderRadius);
        public int PoolCapacity => ActiveChunkCount + Mathf.Max(0, poolSpareCount);
        public float SurfaceFootprint => surfaceFootprint;
        public float SurfaceThickness => surfaceThickness;


        /// <summary>
        /// Kiểm tra cấu hình trước khi prewarm. Cấu hình sai phải fail rõ ràng, không được âm thầm
        /// dựng ra một ring hỏng.
        /// </summary>
        public bool Validate(out string error)
        {
            if (chunkSize <= 0f)
            {
                error = $"chunkSize phải > 0 (đang là {chunkSize}).";
                return false;
            }

            if (renderRadius < 0)
            {
                error = $"renderRadius phải >= 0 (đang là {renderRadius}).";
                return false;
            }

            if (groundResolution < 2)
            {
                error = $"groundResolution phải >= 2 (đang là {groundResolution}).";
                return false;
            }

            if (GroundVertexCount > 65535)
            {
                error = $"groundResolution {groundResolution} cho {GroundVertexCount} đỉnh, vượt giới hạn UInt16.";
                return false;
            }

            if (PoolCapacity < ActiveChunkCount)
            {
                error = $"Sức chứa pool ({PoolCapacity}) nhỏ hơn số chunk active bắt buộc ({ActiveChunkCount}).";
                return false;
            }

            if (surfaceThickness <= 0f)
            {
                error = $"surfaceThickness phải > 0 (đang là {surfaceThickness}).";
                return false;
            }



            float requiredFootprint = ActiveDiameter * chunkSize;
            if (surfaceFootprint < requiredFootprint)
            {
                error = $"surfaceFootprint ({surfaceFootprint}) nhỏ hơn ring vật lý ({requiredFootprint}).";
                return false;
            }

            // Ngân sách 0 hoặc âm khiến hàng đợi không bao giờ chạy: thế giới đứng nguyên mặt đất trần
            // và không có gì báo lỗi. Phải chặn ngay ở đây chứ không để nó im lặng hỏng.
            if (decorationJobsPerFrame < 1)
            {
                error = $"decorationJobsPerFrame phải >= 1 (đang là {decorationJobsPerFrame}).";
                return false;
            }

            if (nearFoliageDensity < 0f || nearFoliageDensity > 1f)
            {
                error = $"nearFoliageDensity phải trong [0,1] (đang là {nearFoliageDensity}).";
                return false;
            }

            if (outerFoliageDensity < 0f || outerFoliageDensity > 1f)
            {
                error = $"outerFoliageDensity phải trong [0,1] (đang là {outerFoliageDensity}).";
                return false;
            }

            if (outerFoliageDensity > nearFoliageDensity)
            {
                error = $"outerFoliageDensity ({outerFoliageDensity}) không được lớn hơn " +
                        $"nearFoliageDensity ({nearFoliageDensity}) — vòng ngoài phải là tập con của vùng gần.";
                return false;
            }

            if (nearDensityRadius < 0)
            {
                error = $"nearDensityRadius phải >= 0 (đang là {nearDensityRadius}).";
                return false;
            }

            if (nearDensityRadius > renderRadius)
            {
                error = $"nearDensityRadius ({nearDensityRadius}) vượt renderRadius ({renderRadius}).";
                return false;
            }

            // Bật tính năng mà không có chunk nào rơi vào vòng ngoài thì cấu hình đang nói dối: báo cáo
            // sẽ ghi "đã bật giảm mật độ" trong khi thực tế không giảm gì. Bắt lỗi ngay còn hơn để nó
            // im lặng trôi vào một lần đo.
            if (distanceDensityEnabled && nearDensityRadius >= renderRadius)
            {
                error = $"distanceDensityEnabled nhưng nearDensityRadius ({nearDensityRadius}) phủ kín " +
                        $"renderRadius ({renderRadius}) — không còn chunk nào ở vòng ngoài.";
                return false;
            }

            if (decorationEnabled && decorationPalette != null && !decorationPalette.Validate(out string paletteError))
            {
                error = $"DecorationPalette khong hop le: {paletteError}";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Gan palette trang tri. Dung boi builder o Editor va boi test.</summary>
        public void SetDecorationPalette(DecorationPalette value) => decorationPalette = value;

        public void SetDecorationEnabled(bool value) => decorationEnabled = value;

        /// <summary>
        /// Đặt hạt giống thế giới. Dùng bởi tích hợp production để mỗi màn mở ra một vùng đất riêng
        /// trên cùng một asset config dùng chung.
        /// </summary>
        public void SetWorldSeed(int value) => worldSeed = value;

        /// <summary>Đặt ngân sách việc mỗi frame. Dùng bởi builder ở Editor và bởi test.</summary>
        public void SetDecorationJobsPerFrame(int value) => decorationJobsPerFrame = value;

        /// <summary>Đặt cấu hình mật độ theo khoảng cách. Dùng bởi builder ở Editor và bởi test.</summary>
        public void SetDistanceDensity(bool enabled, int radius, float near, float outer)
        {
            distanceDensityEnabled = enabled;
            nearDensityRadius = radius;
            nearFoliageDensity = near;
            outerFoliageDensity = outer;
        }

        /// <summary>Tạo config trong bộ nhớ cho test — không cần asset trên đĩa.</summary>
        public static WorldStreamingConfig CreateRuntime(
            float chunkSize = 32f,
            int renderRadius = 2,
            int poolSpareCount = 0,
            float surfaceFootprint = 256f,
            float surfaceThickness = 4f,
            int worldSeed = 20260809,
            int groundResolution = GroundMeshBuilder.DefaultResolution,
            int decorationJobsPerFrame = 1)
        {
            var config = CreateInstance<WorldStreamingConfig>();
            config.decorationJobsPerFrame = decorationJobsPerFrame;
            config.chunkSize = chunkSize;
            config.renderRadius = renderRadius;
            config.poolSpareCount = poolSpareCount;
            config.surfaceFootprint = surfaceFootprint;
            config.surfaceThickness = surfaceThickness;
            config.worldSeed = worldSeed;
            config.groundResolution = groundResolution;
            return config;
        }

        private void OnValidate()
        {
            if (chunkSize <= 0f) chunkSize = 1f;
            if (renderRadius < 0) renderRadius = 0;
            if (poolSpareCount < 0) poolSpareCount = 0;
            if (surfaceThickness <= 0f) surfaceThickness = 0.1f;
            if (groundResolution < 2) groundResolution = 2;
        }
    }
}
