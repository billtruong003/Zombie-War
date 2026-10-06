using System;
using UnityEngine;

namespace ZombieWar.Online
{
    /// <summary>
    /// Offline-first cloud copy of the profile. The device's own save is the truth while playing; the
    /// server keeps the latest copy so a reinstall (same device, same guest account) gets its
    /// progress back.
    ///
    /// An install is "linked" once it has compared its profile with the server's. Until then it
    /// never uploads, so a fresh profile can not overwrite real progress in the cloud:
    ///  - at boot, before the menu (waiting at most <see cref="RestoreWaitSeconds"/>), the copy
    ///    that got further (runs played, then account XP) wins: the cloud one is loaded, or the
    ///    local one is kept and uploaded later;
    ///  - when the first comparison happens later (boot was offline) and the cloud copy is ahead,
    ///    nothing is swapped mid-session: the next boot loads it.
    /// Once linked the profile is uploaded when it changed: at the result screen, on pause and every
    /// few minutes. A version conflict (someone else wrote) unlinks, and the next boot decides again.
    /// </summary>
    public static class CloudSave
    {
        public const float RestoreWaitSeconds = 3f;
        const float PushEverySeconds = 180f;
        const string VersionKey = "hc.cloud.version", HashKey = "hc.cloud.hash", LinkedKey = "hc.cloud.linked";

        [Serializable] class VersionReply { public long version; }

        static bool _busy, _bootOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _busy = false; _bootOpen = false; }

        static long KnownVersion
        {
            get => long.TryParse(PlayerPrefs.GetString(VersionKey, "0"), out var v) ? v : 0;
            set => PlayerPrefs.SetString(VersionKey, value.ToString());
        }

        public static bool Linked
        {
            get => PlayerPrefs.GetInt(LinkedKey, 0) == 1;
            private set { PlayerPrefs.SetInt(LinkedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Calls <paramref name="done"/> once the boot may go on: right away on a linked
        /// install, otherwise after the comparison with the server or the wait, whichever is first.</summary>
        public static void RestoreAtBoot(Action done)
        {
            if (!BackendConfig.Enabled || !RemoteConfig.CloudSaveOn || Linked) { done(); return; }
            bool finished = false;
            _bootOpen = true;
            void Finish() { if (finished) return; finished = true; _bootOpen = false; done(); }
            LinkThen(Finish);
            WaitThenFinish(Finish);
        }

        static async void WaitThenFinish(Action finish)
        {
            await Awaitable.WaitForSecondsAsync(RestoreWaitSeconds);
            finish();
        }

        static async void LinkThen(Action finish)
        {
            try { await Link(); }
            finally { finish(); }
        }

        /// <summary>Compares with the server once; see the class notes for who wins.</summary>
        static async Awaitable Link()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var r = await ApiClient.Call("GET", "/v1/save");
                if (!r.Ok) return;
                long version = JsonUtility.FromJson<VersionReply>(r.Body)?.version ?? 0;
                string data = version > 0 ? DataOf(r.Body) : null;
                var cloud = data != null ? PlayerProfile.ProgressOf(data) : null;
                if (cloud == null) { KnownVersion = version; Linked = true; return; }

                var local = PlayerProfile.Progress;
                bool cloudAhead = cloud.Value.runs > local.runs || (cloud.Value.runs == local.runs && cloud.Value.xp > local.xp);
                if (!cloudAhead) { KnownVersion = version; Linked = true; return; }
                if (!_bootOpen) return;   // never swap a profile under a running menu: next boot loads it
                AnalyticsHooks.Muted = true;
                bool imported;
                try { imported = PlayerProfile.ImportJson(data); }
                finally { AnalyticsHooks.Muted = false; }
                AnalyticsHooks.TakeSnapshot();
                if (imported)
                {
                    KnownVersion = version;
                    PlayerPrefs.SetString(HashKey, Hash(PlayerProfile.ExportJson()));
                    Linked = true;
                    Debug.Log($"[CloudSave] Loaded the cloud profile (version {version}, {cloud.Value.runs} runs).");
                }
            }
            catch (Exception e) { Debug.LogWarning("[CloudSave] Link failed: " + e.Message); }
            finally { _busy = false; }
        }

        /// <summary>The raw "data" object of the server's save reply (JsonUtility cannot hold an
        /// arbitrary object, so it is cut out of the text: the server always writes it last).</summary>
        static string DataOf(string body)
        {
            int i = body?.IndexOf("\"data\":", StringComparison.Ordinal) ?? -1;
            if (i < 0) return null;
            string rest = body.Substring(i + 7).TrimEnd();
            return rest.EndsWith("}") ? rest.Substring(0, rest.Length - 1) : null;
        }

        static string Hash(string json) => json == null ? "" : Hash128.Compute(json).ToString();

        /// <summary>Uploads the profile when it changed since the last upload (linked installs only).</summary>
        public static async void Push()
        {
            if (!BackendConfig.Enabled || !RemoteConfig.CloudSaveOn || _busy || !PlayerProfile.HasProfile) return;
            if (!Linked) { await Link(); if (!Linked) return; }
            string json = PlayerProfile.ExportJson();
            if (string.IsNullOrEmpty(json)) return;
            string hash = Hash(json);
            if (hash == PlayerPrefs.GetString(HashKey, "")) return;

            _busy = true;
            try
            {
                // The body is built by hand: "data" must go up as a JSON object, not as a string.
                var r = await ApiClient.Call("PUT", "/v1/save", "{\"baseVersion\":" + KnownVersion + ",\"data\":" + json + "}");
                if (r.Code == 409) { Linked = false; return; }
                if (!r.Ok) return;
                KnownVersion = JsonUtility.FromJson<VersionReply>(r.Body).version;
                PlayerPrefs.SetString(HashKey, hash);
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("[CloudSave] Upload failed: " + e.Message); }
            finally { _busy = false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!BackendConfig.Enabled || Application.isBatchMode) return;
            var go = new GameObject("[CloudSave]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Pump>();
        }

        sealed class Pump : MonoBehaviour
        {
            float _next;
            void Start() => _next = Time.realtimeSinceStartup + 20f;
            void Update()
            {
                if (Time.realtimeSinceStartup < _next) return;
                _next = Time.realtimeSinceStartup + PushEverySeconds;
                Push();
            }
            void OnApplicationPause(bool paused) { if (paused) Push(); }
        }
    }
}
