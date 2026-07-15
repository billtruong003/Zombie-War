using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BillGameCore
{
    /// <summary>
    /// Lightweight service registry with built-in dependency tracing.
    /// Tracing uses StackFrame in debug builds to capture the REAL caller,
    /// bypassing the Bill.cs facade layer.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, IService> _services = new(32);
        private static readonly List<ITickable> _tickables = new(8);
        private static readonly List<ILateTickable> _lateTickables = new(4);
        private static bool _initialized;

        // --- Trace storage ---
        private static readonly Dictionary<Type, ServiceTrace> _traces = new(32);
        private static readonly List<AccessLog> _accessLog = new(512);
        private static bool _tracingEnabled = true;
        private const int MaxAccessLogSize = 512;

        #region Registration

        /// <summary>
        /// Register a service with a typed interface.
        /// Example: Register&lt;IAudioService, AudioService&gt;(instance)
        /// </summary>
        public static void Register<TInterface, TImpl>(TImpl service,
            [CallerMemberName] string caller = "")
            where TInterface : class, IService
            where TImpl : class, TInterface
        {
            var key = typeof(TInterface);
            if (_services.ContainsKey(key))
            {
                Debug.LogWarning($"[Bill] {key.Name} already registered, replacing.");
                Unregister<TInterface>();
            }

            _services[key] = service;
            if (service is ITickable t) _tickables.Add(t);
            if (service is ILateTickable lt) _lateTickables.Add(lt);

            RecordRegistration(key, typeof(TImpl), caller);
            TryInitialize(key, service);
        }

        /// <summary>
        /// Register a concrete service (interface = implementation).
        /// Example: Register(new GameStateMachine())
        /// </summary>
        public static void Register<T>(T service,
            [CallerMemberName] string caller = "")
            where T : class, IService
        {
            var key = typeof(T);
            if (_services.ContainsKey(key))
            {
                Debug.LogWarning($"[Bill] {key.Name} already registered, replacing.");
                Unregister<T>();
            }

            _services[key] = service;
            if (service is ITickable t) _tickables.Add(t);
            if (service is ILateTickable lt) _lateTickables.Add(lt);

            RecordRegistration(key, key, caller);
            TryInitialize(key, service);
        }

        private static void TryInitialize(Type key, IService service)
        {
            if (service is IInitializable init)
            {
                try { init.Initialize(); }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[Bill] INIT FAILED: {key.Name}\n" +
                        $"  Type: {service.GetType().Name}\n" +
                        $"  Error: {e.Message}\n" +
                        $"  Stack:\n{e.StackTrace}");
                }
            }
        }

        #endregion

        #region Resolution

        /// <summary>
        /// Resolve a service by interface type.
        /// In debug builds, captures the REAL caller via StackFrame
        /// (skips the Bill.cs property getter).
        /// </summary>
        public static T Get<T>() where T : class, IService
        {
            var key = typeof(T);

            if (_services.TryGetValue(key, out var service))
            {
                RecordAccess(key);
                return (T)service;
            }

            ReportMissing(key);
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class, IService
        {
            if (_services.TryGetValue(typeof(T), out var raw))
            {
                service = (T)raw;
                return true;
            }
            service = null;
            return false;
        }

        public static bool Has<T>() where T : class, IService
            => _services.ContainsKey(typeof(T));

        #endregion

        #region Lifecycle

        public static void Unregister<T>() where T : class, IService
        {
            var key = typeof(T);
            if (!_services.TryGetValue(key, out var svc)) return;
            if (svc is ITickable t) _tickables.Remove(t);
            if (svc is ILateTickable lt) _lateTickables.Remove(lt);
            if (svc is IDisposableService d) d.Cleanup();
            _services.Remove(key);
            _traces.Remove(key);
        }

        internal static void TickAll(float dt)
        {
            for (int i = 0; i < _tickables.Count; i++)
            {
                try { _tickables[i].Tick(dt); }
                catch (Exception e)
                {
                    Debug.LogError($"[Bill] Tick error in {_tickables[i].GetType().Name}: {e.Message}");
                }
            }
        }

        internal static void LateTickAll(float dt)
        {
            for (int i = 0; i < _lateTickables.Count; i++)
            {
                try { _lateTickables[i].LateTick(dt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static void Reset()
        {
            var list = new List<IService>(_services.Values);
            list.Reverse();
            foreach (var svc in list)
                if (svc is IDisposableService d)
                    try { d.Cleanup(); } catch (Exception e) { Debug.LogException(e); }

            _services.Clear();
            _tickables.Clear();
            _lateTickables.Clear();
            _traces.Clear();
            _accessLog.Clear();
            _initialized = false;
        }

        public static bool IsInitialized => _initialized;
        internal static void MarkInitialized() => _initialized = true;
        public static int ServiceCount => _services.Count;

        #endregion

        #region Trace Engine

        /// <summary>
        /// Captures the REAL caller using StackFrame.
        /// Frame 0 = this method, Frame 1 = Get&lt;T&gt;, Frame 2 = Bill.X getter, Frame 3 = actual game code.
        /// Only runs in debug builds.
        /// </summary>
        private static string CaptureRealCaller()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_tracingEnabled) return "";
            try
            {
                // Walk up the stack to find the first frame outside BillGameCore namespace
                var trace = new StackTrace(2, false);
                for (int i = 0; i < trace.FrameCount && i < 8; i++)
                {
                    var frame = trace.GetFrame(i);
                    var method = frame?.GetMethod();
                    if (method == null) continue;

                    var declaringType = method.DeclaringType;
                    if (declaringType == null) continue;

                    string ns = declaringType.Namespace ?? "";
                    if (ns.StartsWith("BillGameCore")) continue;
                    if (declaringType.Name == "Bill") continue;

                    return $"{declaringType.Name}.{method.Name}";
                }
                return "Unknown";
            }
            catch { return "Unknown"; }
#else
            return "";
#endif
        }

        private static void RecordRegistration(Type key, Type implType, string caller)
        {
            if (!_tracingEnabled) return;
            _traces[key] = new ServiceTrace
            {
                InterfaceName = key.Name,
                ImplName = implType.Name,
                RegisteredBy = caller,
                RegisteredAt = Time.realtimeSinceStartup,
                Consumers = new List<string>(8)
            };
        }

        private static void RecordAccess(Type key)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_tracingEnabled) return;

            string caller = CaptureRealCaller();

            if (_traces.TryGetValue(key, out var trace))
            {
                trace.AccessCount++;
                trace.LastAccessedAt = Time.realtimeSinceStartup;
                if (!string.IsNullOrEmpty(caller) && !trace.Consumers.Contains(caller))
                    trace.Consumers.Add(caller);
            }

            if (_accessLog.Count >= MaxAccessLogSize)
                _accessLog.RemoveAt(0);

            _accessLog.Add(new AccessLog
            {
                Service = key.Name,
                Caller = caller,
                Frame = Time.frameCount
            });
#endif
        }

        private static void ReportMissing(Type key)
        {
            var registered = new List<string>();
            foreach (var k in _services.Keys) registered.Add(k.Name);

            Debug.LogError(
                $"[Bill] SERVICE NOT FOUND: {key.Name}\n" +
                $"  Called from: {CaptureRealCaller()}\n" +
                $"  Available: [{string.Join(", ", registered)}]\n" +
                $"  ---\n" +
                $"  Possible causes:\n" +
                $"  1. BillBootstrap has not run yet. Is Scene 0 the bootstrap scene?\n" +
                $"  2. You accessed Bill.{key.Name.Replace("I", "").Replace("Service", "")} before GameReadyEvent.\n" +
                $"  3. The service was not registered in BillBootstrap.\n" +
                $"  4. Check console for earlier INIT FAILED errors.");
        }

        #endregion

        #region Trace Public API

        public static bool TracingEnabled
        {
            get => _tracingEnabled;
            set => _tracingEnabled = value;
        }

        public static string GetDependencyReport()
        {
            var sb = new System.Text.StringBuilder(2048);
            sb.AppendLine("=== BillGameCore Dependency Trace ===\n");

            foreach (var kvp in _traces)
            {
                var t = kvp.Value;
                sb.AppendLine($"  {t.InterfaceName}");
                if (t.InterfaceName != t.ImplName)
                    sb.AppendLine($"    impl: {t.ImplName}");
                sb.AppendLine($"    registered by: {t.RegisteredBy}");
                sb.AppendLine($"    accessed: {t.AccessCount}x");

                if (t.Consumers.Count > 0)
                {
                    sb.AppendLine($"    consumers:");
                    foreach (var c in t.Consumers)
                        sb.AppendLine($"      -> {c}");
                }
                else if (t.AccessCount == 0)
                {
                    sb.AppendLine($"    [!] NEVER ACCESSED (dead service?)");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string GetAccessLog(int count = 40)
        {
            var sb = new System.Text.StringBuilder(1024);
            sb.AppendLine("[Bill] Recent access log:");
            int start = Math.Max(0, _accessLog.Count - count);
            for (int i = start; i < _accessLog.Count; i++)
            {
                var a = _accessLog[i];
                sb.AppendLine($"  [frame {a.Frame}] {a.Service} <- {a.Caller}");
            }
            return sb.ToString();
        }

        public static string HealthCheck()
        {
            var sb = new System.Text.StringBuilder(512);
            sb.AppendLine("[Bill] Health Check:");
            bool allOk = true;
            foreach (var kvp in _services)
            {
                bool alive = kvp.Value != null;
                if (alive && kvp.Value is UnityEngine.Object obj)
                    alive = obj != null;
                if (!alive) allOk = false;
                sb.AppendLine($"  {(alive ? "OK" : "DEAD")} {kvp.Key.Name}");
            }
            if (allOk) sb.AppendLine("  All services healthy.");
            return sb.ToString();
        }

        public static string[] GetUnusedServices()
        {
            var result = new List<string>();
            foreach (var kvp in _traces)
                if (kvp.Value.AccessCount == 0) result.Add(kvp.Key.Name);
            return result.ToArray();
        }

        #endregion

        #region Internal Types

        private class ServiceTrace
        {
            public string InterfaceName;
            public string ImplName;
            public string RegisteredBy;
            public float RegisteredAt;
            public float LastAccessedAt;
            public int AccessCount;
            public List<string> Consumers;
        }

        private struct AccessLog
        {
            public string Service;
            public string Caller;
            public int Frame;
        }

        #endregion
    }
}
