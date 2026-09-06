#if UNITY_EDITOR
// Fixture nay doc palette qua AssetDatabase nen chi ton tai trong Editor.
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// PlayMode M3A.1: benchmark trải theo frame, và việc ĐO không được làm đổi thứ được đo.
    public class WorldStreamingProfilingPlayTests
    {
        private const float ChunkSize = 32f;

        private WorldStreamingConfig _config;
        private GameObject _probe;
        private WorldStreamingRig.Rig _rig;

        private WorldStreamManager Manager => _rig.Manager;
        private ChunkPool Pool => _rig.Pool;

        [SetUp]
        public void SetUp()
        {
            var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<DecorationPalette>(
                "Assets/_Project/Data/World/Decoration/DecorationPalette.asset");
            Assert.IsNotNull(palette, "Chưa bake palette.");

            _config = WorldStreamingConfig.CreateRuntime();
            _config.SetDecorationPalette(palette);

            _probe = new GameObject("ProfilingTestProbe");
            _probe.transform.position = new Vector3(4f, 1f, 4f);

            _rig = WorldStreamingRig.Build(_config, _probe.transform, null, null, null, initializeOnStart: false);
            Manager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            WorldStreamingProfiler.Release();

            if (Pool != null) Pool.BeginTeardown();
            if (_rig.Root != null) Object.DestroyImmediate(_rig.Root);
            if (_probe != null) Object.DestroyImmediate(_probe);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Di chuyển RỒI chờ dựng xong.
        ///
        /// Từ M3B.2, `TeleportTargetTo` chỉ xếp hàng phần trang trí. Không vét hàng đợi thì các chặng
        /// DecorationSample/Build/Apply sẽ không có mẫu nào, và vân tay ring sẽ đọc ra một thế giới
        /// mới dựng dở.
        /// </summary>
        private void MoveTo(ChunkCoord coord)
        {
            Vector3 centre = coord.ToWorldCenter(ChunkSize);
            Manager.TeleportTargetTo(new Vector3(centre.x, 1f, centre.z));
            Manager.DrainDecoration();
        }

        private string RingChecksum()
        {
            var sb = new StringBuilder();
            var ordered = new List<ChunkCoord>();
            Manager.CopyActiveCoords(ordered);
            ordered.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X));

            foreach (ChunkCoord coord in ordered)
            {
                ChunkInstance chunk = Manager.ActiveLeases[coord];
                sb.Append(coord).Append(':').Append(chunk.PlacementCount).Append(':')
                  .Append(chunk.SolidVertexCount).Append(':').Append(chunk.FoliageVertexCount).Append('|');
            }

            return sb.ToString();
        }

        // --- Đường chạy nóng ----------------------------------------------------------------------

        [Test]
        public void RepeatedTraversal_KeepsEveryRootNameStable()
        {
            var namesBefore = new Dictionary<int, string>();
            foreach (ChunkInstance chunk in Pool.All) namesBefore[chunk.SlotId] = chunk.gameObject.name;

            for (int i = 0; i < 40; i++) MoveTo(new ChunkCoord(i - 20, (i % 7) - 3));
            MoveTo(new ChunkCoord(74, 47));

            foreach (ChunkInstance chunk in Pool.All)
            {
                Assert.AreEqual(namesBefore[chunk.SlotId], chunk.gameObject.name,
                    $"Slot {chunk.SlotId}: tên GameObject bị đổi trong lúc đi — đó là cấp phát chuỗi mỗi lần gán.");
                StringAssert.DoesNotContain("(", chunk.gameObject.name, "Tên không được mang trạng thái.");
            }
        }

        [Test]
        public void DisabledCapture_CreatesNoTimingBuffersDuringRealTraversal()
        {
            WorldStreamingProfiler.Release();
            WorldStreamingProfiler.ResetClockReadCounter();

            for (int i = 0; i < 30; i++) MoveTo(new ChunkCoord(i, -i));

            Assert.IsFalse(WorldStreamingProfiler.HasCaptureBuffers,
                "Đi thật mà bộ đệm profiling vẫn được cấp phát dù capture đang tắt.");
            Assert.AreEqual(0, WorldStreamingProfiler.ClockReads,
                "Đi thật mà điểm đo vẫn đọc đồng hồ dù capture đang tắt.");
        }

        [Test]
        public void ActiveCapture_ProducesSamplesForEveryStage()
        {
            WorldStreamingProfiler.Begin(4096);
            try
            {
                for (int i = 0; i < 6; i++) MoveTo(new ChunkCoord(i * 40, -i * 40));

                foreach (ProfilerStage stage in new[]
                         {
                             ProfilerStage.GroundBiome, ProfilerStage.DecorationSample, ProfilerStage.DecorationBuild,
                             ProfilerStage.SolidApply, ProfilerStage.FoliageApply, ProfilerStage.ChunkAssign,
                         })
                    Assert.Greater(WorldStreamingProfiler.Stage(stage).Count, 0, $"Chặng {stage} không có mẫu.");
            }
            finally
            {
                WorldStreamingProfiler.Stop();
            }
        }

        // --- Bộ chạy trải theo frame ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator FrameSpreadSteadyState_SpansAtLeast240RealFrames()
        {
            var benchmark = new FrameSpreadBenchmark();
            benchmark.Start(Manager, BenchmarkScenario.SteadyState, "test");

            int guard = 0;
            while (benchmark.IsRunning && guard++ < 2000)
            {
                yield return null;
                benchmark.Tick();
            }

            Assert.IsFalse(benchmark.IsRunning, "Kịch bản không kết thúc.");
            Assert.GreaterOrEqual(benchmark.Result.DistinctFrames, 240,
                "Steady state phải trải qua ít nhất 240 khung hình THẬT, không phải 240 lời gọi hàm.");
            Assert.IsFalse(WorldStreamingProfiler.IsCapturing, "Xong phải tắt capture.");
        }

        [UnityTest]
        public IEnumerator FrameSpreadTraversal_MovesAtMostOneStepPerFrame()
        {
            var benchmark = new FrameSpreadBenchmark();
            benchmark.Start(Manager, BenchmarkScenario.TeleportStress, "test");

            int frames = 0;
            int guard = 0;
            var seen = new List<ChunkCoord>();

            while (benchmark.IsRunning && guard++ < 2000)
            {
                yield return null;
                ChunkCoord before = Manager.CurrentChunk;
                benchmark.Tick();
                frames++;

                if (Manager.CurrentChunk != before) seen.Add(Manager.CurrentChunk);
            }

            Assert.IsFalse(benchmark.IsRunning);
            Assert.GreaterOrEqual(frames, seen.Count,
                "Không được thực hiện nhiều hơn một bước di chuyển trong cùng một khung hình.");
            Assert.GreaterOrEqual(benchmark.Result.DistinctFrames, seen.Count);
        }

        [UnityTest]
        public IEnumerator FrameSpreadCheckpoints_DescribeIdenticalWorldState()
        {
            var benchmark = new FrameSpreadBenchmark();
            benchmark.Start(Manager, BenchmarkScenario.TeleportStress, "test");

            int guard = 0;
            while (benchmark.IsRunning && guard++ < 4000)
            {
                yield return null;
                benchmark.Tick();
            }

            WorldStreamingBenchmarkResult result = benchmark.Result;
            Assert.IsTrue(result.CheckpointWarmup.Valid && result.CheckpointMid.Valid && result.CheckpointEnd.Valid);

            Assert.AreEqual(WorldStreamingBenchmarkRoute.Checkpoint, result.CheckpointWarmup.Centre);
            Assert.AreEqual(WorldStreamingBenchmarkRoute.Checkpoint, result.CheckpointMid.Centre);
            Assert.AreEqual(WorldStreamingBenchmarkRoute.Checkpoint, result.CheckpointEnd.Centre);

            Assert.IsTrue(result.CheckpointsAreEquivalent,
                "Ba mốc bộ nhớ phải mô tả cùng một trạng thái thế giới, nếu không 'plateau' chỉ là so hai vùng biome.");
            Assert.AreEqual(result.CheckpointWarmup.Placements, result.CheckpointEnd.Placements);
            Assert.AreEqual(result.CheckpointWarmup.TotalVertices, result.CheckpointEnd.TotalVertices);
        }

        [UnityTest]
        public IEnumerator FrameSpreadTimings_CoverTheEntireRouteWithoutDroppedSamples()
        {
            var benchmark = new FrameSpreadBenchmark();
            benchmark.Start(Manager, BenchmarkScenario.TeleportStress, "test");

            int guard = 0;
            while (benchmark.IsRunning && guard++ < 4000)
            {
                yield return null;
                benchmark.Tick();
            }

            Assert.IsFalse(benchmark.IsRunning, "Kịch bản không kết thúc.");

            WorldStreamingBenchmarkResult result = benchmark.Result;
            TimingSummary refresh = result.Stage(ProfilerStage.RingRefresh);
            TimingSummary assign = result.Stage(ProfilerStage.ChunkAssign);

            Assert.AreEqual(result.Transitions, refresh.Count,
                "Timing ring refresh phải bao phủ toàn route, không chỉ nửa sau checkpoint.");
            Assert.AreEqual(result.ChunkAssignments, assign.Count,
                "Timing chunk assignment phải bao phủ toàn route.");

            foreach (ProfilerStage stage in new[]
                     {
                         ProfilerStage.GroundBiome, ProfilerStage.DecorationSample,
                         ProfilerStage.DecorationBuild, ProfilerStage.SolidApply,
                         ProfilerStage.FoliageApply, ProfilerStage.ChunkAssign,
                         ProfilerStage.RingRefresh,
                     })
                Assert.AreEqual(0, result.Stage(stage).Dropped, $"Stage {stage} làm rơi mẫu.");
        }

        [UnityTest]
        public IEnumerator CancellingABenchmark_LeavesProfilerDisabled()
        {
            var benchmark = new FrameSpreadBenchmark();
            benchmark.Start(Manager, BenchmarkScenario.Soak, "test");

            for (int i = 0; i < 20; i++)
            {
                yield return null;
                benchmark.Tick();
            }

            benchmark.Cancel();

            Assert.IsFalse(benchmark.IsRunning);
            Assert.IsFalse(WorldStreamingProfiler.IsCapturing, "Huỷ giữa chừng phải để lại profiler ở trạng thái tắt.");
        }

        [Test]
        public void BenchmarkOnUninitialisedManager_ThrowsAndLeavesProfilerDisabled()
        {
            var benchmark = new FrameSpreadBenchmark();

            Assert.Throws<System.InvalidOperationException>(() => benchmark.Start(null, BenchmarkScenario.SteadyState, "test"));
            Assert.IsFalse(WorldStreamingProfiler.IsCapturing, "Ngoại lệ phải để lại profiler ở trạng thái tắt.");
        }

        // --- Không làm đổi thứ được đo -------------------------------------------------------------

        [UnityTest]
        public IEnumerator Benchmarking_DoesNotChangeTheGeneratedWorld()
        {
            MoveTo(new ChunkCoord(74, 47));
            string before = RingChecksum();

            var benchmark = new FrameSpreadBenchmark();
            benchmark.Start(Manager, BenchmarkScenario.TeleportStress, "test");

            int guard = 0;
            while (benchmark.IsRunning && guard++ < 4000)
            {
                yield return null;
                benchmark.Tick();
            }

            MoveTo(new ChunkCoord(74, 47));
            Assert.AreEqual(before, RingChecksum(), "Chạy benchmark đã làm đổi thế giới sinh ra.");
        }

        [UnityTest]
        public IEnumerator Benchmarking_CreatesNoRootsMeshesMaterialsOrColliders()
        {
            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();
            foreach (ChunkInstance chunk in Pool.All)
            {
                meshes.Add(chunk.GroundMesh);
                meshes.Add(chunk.SolidMesh);
                meshes.Add(chunk.FoliageMesh);
                materials.Add(chunk.GroundRenderer.sharedMaterial);
                materials.Add(chunk.SolidDecorRenderer.sharedMaterial);
                materials.Add(chunk.FoliageRenderer.sharedMaterial);
            }

            int collidersBefore = _rig.Root.GetComponentsInChildren<Collider>(true).Length;

            var benchmark = new FrameSpreadBenchmark();
            benchmark.Start(Manager, BenchmarkScenario.TeleportStress, "test");

            int guard = 0;
            while (benchmark.IsRunning && guard++ < 4000)
            {
                yield return null;
                benchmark.Tick();
            }

            var meshesNow = new HashSet<Mesh>();
            var materialsNow = new HashSet<Material>();
            foreach (ChunkInstance chunk in Pool.All)
            {
                meshesNow.Add(chunk.GroundMesh);
                meshesNow.Add(chunk.SolidMesh);
                meshesNow.Add(chunk.FoliageMesh);
                materialsNow.Add(chunk.GroundRenderer.sharedMaterial);
                materialsNow.Add(chunk.SolidDecorRenderer.sharedMaterial);
                materialsNow.Add(chunk.FoliageRenderer.sharedMaterial);
            }

            Assert.IsTrue(meshesNow.SetEquals(meshes), "Benchmark đã tạo/thay Mesh.");
            Assert.IsTrue(materialsNow.SetEquals(materials), "Benchmark đã tạo/thay material.");
            Assert.AreEqual(collidersBefore, _rig.Root.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0, Pool.RootsCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.RootsDestroyedDuringTraversal);
            Assert.AreEqual(0, Pool.GroundMeshesCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.DecorationMeshesCreatedSinceWarmup);
        }

        [Test]
        public void SynchronousMicrobenchmark_LabelsItselfAsSuch()
        {
            WorldStreamingBenchmarkResult result =
                WorldStreamingMicrobenchmark.Run(Manager, BenchmarkScenario.TeleportStress, "UNITY EDITOR (not a device measurement)");

            Assert.AreEqual(BenchmarkMode.SynchronousMicrobenchmark, result.Mode);

            string report = result.ToReport();
            StringAssert.Contains("SYNCHRONOUS MICROBENCHMARK", report,
                "Kết quả đồng bộ phải tự gắn nhãn để không bị đọc nhầm thành số liệu frame.");
            StringAssert.Contains("not a device measurement", report);
            StringAssert.Contains("UNVERIFIED", report,
                "Microbenchmark dong bo khong co bo dem cap phat theo frame — phai tu noi la chua kiem chung.");
            Assert.IsFalse(WorldStreamingProfiler.IsCapturing);
        }
    }
}
#endif
