using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5 Gate 6 — ma trận đúng-sai của mặt đất, chạy TỰ ĐỘNG chứ không lái tay 48 ca.
    ///
    /// 6 cấu hình camera × 8 kiểu di chuyển. Mỗi ô khẳng định lại toàn bộ bất biến streaming, và cả
    /// bảng được ghi ra Review/ để đọc lại mà không cần chạy lại Unity.
    ///
    /// Chính sách production được giữ nguyên: Occlusion Culling tắt bằng code, frustum culling vẫn
    /// chạy, ring 5×5 bám người chơi. Test này KHÔNG khôi phục coverage nhận biết camera và KHÔNG nới
    /// bán kính ring — nếu một ô đỏ thì đó là lỗi thật, không phải lý do để nới cấu hình.
    /// </summary>
    public class GroundMatrixAcceptanceTests
    {
        private const float ChunkSize = 32f;
        private const int Radius = 2;
        private const int ExpectedRoots = 25;
        private const int ExpectedRenderers = 75;   // Ground + SolidDecor + Foliage cho mỗi chunk

        private static readonly Vector3 CameraOffset = new Vector3(0f, 12f, -8f);
        private static readonly Vector3 CameraEuler = new Vector3(60f, 0f, 0f);

        private WorldStreamingConfig _config;
        private GameObject _probe;
        private GameObject _cameraGO;
        private Camera _camera;
        private WorldStreamingRig.Rig _rig;

        private WorldStreamManager Manager => _rig.Manager;

        private readonly struct CameraCase
        {
            public readonly string Name;
            public readonly float Aspect;
            public readonly bool Orthographic;

            public CameraCase(string name, float aspect, bool orthographic)
            {
                Name = name; Aspect = aspect; Orthographic = orthographic;
            }
        }

        private static readonly CameraCase[] Cameras =
        {
            new CameraCase("portrait perspective",      1080f / 1920f, false),
            new CameraCase("portrait orthographic",     1080f / 1920f, true),
            new CameraCase("tall portrait perspective", 1080f / 2340f, false),
            new CameraCase("tall portrait orthographic",1080f / 2340f, true),
            new CameraCase("landscape perspective",     1920f / 1080f, false),
            new CameraCase("landscape orthographic",    1920f / 1080f, true),
        };

        private static readonly (string Name, Vector3 Start, Vector3 Step, int Steps)[] Traversals =
        {
            ("+X",                 new Vector3(  16f, 0f,  16f), new Vector3( 7.3f, 0f,  0f),    14),
            ("-X",                 new Vector3(  16f, 0f,  16f), new Vector3(-7.3f, 0f,  0f),    14),
            ("+Z",                 new Vector3(  16f, 0f,  16f), new Vector3( 0f,   0f,  7.3f),  14),
            ("-Z",                 new Vector3(  16f, 0f,  16f), new Vector3( 0f,   0f, -7.3f),  14),
            ("diagonal",           new Vector3(  16f, 0f,  16f), new Vector3( 5.1f, 0f,  5.1f),  14),
            ("zero crossing",      new Vector3(  40f, 0f,  40f), new Vector3(-6.7f, 0f, -6.7f),  16),
            ("negative coords",    new Vector3(-417f, 0f,-923f), new Vector3(-5.9f, 0f, -5.9f),  12),
            ("teleport",           new Vector3(   8f, 0f,   8f), new Vector3(9000f, 0f,-7500f),   2),
        };

        [SetUp]
        public void SetUp()
        {
            _config = WorldStreamingConfig.CreateRuntime();
            _probe = new GameObject("MatrixProbe");
            _probe.transform.position = new Vector3(16f, 0f, 16f);

            _cameraGO = new GameObject("MatrixCamera");
            _camera = _cameraGO.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100f;
            _camera.fieldOfView = 60f;
            _camera.orthographicSize = 10f;
            _camera.useOcclusionCulling = false;   // chính sách production

            _rig = WorldStreamingRig.Build(_config, _probe.transform, null, initializeOnStart: false);
            Manager.Initialize();
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

        private void PoseCamera() =>
            _cameraGO.transform.SetPositionAndRotation(
                _probe.transform.position + CameraOffset, Quaternion.Euler(CameraEuler));

        /// <summary>Kiểm mọi bất biến của MỘT bước. Trả về mô tả lỗi, hoặc null nếu đạt.</summary>
        private string CheckCell()
        {
            if (_camera.useOcclusionCulling) return "camera.useOcclusionCulling bật trở lại";
            if (_config.RenderRadius != Radius) return $"ring radius đổi thành {_config.RenderRadius}";

            var active = new List<ChunkCoord>();
            Manager.CopyActiveCoords(active);
            if (active.Count != ExpectedRoots) return $"active leases = {active.Count}";
            if (new HashSet<ChunkCoord>(active).Count != ExpectedRoots) return "có toạ độ trùng";
            if (_rig.Pool.Capacity != ExpectedRoots) return $"pool capacity = {_rig.Pool.Capacity}";
            if (_rig.Pool.RootsCreatedSinceWarmup != 0) return "tạo thêm chunk root sau warmup";
            if (_rig.Pool.RootsDestroyedDuringTraversal != 0) return "huỷ chunk root khi di chuyển";

            int renderers = 0, groundOn = 0;
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                renderers += 3;
                var r = lease.Value.GroundRenderer;
                if (r != null && r.enabled && r.gameObject.activeInHierarchy) groundOn++;
            }
            if (renderers != ExpectedRenderers) return $"chunk renderers = {renderers}";
            if (groundOn != ExpectedRoots) return $"ground renderers bật = {groundOn}";

            var colliders = _rig.Root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length != 1) return $"shared gameplay colliders = {colliders.Length}";

            // Người chơi phải nằm trong ring
            var playerChunk = ChunkCoord.FromWorld(_probe.transform.position, ChunkSize);
            if (!active.Contains(playerChunk)) return $"chunk người chơi {playerChunk} không nằm trong ring";

            // Không được có mảng đất nhìn thấy nào thiếu
            GroundCoverage.Footprint f = GroundCoverage.ComputeFootprint(_camera);
            if (f.SeesHorizon) return "camera nhìn thấy chân trời";

            var enabled = new HashSet<ChunkCoord>();
            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in Manager.ActiveLeases)
            {
                var r = lease.Value.GroundRenderer;
                if (r != null && r.enabled) enabled.Add(lease.Key);
            }

            for (int i = 0; i <= 8; i++)
            for (int j = 0; j <= 8; j++)
            {
                float x = Mathf.Lerp(f.MinX, f.MaxX, i / 8f);
                float z = Mathf.Lerp(f.MinZ, f.MaxZ, j / 8f);
                if (!enabled.Contains(ChunkCoord.FromWorld(x, z, ChunkSize)))
                    return $"điểm nhìn thấy ({x:F1},{z:F1}) không có nền";
            }

            Manager.DrainDecoration();
            if (!Manager.IsGenerationSettled) return "thế giới không hội tụ";

            return null;
        }

        [Test]
        public void GroundMatrix_AllCellsPass()
        {
            var report = new StringBuilder();
            report.AppendLine("# Gate 6 — ground correctness matrix (48 cells)");
            report.AppendLine();
            report.AppendLine("Occlusion Culling OFF, frustum culling active, player-centred 5x5 ring, radius unchanged.");
            report.AppendLine();
            report.AppendLine("| camera | traversal | steps | result |");
            report.AppendLine("|---|---|---:|---|");

            int pass = 0, fail = 0;
            var failures = new List<string>();

            foreach (CameraCase cam in Cameras)
            {
                _camera.aspect = cam.Aspect;
                _camera.orthographic = cam.Orthographic;

                foreach ((string name, Vector3 start, Vector3 step, int steps) in Traversals)
                {
                    _probe.transform.position = start;
                    Manager.RefreshNow();
                    PoseCamera();

                    string failure = null;
                    for (int s = 0; s < steps && failure == null; s++)
                    {
                        _probe.transform.position += step;
                        Manager.RefreshNow();
                        PoseCamera();
                        failure = CheckCell();
                        if (failure != null) failure = $"step {s}: {failure}";
                    }

                    if (failure == null) { pass++; report.AppendLine($"| {cam.Name} | {name} | {steps} | PASS |"); }
                    else
                    {
                        fail++;
                        report.AppendLine($"| {cam.Name} | {name} | {steps} | **FAIL** — {failure} |");
                        failures.Add($"{cam.Name} / {name}: {failure}");
                    }
                }
            }

            report.AppendLine();
            report.AppendLine($"**{pass} PASS / {fail} FAIL out of {pass + fail} cells.**");

            string dir = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), "Review/M5_RenderingCloseout/AcceptanceFinal");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "GroundMatrix.md"), report.ToString());

            Assert.AreEqual(48, pass + fail, "ma trận không chạy đủ 48 ô");
            Assert.IsEmpty(failures, "ô đỏ:\n" + string.Join("\n", failures));
        }
    }
}
