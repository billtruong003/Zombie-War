using System;

namespace ZombieWar.WorldStreaming
{
    /// <summary>Các chặng công việc được đo riêng trong một lần gán chunk.</summary>
    public enum ProfilerStage
    {
        GroundBiome = 0,
        DecorationSample = 1,
        DecorationBuild = 2,
        SolidApply = 3,
        FoliageApply = 4,
        ChunkAssign = 5,
        RingRefresh = 6,

        /// <summary>
        /// Trọn một việc sinh trang trí do bộ lập lịch chạy, kể từ M3B.2.
        ///
        /// Tách khỏi <see cref="ChunkAssign"/> là bắt buộc chứ không phải cho đẹp: từ M3B.2, ChunkAssign
        /// chỉ còn đo phần làm ngay (đặt toạ độ + mặt đất), còn phần trang trí xảy ra ở frame khác. Gộp
        /// hai thứ vào một dãy sẽ tạo ra một con số không mô tả bất kỳ khối công việc có thật nào.
        /// </summary>
        DecorationJob = 7,
    }

    /// <summary>
    /// Bản tóm tắt BẤT BIẾN của một dãy mẫu, chép ra tại thời điểm chốt kết quả.
    ///
    /// Có kiểu này vì bộ đệm mẫu là tài nguyên dùng chung: nếu kết quả cứ trỏ vào bộ đệm sống thì
    /// kịch bản chạy sau sẽ ghi đè con số của kịch bản chạy trước.
    /// </summary>
    public readonly struct TimingSummary
    {
        public readonly int Count;
        public readonly int Dropped;
        public readonly float Min;
        public readonly float P50;
        public readonly float P95;
        public readonly float Max;
        public readonly float Total;

        public TimingSummary(int count, int dropped, float min, float p50, float p95, float max, float total)
        {
            Count = count;
            Dropped = dropped;
            Min = min;
            P50 = p50;
            P95 = p95;
            Max = max;
            Total = total;
        }

        public float Mean => Count > 0 ? Total / Count : 0f;
    }

    /// <summary>
    /// Một dãy mẫu thời gian với vùng nhớ cấp phát sẵn. Không cấp phát gì sau khi khởi tạo.
    /// </summary>
    public sealed class TimingSeries
    {
        private readonly float[] _samples;
        private readonly float[] _sortScratch;
        private int _count;
        private bool _sorted;

        public TimingSeries(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            _samples = new float[capacity];
            _sortScratch = new float[capacity];
        }

        public int Capacity => _samples.Length;
        public int Count => _count;
        public int Dropped { get; private set; }
        public float Total { get; private set; }
        public float Mean => _count > 0 ? Total / _count : 0f;

        public void Reset()
        {
            _count = 0;
            Dropped = 0;
            Total = 0f;
            _sorted = false;
        }

        public void Add(float milliseconds)
        {
            if (_count >= _samples.Length)
            {
                Dropped++;
                return;
            }

            _samples[_count++] = milliseconds;
            Total += milliseconds;
            _sorted = false;
        }

        private void EnsureSorted()
        {
            if (_sorted) return;

            Array.Copy(_samples, _sortScratch, _count);
            Array.Sort(_sortScratch, 0, _count);
            _sorted = true;
        }

        public float Min
        {
            get
            {
                if (_count == 0) return 0f;
                EnsureSorted();
                return _sortScratch[0];
            }
        }

        public float Max
        {
            get
            {
                if (_count == 0) return 0f;
                EnsureSorted();
                return _sortScratch[_count - 1];
            }
        }

        /// <summary>Phân vị theo hạng gần nhất — giá trị trả về luôn là một mẫu có thật.</summary>
        public float Percentile(float fraction)
        {
            if (_count == 0) return 0f;
            if (fraction <= 0f) return Min;
            if (fraction >= 1f) return Max;

            EnsureSorted();
            int rank = (int)Math.Ceiling(fraction * _count);
            return _sortScratch[Math.Clamp(rank - 1, 0, _count - 1)];
        }

        public float P50 => Percentile(0.5f);
        public float P95 => Percentile(0.95f);

        public TimingSummary Summarize() => new TimingSummary(_count, Dropped, Min, P50, P95, Max, Total);
    }

    /// <summary>
    /// Bộ đo thời gian cho world streaming.
    ///
    /// Hai điều được sửa ở M3A.1 sau khi soi lại chính bộ đo:
    ///
    /// 1. Bộ đệm mẫu chỉ được cấp phát khi <see cref="Begin"/> được gọi. Trước đó không có mảng nào
    ///    tồn tại. Bản M3A cấp phát trong static constructor, nên chỉ cần chunk đầu tiên chạm vào
    ///    lớp này là ~0.44 MB bộ đệm nằm lại trong bộ nhớ dù không ai bật profiling.
    ///
    /// 2. Khi đang TẮT, <see cref="BeginSample"/> trả về token rỗng mà KHÔNG đọc đồng hồ, và
    ///    <see cref="EndSample"/> thoát ngay khi thấy token rỗng. Bản M3A gọi
    ///    `Stopwatch.GetTimestamp()` vô điều kiện nhiều lần mỗi lần gán chunk, nên câu "chỉ tốn một
    ///    lần đọc bool" trong báo cáo cũ là sai.
    ///
    /// <see cref="ClockReads"/> tồn tại để test chứng minh điều (2) chứ không phải để tin lời hứa.
    /// </summary>
    public static class WorldStreamingProfiler
    {
        public const int StageCount = 8;
        public const int DefaultCapacity = 8192;

        /// <summary>Token báo "không đo". Stopwatch.GetTimestamp() không bao giờ trả 0 trong thực tế.</summary>
        public const long InvalidToken = 0L;

        private static readonly float TicksToMs = 1000f / System.Diagnostics.Stopwatch.Frequency;

        private static TimingSeries[] _series;

        public static bool IsCapturing { get; private set; }

        /// <summary>Bộ đệm mẫu có đang tồn tại hay không. Test dùng để chứng minh cấp phát là lười.</summary>
        public static bool HasCaptureBuffers => _series != null;

        public static int Capacity => _series != null && _series.Length > 0 ? _series[0].Capacity : 0;

        /// <summary>
        /// Số lần bộ đo thật sự đọc đồng hồ. Chỉ dùng cho test: nếu con số này tăng trong khi
        /// profiler đang tắt thì lời hứa "không chạm đồng hồ" đã bị vi phạm.
        /// </summary>
        public static long ClockReads { get; private set; }

        public static void ResetClockReadCounter() => ClockReads = 0;

        public static TimingSeries Stage(ProfilerStage stage) =>
            _series != null ? _series[(int)stage] : null;

        public static TimingSummary Summarize(ProfilerStage stage) =>
            _series != null ? _series[(int)stage].Summarize() : default;

        /// <summary>Cấp phát bộ đệm (nếu cần) và bắt đầu ghi.</summary>
        public static void Begin(int capacity = DefaultCapacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            if (_series == null || _series.Length != StageCount || _series[0].Capacity != capacity)
            {
                _series = new TimingSeries[StageCount];
                for (int i = 0; i < StageCount; i++) _series[i] = new TimingSeries(capacity);
            }
            else
            {
                ResetSamples();
            }

            IsCapturing = true;
        }

        public static void Stop() => IsCapturing = false;

        /// <summary>
        /// Tiếp tục ghi vào bộ đệm hiện có mà không xoá các mẫu đã thu.
        /// Dùng khi benchmark tạm dừng capture để chụp một checkpoint ngoài cửa sổ đo.
        /// </summary>
        public static void Resume()
        {
            if (_series == null)
                throw new InvalidOperationException("[WorldStreaming] Cannot resume profiling before Begin().");

            IsCapturing = true;
        }

        public static void ResetSamples()
        {
            if (_series == null) return;
            for (int i = 0; i < StageCount; i++) _series[i].Reset();
        }

        /// <summary>Dừng ghi VÀ trả lại bộ đệm. Sau lời gọi này profiler không giữ bộ nhớ nào.</summary>
        public static void Release()
        {
            IsCapturing = false;
            _series = null;
        }

        /// <summary>
        /// Mở một phép đo. Khi profiler tắt: trả token rỗng, KHÔNG đọc đồng hồ, không chạm bộ đệm.
        /// </summary>
        public static long BeginSample()
        {
            if (!IsCapturing) return InvalidToken;

            ClockReads++;
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Đóng phép đo và ghi mẫu. Trả về số mili-giây đã đo, hoặc 0 nếu phép đo không hoạt động.
        /// </summary>
        public static float EndSample(ProfilerStage stage, long token)
        {
            if (token == InvalidToken || !IsCapturing || _series == null) return 0f;

            ClockReads++;
            float ms = (System.Diagnostics.Stopwatch.GetTimestamp() - token) * TicksToMs;
            _series[(int)stage].Add(ms);
            return ms;
        }

        /// <summary>
        /// Đọc đồng hồ trực tiếp. CHỈ dành cho bộ điều khiển benchmark khi nó đang chủ động chạy —
        /// không được dùng ở các điểm đo trong đường chạy bình thường.
        /// </summary>
        public static long RawTimestamp()
        {
            ClockReads++;
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        public static float RawElapsedMs(long startTimestamp)
        {
            ClockReads++;
            return (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * TicksToMs;
        }

        public static int TotalSamples()
        {
            if (_series == null) return 0;

            int total = 0;
            for (int i = 0; i < StageCount; i++) total += _series[i].Count;
            return total;
        }
    }
}
