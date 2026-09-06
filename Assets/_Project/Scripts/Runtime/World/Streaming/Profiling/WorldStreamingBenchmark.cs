using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    public enum BenchmarkScenario
    {
        SteadyState = 0,
        AdjacentTraversal = 1,
        TeleportStress = 2,
        Soak = 3,
    }

    /// <summary>
    /// Các tuyến chạy benchmark, sinh ra hoàn toàn tất định.
    /// </summary>
    public static class WorldStreamingBenchmarkRoute
    {
        /// <summary>Toạ độ mốc dùng cho mọi ảnh chụp bộ nhớ — luôn là cùng một trạng thái thế giới.</summary>
        public static readonly ChunkCoord Checkpoint = new ChunkCoord(74, 47);

        public static void Build(BenchmarkScenario scenario, List<ChunkCoord> route)
        {
            if (route == null) return;
            route.Clear();

            switch (scenario)
            {
                case BenchmarkScenario.SteadyState:
                    route.Add(Checkpoint);
                    break;
                case BenchmarkScenario.AdjacentTraversal:
                    BuildAdjacent(route);
                    break;
                case BenchmarkScenario.TeleportStress:
                    BuildTeleport(route);
                    break;
                case BenchmarkScenario.Soak:
                    BuildSoak(route);
                    break;
            }
        }

        private static void BuildAdjacent(List<ChunkCoord> route)
        {
            var cursor = new ChunkCoord(0, 0);

            void Walk(int dx, int dz, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    cursor = new ChunkCoord(cursor.X + dx, cursor.Z + dz);
                    route.Add(cursor);
                }
            }

            // Vòng KHÉP KÍN: mỗi vòng kết thúc đúng tại (0,0) nên vòng sau nối tiếp liền kề. Nếu vòng
            // không khép, bước đầu vòng hai là một cú nhảy xa và kéo theo một lần dựng lại cả 25 chunk
            // — đúng thứ mà kịch bản "đi liền kề" phải loại trừ.
            for (int lap = 0; lap < 2; lap++)
            {
                Walk(1, 0, 40);
                Walk(0, 1, 40);
                Walk(-1, 0, 80);
                Walk(0, -1, 80);
                Walk(1, 1, 40);
                Walk(1, -1, 30);
                Walk(-1, 1, 30);
            }
        }

        private static void BuildTeleport(List<ChunkCoord> route)
        {
            route.Add(new ChunkCoord(74, 47));
            route.Add(new ChunkCoord(-73, -44));
            route.Add(new ChunkCoord(1250, 980));
            route.Add(new ChunkCoord(-1840, 1330));
            route.Add(new ChunkCoord(-620, 391));
            route.Add(new ChunkCoord(9001, -7777));
            route.Add(new ChunkCoord(0, 0));
        }

        private static void BuildSoak(List<ChunkCoord> route)
        {
            var cursor = new ChunkCoord(0, 0);

            void Walk(int dx, int dz, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    cursor = new ChunkCoord(cursor.X + dx, cursor.Z + dz);
                    route.Add(cursor);
                }
            }

            for (int ring = 1; ring <= 16; ring++)
            {
                Walk(1, 0, ring * 2);
                Walk(0, 1, ring * 2);
                Walk(-1, 0, ring * 2 + 1);
                Walk(0, -1, ring * 2 + 1);

                if (ring % 4 != 0) continue;

                cursor = new ChunkCoord(ring * 300, -ring * 240);
                route.Add(cursor);
                cursor = new ChunkCoord(0, 0);
                route.Add(cursor);
            }

            for (int lap = 0; lap < 2; lap++)
            {
                cursor = new ChunkCoord(0, 0);
                Walk(1, 1, 60);
                Walk(-1, 1, 60);
                Walk(-1, -1, 60);
                Walk(1, -1, 60);
            }
        }
    }

    /// <summary>
    /// Bộ chạy benchmark TRẢI THEO FRAME — đây là đường đo chính thức.
    ///
    /// Vì sao phải trải theo frame: bản M3A chạy cả kịch bản trong một tick Editor, nên "steady state"
    /// thực ra chỉ là 240 lời gọi `RefreshNow()` liên tiếp chứ không phải 240 khung hình. Nó không nói
    /// gì về PlayerLoop, về cấp phát mỗi frame hay về độ ổn định frame-time, và nó treo Editor hàng
    /// chục giây.
    ///
    /// Ở đây mỗi bước chỉ chạy khi <c>Time.frameCount</c> đã thật sự tăng, nên giữa hai bước luôn có
    /// ít nhất một khung hình được vẽ.
    /// </summary>
    public sealed class FrameSpreadBenchmark
    {
        /// <summary>
        /// <see cref="Phase.Settle"/> là trạm chờ dùng chung: nó không tự làm gì, chỉ đứng lại cho tới
        /// khi <see cref="WorldStreamManager.IsGenerationSettled"/> bật lên rồi chuyển sang
        /// <see cref="_afterSettle"/>. Bộ lập lịch chạy trong LateUpdate của manager, nên "chờ" ở đây
        /// đúng nghĩa là để các frame thật trôi qua.
        ///
        /// Phải có trạm này vì mọi ảnh chụp mốc đều so sánh nội dung thế giới. Chụp lúc hàng đợi còn
        /// việc sẽ ra một thế giới dựng dở, và ba ảnh chụp sẽ khác nhau vì lý do không liên quan gì tới
        /// bộ nhớ.
        /// </summary>
        private enum Phase
        {
            Warmup,
            Settle,
            CheckpointWarmup,
            FirstHalf,
            RouteDrainFirst,
            CheckpointMid,
            SecondHalf,
            RouteDrainSecond,
            CheckpointEnd,
            Done,
        }

        private readonly List<ChunkCoord> _route = new List<ChunkCoord>(2048);
        private readonly List<float> _frameMs = new List<float>(4096);

        private WorldStreamManager _manager;
        private WorldStreamingBenchmarkResult _result;
        private Phase _phase;
        private int _index;
        private int _lastFrame = -1;
        private int _warmupFramesLeft;
        private int _steadyFramesLeft;
        private long _startTimestamp;
        private long _allocBaseline;
        private int _gen0Baseline;
        private int _gen1Baseline;
        private int _assignmentsBaseline;
        private Unity.Profiling.ProfilerRecorder _allocRecorder;
        private byte[] _allocControl;
        private int _controlFrame = -1;
        private Phase _afterSettle;
        private int _lastCompletedTotal;
        private int _routeDrainFrames;
        private bool _countingRouteDrain;

        // Bộ đếm của bộ lập lịch phải theo ĐÚNG cửa sổ đo, không phải theo cả lần chạy. Di chuyển về
        // mốc cũng sinh việc thật, nhưng nó nằm ngoài cửa sổ nên không có mẫu thời gian tương ứng —
        // gộp vào sẽ khiến "số việc hoàn thành" lớn hơn "số mẫu đo" và làm hỏng phép đối chiếu.
        private int _windowCompleted;
        private int _windowCancelled;
        private int _resumeCompletedBaseline;
        private int _resumeCancelledBaseline;

        private bool IsMeasuring =>
            _phase == Phase.FirstHalf || _phase == Phase.SecondHalf ||
            _phase == Phase.RouteDrainFirst || _phase == Phase.RouteDrainSecond;

        public bool IsRunning { get; private set; }
        public WorldStreamingBenchmarkResult Result => _result;
        public BenchmarkScenario Scenario { get; private set; }

        /// <summary>Số frame steady-state bắt buộc — yêu cầu M3A.1 là ít nhất 240 frame thật.</summary>
        public const int SteadyStateFrames = 260;

        public void Start(WorldStreamManager manager, BenchmarkScenario scenario, string environment)
        {
            if (manager == null || !manager.IsInitialized)
                throw new InvalidOperationException("[WorldStreaming] Manager chưa khởi tạo.");

            _manager = manager;
            Scenario = scenario;
            _result = new WorldStreamingBenchmarkResult(scenario, BenchmarkMode.FrameSpread, environment);

            WorldStreamingBenchmarkRoute.Build(scenario, _route);
            _frameMs.Clear();

            // Bo dem phai song qua nhieu frame: doc trong cung mot tick luon tra 0.
            _allocRecorder = Unity.Profiling.ProfilerRecorder.StartNew(
                Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
            _allocControl = null;
            _controlFrame = -1;

            _phase = Phase.Warmup;
            _index = 0;
            _lastFrame = -1;
            _warmupFramesLeft = 8;
            _steadyFramesLeft = SteadyStateFrames;
            _routeDrainFrames = 0;
            _countingRouteDrain = false;
            _result.ConfiguredJobsPerFrame = manager.Config.DecorationJobsPerFrame;
            IsRunning = true;

            GoToCheckpoint();
        }

        /// <summary>Dừng an toàn ở bất kỳ đâu: profiler LUÔN được tắt.</summary>
        public void Cancel()
        {
            IsRunning = false;
            _phase = Phase.Done;
            WorldStreamingProfiler.Stop();
            DisposeRecorder();
        }

        private void DisposeRecorder()
        {
            if (_allocRecorder.Valid) _allocRecorder.Dispose();
            _allocControl = null;
        }

        /// <summary>
        /// Cap phat doi chung: neu bo dem khong nhuc nhich sau khi ta co tinh cap phat 256 KB thi no
        /// khong do duoc gi, va bao cao phai noi "CHUA KIEM CHUNG" thay vi in ra so 0.
        /// </summary>
        private void RunAllocationControl()
        {
            if (_controlFrame >= 0) return;

            _controlFrame = Time.frameCount;
            _allocControl = new byte[262144];
        }

        private void ObserveAllocationControl()
        {
            if (_controlFrame < 0 || _result.AllocationCounterVerified) return;
            if (Time.frameCount <= _controlFrame) return;

            long observed = _allocRecorder.Valid ? _allocRecorder.LastValue : 0L;
            if (observed <= 0L) return;

            _result.AllocationControlObserved = observed;
            _result.AllocationCounterVerified = observed >= 200000L;
            _allocControl = null;
        }

        private void GoToCheckpoint()
        {
            // Di chuyển về mốc KHÔNG được tính vào phân phối thời gian: tắt capture trước.
            bool wasCapturing = WorldStreamingProfiler.IsCapturing;
            WorldStreamingProfiler.Stop();

            Vector3 centre = WorldStreamingBenchmarkRoute.Checkpoint.ToWorldCenter(_manager.Config.ChunkSize);
            _manager.TeleportTargetTo(new Vector3(centre.x, 1f, centre.z));

            if (wasCapturing) WorldStreamingProfiler.Resume();
        }

        private static int CapacityFor(BenchmarkScenario scenario) =>
            scenario == BenchmarkScenario.Soak ? 32768 : 8192;

        /// <summary>Gọi mỗi khi có một frame PlayMode mới. Trả về true khi đã xong.</summary>
        public bool Tick()
        {
            if (!IsRunning) return true;

            if (!Application.isPlaying)
            {
                Cancel();
                return true;
            }

            // Cổng frame: chỉ đi tiếp khi PlayerLoop đã thật sự chạy thêm một khung hình.
            if (Time.frameCount == _lastFrame) return false;
            _lastFrame = Time.frameCount;
            _result.DistinctFrames++;

            if (_phase != Phase.Warmup) _frameMs.Add(Time.unscaledDeltaTime * 1000f);

            // Cap phat cua frame TRUOC do — doc dau frame nay moi co gia tri hop le.
            if (_phase == Phase.FirstHalf || _phase == Phase.SecondHalf)
            {
                if (_allocRecorder.Valid) _result.AllocatedBytes += _allocRecorder.LastValue;
                _result.AllocationSamples++;
            }

            ObserveAllocationControl();
            ObserveSchedulerProgress();

            try
            {
                Advance();
            }
            catch
            {
                // Ngoại lệ giữa chừng cũng phải để lại profiler ở trạng thái tắt.
                Cancel();
                throw;
            }

            return !IsRunning;
        }

        /// <summary>
        /// Theo dõi tiến độ bộ lập lịch theo từng frame thật.
        ///
        /// Số việc hoàn thành mỗi frame được tính bằng HIỆU của tổng tích luỹ giữa hai frame, chứ không
        /// đọc bộ đếm "lần chạy gần nhất" của bộ lập lịch. Lý do: benchmark tick từ vòng update của
        /// Editor còn bộ lập lịch chạy trong LateUpdate, nên thứ tự hai bên không được đảm bảo — còn
        /// phép hiệu thì đúng bất kể ai chạy trước.
        /// </summary>
        private void ObserveSchedulerProgress()
        {
            int completed = _manager.CompletedDecorationJobs;
            int delta = completed - _lastCompletedTotal;
            _lastCompletedTotal = completed;

            // Chỉ ghi nhận trong cửa sổ đo. Ngoài cửa sổ, việc vét hàng đợi lúc về mốc chạy không giới
            // hạn ngân sách, nên tính vào đây sẽ báo một đỉnh "25 việc trong một frame" chưa từng xảy
            // ra trên đường chạy bình thường.
            if (!IsMeasuring) return;

            if (delta > _result.MaxJobsCompletedInOneFrame) _result.MaxJobsCompletedInOneFrame = delta;

            int depth = _manager.PendingDecorationCount;
            if (depth > _result.MaxDecorationQueueDepth) _result.MaxDecorationQueueDepth = depth;

            if (_countingRouteDrain) _routeDrainFrames++;
        }

        /// <summary>Chuyển sang trạm chờ; khi thế giới dựng xong thì đi tiếp tới <paramref name="next"/>.</summary>
        private void BeginSettle(Phase next)
        {
            _afterSettle = next;
            _phase = Phase.Settle;
        }

        private void Advance()
        {
            switch (_phase)
            {
                case Phase.Warmup:
                    if (--_warmupFramesLeft > 0) return;
                    BeginSettle(Phase.CheckpointWarmup);
                    return;

                case Phase.Settle:
                    if (!_manager.IsGenerationSettled) return;
                    _phase = _afterSettle;
                    return;

                case Phase.CheckpointWarmup:
                    _result.CheckpointWarmup = OwnedResourceSnapshot.Capture(_manager);
                    BeginMeasuredWindow();
                    _phase = Phase.FirstHalf;
                    return;

                case Phase.FirstHalf:
                    if (StepRoute(_route.Count / 2)) return;
                    // Cạn hàng đợi khi ĐANG còn đo: những việc này là công việc thật của tuyến chạy,
                    // chỉ là chúng rơi vào các frame sau bước cuối cùng.
                    // Đếm lại từ đầu cho mỗi nửa: con số cần trả lời là "một tuyến chạy xong thì bao lâu
                    // nữa thế giới đầy đủ", chứ không phải tổng của hai lần.
                    _routeDrainFrames = 0;
                    _countingRouteDrain = true;
                    _phase = Phase.RouteDrainFirst;
                    return;

                case Phase.RouteDrainFirst:
                    if (!_manager.IsGenerationSettled) return;
                    _countingRouteDrain = false;
                    EndMeasuredWindow();
                    GoToCheckpoint();
                    BeginSettle(Phase.CheckpointMid);
                    return;

                case Phase.CheckpointMid:
                    _result.CheckpointMid = OwnedResourceSnapshot.Capture(_manager);
                    ResumeMeasuredWindow();
                    _phase = Phase.SecondHalf;
                    return;

                case Phase.SecondHalf:
                    if (StepRoute(_route.Count)) return;
                    // Đếm lại từ đầu cho mỗi nửa: con số cần trả lời là "một tuyến chạy xong thì bao lâu
                    // nữa thế giới đầy đủ", chứ không phải tổng của hai lần.
                    _routeDrainFrames = 0;
                    _countingRouteDrain = true;
                    _phase = Phase.RouteDrainSecond;
                    return;

                case Phase.RouteDrainSecond:
                    if (!_manager.IsGenerationSettled) return;
                    _countingRouteDrain = false;
                    EndMeasuredWindow();
                    GoToCheckpoint();
                    BeginSettle(Phase.CheckpointEnd);
                    return;

                case Phase.CheckpointEnd:
                    _result.SettledAtFinalCheckpoint = _manager.IsGenerationSettled;
                    _result.CheckpointEnd = OwnedResourceSnapshot.Capture(_manager);
                    Complete();
                    return;
            }
        }

        /// <summary>Một bước tuyến mỗi frame. Trả về true nếu còn bước để đi.</summary>
        private bool StepRoute(int limit)
        {
            if (Scenario == BenchmarkScenario.SteadyState)
            {
                // Đứng yên: chỉ đếm frame thật, không di chuyển gì.
                if (--_steadyFramesLeft <= 0) return false;
                MeasureAllocationOf(null);
                return true;
            }

            if (_index >= limit || _index >= _route.Count) return false;

            ChunkCoord target = _route[_index++];
            MeasureAllocationOf(target);
            return true;
        }

        /// <summary>
        /// Đo cấp phát của ĐÚNG lời gọi di chuyển thế giới.
        ///
        /// Cửa sổ hẹp là có chủ ý: ảnh chụp tài nguyên, dựng tuyến, sắp xếp phân vị và format báo cáo
        /// đều tự cấp phát, và bản M3A đã gộp chúng vào cùng một con số rồi gán cho traversal.
        /// </summary>
        private void MeasureAllocationOf(ChunkCoord? target)
        {
            int assignBefore = TotalAssignments();

            if (target.HasValue)
            {
                Vector3 centre = target.Value.ToWorldCenter(_manager.Config.ChunkSize);
                long refreshToken = WorldStreamingProfiler.BeginSample();
                _manager.TeleportTargetTo(new Vector3(centre.x, 1f, centre.z));
                WorldStreamingProfiler.EndSample(ProfilerStage.RingRefresh, refreshToken);

                if (target.Value != _lastTarget)
                {
                    _result.Transitions++;
                    _lastTarget = target.Value;
                }
            }

            _result.ChunkAssignments += TotalAssignments() - assignBefore;
        }

        private ChunkCoord _lastTarget = new ChunkCoord(int.MinValue, int.MinValue);

        private int TotalAssignments()
        {
            int total = 0;
            IReadOnlyList<ChunkInstance> instances = _manager.Pool.All;
            for (int i = 0; i < instances.Count; i++) total += instances[i].AssignmentCount;
            return total;
        }

        private void BeginMeasuredWindow()
        {
            _gen0Baseline = GC.CollectionCount(0);
            _gen1Baseline = GC.CollectionCount(1);
            _assignmentsBaseline = TotalAssignments();
            _startTimestamp = WorldStreamingProfiler.RawTimestamp();
            RunAllocationControl();
            _lastTarget = new ChunkCoord(int.MinValue, int.MinValue);

            // Không dùng mốc trừ: bảo bộ lập lịch tự về 0 rồi đọc thẳng. Cửa sổ đo bắt đầu ở trạng thái
            // đã dựng xong (trạm Settle vừa chạy trước đó) nên số 0 ở đây là số 0 thật.
            // Không dùng mốc trừ: bảo bộ lập lịch tự về 0 rồi đọc thẳng. Cửa sổ đo bắt đầu ở trạng thái
            // đã dựng xong (trạm Settle vừa chạy trước đó) nên số 0 ở đây là số 0 thật.
            _manager.DecorationScheduler.ResetCounters();
            _lastCompletedTotal = 0;
            _windowCompleted = 0;
            _windowCancelled = 0;
            _resumeCompletedBaseline = 0;
            _resumeCancelledBaseline = 0;
            _result.MaxDecorationQueueDepth = _manager.PendingDecorationCount;

            WorldStreamingProfiler.Begin(CapacityFor(Scenario));
        }

        private void ResumeMeasuredWindow()
        {
            _resumeCompletedBaseline = _manager.CompletedDecorationJobs;
            _resumeCancelledBaseline = _manager.CancelledStaleDecorationJobs;
            _lastCompletedTotal = _resumeCompletedBaseline;
            WorldStreamingProfiler.Resume();
        }

        private void EndMeasuredWindow()
        {
            _windowCompleted += _manager.CompletedDecorationJobs - _resumeCompletedBaseline;
            _windowCancelled += _manager.CancelledStaleDecorationJobs - _resumeCancelledBaseline;
            WorldStreamingProfiler.Stop();
        }

        private void Complete()
        {
            _result.WallClockMs = WorldStreamingProfiler.RawElapsedMs(_startTimestamp);
            _result.GcGen0Collections = GC.CollectionCount(0) - _gen0Baseline;
            _result.GcGen1Collections = GC.CollectionCount(1) - _gen1Baseline;
            _result.FramesObserved = _result.DistinctFrames;

            _result.DecorationJobsCompleted = _windowCompleted;
            _result.DecorationJobsCancelled = _windowCancelled;
            _result.DecorationJobsQueued = _windowCompleted + _windowCancelled + _manager.PendingDecorationCount;
            _result.FramesToSettleAfterRoute = _routeDrainFrames;

            _frameMs.Sort();
            if (_frameMs.Count > 0)
            {
                _result.FrameMsP50 = _frameMs[Mathf.Clamp((int)(_frameMs.Count * 0.5f), 0, _frameMs.Count - 1)];
                _result.FrameMsP95 = _frameMs[Mathf.Clamp((int)(_frameMs.Count * 0.95f), 0, _frameMs.Count - 1)];
                _result.FrameMsMax = _frameMs[_frameMs.Count - 1];
            }

            // Chốt số liệu TRƯỚC khi nhả bộ đệm, để kết quả không còn phụ thuộc bộ đệm dùng chung.
            _result.FinalizeTimings();
            _result.CaptureStreamingState(_manager);

            WorldStreamingProfiler.Stop();
            DisposeRecorder();
            IsRunning = false;
            _phase = Phase.Done;
        }
    }

    /// <summary>
    /// Microbenchmark ĐỒNG BỘ: chạy trọn trong một tick, không có frame thật.
    ///
    /// Giữ lại vì nó tiện cho test tất định và cho so sánh CPU trước/sau. Kết quả của nó KHÔNG được
    /// dùng làm số liệu chính thức về frame hay về cấp phát lúc nhàn rỗi — chính vì thế nó tự gắn nhãn
    /// <see cref="BenchmarkMode.SynchronousMicrobenchmark"/>.
    /// </summary>
    public static class WorldStreamingMicrobenchmark
    {
        private static readonly List<ChunkCoord> Route = new List<ChunkCoord>(2048);

        public static WorldStreamingBenchmarkResult Run(WorldStreamManager manager, BenchmarkScenario scenario,
            string environmentLabel)
        {
            if (manager == null || !manager.IsInitialized)
                throw new InvalidOperationException("[WorldStreaming] Manager chưa khởi tạo.");

            WorldStreamingBenchmarkRoute.Build(scenario, Route);

            float chunkSize = manager.Config.ChunkSize;
            var result = new WorldStreamingBenchmarkResult(scenario, BenchmarkMode.SynchronousMicrobenchmark, environmentLabel);

            Vector3 checkpoint = WorldStreamingBenchmarkRoute.Checkpoint.ToWorldCenter(chunkSize);
            manager.TeleportTargetTo(new Vector3(checkpoint.x, 1f, checkpoint.z));
            manager.DrainDecoration();
            result.CheckpointWarmup = OwnedResourceSnapshot.Capture(manager);
            result.ConfiguredJobsPerFrame = manager.Config.DecorationJobsPerFrame;

            int gen0 = GC.CollectionCount(0);
            // Microbenchmark chay tron trong mot tick nen khong co bo dem cap phat theo frame nao
            // dung duoc. No khong bao cao cap phat — va noi thang nhu vay.
            long monoBefore = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();

            WorldStreamingProfiler.Begin(scenario == BenchmarkScenario.Soak ? 32768 : 8192);
            long start = WorldStreamingProfiler.RawTimestamp();

            try
            {
                if (scenario == BenchmarkScenario.SteadyState)
                {
                    for (int i = 0; i < 240; i++) manager.RefreshNow();
                }
                else
                {
                    var previous = new ChunkCoord(int.MinValue, int.MinValue);
                    for (int i = 0; i < Route.Count; i++)
                    {
                        Vector3 centre = Route[i].ToWorldCenter(chunkSize);
                        long token = WorldStreamingProfiler.BeginSample();
                        manager.TeleportTargetTo(new Vector3(centre.x, 1f, centre.z));
                        WorldStreamingProfiler.EndSample(ProfilerStage.RingRefresh, token);

                        // Microbenchmark khong co frame nao troi qua, nen bo lap lich se khong bao gio
                        // duoc goi. Vet can hang doi ngay tai day de no van do TRON khoi cong viec —
                        // dung muc dich cua no la so sanh CPU dong bo truoc/sau.
                        manager.DrainDecoration();

                        if (Route[i] != previous) result.Transitions++;
                        previous = Route[i];
                    }
                }

                result.WallClockMs = WorldStreamingProfiler.RawElapsedMs(start);
                result.AllocatedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - monoBefore;
                result.AllocationSamples = 1;
                result.AllocationCounterVerified = false;
                result.GcGen0Collections = GC.CollectionCount(0) - gen0;
            }
            finally
            {
                WorldStreamingProfiler.Stop();
            }

            manager.TeleportTargetTo(new Vector3(checkpoint.x, 1f, checkpoint.z));
            manager.DrainDecoration();
            result.CheckpointMid = OwnedResourceSnapshot.Capture(manager);
            result.CheckpointEnd = result.CheckpointMid;
            result.SettledAtFinalCheckpoint = manager.IsGenerationSettled;
            result.DecorationJobsCompleted = manager.CompletedDecorationJobs;
            result.DecorationJobsCancelled = manager.CancelledStaleDecorationJobs;
            result.MaxDecorationQueueDepth = manager.MaxObservedDecorationQueueDepth;

            result.FinalizeTimings();
            result.CaptureStreamingState(manager);
            return result;
        }
    }
}
