#if UNITY_EDITOR
// Fixture nay doc palette qua AssetDatabase nen chi ton tai trong Editor.
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// PlayMode M2B: trang trí trong pool thật.
    ///
    /// Câu hỏi lớn nhất ở đây: đi thật xa rồi quay lại, cây cỏ có mọc lại y hệt chỗ cũ không, và
    /// trong suốt hành trình có sinh thêm một GameObject, material, mesh hay collider nào không.
    public class WorldStreamingDecorationPlayTests
    {
        private const float ChunkSize = 32f;
        private const int ExpectedActive = 25;

        private WorldStreamingConfig _config;
        private DecorationPalette _palette;
        private GameObject _probe;
        private WorldStreamingRig.Rig _rig;

        private WorldStreamManager Manager => _rig.Manager;
        private ChunkPool Pool => _rig.Pool;

        [SetUp]
        public void SetUp()
        {
            _palette = UnityEditor.AssetDatabase.LoadAssetAtPath<DecorationPalette>(
                "Assets/_Project/Data/World/Decoration/DecorationPalette.asset");
            Assert.IsNotNull(_palette, "Chưa bake palette. Chạy 'Rebuild Decoration Assets'.");

            _config = WorldStreamingConfig.CreateRuntime();
            _config.SetDecorationPalette(_palette);

            _probe = new GameObject("DecorationTestProbe");
            _probe.transform.position = new Vector3(4f, 1f, 4f);

            _rig = WorldStreamingRig.Build(_config, _probe.transform, null, null, null, initializeOnStart: false);
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

        /// <summary>
        /// Di chuyển RỒI chờ dựng xong.
        ///
        /// Từ M3B.2 trang trí được sinh rải ra nhiều frame, nên "vừa teleport xong" không còn đồng
        /// nghĩa với "thế giới đã đầy đủ". Mọi bài test dưới đây nói về NỘI DUNG cuối cùng, nên chúng
        /// phải chờ hàng đợi cạn — chờ tường minh chứ không nới lỏng khẳng định.
        /// </summary>
        private void MoveTo(ChunkCoord coord) => TeleportTo(WorldInside(coord));

        private void TeleportTo(Vector3 position)
        {
            Manager.TeleportTargetTo(position);
            Manager.DrainDecoration();
            Assert.IsTrue(Manager.IsGenerationSettled, "Hàng đợi trang trí không cạn.");
        }

        /// <summary>Vân tay của hình học thật đang nằm trong hai mesh gộp của một chunk.</summary>
        private static string MeshChecksum(ChunkInstance chunk)
        {
            var sb = new StringBuilder();
            foreach (Mesh mesh in new[] { chunk.SolidMesh, chunk.FoliageMesh })
            {
                sb.Append(mesh.vertexCount).Append(':').Append(mesh.GetTriangles(0).Length).Append(':');
                Vector3[] positions = mesh.vertices;
                for (int i = 0; i < positions.Length; i += 37)
                    sb.Append(positions[i].x.ToString("F4")).Append(',')
                      .Append(positions[i].y.ToString("F4")).Append(',')
                      .Append(positions[i].z.ToString("F4")).Append(';');
                sb.Append('|');
            }

            return sb.ToString();
        }

        // --- Kiến trúc render --------------------------------------------------------------------

        [Test]
        public void EachChunk_HasExactlyThreeRenderersAndNoPropObjects()
        {
            foreach (ChunkInstance chunk in Pool.All)
            {
                Assert.AreEqual(3, chunk.transform.childCount,
                    $"Slot {chunk.SlotId}: chunk root chỉ được có Ground/SolidDecor/Foliage.");

                var renderers = chunk.GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(3, renderers.Length, $"Slot {chunk.SlotId}: đúng ba renderer.");

                foreach (MeshRenderer renderer in renderers)
                {
                    Assert.AreEqual(1, renderer.sharedMaterials.Length,
                        $"Slot {chunk.SlotId}/{renderer.name}: một renderer chỉ được có một material slot.");
                    Assert.IsFalse(renderer.HasPropertyBlock(),
                        $"Slot {chunk.SlotId}/{renderer.name}: có MaterialPropertyBlock, mất SRP Batcher.");
                }

                Assert.AreEqual(0, chunk.GetComponentsInChildren<Collider>(true).Length,
                    $"Slot {chunk.SlotId}: scenery không được có collider.");
                Assert.AreEqual(0, chunk.GetComponentsInChildren<LODGroup>(true).Length,
                    $"Slot {chunk.SlotId}: không LODGroup cho từng prop.");
            }
        }

        [Test]
        public void WholeRing_UsesOneMaterialPerCategory()
        {
            var ground = new HashSet<Material>();
            var solid = new HashSet<Material>();
            var foliage = new HashSet<Material>();

            foreach (ChunkInstance chunk in Pool.All)
            {
                ground.Add(chunk.GroundRenderer.sharedMaterial);
                solid.Add(chunk.SolidDecorRenderer.sharedMaterial);
                foliage.Add(chunk.FoliageRenderer.sharedMaterial);
            }

            Assert.AreEqual(1, ground.Count, "Cả ring phải dùng chung một material nền.");
            Assert.AreEqual(1, solid.Count, "Cả ring phải dùng chung một material trang trí đặc.");
            Assert.AreEqual(1, foliage.Count, "Cả ring phải dùng chung một material foliage.");
        }

        [Test]
        public void FoliageRenderers_CastNoShadows()
        {
            foreach (ChunkInstance chunk in Pool.All)
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, chunk.FoliageRenderer.shadowCastingMode,
                    $"Slot {chunk.SlotId}: foliage thường không đổ bóng ở MVP.");
        }

        [Test]
        public void EmptyOutputs_DisableTheirRendererInsteadOfDestroyingIt()
        {
            // Output rỗng phải TẮT renderer, chứ không huỷ renderer hay mesh của slot.
            //
            // Trước M4.6CD chỉ có thân cây đi vào output Solid, nên vùng cát — nơi cây không mọc — tự
            // nhiên cho ra những chunk Solid rỗng và bài test này chỉ việc đi tới đó mà xem. M4.6CD
            // thêm đá / rác / thùng phuy / lốp xe, và chúng CÓ mọc trên cát, nên không còn chunk Solid
            // rỗng nào ở đó nữa (đo được: 25/25 chunk quanh (-73,0) đều có hình học Solid).
            //
            // Nên bài test tách làm hai phần. Phần một canh HỢP ĐỒNG THẬT trên bảng màu production, và
            // hợp đồng đó đúng ở mọi nơi chứ không riêng vùng cát: renderer bật khi và chỉ khi mesh
            // gộp có hình học. Phần hai DỰNG RA trường hợp rỗng một cách tất định bằng một bảng màu
            // không bao giờ chấp nhận placement nào — chứ không trông chờ thế giới tình cờ tạo ra nó.
            TeleportTo(new Vector3(-2328f, 1f, 0f));

            int emptyAndDisabled = 0;
            foreach (ChunkInstance chunk in Pool.All)
            {
                Assert.IsNotNull(chunk.SolidDecorRenderer, "Renderer không bao giờ được bị huỷ.");
                Assert.IsNotNull(chunk.FoliageRenderer, "Renderer không bao giờ được bị huỷ.");
                Assert.IsNotNull(chunk.SolidMesh, "Mesh của slot không bao giờ được bị huỷ.");
                Assert.IsNotNull(chunk.FoliageMesh, "Mesh của slot không bao giờ được bị huỷ.");
                if (!chunk.IsAssigned) continue;

                // Đây mới là hợp đồng thật, và nó đúng ở MỌI nơi chứ không riêng vùng cát:
                // renderer bật khi và chỉ khi mesh gộp có hình học.
                Assert.AreEqual(chunk.SolidVertexCount > 0, chunk.SolidDecorRenderer.enabled,
                    $"Slot {chunk.SlotId}: renderer Solid không khớp với việc mesh có hình học hay không.");
                Assert.AreEqual(chunk.FoliageVertexCount > 0, chunk.FoliageRenderer.enabled,
                    $"Slot {chunk.SlotId}: renderer Foliage không khớp với việc mesh có hình học hay không.");

                if (chunk.FoliageVertexCount == 0 && !chunk.FoliageRenderer.enabled) emptyAndDisabled++;
            }

            // --- Phần hai: trường hợp rỗng, dựng ra chứ không đi tìm ---
            var barren = new DecorationPaletteEntry();
            barren.Configure("barren_probe", _palette.Entries[0].PrimarySource, null,
                cell: 4f, candidates: 1, density: 0.5f, spacing: 0f,
                affinity: Vector4.zero,                       // không biome nào nuôi nó → không bao giờ được nhận
                scaleRange: new Vector2(1f, 1f), tint: new Vector2(1f, 1f), priority: 10);
            DecorationPalette empty = DecorationPalette.CreateRuntime(
                new List<DecorationPaletteEntry> { barren });

            try
            {
                _config.SetDecorationPalette(empty);
                TeleportTo(new Vector3(-2328f, 1f, 512f));

                foreach (ChunkInstance chunk in Pool.All)
                {
                    if (!chunk.IsAssigned) continue;

                    Assert.AreEqual(0, chunk.PlacementCount, $"Slot {chunk.SlotId}: bảng màu rỗng vẫn ra placement.");
                    Assert.IsNotNull(chunk.SolidDecorRenderer, "Renderer không bao giờ được bị huỷ.");
                    Assert.IsNotNull(chunk.FoliageRenderer, "Renderer không bao giờ được bị huỷ.");
                    Assert.IsNotNull(chunk.SolidMesh, "Mesh của slot không bao giờ được bị huỷ.");
                    Assert.IsNotNull(chunk.FoliageMesh, "Mesh của slot không bao giờ được bị huỷ.");
                    Assert.IsFalse(chunk.SolidDecorRenderer.enabled, $"Slot {chunk.SlotId}: renderer Solid rỗng mà vẫn bật.");
                    Assert.IsFalse(chunk.FoliageRenderer.enabled, $"Slot {chunk.SlotId}: renderer Foliage rỗng mà vẫn bật.");
                    emptyAndDisabled++;
                }
            }
            finally
            {
                _config.SetDecorationPalette(_palette);
                Object.DestroyImmediate(empty);
            }

            Assert.Greater(emptyAndDisabled, 0,
                "Không dựng được chunk nào có output rỗng — bài test mất đi chính trường hợp nó canh.");
        }

        // --- Nội dung ------------------------------------------------------------------------------

        [Test]
        public void RepresentativeChunk_CarriesTargetPlacementCountInsideBudget()
        {
            MoveTo(new ChunkCoord(74, 47));

            ChunkInstance centre = null;
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
                if (lease.Key == Manager.CurrentChunk) centre = lease.Value;

            Assert.IsNotNull(centre);
            // Dải 100-200 đặt ở M2B với mesh vài chục đỉnh. M4.6CD.1 đổi sang hình học vendor gốc
            // (cỏ 230, bụi 1.863-3.913, cụm đá 1.194-3.372), nên cùng ngân sách mua được ít placement
            // hơn mà mỗi cái nhiều chi tiết hơn. Đo được 82. Bộ EditMode anh em hạ cùng một ngưỡng.
            Assert.GreaterOrEqual(centre.PlacementCount, 55, $"Chỉ có {centre.PlacementCount} placement.");
            Assert.LessOrEqual(centre.PlacementCount, 200, $"Có tới {centre.PlacementCount} placement.");

            Assert.LessOrEqual(centre.FoliageVertexCount, _palette.FoliageVertexBudget);
            Assert.LessOrEqual(centre.SolidVertexCount, _palette.SolidVertexBudget);
            Assert.Greater(centre.FoliageVertexCount, 0, "Chunk cỏ phải có hình học foliage.");
        }

        [Test]
        public void EveryActiveChunk_MatchesDirectSampling()
        {
            TeleportTo(new Vector3(2376f, 1f, 1512f));

            var expected = new List<DecorationPlacement>();
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                // Từ M3B.2D mỗi chunk được dựng ở mật độ của BẬC nó đang thuộc về, nên phép lấy mẫu
                // đối chứng cũng phải hỏi đúng bậc đó. Vẫn là so khớp tuyệt đối, không nới lỏng.
                DecorationDensityTier tier = _config.TierFor(lease.Key, Manager.CurrentChunk);
                DecorationSampleStats stats = default;
                DecorationSampler.Generate(_config.WorldSeed, lease.Key, ChunkSize, _palette, expected,
                    _config.FoliageDensityFor(tier), ref stats);

                Assert.AreEqual(expected.Count, lease.Value.PlacementCount,
                    $"{lease.Key} (bậc {tier}): chunk đang hiển thị khác kết quả lấy mẫu trực tiếp.");
            }
        }

        // --- Tái sử dụng --------------------------------------------------------------------------

        [Test]
        public void LeavingAndReturning_ReproducesIdenticalDecoration()
        {
            var watched = new ChunkCoord(74, 47);
            MoveTo(watched);

            ChunkInstance before = Manager.ActiveLeases[watched];
            int placementsBefore = before.PlacementCount;
            string checksumBefore = MeshChecksum(before);
            Assert.Greater(placementsBefore, 0, "Chunk thử phải có trang trí để phép so sánh có nghĩa.");

            // Đi rất xa rồi vòng lại: slot gần như chắc chắn đã phục vụ toạ độ khác ở giữa chừng.
            TeleportTo(new Vector3(-9000f, 1f, 6000f));
            MoveTo(new ChunkCoord(0, 0));
            MoveTo(watched);

            ChunkInstance after = Manager.ActiveLeases[watched];
            Assert.AreEqual(placementsBefore, after.PlacementCount, "Số placement đổi sau khi quay lại.");
            Assert.AreEqual(checksumBefore, MeshChecksum(after), "Hình học mọc lại khác lần đầu.");
        }

        [Test]
        public void PoolSlot_DoesNotAffectDecoration()
        {
            var watched = new ChunkCoord(-54, 48);

            MoveTo(watched);
            int firstSlot = Manager.ActiveLeases[watched].SlotId;
            string firstChecksum = MeshChecksum(Manager.ActiveLeases[watched]);

            // Đi một vòng khác để slot xoay vòng, rồi quay lại đúng toạ độ đó.
            TeleportTo(new Vector3(4000f, 1f, -7000f));
            MoveTo(new ChunkCoord(11, -3));
            MoveTo(watched);

            ChunkInstance now = Manager.ActiveLeases[watched];
            Assert.AreEqual(firstChecksum, MeshChecksum(now),
                $"Slot {firstSlot} → {now.SlotId}: đổi slot mà lại đổi cả trang trí.");
        }

        // --- Regression streaming --------------------------------------------------------------------

        [Test]
        public void HundredPlusTransitions_CreateNothingNew()
        {
            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();
            int renderers = 0;

            foreach (ChunkInstance chunk in Pool.All)
            {
                meshes.Add(chunk.GroundMesh);
                meshes.Add(chunk.SolidMesh);
                meshes.Add(chunk.FoliageMesh);
                materials.Add(chunk.GroundRenderer.sharedMaterial);
                materials.Add(chunk.SolidDecorRenderer.sharedMaterial);
                materials.Add(chunk.FoliageRenderer.sharedMaterial);
                renderers += chunk.GetComponentsInChildren<MeshRenderer>(true).Length;
            }

            Assert.AreEqual(75, meshes.Count, "Warmup phải cho đúng 25×3 mesh riêng biệt.");
            Assert.AreEqual(3, materials.Count, "Warmup phải cho đúng ba material dùng chung.");

            int decorationBaseline = DecorationMeshBuilder.MeshesCreated;
            int groundBaseline = GroundMeshBuilder.MeshesCreated;

            var cursor = new ChunkCoord(0, 0);
            int transitions = 0;

            void Walk(int dx, int dz, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    cursor = new ChunkCoord(cursor.X + dx, cursor.Z + dz);
                    MoveTo(cursor);
                    transitions++;

                    Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount, $"{cursor}: mất lease.");
                    Assert.AreEqual(0, DecorationMeshBuilder.MeshesCreated - decorationBaseline,
                        $"{cursor}: sinh thêm Mesh trang trí.");
                }
            }

            Walk(1, 0, 22);
            Walk(0, 1, 22);
            Walk(-1, 0, 48);
            Walk(0, -1, 44);
            Walk(1, 1, 16);
            Walk(1, -1, 16);

            cursor = new ChunkCoord(-620, 391);
            MoveTo(cursor);
            transitions++;
            Walk(-1, -1, 12);
            MoveTo(new ChunkCoord(0, 0));
            transitions++;

            Assert.GreaterOrEqual(transitions, 100, "Bài soak phải có ít nhất 100 lần đổi chunk.");

            var meshesNow = new HashSet<Mesh>();
            var materialsNow = new HashSet<Material>();
            int renderersNow = 0;
            int propObjects = 0;
            int colliders = 0;

            foreach (ChunkInstance chunk in Pool.All)
            {
                meshesNow.Add(chunk.GroundMesh);
                meshesNow.Add(chunk.SolidMesh);
                meshesNow.Add(chunk.FoliageMesh);
                materialsNow.Add(chunk.GroundRenderer.sharedMaterial);
                materialsNow.Add(chunk.SolidDecorRenderer.sharedMaterial);
                materialsNow.Add(chunk.FoliageRenderer.sharedMaterial);
                renderersNow += chunk.GetComponentsInChildren<MeshRenderer>(true).Length;
                propObjects += chunk.transform.childCount - 3;
                colliders += chunk.GetComponentsInChildren<Collider>(true).Length;
            }

            Assert.IsTrue(meshesNow.SetEquals(meshes), "Bộ Mesh đã đổi trong lúc đi.");
            Assert.IsTrue(materialsNow.SetEquals(materials), "Bộ material đã đổi trong lúc đi.");
            Assert.AreEqual(renderers, renderersNow, "Số renderer đã đổi.");
            Assert.AreEqual(0, propObjects, "Xuất hiện GameObject cho từng prop.");
            Assert.AreEqual(0, colliders, "Xuất hiện collider trên scenery.");
            Assert.AreEqual(0, Pool.RootsCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.RootsDestroyedDuringTraversal);
            Assert.AreEqual(0, Pool.GroundMeshesCreatedSinceWarmup);
            Assert.AreEqual(0, Pool.DecorationMeshesCreatedSinceWarmup);
            Assert.AreEqual(0, GroundMeshBuilder.MeshesCreated - groundBaseline);
            Assert.AreEqual(1, _rig.Root.GetComponentsInChildren<Collider>(true).Length, "Vẫn phải đúng một collider dùng chung.");
        }

        [Test]
        public void DisablingDecoration_LeavesGroundAndHierarchyIntact()
        {
            _config.SetDecorationEnabled(false);
            MoveTo(new ChunkCoord(74, 47));

            foreach (ChunkInstance chunk in Pool.All)
            {
                Assert.AreEqual(3, chunk.transform.childCount, "Tắt trang trí không được đổi cây con.");
                Assert.IsFalse(chunk.SolidDecorRenderer.enabled);
                Assert.IsFalse(chunk.FoliageRenderer.enabled);
                if (chunk.IsAssigned) Assert.IsTrue(chunk.GroundRenderer.enabled, "Mặt đất vẫn phải hiển thị.");
            }

            // Bật lại chỉ có tác dụng với chunk được GÁN lại — lease đang giữ thì không dựng lại,
            // đúng như thiết kế streaming. Nên phải đi hẳn ra xa rồi mới quay về.
            _config.SetDecorationEnabled(true);
            TeleportTo(new Vector3(-9000f, 1f, 9000f));
            MoveTo(new ChunkCoord(74, 47));

            Assert.Greater(Manager.ActiveLeases[new ChunkCoord(74, 47)].PlacementCount, 0,
                "Bật lại rồi gán lại chunk thì phải có trang trí.");
        }
    }
}
#endif
