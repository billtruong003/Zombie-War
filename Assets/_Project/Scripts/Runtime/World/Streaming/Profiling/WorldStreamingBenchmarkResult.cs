using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>Cách một kết quả được đo — báo cáo phải nói rõ, không được trộn lẫn.</summary>
    public enum BenchmarkMode
    {
        /// <summary>Chạy trọn trong một tick, không có frame thật. Chỉ dùng cho microbenchmark CPU.</summary>
        SynchronousMicrobenchmark = 0,

        /// <summary>Mỗi bước một frame PlayMode thật. Đây là kết quả chính thức.</summary>
        FrameSpread = 1,
    }

    /// <summary>
    /// Ảnh chụp tài nguyên mà lab THẬT SỰ sở hữu tại một thời điểm.
    ///
    /// Hàm này tự nó có cấp phát (HashSet, mảng của GetComponentsInChildren). Vì thế nó phải được gọi
    /// NGOÀI cửa sổ đo cấp phát — nếu không benchmark sẽ đo chính sổ sách của mình rồi gán cho traversal.
    /// </summary>
    public struct OwnedResourceSnapshot
    {
        public int ChunkRoots;
        public int Meshes;
        public int Materials;
        public int Colliders;
        public int EnabledRenderers;
        public long MeshMemoryBytes;
        public int TotalVertices;
        public int TotalTriangles;
        public int Placements;
        public int SolidVertices;
        public int FoliageVertices;
        public ChunkCoord Centre;
        public bool Valid;

        public static OwnedResourceSnapshot Capture(WorldStreamManager manager)
        {
            var snapshot = new OwnedResourceSnapshot { Valid = true, Centre = manager.CurrentChunk };
            ChunkPool pool = manager.Pool;

            var materials = new HashSet<Material>();
            var meshes = new HashSet<Mesh>();

            IReadOnlyList<ChunkInstance> instances = pool.All;
            for (int i = 0; i < instances.Count; i++)
            {
                ChunkInstance chunk = instances[i];
                snapshot.ChunkRoots++;

                AddMesh(meshes, chunk.GroundMesh, ref snapshot);
                AddMesh(meshes, chunk.SolidMesh, ref snapshot);
                AddMesh(meshes, chunk.FoliageMesh, ref snapshot);

                if (chunk.GroundRenderer != null) materials.Add(chunk.GroundRenderer.sharedMaterial);
                if (chunk.SolidDecorRenderer != null) materials.Add(chunk.SolidDecorRenderer.sharedMaterial);
                if (chunk.FoliageRenderer != null) materials.Add(chunk.FoliageRenderer.sharedMaterial);

                if (chunk.GroundRenderer != null && chunk.GroundRenderer.enabled) snapshot.EnabledRenderers++;
                if (chunk.SolidDecorRenderer != null && chunk.SolidDecorRenderer.enabled) snapshot.EnabledRenderers++;
                if (chunk.FoliageRenderer != null && chunk.FoliageRenderer.enabled) snapshot.EnabledRenderers++;

                snapshot.Colliders += chunk.GetComponentsInChildren<Collider>(true).Length;

                if (!chunk.IsAssigned) continue;
                snapshot.Placements += chunk.PlacementCount;
                snapshot.SolidVertices += chunk.SolidVertexCount;
                snapshot.FoliageVertices += chunk.FoliageVertexCount;
            }

            if (manager.Surface != null && manager.Surface.Collider != null) snapshot.Colliders++;

            snapshot.Meshes = meshes.Count;
            snapshot.Materials = materials.Count;
            return snapshot;
        }

        private static void AddMesh(HashSet<Mesh> meshes, Mesh mesh, ref OwnedResourceSnapshot snapshot)
        {
            if (mesh == null || !meshes.Add(mesh)) return;

            snapshot.TotalVertices += mesh.vertexCount;
            if (mesh.subMeshCount > 0) snapshot.TotalTriangles += (int)(mesh.GetIndexCount(0) / 3);
            snapshot.MeshMemoryBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh);
        }

        /// <summary>Hai ảnh chụp có mô tả cùng một trạng thái thế giới hay không.</summary>
        public bool DescribesSameWorldAs(in OwnedResourceSnapshot other) =>
            Valid && other.Valid &&
            Centre == other.Centre &&
            Placements == other.Placements &&
            SolidVertices == other.SolidVertices &&
            FoliageVertices == other.FoliageVertices &&
            TotalVertices == other.TotalVertices &&
            TotalTriangles == other.TotalTriangles;
    }

    /// <summary>
    /// Kết quả của một lần chạy benchmark.
    ///
    /// Các bản tóm tắt thời gian được CHÉP vào đây khi chốt, nên kết quả không đổi khi kịch bản sau
    /// bắt đầu ghi đè bộ đệm mẫu dùng chung.
    /// </summary>
    public class WorldStreamingBenchmarkResult
    {
        public readonly BenchmarkScenario Scenario;
        public readonly BenchmarkMode Mode;
        public readonly string Environment;

        public int Transitions;
        public int ChunkAssignments;
        public int FramesObserved;
        public int DistinctFrames;
        public float WallClockMs;

        // --- Bộ lập lịch trang trí (M3B.2) ------------------------------------------------------
        //
        // Từ M3B.2, "đã gán chunk" và "đã sinh trang trí" là hai sự kiện khác nhau, xảy ra ở hai frame
        // khác nhau. Báo cáo phải in cả hai, vì một tuyến chạy nhanh hơn tốc độ sinh sẽ gán nhiều hơn
        // hẳn số nó kịp dựng — và phần chênh đó là việc bị huỷ, không phải thế giới đã tạo ra.

        /// <summary>Số việc được xếp hàng trong cửa sổ đo.</summary>
        public int DecorationJobsQueued;

        /// <summary>Số việc đã dựng xong và đã áp vào mesh.</summary>
        public int DecorationJobsCompleted;

        /// <summary>Số việc bị bỏ vì toạ độ rời ring hoặc slot bị gán lại trước khi tới lượt.</summary>
        public int DecorationJobsCancelled;

        public int MaxDecorationQueueDepth;

        /// <summary>Số việc nhiều nhất hoàn thành trong MỘT frame. Phải &lt;= ngân sách cấu hình.</summary>
        public int MaxJobsCompletedInOneFrame;

        /// <summary>Ngân sách việc/frame lúc chạy, để đọc con số trên mà không phải tra config.</summary>
        public int ConfiguredJobsPerFrame;

        /// <summary>Số frame cần để hàng đợi cạn sau khi tuyến chạy kết thúc.</summary>
        public int FramesToSettleAfterRoute;

        /// <summary>Không còn việc hợp lệ nào chờ tại thời điểm chụp ảnh mốc cuối.</summary>
        public bool SettledAtFinalCheckpoint;

        /// <summary>
        /// Byte cấp phát mỗi frame, cộng dồn từ bộ đếm `GC Allocated In Frame` của ProfilerRecorder.
        ///
        /// Đây là lưu lượng cấp phát THEO FRAME, không phải kích thước heap sống — và nó bao gồm MỌI
        /// việc trên main thread của frame đó, không riêng phần sinh thế giới. Muốn quy trách nhiệm thì
        /// phải so hiệu giữa frame có di chuyển và frame đứng yên.
        ///
        /// `GC.GetAllocatedBytesForCurrentThread()` KHÔNG dùng được ở đây: trên runtime này nó đứng im
        /// kể cả khi cấp phát 100 KB, nên mọi con số lấy từ nó đều vô nghĩa.
        /// </summary>
        public long AllocatedBytes;

        /// <summary>
        /// Bộ đếm cấp phát đã được kiểm chứng bằng một lần cấp phát đối chứng hay chưa.
        /// Nếu false thì mọi con số cấp phát phải bị coi là CHƯA KIỂM CHỨNG, không được đọc là "0 B".
        /// </summary>
        public bool AllocationCounterVerified;

        public long AllocationControlObserved;
        public int AllocationSamples;
        public int GcGen0Collections;
        public int GcGen1Collections;

        public OwnedResourceSnapshot CheckpointWarmup;
        public OwnedResourceSnapshot CheckpointMid;
        public OwnedResourceSnapshot CheckpointEnd;

        public int RootsCreatedAfterWarmup;
        public int RootsDestroyedDuringTraversal;
        public int GroundMeshesCreatedAfterWarmup;
        public int DecorationMeshesCreatedAfterWarmup;
        public int RecycleCount;
        public int RefreshCount;

        public int ActiveLeases;
        public int RequiredCoords;
        public int DuplicateCoords;
        public int DuplicateSlots;
        public int PoolCapacity;
        public int UInt32Meshes;
        public int BudgetRejections;

        public int Batches = -1;
        public int SetPassCalls = -1;
        public int DrawCalls = -1;

        public float FrameMsP50;
        public float FrameMsP95;
        public float FrameMsMax;

        private readonly TimingSummary[] _stages = new TimingSummary[WorldStreamingProfiler.StageCount];

        public WorldStreamingBenchmarkResult(BenchmarkScenario scenario, BenchmarkMode mode, string environment)
        {
            Scenario = scenario;
            Mode = mode;
            Environment = environment;
        }

        public TimingSummary Stage(ProfilerStage stage) => _stages[(int)stage];

        /// <summary>Chép các bản tóm tắt ra khỏi bộ đệm dùng chung để kết quả trở nên bất biến.</summary>
        public void FinalizeTimings()
        {
            for (int i = 0; i < WorldStreamingProfiler.StageCount; i++)
                _stages[i] = WorldStreamingProfiler.Summarize((ProfilerStage)i);
        }

        public void CaptureStreamingState(WorldStreamManager manager)
        {
            ChunkPool pool = manager.Pool;
            RootsCreatedAfterWarmup = pool.RootsCreatedSinceWarmup;
            RootsDestroyedDuringTraversal = pool.RootsDestroyedDuringTraversal;
            GroundMeshesCreatedAfterWarmup = pool.GroundMeshesCreatedSinceWarmup;
            DecorationMeshesCreatedAfterWarmup = pool.DecorationMeshesCreatedSinceWarmup;
            PoolCapacity = pool.Capacity;
            ActiveLeases = manager.ActiveLeaseCount;
            RequiredCoords = manager.Config.ActiveChunkCount;

            DuplicateCoords = 0;
            DuplicateSlots = 0;
            UInt32Meshes = 0;
            BudgetRejections = 0;

            var coords = new HashSet<ChunkCoord>();
            var slots = new HashSet<int>();
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in manager.ActiveLeases)
            {
                if (!coords.Add(lease.Key)) DuplicateCoords++;
                if (!slots.Add(lease.Value.SlotId)) DuplicateSlots++;
            }

            IReadOnlyList<ChunkInstance> instances = pool.All;
            for (int i = 0; i < instances.Count; i++)
            {
                ChunkInstance chunk = instances[i];
                if (!chunk.IsAssigned) continue;

                BudgetRejections += chunk.DecorationStats.RejectedByBudget;
                if (chunk.SolidMesh != null && chunk.SolidMesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32) UInt32Meshes++;
                if (chunk.FoliageMesh != null && chunk.FoliageMesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32) UInt32Meshes++;
                if (chunk.GroundMesh != null && chunk.GroundMesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32) UInt32Meshes++;
            }

#if UNITY_EDITOR
            Batches = UnityEditor.UnityStats.batches;
            SetPassCalls = UnityEditor.UnityStats.setPassCalls;
            DrawCalls = UnityEditor.UnityStats.drawCalls;
#endif
        }

        public float AllocatedBytesPerTransition => Transitions > 0 ? AllocatedBytes / (float)Transitions : 0f;

        public float AllocatedBytesPerAssignment =>
            ChunkAssignments > 0 ? AllocatedBytes / (float)ChunkAssignments : 0f;

        /// <summary>
        /// Bộ nhớ mesh có đứng lại hay không, so ở NHỮNG TRẠNG THÁI THẾ GIỚI TƯƠNG ĐƯƠNG.
        ///
        /// M3A so ba ảnh chụp ở ba toạ độ khác nhau rồi kết luận "plateau"; chênh lệch khi đó chỉ nói
        /// lên mật độ biome khác nhau. Ở đây cả ba mốc đều chụp tại cùng một toạ độ checkpoint.
        /// </summary>
        public bool MeshMemoryPlateaus =>
            CheckpointWarmup.Valid && CheckpointMid.Valid && CheckpointEnd.Valid &&
            CheckpointWarmup.DescribesSameWorldAs(CheckpointMid) &&
            CheckpointWarmup.DescribesSameWorldAs(CheckpointEnd) &&
            CheckpointEnd.MeshMemoryBytes <= CheckpointWarmup.MeshMemoryBytes;

        /// <summary>
        /// Bộ nhớ có NGỪNG TĂNG ở nửa sau hay không.
        ///
        /// Đây mới là câu hỏi đúng. Buffer của Mesh trong Unity giữ lại dung lượng lớn nhất từng dùng,
        /// nên tổng bộ nhớ sở hữu leo lên một trần rồi đứng lại — nó không bao giờ quay về đúng con số
        /// của lần chụp đầu tiên, và đòi hỏi điều đó là đòi hỏi sai.
        /// </summary>
        public bool MeshMemoryConverges =>
            CheckpointMid.Valid && CheckpointEnd.Valid &&
            CheckpointEnd.MeshMemoryBytes <= CheckpointMid.MeshMemoryBytes * 1.02f;

        public bool CheckpointsAreEquivalent =>
            CheckpointWarmup.DescribesSameWorldAs(CheckpointMid) &&
            CheckpointWarmup.DescribesSameWorldAs(CheckpointEnd);

        private void AppendStage(StringBuilder sb, ProfilerStage stage, string label)
        {
            TimingSummary s = Stage(stage);
            sb.Append("  ").Append(label.PadRight(22))
              .Append("n=").Append(s.Count.ToString().PadLeft(6))
              .Append("  min=").Append(s.Min.ToString("F3").PadLeft(8))
              .Append("  p50=").Append(s.P50.ToString("F3").PadLeft(8))
              .Append("  p95=").Append(s.P95.ToString("F3").PadLeft(8))
              .Append("  max=").Append(s.Max.ToString("F3").PadLeft(8))
              .Append("  total=").Append(s.Total.ToString("F1").PadLeft(9)).Append(" ms");
            if (s.Dropped > 0) sb.Append("  DROPPED=").Append(s.Dropped);
            sb.Append('\n');
        }

        public string ToReport()
        {
            var sb = new StringBuilder(4096);
            sb.Append("=== ").Append(Scenario).Append("  [").Append(Mode == BenchmarkMode.FrameSpread
                ? "FRAME-SPREAD (canonical)"
                : "SYNCHRONOUS MICROBENCHMARK (not a frame result)").Append("] ===\n");
            sb.Append("environment: ").Append(Environment).Append('\n');
            sb.Append("transitions=").Append(Transitions)
              .Append("  chunkAssignments=").Append(ChunkAssignments)
              .Append("  distinctFrames=").Append(DistinctFrames)
              .Append("  wallClock=").Append(WallClockMs.ToString("F1")).Append(" ms\n");

            sb.Append("\noperation timing (ms):\n");
            AppendStage(sb, ProfilerStage.GroundBiome, "ground biome");
            AppendStage(sb, ProfilerStage.DecorationSample, "decoration sample");
            AppendStage(sb, ProfilerStage.DecorationBuild, "decoration build");
            AppendStage(sb, ProfilerStage.SolidApply, "solid apply");
            AppendStage(sb, ProfilerStage.FoliageApply, "foliage apply");
            AppendStage(sb, ProfilerStage.DecorationJob, "decoration JOB TOTAL");
            AppendStage(sb, ProfilerStage.ChunkAssign, "immediate assign");
            AppendStage(sb, ProfilerStage.RingRefresh, "ring remap (enqueue)");

            sb.Append("\ndecoration scheduling (M3B.2):\n");
            sb.Append("  budget=").Append(ConfiguredJobsPerFrame).Append(" job(s)/frame")
              .Append("   queued=").Append(DecorationJobsQueued)
              .Append("   completed=").Append(DecorationJobsCompleted)
              .Append("   cancelledStale=").Append(DecorationJobsCancelled).Append('\n');
            sb.Append("  maxQueueDepth=").Append(MaxDecorationQueueDepth)
              .Append("   maxJobsInOneFrame=").Append(MaxJobsCompletedInOneFrame)
              .Append("   framesToSettleAfterRoute=").Append(FramesToSettleAfterRoute)
              .Append("   settledAtFinalCheckpoint=").Append(SettledAtFinalCheckpoint).Append('\n');
            // In thẳng phép đối chiếu vào báo cáo thay vì để người đọc tự nhẩm. Nếu số mẫu lệch số việc
            // thì hoặc có mẫu bị rơi, hoặc có việc chạy ngoài cửa sổ đo — cả hai đều làm phân phối thời
            // gian ở trên mất giá trị, và báo cáo phải tự nói ra điều đó.
            TimingSummary jobStage = Stage(ProfilerStage.DecorationJob);
            bool samplesMatch = jobStage.Count == DecorationJobsCompleted;
            sb.Append("  integrity: jobSamples=").Append(jobStage.Count)
              .Append(" vs completed=").Append(DecorationJobsCompleted)
              .Append(samplesMatch ? "  MATCH" : "  *** MISMATCH ***").Append('\n');

            sb.Append("  NOTE: 'ring remap' is enqueue-only from M3B.2 — the decoration cost it used to\n");
            sb.Append("        contain now appears under 'decoration JOB TOTAL', spread across frames.\n");
            sb.Append("        Cancelled jobs are NOT generated world content.\n");

            if (Mode == BenchmarkMode.FrameSpread)
                sb.Append("\nframe time (ms): p50=").Append(FrameMsP50.ToString("F2"))
                  .Append("  p95=").Append(FrameMsP95.ToString("F2"))
                  .Append("  max=").Append(FrameMsMax.ToString("F2")).Append('\n');

            sb.Append("\nallocation (ProfilerRecorder 'GC Allocated In Frame' — per-frame throughput):\n");
            if (!AllocationCounterVerified)
            {
                sb.Append("  UNVERIFIED — the allocation counter did not respond to a control allocation.\n");
                sb.Append("  Do NOT read the bytes below as a measurement.\n");
            }

            sb.Append("  control allocation observed: ").Append(AllocationControlObserved).Append(" B\n");
            sb.Append("  NOTE: covers ALL main-thread work in each measured frame, not only world generation.\n");
            sb.Append("  total=").Append(AllocatedBytes).Append(" B over ").Append(AllocationSamples).Append(" frames");
            if (Transitions > 0) sb.Append("  = ").Append(AllocatedBytesPerTransition.ToString("F1")).Append(" B/transition");
            if (ChunkAssignments > 0) sb.Append("  = ").Append(AllocatedBytesPerAssignment.ToString("F1")).Append(" B/assignment");
            sb.Append("\n  GC collections during window: gen0=").Append(GcGen0Collections)
              .Append(" gen1=").Append(GcGen1Collections).Append('\n');

            sb.Append("\nstreaming correctness:\n");
            sb.Append("  leases=").Append(ActiveLeases).Append('/').Append(RequiredCoords)
              .Append("  poolCapacity=").Append(PoolCapacity)
              .Append("  duplicateCoords=").Append(DuplicateCoords)
              .Append("  duplicateSlots=").Append(DuplicateSlots).Append('\n');
            sb.Append("  rootsAfterWarmup=").Append(RootsCreatedAfterWarmup)
              .Append("  rootsDestroyed=").Append(RootsDestroyedDuringTraversal)
              .Append("  groundMeshesAfterWarmup=").Append(GroundMeshesCreatedAfterWarmup)
              .Append("  decorMeshesAfterWarmup=").Append(DecorationMeshesCreatedAfterWarmup).Append('\n');
            sb.Append("  colliders=").Append(CheckpointEnd.Colliders)
              .Append("  materials=").Append(CheckpointEnd.Materials)
              .Append("  meshes=").Append(CheckpointEnd.Meshes)
              .Append("  uint32Meshes=").Append(UInt32Meshes)
              .Append("  budgetRejections=").Append(BudgetRejections).Append('\n');

            sb.Append("\nowned memory at IDENTICAL checkpoint coordinate ").Append(CheckpointWarmup.Centre).Append(":\n");
            sb.Append("  warmup: ").Append((CheckpointWarmup.MeshMemoryBytes / 1048576f).ToString("F2")).Append(" MB")
              .Append("  placements=").Append(CheckpointWarmup.Placements)
              .Append("  v/t=").Append(CheckpointWarmup.TotalVertices).Append('/').Append(CheckpointWarmup.TotalTriangles).Append('\n');
            sb.Append("  mid   : ").Append((CheckpointMid.MeshMemoryBytes / 1048576f).ToString("F2")).Append(" MB")
              .Append("  placements=").Append(CheckpointMid.Placements)
              .Append("  v/t=").Append(CheckpointMid.TotalVertices).Append('/').Append(CheckpointMid.TotalTriangles).Append('\n');
            sb.Append("  end   : ").Append((CheckpointEnd.MeshMemoryBytes / 1048576f).ToString("F2")).Append(" MB")
              .Append("  placements=").Append(CheckpointEnd.Placements)
              .Append("  v/t=").Append(CheckpointEnd.TotalVertices).Append('/').Append(CheckpointEnd.TotalTriangles).Append('\n');
            sb.Append("  equivalent world state at all three: ").Append(CheckpointsAreEquivalent)
              .Append("   no growth in 2nd half (converged): ").Append(MeshMemoryConverges)
              .Append("   end <= warmup: ").Append(MeshMemoryPlateaus).Append('\n');

            sb.Append("\nrendering (Editor stats):\n");
            sb.Append("  batches=").Append(Batches)
              .Append("  setPass=").Append(SetPassCalls)
              .Append("  drawCalls=").Append(DrawCalls)
              .Append("  enabledRenderers=").Append(CheckpointEnd.EnabledRenderers).Append('\n');

            return sb.ToString();
        }
    }
}
