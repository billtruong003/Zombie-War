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
            GrassBenders.EnemiesBendGrass = Theme.hasFoliage;
            GrassBenders.EnsureRunner();

            // The chunks around the spawn exist before the player does.
            Refresh(spawn, int.MaxValue);
            gameObject.AddComponent<MapNavigator>();
            RegisterCheats();
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            GrassBenders.Active = false;
            GrassBenders.EnemiesBendGrass = false;
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

        void Show(Vector2Int c)
        {
            if (_live.ContainsKey(c)) return;
            var prefab = Theme.ChunkAt(c.x, c.y);
            if (prefab == null) return;
            GameObject go;
            if (_pool.TryGetValue(prefab, out var stack) && stack.Count > 0) go = stack.Pop();
            else { go = Instantiate(prefab, _root); go.name = prefab.name; }
            go.transform.position = ChunkCentre(c);
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
