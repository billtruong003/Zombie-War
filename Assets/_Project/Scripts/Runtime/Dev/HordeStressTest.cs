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
    ///   zw.horde.watch   - start measuring the run that is already happening (a real Stage 1 pass)
    ///   zw.horde.stress  - stop the wave director, hold 100 alive for 30s, report, clean up
    ///   zw.horde.report  - print the current window and stop measuring
    /// Turn god mode on first (zw.god) or the player dies before the window closes.
    /// </summary>
    public sealed class HordeStressTest : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
        private const int StressTargetAlive = 100;
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
        private int _peakVisible;
        private long _gcAllocated;
        private ProfilerRecorder _gcRecorder;
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
            if (ZombieManager.VisibleCount > _peakVisible) _peakVisible = ZombieManager.VisibleCount;
            if (_gcRecorder.Valid) _gcAllocated += _gcRecorder.LastValue;
        }

        private void OnDestroy() => StopMeasuring();

        private void RegisterCommands()
        {
            var cheat = Bill.Cheat;
            if (cheat == null) return;
            cheat.Register("zw.horde.watch", () => StartMeasuring("run"), "Measure the current run");
            cheat.Register("zw.horde.stress", StartStress, "Hold 100 alive zombies for 30s and report");
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
            _peakVisible = 0;
            _gcAllocated = 0;
            _windowStart = Time.unscaledTime;
            _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _measuring = true;
            Debug.Log($"[HordeStress] measuring '{label}' - call zw.horde.report to end the window.");
        }

        private void StopMeasuring()
        {
            _measuring = false;
            if (_gcRecorder.Valid) _gcRecorder.Dispose();
        }

        private void StartStress()
        {
            if (_stress != null) StopCoroutine(_stress);
            _stress = StartCoroutine(StressRoutine());
        }

        private IEnumerator StressRoutine()
        {
            var director = FindFirstObjectByType<WaveDirector>();
            var spawner = director != null ? director.GetComponent<ZombieSpawner>() : null;
            var data = FirstZombie(director);

            if (spawner == null || data == null)
            {
                Debug.LogError("[HordeStress] No WaveDirector/ZombieSpawner/ZombieData in the loaded " +
                               "scene - start a stage first.");
                yield break;
            }

            // The director must not fight the stress loop over the alive count.
            director.StopRun();
            Bill.Pool?.WarmUp(ZombieSpawner.KeyFor(data), StressTargetAlive);
            spawner.EnsureRegistered(data, StressTargetAlive);

            StartMeasuring($"stress {StressTargetAlive} alive");

            var wait = new WaitForSeconds(TopUpInterval);
            float until = Time.unscaledTime + StressDuration;

            while (Time.unscaledTime < until)
            {
                int want = Mathf.Min(TopUpBatch, StressTargetAlive - ZombieManager.AliveCount);
                if (want > 0)
                {
                    spawner.BeginBatch();
                    for (int i = 0; i < want; i++) spawner.Spawn(data, ZombieSpawner.SpawnBand.Normal);
                }
                yield return wait;
            }

            Report();

            // Return the stress instances directly rather than firing GameOverEvent - the field has
            // to be cleared without dragging the lose screen and the whole run-end flow in with it.
            Bill.Pool?.ReturnAll(ZombieSpawner.KeyFor(data));
            _stress = null;
        }

        private static ZombieData FirstZombie(WaveDirector director)
        {
            var waves = director != null ? director.Waves : null;
            if (waves?.waves == null) return null;
            foreach (var wave in waves.waves)
            {
                if (wave?.entries == null) continue;
                foreach (var entry in wave.entries)
                    if (entry.zombie != null && entry.zombie.prefab != null) return entry.zombie;
            }
            return null;
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

            var sb = new StringBuilder(320);
            sb.AppendLine($"[HordeStress] '{_label}' over {window:0.0}s ({_frames} frames)");
            sb.AppendLine($"  FPS       avg {avgFps:0.0} | worst {worstFps:0.0} (worst frame {_worstFrameTime * 1000f:0.0} ms)");
            sb.AppendLine($"  Peak      alive {_peakAlive} | visible {_peakVisible}");
            sb.AppendLine($"  GC alloc  {_gcAllocated / 1024f:0.0} KB total | {allocPerSecond / 1024f:0.0} KB/s");
            Debug.Log(sb.ToString());

            StopMeasuring();
        }
#endif
    }
}
