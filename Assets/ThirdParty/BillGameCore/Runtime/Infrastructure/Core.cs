using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    // -------------------------------------------------------
    // EventBus
    // -------------------------------------------------------

    public class EventBus : IEventBus, IInitializable, IDisposableService
    {
        private static class Channel<T> where T : IEvent
        {
            public static readonly List<Action<T>> Listeners = new(8);
            public static EventBus Owner;   // the bus that will clear this channel on Cleanup
        }

        // One clear action per event type ever used, not one closure per Subscribe: the old per-handler
        // closures were never removed by Unsubscribe, so the list grew every run and kept destroyed
        // components reachable until shutdown.
        private readonly List<Action> _channelClears = new(32);

        public void Initialize() { }

        public void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            if (handler == null || Channel<T>.Listeners.Contains(handler)) return;
            Channel<T>.Listeners.Add(handler);
            if (Channel<T>.Owner != this)
            {
                Channel<T>.Owner = this;
                _channelClears.Add(() => { Channel<T>.Listeners.Clear(); Channel<T>.Owner = null; });
            }
        }

        public void SubscribeOnce<T>(Action<T> handler) where T : IEvent
        {
            Action<T> wrapper = null;
            wrapper = e => { handler(e); Unsubscribe(wrapper); };
            Subscribe(wrapper);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IEvent
            => Channel<T>.Listeners.Remove(handler);

        public void Fire<T>(T data) where T : IEvent
        {
            var list = Channel<T>.Listeners;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                try { if (i < list.Count) list[i]?.Invoke(data); }
                catch (Exception e) { Debug.LogError($"[Bill.Events] {typeof(T).Name}: {e.Message}"); }
            }
        }

        public void Fire<T>() where T : struct, IEvent => Fire(default(T));

        public void Cleanup()
        {
            foreach (var a in _channelClears) try { a(); } catch { }
            _channelClears.Clear();
        }
    }

    // -------------------------------------------------------
    // CoroutineRunner
    // -------------------------------------------------------

    [DisallowMultipleComponent]
    public class CoroutineRunner : MonoBehaviour
    {
        public static CoroutineRunner Instance { get; private set; }

        internal static CoroutineRunner Create(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[Bill.Runner]");
            go.transform.SetParent(parent);
            Instance = go.AddComponent<CoroutineRunner>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Update() => ServiceLocator.TickAll(Time.deltaTime);
        void LateUpdate() => ServiceLocator.LateTickAll(Time.deltaTime);

        void OnApplicationPause(bool paused)
        {
            if (ServiceLocator.Has<IEventBus>())
                Bill.Events.Fire(new AppPauseEvent { IsPaused = paused });
        }

        void OnApplicationQuit() => ServiceLocator.Reset();
        void OnDestroy() { if (Instance == this) Instance = null; }

        public static Coroutine Run(IEnumerator routine)
            => Instance != null ? Instance.StartCoroutine(routine) : null;

        public static void Stop(Coroutine c)
        {
            if (Instance != null && c != null) Instance.StopCoroutine(c);
        }

        public static Coroutine RunDelayed(float delay, Action action, bool unscaled = false)
            => Run(DelayRoutine(delay, action, unscaled));

        private static IEnumerator DelayRoutine(float delay, Action action, bool unscaled)
        {
            if (unscaled) { float t = 0; while (t < delay) { t += Time.unscaledDeltaTime; yield return null; } }
            else yield return new WaitForSeconds(delay);
            action?.Invoke();
        }
    }

    // -------------------------------------------------------
    // Built-in Events
    // -------------------------------------------------------

    public struct GameReadyEvent : IEvent { }
    public struct AppPauseEvent : IEvent { public bool IsPaused; }
    public struct SceneLoadStartEvent : IEvent { public string SceneName; }
    public struct SceneLoadCompleteEvent : IEvent { public string SceneName; }
    public struct StateChangedEvent : IEvent { public string From; public string To; }
    public struct NetworkPhaseChangedEvent : IEvent { public NetworkPhase Phase; }
    public struct ConfigRefreshedEvent : IEvent { }
}
