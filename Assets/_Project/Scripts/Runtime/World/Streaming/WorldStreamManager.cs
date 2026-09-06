using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Quyết định chunk nào PHẢI đang hiển thị và slot pool nào giữ chunk đó.
    ///
    /// Toàn bộ logic làm việc trên TẬP HỢP toạ độ, không phải trên bước di chuyển — nên teleport
    /// nhảy hàng trăm chunk và bước đi một ô đều đi qua đúng một đường mã, trong đúng một lần refresh.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldStreamManager : MonoBehaviour
    {
        [SerializeField] private WorldStreamingConfig config;
        [SerializeField] private ChunkPool pool;
        [SerializeField] private SharedGameplaySurface surface;
        [SerializeField] private Transform streamingTarget;

        [Tooltip("Tự khởi tạo ở Start. Test tắt cờ này để tự điều khiển thời điểm.")]
        [SerializeField] private bool initializeOnStart = true;

        private readonly Dictionary<ChunkCoord, ChunkInstance> _active = new Dictionary<ChunkCoord, ChunkInstance>();
        private readonly List<ChunkCoord> _required = new List<ChunkCoord>();
        private readonly HashSet<ChunkCoord> _requiredSet = new HashSet<ChunkCoord>();
        private readonly List<ChunkCoord> _outgoing = new List<ChunkCoord>();

        public bool IsInitialized { get; private set; }
        public WorldStreamingConfig Config => config;
        public ChunkPool Pool => pool;
        public SharedGameplaySurface Surface => surface;
        public Transform StreamingTarget => streamingTarget;

        /// <summary>
        /// Chunk logic mà NGƯỜI CHƠI đang đứng, và cũng là TÂM của ring 5×5 đang hiển thị.
        ///
        /// R2 từng tách tâm ring thành một toạ độ riêng biết nhìn theo camera, để chữa lỗi mất mặt
        /// đất. Chủ dự án sau đó tái hiện được nguyên nhân thật — Occlusion Culling loại nhầm renderer
        /// của chunk tái dụng — nên toàn bộ tầng đó đã được gỡ: nó chữa một bệnh không tồn tại và
        /// khiến cùng một câu hỏi có hai lời đáp. Ring bám người chơi, đúng như trước R2.
        /// </summary>
        public ChunkCoord CurrentChunk { get; private set; }

        /// <summary>Tên nói rõ nghĩa của <see cref="CurrentChunk"/>. Cùng một giá trị.</summary>
        public ChunkCoord PlayerChunk => CurrentChunk;

        public int ActiveLeaseCount => _active.Count;
        public IReadOnlyDictionary<ChunkCoord, ChunkInstance> ActiveLeases => _active;

        /// <summary>Tập bắt buộc của lần refresh gần nhất, giữ nguyên thứ tự ổn định.</summary>
        public IReadOnlyList<ChunkCoord> RequiredCoords => _required;

        public int RefreshCount { get; private set; }

        private DecorationScheduler _scheduler;

        /// <summary>Bộ lập lịch sinh trang trí. Có sau <see cref="Initialize"/>.</summary>
        public DecorationScheduler DecorationScheduler => _scheduler;

        public int PendingDecorationCount => _scheduler != null ? _scheduler.PendingCount : 0;
        public int CompletedDecorationJobs => _scheduler != null ? _scheduler.CompletedJobs : 0;
        public int CancelledStaleDecorationJobs => _scheduler != null ? _scheduler.CancelledStaleJobs : 0;
        public int MaxObservedDecorationQueueDepth => _scheduler != null ? _scheduler.MaxObservedQueueDepth : 0;
        public int LastFrameCompletedDecorationJobs => _scheduler != null ? _scheduler.LastProcessedCount : 0;

        /// <summary>
        /// Thế giới đã dựng xong hay chưa.
        ///
        /// Câu trả lời lấy từ CHÍNH các chunk đang active chứ không từ số đếm hàng đợi. Hàng đợi rỗng
        /// chưa chắc là xong: nếu một việc bị huỷ vì hết hạn mà toạ độ của nó vẫn nằm trong ring thì
        /// hàng đợi rỗng nhưng thế giới vẫn thiếu cây. Hỏi thẳng từng chunk thì không có kẽ hở đó.
        /// </summary>
        public bool IsGenerationSettled
        {
            get
            {
                foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in _active)
                    if (lease.Value.DecorationPending) return false;

                return true;
            }
        }

        /// <summary>Số toạ độ được cấp slot mới trong lần refresh gần nhất — dùng để soi teleport.</summary>
        public int LastIncomingCount { get; private set; }

        /// <summary>Số toạ độ bị trả slot trong lần refresh gần nhất.</summary>
        public int LastOutgoingCount { get; private set; }

        public void SetConfig(WorldStreamingConfig value) => config = value;

        public void SetPool(ChunkPool value) => pool = value;

        public void SetSurface(SharedGameplaySurface value) => surface = value;

        public void SetTarget(Transform value) => streamingTarget = value;

        public void SetInitializeOnStart(bool value) => initializeOnStart = value;

        private void Start()
        {
            if (initializeOnStart) Initialize();
        }

        /// <summary>Validate cấu hình, prewarm pool và dựng ring đầu tiên.</summary>
        public void Initialize()
        {
            if (IsInitialized) return;

            if (config == null)
                throw new System.InvalidOperationException("[WorldStreaming] Chưa gán WorldStreamingConfig.");
            if (pool == null)
                throw new System.InvalidOperationException("[WorldStreaming] Chưa gán ChunkPool.");
            if (!config.Validate(out string error))
                throw new System.InvalidOperationException($"[WorldStreaming] Cấu hình không hợp lệ: {error}");

            pool.Prewarm(config);

            if (surface != null) surface.Configure(config);

            // Cấp phát hàng đợi đúng một lần, đúng bằng số slot. Sau đây không còn lần cấp phát nào.
            _scheduler = new DecorationScheduler(pool.Capacity);

            IsInitialized = true;
            RefreshNow();
        }

        private void LateUpdate()
        {
            if (!IsInitialized) return;

            if (streamingTarget != null)
            {
                ChunkCoord coord = ChunkCoord.FromWorld(streamingTarget.position, config.ChunkSize);
                if (coord != CurrentChunk) RefreshNow();
            }

            // Chạy MỌI frame, không chỉ frame có refresh: sau một lần teleport, hàng đợi còn 25 việc
            // mà không có refresh nào nữa để kéo chúng đi.
            ProcessDecorationJobs(config.DecorationJobsPerFrame);
        }

        /// <summary>
        /// Chạy tối đa <paramref name="maxJobs"/> việc sinh trang trí, gần trước xa sau.
        ///
        /// Công khai vì đây là bước đi thật của bộ lập lịch, không phải cửa hậu cho test: vòng lặp game
        /// gọi nó mỗi frame với ngân sách trong config, còn test gọi nó để bước từng nhịp một cách tất
        /// định thay vì phải chờ frame trôi.
        /// </summary>
        public int ProcessDecorationJobs(int maxJobs)
        {
            if (!IsInitialized || _scheduler == null) return 0;

            return _scheduler.Process(CurrentChunk, maxJobs);
        }

        /// <summary>
        /// Chạy tới khi thế giới dựng xong. Trả về số nhịp đã chạy.
        ///
        /// <paramref name="maxIterations"/> là chốt chặn chống treo: nếu chạm trần thì có lỗi thật sự
        /// trong bộ lập lịch, và người gọi cần biết điều đó thay vì lặp vô hạn.
        /// </summary>
        public int DrainDecoration(int maxIterations = 256)
        {
            if (!IsInitialized) return 0;

            int iterations = 0;
            while (!IsGenerationSettled && iterations < maxIterations)
            {
                iterations++;
                if (ProcessDecorationJobs(config.DecorationJobsPerFrame) == 0) break;
            }

            return iterations;
        }

        /// <summary>
        /// Đồng bộ lại ring ngay lập tức theo vị trí hiện tại của mục tiêu.
        ///
        /// Thứ tự cố ý: trả hết slot ra ngoài TRƯỚC rồi mới cấp cho toạ độ mới. Nếu làm ngược lại,
        /// một teleport không giao nhau sẽ đòi 50 slot trong khi pool chỉ có 25.
        /// </summary>
        public void RefreshNow()
        {
            if (!IsInitialized)
                throw new System.InvalidOperationException("[WorldStreaming] Chưa Initialize().");

            float chunkSize = config.ChunkSize;
            CurrentChunk = streamingTarget != null
                ? ChunkCoord.FromWorld(streamingTarget.position, chunkSize)
                : ChunkCoord.Zero;

            ChunkCoord.EnumerateRing(CurrentChunk, config.RenderRadius, _required);

            _requiredSet.Clear();
            for (int i = 0; i < _required.Count; i++) _requiredSet.Add(_required[i]);

            _outgoing.Clear();
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in _active)
            {
                if (!_requiredSet.Contains(lease.Key)) _outgoing.Add(lease.Key);
            }

            for (int i = 0; i < _outgoing.Count; i++)
            {
                ChunkInstance leaving = _active[_outgoing[i]];

                // Bỏ việc TRƯỚC khi trả slot: toạ độ này đã rời ring nên việc của nó không còn là nội
                // dung thế giới nữa, và phải được ghi nhận là huỷ chứ không lặng lẽ biến mất.
                _scheduler.CancelFor(leaving);
                pool.Release(leaving);
                _active.Remove(_outgoing[i]);
            }

            int incoming = 0;
            for (int i = 0; i < _required.Count; i++)
            {
                ChunkCoord coord = _required[i];
                if (_active.ContainsKey(coord)) continue;

                ChunkInstance instance = pool.Lease();

                // Mặt đất xong ngay trong lời gọi này; trang trí chỉ được xếp hàng.
                instance.AssignTo(coord, chunkSize, config.TierFor(coord, CurrentChunk));
                if (instance.DecorationPending) _scheduler.Enqueue(instance);

                _active.Add(coord, instance);
                incoming++;
            }

            RetierRetainedChunks();

            if (surface != null) surface.RecenterOn(CurrentChunk, chunkSize);

            LastOutgoingCount = _outgoing.Count;
            LastIncomingCount = incoming;
            RefreshCount++;
        }

        /// <summary>
        /// Cập nhật bậc mật độ cho những chunk VẪN Ở LẠI trong ring.
        ///
        /// Đây là ca dễ bỏ sót nhất của M3B.2D. Khi gốc dịch một ô, phần lớn chunk không vào cũng không
        /// ra — chúng giữ nguyên toạ độ và nguyên slot, nên vòng lặp "incoming" ở trên không đụng tới
        /// chúng. Nhưng khoảng cách của chúng tới người chơi vừa đổi, và một số vừa băng qua ranh giới
        /// gần/xa. Không có bước này thì chúng sẽ mắc kẹt vĩnh viễn ở mật độ cũ: vòng ngoài thưa trôi
        /// vào sát người chơi, còn vùng gần dày trôi ra rìa.
        ///
        /// Số lần lặp đúng bằng số chunk active (25) và không cấp phát gì.
        /// </summary>
        private void RetierRetainedChunks()
        {
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in _active)
            {
                DecorationDensityTier tier = config.TierFor(lease.Key, CurrentChunk);

                // Xếp hàng lại kể cả khi việc cũ còn đang chờ: Enqueue ghi đè đúng ô của slot đó, nên
                // không bao giờ có hai việc cho cùng một slot.
                if (lease.Value.RequestDecorationTier(tier)) _scheduler.Enqueue(lease.Value);
            }
        }

        /// <summary>Đặt mục tiêu tới vị trí mới rồi refresh ngay — đường dùng cho teleport và cho test.</summary>
        public void TeleportTargetTo(Vector3 worldPosition)
        {
            if (streamingTarget != null) streamingTarget.position = worldPosition;
            if (IsInitialized) RefreshNow();
        }

        /// <summary>Chép tập toạ độ đang active ra ngoài để test/debug soi mà không lộ dictionary nội bộ.</summary>
        public void CopyActiveCoords(List<ChunkCoord> results)
        {
            if (results == null) return;

            results.Clear();
            foreach (ChunkCoord coord in _active.Keys) results.Add(coord);
        }
    }
}
