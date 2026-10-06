using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ZombieWar.Online
{
    /// <summary>
    /// The game's only door to the HordeCall server. Every call is optional: the game plays offline
    /// and a failed call returns <c>ok = false</c> instead of throwing. The player is a guest bound
    /// to the device (the server stores only a hash of the id). Sessions are a 1 h access token plus a
    /// single-use refresh token (kept in PlayerPrefs): an expired or refused access token is renewed
    /// with the refresh token, and only when that fails does the device sign in as its guest again.
    /// </summary>
    public static class ApiClient
    {
        public readonly struct Response
        {
            public readonly long Code;
            public readonly string Body;
            public Response(long code, string body) { Code = code; Body = body; }
            public bool Ok => Code >= 200 && Code < 300;
        }

        const string TokenKey = "hc.api.token", ExpiresKey = "hc.api.expires", DeviceKey = "hc.api.device",
                     RefreshKey = "hc.api.refresh";
        const int TimeoutSeconds = 10;

        [Serializable] class GuestRequest { public string deviceId; }
        [Serializable] class RefreshRequest { public string refreshToken; }
        [Serializable] class SessionReply { public string playerId; public string token; public string expires; public string refreshToken; }

        public static string PlayerId { get; private set; }

        static string _token;
        static DateTime _expires;
        static bool _signingIn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _token = null; _expires = default; _signingIn = false; PlayerId = null; }

        /// <summary>Android keeps ANDROID_ID across reinstalls, so a reinstall finds its cloud save
        /// again. Platforms without a stable id use a random one kept in PlayerPrefs.</summary>
        static string DeviceId
        {
            get
            {
                string id = SystemInfo.deviceUniqueIdentifier;
                if (Application.platform == RuntimePlatform.Android && !string.IsNullOrEmpty(id)
                    && id != SystemInfo.unsupportedIdentifier && id.Length >= 8)
                    return id;
                id = PlayerPrefs.GetString(DeviceKey, "");
                if (id.Length < 8) { id = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(DeviceKey, id); PlayerPrefs.Save(); }
                return id;
            }
        }

        public static async Awaitable<bool> EnsureLogin()
        {
            if (!BackendConfig.Enabled) return false;
            if (_token == null)
            {
                _token = PlayerPrefs.GetString(TokenKey, "");
                DateTime.TryParse(PlayerPrefs.GetString(ExpiresKey, ""), null, System.Globalization.DateTimeStyles.RoundtripKind, out _expires);
            }
            if (_token.Length > 0 && _expires > DateTime.UtcNow.AddMinutes(2)) return true;

            // One sign-in at a time: two calls spending the same refresh token would look like a
            // stolen copy to the server, which then ends every session.
            if (_signingIn)
            {
                while (_signingIn) await Awaitable.NextFrameAsync();
                return _token.Length > 0 && _expires > DateTime.UtcNow;
            }
            _signingIn = true;
            try { return await SignIn(); }
            finally { _signingIn = false; }
        }

        static async Awaitable<bool> SignIn()
        {
            string refresh = PlayerPrefs.GetString(RefreshKey, "");
            if (refresh.Length > 0)
            {
                var r = await Send("POST", "/v1/auth/refresh", JsonUtility.ToJson(new RefreshRequest { refreshToken = refresh }), auth: false);
                if (r.Ok && Keep(r.Body)) return true;
                if (r.Code == 401) PlayerPrefs.DeleteKey(RefreshKey);   // used or revoked: fall back to the guest sign-in
                else if (r.Code == 0) return false;                      // offline: try again later
            }
            var g = await Send("POST", "/v1/auth/guest", JsonUtility.ToJson(new GuestRequest { deviceId = DeviceId }), auth: false);
            return g.Ok && Keep(g.Body);
        }

        static bool Keep(string body)
        {
            var reply = JsonUtility.FromJson<SessionReply>(body);
            if (reply == null || string.IsNullOrEmpty(reply.token)) return false;
            _token = reply.token;
            PlayerId = reply.playerId;
            DateTime.TryParse(reply.expires, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out _expires);
            PlayerPrefs.SetString(TokenKey, _token);
            PlayerPrefs.SetString(ExpiresKey, _expires.ToString("o"));
            if (!string.IsNullOrEmpty(reply.refreshToken)) PlayerPrefs.SetString(RefreshKey, reply.refreshToken);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>A call that needs the player: logs in first and retries once after a 401.</summary>
        public static async Awaitable<Response> Call(string method, string path, string json = null)
        {
            if (!await EnsureLogin()) return default;
            var r = await Send(method, path, json, auth: true);
            if (r.Code != 401) return r;
            _token = "";
            _expires = default;
            PlayerPrefs.DeleteKey(TokenKey);
            if (!await EnsureLogin()) return r;
            return await Send(method, path, json, auth: true);
        }

        public static async Awaitable<Response> Send(string method, string path, string json, bool auth)
        {
            if (!BackendConfig.Enabled || (auth && RemoteConfig.Maintenance)) return default;
            using var req = new UnityWebRequest(BackendConfig.BaseUrl + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };
            if (json != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            if (auth && !string.IsNullOrEmpty(_token)) req.SetRequestHeader("Authorization", "Bearer " + _token);
            try { await req.SendWebRequest(); }
            catch (Exception e) { Debug.LogWarning($"[Api] {method} {path}: {e.Message}"); return default; }
            if (req.result == UnityWebRequest.Result.ConnectionError) return default;
            if (DateTime.TryParse(req.GetResponseHeader("Date"), System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var serverNow))
                GameClock.SetServerTime(serverNow);
            return new Response(req.responseCode, req.downloadHandler?.text);
        }
    }
}
