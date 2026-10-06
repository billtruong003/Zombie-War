using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ZombieWar.Online
{
    /// <summary>
    /// The game's only door to the HordeCall server. Every call is optional: the game plays offline
    /// and a failed call returns <c>ok = false</c> instead of throwing. The player is a guest bound
    /// to the device (the server stores only a hash of the id); the bearer token is kept in
    /// PlayerPrefs and renewed when it is about to expire or the server refuses it.
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

        const string TokenKey = "hc.api.token", ExpiresKey = "hc.api.expires", DeviceKey = "hc.api.device";
        const int TimeoutSeconds = 10;

        [Serializable] class GuestRequest { public string deviceId; }
        [Serializable] class GuestReply { public string playerId; public string token; public string expires; }

        public static string PlayerId { get; private set; }

        static string _token;
        static DateTime _expires;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _token = null; _expires = default; PlayerId = null; }

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
            if (_token.Length > 0 && _expires > DateTime.UtcNow.AddHours(1)) return true;

            var r = await Send("POST", "/v1/auth/guest", JsonUtility.ToJson(new GuestRequest { deviceId = DeviceId }), auth: false);
            if (!r.Ok) return false;
            var reply = JsonUtility.FromJson<GuestReply>(r.Body);
            if (reply == null || string.IsNullOrEmpty(reply.token)) return false;
            _token = reply.token;
            PlayerId = reply.playerId;
            DateTime.TryParse(reply.expires, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out _expires);
            PlayerPrefs.SetString(TokenKey, _token);
            PlayerPrefs.SetString(ExpiresKey, _expires.ToString("o"));
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
            PlayerPrefs.DeleteKey(TokenKey);
            if (!await EnsureLogin()) return r;
            return await Send(method, path, json, auth: true);
        }

        public static async Awaitable<Response> Send(string method, string path, string json, bool auth)
        {
            if (!BackendConfig.Enabled) return default;
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
            return new Response(req.responseCode, req.downloadHandler?.text);
        }
    }
}
