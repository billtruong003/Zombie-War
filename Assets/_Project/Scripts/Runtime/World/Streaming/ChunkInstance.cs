using System;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Một slot pool: cái vỏ GameObject hiển thị MỘT toạ độ logic tại một thời điểm.
    ///
    /// Nó chỉ sở hữu trạng thái trình bày. Nó KHÔNG sở hữu dữ liệu thế giới, không sở hữu persistence,
    /// và <see cref="SlotId"/> không bao giờ là danh tính thế giới — danh tính thế giới là
    /// <see cref="ChunkCoord"/>.
    ///
    /// Hierarchy dựng sẵn đúng cấu trúc renderer tương lai (Ground / SolidDecor / Foliage), để M2
    /// chỉ việc đổ mesh vào chứ không phải dựng lại cây con.
    ///
    /// M1: slot sở hữu đúng MỘT <see cref="Mesh"/> nền suốt đời. Đổi toạ độ chỉ ghi lại vertex color,
    /// không bao giờ cấp phát Mesh mới.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChunkInstance : MonoBehaviour
    {
        [SerializeField] private int slotId = -1;
        [SerializeField] private MeshFilter groundFilter;
        [SerializeField] private MeshRenderer groundRenderer;
        [SerializeField] private MeshFilter solidDecorFilter;
        [SerializeField] private MeshRenderer solidDecorRenderer;
        [SerializeField] private MeshFilter foliageFilter;
        [SerializeField] private MeshRenderer foliageRenderer;

        private Mesh _groundMesh;
        private Mesh _solidMesh;
        private Mesh _foliageMesh;
        private int _groundResolution;
        private float _chunkSize;
        private int _worldSeed;
        private WorldStreamingConfig _config;

        private static readonly System.Collections.Generic.List<DecorationPlacement> PlacementScratch =
            new System.Collections.Generic.List<DecorationPlacement>(512);

        /// <summary>Pool đăng ký để đếm huỷ root — phân biệt huỷ lúc teardown và huỷ khi đang traverse.</summary>
        internal Action<ChunkInstance> Destroyed;

        public int SlotId => slotId;
        public ChunkCoord Coord { get; private set; }
        public bool IsAssigned { get; private set; }

        /// <summary>Số lần slot này được gán một toạ độ — dùng để đếm tái sử dụng.</summary>
        public int AssignmentCount { get; private set; }

        public MeshFilter GroundFilter => groundFilter;
        /// <summary>
        /// Bit rendering-layer dành cho viền CẢNH VẬT MÔI TRƯỜNG (M4.6CD.1).
        ///
        /// Bit 0–3 đã có chủ theo hợp đồng viền nhân vật: 1 = mặc định, 2 = vũ khí, 4 = quái, 8 = người
        /// chơi (mặt nạ chọn 14). Bit 4 là bit thấp nhất còn trống, đã kiểm trong phiên gameplay thật.
        /// Mặt nạ chọn của production do đó thành <c>14 | 16 = 30</c>.
        ///
        /// Đây là kênh THUẦN HÌNH ẢNH (<c>Renderer.renderingLayerMask</c>), không phải layer vật lý của
        /// GameObject — nên nó không đụng tới va chạm, raycast hay ma trận va chạm.
        /// </summary>
        public const uint EnvironmentOutlineRenderingBit = 1u << 4;

        public MeshRenderer GroundRenderer => groundRenderer;
        public MeshFilter SolidDecorFilter => solidDecorFilter;
        public MeshRenderer SolidDecorRenderer => solidDecorRenderer;
        public MeshFilter FoliageFilter => foliageFilter;
        public MeshRenderer FoliageRenderer => foliageRenderer;

        /// <summary>Mesh nền dùng lại của slot này. Tham chiếu phải giữ nguyên suốt vòng đời pool.</summary>
        public Mesh GroundMesh => _groundMesh;

        public int GroundResolution => _groundResolution;

        /// <summary>Mesh gop dung lai cho trang tri dac. Tham chieu giu nguyen suot vong doi pool.</summary>
        public Mesh SolidMesh => _solidMesh;

        /// <summary>Mesh gop dung lai cho foliage. Tham chieu giu nguyen suot vong doi pool.</summary>
        public Mesh FoliageMesh => _foliageMesh;

        /// <summary>So lan dat trang tri cua toa do dang gan. Tat ca deu la du lieu, khong GameObject.</summary>
        public int PlacementCount { get; private set; }

        public int SolidVertexCount { get; private set; }
        public int SolidTriangleCount { get; private set; }
        public int FoliageVertexCount { get; private set; }
        public int FoliageTriangleCount { get; private set; }

        /// <summary>So lieu cua lan sinh gan nhat.</summary>
        public DecorationSampleStats DecorationStats { get; private set; }

        /// <summary>Thoi gian sinh du lieu va thoi gian do vao Mesh, mili-giay.</summary>
        public float LastGenerateMs { get; private set; }

        public float LastApplyMs { get; private set; }

        /// <summary>
        /// Vé thế hệ của slot, tăng mỗi lần slot đổi chủ (gán mới hoặc trả về pool).
        ///
        /// Bộ lập lịch chụp lại vé lúc xếp hàng và so lại lúc áp kết quả. Nếu slot đã đổi chủ giữa hai
        /// thời điểm đó thì vé lệch, và việc cũ bị vứt đi thay vì đắp hình học của toạ độ cũ lên toạ độ
        /// mới. Đây là hàng rào chính chống việc áp nhầm sau khi người chơi teleport liên tục.
        /// </summary>
        public int DecorationTicket { get; private set; }

        /// <summary>Slot đã được gán toạ độ nhưng trang trí còn đang xếp hàng chờ sinh.</summary>
        public bool DecorationPending { get; private set; }

        /// <summary>
        /// Bậc mật độ mà nội dung trang trí HIỆN TẠI mô tả — hoặc, khi còn đang chờ, bậc mà việc trong
        /// hàng đợi sẽ sinh ra.
        /// </summary>
        public DecorationDensityTier DecorationTier { get; private set; }

        /// <summary>Bậc mật độ của hình học đã thật sự nằm trong mesh. Khác <see cref="DecorationTier"/>
        /// trong lúc một lần đổi bậc còn đang chờ.</summary>
        public DecorationDensityTier BuiltDecorationTier { get; private set; }

        /// <summary>
        /// Dựng một chunk root hoàn chỉnh bằng code. Dựng bằng code (thay vì prefab) giữ cho lab
        /// không phụ thuộc asset ngoài và không có gì để lệch giữa scene với test.
        /// </summary>
        public static ChunkInstance Create(int slotId, Transform parent, WorldStreamingConfig config,
            Material groundMaterial, Material solidMaterial, Material foliageMaterial)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            // Ten dat MOT lan va khong bao gio doi nua: on dinh theo slot, khong theo toa do.
            var rootGO = new GameObject($"ChunkRoot_{slotId:D2}");
            rootGO.transform.SetParent(parent, false);

            var instance = rootGO.AddComponent<ChunkInstance>();
            instance.slotId = slotId;
            instance._groundResolution = config.GroundResolution;
            instance._chunkSize = config.ChunkSize;
            instance._worldSeed = config.WorldSeed;
            instance._config = config;

            Material ground = groundMaterial != null ? groundMaterial : ChunkDiagnosticAssets.FallbackGroundMaterial;
            Material solid = solidMaterial != null ? solidMaterial : ChunkDiagnosticAssets.FallbackSolidMaterial;
            Material foliage = foliageMaterial != null ? foliageMaterial : ChunkDiagnosticAssets.FallbackFoliageMaterial;

            instance.groundFilter = CreateRenderSlot(rootGO.transform, "Ground", ground, out instance.groundRenderer);
            instance.solidDecorFilter = CreateRenderSlot(rootGO.transform, "SolidDecor", solid, out instance.solidDecorRenderer);
            instance.foliageFilter = CreateRenderSlot(rootGO.transform, "Foliage", foliage, out instance.foliageRenderer);

            // M4.6CD.1 — viền cho CẢNH VẬT RẮN, và chỉ cho nó.
            //
            // Mặt đất mà có viền thì mỗi chunk hiện thành một ô vuông; cây cỏ mà có viền thì hàng nghìn
            // lá cỏ mỗi cái một đường kẻ, thành nhiễu. Đá, thùng phuy, lốp xe thì ngược lại: chúng là
            // vật thể rời rạc, có viền mới tách khỏi nền và đọc ra ngay.
            //
            // Đặt ở đây — nơi renderer được TẠO RA — nên nó đúng ngay từ frame đầu và không phụ thuộc
            // vào một công cụ Editor nào chạy sau. Ba renderer này do pool sở hữu và không bao giờ bị
            // huỷ, nên gán một lần lúc warmup là đủ.
            instance.solidDecorRenderer.renderingLayerMask |= EnvironmentOutlineRenderingBit;
            instance.groundRenderer.renderingLayerMask &= ~EnvironmentOutlineRenderingBit;
            instance.foliageRenderer.renderingLayerMask &= ~EnvironmentOutlineRenderingBit;

            // Mesh nền phủ kín 32×32 m, không chừa khe. Biên chunk giờ nhìn qua Gizmo/nhãn,
            // không phải bằng cách thu nhỏ hình học.
            instance._groundMesh = GroundMeshBuilder.CreateGroundMesh(
                $"ChunkGround_{slotId:D2}", config.GroundResolution, config.ChunkSize);
            instance.groundFilter.sharedMesh = instance._groundMesh;

            // M1: chỉ Ground có hình. SolidDecor/Foliage giữ nguyên cây con nhưng tắt renderer,
            // đúng luật "output rỗng thì tắt renderer, không tạo/huỷ object".
            // Mesh trang tri cung duoc tao ngay luc warmup va dung lai mai. Output rong thi TAT
            // renderer chu khong huy object -- cay con cua chunk root khong bao gio thay doi.
            instance._solidMesh = DecorationMeshBuilder.CreateDecorationMesh($"ChunkSolid_{slotId:D2}");
            instance._foliageMesh = DecorationMeshBuilder.CreateDecorationMesh($"ChunkFoliage_{slotId:D2}");
            instance.solidDecorFilter.sharedMesh = instance._solidMesh;
            instance.foliageFilter.sharedMesh = instance._foliageMesh;

            instance.solidDecorRenderer.enabled = false;
            instance.foliageRenderer.enabled = false;

            instance.Release();
            return instance;
        }

        private static MeshFilter CreateRenderSlot(Transform parent, string name, Material material, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var filter = go.AddComponent<MeshFilter>();
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return filter;
        }

        /// <summary>
        /// Gắn slot này vào một toạ độ logic. Đây là phần LÀM NGAY, không bao giờ hoãn.
        ///
        /// Mặt đất phải đúng ngay trong khung hình này: người chơi đứng trên nó, và một lỗ thủng nền
        /// là lỗi không thể chấp nhận. Trang trí thì hoãn được, nên nó chỉ được đánh dấu là đang chờ.
        ///
        /// Trang trí cũ bị XOÁ ngay tại đây chứ không giữ làm hình tạm. Giữ lại sẽ khiến slot hiển thị
        /// cây cỏ của toạ độ cũ tại toạ độ mới — sai rõ ràng và còn khó lần ra hơn là thiếu cây.
        /// </summary>
        public void AssignTo(ChunkCoord coord, float chunkSize) =>
            AssignTo(coord, chunkSize, DecorationDensityTier.Near);

        public void AssignTo(ChunkCoord coord, float chunkSize, DecorationDensityTier tier)
        {
            // Khi profiler tat, BeginSample tra token rong ma khong cham dong ho.
            long assignToken = WorldStreamingProfiler.BeginSample();

            Coord = coord;
            IsAssigned = true;
            AssignmentCount++;
            DecorationTicket++;
            DecorationTier = tier;

            transform.localPosition = coord.ToWorldMin(chunkSize);

            // KHONG doi ten GameObject o day. Doi ten moi lan gan chunk sinh chuoi trong duong chay
            // nong (~234 B/lan do duoc o M3A) chi de phuc vu viec nhin Hierarchy. Toa do hien tai
            // luon doc duoc qua `Coord`, va debug view lay tu do chu khong tu ten object.
            if (_groundMesh != null)
            {
                long groundToken = WorldStreamingProfiler.BeginSample();
                GroundMeshBuilder.ApplyBiome(_groundMesh, coord, _groundResolution, _chunkSize, _worldSeed);
                WorldStreamingProfiler.EndSample(ProfilerStage.GroundBiome, groundToken);
            }

            if (groundRenderer != null) groundRenderer.enabled = true;

            ResetDecorationState();
            ClearDecoration();
            DecorationPending = HasDecorationWork;

            WorldStreamingProfiler.EndSample(ProfilerStage.ChunkAssign, assignToken);
        }

        /// <summary>Config có yêu cầu slot này sinh trang trí hay không.</summary>
        private bool HasDecorationWork =>
            _config != null && _config.DecorationEnabled && _config.DecorationPalette != null;

        private void ResetDecorationState()
        {
            PlacementCount = 0;
            SolidVertexCount = 0;
            SolidTriangleCount = 0;
            FoliageVertexCount = 0;
            FoliageTriangleCount = 0;
            LastGenerateMs = 0f;
            LastApplyMs = 0f;
            DecorationStats = default;
        }

        /// <summary>
        /// Sinh trang tri cho toa do dang gan roi do vao hai mesh gop san co.
        ///
        /// Day la phan HOAN duoc. Bo lap lich goi no, moi frame nhieu nhat vai lan, va chi goi sau khi
        /// da xac nhan slot van con giu dung toa do ma viec do duoc xep hang cho.
        ///
        /// Thu tu quan trong: mesh duoc ghi de TRUOC khi renderer bat len, nen khong co khung hinh nao
        /// slot nay con hien thi cay co cua toa do cu.
        /// </summary>
        public void BuildDecorationNow()
        {
            if (!IsAssigned) return;

            long jobToken = WorldStreamingProfiler.BeginSample();
            RebuildDecoration(Coord);
            WorldStreamingProfiler.EndSample(ProfilerStage.DecorationJob, jobToken);
        }

        /// <summary>
        /// Đổi bậc mật độ của một chunk ĐANG GIỮ NGUYÊN toạ độ. Trả về true nếu cần dựng lại.
        ///
        /// Cố ý KHÔNG xoá hình học cũ ở đây, khác hẳn <see cref="AssignTo"/>. Khi toạ độ đổi, hình học
        /// cũ nằm sai chỗ nên phải xoá ngay. Khi chỉ bậc đổi, hình học cũ vẫn đứng đúng chỗ của chính
        /// toạ độ này — chỉ là dày hoặc thưa hơn mức mong muốn. Xoá nó sẽ tạo ra một khoảng trống nhấp
        /// nháy trên một chunk người chơi đang nhìn thẳng vào, tệ hơn hẳn việc để nguyên vài frame cho
        /// tới khi bản đúng thay thế nguyên khối.
        ///
        /// Vé thế hệ vẫn tăng, nên mọi việc đã xếp hàng cho bậc cũ lập tức hết hạn.
        /// </summary>
        public bool RequestDecorationTier(DecorationDensityTier tier)
        {
            if (!IsAssigned) return false;
            if (DecorationTier == tier && !DecorationPending) return false;
            if (DecorationTier == tier) return false;

            DecorationTier = tier;
            DecorationTicket++;
            DecorationPending = HasDecorationWork;
            return DecorationPending;
        }

        private void RebuildDecoration(ChunkCoord coord)
        {
            ResetDecorationState();
            DecorationPending = false;
            BuiltDecorationTier = DecorationTier;

            DecorationPalette palette = _config != null ? _config.DecorationPalette : null;
            if (palette == null || !_config.DecorationEnabled)
            {
                ClearDecoration();
                return;
            }

            float foliageDensity = _config.FoliageDensityFor(DecorationTier);

            // Moi phep do deu di qua token. Khi profiler tat, khong lan nao doc dong ho —
            // do la ly do LastGenerateMs/LastApplyMs bang 0 khi khong bat capture, va overlay noi ro
            // dieu do thay vi bia ra mot con so.
            DecorationSampleStats stats = default;

            long sampleToken = WorldStreamingProfiler.BeginSample();
            DecorationSampler.Generate(_worldSeed, coord, _chunkSize, palette, PlacementScratch,
                foliageDensity, ref stats);
            float sampleMs = WorldStreamingProfiler.EndSample(ProfilerStage.DecorationSample, sampleToken);

            long buildToken = WorldStreamingProfiler.BeginSample();
            DecorationMeshBuilder.Build(palette, PlacementScratch);
            float buildMs = WorldStreamingProfiler.EndSample(ProfilerStage.DecorationBuild, buildToken);

            long solidToken = WorldStreamingProfiler.BeginSample();
            bool hasSolid = DecorationMeshBuilder.Solid.Apply(_solidMesh, 0f);
            float solidMs = WorldStreamingProfiler.EndSample(ProfilerStage.SolidApply, solidToken);

            long foliageToken = WorldStreamingProfiler.BeginSample();
            bool hasFoliage = DecorationMeshBuilder.Foliage.Apply(_foliageMesh, palette.FoliageBoundsPadding);
            float foliageMs = WorldStreamingProfiler.EndSample(ProfilerStage.FoliageApply, foliageToken);

            solidDecorRenderer.enabled = hasSolid;
            foliageRenderer.enabled = hasFoliage;

            PlacementCount = PlacementScratch.Count;
            DecorationStats = stats;
            SolidVertexCount = DecorationMeshBuilder.Solid.VertexCount;
            SolidTriangleCount = DecorationMeshBuilder.Solid.TriangleCount;
            FoliageVertexCount = DecorationMeshBuilder.Foliage.VertexCount;
            FoliageTriangleCount = DecorationMeshBuilder.Foliage.TriangleCount;

            LastGenerateMs = sampleMs + buildMs;
            LastApplyMs = solidMs + foliageMs;
        }

        private void ClearDecoration()
        {
            if (_solidMesh != null) _solidMesh.Clear(false);
            if (_foliageMesh != null) _foliageMesh.Clear(false);
            if (solidDecorRenderer != null) solidDecorRenderer.enabled = false;
            if (foliageRenderer != null) foliageRenderer.enabled = false;
        }

        /// <summary>Trả slot về trạng thái rỗng. Không huỷ gì cả — đây là điểm mấu chốt của pooling.</summary>
        public void Release()
        {
            IsAssigned = false;
            DecorationPending = false;

            // Tăng vé ngay cả khi trả slot: một việc đã xếp hàng cho toạ độ vừa rời ring phải trở thành
            // hết hạn ngay, kể cả khi slot này chưa được cấp cho toạ độ nào khác.
            DecorationTicket++;

            if (groundRenderer != null) groundRenderer.enabled = false;
            ClearDecoration();
        }

        /// <summary>Mẫu biome ngay tại một vị trí logic trong chunk đang gán — dùng cho overlay debug.</summary>
        public BiomeSample SampleAt(float worldX, float worldZ) => BiomeSampler.Sample(_worldSeed, worldX, worldZ);

        private void OnDestroy()
        {
            Destroyed?.Invoke(this);

            DestroyMesh(ref _groundMesh);
            DestroyMesh(ref _solidMesh);
            DestroyMesh(ref _foliageMesh);
        }

        private static void DestroyMesh(ref Mesh mesh)
        {
            if (mesh == null) return;

            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
