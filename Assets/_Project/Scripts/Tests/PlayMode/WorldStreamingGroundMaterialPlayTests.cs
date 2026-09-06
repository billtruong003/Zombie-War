using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// PlayMode M2A: một material dùng chung cho cả 25 chunk, và không gì trong lúc chạy được phép
    /// nhân bản nó ra.
    ///
    /// Chạm vào `renderer.material` (thay vì `sharedMaterial`) là đủ để Unity âm thầm tạo 25 material
    /// instance. Bài test này canh đúng cái bẫy đó, kể cả khi đổi chế độ hiển thị và khi recycle.
    public class WorldStreamingGroundMaterialPlayTests
    {
        private const float ChunkSize = 32f;
        private const int ExpectedActive = 25;

        private WorldStreamingConfig _config;
        private GameObject _probe;
        private WorldStreamingRig.Rig _rig;

        private WorldStreamManager Manager => _rig.Manager;
        private ChunkPool Pool => _rig.Pool;

        [SetUp]
        public void SetUp()
        {
            _config = WorldStreamingConfig.CreateRuntime();
            _probe = new GameObject("GroundMaterialTestProbe");
            _probe.transform.position = new Vector3(4f, 1f, 4f);

            _rig = WorldStreamingRig.Build(_config, _probe.transform, null, initializeOnStart: false);
            Manager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (Pool != null) Pool.BeginTeardown();
            if (_rig.Root != null) Object.DestroyImmediate(_rig.Root);
            if (_probe != null) Object.DestroyImmediate(_probe);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private static Vector3 WorldInside(ChunkCoord coord) =>
            new Vector3(coord.X * ChunkSize + 7.5f, 1f, coord.Z * ChunkSize + 11.25f);

        private HashSet<Material> GroundMaterials()
        {
            var materials = new HashSet<Material>();
            foreach (ChunkInstance instance in Pool.All)
                materials.Add(instance.GroundRenderer.sharedMaterial);

            return materials;
        }

        [Test]
        public void AllGroundRenderers_ShareExactlyOneMaterial()
        {
            Assert.AreEqual(ExpectedActive, Pool.All.Count);

            HashSet<Material> materials = GroundMaterials();
            Assert.AreEqual(1, materials.Count, "Cả 25 chunk phải dùng chung đúng MỘT material nền.");
            Assert.IsNotNull(Pool.All[0].GroundRenderer.sharedMaterial, "Material nền không được để trống.");
        }

        [Test]
        public void NoChunkRenderer_HasAnInstancedMaterial()
        {
            // Renderer nào bị đụng vào `.material` sẽ mang một instance riêng và tên có hậu tố
            // " (Instance)". Kiểm tra cả tham chiếu lẫn tên để bắt cả hai kiểu rò rỉ.
            Material shared = Pool.All[0].GroundRenderer.sharedMaterial;

            foreach (ChunkInstance instance in Pool.All)
            {
                Material material = instance.GroundRenderer.sharedMaterial;
                Assert.AreSame(shared, material, $"Slot {instance.SlotId} có material riêng.");
                StringAssert.DoesNotContain("(Instance)", material.name,
                    $"Slot {instance.SlotId} đang dùng material instance.");
            }
        }

        [Test]
        public void SwitchingDisplayMode_DoesNotInstantiateMaterials()
        {
            var debugGO = new GameObject("TestDebugView");
            try
            {
                var view = debugGO.AddComponent<WorldStreamingDebugView>();
                view.SetManager(Manager);

                Material shared = Pool.All[0].GroundRenderer.sharedMaterial;
                int sharedInstanceId = shared.GetInstanceID();

                foreach (GroundDisplayMode mode in new[]
                         {
                             GroundDisplayMode.BiomeWeights,
                             GroundDisplayMode.DominantSurface,
                             GroundDisplayMode.Textured,
                         })
                {
                    view.DisplayMode = mode;

                    Assert.AreEqual(1, GroundMaterials().Count, $"Chế độ {mode}: material bị nhân bản.");
                    foreach (ChunkInstance instance in Pool.All)
                        Assert.AreSame(shared, instance.GroundRenderer.sharedMaterial,
                            $"Chế độ {mode}: slot {instance.SlotId} đổi material.");
                }

                // Cùng một instance ID sau cả ba lần đổi chế độ: material dùng chung được cập nhật
                // tại chỗ, không có material nào bị sinh ra rồi gán đè.
                Assert.AreEqual(sharedInstanceId, Pool.All[0].GroundRenderer.sharedMaterial.GetInstanceID(),
                    "Đổi chế độ hiển thị không được thay material.");
            }
            finally
            {
                Object.DestroyImmediate(debugGO);
            }
        }

        [Test]
        public void SwitchingDisplayMode_LeavesGroundRenderersWithoutPropertyBlocks()
        {
            var debugGO = new GameObject("TestDebugViewNoPropertyBlocks");
            try
            {
                var view = debugGO.AddComponent<WorldStreamingDebugView>();
                view.SetManager(Manager);

                foreach (GroundDisplayMode mode in new[]
                         {
                             GroundDisplayMode.BiomeWeights,
                             GroundDisplayMode.DominantSurface,
                             GroundDisplayMode.Textured,
                         })
                {
                    view.DisplayMode = mode;

                    Assert.AreEqual((float)mode,
                        Pool.All[0].GroundRenderer.sharedMaterial.GetFloat(ChunkDiagnosticAssets.DebugModeId),
                        0.001f,
                        $"Chế độ {mode} chưa được đẩy lên material dùng chung.");

                    foreach (ChunkInstance instance in Pool.All)
                        Assert.IsFalse(instance.GroundRenderer.HasPropertyBlock(),
                            $"Chế độ {mode}: slot {instance.SlotId} có MaterialPropertyBlock và mất SRP Batcher compatibility.");
                }
            }
            finally
            {
                Object.DestroyImmediate(debugGO);
            }
        }

        [Test]
        public void Recycling_CreatesNoAdditionalMaterialOrMesh()
        {
            Material shared = Pool.All[0].GroundRenderer.sharedMaterial;
            var meshesBefore = new HashSet<Mesh>();
            foreach (ChunkInstance instance in Pool.All) meshesBefore.Add(instance.GroundMesh);

            int meshBaseline = GroundMeshBuilder.MeshesCreated;

            foreach (ChunkCoord coord in new[]
                     {
                         new ChunkCoord(1, 0), new ChunkCoord(-1, 0), new ChunkCoord(-1, -4),
                         new ChunkCoord(-5, 6), new ChunkCoord(97, -63), new ChunkCoord(0, 0),
                     })
            {
                Manager.TeleportTargetTo(WorldInside(coord));

                Assert.AreEqual(1, GroundMaterials().Count, $"Tại {coord}: material bị nhân bản khi recycle.");
                Assert.AreSame(shared, Pool.All[0].GroundRenderer.sharedMaterial, $"Tại {coord}: material đổi.");

                var meshesNow = new HashSet<Mesh>();
                foreach (ChunkInstance instance in Pool.All) meshesNow.Add(instance.GroundMesh);
                Assert.IsTrue(meshesNow.SetEquals(meshesBefore), $"Tại {coord}: Mesh nền bị thay.");
            }

            Assert.AreEqual(0, GroundMeshBuilder.MeshesCreated - meshBaseline, "Recycle không được tạo Mesh mới.");
            Assert.AreEqual(0, Pool.GroundMeshesCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.RootsCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.RootsDestroyedDuringTraversal);
            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount);
        }

        [Test]
        public void SharedCollider_StaysTheOnlyGroundCollider()
        {
            Collider[] colliders = _rig.Root.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length, "M2A không được thêm collider nào.");
            Assert.AreSame(_rig.Surface.Collider, colliders[0]);

            Manager.TeleportTargetTo(new Vector3(-8000f, 1f, 6000f));
            Assert.AreEqual(1, _rig.Root.GetComponentsInChildren<Collider>(true).Length,
                "Teleport cũng không được nhân collider.");
        }

        [Test]
        public void GroundStaysFlatAtY0_AfterRecycling()
        {
            Manager.TeleportTargetTo(new Vector3(-4096f, 1f, 2048f));

            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                Assert.AreEqual(0f, lease.Value.transform.position.y, 0.0001f,
                    $"Chunk root {lease.Key} không còn nằm ở Y = 0.");

                Bounds bounds = lease.Value.GroundMesh.bounds;
                Assert.AreEqual(0f, bounds.min.y, 0.0001f, $"Mesh của {lease.Key} có độ dày theo Y.");
                Assert.AreEqual(0f, bounds.max.y, 0.0001f, $"Mesh của {lease.Key} có độ dày theo Y.");
            }
        }
    }
}
