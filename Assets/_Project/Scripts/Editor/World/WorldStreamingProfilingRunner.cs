using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Chạy bộ benchmark của `WorldStreamingLab` TRẢI THEO FRAME và ghi báo cáo ra ngoài `Assets/`.
    ///
    /// Bản M3A chạy cả kịch bản trong một tick Editor, làm treo Editor hàng chục giây và biến
    /// "steady state" thành 240 lời gọi hàm chứ không phải 240 khung hình. Ở đây mỗi frame PlayMode
    /// thật chỉ tiến đúng một bước.
    ///
    /// Bất cứ đường thoát nào — xong, huỷ, ngoại lệ, thoát Play Mode — đều phải để lại profiler ở
    /// trạng thái tắt.
    /// </summary>
    public static class WorldStreamingProfilingRunner
    {
        public static string OutputFolder => Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? ".", "Temp", "WorldStreamingProfiling");

        public static string EnvironmentLabel()
        {
            string where = Application.isEditor ? "UNITY EDITOR (not a device measurement)" : "PLAYER";
            return $"{where} · Unity {Application.unityVersion} · target {EditorUserBuildSettings.activeBuildTarget} · " +
                   $"RP {(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "built-in")} · " +
                   $"{SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName} · {SystemInfo.processorType}";
        }

        private static readonly Queue<BenchmarkScenario> Pending = new Queue<BenchmarkScenario>();
        private static readonly StringBuilder Accumulated = new StringBuilder(16384);
        private static readonly FrameSpreadBenchmark Benchmark = new FrameSpreadBenchmark();
        private static bool _hooked;

        public static bool IsRunning { get; private set; }
        public static string LastReport { get; private set; } = string.Empty;
        public static string LastReportPath { get; private set; } = string.Empty;

        /// <summary>Xếp hàng cả bốn kịch bản (trải theo frame) rồi trả về ngay.</summary>
        public static string StartAll()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("[WorldStreaming] Benchmark phải chạy trong Play Mode.");
            if (IsRunning) return "đang chạy";

            Pending.Clear();
            Pending.Enqueue(BenchmarkScenario.SteadyState);
            Pending.Enqueue(BenchmarkScenario.AdjacentTraversal);
            Pending.Enqueue(BenchmarkScenario.TeleportStress);
            Pending.Enqueue(BenchmarkScenario.Soak);

            Accumulated.Length = 0;
            Accumulated.Append("WORLD STREAMING LAB — M3A.1 PROFILING GATE (corrected)\n");
            Accumulated.Append("generated: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            Accumulated.Append(EnvironmentLabel()).Append('\n');
            Accumulated.Append("all scenarios below are FRAME-SPREAD: one step per real PlayMode frame\n\n");

            IsRunning = true;
            LastReport = string.Empty;

            if (!_hooked)
            {
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                _hooked = true;
            }

            StartNext();
            return "đã xếp hàng 4 kịch bản (frame-spread)";
        }

        public static void Cancel()
        {
            Benchmark.Cancel();
            Pending.Clear();
            IsRunning = false;
            WorldStreamingProfiler.Stop();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!IsRunning) return;
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
            {
                Accumulated.Append("HUỶ: thoát Play Mode giữa chừng — báo cáo không đầy đủ.\n");
                Cancel();
            }
        }

        private static void StartNext()
        {
            if (Pending.Count == 0)
            {
                Finish(null);
                return;
            }

            var manager = UnityEngine.Object.FindFirstObjectByType<WorldStreamManager>();
            if (manager == null || !manager.IsInitialized)
            {
                Finish("LỖI: không tìm thấy WorldStreamManager đã khởi tạo.");
                return;
            }

            Benchmark.Start(manager, Pending.Dequeue(), EnvironmentLabel());
        }

        private static void Tick()
        {
            if (!IsRunning) return;

            if (!Application.isPlaying)
            {
                Cancel();
                return;
            }

            bool done;
            try
            {
                done = Benchmark.Tick();
            }
            catch (Exception e)
            {
                Accumulated.Append("LỖI trong ").Append(Benchmark.Scenario).Append(": ").Append(e.Message).Append('\n');
                Finish("lỗi");
                return;
            }

            if (!done) return;

            if (Benchmark.Result != null) Accumulated.Append(Benchmark.Result.ToReport()).Append('\n');
            StartNext();
        }

        private static void Finish(string note)
        {
            IsRunning = false;
            Pending.Clear();
            WorldStreamingProfiler.Stop();

            if (!string.IsNullOrEmpty(note)) Accumulated.Append(note).Append('\n');

            Directory.CreateDirectory(OutputFolder);
            LastReportPath = Path.Combine(OutputFolder, $"M3A1_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(LastReportPath, Accumulated.ToString());
            LastReport = Accumulated.ToString();
        }

        /// <summary>Microbenchmark đồng bộ — nhãn riêng, không dùng làm số liệu frame chính thức.</summary>
        public static string RunSynchronousMicrobenchmark(BenchmarkScenario scenario)
        {
            var manager = UnityEngine.Object.FindFirstObjectByType<WorldStreamManager>();
            if (manager == null || !manager.IsInitialized)
                throw new InvalidOperationException("[WorldStreaming] Không tìm thấy WorldStreamManager đã khởi tạo.");

            return WorldStreamingMicrobenchmark.Run(manager, scenario, EnvironmentLabel()).ToReport();
        }

        [MenuItem("ZombieWar/World Streaming/Run Profiling Benchmarks", priority = 120)]
        private static void RunAllMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[WorldStreaming] Vào Play Mode trong WorldStreamingLab rồi chạy lại.");
                return;
            }

            Debug.Log("[WorldStreaming] " + StartAll() + " — xem Temp/WorldStreamingProfiling/ khi xong.");
        }
    }
}
