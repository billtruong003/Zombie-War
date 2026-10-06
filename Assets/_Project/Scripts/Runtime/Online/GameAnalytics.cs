#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
#define HC_FIREBASE
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
#if HC_FIREBASE
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
#endif

namespace ZombieWar.Online
{
    /// <summary>
    /// Analytics events (event list: Docs/Plans/ANALYTICS_EVENTS.md). On Android/iOS they go to
    /// Firebase Analytics; WebGL has no Firebase SDK, so there they are batched to our own server
    /// (/v1/events); the editor only logs them. Events logged before Firebase is ready wait in a
    /// queue. Names and keys follow Firebase's rules: snake_case, up to 40 characters.
    /// </summary>
    public static class GameAnalytics
    {
        public static bool Verbose = false;

        /// <summary>Web builds stay silent (06/10): a web page would need its own consent banner
        /// before sending usage data. Flip this once the web build has one.</summary>
        public const bool SendWebEventsToServer = false;

        static bool _ready;
        static readonly List<(string name, (string key, object value)[] args)> Pending = new();
        static readonly List<string> ServerBatch = new();
        static float _nextServerFlush;
        const int ServerBatchSize = 20;
        const float ServerFlushSeconds = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _ready = false; Pending.Clear(); ServerBatch.Clear(); _nextServerFlush = 0f; }

        public static void Log(string name, params (string key, object value)[] args)
        {
            if (string.IsNullOrEmpty(name)) return;
#if UNITY_EDITOR
            if (Verbose) Debug.Log("[Analytics] " + name + " " + Describe(args));
#elif HC_FIREBASE
            if (!_ready) { if (Pending.Count < 200) Pending.Add((name, args)); return; }
            FirebaseAnalytics.LogEvent(name, ToParameters(args));
#else
            if (SendWebEventsToServer) QueueForServer(name, args);
#endif
        }

        public static void SetUserProperty(string name, string value)
        {
#if HC_FIREBASE
            if (_ready) FirebaseAnalytics.SetUserProperty(name, value);
#endif
        }

        static string Describe((string key, object value)[] args)
        {
            if (args == null || args.Length == 0) return "";
            var parts = new string[args.Length];
            for (int i = 0; i < args.Length; i++) parts[i] = args[i].key + "=" + args[i].value;
            return string.Join(" ", parts);
        }

#if HC_FIREBASE
        static Parameter[] ToParameters((string key, object value)[] args)
        {
            if (args == null) return Array.Empty<Parameter>();
            var list = new Parameter[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var (k, v) = args[i];
                list[i] = v switch
                {
                    int n => new Parameter(k, n),
                    long n => new Parameter(k, n),
                    float f => new Parameter(k, f),
                    double d => new Parameter(k, d),
                    bool b => new Parameter(k, b ? 1L : 0L),
                    _ => new Parameter(k, v?.ToString() ?? ""),
                };
            }
            return list;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning("[Firebase] Not available: " + task.Result);
                    return;
                }
                _ = FirebaseApp.DefaultInstance;
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                CrashReports.MarkReady();
                _ready = true;
                foreach (var (name, args) in Pending) FirebaseAnalytics.LogEvent(name, ToParameters(args));
                Pending.Clear();
            });
        }
#else
        [Serializable] class ServerEvent { public string name; public string at; }

        static void QueueForServer(string name, (string key, object value)[] args)
        {
            // Parameters ride along as a JSON object; JsonUtility cannot write one, so it is built here.
            var p = new System.Text.StringBuilder("{");
            if (args != null)
                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0) p.Append(',');
                    p.Append('"').Append(Escape(args[i].key)).Append("\":");
                    p.Append(args[i].value switch
                    {
                        int or long or float or double => Convert.ToString(args[i].value, System.Globalization.CultureInfo.InvariantCulture),
                        bool b => b ? "true" : "false",
                        _ => "\"" + Escape(args[i].value?.ToString() ?? "") + "\"",
                    });
                }
            p.Append('}');
            string at = DateTime.UtcNow.ToString("o");
            ServerBatch.Add("{\"name\":\"" + Escape(name) + "\",\"at\":\"" + at + "\",\"params\":" + p + "}");
            if (ServerBatch.Count >= ServerBatchSize || Time.realtimeSinceStartup >= _nextServerFlush) FlushToServer();
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static async void FlushToServer()
        {
            if (ServerBatch.Count == 0) return;
            _nextServerFlush = Time.realtimeSinceStartup + ServerFlushSeconds;
            string body = "{\"events\":[" + string.Join(",", ServerBatch) + "]}";
            ServerBatch.Clear();
            try { await ApiClient.Call("POST", "/v1/events", body); }
            catch (Exception e) { Debug.LogWarning("[Analytics] Upload failed: " + e.Message); }
        }
#endif
    }

    /// <summary>Crash reporting (Crashlytics on Android/iOS). Uncaught exceptions are reported by
    /// the SDK itself; these add breadcrumbs and context keys to the next report.</summary>
    public static class CrashReports
    {
        static bool _ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _ready = false;

        internal static void MarkReady() => _ready = true;

        public static void Breadcrumb(string message)
        {
#if HC_FIREBASE
            if (_ready) Crashlytics.Log(message);
#endif
        }

        public static void SetKey(string key, string value)
        {
#if HC_FIREBASE
            if (_ready) Crashlytics.SetCustomKey(key, value);
#endif
        }

        public static void Report(Exception e)
        {
#if HC_FIREBASE
            if (_ready) { Crashlytics.LogException(e); return; }
#endif
            Debug.LogException(e);
        }
    }
}
