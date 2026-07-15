using UnityEngine;
using UnityEngine.SceneManagement;
using BillGameCore;
namespace BillGameCore
{
    /// <summary>
    /// Static gateway for all framework services.
    /// Every service is accessed through a typed interface.
    /// </summary>
    public static class Bill
    {
        public static ITweenService Tween => ServiceLocator.Get<ITweenService>();
        public static ISceneService Scene => ServiceLocator.Get<ISceneService>();
        public static IPoolService Pool => ServiceLocator.Get<IPoolService>();
        public static IAudioService Audio => ServiceLocator.Get<IAudioService>();
        public static ISaveService Save => ServiceLocator.Get<ISaveService>();
        public static IUIService UI => ServiceLocator.Get<IUIService>();
        public static ITimerService Timer => ServiceLocator.Get<ITimerService>();
        public static IConfigService Config => ServiceLocator.Get<IConfigService>();
        public static IEventBus Events => ServiceLocator.Get<IEventBus>();
        public static INetworkService Net => ServiceLocator.Get<INetworkService>();
        public static GameStateMachine State => ServiceLocator.Get<GameStateMachine>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static CheatConsole Cheat => ServiceLocator.Get<CheatConsole>();
        public static DebugOverlay Debug => ServiceLocator.Get<DebugOverlay>();
        public static AnalyticsTracker Analytics => ServiceLocator.Get<AnalyticsTracker>();
#endif

#if PHOTON_FUSION
        /// <summary>
        /// Live player registry (local + remote), Shmackle-style: <c>Bill.Players.Local.HandRight</c>,
        /// <c>Bill.Players.Get(playerRef)</c>, <c>Bill.Players.All</c>. Thin forwarder over the standalone
        /// <see cref="BillPlayers"/> static (kept separate so it's usable without the ServiceLocator/Bill facade
        /// too). Populated by whichever NetworkBehaviour implements <see cref="IBillPlayer"/> and registers
        /// itself — this class holds no state of its own.
        /// </summary>
        public static class Players
        {
            public static IBillPlayer Local => BillPlayers.Local;
            public static System.Collections.Generic.IReadOnlyList<IBillPlayer> All => BillPlayers.All;
            public static IBillPlayer Get(Fusion.PlayerRef playerRef) => BillPlayers.Get(playerRef);
            public static event System.Action<IBillPlayer> PlayerRegistered
            {
                add => BillPlayers.PlayerRegistered += value;
                remove => BillPlayers.PlayerRegistered -= value;
            }
            public static event System.Action<IBillPlayer> PlayerUnregistered
            {
                add => BillPlayers.PlayerUnregistered += value;
                remove => BillPlayers.PlayerUnregistered -= value;
            }
        }
#endif

        public static bool IsReady => ServiceLocator.IsInitialized;

        /// <summary>
        /// Dependency tracing API. Use when debugging:
        ///   Bill.Trace.Print()       - Full dependency report
        ///   Bill.Trace.Log()         - Recent access log
        ///   Bill.Trace.HealthCheck() - Service health status
        ///   Bill.Trace.Unused()      - Find dead services
        /// </summary>
        public static class Trace
        {
            public static void Print() => UnityEngine.Debug.Log(ServiceLocator.GetDependencyReport());
            public static void Log(int count = 40) => UnityEngine.Debug.Log(ServiceLocator.GetAccessLog(count));
            public static void HealthCheck() => UnityEngine.Debug.Log(ServiceLocator.HealthCheck());

            public static void Unused()
            {
                var u = ServiceLocator.GetUnusedServices();
                if (u.Length == 0) UnityEngine.Debug.Log("[Bill] All services in use.");
                else UnityEngine.Debug.LogWarning($"[Bill] Unused: {string.Join(", ", u)}");
            }

            public static bool Enabled
            {
                get => ServiceLocator.TracingEnabled;
                set => ServiceLocator.TracingEnabled = value;
            }
        }
    }

    /// <summary>
    /// Automatic bootstrap entry point via [RuntimeInitializeOnLoadMethod].
    /// No manual setup required.
    /// </summary>
    public static class BillBootstrap
    {
        private static BillBootstrapConfig _cfg;
        private static GameObject _root;
        private static bool _booting;
        private static float _bootStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Phase1()
        {
            _bootStart = Time.realtimeSinceStartup;
            _cfg = BillBootstrapConfig.Instance;
            if (_cfg == null) { UnityEngine.Debug.LogError("[Bill] BillBootstrapConfig missing from Resources/"); return; }

            if (_cfg.targetFrameRate > 0) Application.targetFrameRate = _cfg.targetFrameRate;
            QualitySettings.vSyncCount = _cfg.vSyncCount;
            ServiceLocator.TracingEnabled = _cfg.enableTracing;

            int current = SceneManager.GetActiveScene().buildIndex;
            if (_cfg.enforceBootstrapScene && current != 0)
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetInt("Bill_ReturnScene", current);
                UnityEditor.EditorPrefs.SetString("Bill_ReturnSceneName", SceneManager.GetActiveScene().name);
#endif
                _booting = true;
                SceneManager.LoadScene(0);
                return;
            }
            _booting = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Phase2()
        {
            if (!_booting || ServiceLocator.IsInitialized) return;
            _cfg = BillBootstrapConfig.Instance;
            if (_cfg == null) return;

            _root = new GameObject("[BillGameCore]");
            Object.DontDestroyOnLoad(_root);

            Step("Infrastructure", () =>
            {
                CoroutineRunner.Create(_root.transform);
                ServiceLocator.Register<IEventBus, EventBus>(new EventBus());
            });

            Step("Core Services", () =>
            {
                ServiceLocator.Register<IConfigService, ConfigService>(new ConfigService());
                ServiceLocator.Register<ISaveService, SaveService>(new SaveService());
                ServiceLocator.Register<ITimerService, TimerService>(new TimerService());
                ServiceLocator.Register<ITweenService, TweenService>(new TweenService());

                var audioGo = new GameObject("[Bill.Audio]");
                audioGo.transform.SetParent(_root.transform);
                ServiceLocator.Register<IAudioService, AudioService>(audioGo.AddComponent<AudioService>());

                ServiceLocator.Register<IPoolService, PoolService>(new PoolService());
                ServiceLocator.Register<IUIService, UIService>(new UIService());
                ServiceLocator.Register<ISceneService, SceneService>(new SceneService());
            });

            Step("State Machine", () =>
            {
                var sm = new GameStateMachine();
                ServiceLocator.Register(sm);
                sm.AddState<BootState>().AddState<MenuState>().AddState<LoadingState>()
                  .AddState<GameplayState>().AddState<PauseState>().AddState<GameOverState>();
            });

            Step("Network", () =>
            {
                ServiceLocator.Register<INetworkService, NetworkService>(new NetworkService());
            });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Step("Dev Tools", () =>
            {
                if (_cfg.includeDebugOverlay) ServiceLocator.Register(new DebugOverlay());
                if (_cfg.includeCheatConsole)
                {
                    var cc = new CheatConsole();
                    ServiceLocator.Register(cc);
                    cc.Register("trace", () => Bill.Trace.Print(), "Dependency trace report");
                    cc.Register("health", () => Bill.Trace.HealthCheck(), "Service health check");
                    cc.Register("log", () => Bill.Trace.Log(), "Recent access log");
                    cc.Register("unused", () => Bill.Trace.Unused(), "Find unused services");
                    cc.Register("states", () => UnityEngine.Debug.Log(Bill.State.GetHistoryLog()), "State history");
                }
                ServiceLocator.Register(new AnalyticsTracker());
            });
#endif

            ServiceLocator.MarkInitialized();
            _booting = false;
            Bill.State.GoTo<BootState>();
            Bill.Events.Fire<GameReadyEvent>();

            float ms = (Time.realtimeSinceStartup - _bootStart) * 1000f;
            UnityEngine.Debug.Log($"[Bill] Ready. {ServiceLocator.ServiceCount} services in {ms:F0}ms.");

#if UNITY_EDITOR
            if (_cfg.returnToEditSceneInEditor)
            {
                int ret = UnityEditor.EditorPrefs.GetInt("Bill_ReturnScene", -1);
                if (ret > 0)
                {
                    string name = UnityEditor.EditorPrefs.GetString("Bill_ReturnSceneName", "");
                    UnityEditor.EditorPrefs.DeleteKey("Bill_ReturnScene");
                    UnityEditor.EditorPrefs.DeleteKey("Bill_ReturnSceneName");
                    if (!string.IsNullOrEmpty(name)) { Bill.Scene.Load(name); return; }
                }
            }
#endif
            if (!string.IsNullOrEmpty(_cfg.defaultGameScene)) Bill.Scene.Load(_cfg.defaultGameScene);
        }

        static void Step(string name, System.Action action)
        {
            float t = Time.realtimeSinceStartup;
            try
            {
                action();
                UnityEngine.Debug.Log($"[Bill] + {name} ({(Time.realtimeSinceStartup - t) * 1000f:F1}ms)");
            }
            catch (System.Exception e) { UnityEngine.Debug.LogError($"[Bill] FAILED {name}: {e}"); }
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload() { ServiceLocator.Reset(); _cfg = null; _root = null; _booting = false; }
#endif
    }
}
