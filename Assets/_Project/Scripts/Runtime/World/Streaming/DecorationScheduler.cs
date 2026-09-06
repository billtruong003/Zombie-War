using System.Collections.Generic;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Hàng đợi sinh trang trí có giới hạn, ưu tiên chunk gần người chơi trước.
    ///
    /// Vì sao cần: M3B.1 đã kéo chi phí MỘT chunk xuống mức chấp nhận được (decoration build p95
    /// 2,422 ms), nhưng một lần teleport vẫn đòi dựng lại cả 25 chunk TRONG CÙNG MỘT KHUNG HÌNH. Vấn đề
    /// còn lại không phải chi phí mỗi chunk mà là số chunk gộp vào một frame.
    ///
    /// Cách lưu trữ là điểm mấu chốt: hàng đợi KHÔNG phải danh sách động mà là một mảng cố định đánh
    /// chỉ số theo slot pool. Mỗi slot giữ nhiều nhất một việc, nên độ sâu hàng đợi bị chặn cứng bằng
    /// sức chứa pool, không bao giờ cấp phát lại, và việc "một toạ độ mới thay chỗ toạ độ cũ trên cùng
    /// slot" trở thành phép ghi đè một ô — không cần tìm rồi xoá.
    ///
    /// Bộ lập lịch KHÔNG quyết định nội dung thế giới. Nó chỉ quyết định THỜI ĐIỂM. Cùng một tập toạ
    /// độ, chạy theo thứ tự nào thì kết quả cuối cùng cũng phải giống hệt nhau.
    /// </summary>
    public sealed class DecorationScheduler
    {
        /// <summary>Một việc đang chờ, gắn với đúng một slot pool.</summary>
        private struct PendingJob
        {
            public ChunkInstance Chunk;
            public ChunkCoord Coord;

            /// <summary>Vé thế hệ của slot lúc xếp hàng. Khác đi nghĩa là slot đã bị gán lại.</summary>
            public int Ticket;

            /// <summary>Bậc mật độ mà việc này được xếp hàng để sinh ra.</summary>
            public DecorationDensityTier Tier;
            public bool Active;
        }

        private readonly PendingJob[] _jobs;

        public DecorationScheduler(int slotCapacity)
        {
            if (slotCapacity <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(slotCapacity));

            // Cấp phát MỘT lần lúc khởi tạo. Sau đây đường chạy nóng không tạo mảng, list hay closure nào.
            _jobs = new PendingJob[slotCapacity];
        }

        public int SlotCapacity => _jobs.Length;

        /// <summary>Số việc hợp lệ đang chờ.</summary>
        public int PendingCount { get; private set; }

        /// <summary>Tổng số việc đã hoàn thành và ĐÃ áp vào mesh.</summary>
        public int CompletedJobs { get; private set; }

        /// <summary>
        /// Tổng số việc bị bỏ vì toạ độ của nó đã rời ring hoặc slot đã bị gán lại.
        ///
        /// Đây KHÔNG phải nội dung thế giới đã sinh. Báo cáo phải tách bạch hai con số này, nếu không
        /// một tuyến chạy nhanh hơn tốc độ sinh sẽ trông như đã dựng nhiều hơn thực tế.
        /// </summary>
        public int CancelledStaleJobs { get; private set; }

        /// <summary>Độ sâu hàng đợi lớn nhất từng quan sát được.</summary>
        public int MaxObservedQueueDepth { get; private set; }

        /// <summary>Số việc hoàn thành trong lần <see cref="Process"/> gần nhất.</summary>
        public int LastProcessedCount { get; private set; }

        public void ResetCounters()
        {
            CompletedJobs = 0;
            CancelledStaleJobs = 0;
            MaxObservedQueueDepth = PendingCount;
            LastProcessedCount = 0;
        }

        /// <summary>Xoá sạch hàng đợi. Việc đang chờ bị tính là huỷ, không phải hoàn thành.</summary>
        public void Clear()
        {
            for (int i = 0; i < _jobs.Length; i++)
            {
                if (_jobs[i].Active) CancelledStaleJobs++;
                _jobs[i] = default;
            }

            PendingCount = 0;
            LastProcessedCount = 0;
        }

        /// <summary>
        /// Xếp hàng việc sinh trang trí cho một slot vừa được gán toạ độ.
        ///
        /// Ghi đè thẳng lên ô của slot: nếu slot đó còn việc cũ chưa làm thì việc cũ bị huỷ ngay tại
        /// đây, chứ không nằm lại chờ bị phát hiện lúc áp kết quả.
        /// </summary>
        public void Enqueue(ChunkInstance chunk)
        {
            if (chunk == null) return;

            int slot = chunk.SlotId;
            if (slot < 0 || slot >= _jobs.Length)
                throw new System.ArgumentOutOfRangeException(nameof(chunk),
                    $"[WorldStreaming] SlotId {slot} nằm ngoài sức chứa bộ lập lịch ({_jobs.Length}).");

            if (_jobs[slot].Active)
            {
                CancelledStaleJobs++;
                PendingCount--;
            }

            _jobs[slot].Chunk = chunk;
            _jobs[slot].Coord = chunk.Coord;
            _jobs[slot].Ticket = chunk.DecorationTicket;
            _jobs[slot].Tier = chunk.DecorationTier;
            _jobs[slot].Active = true;
            PendingCount++;

            if (PendingCount > MaxObservedQueueDepth) MaxObservedQueueDepth = PendingCount;
        }

        /// <summary>Bỏ việc của một slot sắp bị trả về pool. Toạ độ đó không còn trong ring nữa.</summary>
        public void CancelFor(ChunkInstance chunk)
        {
            if (chunk == null) return;

            int slot = chunk.SlotId;
            if (slot < 0 || slot >= _jobs.Length || !_jobs[slot].Active) return;

            _jobs[slot] = default;
            PendingCount--;
            CancelledStaleJobs++;
        }

        /// <summary>
        /// Làm tối đa <paramref name="budget"/> việc, gần <paramref name="origin"/> trước.
        /// Trả về số việc THẬT SỰ hoàn thành — việc bị huỷ không được tính.
        /// </summary>
        public int Process(ChunkCoord origin, int budget)
        {
            LastProcessedCount = 0;
            if (budget <= 0 || PendingCount == 0) return 0;

            int done = 0;
            while (done < budget)
            {
                int slot = SelectNext(origin);
                if (slot < 0) break;

                PendingJob job = _jobs[slot];
                _jobs[slot] = default;
                PendingCount--;

                // Kiểm tra lần cuối ngay trước khi dựng. Ô hàng đợi đáng lẽ đã được dọn khi slot bị
                // trả hoặc gán lại, nhưng việc áp hình học vào sai toạ độ là hỏng không thể sửa được
                // nên chỗ này vẫn kiểm tra thêm một lần.
                if (job.Chunk == null || !job.Chunk.IsAssigned ||
                    job.Chunk.DecorationTicket != job.Ticket || job.Chunk.Coord != job.Coord ||
                    job.Chunk.DecorationTier != job.Tier)
                {
                    CancelledStaleJobs++;
                    continue;
                }

                job.Chunk.BuildDecorationNow();
                CompletedJobs++;
                done++;
            }

            LastProcessedCount = done;
            return done;
        }

        /// <summary>
        /// Chọn việc đứng đầu theo khoảng cách Chebyshev tới gốc, tức là theo đúng vòng ring.
        ///
        /// Chọn Chebyshev chứ không phải Euclid vì ring của hệ này là hình vuông: mọi chunk có
        /// <c>max(|dx|,|dz|) == 1</c> đều là hàng xóm trực tiếp và đáng được ưu tiên như nhau. Sau đó
        /// mới tách bằng khoảng cách bình phương để trong cùng một vòng thì chunk thẳng trục đi trước
        /// chunk ở góc, rồi tới Z, X và cuối cùng là slot id — chuỗi tách này không bao giờ hoà, nên
        /// thứ tự luôn tái lập được.
        ///
        /// Quét tuyến tính chứ không sắp xếp: mảng chỉ có 25 ô, và quét lại mỗi lần lấy việc cho phép
        /// độ ưu tiên bám theo gốc HIỆN TẠI khi người chơi vừa di chuyển giữa chừng.
        /// </summary>
        private int SelectNext(ChunkCoord origin)
        {
            int best = -1;
            int bestRing = int.MaxValue;
            int bestSquared = int.MaxValue;
            int bestZ = int.MaxValue;
            int bestX = int.MaxValue;

            for (int i = 0; i < _jobs.Length; i++)
            {
                if (!_jobs[i].Active) continue;

                ChunkCoord coord = _jobs[i].Coord;
                int dx = coord.X - origin.X;
                int dz = coord.Z - origin.Z;
                int ring = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz));
                int squared = dx * dx + dz * dz;

                if (ring > bestRing) continue;
                if (ring == bestRing)
                {
                    if (squared > bestSquared) continue;
                    if (squared == bestSquared)
                    {
                        if (coord.Z > bestZ) continue;
                        if (coord.Z == bestZ && coord.X >= bestX) continue;
                    }
                }

                best = i;
                bestRing = ring;
                bestSquared = squared;
                bestZ = coord.Z;
                bestX = coord.X;
            }

            return best;
        }

        /// <summary>Chép các toạ độ đang chờ ra ngoài — dùng cho test và debug, không dùng khi chạy.</summary>
        public void CopyPendingCoords(List<ChunkCoord> results)
        {
            if (results == null) return;

            results.Clear();
            for (int i = 0; i < _jobs.Length; i++)
                if (_jobs[i].Active) results.Add(_jobs[i].Coord);
        }
    }
}
