using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ZombieWar.World
{
    /// <summary>
    /// One playable map (2026-10-01): the baked 32 m chunks of a theme's wrapping map, written by the
    /// Env map baker (Editor/World/EnvMapBaker.cs) into <see cref="AssetFolder"/>. Themes are
    /// Addressables ("map/&lt;id&gt;", G12.10): only the theme in play is downloaded and loaded, during the
    /// loading screen (<see cref="PreloadForRun"/>); they used to sit in Resources, which put every
    /// map's chunks into the WebGL first download. The map repeats in both directions; chunk (x, z) of the endless world is
    /// chunk (x mod N, z mod N) of the map.
    /// </summary>
    [CreateAssetMenu(menuName = "HordeCall/Map Theme")]
    public sealed class MapTheme : ScriptableObject
    {
        public const string AssetFolder = "Assets/_Project/Data/MapThemes/";
        public const string AddressPrefix = "map/", Label = "maptheme";

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

        [Header("Voice and music (G10: each map carries its own, no id switches elsewhere)")]
        [Tooltip("Run music cue (music.run.<theme>); empty plays the shared run bed.")]
        public string musicKey = "";
        [Tooltip("Radio line played the first time the player deploys to this sector; empty = none.")]
        public string briefingLine = "";
        [Tooltip("Radio dialogue that follows the briefing (RadioDirector dialogue number); -1 = none.")]
        public int briefingDialogue = -1;

        [Header("Light (2026-10-01: the scene's default ambient was a dark grey sky on every map)")]
        [Tooltip("Ambient from above, the horizon and below: lights the shaded side of everything.")]
        public Color ambientSky = new(0.80f, 0.84f, 0.92f);
        public Color ambientEquator = new(0.66f, 0.68f, 0.66f);
        public Color ambientGround = new(0.46f, 0.43f, 0.38f);
        public Color sunColor = new(1f, 0.97f, 0.92f);
        public float sunIntensity = 1.1f;
        [Tooltip("Where the sun stands (toon light rig rotation). The baked shadows use the same angle.")]
        public Vector3 sunEuler = new(50f, 60f, 0f);
        [Tooltip("What sunlight drops to in the baked shadows and the hero's shadow (toon tint, not black).")]
        public Color shadowTint = new(0.56f, 0.58f, 0.76f);
        [Tooltip("Set on snow maps: walkers leave trails in the snow (SnowTrails, Hidden/HordeCall/SnowTrail).")]
        public Shader snowTrails;
        [Tooltip("Set on snowy maps: falling snow on the GPU (Snowfall, HordeCall/Fx/Snowfall).")]
        public Material snowfall;
        [Tooltip("The map's colour grade (owner picks per map, 02/10): a global volume above the gameplay one.")]
        public UnityEngine.Rendering.VolumeProfile post;

        /// <summary>A look on trial (QA panel, LOOK tab): another grade, or other ground / fluid
        /// materials. Empty fields leave that part as it is.</summary>
        [System.Serializable]
        public struct LookOption
        {
            public string name;
            public UnityEngine.Rendering.VolumeProfile post;
            public Material ground;
            public Material fluid;
        }

        [Tooltip("Looks the owner can switch to live while testing (written by MapLooks).")]
        public LookOption[] devLooks = new LookOption[0];
        [Tooltip("Baked by MapLightBaker: R sun, G ambient occlusion, B caster height, A ground height.")]
        public Texture2D mapLight;

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

        // ── loading ──────────────────────────────────────────────────────────────────────
        static readonly Dictionary<string, AsyncOperationHandle<MapTheme>> Loaded = new();
        static readonly List<string> Known = new();

        /// A theme that has been preloaded (null otherwise). In the editor a theme that was not
        /// preloaded is read from its asset, for the editor tools and the tests.
        public static MapTheme Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Loaded.TryGetValue(id, out var h) && h.IsValid() && h.Status == AsyncOperationStatus.Succeeded) return h.Result;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<MapTheme>(AssetFolder + "MapTheme_" + id + ".asset");
#else
            return null;
#endif
        }

        /// Loads <paramref name="id"/> (downloading its bundle if needed) and calls back with it, or
        /// with null when there is no such map. The previously loaded theme is let go.
        public static void Preload(string id, Action<MapTheme> done)
        {
            if (string.IsNullOrEmpty(id) || id == ProceduralId) { done?.Invoke(null); return; }
            if (Loaded.TryGetValue(id, out var have) && have.IsValid())
            {
                if (have.IsDone) done?.Invoke(have.Status == AsyncOperationStatus.Succeeded ? have.Result : null);
                else have.Completed += h => done?.Invoke(h.Status == AsyncOperationStatus.Succeeded ? h.Result : null);
                return;
            }
            // A saved id can outlive its map (renamed or cut): ask the catalog first, so a stale id
            // falls back quietly instead of Addressables throwing InvalidKeyException.
            Addressables.LoadResourceLocationsAsync(AddressPrefix + id, typeof(MapTheme)).Completed += where =>
            {
                bool exists = where.Status == AsyncOperationStatus.Succeeded && where.Result.Count > 0;
                Addressables.Release(where);
                if (!exists) { Debug.LogWarning($"[MapTheme] No map '{id}' in the content catalog."); done?.Invoke(null); return; }
                if (Loaded.TryGetValue(id, out var raced) && raced.IsValid()) { Preload(id, done); return; }
                var handle = Addressables.LoadAssetAsync<MapTheme>(AddressPrefix + id);
                Loaded[id] = handle;
                handle.Completed += h =>
                {
                    if (h.Status != AsyncOperationStatus.Succeeded)
                    {
                        Debug.LogWarning($"[MapTheme] Map '{id}' failed to load ({h.OperationException?.Message}).");
                        Loaded.Remove(id);
                        Addressables.Release(h);
                        done?.Invoke(null);
                        return;
                    }
                    ReleaseAllBut(id);
                    done?.Invoke(h.Result);
                };
            };
        }

        /// The loading screen's step: the next run's map (or the default one when that is gone) is
        /// in memory before the gameplay scene wakes up, since BakedMapStreamer reads it in Awake.
        public static void PreloadForRun(Action ready)
        {
            string id = CurrentId;
            if (id == ProceduralId) { ready(); return; }
            Preload(id, t =>
            {
                if (t != null || id == DefaultTheme) ready();
                else Preload(DefaultTheme, _ => ready());
            });
        }

        static void ReleaseAllBut(string keep)
        {
            var drop = new List<string>();
            foreach (var kv in Loaded) if (kv.Key != keep && kv.Value.IsDone) drop.Add(kv.Key);
            foreach (var k in drop) { if (Loaded[k].IsValid()) Addressables.Release(Loaded[k]); Loaded.Remove(k); }
        }

        /// The baked maps in the content catalog (QA map list); filled by <see cref="RefreshKnown"/>.
        public static IReadOnlyList<string> KnownIds => Known;
        public static bool IsKnown(string id) => Known.Contains(id);

        // Domain reload is off: the last session's handles are dead in the next Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Loaded.Clear(); Known.Clear(); }

        public static void RefreshKnown(Action done = null)
        {
            Addressables.LoadResourceLocationsAsync(Label, typeof(MapTheme)).Completed += h =>
            {
                Known.Clear();
                if (h.Status == AsyncOperationStatus.Succeeded)
                    foreach (var loc in h.Result)
                        if (loc.PrimaryKey.StartsWith(AddressPrefix) && !Known.Contains(loc.PrimaryKey.Substring(AddressPrefix.Length)))
                            Known.Add(loc.PrimaryKey.Substring(AddressPrefix.Length));
                Known.Sort(StringComparer.Ordinal);
                Addressables.Release(h);
                done?.Invoke();
            };
        }

        public const string SharedRunMusic = "music.run.stage1";

        /// The music of the map being played (or about to be); the shared bed without a baked map.
        public static string RunMusicKey()
        {
            var theme = BakedMapStreamer.Active != null ? BakedMapStreamer.Active.Theme : null;
            return theme != null && !string.IsNullOrEmpty(theme.musicKey) ? theme.musicKey : SharedRunMusic;
        }
    }
}
