using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode M3A.1: chính bộ đo phải đúng trước khi tin bất kỳ con số nào nó in ra.
    ///
    /// Mỗi bài ở nhóm "integrity" dưới đây đều FAIL trên bản M3A: bản đó cấp phát bộ đệm trong static
    /// constructor và đọc đồng hồ vô điều kiện, trong khi báo cáo lại nói nó "chỉ tốn một lần đọc bool".
    public class WorldStreamingProfilingTests
    {
        private const string ProfilerSourcePath =
            "Assets/_Project/Scripts/Runtime/World/Streaming/Profiling/WorldStreamingProfiler.cs";
        private const string ResultSourcePath =
            "Assets/_Project/Scripts/Runtime/World/Streaming/Profiling/WorldStreamingBenchmarkResult.cs";
        private const string BenchmarkSourcePath =
            "Assets/_Project/Scripts/Runtime/World/Streaming/Profiling/WorldStreamingBenchmark.cs";
        private const string ChunkInstanceSourcePath =
            "Assets/_Project/Scripts/Runtime/World/Streaming/ChunkInstance.cs";

        [SetUp]
        public void SetUp() => WorldStreamingProfiler.Release();

        [TearDown]
        public void TearDown() => WorldStreamingProfiler.Release();

        // --- Tính toàn vẹn của bộ đo -------------------------------------------------------------

        [Test]
        public void DisabledProfiler_HoldsNoTimingBuffers()
        {
            Assert.IsFalse(WorldStreamingProfiler.IsCapturing);
            Assert.IsFalse(WorldStreamingProfiler.HasCaptureBuffers,
                "Bộ đệm mẫu không được tồn tại khi chưa ai bật capture.");
            Assert.AreEqual(0, WorldStreamingProfiler.Capacity);
        }

        [Test]
        public void InactiveSampling_NeverAllocatesBuffers_AndRecordsNothing()
        {
            for (int i = 0; i < 500; i++)
            {
                long token = WorldStreamingProfiler.BeginSample();
                Assert.AreEqual(WorldStreamingProfiler.InvalidToken, token, "Profiler tắt phải trả token rỗng.");
                WorldStreamingProfiler.EndSample(ProfilerStage.ChunkAssign, token);
            }

            Assert.IsFalse(WorldStreamingProfiler.HasCaptureBuffers,
                "Gọi điểm đo khi tắt vẫn làm bộ đệm được cấp phát — đúng lỗi của bản M3A.");
            Assert.AreEqual(0, WorldStreamingProfiler.TotalSamples());
        }

        [Test]
        public void InactiveSampling_DoesNotReadTheClock()
        {
            WorldStreamingProfiler.ResetClockReadCounter();

            for (int i = 0; i < 500; i++)
            {
                long token = WorldStreamingProfiler.BeginSample();
                WorldStreamingProfiler.EndSample(ProfilerStage.DecorationBuild, token);
            }

            Assert.AreEqual(0, WorldStreamingProfiler.ClockReads,
                "Profiler tắt mà vẫn đọc Stopwatch — câu 'chỉ một lần đọc bool' trong báo cáo M3A là sai.");
        }

        [Test]
        public void BeginLazilyAllocatesBuffersOfTheRequestedSize()
        {
            Assert.IsFalse(WorldStreamingProfiler.HasCaptureBuffers);

            WorldStreamingProfiler.Begin(256);

            Assert.IsTrue(WorldStreamingProfiler.HasCaptureBuffers);
            Assert.AreEqual(256, WorldStreamingProfiler.Capacity);
            Assert.IsTrue(WorldStreamingProfiler.IsCapturing);
        }

        [Test]
        public void ActiveSamplingRecordsAndStopHalts()
        {
            WorldStreamingProfiler.Begin(64);
            WorldStreamingProfiler.ResetClockReadCounter();

            long token = WorldStreamingProfiler.BeginSample();
            Assert.AreNotEqual(WorldStreamingProfiler.InvalidToken, token);
            WorldStreamingProfiler.EndSample(ProfilerStage.ChunkAssign, token);

            Assert.AreEqual(1, WorldStreamingProfiler.Stage(ProfilerStage.ChunkAssign).Count);
            Assert.AreEqual(2, WorldStreamingProfiler.ClockReads, "Một phép đo phải đúng hai lần đọc đồng hồ.");

            WorldStreamingProfiler.Stop();
            long stopped = WorldStreamingProfiler.BeginSample();
            WorldStreamingProfiler.EndSample(ProfilerStage.ChunkAssign, stopped);

            Assert.AreEqual(1, WorldStreamingProfiler.Stage(ProfilerStage.ChunkAssign).Count,
                "Stop phải dừng hẳn việc ghi.");
        }

        [Test]
        public void ReleaseFreesRetainedBuffers()
        {
            WorldStreamingProfiler.Begin(128);
            Assert.IsTrue(WorldStreamingProfiler.HasCaptureBuffers);

            WorldStreamingProfiler.Release();

            Assert.IsFalse(WorldStreamingProfiler.HasCaptureBuffers, "Release phải nhả bộ đệm.");
            Assert.IsFalse(WorldStreamingProfiler.IsCapturing);
            Assert.AreEqual(0, WorldStreamingProfiler.Capacity);
        }

        [Test]
        public void Resume_PreservesSamplesCollectedBeforePause()
        {
            WorldStreamingProfiler.Begin(64);
            long first = WorldStreamingProfiler.BeginSample();
            WorldStreamingProfiler.EndSample(ProfilerStage.RingRefresh, first);

            WorldStreamingProfiler.Stop();
            WorldStreamingProfiler.Resume();

            long second = WorldStreamingProfiler.BeginSample();
            WorldStreamingProfiler.EndSample(ProfilerStage.RingRefresh, second);

            Assert.AreEqual(2, WorldStreamingProfiler.Stage(ProfilerStage.RingRefresh).Count,
                "Resume không được reset những mẫu đã thu trước checkpoint.");
            Assert.AreEqual(0, WorldStreamingProfiler.Stage(ProfilerStage.RingRefresh).Dropped);
        }

        [Test]
        public void FinalizedSummaries_SurviveALaterCapture()
        {
            WorldStreamingProfiler.Begin(64);
            for (int i = 1; i <= 10; i++)
            {
                long token = WorldStreamingProfiler.BeginSample();
                WorldStreamingProfiler.EndSample(ProfilerStage.ChunkAssign, token);
            }

            var result = new WorldStreamingBenchmarkResult(
                BenchmarkScenario.SteadyState, BenchmarkMode.SynchronousMicrobenchmark, "test");
            result.FinalizeTimings();

            int countBefore = result.Stage(ProfilerStage.ChunkAssign).Count;
            Assert.AreEqual(10, countBefore);

            // Kịch bản sau ghi đè bộ đệm dùng chung — kết quả đã chốt không được đổi theo.
            WorldStreamingProfiler.Begin(64);
            for (int i = 0; i < 3; i++)
            {
                long token = WorldStreamingProfiler.BeginSample();
                WorldStreamingProfiler.EndSample(ProfilerStage.ChunkAssign, token);
            }

            Assert.AreEqual(countBefore, result.Stage(ProfilerStage.ChunkAssign).Count,
                "Kết quả đã chốt vẫn trỏ vào bộ đệm sống.");
        }

        [Test]
        public void AllocationMetricIsNotBasedOnLiveHeapSize()
        {
            // GC.GetTotalMemory(false) là kích thước heap SỐNG, không phải lưu lượng cấp phát: nó có
            // thể đứng yên hoặc giảm trong khi vẫn đang cấp phát liên tục.
            string benchmark = File.ReadAllText(BenchmarkSourcePath);
            string result = File.ReadAllText(ResultSourcePath);

            StringAssert.DoesNotContain("GetTotalMemory", benchmark,
                "Benchmark vẫn đo cấp phát bằng kích thước heap sống.");
            StringAssert.DoesNotContain("GetTotalMemory", result,
                "Kết quả vẫn đo cấp phát bằng kích thước heap sống.");
            // GC.GetAllocatedBytesForCurrentThread cung KHONG dung duoc: tren runtime nay no dung im
            // ke ca khi cap phat 100 KB. Bo dem hop le duy nhat la ProfilerRecorder theo frame, va no
            // phai di kem mot lan cap phat doi chung.
            StringAssert.DoesNotContain("GetAllocatedBytesForCurrentThread", benchmark,
                "Bo dem nay khong phan ung tren runtime nay — khong duoc dung de ket luan.");
            StringAssert.Contains("ProfilerRecorder", benchmark,
                "Phai dung bo dem cap phat theo frame cua ProfilerRecorder.");
            StringAssert.Contains("AllocationCounterVerified", File.ReadAllText(ResultSourcePath),
                "Bao cao phai noi ro bo dem da duoc kiem chung hay chua.");
        }

        [Test]
        public void ProfilerHasNoStaticConstructorAllocation()
        {
            string source = File.ReadAllText(ProfilerSourcePath);
            StringAssert.DoesNotContain("static WorldStreamingProfiler()", source,
                "Static constructor cấp phát bộ đệm là đúng lỗi của bản M3A.");
        }

        [Test]
        public void ChunkInstanceDoesNotRenameOrReadTheClockDirectly()
        {
            string source = File.ReadAllText(ChunkInstanceSourcePath);

            StringAssert.DoesNotContain("Stopwatch.GetTimestamp", source,
                "Đường gán chunk không được đọc đồng hồ trực tiếp.");
            Assert.AreEqual(1, CountOccurrences(source, "new GameObject($\"ChunkRoot_"),
                "Tên chunk root chỉ được đặt đúng một lần, lúc tạo.");
            StringAssert.DoesNotContain("gameObject.name =", source,
                "Đổi tên GameObject trong đường chạy nóng sinh chuỗi mỗi lần gán chunk.");
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, index = 0;
            while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }

        // --- Phân vị -----------------------------------------------------------------------------

        [Test]
        public void Percentile_UsesNearestRank_OnAKnownSeries()
        {
            var series = new TimingSeries(16);
            for (int i = 1; i <= 10; i++) series.Add(i);

            Assert.AreEqual(10, series.Count);
            Assert.AreEqual(1f, series.Min, 1e-4f);
            Assert.AreEqual(10f, series.Max, 1e-4f);
            Assert.AreEqual(5f, series.P50, 1e-4f);
            Assert.AreEqual(10f, series.P95, 1e-4f);
            Assert.AreEqual(5.5f, series.Mean, 1e-4f);
        }

        [Test]
        public void Percentile_IsIndependentOfInsertionOrder()
        {
            var ascending = new TimingSeries(32);
            var descending = new TimingSeries(32);

            for (int i = 1; i <= 20; i++) ascending.Add(i);
            for (int i = 20; i >= 1; i--) descending.Add(i);

            Assert.AreEqual(ascending.P50, descending.P50, 1e-4f);
            Assert.AreEqual(ascending.P95, descending.P95, 1e-4f);
        }

        [Test]
        public void EmptySeries_ReportsZerosInsteadOfThrowing()
        {
            var series = new TimingSeries(8);
            Assert.AreEqual(0, series.Count);
            Assert.AreEqual(0f, series.P50);
            Assert.AreEqual(0f, series.P95);
        }

        [Test]
        public void OverflowingCapacity_IsReportedNotSilentlyTruncated()
        {
            var series = new TimingSeries(4);
            for (int i = 0; i < 10; i++) series.Add(i);

            Assert.AreEqual(4, series.Count);
            Assert.AreEqual(6, series.Dropped);
            Assert.AreEqual(6, series.Summarize().Dropped, "Bản tóm tắt phải mang theo số mẫu bị bỏ.");
        }

        // --- Tuyến benchmark -----------------------------------------------------------------------

        private static List<ChunkCoord> Route(BenchmarkScenario scenario)
        {
            var route = new List<ChunkCoord>();
            WorldStreamingBenchmarkRoute.Build(scenario, route);
            return route;
        }

        [Test]
        public void Routes_AreDeterministic()
        {
            foreach (BenchmarkScenario scenario in System.Enum.GetValues(typeof(BenchmarkScenario)))
                CollectionAssert.AreEqual(Route(scenario), Route(scenario), $"{scenario}: tuyến không tái lập.");
        }

        [Test]
        public void AdjacentRoute_MeetsItsContract()
        {
            List<ChunkCoord> route = Route(BenchmarkScenario.AdjacentTraversal);
            Assert.GreaterOrEqual(route.Count, 500);

            bool plusX = false, minusX = false, plusZ = false, minusZ = false;
            bool diagonalUp = false, diagonalDown = false, crossesZero = false;

            var previous = new ChunkCoord(0, 0);
            for (int i = 0; i < route.Count; i++)
            {
                int dx = route[i].X - previous.X;
                int dz = route[i].Z - previous.Z;

                Assert.LessOrEqual(System.Math.Abs(dx), 1, $"Bước {i} không liền kề theo X.");
                Assert.LessOrEqual(System.Math.Abs(dz), 1, $"Bước {i} không liền kề theo Z.");

                if (dx > 0 && dz == 0) plusX = true;
                if (dx < 0 && dz == 0) minusX = true;
                if (dz > 0 && dx == 0) plusZ = true;
                if (dz < 0 && dx == 0) minusZ = true;
                if (dx > 0 && dz > 0) diagonalUp = true;
                if (dx > 0 && dz < 0) diagonalDown = true;
                if (previous.X > 0 && route[i].X <= 0) crossesZero = true;
                if (previous.Z > 0 && route[i].Z <= 0) crossesZero = true;

                previous = route[i];
            }

            Assert.IsTrue(plusX && minusX && plusZ && minusZ, "Tuyến phải đi cả bốn hướng trục.");
            Assert.IsTrue(diagonalUp && diagonalDown, "Tuyến phải có cả hai hướng chéo.");
            Assert.IsTrue(crossesZero, "Tuyến phải đảo chiều qua mốc 0.");

            var seen = new HashSet<ChunkCoord>();
            int repeats = 0;
            foreach (ChunkCoord coord in route)
                if (!seen.Add(coord)) repeats++;

            Assert.Greater(repeats, 100, "Tuyến phải quay lại toạ độ cũ để ép slot dựng lại nội dung.");
        }

        [Test]
        public void SoakRoute_HasEnoughTransitions()
        {
            List<ChunkCoord> route = Route(BenchmarkScenario.Soak);
            Assert.GreaterOrEqual(route.Count, 1000);

            var distinct = new HashSet<ChunkCoord>(route);
            Assert.Greater(distinct.Count, 400);
        }

        [Test]
        public void TeleportRoute_IsNonAdjacentAndReturnsToOrigin()
        {
            List<ChunkCoord> route = Route(BenchmarkScenario.TeleportStress);
            bool hasNegative = false, hasLarge = false;

            var previous = new ChunkCoord(0, 0);
            foreach (ChunkCoord coord in route)
            {
                Assert.Greater(System.Math.Abs(coord.X - previous.X) + System.Math.Abs(coord.Z - previous.Z), 5);
                if (coord.X < 0 || coord.Z < 0) hasNegative = true;
                if (System.Math.Abs(coord.X) > 1000 || System.Math.Abs(coord.Z) > 1000) hasLarge = true;
                previous = coord;
            }

            Assert.IsTrue(hasNegative && hasLarge);
            Assert.AreEqual(new ChunkCoord(0, 0), route[route.Count - 1]);
        }

        [Test]
        public void CheckpointCoordinateIsSharedByEveryScenario()
        {
            // Mọi ảnh chụp bộ nhớ phải xảy ra tại đúng một toạ độ, nếu không "plateau" chỉ là so sánh
            // hai vùng biome khác nhau — đúng lỗi phương pháp của bản M3A.
            Assert.AreEqual(new ChunkCoord(74, 47), WorldStreamingBenchmarkRoute.Checkpoint);
        }
    }
}
