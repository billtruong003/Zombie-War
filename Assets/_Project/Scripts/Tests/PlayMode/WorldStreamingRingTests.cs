using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// PlayMode: pool 25 chunk root, ring 5×5 và hành vi tái sử dụng khi di chuyển/teleport.
    ///
    /// Test đặt lại vị trí probe rồi gọi refresh ngay, không giả lập phím và không phụ thuộc timing
    /// theo frame — kết quả phải đúng ngay sau mỗi lần refresh, không phải "đúng sau vài frame".
    public class WorldStreamingRingTests
    {
        private const float ChunkSize = 32f;
        private const int ExpectedActive = 25;

        private WorldStreamingConfig _config;
        private GameObject _probe;
        private WorldStreamingRig.Rig _rig;
        private int _rootsAfterWarmup;

        private WorldStreamManager Manager => _rig.Manager;
        private ChunkPool Pool => _rig.Pool;

        [SetUp]
        public void SetUp()
        {
            _config = WorldStreamingConfig.CreateRuntime();
            _probe = new GameObject("StreamingTestProbe");
            _probe.transform.position = new Vector3(4f, 1f, 4f);

            _rig = WorldStreamingRig.Build(_config, _probe.transform, null, initializeOnStart: false);
            Manager.Initialize();

            _rootsAfterWarmup = Pool.TotalRootsCreated;
        }

        [TearDown]
        public void TearDown()
        {
            // Huỷ khi dọn dẹp KHÔNG phải hành vi traversal — báo cho pool biết trước khi phá.
            if (Pool != null) Pool.BeginTeardown();

            if (_rig.Root != null) Object.DestroyImmediate(_rig.Root);
            if (_probe != null) Object.DestroyImmediate(_probe);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        // --- Trợ giúp ------------------------------------------------------------------------

        private static Vector3 WorldInside(ChunkCoord coord) =>
            new Vector3(coord.X * ChunkSize + 7.5f, 1f, coord.Z * ChunkSize + 11.25f);

        private void MoveTo(ChunkCoord coord) => Manager.TeleportTargetTo(WorldInside(coord));

        private void AssertRingIsExactly(ChunkCoord expectedCenter, string context)
        {
            Assert.AreEqual(expectedCenter, Manager.CurrentChunk, $"{context}: chunk trung tâm sai.");

            var active = new List<ChunkCoord>();
            Manager.CopyActiveCoords(active);

            Assert.AreEqual(ExpectedActive, active.Count, $"{context}: phải có đúng {ExpectedActive} lease.");
            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount, $"{context}: ActiveLeaseCount sai.");

            var uniqueCoords = new HashSet<ChunkCoord>(active);
            Assert.AreEqual(ExpectedActive, uniqueCoords.Count, $"{context}: có toạ độ trùng trong ring.");

            var expected = new HashSet<ChunkCoord>(ChunkCoord.EnumerateRing(expectedCenter, 2));
            Assert.IsTrue(uniqueCoords.SetEquals(expected), $"{context}: tập active khác tập bắt buộc.");

            // Mỗi slot pool chỉ được giữ đúng một toạ độ tại một thời điểm.
            var slots = new HashSet<int>();
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                Assert.IsTrue(lease.Value.IsAssigned, $"{context}: slot {lease.Value.SlotId} phải ở trạng thái assigned.");
                Assert.AreEqual(lease.Key, lease.Value.Coord, $"{context}: slot giữ toạ độ khác với khoá dictionary.");
                Assert.IsTrue(slots.Add(lease.Value.SlotId), $"{context}: slot {lease.Value.SlotId} bị cấp cho hai toạ độ.");

                Vector3 expectedPos = lease.Key.ToWorldMin(ChunkSize);
                Assert.AreEqual(expectedPos.x, lease.Value.transform.position.x, 0.05f, $"{context}: vị trí X sai.");
                Assert.AreEqual(expectedPos.z, lease.Value.transform.position.z, 0.05f, $"{context}: vị trí Z sai.");
            }

            Assert.AreEqual(ExpectedActive, slots.Count, $"{context}: số slot đang dùng phải bằng {ExpectedActive}.");
        }

        private void AssertPoolStayedFixed(string context)
        {
            Assert.AreEqual(0, Pool.RootsCreatedSinceWarmup,
                $"{context}: không được tạo thêm chunk root nào sau warmup.");
            Assert.AreEqual(0, Pool.RootsDestroyedDuringTraversal,
                $"{context}: không được huỷ chunk root nào khi đang chạy.");
            Assert.AreEqual(_rootsAfterWarmup, Pool.TotalRootsCreated, $"{context}: tổng root đã đổi.");
            Assert.AreEqual(ExpectedActive, Pool.Capacity, $"{context}: sức chứa pool phải giữ nguyên {ExpectedActive}.");
        }

        // --- Khởi tạo ------------------------------------------------------------------------

        [Test]
        public void Initialization_Prewarms25Roots_AndLeasesFullRing()
        {
            Assert.AreEqual(ExpectedActive, Pool.Capacity);
            Assert.AreEqual(ExpectedActive, Pool.TotalRootsCreated);
            Assert.AreEqual(0, Pool.RootsCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.FreeCount, "Ring 5×5 dùng hết pool 25 slot.");
            Assert.AreEqual(0, Pool.RecycleCount, "Lần điền đầu tiên chưa phải tái sử dụng.");

            AssertRingIsExactly(ChunkCoord.Zero, "Khởi tạo");
        }

        [Test]
        public void EveryChunkRoot_HasGroundSolidFoliage_AndNoCollider()
        {
            Assert.AreEqual(ExpectedActive, Pool.All.Count);

            foreach (ChunkInstance instance in Pool.All)
            {
                Assert.IsNotNull(instance.GroundFilter, $"Slot {instance.SlotId}: thiếu Ground MeshFilter.");
                Assert.IsNotNull(instance.GroundRenderer, $"Slot {instance.SlotId}: thiếu Ground MeshRenderer.");
                Assert.IsNotNull(instance.SolidDecorFilter, $"Slot {instance.SlotId}: thiếu SolidDecor MeshFilter.");
                Assert.IsNotNull(instance.SolidDecorRenderer, $"Slot {instance.SlotId}: thiếu SolidDecor MeshRenderer.");
                Assert.IsNotNull(instance.FoliageFilter, $"Slot {instance.SlotId}: thiếu Foliage MeshFilter.");
                Assert.IsNotNull(instance.FoliageRenderer, $"Slot {instance.SlotId}: thiếu Foliage MeshRenderer.");

                Assert.AreEqual("Ground", instance.GroundFilter.name);
                Assert.AreEqual("SolidDecor", instance.SolidDecorFilter.name);
                Assert.AreEqual("Foliage", instance.FoliageFilter.name);

                Assert.IsFalse(instance.SolidDecorRenderer.enabled, "M0: SolidDecor phải tắt renderer.");
                Assert.IsFalse(instance.FoliageRenderer.enabled, "M0: Foliage phải tắt renderer.");

                Assert.AreEqual(0, instance.GetComponentsInChildren<Collider>(true).Length,
                    $"Slot {instance.SlotId}: chunk hiển thị không được có collider.");
            }
        }

        [Test]
        public void SharedSurface_IsTheOnlyCollider_WithTopAtY0()
        {
            Collider[] colliders = _rig.Root.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length, "Cả StreamingWorld chỉ được có đúng một collider.");

            BoxCollider box = _rig.Surface.Collider;
            Assert.AreSame(box, colliders[0]);
            Assert.IsFalse(box.isTrigger);

            float top = _rig.Surface.transform.position.y + box.center.y + box.size.y * 0.5f;
            Assert.AreEqual(0f, top, 0.001f, "Mặt trên của surface phải nằm ở Y = 0.");
            Assert.GreaterOrEqual(box.size.x, _config.ActiveDiameter * ChunkSize, "Footprint phải phủ hết ring vật lý.");
            Assert.GreaterOrEqual(box.size.z, _config.ActiveDiameter * ChunkSize, "Footprint phải phủ hết ring vật lý.");
        }

        // --- Di chuyển theo hướng -------------------------------------------------------------

        [Test]
        public void DirectionalTransitions_KeepRingExact_AndPoolFixed()
        {
            var path = new (ChunkCoord Coord, string Label)[]
            {
                (new ChunkCoord(1, 0), "+X một bước"),
                (new ChunkCoord(2, 0), "+X tiếp"),
                (new ChunkCoord(1, 0), "-X đảo chiều"),
                (new ChunkCoord(-1, 0), "-X qua mốc 0"),
                (new ChunkCoord(-2, 0), "-X sâu vào vùng âm"),
                (new ChunkCoord(-2, 1), "+Z"),
                (new ChunkCoord(-2, 2), "+Z tiếp"),
                (new ChunkCoord(-2, -1), "-Z qua mốc 0"),
                (new ChunkCoord(-2, -3), "-Z nhảy hai chunk"),
                (new ChunkCoord(-1, -2), "chéo dương"),
                (new ChunkCoord(-3, -4), "chéo âm"),
                (new ChunkCoord(1, -6), "chéo lệch dấu"),
                (new ChunkCoord(-5, 5), "chéo lệch dấu ngược"),
            };

            foreach ((ChunkCoord coord, string label) in path)
            {
                MoveTo(coord);
                AssertRingIsExactly(coord, label);
                AssertPoolStayedFixed(label);
            }
        }

        [Test]
        public void CardinalStep_RetainsOverlap_AndRecyclesOnlyTheChangedEdge()
        {
            MoveTo(new ChunkCoord(0, 0));
            int recyclesBefore = Pool.RecycleCount;

            MoveTo(new ChunkCoord(1, 0));

            Assert.AreEqual(5, Manager.LastOutgoingCount, "Bước một chunk theo trục chỉ được nhả đúng một cạnh 5 chunk.");
            Assert.AreEqual(5, Manager.LastIncomingCount, "…và chỉ nhận đúng một cạnh 5 chunk.");
            Assert.AreEqual(recyclesBefore + 5, Pool.RecycleCount, "Số lần tái sử dụng phải khớp số slot đổi chủ.");
            AssertRingIsExactly(new ChunkCoord(1, 0), "Bước một chunk");
            AssertPoolStayedFixed("Bước một chunk");
        }

        [Test]
        public void DiagonalStep_HandlesTheLargerChangedEdge()
        {
            MoveTo(new ChunkCoord(0, 0));
            MoveTo(new ChunkCoord(1, 1));

            // Giao của ring cũ và mới là 4×4 = 16 → 9 chunk ra, 9 chunk vào.
            Assert.AreEqual(9, Manager.LastOutgoingCount, "Bước chéo phải nhả 9 chunk, không phải 5.");
            Assert.AreEqual(9, Manager.LastIncomingCount, "Bước chéo phải nhận 9 chunk.");
            AssertRingIsExactly(new ChunkCoord(1, 1), "Bước chéo");
            AssertPoolStayedFixed("Bước chéo");
        }

        // --- Teleport -------------------------------------------------------------------------

        [Test]
        public void LargeTeleports_RebuildWholeRing_FromTheSamePool()
        {
            var destinations = new (Vector3 World, ChunkCoord Expected, string Label)[]
            {
                (new Vector3(2048f, 1f, 2048f), new ChunkCoord(64, 64), "teleport dương lớn"),
                (new Vector3(-3072f, 1f, -1600f), new ChunkCoord(-96, -50), "teleport âm lớn"),
                (new Vector3(-4096.5f, 1f, 5120.25f), new ChunkCoord(-129, 160), "teleport lệch dấu"),
                (new Vector3(0f, 1f, 0f), ChunkCoord.Zero, "về gốc"),
            };

            var prewarmedRoots = new HashSet<ChunkInstance>(Pool.All);
            int refreshesBefore = Manager.RefreshCount;

            foreach ((Vector3 world, ChunkCoord expected, string label) in destinations)
            {
                Manager.TeleportTargetTo(world);

                Assert.AreEqual(ChunkCoord.FromWorld(world, ChunkSize), Manager.CurrentChunk,
                    $"{label}: chunk hiện tại phải bằng floor(vị trí / chunkSize).");
                Assert.AreEqual(expected, Manager.CurrentChunk, $"{label}: đích tính sai.");
                Assert.AreEqual(ExpectedActive, Manager.LastIncomingCount,
                    $"{label}: teleport không giao nhau phải cấp lại cả {ExpectedActive} slot.");
                Assert.AreEqual(ExpectedActive, Manager.LastOutgoingCount,
                    $"{label}: teleport không giao nhau phải nhả cả {ExpectedActive} slot.");

                AssertRingIsExactly(expected, label);
                AssertPoolStayedFixed(label);

                foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                {
                    Assert.IsTrue(prewarmedRoots.Contains(lease.Value),
                        $"{label}: chunk root phải là chính object đã prewarm, không phải object mới.");
                }
            }

            Assert.AreEqual(destinations.Length, Manager.RefreshCount - refreshesBefore,
                "Mỗi teleport chỉ được tốn đúng một lần refresh — không lặp qua từng chunk trung gian.");
        }

        [Test]
        public void PoolSlotId_IsNotWorldIdentity()
        {
            MoveTo(ChunkCoord.Zero);
            int slotAtOriginBefore = Manager.ActiveLeases[ChunkCoord.Zero].SlotId;

            Manager.TeleportTargetTo(new Vector3(10000f, 1f, -10000f));
            MoveTo(ChunkCoord.Zero);

            Assert.IsTrue(Manager.ActiveLeases.ContainsKey(ChunkCoord.Zero),
                "Quay lại gốc thì (0,0) phải lại nằm trong ring.");

            int slotAtOriginAfter = Manager.ActiveLeases[ChunkCoord.Zero].SlotId;
            Assert.AreEqual(ChunkCoord.Zero, Manager.ActiveLeases[ChunkCoord.Zero].Coord,
                "Danh tính thế giới là toạ độ logic, bất kể slot nào đang phục vụ nó.");

            // Slot có thể giống hoặc khác — điều cần khẳng định là hệ thống không phụ thuộc vào nó.
            Assert.GreaterOrEqual(slotAtOriginAfter, 0);
            Assert.Less(slotAtOriginAfter, ExpectedActive);
            Assert.GreaterOrEqual(slotAtOriginBefore, 0);
            AssertPoolStayedFixed("Quay lại gốc");
        }

        // --- Soak 100+ lần đổi chunk ------------------------------------------------------------

        private static List<ChunkCoord> BuildSoakPath()
        {
            var path = new List<ChunkCoord>();
            var cursor = new ChunkCoord(0, 0);

            void Walk(int stepX, int stepZ, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    cursor = new ChunkCoord(cursor.X + stepX, cursor.Z + stepZ);
                    path.Add(cursor);
                }
            }

            Walk(1, 0, 20);    // đi dương theo X
            Walk(0, 1, 20);    // đi dương theo Z
            Walk(-1, 0, 45);   // đảo chiều, vượt qua 0 sang vùng âm
            Walk(0, -1, 40);   // đi âm theo Z, vượt qua 0
            Walk(1, 1, 15);    // chéo
            Walk(1, -1, 15);   // chéo lệch dấu

            cursor = new ChunkCoord(312, -487);   // nhảy không liền kề
            path.Add(cursor);

            Walk(-1, -1, 10);

            return path;
        }

        [Test]
        public void SoakPath_Keeps_RingCorrect_Across100PlusTransitions()
        {
            List<ChunkCoord> path = BuildSoakPath();
            Assert.GreaterOrEqual(path.Count, 100, "Bài soak phải có ít nhất 100 lần đổi chunk trung tâm.");

            ChunkCoord previous = Manager.CurrentChunk;
            int transitions = 0;

            for (int i = 0; i < path.Count; i++)
            {
                ChunkCoord coord = path[i];
                MoveTo(coord);

                Assert.AreNotEqual(previous, coord, $"Bước {i}: đường đi phải thật sự đổi chunk.");
                previous = coord;
                transitions++;

                AssertRingIsExactly(coord, $"Bước soak {i} tới {coord}");
            }

            Assert.GreaterOrEqual(transitions, 100, "Số lần đổi chunk quan sát được phải >= 100.");
            AssertPoolStayedFixed($"Kết thúc soak ({transitions} lần đổi chunk)");
            Assert.Greater(Pool.RecycleCount, 100, "Đi xa như vậy thì slot phải được tái sử dụng nhiều lần.");
        }
    }
}
