using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// EditMode M4 CP1.1: costume dựng lúc CHẠY phải mang bit outline của nhân vật.
    ///
    /// Đây chính là lỗ hổng đã làm outline "biến mất" trên người chơi: năm renderer được tag sẵn
    /// trong prefab đều bị tắt lúc chạy, còn người chơi nhìn thấy được là do
    /// `CharacterModularApplier` dựng mới các `Costume_*`. Những renderer mới đó trước đây ở lại
    /// mask 1, nên SelectionOnly không thấy người chơi đâu cả.
    ///
    /// Test đi qua ĐÚNG `Apply()` của production — dựng skeleton thật, `PartEntry` thật, rồi để
    /// applier tự tạo `SkinnedMeshRenderer`. Không tự tay gán mask, không đọc source, không chỉ
    /// kiểm prefab tác giả — làm vậy thì đúng cái đường đã hỏng lại không được kiểm.
    public class PlayerCostumeOutlineRegressionTests
    {
        private const uint WeaponBit = 1u << 1;   // 2
        private const uint EnemyBit = 1u << 2;    // 4
        private const uint PlayerBit = 1u << 3;   // 8

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            // Dọn kể cả khi assert ném giữa chừng — test này tạo GameObject thật.
            foreach (GameObject go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        /// <summary>Dựng một nhân vật tối thiểu nhưng ĐỦ để `Apply()` chạy hết đường thật.</summary>
        private CharacterModularApplier BuildCharacter(bool isPlayer, out Transform skeletonRoot)
        {
            var root = new GameObject(isPlayer ? "TestPlayer" : "TestPreview");
            _spawned.Add(root);

            // `Apply` phân biệt người chơi bằng GetComponentInParent<PlayerMovement>().
            if (isPlayer) root.AddComponent<PlayerMovement>();

            var skel = new GameObject("Skeleton");
            skel.transform.SetParent(root.transform, false);
            skeletonRoot = skel.transform;

            foreach (string bone in new[] { "Hips", "Spine", "Head" })
            {
                var b = new GameObject(bone);
                b.transform.SetParent(skel.transform, false);
            }

            var applier = root.AddComponent<CharacterModularApplier>();
            SetPrivate(applier, "skeletonRoot", skel.transform);
            return applier;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(f, $"CharacterModularApplier không còn field '{field}' — cập nhật test.");
            f.SetValue(target, value);
        }

        private static ModularCostumeCatalog.PartEntry MakePart(string name)
        {
            // Mesh skinned tối thiểu, bind vào đúng những bone vừa dựng ở trên.
            var mesh = new Mesh { name = "TestPart_" + name };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 1, weight0 = 1f },
                new BoneWeight { boneIndex0 = 2, weight0 = 1f },
            };
            mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity };

            return new ModularCostumeCatalog.PartEntry
            {
                name = name,
                skinnedMesh = mesh,
                boneNames = new[] { "Hips", "Spine", "Head" },
                rootBoneName = "Hips",
                materials = new Material[0],
            };
        }

        private static SkinnedMeshRenderer FindCostume(CharacterModularApplier applier, string slot)
        {
            foreach (var smr in applier.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.gameObject.name == "Costume_" + slot) return smr;
            return null;
        }

        [Test]
        public void RuntimeCostume_OnPlayer_ReceivesPlayerBitAndNotEnemyBit()
        {
            CharacterModularApplier applier = BuildCharacter(isPlayer: true, out _);
            Assert.IsTrue(applier.Apply("Chest", MakePart("chest_a")), "Apply() từ chối part hợp lệ.");

            SkinnedMeshRenderer smr = FindCostume(applier, "Chest");
            Assert.IsNotNull(smr, "Apply() không tạo ra Costume_Chest.");

            Assert.AreNotEqual(0u, smr.renderingLayerMask & PlayerBit,
                "Costume dựng lúc chạy KHÔNG mang bit 8 — người chơi sẽ vắng mặt khỏi mặt nạ outline.");
            Assert.AreEqual(0u, smr.renderingLayerMask & EnemyBit,
                "Costume của người chơi lại mang bit quái (4).");
            Assert.AreEqual(0u, smr.renderingLayerMask & WeaponBit,
                "Costume của người chơi lại mang bit vũ khí (2).");
            Assert.AreNotEqual(0u, smr.renderingLayerMask & 1u,
                "Bit ánh sáng mặc định bị xoá mất — chỉ được CỘNG thêm bit outline.");
        }

        [Test]
        public void ReplacingSameSlot_KeepsOneRenderer_AndPreservesPlayerBit()
        {
            CharacterModularApplier applier = BuildCharacter(isPlayer: true, out _);
            applier.Apply("Chest", MakePart("chest_a"));
            applier.Apply("Chest", MakePart("chest_b"));

            int count = 0;
            foreach (var smr in applier.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.gameObject.name == "Costume_Chest") count++;

            Assert.AreEqual(1, count,
                $"Thay đồ cùng một slot để lại {count} renderer — costume cũ chưa được dọn.");
            Assert.AreNotEqual(0u, FindCostume(applier, "Chest").renderingLayerMask & PlayerBit,
                "Sau khi thay đồ, costume mới mất bit 8.");
        }

        [Test]
        public void RepeatedApply_IsDeterministic()
        {
            CharacterModularApplier applier = BuildCharacter(isPlayer: true, out _);
            uint first = 0;
            for (int i = 0; i < 4; i++)
            {
                applier.Apply("Chest", MakePart("chest_a"));
                uint mask = FindCostume(applier, "Chest").renderingLayerMask;
                if (i == 0) first = mask;
                else Assert.AreEqual(first, mask, $"Lần Apply thứ {i + 1} cho mask khác lần đầu.");
            }
        }

        [Test]
        public void PreviewCharacter_IsNotForcedIntoPlayerSelectionMask()
        {
            // Nhân vật preview ở menu dùng chung applier nhưng KHÔNG phải người chơi trong trận.
            CharacterModularApplier applier = BuildCharacter(isPlayer: false, out _);
            applier.Apply("Chest", MakePart("chest_a"));

            SkinnedMeshRenderer smr = FindCostume(applier, "Chest");
            Assert.IsNotNull(smr, "Apply() không tạo costume cho preview.");
            Assert.AreEqual(0u, smr.renderingLayerMask & PlayerBit,
                "Nhân vật preview bị kéo vào mặt nạ outline của gameplay.");
        }

        [Test]
        public void AuthoredPlayerPrefab_ShadowBlob_CarriesNoSelectionBits()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Player.prefab");
            Assert.IsNotNull(prefab, "Không tìm thấy Player.prefab.");

            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                bool isShadowBlob = m != null && m.shader != null &&
                                    m.shader.name == "Universal Render Pipeline/Unlit";
                if (!isShadowBlob) continue;

                Assert.AreEqual(0u, r.renderingLayerMask & (WeaponBit | EnemyBit | PlayerBit),
                    $"Tấm bóng đổ '{r.gameObject.name}' lọt vào mặt nạ outline — sẽ vẽ ra một hình " +
                    "chữ nhật quanh chân nhân vật.");
            }
        }
    }
}
