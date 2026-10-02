using System.Collections.Generic;
using UnityEngine;
using ZombieWar.WorldNav;

namespace ZombieWar.World
{
    /// <summary>
    /// Streams a baked map (<see cref="MapTheme"/>) around the player (2026-10-01), replacing the
    /// procedural streaming world when a theme is available.
    ///  - A 5 × 5 ring of 32 m chunks around the player, wrapping over the theme's map. Chunk
    ///    instances are pooled per prefab and new ones appear at most two per frame, nearest first
    ///    (the ring's edge is far off screen).
    ///  - The map is placed so the scene's spawn point lands on the theme's open spawn spot.
    ///  - A flat floor collider (top at y = 0) follows the player, as the procedural world's did; the
    ///    chunks carry the obstacles (basins, big props) on the NavObstacle layer.
    ///  - Enemies route around obstacles through <see cref="MapNavigator"/>.
    /// Without a theme (none baked, or the id is unknown) it re-enables the procedural world.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class BakedMapStreamer : MonoBehaviour
    {
        [Tooltip("The procedural world, used when no baked theme can be loaded.")]
        [SerializeField] GameObject proceduralWorld;
        [SerializeField] int ringRadius = 2;
        [SerializeField] int spawnsPerFrame = 2;

        public static BakedMapStreamer Active { get; private set; }
        public MapTheme Theme { get; private set; }
        /// World position of map coordinate (0, 0).
        public Vector3 MapOrigin { get; private set; }

        readonly Dictionary<Vector2Int, GameObject> _live = new();
        readonly Dictionary<GameObject, Stack<GameObject>> _pool = new();
        readonly List<Vector2Int> _pending = new();
        readonly List<Vector2Int> _scratch = new();
        Vector2Int _center = new(int.MinValue, int.MinValue);
        Transform _root, _floor, _ambient;

        void Awake()
        {
            // "procedural" plays the old procedural world (for comparison, from the cheat panel).
            if (MapTheme.CurrentId != MapTheme.ProceduralId)
            {
                Theme = MapTheme.Load(MapTheme.CurrentId);
                if (Theme == null) Theme = MapTheme.Load(MapTheme.DefaultTheme);
            }
            if (Theme == null || Theme.chunks == null || Theme.chunks.Length == 0)
            {
                if (proceduralWorld != null) proceduralWorld.SetActive(true);
                enabled = false;
                return;
            }
            if (proceduralWorld != null) proceduralWorld.SetActive(false);
            Active = this;

            Vector3 spawn = SpawnPosition();
            MapOrigin = new Vector3(spawn.x - Theme.spawnPoint.x, 0f, spawn.z - Theme.spawnPoint.y);

            _root = new GameObject("BakedChunks").transform;
            _root.SetParent(transform, false);
            var floor = new GameObject("Floor");
            floor.transform.SetParent(transform, false);
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(256f, 4f, 256f);
            box.center = new Vector3(0f, -2f, 0f);
            _floor = floor.transform;

            if (Theme.ambientFx != null) _ambient = Instantiate(Theme.ambientFx, transform).transform;
            // Benders (grass) and the see-through dither both run off GrassBenders; enemies only submit
            // themselves on maps that have grass.
            GrassBenders.Active = true;
            GrassBenders.EnemiesBendGrass = Theme.hasFoliage || Theme.snowTrails != null;   // the same feet press the snow
            if (Theme.snowTrails != null) SnowTrails.Create(transform, Theme.snowTrails);
            // The map's own grade over the gameplay volume (which keeps the outline and the bloom).
            if (Theme.post != null)
            {
                var post = new GameObject("MapPost");
                post.transform.SetParent(transform, false);
                var v = post.AddComponent<UnityEngine.Rendering.Volume>();
                v.isGlobal = true;
                v.priority = 50f;
                v.sharedProfile = Theme.post;
            }
            GrassBenders.EnsureRunner();

            // The chunks around the spawn exist before the player does.
            Refresh(spawn, int.MaxValue);
            gameObject.AddComponent<MapNavigator>();
            RegisterCheats();
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            Shader.SetGlobalFloat(MapLightOnId, 0f);
            Shader.SetGlobalFloat(PlanarShadowOnId, 0f);
            GrassBenders.Active = false;
            GrassBenders.EnemiesBendGrass = false;
        }

        // Applied on the first frame the map's scene is the active one: the gameplay scene loads
        // additively and becomes active a few frames later, and the active scene's RenderSettings
        // are the ones that render, so values set earlier would never show.
        bool _lit;

        // The map's own light: a three-colour ambient instead of the scene's dark default sky, and
        // its sun colour on the toon rig. The ambient probe is written directly (the shaders read
        // SH), since nothing re-bakes it at run time. RenderSettings belong to the gameplay scene,
        // so they go with it when the run ends.
        void ApplyLight()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Theme.ambientSky;
            RenderSettings.ambientEquatorColor = Theme.ambientEquator;
            RenderSettings.ambientGroundColor = Theme.ambientGround;
            RenderSettings.ambientProbe = TrilightProbe(Theme.ambientSky, Theme.ambientEquator, Theme.ambientGround);
            var rig = FindFirstObjectByType<ToonLightRig>();
            if (rig != null)
            {
                rig.transform.rotation = Quaternion.Euler(Theme.sunEuler);   // the angle the shadows were baked with
                rig.SetLight(Theme.sunColor, Theme.sunIntensity);
            }
            PushMapLight();
        }

        static readonly int MapLightId = Shader.PropertyToID("_ZWMapLight");
        static readonly int MapLightSTId = Shader.PropertyToID("_ZWMapLightST");
        static readonly int MapLightOnId = Shader.PropertyToID("_ZWMapLightOn");
        static readonly int SunDirId = Shader.PropertyToID("_ZWSunDir");
        static readonly int ShadowTintId = Shader.PropertyToID("_ZWShadowTint");
        static readonly int PlanarShadowOnId = Shader.PropertyToID("_ZWPlanarShadowOn");
        static readonly int ShadowPlaneYId = Shader.PropertyToID("_ZWShadowPlaneY");

        // The baked shadows and AO (MapLight.hlsl): world XZ -> map UV, wrapping with the map.
        void PushMapLight()
        {
            bool on = Theme.mapLight != null;
            Shader.SetGlobalFloat(MapLightOnId, on ? 1f : 0f);
            Vector3 sunTravel = Quaternion.Euler(Theme.sunEuler) * Vector3.forward;
            Shader.SetGlobalVector(SunDirId, sunTravel);
            Shader.SetGlobalColor(ShadowTintId, Theme.shadowTint);
            Shader.SetGlobalFloat(PlanarShadowOnId, 1f);     // the hero's planar shadow (CharacterToon)
            Shader.SetGlobalFloat(ShadowPlaneYId, 0.02f);
            if (!on) return;
            Shader.SetGlobalTexture(MapLightId, Theme.mapLight);
            float inv = 1f / Theme.MapSize;
            Shader.SetGlobalVector(MapLightSTId, new Vector4(inv, inv, -MapOrigin.x * inv, -MapOrigin.z * inv));
        }

        // An SH that reads `sky` straight up, `equator` sideways and `ground` straight down. Unity's
        // SH basis (as Evaluate reads it): k0 = 1, k1 = y, k6 = 3z²-1, k8 = x²-y². The vertical zonal
        // term Z = -0.5·k6 - 1.5·k8 reads 2 up and down and -1 sideways, so with f = a + b·y + c·Z:
        // up = a+b+2c, down = a-b+2c, side = a-c.
        static UnityEngine.Rendering.SphericalHarmonicsL2 TrilightProbe(Color sky, Color equator, Color ground)
        {
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2();
            sh.Clear();
            for (int ch = 0; ch < 3; ch++)
            {
                float s = sky[ch], e = equator[ch], g = ground[ch];
                float b = (s - g) * 0.5f;
                float c = ((s + g) * 0.5f - e) / 3f;
                float a = e + c;
                sh[ch, 0] = a;
                sh[ch, 1] = b;
                sh[ch, 6] = -0.5f * c;
                sh[ch, 8] = -1.5f * c;
            }
            return sh;
        }

        static Vector3 SpawnPosition()
        {
            var point = FindFirstObjectByType<PlayerSpawnPoint>();
            if (point != null) return point.transform.position;
            var spawner = FindFirstObjectByType<PlayerSpawner>();
            return spawner != null ? spawner.transform.position : Vector3.zero;
        }

        Vector2Int ChunkOf(Vector3 world)
        {
            float s = Theme.chunkSize;
            return new Vector2Int(Mathf.FloorToInt((world.x - MapOrigin.x) / s), Mathf.FloorToInt((world.z - MapOrigin.z) / s));
        }

        Vector3 ChunkCentre(Vector2Int c)
        {
            float s = Theme.chunkSize;
            return MapOrigin + new Vector3((c.x + 0.5f) * s, 0f, (c.y + 0.5f) * s);
        }

        void LateUpdate()
        {
            if (!_lit && gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene()) { ApplyLight(); _lit = true; }
            var player = PlayerMovement.Instance;
            if (player != null)
            {
                Vector3 p = player.transform.position;
                if (ChunkOf(p) != _center) Refresh(p, 0);
                if (_ambient != null) _ambient.position = p;
            }
            // Spread instantiation: nearest pending chunks first.
            for (int i = 0; i < spawnsPerFrame && _pending.Count > 0; i++)
            {
                var c = _pending[0];
                _pending.RemoveAt(0);
                Show(c);
            }
        }

        /// Recentres the ring on <paramref name="world"/>; <paramref name="immediate"/> chunks are
        /// shown right away, the rest queued.
        void Refresh(Vector3 world, int immediate)
        {
            _center = ChunkOf(world);
            _floor.position = ChunkCentre(_center);

            _scratch.Clear();
            foreach (var kv in _live)
                if (Mathf.Abs(kv.Key.x - _center.x) > ringRadius || Mathf.Abs(kv.Key.y - _center.y) > ringRadius) _scratch.Add(kv.Key);
            foreach (var c in _scratch) Hide(c);

            _pending.Clear();
            for (int dz = -ringRadius; dz <= ringRadius; dz++)
                for (int dx = -ringRadius; dx <= ringRadius; dx++)
                {
                    var c = new Vector2Int(_center.x + dx, _center.y + dz);
                    if (!_live.ContainsKey(c)) _pending.Add(c);
                }
            _pending.Sort((a, b) => (a - _center).sqrMagnitude.CompareTo((b - _center).sqrMagnitude));
            for (int i = 0; i < immediate && _pending.Count > 0; i++) { Show(_pending[0]); _pending.RemoveAt(0); }
            MapNavigator.MarkDirty();
        }

        // ── looks on trial (QA panel) ─────────────────────────────────────────────────────

        Material _groundTry, _fluidTry;
        readonly Dictionary<Renderer, Material> _baseMats = new();

        /// <summary>Puts a trial look on the running map (null fields: back to the map's own).</summary>
        public void TryLook(UnityEngine.Rendering.VolumeProfile post, Material ground, Material fluid)
        {
            var v = transform.Find("MapPost")?.GetComponent<UnityEngine.Rendering.Volume>();
            if (v == null && post != null)
            {
                var go = new GameObject("MapPost");
                go.transform.SetParent(transform, false);
                v = go.AddComponent<UnityEngine.Rendering.Volume>();
                v.isGlobal = true; v.priority = 50f;
            }
            // The game clones every volume's profile and renders the clone: replace the clone.
            if (v != null) v.profile = post != null ? post : Theme.post;
            _groundTry = ground; _fluidTry = fluid;
            foreach (var go in _live.Values) if (go != null) Retexture(go);
            foreach (var s in _pool.Values) foreach (var go in s) if (go != null) Retexture(go);
        }

        void Retexture(GameObject chunk)
        {
            foreach (var r in chunk.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!_baseMats.TryGetValue(r, out var baseMat))
                {
                    baseMat = r.sharedMaterial;
                    if (baseMat == null) continue;
                    bool ground = baseMat.name.StartsWith("M_MapGround_"), fluid = baseMat.name.StartsWith("M_Fluid_");
                    if (!ground && !fluid) continue;
                    _baseMats[r] = baseMat;
                }
                var tryMat = baseMat.name.StartsWith("M_MapGround_") ? _groundTry : _fluidTry;
                r.sharedMaterial = tryMat != null ? tryMat : baseMat;
            }
        }

        void Show(Vector2Int c)
        {
            if (_live.ContainsKey(c)) return;
            var prefab = Theme.ChunkAt(c.x, c.y);
            if (prefab == null) return;
            GameObject go;
            if (_pool.TryGetValue(prefab, out var stack) && stack.Count > 0) go = stack.Pop();
            else { go = Instantiate(prefab, _root); go.name = prefab.name; }
            go.transform.position = ChunkCentre(c);
            if (_groundTry != null || _fluidTry != null) Retexture(go);
            go.SetActive(true);
            _live[c] = go;
            MapNavigator.MarkDirty();
        }

        void Hide(Vector2Int c)
        {
            if (!_live.TryGetValue(c, out var go)) return;
            _live.Remove(c);
            if (go == null) return;
            go.SetActive(false);
            var prefab = Theme.ChunkAt(c.x, c.y);
            if (!_pool.TryGetValue(prefab, out var stack)) _pool[prefab] = stack = new Stack<GameObject>();
            stack.Push(go);
        }

        void RegisterCheats()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BillGameCore.Bill.Cheat?.Register<string>("zw.map", id => { MapTheme.CurrentId = id; Debug.Log("[Map] next run plays " + id); },
                "Map theme for the next run: meadow, forest, volcano, swamp, tundra, or procedural (old world)");
#endif
        }
    }
}
