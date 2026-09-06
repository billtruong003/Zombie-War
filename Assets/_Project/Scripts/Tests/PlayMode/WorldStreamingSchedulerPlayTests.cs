#if UNITY_EDITOR
// Fixture nay doc palette qua AssetDatabase nen chi ton tai trong Editor.
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// PlayMode M3B.2: lập lịch trang trí trên pool thật.
    ///
    /// Câu hỏi trung tâm: sau khi tách "gán chunk" khỏi "sinh trang trí", thế giới cuối cùng có còn
    /// đúng y như hồi làm đồng bộ không, và trong lúc chưa dựng xong thì người chơi có bao giờ nhìn
    /// thấy thứ sai không.
    public class WorldStreamingSchedulerPlayTests
    {
        private const float ChunkSize = 32f;
        private const int ExpectedActive = 25;

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

            // Fixture này đo bộ LẬP LỊCH: thứ tự, ngân sách, vé thế hệ, trạng thái đã lắng. Tắt mật độ
            // theo khoảng cách để mỗi lần refresh chỉ sinh ra đúng những việc của chunk mới vào ring,
            // nên các phép đếm ở đây đo đúng một nguyên nhân. Phần lập lịch KÈM đổi bậc được kiểm riêng
            // trong `WorldStreamingDistanceDensityPlayTests`.
            _config.SetDistanceDensity(false, 1, 1f, 0.4f);

            _probe = new GameObject("SchedulerTestProbe");
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

        /// <summary>Đi tới một toạ độ RỒI dựng xong — dùng khi bài test cần thế giới hoàn chỉnh.</summary>
        private void MoveToAndSettle(ChunkCoord coord)
        {
            MoveTo(coord);
            Manager.DrainDecoration();
            Assert.IsTrue(Manager.IsGenerationSettled, $"Không dựng xong tại {coord}.");
        }

        /// <summary>Vân tay hình học thật trong hai mesh gộp của một chunk.</summary>
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

        // --- Mặt đất làm ngay, trang trí hoãn lại -------------------------------------------------

        [Test]
        public void Teleport_GivesTwentyFiveCorrectGroundChunksImmediately()
        {
            MoveTo(new ChunkCoord(500, -400));

            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount);
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                ChunkInstance chunk = lease.Value;
                Assert.AreEqual(lease.Key, chunk.Coord);
                Assert.IsTrue(chunk.GroundRenderer.enabled,
                    $"{lease.Key}: mặt đất phải hiện ngay, không được chờ hàng đợi.");
                Assert.AreEqual(lease.Key.ToWorldMin(ChunkSize), chunk.transform.localPosition);
            }
        }

        [Test]
        public void PendingChunks_ShowNoStaleDecoration()
        {
            // Dựng đủ một thế giới rậm rạp trước, để chắc chắn các mesh trang trí đang có nội dung.
            MoveToAndSettle(new ChunkCoord(74, 47));
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                Assert.Greater(lease.Value.FoliageVertexCount, 0, "Thế giới mốc phải có cây.");

            // Teleport đi xa: mọi slot bị gán lại. Trước khi hàng đợi chạy, không slot nào được phép
            // còn hiển thị hình học của toạ độ cũ.
            MoveTo(new ChunkCoord(-900, 610));

            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                ChunkInstance chunk = lease.Value;
                if (!chunk.DecorationPending) continue;

                Assert.IsFalse(chunk.SolidDecorRenderer.enabled, $"{lease.Key}: còn hiện SolidDecor cũ.");
                Assert.IsFalse(chunk.FoliageRenderer.enabled, $"{lease.Key}: còn hiện Foliage cũ.");
                Assert.AreEqual(0, chunk.SolidMesh.vertexCount, $"{lease.Key}: mesh Solid chưa được xoá.");
                Assert.AreEqual(0, chunk.FoliageMesh.vertexCount, $"{lease.Key}: mesh Foliage chưa được xoá.");
                Assert.AreEqual(0, chunk.PlacementCount);
            }
        }

        [Test]
        public void Budget_OfOne_CompletesExactlyOneJobPerStep()
        {
            MoveTo(new ChunkCoord(74, 47));
            Assert.AreEqual(ExpectedActive, Manager.PendingDecorationCount);

            for (int step = 0; step < ExpectedActive; step++)
            {
                int before = Manager.CompletedDecorationJobs;
                Manager.ProcessDecorationJobs(1);
                Assert.AreEqual(before + 1, Manager.CompletedDecorationJobs,
                    $"Nhịp {step}: ngân sách 1 mà làm nhiều hơn một việc.");
            }

            Assert.IsTrue(Manager.IsGenerationSettled);
            Assert.AreEqual(0, Manager.PendingDecorationCount);
        }

        [Test]
        public void NearestChunkCompletesBeforeFartherChunks()
        {
            var centre = new ChunkCoord(74, 47);
            MoveTo(centre);

            ChunkInstance centreChunk = Manager.ActiveLeases[centre];
            Manager.ProcessDecorationJobs(1);

            Assert.IsFalse(centreChunk.DecorationPending, "Chunk người chơi đang đứng phải xong đầu tiên.");

            // Vòng 1 (8 chunk) phải xong hết trước khi bất kỳ chunk vòng 2 nào được đụng tới.
            for (int i = 0; i < 8; i++) Manager.ProcessDecorationJobs(1);

            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                int ring = Mathf.Max(Mathf.Abs(lease.Key.X - centre.X), Mathf.Abs(lease.Key.Z - centre.Z));
                if (ring <= 1) Assert.IsFalse(lease.Value.DecorationPending, $"{lease.Key} (vòng {ring}) phải xong rồi.");
                else Assert.IsTrue(lease.Value.DecorationPending, $"{lease.Key} (vòng {ring}) không được xong sớm.");
            }
        }

        [Test]
        public void UninterruptedTeleport_SettlesWithinRingSizeSteps()
        {
            MoveTo(new ChunkCoord(-310, 208));

            int steps = 0;
            while (!Manager.IsGenerationSettled && steps < 100)
            {
                Manager.ProcessDecorationJobs(1);
                steps++;
            }

            Assert.IsTrue(Manager.IsGenerationSettled);
            Assert.LessOrEqual(steps, ExpectedActive,
                $"Cần {steps} nhịp cho một ring {ExpectedActive} chunk ở ngân sách 1.");
        }

        // --- Di chuyển nhanh và việc hết hạn ------------------------------------------------------

        [Test]
        public void RapidTeleports_CancelStaleWorkWithoutApplyingIt()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));

            var destinations = new[]
            {
                new ChunkCoord(1200, -900), new ChunkCoord(-1500, 700), new ChunkCoord(50, 50),
                new ChunkCoord(-40, -60), new ChunkCoord(9000, 9000), new ChunkCoord(0, 0),
            };

            // Mỗi lần chỉ chạy một nhịp rồi teleport tiếp: hàng đợi không bao giờ kịp cạn.
            foreach (ChunkCoord destination in destinations)
            {
                MoveTo(destination);
                Manager.ProcessDecorationJobs(1);
            }

            Assert.Greater(Manager.CancelledStaleDecorationJobs, 0,
                "Chạy nhanh hơn tốc độ sinh mà không huỷ việc nào là dấu hiệu bộ đếm sai.");

            var final = new ChunkCoord(0, 0);
            MoveToAndSettle(final);

            // Sau khi lắng, thế giới phải đúng bằng thế giới dựng đồng bộ tại cùng toạ độ.
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                int ring = Mathf.Max(Mathf.Abs(lease.Key.X - final.X), Mathf.Abs(lease.Key.Z - final.Z));
                Assert.LessOrEqual(ring, 2, $"{lease.Key} nằm ngoài ring cuối cùng.");
                Assert.IsFalse(lease.Value.DecorationPending);
            }

            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount);
            Assert.AreEqual(0, Pool.RootsCreatedSinceWarmup);
        }

        [Test]
        public void CancelledJobs_AreNeverCountedAsGeneratedContent()
        {
            MoveToAndSettle(ChunkCoord.Zero);
            Manager.DecorationScheduler.ResetCounters();

            MoveTo(new ChunkCoord(3000, -3000));
            MoveTo(new ChunkCoord(-3000, 3000));
            Manager.DrainDecoration();

            // 25 chunk bị bỏ giữa chừng + 25 chunk dựng xong. Con số hoàn thành không được nuốt phần huỷ.
            Assert.AreEqual(ExpectedActive, Manager.CompletedDecorationJobs);
            Assert.AreEqual(ExpectedActive, Manager.CancelledStaleDecorationJobs);
        }

        [Test]
        public void AdjacentMovement_KeepsRetainedDecorationWithoutRebuilding()
        {
            var start = new ChunkCoord(74, 47);
            MoveToAndSettle(start);

            var retained = new ChunkCoord(74, 47);
            ChunkInstance retainedChunk = Manager.ActiveLeases[retained];
            string before = MeshChecksum(retainedChunk);
            int assignmentsBefore = retainedChunk.AssignmentCount;

            MoveToAndSettle(new ChunkCoord(75, 47));

            Assert.AreSame(retainedChunk, Manager.ActiveLeases[retained],
                "Chunk còn trong ring phải giữ nguyên slot.");
            Assert.AreEqual(assignmentsBefore, retainedChunk.AssignmentCount,
                "Chunk giữ lại không được gán lại — đó là dựng thừa.");
            Assert.AreEqual(before, MeshChecksum(retainedChunk));
            Assert.IsTrue(retainedChunk.FoliageRenderer.enabled || retainedChunk.FoliageVertexCount == 0);
        }

        [Test]
        public void AdjacentMovement_OnlyEnqueuesTheNewColumn()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));
            MoveTo(new ChunkCoord(75, 47));

            // Bước một ô sang ngang chỉ mang vào một cột 5 chunk.
            Assert.AreEqual(5, Manager.PendingDecorationCount);
            Assert.AreEqual(5, Manager.LastIncomingCount);
        }

        // --- Thế giới cuối cùng vẫn tất định -------------------------------------------------------

        [Test]
        public void ReturnToSameCoordinate_ProducesByteIdenticalMeshes()
        {
            var home = new ChunkCoord(74, 47);
            MoveToAndSettle(home);
            string before = RingChecksum();

            MoveToAndSettle(new ChunkCoord(-2100, 1450));
            MoveToAndSettle(home);

            Assert.AreEqual(before, RingChecksum(),
                "Quay lại cùng toạ độ mà hình học khác đi — lập lịch đã ảnh hưởng nội dung thế giới.");
        }

        [Test]
        public void FinalWorld_IsIndependentOfSchedulingOrder()
        {
            var home = new ChunkCoord(74, 47);

            // Lần 1: ngân sách 1, dựng rải ra nhiều nhịp.
            MoveToAndSettle(home);
            string spread = RingChecksum();
            int placementsSpread = 0, foliageSpread = 0, solidSpread = 0;
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                placementsSpread += lease.Value.PlacementCount;
                foliageSpread += lease.Value.FoliageVertexCount;
                solidSpread += lease.Value.SolidVertexCount;
            }

            // Lần 2: cùng toạ độ nhưng dựng trọn trong một nhịp — thứ tự hoàn toàn khác.
            MoveToAndSettle(new ChunkCoord(-800, -650));
            MoveTo(home);
            Manager.ProcessDecorationJobs(64);
            Assert.IsTrue(Manager.IsGenerationSettled);

            Assert.AreEqual(spread, RingChecksum(), "Thứ tự lập lịch đã làm đổi nội dung thế giới.");

            int placementsBurst = 0, foliageBurst = 0, solidBurst = 0;
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                placementsBurst += lease.Value.PlacementCount;
                foliageBurst += lease.Value.FoliageVertexCount;
                solidBurst += lease.Value.SolidVertexCount;
            }

            Assert.AreEqual(placementsSpread, placementsBurst);
            Assert.AreEqual(foliageSpread, foliageBurst);
            Assert.AreEqual(solidSpread, solidBurst);
        }

        [Test]
        public void SettledWorld_MatchesTheM3B1Baseline()
        {
            MoveToAndSettle(new ChunkCoord(74, 47));

            int placements = 0, foliage = 0, solid = 0;
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                placements += lease.Value.PlacementCount;
                foliage += lease.Value.FoliageVertexCount;
                solid += lease.Value.SolidVertexCount;
            }

            // Lập lịch chỉ đổi THỜI ĐIỂM, không đổi nội dung — đó vẫn là điều đang được canh.
            // Hai con số đầu hạ xuống ở M4.6B.1 vì bản vón cụm cỏ đã duyệt làm giảm số bụi cỏ;
            // `solid` KHÔNG đổi, đúng như phải vậy, vì cỏ chỉ đóng góp vào Foliage.
                        // M4.6C.4 gỡ `fern_d` khỏi bảng (hoa thị dẹt bị từ chối ở góc nhìn từ trên xuống):
            // tổng rơi đúng 97 placement và 3.492 đỉnh — bằng CHÍNH phần fern_d đóng góp — còn
            // `solid` không nhúc nhích, đúng như phải vậy vì fern_d chỉ thuộc Foliage.
            //
            // M4.6CD thêm bụi rậm (tự sinh, Foliage) và sáu loại cảnh vật rắn (Solid):
            //   placement 2144 → 2863 (+719)
            //   foliage   262014 → 347838 (+85824 = cỏ dày lên 1719×16 + bụi rậm 360×162)
            //   solid      66664 → 153538 (+86874, lần đầu Solid có thứ khác ngoài thân cây)
            Assert.AreEqual(1705, placements);
            Assert.AreEqual(569429, foliage);
            Assert.AreEqual(242237, solid);
        }

        // --- Hợp đồng trạng thái đã lắng -----------------------------------------------------------

        [Test]
        public void NotSettled_WhileAValidJobIsStillPending()
        {
            MoveTo(new ChunkCoord(120, 88));

            Assert.IsFalse(Manager.IsGenerationSettled, "Còn việc hợp lệ mà đã báo lắng.");
            Assert.Greater(Manager.PendingDecorationCount, 0);

            Manager.DrainDecoration();
            Assert.IsTrue(Manager.IsGenerationSettled);
        }

        [Test]
        public void EmptyQueue_AfterCancellation_IsNotReportedAsSettledUntilRebuilt()
        {
            MoveToAndSettle(ChunkCoord.Zero);
            MoveTo(new ChunkCoord(4000, 4000));

            // Ép huỷ toàn bộ hàng đợi mà KHÔNG dựng gì. Hàng đợi rỗng, nhưng thế giới còn thiếu cây —
            // và trạng thái "đã lắng" phải phản ánh điều đó chứ không phản ánh hàng đợi.
            Manager.DecorationScheduler.Clear();

            Assert.AreEqual(0, Manager.PendingDecorationCount);
            Assert.IsFalse(Manager.IsGenerationSettled,
                "Hàng đợi rỗng vì bị huỷ không phải là thế giới đã dựng xong.");
        }

        // --- Không hồi quy kiến trúc ---------------------------------------------------------------

        [Test]
        public void LongTraversal_WithQueueActivity_KeepsPoolAndRenderersFixed()
        {
            MoveToAndSettle(ChunkCoord.Zero);

            var cursor = new ChunkCoord(0, 0);
            int transitions = 0;
            for (int lap = 0; lap < 4; lap++)
            {
                for (int i = 0; i < 30; i++)
                {
                    cursor = new ChunkCoord(cursor.X + (lap % 2 == 0 ? 1 : -1), cursor.Z + (lap < 2 ? 1 : -1));
                    MoveTo(cursor);
                    Manager.ProcessDecorationJobs(1);
                    transitions++;
                }
            }

            Assert.Greater(transitions, 100);
            Assert.Greater(Manager.CompletedDecorationJobs, 0);

            Manager.DrainDecoration();
            Assert.IsTrue(Manager.IsGenerationSettled);

            Assert.AreEqual(0, Pool.RootsCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.RootsDestroyedDuringTraversal);
            Assert.AreEqual(0, Pool.GroundMeshesCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.DecorationMeshesCreatedSinceWarmup);
            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount);

            var coords = new HashSet<ChunkCoord>();
            var slots = new HashSet<int>();
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                Assert.IsTrue(coords.Add(lease.Key), $"Toạ độ trùng: {lease.Key}");
                Assert.IsTrue(slots.Add(lease.Value.SlotId), $"Slot trùng: {lease.Value.SlotId}");
            }

            int colliders = 0;
            var materials = new HashSet<Material>();
            foreach (ChunkInstance chunk in Pool.All)
            {
                colliders += chunk.GetComponentsInChildren<Collider>(true).Length;
                Assert.AreEqual(3, chunk.transform.childCount);
                materials.Add(chunk.GroundRenderer.sharedMaterial);
                materials.Add(chunk.SolidDecorRenderer.sharedMaterial);
                materials.Add(chunk.FoliageRenderer.sharedMaterial);

                Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, chunk.SolidMesh.indexFormat);
                Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, chunk.FoliageMesh.indexFormat);
                Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, chunk.GroundMesh.indexFormat);
            }

            Assert.AreEqual(0, colliders, "Scenery không được có collider.");
            Assert.AreEqual(3, materials.Count, "Đúng ba material dùng chung.");
            Assert.IsNotNull(Manager.Surface.Collider, "Mặt gameplay dùng chung phải còn đó.");
        }

        [Test]
        public void QueueDepth_NeverExceedsTheActiveRing()
        {
            MoveToAndSettle(ChunkCoord.Zero);
            Manager.DecorationScheduler.ResetCounters();

            for (int i = 0; i < 30; i++)
            {
                MoveTo(new ChunkCoord(i * 137, -i * 211));
                Manager.ProcessDecorationJobs(1);
                Assert.LessOrEqual(Manager.PendingDecorationCount, ExpectedActive);
            }

            Assert.LessOrEqual(Manager.MaxObservedDecorationQueueDepth, ExpectedActive);
        }

        [Test]
        public void ProfilerBuffers_AreReleasedAfterSchedulingWork()
        {
            WorldStreamingProfiler.Release();
            Assert.IsFalse(WorldStreamingProfiler.HasCaptureBuffers);

            MoveTo(new ChunkCoord(74, 47));
            Manager.DrainDecoration();

            // Đường chạy bình thường không được tự dựng bộ đệm đo.
            Assert.IsFalse(WorldStreamingProfiler.HasCaptureBuffers,
                "Sinh trang trí khi profiler tắt vẫn cấp phát bộ đệm đo.");

            WorldStreamingProfiler.Begin(256);
            MoveTo(new ChunkCoord(-74, -47));
            Manager.DrainDecoration();
            Assert.Greater(WorldStreamingProfiler.Summarize(ProfilerStage.DecorationJob).Count, 0,
                "Việc của bộ lập lịch phải có mẫu riêng khi bật đo.");

            WorldStreamingProfiler.Release();
            Assert.IsFalse(WorldStreamingProfiler.HasCaptureBuffers);
        }

        [Test]
        public void DecorationJobSamples_MatchCompletedJobs()
        {
            MoveToAndSettle(ChunkCoord.Zero);
            Manager.DecorationScheduler.ResetCounters();

            WorldStreamingProfiler.Begin(4096);
            try
            {
                MoveTo(new ChunkCoord(600, -450));
                Manager.DrainDecoration();
                MoveTo(new ChunkCoord(601, -450));
                Manager.DrainDecoration();

                TimingSummary jobs = WorldStreamingProfiler.Summarize(ProfilerStage.DecorationJob);
                Assert.AreEqual(Manager.CompletedDecorationJobs, jobs.Count,
                    "Số mẫu đo phải bằng đúng số việc hoàn thành.");
                Assert.AreEqual(0, jobs.Dropped);
            }
            finally
            {
                WorldStreamingProfiler.Release();
            }
        }
    }
}
#endif
