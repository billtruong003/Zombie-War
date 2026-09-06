#if UNITY_EDITOR
// Fixture nay doc palette/profile qua AssetDatabase nen chi ton tai trong Editor.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// PlayMode M4.6CD.1: hợp đồng viền cảnh vật môi trường.
    ///
    /// Hướng nghệ thuật mới: cảnh vật RẮN có viền, còn MẶT ĐẤT và CÂY CỎ thì không. Mặt đất có viền
    /// sẽ vẽ ra một lưới ô vuông 32 m; cây cỏ có viền sẽ biến hàng nghìn lá cỏ thành nhiễu. Cả hai
    /// đều là lỗi nhìn thấy ngay nhưng rất dễ tái phát khi ai đó chỉnh mặt nạ, nên chốt bằng test.
    ///
    /// Bit này là `Renderer.renderingLayerMask` — kênh THUẦN HÌNH ẢNH. Nó không phải layer vật lý,
    /// nên không đụng va chạm, raycast hay ma trận va chạm; có một bài kiểm riêng cho điều đó.
    public class WorldStreamingEnvironmentOutlineTests
    {
        // Hai bai kiem HANG SO va PROFILE nam o bo EditMode (`GameplayOutlineContractTests`):
        // asmdef cua PlayMode khong tham chieu duoc assembly Editor lan URP.
        private const uint EnvironmentBit = 1u << 4;
        private const uint CharacterSelectionMask = 14;   // vũ khí 2 | quái 4 | người chơi 8

        private WorldStreamingConfig _config;
        private GameObject _probe;
        private WorldStreamingRig.Rig _rig;

        private ChunkPool Pool => _rig.Pool;

        [SetUp]
        public void SetUp()
        {
            var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<DecorationPalette>(
                "Assets/_Project/Data/World/Decoration/DecorationPalette.asset");
            Assert.IsNotNull(palette, "Chưa bake palette. Chạy 'Rebuild Decoration Assets'.");

            _config = WorldStreamingConfig.CreateRuntime();
            _config.SetDecorationPalette(palette);

            _probe = new GameObject("EnvironmentOutlineProbe");
            _probe.transform.position = new Vector3(4f, 1f, 4f);

            _rig = WorldStreamingRig.Build(_config, _probe.transform, null, null, null, initializeOnStart: false);
            _rig.Manager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (Pool != null) Pool.BeginTeardown();
            if (_rig.Root != null) Object.DestroyImmediate(_rig.Root);
            if (_probe != null) Object.DestroyImmediate(_probe);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [Test]
        public void EnvironmentBit_IsOnSolidDecorOnly()
        {
            int solid = 0;
            foreach (ChunkInstance chunk in Pool.All)
            {
                Assert.AreNotEqual(0u, chunk.SolidDecorRenderer.renderingLayerMask & EnvironmentBit,
                    $"Slot {chunk.SlotId}: SolidDecor thiếu bit viền môi trường.");
                Assert.AreEqual(0u, chunk.GroundRenderer.renderingLayerMask & EnvironmentBit,
                    $"Slot {chunk.SlotId}: mặt đất bị đưa vào viền — mỗi chunk sẽ hiện thành một ô vuông.");
                Assert.AreEqual(0u, chunk.FoliageRenderer.renderingLayerMask & EnvironmentBit,
                    $"Slot {chunk.SlotId}: cây cỏ bị đưa vào viền — hàng nghìn lá cỏ sẽ thành nhiễu.");
                solid++;
            }

            Assert.AreEqual(25, solid, "Pool phải có đúng 25 slot.");
        }

        [Test]
        public void EnvironmentBit_DoesNotCollideWithTheCharacterSelectionBits()
        {
            // Bit 1/2/3 thuộc về vũ khí/quái/người chơi. Nếu viền môi trường trùng vào một trong số đó,
            // đá sẽ được vẽ viền nhân vật và ngược lại.
            Assert.AreEqual(0u, EnvironmentBit & CharacterSelectionMask,
                "Bit viền môi trường chồng lên mặt nạ chọn của nhân vật.");
        }

        [Test]
        public void EnvironmentOutline_DoesNotTouchPhysicsLayersOrColliders()
        {
            // `renderingLayerMask` và `GameObject.layer` là hai kênh khác nhau. Bài test này chốt rằng
            // việc thêm viền không hề chạm vào kênh vật lý — đó chính là lỗi mà M4.6C.2 đã mắc.
            foreach (ChunkInstance chunk in Pool.All)
            {
                Assert.AreEqual(chunk.gameObject.layer, chunk.SolidDecorRenderer.gameObject.layer,
                    $"Slot {chunk.SlotId}: layer vật lý của SolidDecor đã bị đổi.");
                Assert.AreEqual(0, chunk.SolidDecorRenderer.GetComponents<Collider>().Length,
                    $"Slot {chunk.SlotId}: cảnh vật rắn không được có collider riêng.");
                Assert.AreEqual(0, chunk.FoliageRenderer.GetComponents<Collider>().Length);
            }
        }

        [Test]
        public void ChunkStructure_StaysAtThreeRenderersAndOneSharedCollider()
        {
            var solidMaterials = new HashSet<int>();
            var foliageMaterials = new HashSet<int>();
            int colliders = 0;

            foreach (ChunkInstance chunk in Pool.All)
            {
                Assert.AreEqual(3, chunk.transform.childCount, $"Slot {chunk.SlotId}");
                Assert.AreEqual(3, chunk.GetComponentsInChildren<MeshRenderer>(true).Length, $"Slot {chunk.SlotId}");
                colliders += chunk.GetComponentsInChildren<Collider>(true).Length;
                solidMaterials.Add(chunk.SolidDecorRenderer.sharedMaterial.GetInstanceID());
                foliageMaterials.Add(chunk.FoliageRenderer.sharedMaterial.GetInstanceID());
            }

            Assert.AreEqual(1, solidMaterials.Count, "Cả ring phải dùng chung một vật liệu Solid.");
            Assert.AreEqual(1, foliageMaterials.Count, "Cả ring phải dùng chung một vật liệu Foliage.");
            Assert.AreEqual(0, colliders, "Trang trí không bao giờ được mang collider.");
        }
    }
}
#endif
