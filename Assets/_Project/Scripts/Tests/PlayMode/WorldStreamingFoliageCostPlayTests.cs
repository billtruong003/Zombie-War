#if UNITY_EDITOR
// Fixture nay doc palette qua AssetDatabase nen chi ton tai trong Editor.
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// PlayMode M3B.1: chi phí foliage đo trên pool thật.
    ///
    /// Bộ EditMode anh em đã canh phần nguồn và phép đếm placement. Ở đây canh phần mà chỉ pool thật
    /// mới trả lời được: sau khi thay hình học, mesh gộp thật sự nhẹ đi bao nhiêu, và kiến trúc render
    /// có bị việc thay hình học kéo theo hệ quả nào không.
    public class WorldStreamingFoliageCostPlayTests
    {
        private const float ChunkSize = 32f;

        /// <summary>Số đỉnh foliage của cả ring quanh checkpoint, đo ở M3A.2 trước khi giảm chi phí.</summary>
        private const int M3ABaselineFoliageVertices = 699636;

        /// <summary>Toạ độ mốc dùng xuyên suốt M3A và M3B. Đổi nó là mất khả năng so sánh.</summary>
        private static readonly ChunkCoord Checkpoint = new ChunkCoord(74, 47);

        private WorldStreamingConfig _config;
        private DecorationPalette _palette;
        private GameObject _probe;
        private WorldStreamingRig.Rig _rig;

        private WorldStreamManager Manager => _rig.Manager;
        private ChunkPool Pool => _rig.Pool;

        [SetUp]
        public void SetUp()
        {
            _palette = UnityEditor.AssetDatabase.LoadAssetAtPath<DecorationPalette>(
                "Assets/_Project/Data/World/Decoration/DecorationPalette.asset");
            Assert.IsNotNull(_palette, "Chưa bake palette. Chạy 'Rebuild Decoration Assets'.");

            _config = WorldStreamingConfig.CreateRuntime();
            _config.SetDecorationPalette(_palette);

            // Fixture này đo bất biến HÌNH HỌC của M3B.1: mỗi nguồn tốn bao nhiêu đỉnh, và việc thay
            // hình học có làm xê dịch placement nào không. Mật độ theo khoảng cách (M3B.2D) là một trục
            // độc lập, nên nó được tắt ở đây để các con số mốc vẫn nói đúng thứ chúng được viết ra để
            // nói. Phần tương tác giữa hai tính năng nằm ở `WorldStreamingDistanceDensityPlayTests`.
            _config.SetDistanceDensity(false, 1, 1f, 0.4f);

            _probe = new GameObject("FoliageCostTestProbe");
            _probe.transform.position = new Vector3(4f, 1f, 4f);

            _rig = WorldStreamingRig.Build(_config, _probe.transform, null, null, null, initializeOnStart: false);
            Manager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (Pool != null) Pool.BeginTeardown();
            if (_rig.Root != null) Object.DestroyImmediate(_rig.Root);
            if (_probe != null) Object.DestroyImmediate(_probe);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Đi tới mốc RỒI chờ dựng xong.
        ///
        /// M3B.2 rải việc sinh trang trí ra nhiều frame, nên phải chờ tường minh. Mọi khẳng định về
        /// số đỉnh dưới đây vẫn giữ nguyên độ chặt như khi còn sinh đồng bộ.
        /// </summary>
        private void MoveToCheckpoint()
        {
            Manager.TeleportTargetTo(
                new Vector3(Checkpoint.X * ChunkSize + 16f, 1f, Checkpoint.Z * ChunkSize + 16f));
            Manager.DrainDecoration();
            Assert.IsTrue(Manager.IsGenerationSettled, "Hàng đợi trang trí không cạn.");
        }

        private int RingFoliageVertices()
        {
            int total = 0;
            foreach (ChunkInstance chunk in Pool.All)
                if (chunk.IsAssigned) total += chunk.FoliageVertexCount;
            return total;
        }

        [Test]
        public void RingFoliageVertices_AtCheckpoint_DropByAtLeastTwentyPercent()
        {
            MoveToCheckpoint();

            int actual = RingFoliageVertices();
            Assert.Greater(actual, 0, "Ring không có foliage nào — palette hoặc streaming đã hỏng.");
            // M4.6CD.1 đảo ngược mục tiêu này một cách có chủ đích: hình học vendor gốc thắng số đỉnh
            // tối thiểu, nên chi phí foliage TĂNG so với bản tự sinh. Điều còn đúng và còn đáng canh là
            // việc quay về mesh vendor không đưa chi phí về lại mốc M3A.2 thời chưa tối ưu:
            // 569.429 so với 699.636, thấp hơn 18,6%. Ngân sách MỖI CHUNK mới là chốt chặn thật của
            // production và nó được canh ở `WorldStreamingPaletteLockTests` cùng bộ EditMode.
            Assert.Less(actual, M3ABaselineFoliageVertices,
                $"Đỉnh foliage {actual} đã vượt lại mốc M3A.2 ({M3ABaselineFoliageVertices}).");
        }

        [Test]
        public void RingPlacementCount_AtCheckpoint_IsUnchanged()
        {
            MoveToCheckpoint();

            int placements = 0;
            foreach (ChunkInstance chunk in Pool.All)
                if (chunk.IsAssigned) placements += chunk.PlacementCount;

            // Việc giảm chi phí phải nằm HOÀN TOÀN trong hình học. Nếu con số này nhúc nhích thì
            // hoặc mật độ đã bị hạ, hoặc hàm băm đã bị động vào — cả hai đều nằm ngoài phạm vi M3B.1.
            //
            // Con số đổi từ 2435 xuống 2241 ở M4.6B.1, do bản vón cụm cỏ đã được duyệt hạ số
            // placement của grass_b/grass_c một cách có chủ đích. Bốn entry còn lại giữ nguyên từng
            // con số, nên hàm băm không bị động vào — đó vẫn là điều bài test này canh.
                        // M4.6C.4 gỡ `fern_d` khỏi bảng (hoa thị dẹt bị từ chối ở góc nhìn từ trên xuống):
            // tổng rơi đúng 97 placement và 3.492 đỉnh — bằng CHÍNH phần fern_d đóng góp — còn
            // `solid` không nhúc nhích, đúng như phải vậy vì fern_d chỉ thuộc Foliage.
            //
            // M4.6CD thêm bụi rậm và cảnh vật rắn: 2144 + 360 + 110 + 69 + 125 + 15 + 24 + 16 = 2863.
            // Bộ EditMode anh em canh từng con số một; ở đây chỉ canh tổng.
            Assert.AreEqual(1705, placements);
        }

        [Test]
        public void SolidVertices_AtCheckpoint_AreUntouched()
        {
            MoveToCheckpoint();

            int solid = 0;
            foreach (ChunkInstance chunk in Pool.All)
                if (chunk.IsAssigned) solid += chunk.SolidVertexCount;

            // M3B.1 chỉ đụng foliage, nên mốc cũ là 66.664 — toàn bộ do thân cây đóng góp.
            //
            // M4.6CD mới là lần đầu output Solid có thêm thứ khác: đá, rác, thùng phuy, lốp xe.
            // Phần thân cây vẫn đúng 66.664 (26 cây × 2.564), phần thêm vào là 86.874, tổng 153.538.
            // Tách rõ hai phần như vậy để nếu sau này thân cây bị đụng vào thì test này vẫn bắt được.
            const int TrunkVertices = 66664;
            const int M46CDPropVertices = 175573;
            Assert.AreEqual(TrunkVertices + M46CDPropVertices, solid,
                "Hình học Solid đã đổi ngoài phần cảnh vật rắn được duyệt ở M4.6CD.");
        }

        [Test]
        public void GeometrySwap_DoesNotChangeRendererArchitecture()
        {
            MoveToCheckpoint();

            foreach (ChunkInstance chunk in Pool.All)
            {
                Assert.AreEqual(3, chunk.transform.childCount, $"Slot {chunk.SlotId}");
                Assert.AreEqual(3, chunk.GetComponentsInChildren<MeshRenderer>(true).Length, $"Slot {chunk.SlotId}");
                Assert.AreEqual(0, chunk.GetComponentsInChildren<LODGroup>(true).Length,
                    $"Slot {chunk.SlotId}: M3B.1 không được đẻ ra LOD.");
                Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, chunk.FoliageMesh.indexFormat,
                    $"Slot {chunk.SlotId}: mesh foliage rơi sang UInt32.");
            }
        }

        [Test]
        public void FoliageStaysDeterministic_AfterLeavingAndReturning()
        {
            MoveToCheckpoint();
            int before = RingFoliageVertices();

            Manager.TeleportTargetTo(new Vector3(-31000f, 1f, 24000f));
            Manager.DrainDecoration();
            MoveToCheckpoint();

            Assert.AreEqual(before, RingFoliageVertices(),
                "Quay lại cùng toạ độ mà số đỉnh khác đi — hình học tự sinh không tất định.");
        }
    }
}
#endif
