using System;
using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    public abstract class GameState
    {
        public virtual void Enter() { }
        public virtual void Tick(float dt) { }
        public virtual void Exit() { }
        public virtual string Name => GetType().Name.Replace("State", "");
    }

    public class GameStateMachine : IService, IInitializable, ITickable, IDisposableService
    {
        private readonly Dictionary<Type, GameState> _states = new(8);
        private readonly Dictionary<Type, List<Action>> _onEnter = new(8);
        private readonly Dictionary<Type, List<Action>> _onExit = new(8);
        private readonly List<StateTransition> _history = new(64);
        private GameState _current, _prev;
        private event Action<GameState, GameState> _onAny;

        public GameState Current => _current;
        public GameState Previous => _prev;
        public string CurrentName => _current?.Name ?? "None";
        public IReadOnlyList<StateTransition> History => _history;

        public void Initialize() { }

        public GameStateMachine AddState<T>(T s) where T : GameState { _states[typeof(T)] = s; return this; }
        public GameStateMachine AddState<T>() where T : GameState, new() { _states[typeof(T)] = new T(); return this; }

        public void GoTo<T>() where T : GameState => GoImpl(typeof(T));
        public void GoTo(Type t) => GoImpl(t);
        public void GoBack() { if (_prev != null) GoImpl(_prev.GetType()); }

        public bool IsInState<T>() where T : GameState => _current?.GetType() == typeof(T);
        public T GetState<T>() where T : GameState => _states.TryGetValue(typeof(T), out var s) ? (T)s : null;

        public void OnEnter<T>(Action cb) where T : GameState { if (!_onEnter.ContainsKey(typeof(T))) _onEnter[typeof(T)] = new(4); _onEnter[typeof(T)].Add(cb); }
        public void OnExit<T>(Action cb) where T : GameState { if (!_onExit.ContainsKey(typeof(T))) _onExit[typeof(T)] = new(4); _onExit[typeof(T)].Add(cb); }
        public void OnTransition(Action<GameState, GameState> cb) => _onAny += cb;

        void GoImpl(Type target)
        {
            if (!_states.TryGetValue(target, out var next))
            {
                Debug.LogError($"[Bill.State] '{target.Name}' not registered.");
                return;
            }
            if (_current?.GetType() == target) return;

            var from = _current;

            if (_current != null)
            {
                try { _current.Exit(); } catch (Exception e) { Debug.LogError($"[Bill.State] {_current.Name}.Exit: {e.Message}"); }
                if (_onExit.TryGetValue(_current.GetType(), out var ecbs)) foreach (var c in ecbs) c?.Invoke();
            }

            _prev = _current;
            _current = next;

            _history.Add(new StateTransition { From = from?.Name ?? "None", To = next.Name, Timestamp = Time.realtimeSinceStartup, Frame = Time.frameCount });

            try { _current.Enter(); } catch (Exception e) { Debug.LogError($"[Bill.State] {_current.Name}.Enter: {e.Message}\n{e.StackTrace}"); }
            if (_onEnter.TryGetValue(target, out var ncbs)) foreach (var c in ncbs) c?.Invoke();
            _onAny?.Invoke(from, _current);
            Bill.Events?.Fire(new StateChangedEvent { From = from?.Name ?? "None", To = _current.Name });

            Debug.Log($"[Bill.State] {from?.Name ?? "None"} -> {_current.Name}");
        }

        public void Tick(float dt) { try { _current?.Tick(dt); } catch (Exception e) { Debug.LogError($"[Bill.State] {_current?.Name}.Tick: {e.Message}"); } }

        public string GetHistoryLog()
        {
            var sb = new System.Text.StringBuilder(512);
            sb.AppendLine("[Bill.State] History:");
            foreach (var h in _history) sb.AppendLine($"  [frame {h.Frame}] {h.From} -> {h.To} (t={h.Timestamp:F1}s)");
            return sb.ToString();
        }

        public void Cleanup() { _current?.Exit(); _states.Clear(); _history.Clear(); _onEnter.Clear(); _onExit.Clear(); _onAny = null; }
    }

    [Serializable]
    public struct StateTransition { public string From, To; public float Timestamp; public int Frame; }

    // Built-in states
    public class BootState : GameState { }
    public class MenuState : GameState { }
    public class LoadingState : GameState { }
    public class GameplayState : GameState { }
    public class PauseState : GameState
    {
        public override void Enter() => Time.timeScale = 0f;
        public override void Exit() => Time.timeScale = 1f;
    }
    public class GameOverState : GameState { }
}
