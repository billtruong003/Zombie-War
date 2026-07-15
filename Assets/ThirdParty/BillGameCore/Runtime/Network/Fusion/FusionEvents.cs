#if PHOTON_FUSION
namespace BillGameCore
{
    // ─────────────────────────────────────────────────────────────
    // Connect arguments
    // ─────────────────────────────────────────────────────────────

    /// <summary>Parameters for a Fusion connect attempt. SceneIndex < 0 = stay in current scene.</summary>
    public struct FusionConnectArgs
    {
        public NetworkMode Mode;       // FusionShared / FusionHost / FusionClient / FusionAutoHostOrClient
        public string SessionName;     // null/empty = random matchmaking; fixed name = lobby room
        public int SceneIndex;         // build index loaded on connect; -1 = none
        public int MaxPlayers;         // 0 = Fusion default
        public bool HideFromMatchmaking; // true = IsVisible false (private/coded rooms never random-matched)
        public bool JoinOnly;            // true = fail if the session doesn't exist instead of creating it

        public static FusionConnectArgs Shared(string session, int sceneIndex = -1, int maxPlayers = 0)
            => new FusionConnectArgs { Mode = NetworkMode.FusionShared, SessionName = session, SceneIndex = sceneIndex, MaxPlayers = maxPlayers };
    }

    // ─────────────────────────────────────────────────────────────
    // Events fired on Bill.Events (struct : IEvent, primitive payloads).
    // Subscribe with Bill.Events.Subscribe<FusionPlayerJoinedEvent>(...).
    // ─────────────────────────────────────────────────────────────

    public struct FusionStartedEvent : IEvent { public NetworkMode Mode; public string Session; }
    public struct FusionStartFailedEvent : IEvent { public string Reason; }
    public struct FusionConnectedEvent : IEvent { }
    public struct FusionDisconnectedEvent : IEvent { public string Reason; }
    public struct FusionConnectFailedEvent : IEvent { public string Reason; }
    public struct FusionPlayerJoinedEvent : IEvent { public int PlayerId; public bool IsLocal; }
    public struct FusionPlayerLeftEvent : IEvent { public int PlayerId; }
    public struct FusionShutdownEvent : IEvent { public string Reason; }
    public struct FusionSceneLoadStartEvent : IEvent { }
    public struct FusionSceneLoadDoneEvent : IEvent { }
    public struct FusionHostMigrationEvent : IEvent { }
}
#endif
