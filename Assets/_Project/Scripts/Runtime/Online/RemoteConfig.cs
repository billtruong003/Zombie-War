using System;
using UnityEngine;

namespace ZombieWar.Online
{
    /// <summary>
    /// Switches the owner flips on the server without a new build (GET /v1/config, file
    /// /etc/hordecall/config/game-config.json on the VPS): turn ads, interstitials, the gacha or cloud
    /// saving off, put the online side in maintenance, or ask old versions to update. Fetched once per
    /// launch and kept, so an offline launch uses the last answer; anything missing means "on".
    /// The update / maintenance screens wait for their mockup (S3); until then they show as a toast.
    /// </summary>
    public static class RemoteConfig
    {
        const string CacheKey = "hc.config";

        [Serializable] class Maint { public bool on; public string message; }
        [Serializable] class Features { public bool ads = true, interstitial = true, gacha = true, cloudSave = true; }
        [Serializable] class Doc
        {
            public string minVersion = "0.0.0", latestVersion = "0.0.0", storeUrl;
            public Maint maintenance = new();
            public Features features = new();
        }

        static Doc _doc;
        static bool _toldPlayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _doc = null; _toldPlayer = false; }

        static Doc Current
        {
            get
            {
                if (_doc != null) return _doc;
                try { _doc = JsonUtility.FromJson<Doc>(PlayerPrefs.GetString(CacheKey, "")) ?? new Doc(); }
                catch (Exception) { _doc = new Doc(); }
                return _doc;
            }
        }

        public static bool AdsOn => Current.features?.ads ?? true;
        public static bool InterstitialOn => Current.features?.interstitial ?? true;
        public static bool GachaOn => Current.features?.gacha ?? true;
        public static bool CloudSaveOn => Current.features?.cloudSave ?? true;
        public static bool Maintenance => Current.maintenance?.on ?? false;
        public static string MaintenanceMessage => Current.maintenance?.message ?? "";
        public static string StoreUrl => string.IsNullOrEmpty(Current.storeUrl)
            ? "https://play.google.com/store/apps/details?id=" + Application.identifier : Current.storeUrl;

        /// <summary>This build is older than the oldest version the server still accepts.</summary>
        public static bool UpdateRequired => Older(Application.version, Current.minVersion);
        public static bool UpdateAvailable => Older(Application.version, Current.latestVersion);

        public static bool Older(string mine, string other) =>
            Version.TryParse(mine, out var a) && Version.TryParse(other, out var b) && a < b;

        /// <summary>Reads a config document (also the test seam).</summary>
        public static void Apply(string json)
        {
            Doc doc;
            try { doc = JsonUtility.FromJson<Doc>(json); }
            catch (Exception) { return; }
            if (doc == null) return;
            _doc = doc;
            PlayerPrefs.SetString(CacheKey, json);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void Fetch()
        {
            if (!BackendConfig.Enabled || Application.isBatchMode) return;
            var r = await ApiClient.Send("GET", "/v1/config", null, auth: false);
            if (r.Ok) Apply(r.Body);
            TellPlayerOnce();
        }

        /// <summary>Until the S3 screens exist: one toast on the menu.</summary>
        static void TellPlayerOnce()
        {
            if (_toldPlayer) return;
            if (UpdateRequired) { _toldPlayer = true; UI.Toast.Show("A new version is out - please update HordeCall", 5f); }
            else if (Maintenance) { _toldPlayer = true; UI.Toast.Show(MaintenanceMessage.Length > 0 ? MaintenanceMessage : "Online features are under maintenance", 5f); }
        }
    }
}
