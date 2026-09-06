using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// PlayMode: mesh nền M1 trong pool thật.
    ///
    /// Câu hỏi M1 phải trả lời ở đây: mỗi slot có dùng lại đúng một Mesh suốt đời không, và sau khi
    /// bị tái sử dụng, dữ liệu trong Mesh đó có đúng bằng dữ liệu lấy mẫu trực tiếp cho toạ độ mới không.
    public class WorldStreamingGroundTests
    {
        private const float ChunkSize = 32f;
        private const int Resolution = 17;
        private const int ExpectedActive = 25;
        private const int Seed = 20260809;

        private WorldStreamingConfig _config;
        private GameObject _probe;
        private WorldStreamingRig.Rig _rig;
        private int _meshBaseline;
        private readonly Dictionary<int, Mesh> _meshAfterWarmup = new Dictionary<int, Mesh>();

        private WorldStreamManager Manager => _rig.Manager;
        private ChunkPool Pool => _rig.Pool;

        [SetUp]
        public void SetUp()
        {
            _config = WorldStreamingConfig.CreateRuntime(worldSeed: Seed, groundResolution: Resolution);
            _probe = new GameObject("GroundTestProbe");
            _probe.transform.position = new Vector3(4f, 1f, 4f);

            _rig = WorldStreamingRig.Build(_config, _probe.transform, null, initializeOnStart: false);
            Manager.Initialize();

            _meshBaseline = GroundMeshBuilder.MeshesCreated;
            _meshAfterWarmup.Clear();
            foreach (ChunkInstance instance in Pool.All) _meshAfterWarmup[instance.SlotId] = instance.GroundMesh;
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

        private void AssertMeshesAreStillTheSameObjects(string context)
        {
            Assert.AreEqual(0, GroundMeshBuilder.MeshesCreated - _meshBaseline,
                $"{context}: không được tạo Mesh nền mới sau warmup.");
            Assert.AreEqual(0, Pool.GroundMeshesCreatedSinceWarmup,
                $"{context}: bộ đếm Mesh của pool phải đứng yên.");
            Assert.AreEqual(0, Pool.RootsCreatedSinceWarmup, $"{context}: không được tạo chunk root mới.");
            Assert.AreEqual(0, Pool.RootsDestroyedDuringTraversal, $"{context}: không được huỷ chunk root nào.");
            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount, $"{context}: lease phải giữ nguyên 25.");

            foreach (ChunkInstance instance in Pool.All)
            {
                Assert.IsTrue(_meshAfterWarmup.TryGetValue(instance.SlotId, out Mesh original),
                    $"{context}: xuất hiện slot lạ {instance.SlotId}.");
                Assert.AreSame(original, instance.GroundMesh,
                    $"{context}: slot {instance.SlotId} đã đổi sang Mesh khác — pooling Mesh bị hỏng.");
                Assert.IsNotNull(instance.GroundFilter.sharedMesh, $"{context}: slot {instance.SlotId} mất mesh.");
                Assert.AreSame(instance.GroundMesh, instance.GroundFilter.sharedMesh,
                    $"{context}: MeshFilter của slot {instance.SlotId} không trỏ vào Mesh của slot.");
            }

            var coords = new HashSet<ChunkCoord>();
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                Assert.IsTrue(coords.Add(lease.Key), $"{context}: toạ độ {lease.Key} bị trùng.");
        }

        // --- Ring nền ban đầu ------------------------------------------------------------------

        [Test]
        public void InitialRing_Has25GeneratedGroundMeshes()
        {
            Assert.AreEqual(ExpectedActive, Pool.Capacity);
            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount);
            Assert.AreEqual(ExpectedActive, _meshAfterWarmup.Count, "Phải có đúng 25 Mesh nền sau prewarm.");

            var distinct = new HashSet<Mesh>(_meshAfterWarmup.Values);
            Assert.AreEqual(ExpectedActive, distinct.Count, "25 slot phải có 25 Mesh riêng biệt, không dùng chung.");

            foreach (ChunkInstance instance in Pool.All)
            {
                Mesh mesh = instance.GroundMesh;
                Assert.IsNotNull(mesh, $"Slot {instance.SlotId}: thiếu Mesh nền.");
                Assert.AreEqual(289, mesh.vertexCount, $"Slot {instance.SlotId}: sai số đỉnh.");
                Assert.AreEqual(1, mesh.subMeshCount, $"Slot {instance.SlotId}: phải đúng một submesh.");
                Assert.AreEqual(1536, mesh.GetTriangles(0).Length, $"Slot {instance.SlotId}: sai số chỉ số.");
                Assert.AreEqual(mesh.vertexCount, mesh.colors.Length, $"Slot {instance.SlotId}: thiếu vertex color.");
                Assert.AreEqual(ChunkSize, mesh.bounds.size.x, 0.001f, $"Slot {instance.SlotId}: bounds không phủ hết chunk.");

                Assert.IsFalse(instance.SolidDecorRenderer.enabled, "M1 vẫn chưa dùng SolidDecor.");
                Assert.IsFalse(instance.FoliageRenderer.enabled, "M1 vẫn chưa dùng Foliage.");
                Assert.AreEqual(0, instance.GetComponentsInChildren<Collider>(true).Length,
                    $"Slot {instance.SlotId}: chunk hiển thị không được có collider.");
            }
        }

        [Test]
        public void AllChunks_ShareOneGroundMaterial()
        {
            var materials = new HashSet<Material>();
            foreach (ChunkInstance instance in Pool.All) materials.Add(instance.GroundRenderer.sharedMaterial);

            Assert.AreEqual(1, materials.Count, "Cả 25 chunk phải dùng chung đúng một material nền.");
            Assert.IsNotNull(Pool.All[0].GroundRenderer.sharedMaterial);
        }

        [Test]
        public void SharedSurface_RemainsTheOnlyCollider()
        {
            Collider[] colliders = _rig.Root.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length, "M1 không được thêm collider nào.");
            Assert.AreSame(_rig.Surface.Collider, colliders[0]);
        }

        // --- Dùng lại Mesh -----------------------------------------------------------------------

        [Test]
        public void CardinalDiagonalAndTeleport_ReuseTheSame25Meshes()
        {
            foreach ((ChunkCoord coord, string label) in new[]
                     {
                         (new ChunkCoord(1, 0), "+X"),
                         (new ChunkCoord(-1, 0), "-X qua mốc 0"),
                         (new ChunkCoord(-1, 2), "+Z"),
                         (new ChunkCoord(-1, -3), "-Z qua mốc 0"),
                         (new ChunkCoord(-4, -6), "chéo âm"),
                         (new ChunkCoord(3, -9), "chéo lệch dấu"),
                     })
            {
                MoveTo(coord);
                AssertMeshesAreStillTheSameObjects(label);
            }

            Manager.TeleportTargetTo(new Vector3(2048f, 1f, 2048f));
            AssertMeshesAreStillTheSameObjects("teleport dương lớn");
            Assert.AreEqual(new ChunkCoord(64, 64), Manager.CurrentChunk);

            Manager.TeleportTargetTo(new Vector3(-3072f, 1f, -1600f));
            AssertMeshesAreStillTheSameObjects("teleport âm lớn");
            Assert.AreEqual(new ChunkCoord(-96, -50), Manager.CurrentChunk);
        }

        [Test]
        public void HundredPlusTransitions_CreateNoAdditionalMesh()
        {
            var cursor = new ChunkCoord(0, 0);
            int transitions = 0;

            void Walk(int stepX, int stepZ, int count, string label)
            {
                for (int i = 0; i < count; i++)
                {
                    cursor = new ChunkCoord(cursor.X + stepX, cursor.Z + stepZ);
                    MoveTo(cursor);
                    transitions++;
                    AssertMeshesAreStillTheSameObjects($"{label} bước {i} tới {cursor}");
                }
            }

            Walk(1, 0, 20, "đi dương X");
            Walk(0, 1, 20, "đi dương Z");
            Walk(-1, 0, 45, "đảo chiều qua 0");
            Walk(0, -1, 40, "đi âm Z qua 0");
            Walk(1, 1, 15, "chéo");

            cursor = new ChunkCoord(-451, 288);
            MoveTo(cursor);
            transitions++;
            AssertMeshesAreStillTheSameObjects("nhảy không liền kề");

            Walk(-1, -1, 10, "chéo âm sau nhảy");

            Assert.GreaterOrEqual(transitions, 100, "Bài soak phải có ít nhất 100 lần đổi chunk.");
            Assert.AreEqual(0, GroundMeshBuilder.MeshesCreated - _meshBaseline,
                $"Sau {transitions} lần đổi chunk vẫn không được tạo Mesh nền mới.");
            Assert.AreEqual(ExpectedActive, Pool.Capacity, "Sức chứa pool phải giữ nguyên 25.");
        }

        // --- Dữ liệu sau khi tái sử dụng ------------------------------------------------------------

        [Test]
        public void RecycledSlot_CarriesDataOfItsNewCoordinate()
        {
            MoveTo(ChunkCoord.Zero);

            var watched = new ChunkCoord(-2, -2);   // góc ring, chắc chắn bị nhả sớm
            Assert.IsTrue(Manager.ActiveLeases.ContainsKey(watched));

            ChunkInstance slot = Manager.ActiveLeases[watched];
            int slotId = slot.SlotId;
            Mesh mesh = slot.GroundMesh;
            Color[] originalColors = (Color[])mesh.colors.Clone();

            AssertColorsMatchDirectSampling(mesh, watched, "trước khi tái sử dụng");

            // Đi đủ xa để slot chắc chắn được cấp cho toạ độ khác.
            Manager.TeleportTargetTo(new Vector3(9000f, 1f, -7000f));

            ChunkInstance sameSlot = FindSlot(slotId);
            Assert.IsTrue(sameSlot.IsAssigned, "Sau teleport, mọi slot đều phải đang phục vụ một toạ độ.");
            Assert.AreNotEqual(watched, sameSlot.Coord, "Slot phải đã đổi sang toạ độ khác.");
            Assert.AreSame(mesh, sameSlot.GroundMesh, "Slot phải vẫn dùng đúng Mesh cũ.");

            AssertColorsMatchDirectSampling(sameSlot.GroundMesh, sameSlot.Coord, "sau khi tái sử dụng");

            // Quay lại: dữ liệu tất định của toạ độ cũ phải hiện ra y như lần đầu, bất kể slot nào phục vụ.
            MoveTo(ChunkCoord.Zero);
            Assert.IsTrue(Manager.ActiveLeases.ContainsKey(watched), "Quay lại gốc thì (-2,-2) phải nằm trong ring.");

            ChunkInstance servingNow = Manager.ActiveLeases[watched];
            Color[] reproduced = servingNow.GroundMesh.colors;

            Assert.AreEqual(originalColors.Length, reproduced.Length);
            for (int i = 0; i < originalColors.Length; i++)
            {
                Assert.AreEqual(originalColors[i].r, reproduced[i].r, 1e-5f, $"đỉnh {i}: R không tái lập.");
                Assert.AreEqual(originalColors[i].g, reproduced[i].g, 1e-5f, $"đỉnh {i}: G không tái lập.");
                Assert.AreEqual(originalColors[i].b, reproduced[i].b, 1e-5f, $"đỉnh {i}: B không tái lập.");
                Assert.AreEqual(originalColors[i].a, reproduced[i].a, 1e-5f, $"đỉnh {i}: A không tái lập.");
            }

            AssertMeshesAreStillTheSameObjects("kết thúc bài tái sử dụng");
        }

        [Test]
        public void EveryActiveChunk_HoldsDataOfItsOwnCoordinate()
        {
            Manager.TeleportTargetTo(new Vector3(-5000f, 1f, 3000f));

            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                AssertColorsMatchDirectSampling(lease.Value.GroundMesh, lease.Key, $"chunk {lease.Key}");
        }

        private ChunkInstance FindSlot(int slotId)
        {
            foreach (ChunkInstance instance in Pool.All)
                if (instance.SlotId == slotId) return instance;

            Assert.Fail($"Không tìm thấy slot {slotId}.");
            return null;
        }

        private static void AssertColorsMatchDirectSampling(Mesh mesh, ChunkCoord coord, string context)
        {
            Color[] colors = mesh.colors;
            Assert.AreEqual(GroundMeshBuilder.VertexCount(Resolution), colors.Length, $"{context}: sai số vertex color.");

            for (int iz = 0; iz < Resolution; iz += 2)
            {
                for (int ix = 0; ix < Resolution; ix += 2)
                {
                    BiomeSample expected = GroundMeshBuilder.SampleVertex(Seed, coord, ix, iz, Resolution, ChunkSize);
                    Color actual = colors[iz * Resolution + ix];

                    // Vertex color lưu 8 bit mỗi kênh nên so ở dung sai lượng tử hoá.
                    Assert.AreEqual(expected.Dry, actual.r, 1f / 255f, $"{context} ({ix},{iz}): R lệch.");
                    Assert.AreEqual(expected.Grass, actual.g, 1f / 255f, $"{context} ({ix},{iz}): G lệch.");
                    Assert.AreEqual(expected.Sand, actual.b, 1f / 255f, $"{context} ({ix},{iz}): B lệch.");
                    Assert.AreEqual(expected.Rock, actual.a, 1f / 255f, $"{context} ({ix},{iz}): A lệch.");
                }
            }
        }
    }
}
