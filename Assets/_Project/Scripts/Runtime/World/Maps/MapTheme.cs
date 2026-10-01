using UnityEngine;

namespace ZombieWar.World
{
    /// <summary>
    /// One playable map (2026-10-01): the baked 32 m chunks of a theme's wrapping map, written by the
    /// Env map baker (Editor/World/EnvMapBaker.cs) into Resources/MapThemes, so only the theme in
    /// play is loaded. The map repeats in both directions; chunk (x, z) of the endless world is
    /// chunk (x mod N, z mod N) of the map.
    /// </summary>
    [CreateAssetMenu(menuName = "HordeCall/Map Theme")]
    public sealed class MapTheme : ScriptableObject
    {
        public const string ResourceFolder = "MapThemes/";

        public string id;
        public string displayName;
        public int chunksPerSide = 6;
        public float chunkSize = 32f;
        [Tooltip("Row-major, z * chunksPerSide + x.")]
        public GameObject[] chunks = new GameObject[0];
        [Tooltip("A walkable, open spot in map coordinates (0..size) where the player starts.")]
        public Vector2 spawnPoint;
        [Tooltip("Follows the player (weather), optional.")]
        public GameObject ambientFx;
        public bool hasFoliage = true;

        [Header("Monsters of this map, added to the base roster (ThreatDirector)")]
        [Tooltip("Join tier 0: the everyday crowd of this map.")]
        public ZombieData[] crowd = new ZombieData[0];
        [Tooltip("Join tier 2.")]
        public ZombieData[] later = new ZombieData[0];
        [Tooltip("Join tier 3: this map's elites.")]
        public ZombieData[] elites = new ZombieData[0];

        public float MapSize => chunksPerSide * chunkSize;

        public GameObject ChunkAt(int cx, int cz)
        {
            int n = chunksPerSide;
            if (chunks == null || chunks.Length < n * n) return null;
            return chunks[((cz % n + n) % n) * n + ((cx % n + n) % n)];
        }

        const string PrefKey = "zw.map.theme";
        public const string DefaultTheme = "meadow";
        public const string ProceduralId = "procedural";

        /// The theme the next run plays (saved). Until V1's themes are picked it is set from the cheat
        /// panel ("zw.map &lt;id&gt;").
        /// Read every time: statics survive entering Play when domain reload is off.
        public static string CurrentId
        {
            get => PlayerPrefs.GetString(PrefKey, DefaultTheme);
            set { PlayerPrefs.SetString(PrefKey, value); PlayerPrefs.Save(); }
        }

        public static MapTheme Load(string id) => Resources.Load<MapTheme>(ResourceFolder + "MapTheme_" + id);
    }
}
