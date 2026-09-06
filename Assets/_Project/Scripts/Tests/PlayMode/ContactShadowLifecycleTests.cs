using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5 — hợp đồng vòng đời của mesh bóng gộp.
    ///
    /// Đây là phần dễ rò rỉ nhất của kiến trúc gộp: không còn renderer riêng cho từng nhân vật, nên
    /// một handle bị quên KHÔNG hiện ra thành một object thừa trong Hierarchy — nó chỉ lặng lẽ chiếm
    /// một chỗ trong bảng đăng ký cho tới khi tràn. Vì vậy mọi đường vào/ra đều phải có test riêng.
    ///
    /// Fixture dựng manager một cách TƯỜNG MINH kèm material, đúng như production yêu cầu, thay vì
    /// dựa vào một đường tự tạo lúc chạy.
    /// </summary>
    public class ContactShadowLifecycleTests
    {
        private CharacterContactShadows _batch;
        private readonly List<GameObject> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            // Dựng object ở trạng thái TẮT trước: Awake chạy ngay khi AddComponent vào một object đang
            // bật, và nó dựng bảng đăng ký bằng capacity mặc định. Đặt trường xong rồi mới bật thì
            // Awake mới thấy đúng giá trị của fixture.
            var go = new GameObject("TestContactShadows");
            go.SetActive(false);
            _spawned.Add(go);

            var material = new Material(Shader.Find("ZombieWar/Environment/CharacterContactShadow"));
            _batch = go.AddComponent<CharacterContactShadows>();

            var so = new UnityEditor.SerializedObject(_batch);
            so.FindProperty("shadowMaterial").objectReferenceValue = material;
            so.FindProperty("capacity").intValue = 16;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private Transform MakeTarget(Vector3 position)
        {
            var go = new GameObject("ShadowTarget");
            go.transform.position = position;
            _spawned.Add(go);
            return go.transform;
        }

        // --- đăng ký / trả chỗ -----------------------------------------------------------------

        [Test]
        public void Register_ReturnsAValidHandleAndCountsOnce()
        {
            int before = _batch.RegisteredCount;
            int handle = _batch.Register(MakeTarget(Vector3.zero), 0.4f, 0.4f, 0.5f);

            Assert.GreaterOrEqual(handle, 0, "registration refused unexpectedly");
            Assert.AreEqual(before + 1, _batch.RegisteredCount);
        }

        [Test]
        public void Unregister_ReturnsTheSlotExactlyOnce_AndRepeatingIsHarmless()
        {
            int handle = _batch.Register(MakeTarget(Vector3.zero), 0.4f, 0.4f, 0.5f);
            int registered = _batch.RegisteredCount;

            _batch.Unregister(handle);
            Assert.AreEqual(registered - 1, _batch.RegisteredCount);

            // Trả chỗ hai lần là ca THẬT: OnDisable có thể chạy sau khi object đã bị huỷ theo đường khác.
            _batch.Unregister(handle);
            Assert.AreEqual(registered - 1, _batch.RegisteredCount,
                "trả chỗ lần hai làm hỏng bộ đếm — bảng đăng ký sẽ tự cấp trùng slot.");
        }

        [Test]
        public void RepeatedPoolCycles_DoNotLeakHandles()
        {
            var target = MakeTarget(Vector3.zero);
            int baseline = _batch.RegisteredCount;

            for (int i = 0; i < 20; i++)
            {
                int handle = _batch.Register(target, 0.4f, 0.4f, 0.5f);
                Assert.GreaterOrEqual(handle, 0, $"vòng {i}: hết chỗ — có rò rỉ handle.");
                _batch.Unregister(handle);
            }

            Assert.AreEqual(baseline, _batch.RegisteredCount, "20 vòng pool để lại đăng ký thừa.");
        }

        [Test]
        public void CapacityOverflow_FailsLoudlyInsteadOfSilentlyDroppingAShadow()
        {
            var handles = new List<int>();
            for (int i = 0; i < _batch.Capacity; i++)
                handles.Add(_batch.Register(MakeTarget(new Vector3(i, 0f, 0f)), 0.4f, 0.4f, 0.5f));

            CollectionAssert.DoesNotContain(handles, -1, "hết chỗ trước khi đạt capacity đã khai báo.");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Hết chỗ"));
            int overflow = _batch.Register(MakeTarget(Vector3.one), 0.4f, 0.4f, 0.5f);

            Assert.AreEqual(-1, overflow, "tràn capacity phải trả -1, không được âm thầm nuốt.");
        }

        // --- hiển thị / mờ dần ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SetVisible_HidesAndRestoresTheSameSlot()
        {
            int handle = _batch.Register(MakeTarget(Vector3.zero), 0.4f, 0.4f, 0.5f);
            yield return null;
            Assert.AreEqual(1, _batch.VisibleShadowCount);

            _batch.SetVisible(handle, false);
            yield return null;
            Assert.AreEqual(0, _batch.VisibleShadowCount, "ẩn không có tác dụng");

            _batch.SetVisible(handle, true);
            yield return null;
            Assert.AreEqual(1, _batch.VisibleShadowCount, "hiện lại không khôi phục đúng slot");
        }

        [UnityTest]
        public IEnumerator FullyFadedShadow_StopsBeingDrawn()
        {
            int handle = _batch.Register(MakeTarget(Vector3.zero), 0.4f, 0.4f, 0.5f);
            yield return null;
            Assert.AreEqual(1, _batch.VisibleShadowCount);

            _batch.SetFade(handle, 0.5f);
            yield return null;
            Assert.AreEqual(1, _batch.VisibleShadowCount, "mờ một nửa vẫn phải vẽ");

            _batch.SetFade(handle, 1f);
            yield return null;
            Assert.AreEqual(0, _batch.VisibleShadowCount, "tan hết mà bóng vẫn còn trên đất");
        }

        [UnityTest]
        public IEnumerator ZeroVisibleShadows_DisablesTheBatchRenderer()
        {
            int handle = _batch.Register(MakeTarget(Vector3.zero), 0.4f, 0.4f, 0.5f);
            yield return null;
            Assert.IsTrue(_batch.Renderer.enabled);

            _batch.Unregister(handle);
            yield return null;

            Assert.IsFalse(_batch.Renderer.enabled,
                "không còn bóng nào mà renderer vẫn bật — vẫn tốn một draw rỗng mỗi frame.");
        }

        [UnityTest]
        public IEnumerator DestroyedTarget_IsDroppedSafely()
        {
            var target = MakeTarget(Vector3.zero);
            _batch.Register(target, 0.4f, 0.4f, 0.5f);
            yield return null;
            Assert.AreEqual(1, _batch.VisibleShadowCount);

            Object.DestroyImmediate(target.gameObject);
            yield return null;

            Assert.AreEqual(0, _batch.VisibleShadowCount,
                "mục tiêu đã bị huỷ mà bóng vẫn nằm lại trên mặt đất.");
        }

        // --- toạ độ ------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator NegativeCoordinatesAndLongTeleport_KeepTheShadowUnderTheTarget()
        {
            var target = MakeTarget(new Vector3(-1240.5f, 0f, -880.25f));
            _batch.Register(target, 0.4f, 0.4f, 0.5f);
            yield return null;

            Bounds bounds = _batch.BatchMesh.bounds;
            Vector3 worldCentre = _batch.transform.TransformPoint(bounds.center);
            Assert.AreEqual(target.position.x, worldCentre.x, 1.0f, "bóng lệch khỏi mục tiêu ở toạ độ âm");
            Assert.AreEqual(target.position.z, worldCentre.z, 1.0f);

            target.position = new Vector3(7400f, 0f, -6100f);
            yield return null;

            bounds = _batch.BatchMesh.bounds;
            worldCentre = _batch.transform.TransformPoint(bounds.center);
            Assert.AreEqual(target.position.x, worldCentre.x, 1.0f, "bóng lệch sau teleport xa");
            Assert.AreEqual(target.position.z, worldCentre.z, 1.0f);
        }

        // --- cấp phát -----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SteadyStateUpdate_DoesNotAllocate()
        {
            for (int i = 0; i < 8; i++)
                _batch.Register(MakeTarget(new Vector3(i * 0.6f, 0f, 0f)), 0.4f, 0.4f, 0.5f);

            // cho buffer và mesh ổn định trước khi đo
            for (int i = 0; i < 10; i++) yield return null;

            // Đo CHÍNH đường ghi mesh, không đo heap của cả domain qua nhiều frame: phép đo qua frame
            // gộp cả rác của test runner và của mọi hệ khác, nên nó không nói gì về đoạn mã đang xét.
            _batch.RebuildNow();
            System.GC.Collect();
            long before = System.GC.GetTotalMemory(true);

            for (int i = 0; i < 240; i++) _batch.RebuildNow();

            long growth = System.GC.GetTotalMemory(false) - before;

            Assert.Less(growth, 16 * 1024,
                $"240 lần dựng mesh bóng cấp phát {growth} B — đường chạy mỗi frame phải không sinh rác.");
            yield return null;
        }

        // --- một hệ duy nhất -----------------------------------------------------------------------

        [Test]
        public void ASecondManager_IsRejected()
        {
            var extra = new GameObject("DuplicateContactShadows");
            _spawned.Add(extra);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Đã có một CharacterContactShadows khác"));
            extra.AddComponent<CharacterContactShadows>();

            Assert.AreSame(_batch, CharacterContactShadows.Instance,
                "manager thứ hai chiếm mất Instance — sẽ có hai mesh bóng cùng vẽ.");
        }
    }
}
