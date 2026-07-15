using UnityEngine;
using BillGameCore;

/// <summary>
/// Drop this on any GameObject in your game scene to see BillGameCore in action.
/// Demonstrates: state machine, audio, pool, timer, events, scene, save, cheats.
/// </summary>
public class BillSampleGame : MonoBehaviour
{
    void Start()
    {
        if (!Bill.IsReady)
        {
            // Subscribe to know when framework is ready
            Bill.Events.Subscribe<GameReadyEvent>(OnReady);
            return;
        }
        OnReady(default);
    }

    void OnReady(GameReadyEvent _)
    {
        Debug.Log("[Sample] BillGameCore is ready!");

        // --- State Machine ---
        // Built-in states are already registered. Add custom ones:
        // Bill.State.AddState(new MyCustomState());

        // Transition to menu
        Bill.State.GoTo<MenuState>();

        // Listen for state changes
        Bill.State.OnEnter<GameplayState>(() => Debug.Log("[Sample] Game started!"));
        Bill.State.OnExit<GameplayState>(() => Debug.Log("[Sample] Game ended."));

        // --- Timer ---
        Bill.Timer.Delay(2f, () => Debug.Log("[Sample] 2 seconds passed."));
        var heartbeat = Bill.Timer.Repeat(1f, () => Debug.Log("[Sample] tick"), 5);

        // --- Save ---
        Bill.Save.Set("highscore", 9999);
        int score = Bill.Save.GetInt("highscore");
        Debug.Log($"[Sample] Highscore: {score}");

        // --- Events ---
        Bill.Events.Subscribe<SceneLoadCompleteEvent>(e =>
            Debug.Log($"[Sample] Scene loaded: {e.SceneName}"));

        // --- Pool ---
        // Pre-register a pool (or put prefab in Resources/Pools/Bullet)
        // Bill.Pool.Register("Bullet", bulletPrefab, warmCount: 20);
        // var obj = Bill.Pool.Spawn("Bullet", transform.position, Quaternion.identity);
        // Bill.Pool.Return(obj, delay: 3f);

        // --- Audio ---
        // Bill.Audio.Play("jump");
        // Bill.Audio.PlayMusic("bgm_menu", fade: 1.5f);
        // Bill.Audio.SetVolume(AudioChannel.Music, 0.5f);

        // --- Network (safe even without Photon) ---
        if (Bill.Net.IsOffline)
            Debug.Log("[Sample] Running offline. Network calls are no-ops.");

        // --- Cheats (dev builds only) ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Bill.Cheat.Register("win", () => Debug.Log("[Cheat] You win!"), "Instant win");
        Bill.Cheat.Register<int>("score", s => Debug.Log($"[Cheat] Score set to {s}"), "Set score");
#endif

        // --- Config ---
        // var maxHp = Bill.Config.GetInt("max_hp", fallback: 100);

        // --- Scene ---
        // Bill.Scene.Load("Level_01");
        // Bill.Scene.Load("Level_01", TransitionType.Fade, 0.5f);

        // --- Trace (the killer feature for debugging) ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Call any of these when something goes wrong:
        // Bill.Trace.Print();        // Who registered what, who uses what
        // Bill.Trace.Log();          // Last 40 service accesses
        // Bill.Trace.HealthCheck();  // Which services are still alive
        // Bill.Trace.Unused();       // Services nobody is using

        // Or type in the cheat console (press ` backtick):
        // > trace
        // > health
        // > states
#endif
    }
}
