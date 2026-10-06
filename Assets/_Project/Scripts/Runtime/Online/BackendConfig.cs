namespace ZombieWar.Online
{
    /// <summary>
    /// Where the HordeCall server lives (repo billtruong003/game-backend, Vietnix VPS, 06/10).
    /// Plain HTTP on the server's IP until there is a domain; a release build must not ship on it,
    /// since Unity only allows HTTP in development builds (Player Settings, insecure HTTP option).
    /// </summary>
    public static class BackendConfig
    {
        public const string DevUrl = "http://14.225.255.73:8088";
        // HTTPS once the DNS record and the certificate exist (certbot on the VPS, 06/10).
        public const string ReleaseUrl = "https://api.billthedevstudio.com";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static string BaseUrl => DevUrl;
#else
        public static string BaseUrl => ReleaseUrl;
#endif

        /// <summary>Off in edit-mode tests and when a build should stay fully offline.</summary>
        public static bool Enabled = true;
    }
}
