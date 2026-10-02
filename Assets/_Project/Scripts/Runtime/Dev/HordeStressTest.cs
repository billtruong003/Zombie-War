using UnityEngine;
using BillGameCore;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
using System.Collections;
using System.Text;
using Unity.Profiling;
#endif

namespace ZombieWar
{
    /// <summary>
    /// Development-only instrumentation for the horde spawner: hold N zombies alive for a fixed
    /// window and report what that costs. Exists because "does the pool scale to 100 alive" is a
    /// measurement, not an opinion, and the answer decides how far later stages can push.
    ///
    /// Self-installing and console-driven, so it needs no scene object and no UI:
    ///   zw.horde.watch   - start measuring the run that is already happening
    ///   zw.horde.stress  - pause the threat director, hold 100 alive for 30s, report, clean up
    ///   zw.horde.report  - print the current window and stop measuring
    /// Turn god mode on first (zw.god) or the player dies before the window closes.
    /// </summary>
    public sealed class HordeStressTest : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
        /// <summary>Alive count the stress window holds. Settable so a density ceiling can be
        /// measured at several sizes (100 / 150 / 200) without editing code.</summary>
        public static int StressTargetAlive { get; set; } = 100;

        /// <summary>Last closed window: average FPS, average PlayerLoop CPU ms, peak alive.</summary>
        public static (float avgFps, float playerLoopMs, int peakAlive) LastReport { get; private set; }
        private const float StressDuration = 30f;
        private const float TopUpInterval = 0.1f;
        private const int TopUpBatch = 10;

        private static HordeStressTest _instance;

        private bool _measuring;
        private float _windowStart;
        private int _frames;
        private float _elapsed;
        private float _worstFrameTime;
        private int _peakAlive;
        private long _gcAllocated;
        private ProfilerRecorder _gcRecorder;
        // PlayerLoop CPU time, not wall-clock frame time: an unfocused editor throttles its frame
        // rate, so FPS alone measures the throttle rather than the horde.
        private ProfilerRecorder _loopRecorder;
        private double _loopNanos;
        private int _loopSamples;
        private Coroutine _stress;
        private string _label = "run";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("[ZombieWar.HordeStress]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HordeStressTest>();
        }

        private bool _registered;

        private void Update()
        {
            if (!_registered && Bill.IsReady) RegisterCommands();
            if (!_measuring) return;

            _frames++;
            _elapsed += Time.unscaledDeltaTime;
            if (Time.unscaledDeltaTime > _worstFrameTime) _worstFrameTime = Time.unscaledDeltaTime;
            if (ZombieManager.AliveCount > _peakAlive) _peakAlive = ZombieManager.AliveCount;
            if (_gcRecorder.Valid) _gcAllocated += _gcRecorder.LastValue;
            if (_loopRecorder.Valid && _loopRecorder.LastValue > 0) { _loopNanos += _loopRecorder.LastValue; _loopSamples++; }
        }

        private void OnDestroy() => StopMeasuring();

        private void RegisterCommands()
        {
            var cheat = Bill.Cheat;
            if (cheat == null) return;
            cheat.Register("zw.horde.watch", () => StartMeasuring("run"), "Measure the current run");
            cheat.Register("zw.horde.stress", StartStress, "Hold StressTargetAlive (default 100) zombies for 30s and report");
            cheat.Register("zw.horde.report", Report, "Print + end the current measurement window");
            _registered = true;
        }

        private void StartMeasuring(string label)
        {
            StopMeasuring();
            _label = label;
            _frames = 0;
            _elapsed = 0f;
            _worstFrameTime = 0f;
            _peakAlive = 0;
            _gcAllocated = 0;
            _windowStart = Time.unscaledTime;
            _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _loopRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop");
            _loopNanos = 0;
            _loopSamples = 0;
            _measuring = true;
            Debug.Log($"[HordeStress] measuring '{label}' - call zw.horde.report to end the window.");
        }

        private void StopMeasuring()
        {
            _measuring = false;
            if (_gcRecorder.Valid) _gcRecorder.Dispose();
            if (_loopRecorder.Valid) _loopRecorder.Dispose();
        }

        /// <summary>Starts a stress window from code (editor tooling / MCP), same as the cheat command.</summary>
        public static void Run(int alive)
        {
            if (_instance == null) return;
            StressTargetAlive = Mathf.Max(1, alive);
            _instance.StartStress();
        }

        private void StartStress()
        {
            if (_stress != null) StopCoroutine(_stress);
            _stress = StartCoroutine(StressRoutine());
        }

        private IEnumerator StressRoutine()
        {
            var director = Threat.ThreatDirector.Instance;
            var spawner = FindFirstObjectByType<ZombieSpawner>();
            var data = director != null ? director.PickFor(0) : null;

            if (spawner == null || data == null)
            {
                Debug.LogError("[HordeStress] No ThreatDirector/ZombieSpawner/ZombieData in the loaded " +
                               "scene - start a run first.");
                yield break;
            }

            // The director must not fight the stress loop over the alive count.
            director.enabled = false;
            Bill.Pool?.WarmUp(ZombieSpawner.KeyFor(data), StressTargetAlive);
            spawner.EnsureRegistered(data, StressTargetAlive);

            StartMeasuring($"stress {StressTargetAlive} alive");

            // Real time: a level-up card sets the time scale to 0, and a scaled wait would never
            // come back to close it.
            var wait = new WaitForSecondsRealtime(TopUpInterval);
            float until = Time.unscaledTime + StressDuration;

            while (Time.unscaledTime < until)
            {
                CloseLevelUp();
                int want = Mathf.Min(TopUpBatch, StressTargetAlive - ZombieManager.AliveCount);
                if (want > 0)
                {
                    spawner.BeginBatch();
                    for (int i = 0; i < want; i++) spawner.Spawn(data);
                }
                yield return wait;
            }

            Report();

            // Return the stress instances directly rather than firing GameOverEvent - the field has
            // to be cleared without dragging the lose screen and the whole run-end flow in with it.
            Bill.Pool?.ReturnAll(ZombieSpawner.KeyFor(data));
            if (director != null) director.enabled = true;
            _stress = null;
        }

        private RunOverlays _overlays;

        // Kills earn XP, and a level-up card pauses the game for up to 30 s: the 02/10 volcano run
        // held 61 alive instead of 100 because the window spent most of its time paused. Close the
        // card without a pick, as the skill sandbox does, so the window measures a moving horde.
        private void CloseLevelUp()
        {
            if (_overlays == null) _overlays = FindFirstObjectByType<RunOverlays>(FindObjectsInactive.Include);
            if (_overlays == null) return;
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(RunOverlays);
            var root = t.GetField("levelUpRoot", F)?.GetValue(_overlays) as GameObject;
            if (root == null || !root.activeSelf) return;
            t.GetField("_pendingLevelUps", F)?.SetValue(_overlays, 0);
            t.GetField("_skillOffer", F)?.SetValue(_overlays, null);
            root.SetActive(false);
            Time.timeScale = 1f;
        }

        private void Report()
        {
            if (!_measuring)
            {
                Debug.LogWarning("[HordeStress] no measurement window is open.");
                return;
            }

            float window = Time.unscaledTime - _windowStart;
            float avgFps = _elapsed > 0f ? _frames / _elapsed : 0f;
            float worstFps = _worstFrameTime > 0f ? 1f / _worstFrameTime : 0f;
            float allocPerSecond = window > 0f ? _gcAllocated / window : 0f;

            float loopMs = _loopSamples > 0 ? (float)(_loopNanos / _loopSamples / 1e6) : 0f;
            LastReport = (avgFps, loopMs, _peakAlive);
            Debug.Log($"[HordeStress] SUMMARY alive={_peakAlive} playerLoopMs={loopMs:0.00} avgFps={avgFps:0.0} worstMs={_worstFrameTime * 1000f:0.0} gcKBps={allocPerSecond / 1024f:0.0}");

            var sb = new StringBuilder(320);
            sb.AppendLine($"[HordeStress] '{_label}' over {window:0.0}s ({_frames} frames)");
            sb.AppendLine($"  FPS       avg {avgFps:0.0} | worst {worstFps:0.0} (worst frame {_worstFrameTime * 1000f:0.0} ms)");
            sb.AppendLine($"  Peak      alive {_peakAlive}");
            sb.AppendLine($"  GC alloc  {_gcAllocated / 1024f:0.0} KB total | {allocPerSecond / 1024f:0.0} KB/s");
            Debug.Log(sb.ToString());

            StopMeasuring();
        }
#endif
    }
}
