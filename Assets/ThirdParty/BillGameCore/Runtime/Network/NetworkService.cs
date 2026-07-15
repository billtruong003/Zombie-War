using System;
using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    // -------------------------------------------------------
    // Network Adapter Interface
    // -------------------------------------------------------

    public interface INetworkAdapter
    {
        bool IsConnected { get; }
        bool IsOffline { get; }
        NetworkMode Mode { get; }
        int PlayerCount { get; }
        bool IsHost { get; }
        void CreateRoom(string id, int max, Action ok, Action<string> fail);
        void JoinRoom(string id, Action ok, Action<string> fail);
        void LeaveRoom(Action done);
        void Cleanup();
    }

    // -------------------------------------------------------
    // Offline Adapter (null-object pattern)
    // -------------------------------------------------------

    public class OfflineAdapter : INetworkAdapter
    {
        public bool IsConnected => false;
        public bool IsOffline => true;
        public NetworkMode Mode => NetworkMode.Offline;
        public int PlayerCount => 1;
        public bool IsHost => true;
        public void CreateRoom(string id, int max, Action ok, Action<string> fail) => ok?.Invoke();
        public void JoinRoom(string id, Action ok, Action<string> fail) => ok?.Invoke();
        public void LeaveRoom(Action done) => done?.Invoke();
        public void Cleanup() { }
    }

    // -------------------------------------------------------
    // NetworkService
    // -------------------------------------------------------

    public class NetworkService : INetworkService, IInitializable, IDisposableService
    {
        private INetworkAdapter _adapter;
        private CycleHandler _cycle;

        public void Initialize()
        {
            var cfg = BillBootstrapConfig.Instance;
#if PHOTON_FUSION
            if (cfg != null && cfg.defaultNetworkMode != NetworkMode.Offline)
                _adapter = new FusionNetworkAdapter(cfg.defaultNetworkMode);
            else
#endif
            _adapter = new OfflineAdapter();
            _cycle = new CycleHandler();
        }

        public bool IsConnected => _adapter.IsConnected;
        public bool IsOffline => _adapter.IsOffline;
        public NetworkMode Mode => _adapter.Mode;
        public int PlayerCount => _adapter.PlayerCount;
        public bool IsHost => _adapter.IsHost;
        public CycleHandler Cycle => _cycle;

        public void CreateRoom(string id, int max = 8, Action ok = null, Action<string> fail = null) => _adapter.CreateRoom(id, max, ok, fail);
        public void JoinRoom(string id, Action ok = null, Action<string> fail = null) => _adapter.JoinRoom(id, ok, fail);
        public void LeaveRoom(Action done = null) => _adapter.LeaveRoom(done);

        public void SetAdapter(INetworkAdapter a) { _adapter?.Cleanup(); _adapter = a ?? new OfflineAdapter(); }
        public void Cleanup() => _adapter?.Cleanup();
    }

    // -------------------------------------------------------
    // CycleHandler
    // -------------------------------------------------------

    public class CycleHandler
    {
        public NetworkPhase Phase { get; private set; } = NetworkPhase.Disconnected;
        public event Action<NetworkPhase> OnPhaseChanged;

        public void SetPhase(NetworkPhase p)
        {
            if (Phase == p) return;
            Phase = p;
            OnPhaseChanged?.Invoke(p);
            Bill.Events?.Fire(new NetworkPhaseChangedEvent { Phase = p });
        }

        public void StartCycle(string roomId, int max = 8)
        {
            SetPhase(NetworkPhase.Connecting);
            Bill.Net.CreateRoom(roomId, max, () => SetPhase(NetworkPhase.InRoom), err => { Debug.LogError(err); SetPhase(NetworkPhase.Disconnected); });
        }

        public void StartPlaying() => SetPhase(NetworkPhase.Playing);
        public void EndSession() { SetPhase(NetworkPhase.Disconnecting); Bill.Net.LeaveRoom(() => SetPhase(NetworkPhase.Disconnected)); }
    }

    // -------------------------------------------------------
    // SyncList + SyncState
    // -------------------------------------------------------

    public enum SyncOp { Add, Remove, Update, Clear, Reset }
    public struct SyncChange<T> { public SyncOp Op; public T Item; public int Index; }

    public class SyncList<T> : IReadOnlyList<T>
    {
        private readonly List<T> _items = new();
        private event Action<SyncChange<T>> _changed;

        public int Count => _items.Count;
        public T this[int i] => _items[i];

        public void Add(T item) { _items.Add(item); _changed?.Invoke(new SyncChange<T> { Op = SyncOp.Add, Item = item, Index = _items.Count - 1 }); }
        public bool Remove(T item) { int i = _items.IndexOf(item); if (i < 0) return false; _items.RemoveAt(i); _changed?.Invoke(new SyncChange<T> { Op = SyncOp.Remove, Item = item, Index = i }); return true; }
        public void Clear() { _items.Clear(); _changed?.Invoke(new SyncChange<T> { Op = SyncOp.Clear }); }
        public void ApplyRemote(IList<T> data) { _items.Clear(); if (data != null) _items.AddRange(data); _changed?.Invoke(new SyncChange<T> { Op = SyncOp.Reset }); }
        public void OnChanged(Action<SyncChange<T>> cb) => _changed += cb;
        public void OffChanged(Action<SyncChange<T>> cb) => _changed -= cb;
        public bool Contains(T item) => _items.Contains(item);
        public List<T> ToList() => new(_items);
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class SyncState<T>
    {
        private T _val;
        private event Action<T, T> _changed;
        public T Value { get => _val; set { if (EqualityComparer<T>.Default.Equals(_val, value)) return; var old = _val; _val = value; _changed?.Invoke(old, _val); } }
        public SyncState() { } public SyncState(T init) => _val = init;
        public void OnChanged(Action<T, T> cb) => _changed += cb;
        public void OffChanged(Action<T, T> cb) => _changed -= cb;
        public void Bind(Action<T, T> cb) { _changed += cb; cb(default, _val); }
        public static implicit operator T(SyncState<T> s) => s._val;
    }
}
