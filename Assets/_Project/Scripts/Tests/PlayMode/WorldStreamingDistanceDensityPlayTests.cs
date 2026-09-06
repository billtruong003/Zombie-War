#if UNITY_EDITOR
// Fixture nay doc palette qua AssetDatabase nen chi ton tai trong Editor.
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// PlayMode M3B.2D: mật độ theo khoảng cách trên pool thật.
    ///
    /// Ca then chốt là chunk GIỮ NGUYÊN toạ độ nhưng đổi bậc khi người chơi bước một ô. Nó không đi qua
    /// đường "chunk mới vào ring", nên rất dễ bị bỏ quên và mắc kẹt vĩnh viễn ở mật độ cũ — vùng thưa
    /// trôi vào sát người chơi mà không có gì báo lỗi.
    public class WorldStreamingDistanceDensityPlayTests
    {
        private const float ChunkSize = 32f;
        private const int ExpectedActive = 25;
        private const int ExpectedNear = 9;
        private const int ExpectedOuter = 16;

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
            _config.SetDistanceDensity(true, 1, 1f, 0.4f);

            _probe = new GameObject("DensityTestProbe");
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
            WorldStreamingProfiler.Release();
        }

        private static Vector3 WorldInside(ChunkCoord coord) =>
            new Vector3(coord.X * ChunkSize + 7.5f, 1f, coord.Z * ChunkSize + 11.25f);

        private void MoveTo(ChunkCoord coord) => Manager.TeleportTargetTo(WorldInside(coord));

        private void MoveToAndSettle(ChunkCoord coord)
        {
            MoveTo(coord);
            Manager.DrainDecoration();
            Assert.IsTrue(Manager.IsGenerationSettled, $"Không dựng xong tại {coord}.");
        }

        private static string MeshChecksum(ChunkInstance chunk)
        {
            var sb = new StringBuilder();
            foreach (Mesh mesh in new[] { chunk.SolidMesh, chunk.FoliageMesh })
            {
                sb.Append(mesh.vertexCount).Append(':').Append(mesh.GetTriangles(0).Length).Append(':');
                Vector3[] positions = mesh.vertices;
                for (int i = 0; i < positions.Length; i += 37)
                    sb.Append(positions[i].x.ToString("F4")).Append(',')
                      .Append(positions[i].y.ToString("F4")).Append(',')
                      .Append(positions[i].z.ToString("F4")).Append(';');
                sb.Append('|');
            }

            return sb.ToString();
        }

        private string RingChecksum()
        {
            var coords = new List<ChunkCoord>();
            Manager.CopyActiveCoords(coords);
            coords.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X));

            var sb = new StringBuilder();
            foreach (ChunkCoord coord in coords)
                sb.Append(coord).Append('=').Append(MeshChecksum(Manager.ActiveLeases[coord])).Append('\n');

            return sb.ToString();
        }

        /// <summary>Không chunk nào được giữ hình học của một bậc khác bậc hiện tại của nó.</summary>
        private void AssertNoTierMismatch()
        {
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                DecorationDensityTier expected = _config.TierFor(lease.Key, Manager.CurrentChunk);
                Assert.AreEqual(expected, lease.Value.DecorationTier, $"{lease.Key}: bậc mục tiêu sai.");
                Assert.AreEqual(expected, lease.Value.BuiltDecorationTier,
                    $"{lease.Key}: hình học vẫn là của bậc {lease.Value.BuiltDecorationTier}.");
            }
        }

        private int CountTier(DecorationDensityTier tier)
        {
            int n = 0;
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                if (lease.Value.BuiltDecorationTier == tier) n++;
            return n;
        }

        // --- Bố cục bậc -----------------------------------------------------------------------------

        [Test]
        public void SettledRing_HasNineNearAndSixteenOuterChunks()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));

            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount);
            Assert.AreEqual(ExpectedNear, CountTier(DecorationDensityTier.Near));
            Assert.AreEqual(ExpectedOuter, CountTier(DecorationDensityTier.Outer));
            AssertNoTierMismatch();
        }

        [Test]
        public void OuterChunks_CarryFewerFoliageVerticesThanTheSameChunkWouldNear()
        {
            var centre = new ChunkCoord(74, 47);
            MoveToAndSettle(centre);

            // Một chunk cụ thể ở vòng ngoài, so với chính nó khi được dựng ở bậc gần.
            var probe = new ChunkCoord(centre.X + 2, centre.Z);
            int outerVertices = Manager.ActiveLeases[probe].FoliageVertexCount;

            var full = new List<DecorationPlacement>(512);
            DecorationSampleStats stats = default;
            DecorationSampler.Generate(_config.WorldSeed, probe, ChunkSize, _palette, full,
                DecorationSampler.FullDensity, ref stats);

            Assert.Less(outerVertices, stats.FoliageVertices,
                "Chunk vòng ngoài phải nhẹ hơn chính nó ở mật độ đầy đủ.");
            Assert.Greater(outerVertices, 0, "Vòng ngoài vẫn phải có cây, không được rỗng.");
        }

        [Test]
        public void NearChunks_MatchTheFullDensityReference()
        {
            var centre = new ChunkCoord(74, 47);
            MoveToAndSettle(centre);

            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                if (lease.Value.BuiltDecorationTier != DecorationDensityTier.Near) continue;

                var expected = new List<DecorationPlacement>(512);
                DecorationSampleStats stats = default;
                DecorationSampler.Generate(_config.WorldSeed, lease.Key, ChunkSize, _palette, expected,
                    DecorationSampler.FullDensity, ref stats);

                Assert.AreEqual(expected.Count, lease.Value.PlacementCount, $"{lease.Key}");
                Assert.AreEqual(stats.FoliageVertices, lease.Value.FoliageVertexCount, $"{lease.Key}");
            }
        }

        [Test]
        public void OuterChunks_MatchTheDeterministicReducedSubset()
        {
            var centre = new ChunkCoord(74, 47);
            MoveToAndSettle(centre);

            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                if (lease.Value.BuiltDecorationTier != DecorationDensityTier.Outer) continue;

                var expected = new List<DecorationPlacement>(512);
                DecorationSampleStats stats = default;
                DecorationSampler.Generate(_config.WorldSeed, lease.Key, ChunkSize, _palette, expected,
                    _config.OuterFoliageDensity, ref stats);

                Assert.AreEqual(expected.Count, lease.Value.PlacementCount, $"{lease.Key}");
                Assert.AreEqual(stats.FoliageVertices, lease.Value.FoliageVertexCount, $"{lease.Key}");
            }
        }

        [Test]
        public void SolidGeometry_IsIdenticalToTheDensityDisabledReference()
        {
            var centre = new ChunkCoord(74, 47);
            MoveToAndSettle(centre);

            int withDensity = 0;
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                withDensity += lease.Value.SolidVertexCount;

            // Tham chiếu: tắt hẳn tính năng rồi dựng lại đúng vòng ring đó.
            _config.SetDistanceDensity(false, 1, 1f, 0.4f);
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                lease.Value.AssignTo(lease.Key, ChunkSize, DecorationDensityTier.Near);
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                Manager.DecorationScheduler.Enqueue(lease.Value);
            Manager.DrainDecoration();

            int reference = 0;
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                reference += lease.Value.SolidVertexCount;

            Assert.AreEqual(reference, withDensity, "Mật độ theo khoảng cách đã đụng vào hình học Solid.");
            // 66.664 thân cây (mốc M3B.1) + 86.874 cảnh vật rắn M4.6CD. Điều đang được canh vẫn là:
            // mật độ theo khoảng cách KHÔNG được chạm vào output Solid, dù Solid nay có nhiều loại.
            Assert.AreEqual(242237, withDensity, "Solid ở checkpoint phải khớp mốc M4.6CD.");
        }

        // --- Chuyển bậc của chunk giữ lại -----------------------------------------------------------

        [Test]
        public void AdjacentMovement_RebuildsRetainedNearToOuterChunks()
        {
            var start = new ChunkCoord(74, 47);
            MoveToAndSettle(start);

            // (73,47) đang là GẦN. Bước sang phải hai ô thì nó thành XA mà vẫn nằm trong ring.
            var watched = new ChunkCoord(73, 47);
            ChunkInstance chunk = Manager.ActiveLeases[watched];
            Assert.AreEqual(DecorationDensityTier.Near, chunk.BuiltDecorationTier);
            int nearVertices = chunk.FoliageVertexCount;
            int assignments = chunk.AssignmentCount;

            MoveTo(new ChunkCoord(75, 47));

            Assert.AreSame(chunk, Manager.ActiveLeases[watched], "Chunk phải giữ nguyên slot.");
            Assert.AreEqual(assignments, chunk.AssignmentCount, "Đổi bậc không được tính là gán lại.");
            Assert.AreEqual(DecorationDensityTier.Outer, chunk.DecorationTier, "Bậc mục tiêu chưa cập nhật.");
            Assert.IsTrue(chunk.DecorationPending, "Phải có việc dựng lại đang chờ.");
            Assert.IsFalse(Manager.IsGenerationSettled, "Còn việc đổi bậc mà đã báo lắng.");

            Manager.DrainDecoration();

            Assert.AreEqual(DecorationDensityTier.Outer, chunk.BuiltDecorationTier);
            Assert.Less(chunk.FoliageVertexCount, nearVertices, "Sang vòng ngoài mà không thưa đi.");
            AssertNoTierMismatch();
        }

        [Test]
        public void AdjacentMovement_RebuildsRetainedOuterToNearChunks()
        {
            var start = new ChunkCoord(74, 47);
            MoveToAndSettle(start);

            // (76,47) đang là XA. Bước sang phải một ô thì nó thành GẦN.
            var watched = new ChunkCoord(76, 47);
            ChunkInstance chunk = Manager.ActiveLeases[watched];
            Assert.AreEqual(DecorationDensityTier.Outer, chunk.BuiltDecorationTier);
            int outerVertices = chunk.FoliageVertexCount;

            MoveTo(new ChunkCoord(75, 47));

            Assert.AreEqual(DecorationDensityTier.Near, chunk.DecorationTier);
            Assert.IsTrue(chunk.DecorationPending);

            Manager.DrainDecoration();

            Assert.AreEqual(DecorationDensityTier.Near, chunk.BuiltDecorationTier);
            Assert.Greater(chunk.FoliageVertexCount, outerVertices, "Vào vùng gần mà không dày lên.");
            AssertNoTierMismatch();
        }

        [Test]
        public void CrossingTheBoundaryAndReturning_RestoresIdenticalContent()
        {
            var home = new ChunkCoord(74, 47);
            MoveToAndSettle(home);

            var watched = new ChunkCoord(73, 47);
            string before = MeshChecksum(Manager.ActiveLeases[watched]);
            int placementsBefore = Manager.ActiveLeases[watched].PlacementCount;
            string ringBefore = RingChecksum();

            // Gần → Xa → Gần trên đúng chunk đó.
            MoveToAndSettle(new ChunkCoord(75, 47));
            Assert.AreEqual(DecorationDensityTier.Outer, Manager.ActiveLeases[watched].BuiltDecorationTier);

            MoveToAndSettle(home);

            Assert.AreEqual(DecorationDensityTier.Near, Manager.ActiveLeases[watched].BuiltDecorationTier);
            Assert.AreEqual(placementsBefore, Manager.ActiveLeases[watched].PlacementCount);
            Assert.AreEqual(before, MeshChecksum(Manager.ActiveLeases[watched]),
                "Quay lại bậc cũ mà hình học khác đi — đã có reroll.");
            Assert.AreEqual(ringBefore, RingChecksum(), "Cả vòng ring phải trở lại y hệt.");
        }

        [Test]
        public void SettledState_IsFalseWhileATierCorrectionIsPending()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));
            Assert.IsTrue(Manager.IsGenerationSettled);

            MoveTo(new ChunkCoord(75, 47));

            Assert.IsFalse(Manager.IsGenerationSettled);
            Assert.Greater(Manager.PendingDecorationCount, 0);

            Manager.DrainDecoration();
            Assert.IsTrue(Manager.IsGenerationSettled);
            AssertNoTierMismatch();
        }

        [Test]
        public void ClearingTheQueue_CannotMakeSettledStateLie()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));
            MoveTo(new ChunkCoord(75, 47));

            // Vứt sạch hàng đợi mà không dựng gì: một số chunk vẫn kẹt ở bậc cũ.
            Manager.DecorationScheduler.Clear();

            Assert.AreEqual(0, Manager.PendingDecorationCount);
            Assert.IsFalse(Manager.IsGenerationSettled,
                "Hàng đợi rỗng vì bị huỷ không phải là thế giới đã đúng bậc.");
        }

        [Test]
        public void RetainedChunk_CannotApplyAResultBuiltForItsPreviousTier()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));

            var watched = new ChunkCoord(73, 47);
            ChunkInstance chunk = Manager.ActiveLeases[watched];

            // Xếp hàng một việc ở bậc GẦN, rồi đổi bậc trước khi nó tới lượt.
            Manager.DecorationScheduler.Enqueue(chunk);
            int ticketBefore = chunk.DecorationTicket;

            MoveTo(new ChunkCoord(75, 47));
            Assert.AreNotEqual(ticketBefore, chunk.DecorationTicket, "Đổi bậc phải làm hết hạn vé cũ.");

            Manager.DrainDecoration();

            Assert.AreEqual(DecorationDensityTier.Outer, chunk.BuiltDecorationTier,
                "Đã áp nhầm kết quả của bậc cũ.");
            AssertNoTierMismatch();
        }

        [Test]
        public void RapidTeleport_BeforeSettlement_ConvergesToTheCorrectRing()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));

            var hops = new[]
            {
                new ChunkCoord(900, -700), new ChunkCoord(-1400, 850), new ChunkCoord(41, 39),
                new ChunkCoord(42, 39), new ChunkCoord(-77, -55), new ChunkCoord(74, 47),
            };

            foreach (ChunkCoord hop in hops)
            {
                MoveTo(hop);
                Manager.ProcessDecorationJobs(1);
            }

            Manager.DrainDecoration();

            Assert.IsTrue(Manager.IsGenerationSettled);
            Assert.AreEqual(ExpectedNear, CountTier(DecorationDensityTier.Near));
            Assert.AreEqual(ExpectedOuter, CountTier(DecorationDensityTier.Outer));
            AssertNoTierMismatch();
        }

        [Test]
        public void ReversedRoute_FinishesWithIdenticalContent()
        {
            var home = new ChunkCoord(74, 47);
            MoveToAndSettle(home);
            string expected = RingChecksum();

            var forward = new[]
            {
                new ChunkCoord(75, 47), new ChunkCoord(76, 47), new ChunkCoord(76, 48),
                new ChunkCoord(75, 48), new ChunkCoord(74, 48),
            };

            foreach (ChunkCoord step in forward) MoveToAndSettle(step);
            for (int i = forward.Length - 1; i >= 0; i--) MoveToAndSettle(forward[i]);
            MoveToAndSettle(home);

            Assert.AreEqual(expected, RingChecksum(), "Đi ngược lại cho ra thế giới khác.");
        }

        [Test]
        public void LongMixedRoute_LeavesNoTierMismatchAndNoPoolGrowth()
        {
            MoveToAndSettle(ChunkCoord.Zero);

            var cursor = new ChunkCoord(0, 0);
            for (int lap = 0; lap < 4; lap++)
            {
                for (int i = 0; i < 30; i++)
                {
                    cursor = new ChunkCoord(cursor.X + (lap % 2 == 0 ? 1 : -1), cursor.Z + (lap < 2 ? 1 : -1));
                    MoveTo(cursor);
                    Manager.ProcessDecorationJobs(1);
                }
            }

            Manager.DrainDecoration();

            AssertNoTierMismatch();
            Assert.AreEqual(ExpectedNear, CountTier(DecorationDensityTier.Near));
            Assert.AreEqual(ExpectedOuter, CountTier(DecorationDensityTier.Outer));

            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount);
            Assert.AreEqual(25, Pool.Capacity);
            Assert.AreEqual(0, Pool.RootsCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.RootsDestroyedDuringTraversal);
            Assert.AreEqual(0, Pool.GroundMeshesCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.DecorationMeshesCreatedSinceWarmup);

            var coords = new HashSet<ChunkCoord>();
            var slots = new HashSet<int>();
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                Assert.IsTrue(coords.Add(lease.Key), $"Toạ độ trùng: {lease.Key}");
                Assert.IsTrue(slots.Add(lease.Value.SlotId), $"Slot trùng: {lease.Value.SlotId}");
            }
        }

        [Test]
        public void TierTransitions_NeverExceedTheJobsPerFrameBudget()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));

            // Bước một ô làm 5 chunk mới vào ring VÀ một loạt chunk giữ lại đổi bậc — tất cả phải đi
            // qua cùng một hàng đợi có ngân sách, không được có đường tắt nào dựng ngay.
            for (int step = 0; step < 12; step++)
            {
                MoveTo(new ChunkCoord(74 + step, 47));

                int before = Manager.CompletedDecorationJobs;
                Manager.ProcessDecorationJobs(_config.DecorationJobsPerFrame);
                Assert.LessOrEqual(Manager.CompletedDecorationJobs - before, _config.DecorationJobsPerFrame,
                    $"Nhịp {step}: vượt ngân sách việc/frame.");
            }

            Manager.DrainDecoration();
            AssertNoTierMismatch();
        }

        [Test]
        public void RendererAndMaterialArchitecture_DoesNotRegress()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));
            MoveToAndSettle(new ChunkCoord(75, 47));

            int colliders = 0;
            var materials = new HashSet<Material>();
            foreach (ChunkInstance chunk in Pool.All)
            {
                Assert.AreEqual(3, chunk.transform.childCount, $"Slot {chunk.SlotId}");
                Assert.AreEqual(3, chunk.GetComponentsInChildren<MeshRenderer>(true).Length, $"Slot {chunk.SlotId}");
                Assert.AreEqual(0, chunk.GetComponentsInChildren<LODGroup>(true).Length, $"Slot {chunk.SlotId}");

                colliders += chunk.GetComponentsInChildren<Collider>(true).Length;
                materials.Add(chunk.GroundRenderer.sharedMaterial);
                materials.Add(chunk.SolidDecorRenderer.sharedMaterial);
                materials.Add(chunk.FoliageRenderer.sharedMaterial);

                Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, chunk.FoliageMesh.indexFormat);
                Assert.IsFalse(chunk.FoliageRenderer.HasPropertyBlock(),
                    $"Slot {chunk.SlotId}: MaterialPropertyBlock làm mất SRP Batcher.");
            }

            Assert.AreEqual(0, colliders, "Scenery không được có collider.");
            Assert.AreEqual(3, materials.Count, "Đúng ba material dùng chung.");
            Assert.IsNotNull(Manager.Surface.Collider, "Mặt gameplay dùng chung phải còn đó.");
        }
    }
}
#endif
