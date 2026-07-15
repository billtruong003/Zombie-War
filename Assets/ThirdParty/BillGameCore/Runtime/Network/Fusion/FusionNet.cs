#if PHOTON_FUSION
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BillGameCore
{
    /// <summary>
    /// Reusable Photon Fusion 2 controller for BillGameCore. Owns a <see cref="NetworkRunner"/>
    /// (on a child GameObject so it can be recreated per session) and exposes a complete,
    /// game-agnostic API: connection lifecycle, session/room, scene load, spawn/despawn,
    /// state authority, player queries, plus C# events and Bill.Events bridges for every
    /// runner callback. Game logic is layered ON TOP of this — this module holds no gameplay.
    ///
    /// Access via the static <see cref="Instance"/> / convenience statics, or let
    /// <see cref="FusionNetworkAdapter"/> drive it through <c>Bill.Net</c>.
    ///
    /// Activates only when the <c>PHOTON_FUSION</c> scripting define is set.
    /// </summary>
    [DisallowMultipleComponent]
    public class FusionNet : MonoBehaviour, INetworkRunnerCallbacks
    {
        // ─────────────────────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────────────────────

        public static FusionNet Instance { get; private set; }
        public static bool Exists => Instance != null;

        /// <summary>Get the persistent FusionNet, creating its DontDestroyOnLoad host if needed.</summary>
        public static FusionNet GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[FusionNet]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<FusionNet>();
            return Instance;
        }

        // ─────────────────────────────────────────────────────────────
        // Events (subscribe from game logic). Names avoid the INetworkRunnerCallbacks
        // method names; the interface is implemented explicitly below.
        // ─────────────────────────────────────────────────────────────

        public event Action Started;                          // session start succeeded
        public event Action<string> StartFailed;              // session start failed (reason)
        public event Action Connected;                        // connected to server/cloud
        public event Action<string> Disconnected;             // disconnected (reason)
        public event Action<string> ConnectFailed;            // connect attempt failed (reason)
        public event Action<PlayerRef> PlayerJoined;
        public event Action<PlayerRef> PlayerLeft;
        public event Action<ShutdownReason> DidShutdown;
        public event Action SceneLoadStarted;
        public event Action SceneLoadCompleted;
        public event Action<HostMigrationToken> HostMigrating;
        public event Action<List<SessionInfo>> SessionListUpdated;

        // ─────────────────────────────────────────────────────────────
        // Runtime state
        // ─────────────────────────────────────────────────────────────

        NetworkRunner _runner;
        bool _isConnecting;
        bool _isShuttingDown;

        // ─────────────────────────────────────────────────────────────
        // State getters (full surface)
        // ─────────────────────────────────────────────────────────────

        /// <summary>Raw runner for advanced use. Null until a connect is attempted.</summary>
        public NetworkRunner Runner => _runner;

        public bool IsConnecting => _isConnecting;
        public bool IsShuttingDown => _isShuttingDown;
        public bool IsRunning => _runner != null && _runner.IsRunning;
        public bool IsConnected => _runner != null && _runner.IsRunning && _runner.SessionInfo.IsValid;
        public bool IsOffline => !IsConnected;

        public bool IsServer => _runner != null && _runner.IsServer;
        public bool IsClient => _runner != null && _runner.IsClient;
        public bool IsSharedModeMasterClient => _runner != null && _runner.IsSharedModeMasterClient;
        /// <summary>Authoritative peer: dedicated server/host, or the Shared-mode master client.</summary>
        public bool IsHost => _runner != null && (_runner.IsServer || _runner.IsSharedModeMasterClient);

        public GameMode GameMode => _runner != null ? _runner.GameMode : GameMode.Single;
        public NetworkMode NetworkMode => MapMode(_runner != null ? _runner.GameMode : (GameMode?)null);

        public PlayerRef LocalPlayer => _runner != null ? _runner.LocalPlayer : PlayerRef.None;
        public int LocalPlayerId => _runner != null ? _runner.LocalPlayer.PlayerId : -1;
        public IEnumerable<PlayerRef> ActivePlayers => _runner != null ? _runner.ActivePlayers : System.Linq.Enumerable.Empty<PlayerRef>();
        public int PlayerCount => _runner != null && _runner.SessionInfo.IsValid ? _runner.SessionInfo.PlayerCount : 0;
        public int MaxPlayers => _runner != null && _runner.SessionInfo.IsValid ? _runner.SessionInfo.MaxPlayers : 0;

        public string SessionName => _runner != null && _runner.SessionInfo.IsValid ? _runner.SessionInfo.Name : null;
        public string Region => _runner != null && _runner.SessionInfo.IsValid ? _runner.SessionInfo.Region : null;
        public int Tick => _runner != null ? _runner.Tick : 0;

        public bool IsLocal(PlayerRef p) => _runner != null && p == _runner.LocalPlayer;
        public double GetRtt(PlayerRef p) => _runner != null ? _runner.GetPlayerRtt(p) : 0d;

        // ─────────────────────────────────────────────────────────────
        // Lifecycle: connect / shutdown
        // ─────────────────────────────────────────────────────────────

        /// <summary>Start or join a session. <paramref name="onResult"/> fires with success/fail.</summary>
        public void Connect(FusionConnectArgs args, Action<bool> onResult = null)
        {
            if (_isConnecting || IsRunning)
            {
                Warn("Connect ignored — already connecting/running. Shutdown first.");
                onResult?.Invoke(false);
                return;
            }
            _ = ConnectAsync(args, onResult);
        }

        public void StartShared(string session, int sceneIndex = -1, int maxPlayers = 0, Action<bool> onResult = null)
            => Connect(FusionConnectArgs.Shared(session, sceneIndex, maxPlayers), onResult);

        public void StartHost(string session, int sceneIndex = -1, int maxPlayers = 0, Action<bool> onResult = null)
            => Connect(new FusionConnectArgs { Mode = BillGameCore.NetworkMode.FusionHost, SessionName = session, SceneIndex = sceneIndex, MaxPlayers = maxPlayers }, onResult);

        public void StartClient(string session, Action<bool> onResult = null)
            => Connect(new FusionConnectArgs { Mode = BillGameCore.NetworkMode.FusionClient, SessionName = session, SceneIndex = -1 }, onResult);

        public void StartAutoHostOrClient(string session, int sceneIndex = -1, int maxPlayers = 0, Action<bool> onResult = null)
            => Connect(new FusionConnectArgs { Mode = BillGameCore.NetworkMode.FusionAutoHostOrClient, SessionName = session, SceneIndex = sceneIndex, MaxPlayers = maxPlayers }, onResult);

        async Task ConnectAsync(FusionConnectArgs args, Action<bool> onResult)
        {
            _isConnecting = true;
            Bill.Net?.Cycle?.SetPhase(NetworkPhase.Connecting);
            EnsureRunner();

            try
            {
                var sceneInfo = new NetworkSceneInfo();
                if (args.SceneIndex >= 0)
                    sceneInfo.AddSceneRef(SceneRef.FromIndex(args.SceneIndex), LoadSceneMode.Additive);

                var startArgs = new StartGameArgs
                {
                    GameMode = ToGameMode(args.Mode),
                    SessionName = string.IsNullOrEmpty(args.SessionName) ? null : args.SessionName,
                    Scene = sceneInfo,   // empty NetworkSceneInfo = no initial scene; filled above when SceneIndex >= 0
                    SceneManager = GetOrAddSceneManager(),
                    ObjectProvider = GetOrAddObjectProvider(),
                };
                if (args.MaxPlayers > 0) startArgs.PlayerCount = args.MaxPlayers;
                if (args.HideFromMatchmaking) startArgs.IsVisible = false;
                if (args.JoinOnly) startArgs.EnableClientSessionCreation = false;

                StartGameResult result = await _runner.StartGame(startArgs);
                _isConnecting = false;

                if (result.Ok)
                {
                    Log($"Started {args.Mode} session '{SessionName}' ({PlayerCount}/{MaxPlayers}).");
                    Bill.Net?.Cycle?.SetPhase(NetworkPhase.InRoom);
                    Bill.Events?.Fire(new FusionStartedEvent { Mode = NetworkMode, Session = SessionName });
                    Started?.Invoke();
                    onResult?.Invoke(true);
                }
                else
                {
                    string reason = result.ShutdownReason.ToString();
                    Warn($"Start failed: {reason}");
                    Bill.Net?.Cycle?.SetPhase(NetworkPhase.Disconnected);
                    Bill.Events?.Fire(new FusionStartFailedEvent { Reason = reason });
                    StartFailed?.Invoke(reason);
                    onResult?.Invoke(false);
                }
            }
            catch (Exception e)
            {
                _isConnecting = false;
                Debug.LogException(e);
                Bill.Net?.Cycle?.SetPhase(NetworkPhase.Disconnected);
                StartFailed?.Invoke(e.Message);
                onResult?.Invoke(false);
            }
        }

        /// <summary>Leave the session and tear down the runner (FusionNet host persists for reuse).</summary>
        public void Shutdown(ShutdownReason reason = ShutdownReason.Ok)
        {
            if (_runner == null || _isShuttingDown) return;
            _isShuttingDown = true;
            Bill.Net?.Cycle?.SetPhase(NetworkPhase.Disconnecting);
            _ = _runner.Shutdown(shutdownReason: reason);   // destroys the runner GO; OnShutdown resets state
        }

        // ─────────────────────────────────────────────────────────────
        // Scene (networked). Only the authoritative peer should trigger.
        // ─────────────────────────────────────────────────────────────

        public bool LoadScene(int buildIndex)
        {
            if (!IsRunning) { Warn("LoadScene before running."); return false; }
            if (!IsHost) { Warn("LoadScene called by non-authoritative peer — ignored (only host/master loads)."); return false; }
            // Explicit LoadSceneMode disambiguates the SceneRef overloads (LoadSceneMode vs LoadSceneParameters).
            _runner.LoadScene(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);
            return true;
        }

        /// <summary>
        /// T13 — load a scene ADDITIVELY (existing scenes + their NetworkObjects stay live), e.g. a minigame
        /// scene loaded on top of a persistent hub. Separate from <see cref="LoadScene"/> (Single mode) so the
        /// existing hub↔arena flow is completely untouched by this — opt-in only. Per
        /// Docs/deprecated/Networking_Architecture.md §2: don't reach for this until something genuinely needs
        /// the hub and a minigame coexisting; a straight <see cref="LoadScene"/> transition is simpler otherwise.
        /// </summary>
        public bool LoadSceneAdditive(int buildIndex, bool setActive = false)
        {
            if (!IsRunning) { Warn("LoadSceneAdditive before running."); return false; }
            if (!IsHost) { Warn("LoadSceneAdditive called by non-authoritative peer — ignored (only host/master loads)."); return false; }
            _runner.LoadScene(SceneRef.FromIndex(buildIndex), LoadSceneMode.Additive, setActiveOnLoad: setActive);
            return true;
        }

        /// <summary>T13 — unload a scene previously loaded via <see cref="LoadSceneAdditive"/>.</summary>
        public bool UnloadSceneAdditive(int buildIndex)
        {
            if (!IsRunning) { Warn("UnloadSceneAdditive before running."); return false; }
            if (!IsHost) { Warn("UnloadSceneAdditive called by non-authoritative peer — ignored (only host/master unloads)."); return false; }
            _runner.UnloadScene(SceneRef.FromIndex(buildIndex));
            return true;
        }

        // ─────────────────────────────────────────────────────────────
        // Spawn / despawn
        // ─────────────────────────────────────────────────────────────

        public NetworkObject Spawn(NetworkObject prefab, Vector3 position = default, Quaternion rotation = default,
            PlayerRef inputAuthority = default, Action<NetworkRunner, NetworkObject> onBeforeSpawned = null)
        {
            if (!IsRunning) { Warn("Spawn before running."); return null; }
            if (prefab == null) { Warn("Spawn called with null prefab."); return null; }
            NetworkRunner.OnBeforeSpawned cb = onBeforeSpawned == null ? null : (r, o) => onBeforeSpawned(r, o);
            return _runner.Spawn(prefab, position, rotation, inputAuthority, cb);
        }

        public void Despawn(NetworkObject obj)
        {
            if (_runner == null || obj == null) return;
            _runner.Despawn(obj);
        }

        // ─────────────────────────────────────────────────────────────
        // State authority (Shared mode)
        // ─────────────────────────────────────────────────────────────

        public void RequestAuthority(NetworkObject obj) => obj?.RequestStateAuthority();
        public void ReleaseAuthority(NetworkObject obj) => obj?.ReleaseStateAuthority();
        public bool HasAuthority(NetworkObject obj) => obj != null && obj.HasStateAuthority;

        // ─────────────────────────────────────────────────────────────
        // Player objects (Fusion's per-player registry)
        // ─────────────────────────────────────────────────────────────

        public void SetPlayerObject(PlayerRef player, NetworkObject obj) => _runner?.SetPlayerObject(player, obj);
        public NetworkObject GetPlayerObject(PlayerRef player) => _runner != null ? _runner.GetPlayerObject(player) : null;
        public bool TryGetPlayerObject(PlayerRef player, out NetworkObject obj)
        {
            obj = GetPlayerObject(player);
            return obj != null;
        }

        // ─────────────────────────────────────────────────────────────
        // INetworkRunnerCallbacks (explicit) → events + Bill.Events + phase
        // ─────────────────────────────────────────────────────────────

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            bool isLocal = player == runner.LocalPlayer;
            Log($"Player joined: {player.PlayerId} (local={isLocal}). Count={PlayerCount}.");
            Bill.Events?.Fire(new FusionPlayerJoinedEvent { PlayerId = player.PlayerId, IsLocal = isLocal });
            PlayerJoined?.Invoke(player);
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Log($"Player left: {player.PlayerId}. Count={PlayerCount}.");
            Bill.Events?.Fire(new FusionPlayerLeftEvent { PlayerId = player.PlayerId });
            PlayerLeft?.Invoke(player);
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
        {
            Log("Connected to server.");
            Bill.Events?.Fire(new FusionConnectedEvent());
            Connected?.Invoke();
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Warn($"Disconnected: {reason}.");
            Bill.Net?.Cycle?.SetPhase(NetworkPhase.Disconnected);
            Bill.Events?.Fire(new FusionDisconnectedEvent { Reason = reason.ToString() });
            Disconnected?.Invoke(reason.ToString());
        }

        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Warn($"Connect failed: {reason}.");
            Bill.Net?.Cycle?.SetPhase(NetworkPhase.Disconnected);
            Bill.Events?.Fire(new FusionConnectFailedEvent { Reason = reason.ToString() });
            ConnectFailed?.Invoke(reason.ToString());
        }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Log($"Shutdown: {shutdownReason}.");
            _isShuttingDown = false;
            _isConnecting = false;
            _runner = null;   // runner GO is destroyed; recreate on next connect
            Bill.Net?.Cycle?.SetPhase(NetworkPhase.Disconnected);
            Bill.Events?.Fire(new FusionShutdownEvent { Reason = shutdownReason.ToString() });
            DidShutdown?.Invoke(shutdownReason);
        }

        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
        {
            Bill.Events?.Fire(new FusionSceneLoadStartEvent());
            SceneLoadStarted?.Invoke();
        }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
        {
            Bill.Events?.Fire(new FusionSceneLoadDoneEvent());
            SceneLoadCompleted?.Invoke();
        }

        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            Warn("Host migration started. Game logic should resume via the token.");
            Bill.Events?.Fire(new FusionHostMigrationEvent());
            HostMigrating?.Invoke(hostMigrationToken);
        }

        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
            => SessionListUpdated?.Invoke(sessionList);

        // Unused (kept empty so the contract is fully implemented).
        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        // ─────────────────────────────────────────────────────────────
        // Internals
        // ─────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void EnsureRunner()
        {
            if (_runner != null) return;
            // The runner must be a ROOT DontDestroyOnLoad object: Fusion calls DontDestroyOnLoad on it
            // internally, which warns (and can misbehave on Single-mode scene loads, e.g. Main -> Arena) if
            // it is parented under [FusionNet]. Keep it root and DDOL it ourselves.
            var go = new GameObject("Runner");
            DontDestroyOnLoad(go);
            _runner = go.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);
        }

        INetworkSceneManager GetOrAddSceneManager()
        {
            var sm = _runner.GetComponent<NetworkSceneManagerDefault>();
            if (sm == null) sm = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            return sm;
        }

        INetworkObjectProvider GetOrAddObjectProvider()
        {
            // Pooling provider: recycles NetworkPoolable prefabs (projectiles) instead of Instantiate/Destroy.
            var op = _runner.GetComponent<PooledNetworkObjectProvider>();
            if (op == null) op = _runner.gameObject.AddComponent<PooledNetworkObjectProvider>();
            return op;
        }

        static GameMode ToGameMode(NetworkMode m)
        {
            switch (m)
            {
                case BillGameCore.NetworkMode.FusionHost: return GameMode.Host;
                case BillGameCore.NetworkMode.FusionClient: return GameMode.Client;
                case BillGameCore.NetworkMode.FusionAutoHostOrClient: return GameMode.AutoHostOrClient;
                case BillGameCore.NetworkMode.FusionShared:
                default: return GameMode.Shared;
            }
        }

        static NetworkMode MapMode(GameMode? g)
        {
            if (g == null) return BillGameCore.NetworkMode.Offline;
            switch (g.Value)
            {
                case GameMode.Host:
                case GameMode.Server: return BillGameCore.NetworkMode.FusionHost;
                case GameMode.Client: return BillGameCore.NetworkMode.FusionClient;
                case GameMode.AutoHostOrClient: return BillGameCore.NetworkMode.FusionAutoHostOrClient;
                case GameMode.Shared: return BillGameCore.NetworkMode.FusionShared;
                default: return BillGameCore.NetworkMode.Offline;
            }
        }

        static void Log(string msg) => Debug.Log($"[FusionNet] {msg}");
        static void Warn(string msg) => Debug.LogWarning($"[FusionNet] {msg}");
    }
}
#endif
