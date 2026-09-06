using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode M3B.2: luật của bộ lập lịch trang trí, tách khỏi pool và khỏi Play Mode.
    ///
    /// Bộ test này nói về THỨ TỰ và về TÍNH HỢP LỆ, không nói về hiệu năng. Hai thứ dễ hỏng nhất khi
    /// sửa bộ lập lịch là: chunk xa được dựng trước chunk gần (người chơi thấy đất trống ngay dưới
    /// chân), và một việc cũ được áp lên slot đã đổi chủ (cây của toạ độ khác mọc lên nhầm chỗ).
    /// Cả hai đều không lộ ra trong ảnh chụp màn hình nên phải chặn bằng test.
    public class WorldStreamingSchedulerTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private WorldStreamingConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = WorldStreamingConfig.CreateRuntime();

            // Phải có palette thật: nếu không, chunk coi như không có việc gì để làm và
            // `DecorationPending` không bao giờ bật, khiến mọi khẳng định về trạng thái chờ trở nên rỗng.
            var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<DecorationPalette>(
                "Assets/_Project/Data/World/Decoration/DecorationPalette.asset");
            Assert.IsNotNull(palette, "Chưa bake palette. Chạy 'Rebuild Decoration Assets'.");
            _config.SetDecorationPalette(palette);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null) Object.DestroyImmediate(go);

            _spawned.Clear();
            if (_config != null) Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Một chunk trần dùng làm quân cờ: có SlotId, có Coord, có vé thế hệ — đủ để bộ lập lịch
        /// xếp thứ tự và kiểm tra hợp lệ, mà không cần mesh hay material nào.
        /// </summary>
        private ChunkInstance MakeChunk(int slotId, ChunkCoord coord)
        {
            ChunkInstance chunk = ChunkInstance.Create(slotId, null, _config, null, null, null);
            _spawned.Add(chunk.gameObject);
            chunk.AssignTo(coord, _config.ChunkSize);
            return chunk;
        }

        private static List<ChunkCoord> Pending(DecorationScheduler scheduler)
        {
            var list = new List<ChunkCoord>();
            scheduler.CopyPendingCoords(list);
            return list;
        }

        // --- Thứ tự gần trước ---------------------------------------------------------------------

        [Test]
        public void Process_CompletesNearestChunkFirst()
        {
            var scheduler = new DecorationScheduler(8);
            var origin = new ChunkCoord(10, 10);

            // Xếp hàng theo thứ tự XA TRƯỚC, để nếu bộ lập lịch chỉ chạy theo thứ tự nạp thì test đổ.
            ChunkInstance far = MakeChunk(0, new ChunkCoord(12, 12));
            ChunkInstance mid = MakeChunk(1, new ChunkCoord(11, 10));
            ChunkInstance near = MakeChunk(2, new ChunkCoord(10, 10));

            scheduler.Enqueue(far);
            scheduler.Enqueue(mid);
            scheduler.Enqueue(near);

            scheduler.Process(origin, 1);
            Assert.IsFalse(near.DecorationPending, "Chunk ngay tại gốc phải được dựng đầu tiên.");
            Assert.IsTrue(mid.DecorationPending);
            Assert.IsTrue(far.DecorationPending);

            scheduler.Process(origin, 1);
            Assert.IsFalse(mid.DecorationPending, "Vòng 1 phải xong trước vòng 2.");
            Assert.IsTrue(far.DecorationPending);

            scheduler.Process(origin, 1);
            Assert.IsFalse(far.DecorationPending);
        }

        [Test]
        public void Process_UsesChebyshevRing_NotEuclideanDistance()
        {
            var scheduler = new DecorationScheduler(8);
            var origin = ChunkCoord.Zero;

            // (2,0) xa hơn (1,1) theo Euclid (2.00 so với 1.41) nhưng cùng thuộc vòng ngoài theo
            // Chebyshev... khong: (2,0) ring=2, (1,1) ring=1. Nen (1,1) phai di truoc.
            ChunkInstance axis = MakeChunk(0, new ChunkCoord(2, 0));
            ChunkInstance diagonal = MakeChunk(1, new ChunkCoord(1, 1));

            scheduler.Enqueue(axis);
            scheduler.Enqueue(diagonal);

            scheduler.Process(origin, 1);
            Assert.IsFalse(diagonal.DecorationPending, "Vòng Chebyshev 1 phải đi trước vòng 2.");
            Assert.IsTrue(axis.DecorationPending);
        }

        [Test]
        public void Process_WithinSameRing_PrefersAxisOverCorner()
        {
            var scheduler = new DecorationScheduler(8);
            var origin = ChunkCoord.Zero;

            ChunkInstance corner = MakeChunk(0, new ChunkCoord(1, 1));
            ChunkInstance axis = MakeChunk(1, new ChunkCoord(1, 0));

            scheduler.Enqueue(corner);
            scheduler.Enqueue(axis);

            scheduler.Process(origin, 1);
            Assert.IsFalse(axis.DecorationPending, "Cùng vòng thì khoảng cách bình phương tách tiếp.");
            Assert.IsTrue(corner.DecorationPending);
        }

        [Test]
        public void Process_TieBreak_IsStableByCoordinateThenSlot()
        {
            // Bốn toạ độ cách gốc y hệt nhau theo cả Chebyshev lẫn Euclid. Thứ tự phải do toạ độ quyết
            // định (Z rồi X), không do slot id hay thứ tự nạp — nếu không, cùng một thế giới sẽ dựng
            // theo thứ tự khác nhau giữa hai lần chạy.
            var scheduler = new DecorationScheduler(8);
            var origin = ChunkCoord.Zero;

            ChunkInstance a = MakeChunk(3, new ChunkCoord(1, 1));
            ChunkInstance b = MakeChunk(2, new ChunkCoord(-1, 1));
            ChunkInstance c = MakeChunk(1, new ChunkCoord(1, -1));
            ChunkInstance d = MakeChunk(0, new ChunkCoord(-1, -1));

            scheduler.Enqueue(a);
            scheduler.Enqueue(b);
            scheduler.Enqueue(c);
            scheduler.Enqueue(d);

            var order = new List<ChunkCoord>();
            for (int i = 0; i < 4; i++)
            {
                List<ChunkCoord> before = Pending(scheduler);
                scheduler.Process(origin, 1);
                List<ChunkCoord> after = Pending(scheduler);
                foreach (ChunkCoord coord in before)
                    if (!after.Contains(coord)) order.Add(coord);
            }

            Assert.AreEqual(new ChunkCoord(-1, -1), order[0]);
            Assert.AreEqual(new ChunkCoord(1, -1), order[1]);
            Assert.AreEqual(new ChunkCoord(-1, 1), order[2]);
            Assert.AreEqual(new ChunkCoord(1, 1), order[3]);
        }

        [Test]
        public void Process_ReprioritisesWhenOriginMoves()
        {
            var scheduler = new DecorationScheduler(8);

            ChunkInstance left = MakeChunk(0, new ChunkCoord(-5, 0));
            ChunkInstance right = MakeChunk(1, new ChunkCoord(5, 0));
            scheduler.Enqueue(left);
            scheduler.Enqueue(right);

            // Gốc nằm bên phải: chunk phải phải đi trước, dù nó được nạp sau.
            scheduler.Process(new ChunkCoord(4, 0), 1);
            Assert.IsFalse(right.DecorationPending);
            Assert.IsTrue(left.DecorationPending);
        }

        // --- Ngân sách ----------------------------------------------------------------------------

        [Test]
        public void Process_NeverExceedsBudget()
        {
            var scheduler = new DecorationScheduler(8);
            for (int i = 0; i < 6; i++) scheduler.Enqueue(MakeChunk(i, new ChunkCoord(i, 0)));

            Assert.AreEqual(1, scheduler.Process(ChunkCoord.Zero, 1));
            Assert.AreEqual(5, scheduler.PendingCount);

            Assert.AreEqual(3, scheduler.Process(ChunkCoord.Zero, 3));
            Assert.AreEqual(2, scheduler.PendingCount);
        }

        [Test]
        public void Process_WithNonPositiveBudget_DoesNothing()
        {
            var scheduler = new DecorationScheduler(4);
            scheduler.Enqueue(MakeChunk(0, ChunkCoord.Zero));

            Assert.AreEqual(0, scheduler.Process(ChunkCoord.Zero, 0));
            Assert.AreEqual(0, scheduler.Process(ChunkCoord.Zero, -3));
            Assert.AreEqual(1, scheduler.PendingCount);
        }

        [Test]
        public void Process_StopsWhenQueueEmpty_EvenWithLargeBudget()
        {
            var scheduler = new DecorationScheduler(8);
            scheduler.Enqueue(MakeChunk(0, ChunkCoord.Zero));
            scheduler.Enqueue(MakeChunk(1, new ChunkCoord(1, 0)));

            Assert.AreEqual(2, scheduler.Process(ChunkCoord.Zero, 999));
            Assert.AreEqual(0, scheduler.PendingCount);
            Assert.AreEqual(2, scheduler.CompletedJobs);
        }

        // --- Việc hết hạn -------------------------------------------------------------------------

        [Test]
        public void StaleJob_AfterReassignment_IsCancelledNotApplied()
        {
            var scheduler = new DecorationScheduler(4);
            ChunkInstance chunk = MakeChunk(0, new ChunkCoord(3, 3));
            scheduler.Enqueue(chunk);

            // Slot bị cấp cho toạ độ khác trước khi việc cũ tới lượt. Ô hàng đợi bị ghi đè, và việc cũ
            // phải biến mất — không được dựng cây của (3,3) lên (9,9).
            chunk.AssignTo(new ChunkCoord(9, 9), _config.ChunkSize);
            scheduler.Enqueue(chunk);

            Assert.AreEqual(1, scheduler.PendingCount, "Một slot chỉ được giữ một việc.");
            Assert.AreEqual(1, scheduler.CancelledStaleJobs);

            scheduler.Process(ChunkCoord.Zero, 4);
            Assert.AreEqual(1, scheduler.CompletedJobs);
            Assert.AreEqual(new ChunkCoord(9, 9), chunk.Coord);
        }

        [Test]
        public void StaleJob_AfterRelease_IsCancelledNotApplied()
        {
            var scheduler = new DecorationScheduler(4);
            ChunkInstance chunk = MakeChunk(0, new ChunkCoord(3, 3));
            scheduler.Enqueue(chunk);

            chunk.Release();

            Assert.AreEqual(1, scheduler.Process(ChunkCoord.Zero, 4) + scheduler.CancelledStaleJobs,
                "Việc của một slot đã trả phải bị huỷ chứ không chạy.");
            Assert.AreEqual(0, scheduler.CompletedJobs);
            Assert.AreEqual(1, scheduler.CancelledStaleJobs);
        }

        [Test]
        public void CancelFor_RemovesTheJobAndCountsIt()
        {
            var scheduler = new DecorationScheduler(4);
            ChunkInstance chunk = MakeChunk(0, new ChunkCoord(3, 3));
            scheduler.Enqueue(chunk);

            scheduler.CancelFor(chunk);

            Assert.AreEqual(0, scheduler.PendingCount);
            Assert.AreEqual(1, scheduler.CancelledStaleJobs);
            Assert.AreEqual(0, scheduler.Process(ChunkCoord.Zero, 4));
        }

        [Test]
        public void CancelledJobs_AreNotCountedAsCompleted()
        {
            var scheduler = new DecorationScheduler(4);
            ChunkInstance kept = MakeChunk(0, new ChunkCoord(1, 0));
            ChunkInstance dropped = MakeChunk(1, new ChunkCoord(2, 0));

            scheduler.Enqueue(kept);
            scheduler.Enqueue(dropped);
            dropped.Release();

            int done = scheduler.Process(ChunkCoord.Zero, 8);

            Assert.AreEqual(1, done, "Chỉ một việc thật sự dựng được.");
            Assert.AreEqual(1, scheduler.CompletedJobs);
            Assert.AreEqual(1, scheduler.CancelledStaleJobs);
        }

        [Test]
        public void Job_CannotCompleteTwice()
        {
            var scheduler = new DecorationScheduler(4);
            ChunkInstance chunk = MakeChunk(0, ChunkCoord.Zero);
            scheduler.Enqueue(chunk);

            Assert.AreEqual(1, scheduler.Process(ChunkCoord.Zero, 4));
            Assert.AreEqual(0, scheduler.Process(ChunkCoord.Zero, 4), "Việc đã xong không được chạy lại.");
            Assert.AreEqual(1, scheduler.CompletedJobs);
        }

        // --- Sức chứa và bộ đếm -------------------------------------------------------------------

        [Test]
        public void QueueDepth_IsBoundedBySlotCapacity()
        {
            var scheduler = new DecorationScheduler(4);

            // Nạp cùng một slot nhiều lần: độ sâu không được vượt quá số slot, vì mỗi slot chỉ có một ô.
            ChunkInstance chunk = MakeChunk(0, ChunkCoord.Zero);
            for (int i = 0; i < 20; i++)
            {
                chunk.AssignTo(new ChunkCoord(i, 0), _config.ChunkSize);
                scheduler.Enqueue(chunk);
            }

            Assert.AreEqual(1, scheduler.PendingCount);
            Assert.LessOrEqual(scheduler.MaxObservedQueueDepth, scheduler.SlotCapacity);
        }

        [Test]
        public void Enqueue_BeyondSlotCapacity_ThrowsLoudly()
        {
            var scheduler = new DecorationScheduler(2);
            ChunkInstance chunk = MakeChunk(5, ChunkCoord.Zero);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => scheduler.Enqueue(chunk));
        }

        [Test]
        public void Constructor_RejectsNonPositiveCapacity()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DecorationScheduler(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DecorationScheduler(-4));
        }

        [Test]
        public void Clear_CountsPendingWorkAsCancelled()
        {
            var scheduler = new DecorationScheduler(4);
            scheduler.Enqueue(MakeChunk(0, ChunkCoord.Zero));
            scheduler.Enqueue(MakeChunk(1, new ChunkCoord(1, 0)));

            scheduler.Clear();

            Assert.AreEqual(0, scheduler.PendingCount);
            Assert.AreEqual(2, scheduler.CancelledStaleJobs);
            Assert.AreEqual(0, scheduler.CompletedJobs);
        }

        // --- Cấu hình -----------------------------------------------------------------------------

        [Test]
        public void Config_RejectsNonPositiveJobBudget()
        {
            _config.SetDecorationJobsPerFrame(0);
            Assert.IsFalse(_config.Validate(out string zeroError));
            Assert.IsTrue(zeroError.Contains("decorationJobsPerFrame"), zeroError);

            _config.SetDecorationJobsPerFrame(-2);
            Assert.IsFalse(_config.Validate(out string negativeError));
            Assert.IsTrue(negativeError.Contains("decorationJobsPerFrame"), negativeError);

            _config.SetDecorationJobsPerFrame(1);
            Assert.IsTrue(_config.Validate(out string error), error);
        }

        [Test]
        public void Config_DefaultBudgetIsOneJobPerFrame()
        {
            Assert.AreEqual(1, WorldStreamingConfig.CreateRuntime().DecorationJobsPerFrame);
        }
    }
}
