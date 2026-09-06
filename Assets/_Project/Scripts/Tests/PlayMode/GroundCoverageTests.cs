using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5 final closeout — bất biến NHÌN THẤY được của mặt đất stream.
    ///
    /// Bất biến: mọi điểm trên vệt chiếu của camera gameplay xuống mặt phẳng gameplay phải nằm trong
    /// một chunk nền ĐANG BẬT, còn dư ít nhất một dải đệm, trước khi nó lọt vào Game View.
    ///
    /// Cố ý KHÔNG có test nào khẳng định <c>RenderRadius == 2</c>. Bán kính là phương tiện, không phải
    /// hợp đồng: một camera rộng hơn hay một tỉ lệ khung hình khác có thể làm bán kính 2 không đủ mà
    /// con số 2 vẫn "đúng". Ở đây mọi test đo bằng phép chiếu thật của camera, đúng phép toán mà
    /// production dùng, nên khi camera đổi thì test đổi kết quả theo.
    /// </summary>
    public class GroundCoverageTests
    {
        private const float ChunkSize = 32f;
        private const int Radius = 2;
        private const int ExpectedActive = 25;

        // Tư thế camera gameplay của production (CameraFollow: offset + góc nhìn cố định).
        private static readonly Vector3 CameraOffset = new Vector3(0f, 12f, -8f);
        private static readonly Vector3 CameraEuler = new Vector3(60f, 0f, 0f);

        // Dải đệm mục tiêu: một chunk đầy đủ ngoài mép màn hình theo mọi hướng, nên lớp đất kế tiếp
        // đã tồn tại TRƯỚC khi người chơi bắt đầu bước vào nó.
        private const float GuardBand = ChunkSize;

        private WorldStreamingConfig _config;
        private GameObject _probe;
        private GameObject _cameraGO;
        private Camera _camera;
        private WorldStreamingRig.Rig _rig;

        private WorldStreamManager Manager => _rig.Manager;

        [SetUp]
        public void SetUp()
        {
            _config = WorldStreamingConfig.CreateRuntime();
            _probe = new GameObject("CoverageProbe");
            _probe.transform.position = new Vector3(4f, 0f, 4f);

            _cameraGO = new GameObject("CoverageCamera");
            _camera = _cameraGO.AddComponent<Camera>();
            _camera.enabled = false;              // không vẽ; chỉ dùng phép chiếu
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100f;
            _camera.fieldOfView = 60f;
            SetPortrait();

            _rig = WorldStreamingRig.Build(_config, _probe.transform, null, initializeOnStart: false);
            Manager.Initialize();
            SyncCamera();
        }

        [TearDown]
        public void TearDown()
        {
            if (_rig.Pool != null) _rig.Pool.BeginTeardown();
            if (_rig.Root != null) Object.DestroyImmediate(_rig.Root);
            if (_cameraGO != null) Object.DestroyImmediate(_cameraGO);
            if (_probe != null) Object.DestroyImmediate(_probe);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        // --- helpers -------------------------------------------------------------------------

        private void SetPortrait() => _camera.aspect = 1080f / 1920f;
        private void SetTallPortrait() => _camera.aspect = 1080f / 2340f;
        private void SetLandscape() => _camera.aspect = 1920f / 1080f;

        /// <summary>Đặt camera đúng tư thế mà CameraFollow sẽ đưa nó tới cho vị trí hiện tại của probe.</summary>
        private void SyncCamera()
        {
            _cameraGO.transform.position = _probe.transform.position + CameraOffset;
            _cameraGO.transform.rotation = Quaternion.Euler(CameraEuler);
        }

        private void MoveTo(Vector3 world)
        {
            _probe.transform.position = world;
            Manager.RefreshNow();
            SyncCamera();
        }

        private GroundCoverage.Footprint Footprint() => GroundCoverage.ComputeFootprint(_camera);

        /// <summary>Tập toạ độ có nền ĐANG BẬT — không phải tập lease, mà là thứ thật sự vẽ ra.</summary>
        private HashSet<ChunkCoord> EnabledGroundCoords()
        {
            var set = new HashSet<ChunkCoord>();
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                MeshRenderer r = lease.Value.GroundRenderer;
                if (r != null && r.enabled && r.gameObject.activeInHierarchy) set.Add(lease.Key);
            }
            return set;
        }

        /// <summary>
        /// Khẳng định TỪNG ĐIỂM trên vệt chiếu rơi vào một chunk nền đang bật, và còn dư dải đệm.
        ///
        /// Lấy mẫu lưới trên vệt chiếu chứ không chỉ so hai hình chữ nhật: một lỗ thủng ở giữa ring
        /// vẫn giữ nguyên hình bao ngoài, nên so hình bao sẽ bỏ sót đúng loại lỗi tệ nhất.
        /// </summary>
        private void AssertCovered(string context)
        {
            GroundCoverage.Footprint f = Footprint();
            Assert.IsFalse(f.SeesHorizon,
                $"{context}: camera nhìn thấy đường chân trời — không ring hữu hạn nào phủ nổi.");

            HashSet<ChunkCoord> enabled = EnabledGroundCoords();

            for (int i = 0; i <= 12; i++)
            for (int j = 0; j <= 12; j++)
            {
                float x = Mathf.Lerp(f.MinX, f.MaxX, i / 12f);
                float z = Mathf.Lerp(f.MinZ, f.MaxZ, j / 12f);
                var coord = ChunkCoord.FromWorld(x, z, ChunkSize);
                Assert.IsTrue(enabled.Contains(coord),
                    $"{context}: điểm nhìn thấy ({x:F1},{z:F1}) thuộc chunk {coord} nhưng chunk đó " +
                    $"không có nền đang bật. Người chơi ở {_probe.transform.position:F1}, " +
                    $"gốc ring {Manager.CurrentChunk}.");
            }

            // Đo từ TÂM PHỦ, tức tâm thật của ring. Ở bộ test này không camera nào được gắn vào
            // manager nên tâm phủ luôn bằng ô người chơi; dùng đúng tên vẫn quan trọng, vì nó nói ra
            // cái đang được đo thay vì trùng hợp mà đúng.
            float margin = GroundCoverage.MarginToRing(f, Manager.CurrentChunk, Radius, ChunkSize);
            Assert.GreaterOrEqual(margin, GuardBand,
                $"{context}: chỉ còn {margin:F2} m đất ngoài mép màn hình, dưới dải đệm " +
                $"{GuardBand} m — lớp đất kế tiếp sẽ hiện ra sau khi người chơi đã tới sát mép.");
        }

        // --- 1/2. hai phép chiếu ---------------------------------------------------------------

        [Test]
        public void PerspectiveProductionCamera_FootprintIsCovered()
        {
            _camera.orthographic = false;
            AssertCovered("perspective fov60");
        }

        [Test]
        public void OrthographicSize10_FootprintIsCovered()
        {
            _camera.orthographic = true;
            _camera.orthographicSize = 10f;
            AssertCovered("orthographic size 10");
        }

        // --- 3/4/5. ba tỉ lệ khung hình ---------------------------------------------------------

        [Test]
        public void PortraitAspect_IsCovered()
        {
            SetPortrait();
            AssertCovered("portrait 1080x1920");
        }

        [Test]
        public void TallPortraitAspect_IsCovered()
        {
            SetTallPortrait();
            AssertCovered("tall portrait 1080x2340");
        }

        [Test]
        public void LandscapeAspect_IsCovered()
        {
            SetLandscape();
            AssertCovered("landscape 1920x1080");
        }

        // --- 6/7/8/9/10. hướng đi, dấu toạ độ, băng qua gốc -------------------------------------

        /// <summary>
        /// Đi thật theo từng bước nhỏ qua NHIỀU ranh giới chunk, kiểm mọi bước — và đi bằng cách dời
        /// transform rồi để <c>LateUpdate</c> tự bắt, tức là đúng đường mã của lúc chơi, không phải
        /// đường refresh thủ công.
        /// </summary>
        private IEnumerator Traverse(Vector3 direction, string context)
        {
            _probe.transform.position = new Vector3(4f, 0f, 4f);
            Manager.RefreshNow();

            Vector3 step = direction.normalized * 3.7f;   // bước lệch pha với 32 m để rơi vào mọi vị trí trong chunk
            for (int i = 0; i < 40; i++)
            {
                _probe.transform.position += step;
                yield return null;                        // LateUpdate của manager chạy ở đây
                SyncCamera();
                AssertCovered($"{context} bước {i} tại {_probe.transform.position:F1}");
            }
        }

        [UnityTest] public IEnumerator TraversePlusX_StaysCovered() { yield return Traverse(Vector3.right, "+X"); }
        [UnityTest] public IEnumerator TraverseMinusX_StaysCovered() { yield return Traverse(Vector3.left, "-X"); }
        [UnityTest] public IEnumerator TraversePlusZ_StaysCovered() { yield return Traverse(Vector3.forward, "+Z"); }
        [UnityTest] public IEnumerator TraverseMinusZ_StaysCovered() { yield return Traverse(Vector3.back, "-Z"); }

        [UnityTest]
        public IEnumerator TraverseDiagonals_StayCovered()
        {
            yield return Traverse(new Vector3(1f, 0f, 1f), "diagonal +X+Z");
            yield return Traverse(new Vector3(-1f, 0f, 1f), "diagonal -X+Z");
        }

        [UnityTest]
        public IEnumerator CrossingWorldOrigin_StaysCovered()
        {
            // Băng qua 0 theo cả hai trục: đây là chỗ floor() sai dấu sẽ lộ ra.
            _probe.transform.position = new Vector3(20f, 0f, 20f);
            Manager.RefreshNow();

            for (int i = 0; i < 30; i++)
            {
                _probe.transform.position += new Vector3(-1.6f, 0f, -1.6f);
                yield return null;
                SyncCamera();
                AssertCovered($"zero crossing tại {_probe.transform.position:F1}");
            }

            Assert.Less(_probe.transform.position.x, 0f, "chưa thật sự băng qua gốc toạ độ.");
        }

        [Test]
        public void DeepNegativeCoordinates_AreCovered()
        {
            MoveTo(new Vector3(-417.3f, 0f, -923.8f));
            AssertCovered("toạ độ âm sâu");
            Assert.AreEqual(new ChunkCoord(-14, -29), Manager.CurrentChunk);
        }

        [Test]
        public void DeepPositiveCoordinates_AreCovered()
        {
            MoveTo(new Vector3(1288.4f, 0f, 655.1f));
            AssertCovered("toạ độ dương sâu");
        }

        // --- 11. ngay trước và ngay sau ngưỡng refresh -------------------------------------------

        [Test]
        public void StandingEitherSideOfARefreshThreshold_IsCovered()
        {
            MoveTo(new Vector3(31.99f, 0f, 31.99f));      // sát mép trên của chunk (0,0)
            Assert.AreEqual(new ChunkCoord(0, 0), Manager.CurrentChunk);
            AssertCovered("ngay trước ngưỡng");

            MoveTo(new Vector3(32.01f, 0f, 32.01f));      // vừa qua, chunk (1,1)
            Assert.AreEqual(new ChunkCoord(1, 1), Manager.CurrentChunk);
            AssertCovered("ngay sau ngưỡng");

            MoveTo(new Vector3(-0.01f, 0f, -0.01f));      // mép âm, nơi truncate-toward-zero sẽ sai
            Assert.AreEqual(new ChunkCoord(-1, -1), Manager.CurrentChunk);
            AssertCovered("mép âm của ngưỡng");
        }

        // --- 12. teleport ------------------------------------------------------------------------

        [Test]
        public void TeleportFarAway_IsCoveredImmediately()
        {
            MoveTo(new Vector3(6f, 0f, 6f));
            MoveTo(new Vector3(9_000f, 0f, -7_500f));     // không giao nhau chút nào với ring cũ

            AssertCovered("ngay sau teleport");
            Assert.AreEqual(ExpectedActive, Manager.LastIncomingCount,
                "teleport không giao nhau phải cấp lại toàn bộ ring trong đúng một lần refresh.");
            Assert.AreEqual(ExpectedActive, Manager.ActiveLeaseCount);
        }

        // --- 13. đứng yên thì không refresh lặp ---------------------------------------------------

        [UnityTest]
        public IEnumerator StationaryTargetAndCamera_DoNotRefreshRepeatedly()
        {
            MoveTo(new Vector3(16f, 0f, 16f));            // giữa chunk, xa mọi ngưỡng
            int before = Manager.RefreshCount;

            for (int i = 0; i < 60; i++) yield return null;

            Assert.AreEqual(before, Manager.RefreshCount,
                "ring dựng lại trong khi người chơi và camera đứng yên — đây là dao động refresh.");
        }

        // --- 14/15. lease, toạ độ trùng, số nền đang bật ------------------------------------------

        [Test]
        public void ActiveSet_HasNoDuplicatesAndExactlyTheConfiguredCount()
        {
            MoveTo(new Vector3(77.5f, 0f, -412.25f));

            var active = new List<ChunkCoord>();
            Manager.CopyActiveCoords(active);

            Assert.AreEqual(ExpectedActive, active.Count);
            Assert.AreEqual(ExpectedActive, new HashSet<ChunkCoord>(active).Count, "có toạ độ trùng trong ring.");
            Assert.AreEqual(ExpectedActive, EnabledGroundCoords().Count,
                "số chunk có nền đang bật khác số chunk active.");
            Assert.AreEqual(_config.ActiveChunkCount, Manager.ActiveLeaseCount);
        }

        // --- 16/17. đã nằm trong AssertCovered (điểm-theo-điểm + dải đệm) --------------------------

        [Test]
        public void GuardBandIsSatisfiedInEveryTestedConfiguration()
        {
            float worst = float.MaxValue;
            string worstAt = "";

            foreach (bool ortho in new[] { false, true })
            foreach (float aspect in new[] { 1080f / 1920f, 1080f / 2340f, 1920f / 1080f })
            {
                _camera.orthographic = ortho;
                _camera.orthographicSize = 10f;
                _camera.aspect = aspect;

                // quét mọi vị trí trong một chunk: dải đệm mỏng nhất nằm ở mép chunk, không phải ở giữa
                for (float ox = 0f; ox < ChunkSize; ox += 4f)
                for (float oz = 0f; oz < ChunkSize; oz += 4f)
                {
                    MoveTo(new Vector3(ox, 0f, oz));
                    float margin = GroundCoverage.MarginToRing(Footprint(), Manager.CurrentChunk, Radius, ChunkSize);
                    if (margin < worst)
                    {
                        worst = margin;
                        worstAt = $"ortho={ortho} aspect={aspect:F3} offset=({ox:F0},{oz:F0})";
                    }
                }
            }

            Assert.GreaterOrEqual(worst, GuardBand,
                $"dải đệm mỏng nhất là {worst:F2} m tại {worstAt}, dưới mức {GuardBand} m.");
        }

        // --- 18. đúng MỘT collider gameplay dùng chung ---------------------------------------------

        [Test]
        public void ExactlyOneSharedGameplayCollider_AndNoDecorationColliders()
        {
            MoveTo(new Vector3(140f, 0f, -260f));

            Collider[] colliders = _rig.Root.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length,
                "phải có đúng một collider gameplay dùng chung trong toàn nhánh streaming.");
            Assert.AreEqual(_rig.Surface.gameObject, colliders[0].gameObject);
        }

        // --- 19. nền có ngay, kể cả khi trang trí còn đang chờ --------------------------------------

        [Test]
        public void GroundIsImmediateOnAssignment_EvenBeforeDecorationRuns()
        {
            MoveTo(new Vector3(512f, 0f, 512f));          // ring hoàn toàn mới, chưa frame nào trôi qua

            Assert.AreEqual(ExpectedActive, EnabledGroundCoords().Count,
                "nền phải xong ngay trong lần refresh, không được hoãn cùng trang trí.");
            AssertCovered("ngay sau khi cấp ring mới, trước khi trang trí chạy");
        }

        // --- 20. bậc mật độ đúng sau khi gốc dịch ----------------------------------------------------

        [Test]
        public void RetainedChunksCarryTheCorrectDensityTierAfterTheOriginShifts()
        {
            MoveTo(new Vector3(16f, 0f, 16f));
            MoveTo(new Vector3(48f, 0f, 16f));            // dịch gốc một ô: 20/25 chunk được GIỮ LẠI
            MoveTo(new Vector3(48f, 0f, 48f));            // dịch tiếp theo trục kia

            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                DecorationDensityTier expected = _config.TierFor(lease.Key, Manager.CurrentChunk);
                Assert.AreEqual(expected, lease.Value.DecorationTier,
                    $"chunk {lease.Key} giữ bậc mật độ cũ sau khi gốc dịch tới {Manager.CurrentChunk}.");
            }
        }

        // --- phép toán không được rỗng nghĩa ----------------------------------------------------------

        /// <summary>
        /// Chứng minh phép đo BẮT được lỗi thật, chứ không phải luôn luôn đúng.
        ///
        /// Không có test này thì mọi khẳng định "đã phủ" ở trên đều vô nghĩa: một hàm luôn trả về
        /// "đã phủ" cũng sẽ làm chúng xanh hết.
        /// </summary>
        [Test]
        public void TheCoverageCalculationFailsWhenItShould()
        {
            MoveTo(new Vector3(16f, 0f, 16f));

            // a) camera ngẩng lên nhìn chân trời -> không phủ nổi
            _cameraGO.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            Assert.IsTrue(GroundCoverage.ComputeFootprint(_camera).SeesHorizon,
                "camera nhìn gần như song song mặt đất mà vẫn báo là phủ được.");

            // b) vệt chiếu quá rộng so với ring -> lộ nền
            SyncCamera();
            _camera.orthographic = true;
            _camera.orthographicSize = 200f;
            GroundCoverage.Footprint wide = GroundCoverage.ComputeFootprint(_camera);
            Assert.IsFalse(GroundCoverage.IsFullyCovered(wide, Manager.CurrentChunk, Radius, ChunkSize),
                "vệt chiếu rộng 400 m trên ring 160 m mà vẫn báo là đã phủ.");

            // c) gốc ring lệch xa người chơi -> lộ nền
            _camera.orthographic = false;
            _camera.orthographicSize = 10f;
            GroundCoverage.Footprint normal = GroundCoverage.ComputeFootprint(_camera);
            Assert.IsFalse(GroundCoverage.IsFullyCovered(normal, new ChunkCoord(40, 40), Radius, ChunkSize),
                "ring đặt cách xa 1200 m mà vẫn báo là phủ được tầm nhìn.");
            Assert.IsTrue(GroundCoverage.IsFullyCovered(normal, Manager.CurrentChunk, Radius, ChunkSize, GuardBand),
                "ring đúng tâm lại báo là không phủ.");
        }
    }
}
