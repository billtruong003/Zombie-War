namespace ZombieWar.Online
{
    /// <summary>
    /// Where the HordeCall server lives (repo billtruong003/game-backend, Vietnix VPS, 06/10):
    /// api.billthedevstudio.com behind nginx, Let's Encrypt certificate renewed by certbot.
    /// The old plain-HTTP door (IP, port 8088) is for debugging the server only.
    /// </summary>
    public static class BackendConfig
    {
        public const string Url = "https://api.billthedevstudio.com";
        public const string DebugUrl = "http://14.225.255.73:8088";

        public static string BaseUrl => Url;

        /// <summary>Off in edit-mode tests and when a build should stay fully offline.</summary>
        public static bool Enabled = true;
    }
}
