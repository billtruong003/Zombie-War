#if PHOTON_FUSION
using System;

namespace BillGameCore
{
    /// <summary>
    /// Bridges Photon Fusion (<see cref="FusionNet"/>) to BillGameCore's <see cref="INetworkAdapter"/>,
    /// so <c>Bill.Net</c> drives Fusion. Instantiated by <see cref="NetworkService"/> when
    /// <c>BillBootstrapConfig.defaultNetworkMode</c> is a Fusion mode and the <c>PHOTON_FUSION</c>
    /// define is set.
    ///
    /// This covers the minimal BillGameCore contract (room/connection/state). For scene-aware
    /// connects, spawning, authority, and per-callback events, use <see cref="FusionNet"/> directly
    /// (e.g. <c>FusionNet.Instance.StartShared(session, arenaSceneIndex)</c>).
    /// </summary>
    public class FusionNetworkAdapter : INetworkAdapter
    {
        readonly NetworkMode _mode;
        readonly FusionNet _net;

        public FusionNetworkAdapter(NetworkMode mode)
        {
            _mode = mode == NetworkMode.Offline ? NetworkMode.FusionShared : mode;
            _net = FusionNet.GetOrCreate();
        }

        public bool IsConnected => _net != null && _net.IsConnected;
        public bool IsOffline => !IsConnected;
        public NetworkMode Mode => _net != null && _net.IsRunning ? _net.NetworkMode : _mode;
        public int PlayerCount => _net != null ? _net.PlayerCount : 0;
        public bool IsHost => _net != null && _net.IsHost;

        // In Shared mode CreateRoom and JoinRoom are the same call (join-or-create the named session).
        public void CreateRoom(string id, int max, Action ok, Action<string> fail) => StartOrJoin(id, max, ok, fail);
        public void JoinRoom(string id, Action ok, Action<string> fail) => StartOrJoin(id, 0, ok, fail);

        void StartOrJoin(string id, int max, Action ok, Action<string> fail)
        {
            var args = new FusionConnectArgs { Mode = _mode, SessionName = id, SceneIndex = -1, MaxPlayers = max };
            _net.Connect(args, success =>
            {
                if (success) ok?.Invoke();
                else fail?.Invoke("Fusion start failed");
            });
        }

        public void LeaveRoom(Action done)
        {
            _net?.Shutdown();
            done?.Invoke();
        }

        public void Cleanup() => _net?.Shutdown();
    }
}
#endif
