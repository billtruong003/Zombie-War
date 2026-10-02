using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Env Sandbox maps (2026-10-01). Each theme gets one whole map, generated as a single piece and
    /// then cut into 32 m chunks (the game's chunk size) that are baked here in the editor: sunken
    /// ground, fluid, bridges, props and obstacle colliders. Nothing is generated while the game runs.
    ///
    /// Seamless because nothing is decided per chunk: rivers meander freely across the whole map,
    /// ground patches and bank wobble come from one noise, and a chunk is only a window onto them.
    /// The map also wraps: its east edge continues its west edge and north continues south (rivers
    /// and noise repeat with the map's period), so an endless streaming world can repeat the map
    /// without a visible join.
    ///
    /// Basins full of water, toxic, lava or ice are obstacles (colliders on the NavObstacle layer):
    /// players and enemies walk around them, bullets fly over. Rivers get plank bridges, the whole
    /// map is flood-filled (with wrap) and must be one walkable area; cut-off pockets are filled in.
    /// </summary>
    public static partial class EnvSandboxBuilder
    {
        const float ChunkSize = 32f;
        const float Cell = 0.5f;               // walk grid
        const float FieldStep = 0.25f;         // basin field sample spacing
        // Basin value from which a cell is not walkable: where the fluid surface shows (it sits at 35 %
        // of the basin depth, which the ground passes at a basin value of ~0.25). It was 0.12, which
        // blocked a 1-2 m band of dry bank round every river and pool (found 02/10, ColliderAudit).
        const float BlockAt = 0.24f;
        const float BridgeWidth = 2.6f;
        const float MapSpacing = 300f, MapZ = 170f;
        const int NavObstacleLayer = 10;       // ProjectSettings/TagManager.asset: "NavObstacle"

        sealed class MapDef
        {
            public string id;
            public int chunks = 6;
            public int riversH, riversV;
            public int pools;
            public Vector2 poolRadius;
            public float bridgeEvery = 18f;
            // Dressing (EnvDressing.cs): tree groves, lone trees, vignettes, singles, ground cover.
            public float canopyOverlap = 0.9f;     // canopy spacing factor: 1 = canopies just touch
            public float groveNoise = 0.5f, groveSpacing = 22f;
            public Vector2 groveRadius = new(4f, 7f);
            public float loneTrees = 0.3f, vignettes = 0.6f, singles = 1.5f;   // per 1 000 m²
            public float coverPer100 = 2f, coverPatch = 0.5f;                   // tries per 100 m², patch noise cut
            public float coverNoise = 0.06f;      // patch noise frequency: higher = smaller, more clumps
            public float coverSpacing = 0.7f;     // tuft spacing factor: lower = tufts packed closer
            public Vector2 coverScale = Vector2.one;   // tuft size range: big tufts fill a clump with fewer triangles
            // Winding paths painted into the ground's path layer (desert clay paths): 0 = none.
            public float pathBands;
        }

        static readonly MapDef[] Maps =
        {
            new MapDef { id = "meadow", riversH = 1, pools = 5, poolRadius = new Vector2(3.5f, 6f),
                canopyOverlap = 1f, groveNoise = 0.55f, groveSpacing = 26f, groveRadius = new Vector2(5f, 8f),
                // Owner 02/10: grass dense in clumps, bare between them, so the meadow reads and sways.
                loneTrees = 0.25f, vignettes = 1.2f, singles = 1.5f, coverPer100 = 95f, coverPatch = 0.44f, coverNoise = 0.16f, coverSpacing = 0.3f, coverScale = new Vector2(1.35f, 1.85f) },
            new MapDef { id = "forest", riversH = 1, riversV = 1, pools = 3, poolRadius = new Vector2(3f, 5f),
                canopyOverlap = 0.75f, groveNoise = 0.35f, groveSpacing = 16f, groveRadius = new Vector2(6f, 10f),
                loneTrees = 0.6f, vignettes = 0.8f, singles = 1.5f, coverPer100 = 30f, coverPatch = 0.45f },
            new MapDef { id = "volcano", riversH = 1, riversV = 1, pools = 7, poolRadius = new Vector2(2.5f, 4.5f),
                canopyOverlap = 0.9f, groveNoise = 0.5f, groveSpacing = 22f, groveRadius = new Vector2(4f, 7f),
                loneTrees = 0.3f, vignettes = 0.5f, singles = 2.5f, coverPer100 = 2f, coverPatch = 0.5f },
            new MapDef { id = "swamp", riversH = 1, pools = 11, poolRadius = new Vector2(3f, 6f),
                canopyOverlap = 0.9f, groveNoise = 0.45f, groveSpacing = 20f, groveRadius = new Vector2(4f, 7f),
                loneTrees = 0.5f, vignettes = 0.6f, singles = 1.2f, coverPer100 = 5f, coverPatch = 0.42f },
            new MapDef { id = "tundra", pools = 7, poolRadius = new Vector2(5f, 8f),
                canopyOverlap = 1f, groveNoise = 0.5f, groveSpacing = 24f, groveRadius = new Vector2(4f, 7f),
                loneTrees = 0.3f, vignettes = 0.5f, singles = 1.5f, coverPer100 = 2f, coverPatch = 0.5f },
            // Desert (owner pick D3, 02/10): plain sand with winding clay paths, a few oasis pools, no
            // Tiny Teacup cliff walls (the map streams without edges now).
            new MapDef { id = "desert", pools = 4, poolRadius = new Vector2(3f, 5f),
                canopyOverlap = 1f, groveNoise = 0.6f, groveSpacing = 30f, groveRadius = new Vector2(3f, 5f),
                loneTrees = 0.35f, vignettes = 1.1f, singles = 3.2f, coverPer100 = 9f, coverPatch = 0.45f, pathBands = 1f },
        };

        /// One generated map: its features and the sampled basin field (wraps with period M).
        sealed class MapData
        {
            public MapDef def;
            public Zone z;
            public float M;
            public List<Vector2[]> rivers = new();
            public List<Vector3> pools = new();          // x, z, radius
            public List<(Vector2 c, Vector2 along, float span)> bridges = new();
            public int S;                                 // field samples per side
            public float[] field;
            public int G;                                 // walk cells per side
            public bool[] blocked, bridgeCells;
        }

        // ── noise and distances with the map's period

        static float Wrap(float v, float m) { v %= m; return v < 0f ? v + m : v; }

        /// Perlin noise repeating every m metres: four samples a period apart, blended across it.
        static float PNoise(Vector2 p, float m, float scale, float ox, float oy)
        {
            float x = Wrap(p.x, m), y = Wrap(p.y, m);
            float u = x / m, v = y / m;
            float F(float a, float b) => Mathf.PerlinNoise(a * scale + ox, b * scale + oy);
            float a0 = Mathf.Lerp(F(x, y), F(x - m, y), u);
            float a1 = Mathf.Lerp(F(x, y - m), F(x - m, y - m), u);
            // The blend flattens contrast toward the middle of the period; stretch it back.
            return Mathf.Clamp01(0.5f + (Mathf.Lerp(a0, a1, v) - 0.5f) * 1.6f);
        }

        static Vector2 WrapDelta(Vector2 d, float m) => new(d.x - m * Mathf.Round(d.x / m), d.y - m * Mathf.Round(d.y / m));

        static float DistRiver(Vector2 p, Vector2[] line, float m, float reach)
        {
            float best = float.MaxValue;
            // The polyline already runs past both ends of the map along its own axis; the copies a
            // period away cover a river that meanders close to the other edge.
            for (int o = 0; o < 5; o++)
            {
                var q = p + (o == 1 ? new Vector2(m, 0) : o == 2 ? new Vector2(-m, 0) : o == 3 ? new Vector2(0, m) : o == 4 ? new Vector2(0, -m) : Vector2.zero);
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    var a = line[i]; var b = line[i + 1];
                    if (q.x < Mathf.Min(a.x, b.x) - reach || q.x > Mathf.Max(a.x, b.x) + reach ||
                        q.y < Mathf.Min(a.y, b.y) - reach || q.y > Mathf.Max(a.y, b.y) + reach) continue;
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
                }
            }
            return best;
        }

        static float BasinExact(MapData d, Vector2 p)
        {
            float warp = PNoise(p, d.M, 0.17f, 11.3f, 5.1f) - 0.5f;
            float v = 0f;
            foreach (var c in d.pools)
            {
                float dist = WrapDelta(p - new Vector2(c.x, c.y), d.M).magnitude + warp * c.z * 0.7f;
                v = Mathf.Max(v, 1f - dist / c.z);
            }
            float w = d.z.riverWidth;
            foreach (var r in d.rivers)
            {
                float dist = DistRiver(p, r, d.M, w * 2f) + warp * w * 0.8f;
                v = Mathf.Max(v, (1f - dist / w) * 0.85f);
            }
            return Mathf.Clamp01(v);
        }

        /// The sampled basin field, bilinear, wrapping.
        static float Basin(MapData d, Vector2 p)
        {
            float x = Wrap(p.x, d.M) / FieldStep, y = Wrap(p.y, d.M) / FieldStep;
            int i = (int)x, j = (int)y;
            float fx = x - i, fy = y - j;
            int S = d.S;
            int i0 = i % S, i1 = (i + 1) % S, j0 = j % S, j1 = (j + 1) % S;
            float a = Mathf.Lerp(d.field[j0 * S + i0], d.field[j0 * S + i1], fx);
            float b = Mathf.Lerp(d.field[j1 * S + i0], d.field[j1 * S + i1], fx);
            return Mathf.Lerp(a, b, fy);
        }

        // ── generation

        /// A river across the whole map: control points every ~24 m, repeating with the map's period
        /// (so it leaves one edge exactly where it enters the opposite one), smoothed.
        static Vector2[] MakeRiver(float m, bool vertical, System.Random rng)
        {
            int k = Mathf.Max(4, Mathf.RoundToInt(m / 24f));
            float baseLine = (float)rng.NextDouble() * m;
            var off = new float[k];
            for (int i = 0; i < k; i++) off[i] = ((float)rng.NextDouble() * 2f - 1f) * 11f;
            var ctrl = new List<Vector2>();
            for (int i = -2; i <= k + 2; i++)
            {
                float a = i * m / k, b = baseLine + off[((i % k) + k) % k];
                ctrl.Add(vertical ? new Vector2(b, a) : new Vector2(a, b));
            }
            var pts = new List<Vector2>();
            for (int i = 1; i + 2 < ctrl.Count; i++)
                for (int s = 0; s < 8; s++)
                {
                    float t = s / 8f, t2 = t * t, t3 = t2 * t;
                    var p0 = ctrl[i - 1]; var p1 = ctrl[i]; var p2 = ctrl[i + 1]; var p3 = ctrl[i + 2];
                    pts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            return pts.ToArray();
        }

        static MapData Generate(MapDef def, Zone z)
        {
            var d = new MapData { def = def, z = z, M = def.chunks * ChunkSize };
            var rng = new System.Random(("map" + def.id).GetHashCode());
            for (int i = 0; i < def.riversH; i++) d.rivers.Add(MakeRiver(d.M, false, rng));
            for (int i = 0; i < def.riversV; i++) d.rivers.Add(MakeRiver(d.M, true, rng));
            for (int tries = 0; d.pools.Count < def.pools && tries < 400; tries++)
            {
                float r = Mathf.Lerp(def.poolRadius.x, def.poolRadius.y, (float)rng.NextDouble());
                var c = new Vector2((float)rng.NextDouble() * d.M, (float)rng.NextDouble() * d.M);
                bool clear = true;
                foreach (var riv in d.rivers) if (DistRiver(c, riv, d.M, r + 10f) < r + z.riverWidth + 5f) clear = false;
                foreach (var o in d.pools) if (WrapDelta(c - new Vector2(o.x, o.y), d.M).magnitude < r + o.z + 5f) clear = false;
                if (clear) d.pools.Add(new Vector3(c.x, c.y, r));
            }

            d.S = Mathf.RoundToInt(d.M / FieldStep);
            d.field = new float[d.S * d.S];
            for (int j = 0; j < d.S; j++)
                for (int i = 0; i < d.S; i++)
                    d.field[j * d.S + i] = BasinExact(d, new Vector2(i * FieldStep, j * FieldStep));

            d.G = Mathf.RoundToInt(d.M / Cell);
            d.blocked = new bool[d.G * d.G];
            for (int j = 0; j < d.G; j++)
                for (int i = 0; i < d.G; i++)
                    d.blocked[j * d.G + i] = Basin(d, CellCentre(i, j)) > BlockAt;

            // Bridges along every river, then more halfway between them until the map is one area.
            d.bridgeCells = new bool[d.blocked.Length];
            foreach (var r in d.rivers) AddBridges(d, r, def.bridgeEvery, def.bridgeEvery * 0.5f);
            int pass = 0;
            while (!Connected(d, out _, fill: false) && pass < 3)
            {
                foreach (var r in d.rivers) AddBridges(d, r, def.bridgeEvery, def.bridgeEvery * (pass % 2 == 0 ? 0f : 0.25f));
                pass++;
            }
            Connected(d, out int filled, fill: true);
            if (filled > 0) Debug.Log($"[EnvMaps] {def.id}: filled {filled} cut-off cells");
            return d;
        }

        static Vector2 CellCentre(int i, int j) => new((i + 0.5f) * Cell, (j + 0.5f) * Cell);

        static void AddBridges(MapData d, Vector2[] river, float every, float offset)
        {
            float acc = 0f, next = offset;
            for (int i = 1; i < river.Length; i++)
            {
                var a = river[i - 1]; var b = river[i];
                float seg = Vector2.Distance(a, b);
                while (acc + seg >= next)
                {
                    var c = Vector2.Lerp(a, b, (next - acc) / seg);
                    next += every;
                    // Only the part of the polyline that lies on the map; the rest is its wrap copy.
                    if (c.x < 0f || c.y < 0f || c.x >= d.M || c.y >= d.M) continue;
                    var flow = (b - a).normalized;
                    var along = new Vector2(-flow.y, flow.x);
                    float s1 = 0f, s2 = 0f;
                    while (s1 < 7f && Basin(d, c + along * s1) > 0.03f) s1 += 0.25f;
                    while (s2 < 7f && Basin(d, c - along * s2) > 0.03f) s2 += 0.25f;
                    if (s1 >= 7f || s2 >= 7f) continue;            // lands in a pool or a crossing
                    var centre = c + along * ((s1 - s2) * 0.5f);
                    bool crowded = false;
                    foreach (var br in d.bridges) if (WrapDelta(br.c - centre, d.M).magnitude < 8f) crowded = true;
                    if (crowded) continue;
                    var bridge = (new Vector2(Wrap(centre.x, d.M), Wrap(centre.y, d.M)), along, s1 + s2 + 0.8f);
                    d.bridges.Add(bridge);
                    Carve(d, bridge.Item1, along, bridge.Item3 + 1f, BridgeWidth);
                }
                acc += seg;
            }
        }

        static void Carve(MapData d, Vector2 c, Vector2 along, float length, float width)
        {
            var side = new Vector2(along.y, -along.x);
            float reach = Mathf.Max(length, width);
            int r = Mathf.CeilToInt(reach / Cell) + 1;
            int ci = Mathf.FloorToInt(c.x / Cell), cj = Mathf.FloorToInt(c.y / Cell);
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                {
                    int i = ((ci + di) % d.G + d.G) % d.G, j = ((cj + dj) % d.G + d.G) % d.G;
                    var p = new Vector2((ci + di + 0.5f) * Cell, (cj + dj + 0.5f) * Cell) - c;
                    if (Mathf.Abs(Vector2.Dot(p, along)) <= length * 0.5f && Mathf.Abs(Vector2.Dot(p, side)) <= width * 0.5f)
                    {
                        d.blocked[j * d.G + i] = false;
                        d.bridgeCells[j * d.G + i] = true;
                    }
                }
        }

        /// One walkable area over the wrapping map? With fill, every smaller area is filled in.
        static bool Connected(MapData d, out int filled, bool fill)
        {
            filled = 0;
            int G = d.G, n = G * G;
            var comp = new int[n];
            var sizes = new List<int> { 0 };
            var stack = new Stack<int>();
            for (int s = 0; s < n; s++)
            {
                if (d.blocked[s] || comp[s] != 0) continue;
                int id = sizes.Count, size = 0;
                sizes.Add(0);
                comp[s] = id; stack.Push(s);
                while (stack.Count > 0)
                {
                    int k = stack.Pop(); size++;
                    int i = k % G, j = k / G;
                    int[] nb = { j * G + (i + 1) % G, j * G + (i + G - 1) % G, ((j + 1) % G) * G + i, ((j + G - 1) % G) * G + i };
                    foreach (int nk in nb)
                        if (!d.blocked[nk] && comp[nk] == 0) { comp[nk] = id; stack.Push(nk); }
                }
                sizes[id] = size;
            }
            int main = 1;
            for (int i = 2; i < sizes.Count; i++) if (sizes[i] > sizes[main]) main = i;
            bool one = true;
            for (int i = 1; i < sizes.Count; i++) if (i != main && sizes[i] >= 16) one = false;
            if (fill)
                for (int k = 0; k < n; k++)
                    if (!d.blocked[k] && comp[k] != main) { d.blocked[k] = true; filled++; }
            return one;
        }

        // ── baking a chunk

        static GameObject _plank;

        static string BuildMaps(Transform root)
        {
            AssetDatabase.DeleteAsset(Art + "Tiles");      // the per-tile variants this replaced
            AssetDatabase.DeleteAsset(Art + "Maps");
            var zones = Zones();
            ThemeMats.Clear();              // rebuilt from the current palette and atlas
            var log = new System.Text.StringBuilder();
            for (int m = 0; m < Maps.Length; m++)
            {
                var z = zones.Find(x => x.id == Maps[m].id);
                if (z == null) continue;
                log.Append(BuildMap(Maps[m], z, root, new Vector3(m * MapSpacing, 0f, MapZ))).Append("; ");
            }
            return log.ToString();
        }

        static string BuildMap(MapDef def, Zone z, Transform root, Vector3 origin)
        {
            var d = Generate(def, z);
            string dir = Art + "Maps/" + def.id + "/";
            Directory.CreateDirectory(dir);

            var ground = new Material(GroundMaterial(z));
            ground.SetFloat("_BasinFromUV", 1f);
            AssetDatabase.CreateAsset(ground, dir + "M_MapGround_" + def.id + ".mat");
            var fluid = string.IsNullOrEmpty(z.fluid) ? null : FluidMaterial(z, origin);

            // Props for the whole map first, then handed to the chunk they stand in.
            var props = MapProps(d);

            int n = def.chunks;
            var prefabs = new GameObject[n, n];
            for (int cz = 0; cz < n; cz++)
                for (int cx = 0; cx < n; cx++)
                    prefabs[cx, cz] = BakeChunk(d, cx, cz, dir, ground, fluid, props);
            WriteMapTheme(d, z, prefabs, props);

            // The map, plus one ring of wrapped chunks around it to show the edges join.
            var mapRoot = new GameObject("Map_" + def.id).transform;
            mapRoot.SetParent(root, false);
            mapRoot.position = origin;
            float half = d.M * 0.5f;
            for (int gz = -1; gz <= n; gz++)
                for (int gx = -1; gx <= n; gx++)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[(gx + n) % n, (gz + n) % n], mapRoot);
                    inst.transform.localPosition = new Vector3(gx * ChunkSize + ChunkSize * 0.5f - half, 0f, gz * ChunkSize + ChunkSize * 0.5f - half);
                    if (gx < 0 || gz < 0 || gx >= n || gz >= n) inst.name += "_wrap";
                }

            // Cameras: whole map from above, the game view over a bridge and over the map's edge
            // where a river crosses it, and a wide angle.
            var top = NewCamera("Cam_M_" + def.id + "_Top", mapRoot);
            top.transform.position = origin + new Vector3(0f, 400f, 0f);
            top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            top.fieldOfView = 60f; top.nearClipPlane = 100f; top.farClipPlane = 600f;
            Vector3 L(Vector2 p) => origin + new Vector3(p.x - half, 0f, p.y - half);
            if (d.bridges.Count > 0) GameCam("Cam_M_" + def.id, mapRoot, L(d.bridges[d.bridges.Count / 2].c));
            else GameCam("Cam_M_" + def.id, mapRoot, origin);
            if (d.rivers.Count > 0)
            {
                // Where the first river crosses the map's west (or south) edge.
                var r = d.rivers[0];
                Vector2 edge = r[0];
                for (int i = 1; i < r.Length; i++)
                    if ((r[i - 1].x < 0f && r[i].x >= 0f) || (r[i - 1].y < 0f && r[i].y >= 0f)) { edge = r[i]; break; }
                GameCam("Cam_M_" + def.id + "_Seam", mapRoot, L(new Vector2(Mathf.Max(edge.x, 0f), Mathf.Max(edge.y, 0f))));
            }
            var hero = NewCamera("Cam_M_" + def.id + "_Hero", mapRoot);
            hero.transform.position = origin + new Vector3(40f, 70f, -120f);
            hero.transform.LookAt(origin + new Vector3(0f, 0f, 10f));
            hero.fieldOfView = 45f;

            // Pathing demo: a crowd crosses the map from the south to the north.
            if (def.id == "volcano" || def.id == "meadow")
            {
                var demo = new GameObject("NavDemo").AddComponent<ZombieWar.WorldNav.EnvNavDemo>();
                demo.transform.SetParent(mapRoot, false);
                demo.spawnCentre = new Vector3(0f, 0f, -half + 6f);
                demo.spawnHalfWidth = half - 8f;
                demo.target = new Vector3(0f, 0f, half - 8f);
                demo.window = d.M + 16f;
                var navTop = NewCamera("Cam_Nav_" + def.id, mapRoot);
                navTop.transform.position = top.transform.position;
                navTop.transform.rotation = top.transform.rotation;
                navTop.fieldOfView = 60f; navTop.nearClipPlane = 100f; navTop.farClipPlane = 600f;
            }

            var label = new GameObject("Label").AddComponent<TMPro.TextMeshPro>();
            label.transform.SetParent(mapRoot, false);
            label.transform.localPosition = new Vector3(0f, 0.2f, -half - ChunkSize - 6f);
            label.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            label.text = z.name + $" · map {d.M:0} m, cuộn vòng";
            label.fontSize = 60f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(200f, 14f);
            label.color = Color.white;

            var dr = _lastDressing;
            return $"{def.id}: {n * n} chunks, {d.rivers.Count} rivers, {d.pools.Count} pools, {d.bridges.Count} bridges, {NavReport(d)}; " +
                   $"dressing: {dr.landmarks} landmarks, {dr.trees} trees in {dr.groves} groves, {dr.vignettes} vignettes ({dr.vignetteProps} pieces), " +
                   $"{dr.singles} singles, {dr.cover} cover, {dr.blockers} blockers ({dr.removedForPaths} removed to keep paths), {dr.rejected} rejected for overlap";
        }

        /// The game reads the map through a MapTheme in Resources/MapThemes (only the theme in play
        /// is loaded). The spawn spot is the open ground nearest the map centre: no obstacle cell and
        /// no blocking piece within 7 m.
        /// Each map's light (2026-10-01, owner: "every map is too dark"): a three-colour ambient that
        /// fits the theme and its sun. Sky, horizon, ground, sun colour, sun intensity.
        static readonly Dictionary<string, (Color sky, Color eq, Color ground, Color sun, float intensity)> ThemeLight = new()
        {
            ["meadow"]  = (new Color(0.82f, 0.86f, 0.94f), new Color(0.68f, 0.70f, 0.66f), new Color(0.48f, 0.44f, 0.38f), new Color(1f, 0.97f, 0.90f), 1.15f),
            ["forest"]  = (new Color(0.68f, 0.78f, 0.72f), new Color(0.54f, 0.60f, 0.52f), new Color(0.36f, 0.35f, 0.30f), new Color(1f, 0.95f, 0.85f), 1.1f),
            ["swamp"]   = (new Color(0.68f, 0.76f, 0.64f), new Color(0.54f, 0.60f, 0.48f), new Color(0.36f, 0.38f, 0.28f), new Color(0.96f, 1f, 0.86f), 1.05f),
            ["volcano"] = (new Color(0.84f, 0.70f, 0.64f), new Color(0.74f, 0.58f, 0.50f), new Color(0.62f, 0.40f, 0.30f), new Color(1f, 0.88f, 0.76f), 1.18f),
            ["tundra"]  = (new Color(0.58f, 0.64f, 0.76f), new Color(0.48f, 0.54f, 0.64f), new Color(0.38f, 0.42f, 0.50f), new Color(0.92f, 0.95f, 1f), 0.9f),   // snow is bright already
            ["desert"]  = (new Color(0.74f, 0.74f, 0.78f), new Color(0.64f, 0.58f, 0.5f), new Color(0.5f, 0.4f, 0.3f), new Color(1f, 0.92f, 0.8f), 1f),
        };

        public static void ApplyThemeLight(ZombieWar.World.MapTheme theme)
        {
            if (theme == null || !ThemeLight.TryGetValue(theme.id, out var l)) return;
            theme.ambientSky = l.sky; theme.ambientEquator = l.eq; theme.ambientGround = l.ground;
            theme.sunColor = l.sun; theme.sunIntensity = l.intensity;
            EditorUtility.SetDirty(theme);
        }

        static void WriteMapTheme(MapData d, Zone z, GameObject[,] prefabs, List<PropSpot> props)
        {
            const string folder = "Assets/Resources/MapThemes/";
            Directory.CreateDirectory(folder);
            string path = folder + "MapTheme_" + d.def.id + ".asset";
            var theme = AssetDatabase.LoadAssetAtPath<ZombieWar.World.MapTheme>(path);
            if (theme == null) { theme = ScriptableObject.CreateInstance<ZombieWar.World.MapTheme>(); AssetDatabase.CreateAsset(theme, path); }
            int n = d.def.chunks;
            theme.id = d.def.id;
            theme.displayName = z.name;
            theme.chunksPerSide = n;
            theme.chunkSize = ChunkSize;
            theme.chunks = new GameObject[n * n];
            for (int cz = 0; cz < n; cz++) for (int cx = 0; cx < n; cx++) theme.chunks[cz * n + cx] = prefabs[cx, cz];
            theme.hasFoliage = d.def.id == "meadow" || d.def.id == "forest" || d.def.id == "swamp";
            theme.ambientFx = AmbientFxFor(d.def.id);
            ApplyMonsterRoster(theme);
            ApplyThemeLight(theme);

            bool Open(Vector2 p)
            {
                for (float y = -7f; y <= 7f; y += 0.5f)
                    for (float x = -7f; x <= 7f; x += 0.5f)
                    {
                        if (x * x + y * y > 49f) continue;
                        var q = p + new Vector2(x, y);
                        int i = ((Mathf.FloorToInt(q.x / Cell)) % d.G + d.G) % d.G, j = ((Mathf.FloorToInt(q.y / Cell)) % d.G + d.G) % d.G;
                        if (d.blocked[j * d.G + i]) return false;
                    }
                foreach (var s in props) if (s.blocks && WrapDelta(s.p - p, d.M).magnitude < 7f + s.baseR) return false;
                return true;
            }
            var centre = new Vector2(d.M * 0.5f, d.M * 0.5f);
            theme.spawnPoint = centre;
            for (float r = 0f; r < d.M * 0.5f; r += 2f)
            {
                bool found = false;
                int steps = Mathf.Max(1, Mathf.RoundToInt(r * 3f));
                for (int k = 0; k < steps && !found; k++)
                {
                    float a = k * Mathf.PI * 2f / steps;
                    var p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (Open(p)) { theme.spawnPoint = p; found = true; }
                }
                if (found) break;
            }
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
        }

        /// The map's own monsters (17 Blob line, owner 01/10), added to the base roster in play.
        static readonly System.Collections.Generic.Dictionary<string, (string[] crowd, string[] later, string[] elites)> MonsterRoster = new()
        {
            ["meadow"] = (new[] { "BlobChicken", "BlobDog", "BlobCat", "BlobPigeon" }, new[] { "BlobBird" }, new[] { "BlobSpiky" }),
            ["forest"] = (new[] { "BlobMushroom", "BlobOrc", "BlobBird" }, new[] { "BlobNinja" }, new[] { "BlobMushnub" }),
            ["swamp"] = (new[] { "BlobFish", "BlobGreen", "BlobPink" }, new[] { "BlobWizard" }, new[] { "BlobSpiky" }),
            ["volcano"] = (new[] { "BlobCactoro", "BlobAlien", "BlobNinja" }, new[] { "BlobOrc" }, new[] { "BlobSpiky" }),
            ["tundra"] = (new[] { "BlobYeti", "BlobPigeon" }, new[] { "BlobWizard" }, new[] { "BlobMushnub" }),
            ["desert"] = (new[] { "BlobCactoro", "BlobAlien", "BlobChicken" }, new[] { "BlobNinja" }, new[] { "BlobSpiky" }),
        };

        public static void ApplyMonsterRoster(ZombieWar.World.MapTheme theme)
        {
            if (!MonsterRoster.TryGetValue(theme.id, out var r)) return;
            ZombieWar.ZombieData[] Load(string[] names) => names
                .Select(n => AssetDatabase.LoadAssetAtPath<ZombieWar.ZombieData>("Assets/_Project/Data/Zombies/ZD_" + n + ".asset"))
                .Where(x => x != null).ToArray();
            theme.crowd = Load(r.crowd);
            theme.later = Load(r.later);
            theme.elites = Load(r.elites);
            EditorUtility.SetDirty(theme);
        }

        /// Weather that follows the player, from Epic Toon (the only FX source for the game).
        public static GameObject AmbientFxFor(string id)
        {
            const string env = "Assets/ThirdParty/Epic Toon FX/Prefabs/Environment/";
            string path = id switch
            {
                "tundra" => env + "Weather/Snow/SnowLight.prefab",
                "volcano" => env + "Fireflies/FireFliesRed.prefab",
                "forest" => env + "Weather/Wind & Leaves/FallingLeaves.prefab",
                "swamp" => env + "Fireflies/FireFliesGreen.prefab",
                "desert" => env + "Dust/DustMotesCalm.prefab",
                _ => null,
            };
            return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void GameCam(string name, Transform parent, Vector3 at)
        {
            var cam = NewCamera(name, parent);
            cam.transform.position = at + new Vector3(0f, 12f, -8f);
            cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            cam.fieldOfView = 60f;
        }

        static GameObject BakeChunk(MapData d, int cx, int cz, string dir, Material ground, Material fluid, List<PropSpot> props)
        {
            var z = d.z;
            string name = $"Chunk_{d.def.id}_{cx}_{cz}";
            var corner = new Vector2(cx * ChunkSize, cz * ChunkSize);
            var centre = corner + Vector2.one * (ChunkSize * 0.5f);
            var go = new GameObject(name);

            // Ground: 0.5 m quads where the ground sinks, 2 m where the chunk is flat.
            // Two metres of margin: the bank's colour fades in before the ground starts to sink.
            bool wet = false;
            for (float y = -2f; y <= ChunkSize + 2f && !wet; y += 0.5f)
                for (float x = -2f; x <= ChunkSize + 2f && !wet; x += 0.5f)
                    if (Basin(d, corner + new Vector2(x, y)) > 0f) wet = true;
            var g = new GameObject("Ground");
            g.transform.SetParent(go.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = StoreMesh(ChunkGround(d, corner, wet ? 64 : 16, name), dir + name + "_Meshes.asset");
            g.AddComponent<MeshRenderer>().sharedMaterial = ground;

            if (wet && fluid != null)
            {
                var fm = ChunkFluid(d, corner, name);
                if (fm != null)
                {
                    var f = new GameObject("Fluid_" + z.fluid);
                    f.transform.SetParent(go.transform, false);
                    f.AddComponent<MeshFilter>().sharedMesh = StoreMesh(fm, dir + name + "_Meshes.asset");
                    f.AddComponent<MeshRenderer>().sharedMaterial = fluid;
                }
            }

            // Obstacles: this chunk's blocked cells merged into boxes, 1.2 m tall. When maps reach the
            // game, the weapons' hit mask leaves this layer out so bullets fly over.
            int n = Mathf.RoundToInt(ChunkSize / Cell), i0 = cx * n, j0 = cz * n;
            var local = new bool[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    local[j * n + i] = d.blocked[(j0 + j) * d.G + i0 + i];
            var obs = new GameObject("Obstacles");
            obs.transform.SetParent(go.transform, false);
            obs.layer = NavObstacleLayer;
            foreach (var r in MergeRects(local, n))
            {
                var box = obs.AddComponent<BoxCollider>();
                box.center = new Vector3(-ChunkSize * 0.5f + (r.x + r.width * 0.5f) * Cell, 0.6f, -ChunkSize * 0.5f + (r.y + r.height * 0.5f) * Cell);
                box.size = new Vector3(r.width * Cell, 1.2f, r.height * Cell);
            }

            var br = new GameObject("Bridges").transform;
            br.SetParent(go.transform, false);
            foreach (var b in d.bridges)
                if (b.c.x >= corner.x && b.c.y >= corner.y && b.c.x < corner.x + ChunkSize && b.c.y < corner.y + ChunkSize)
                    BuildBridge(br, b.c - centre, b.along, b.span);

            var pr = new GameObject("Props").transform;
            pr.SetParent(go.transform, false);
            PlaceChunkProps(pr, props, corner, centre, dir + name);
            ApplyThemeLook(go, z.id);       // before the snow cover, which copies the material it covers
            if (z.snow) SnowCover(go.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, dir + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// Adds a baked mesh to a binary store (<see cref="ZombieWar.World.BakedMeshStore"/>), or updates
        /// the store's mesh of the same name in place so references to it survive a rebake. The project
        /// writes assets as text, which turns every vertex into YAML: a store keeps them binary.
        static Mesh StoreMesh(Mesh m, string storePath)
        {
            var store = AssetDatabase.LoadMainAssetAtPath(storePath) as ZombieWar.World.BakedMeshStore;
            if (store == null)
            {
                store = ScriptableObject.CreateInstance<ZombieWar.World.BakedMeshStore>();
                store.name = Path.GetFileNameWithoutExtension(storePath);
                AssetDatabase.CreateAsset(store, storePath);
            }
            // Quantised on disk and in the build (memory at run time is unchanged); checked in game shots
            // for gaps between pieces and palette colours bleeding across cells.
            MeshUtility.SetMeshCompression(m, ModelImporterMeshCompression.Medium);
            var old = AssetDatabase.LoadAllAssetsAtPath(storePath).OfType<Mesh>().FirstOrDefault(x => x.name == m.name);
            if (old != null) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); return old; }
            AssetDatabase.AddObjectToAsset(m, store);
            return m;
        }

        static Mesh ChunkGround(MapData d, Vector2 corner, int q, string name)
        {
            var z = d.z;
            int n1 = q + 1;
            var verts = new Vector3[n1 * n1];
            var norms = new Vector3[verts.Length];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            const float e = 0.25f;
            float H(Vector2 p) => Sink(z, Basin(d, p));
            for (int j = 0; j < n1; j++)
                for (int i = 0; i < n1; i++)
                {
                    int k = j * n1 + i;
                    var lp = new Vector2(i * ChunkSize / q, j * ChunkSize / q);
                    var p = corner + lp;
                    float basin = Basin(d, p);
                    verts[k] = new Vector3(lp.x - ChunkSize * 0.5f, Sink(z, basin), lp.y - ChunkSize * 0.5f);
                    // Normals from the height field, identical on both sides of a chunk edge.
                    norms[k] = new Vector3(H(p - new Vector2(e, 0)) - H(p + new Vector2(e, 0)), 2f * e, H(p - new Vector2(0, e)) - H(p + new Vector2(0, e))).normalized;
                    uvs[k] = new Vector2(basin, 0f);
                    var w = new float[4];
                    w[z.primary] = 1f;
                    w[z.secondary] += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.72f, PNoise(p, d.M, 0.07f, 3.1f, 7.7f)));
                    w[z.outerCh] += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.8f, PNoise(p, d.M, 0.045f, 17.2f, 2.9f))) * 0.8f;
                    w[z.bankChannel] += Mathf.InverseLerp(0f, 0.3f, basin) * 3f;
                    if (d.def.pathBands > 0f)
                    {
                        // A path where a slow noise crosses its middle value: thin lines that wind and
                        // join up, and wrap with the map like everything else.
                        float band = Mathf.Abs(PNoise(p, d.M, 0.028f, 41.3f, 13.7f) - 0.5f);
                        w[z.path] += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.025f, band)) * 3f * d.def.pathBands * (1f - Mathf.InverseLerp(0f, 0.2f, basin));
                    }
                    float sum = w[0] + w[1] + w[2] + w[3];
                    cols[k] = new Color(w[0] / sum, w[1] / sum, w[2] / sum, w[3] / sum);
                }
            var tris = new int[q * q * 6];
            int t = 0;
            for (int j = 0; j < q; j++)
                for (int i = 0; i < q; i++)
                {
                    int a = j * n1 + i, b = a + n1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            var m = new Mesh { name = name + "_Ground", vertices = verts, normals = norms, colors = cols, uv = uvs, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        /// The fluid surface over the cells where the ground sinks (plus a cell of margin, which may
        /// look across the chunk edge so both sides agree).
        static Mesh ChunkFluid(MapData d, Vector2 corner, string name)
        {
            const int F = 32;
            float level = d.z.basinDepth * -0.35f;
            bool WetCell(int i, int j)
            {
                float v = 0f;
                for (int s = 0; s <= 2; s++)
                    for (int r = 0; r <= 2; r++)
                        v = Mathf.Max(v, Basin(d, corner + new Vector2(i + s * 0.5f, j + r * 0.5f)));
                return v > 0.05f;
            }
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var index = new Dictionary<int, int>();
            int Vert(int i, int j)
            {
                int key = j * (F + 1) + i;
                if (!index.TryGetValue(key, out int v))
                {
                    v = verts.Count;
                    verts.Add(new Vector3(i - ChunkSize * 0.5f, level, j - ChunkSize * 0.5f));
                    index[key] = v;
                }
                return v;
            }
            for (int j = 0; j < F; j++)
                for (int i = 0; i < F; i++)
                {
                    bool any = false;
                    for (int dj = -1; dj <= 1 && !any; dj++)
                        for (int di = -1; di <= 1 && !any; di++)
                            any = WetCell(i + di, j + dj);
                    if (!any) continue;
                    int a = Vert(i, j), b = Vert(i, j + 1), c = Vert(i + 1, j), e = Vert(i + 1, j + 1);
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(c); tris.Add(b); tris.Add(e);
                }
            if (tris.Count == 0) return null;
            var m = new Mesh { name = name + "_Fluid" };
            m.SetVertices(verts); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// Greedy merge of blocked cells into rectangles (x, y, width, height in cells).
        static List<RectInt> MergeRects(bool[] blocked, int n)
        {
            var used = new bool[blocked.Length];
            var rects = new List<RectInt>();
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int k = j * n + i;
                    if (!blocked[k] || used[k]) continue;
                    int w = 1;
                    while (i + w < n && blocked[k + w] && !used[k + w]) w++;
                    int h = 1;
                    for (; j + h < n; h++)
                    {
                        bool row = true;
                        for (int x = 0; x < w && row; x++) { int kk = (j + h) * n + i + x; row = blocked[kk] && !used[kk]; }
                        if (!row) break;
                    }
                    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) used[(j + y) * n + i + x] = true;
                    rects.Add(new RectInt(i, j, w, h));
                }
            return rects;
        }

        /// Plank bridge (KayKit Wood_Plank_A): boards across the walking direction on two beams, a
        /// short post at each corner. Decoration only; the walk grid already treats it as ground.
        static void BuildBridge(Transform parent, Vector2 c, Vector2 along, float span)
        {
            if (_plank == null) _plank = ConvertedPrefab("KK/Wood_Plank_A") ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)/Prefabs/Wood_Plank_A.prefab");
            if (_plank == null) return;
            var root = new GameObject("Bridge").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(c.x, 0f, c.y);
            root.localRotation = Quaternion.LookRotation(new Vector3(along.x, 0f, along.y));
            const float plankLen = 1.5f, plankW = 0.4f;
            void Piece(Vector3 pos, Quaternion rot, Vector3 scale)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(_plank, root);
                go.transform.localPosition = pos;
                go.transform.localRotation = rot;
                go.transform.localScale = scale;
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            }
            var rng = new System.Random((int)(c.x * 1000 + c.y * 7));
            for (float s = -span * 0.5f; s <= span * 0.5f; s += plankW + 0.05f)
            {
                float jitter = (float)(rng.NextDouble() - 0.5) * 5f;
                Piece(new Vector3((float)(rng.NextDouble() - 0.5) * 0.15f, 0.02f, s), Quaternion.Euler(0f, 90f + jitter, 0f), new Vector3(1f, 1f, BridgeWidth / plankLen));
            }
            foreach (float x in new[] { -BridgeWidth * 0.38f, BridgeWidth * 0.38f })
            {
                Piece(new Vector3(x, -0.12f, 0f), Quaternion.identity, new Vector3(0.8f, 1.2f, (span + 0.8f) / plankLen));
                foreach (float s in new[] { -span * 0.5f - 0.15f, span * 0.5f + 0.15f })
                    Piece(new Vector3(x * 1.22f, 0.3f, s), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.6f, 1.2f, 0.45f));
            }
        }

        /// The map's walk grid as a picture (green = walkable, red = obstacle, yellow = bridge) and a
        /// count of walkable areas over the wrapping map.
        static string NavReport(MapData d)
        {
            int G = d.G;
            var tex = new Texture2D(G, G, TextureFormat.RGB24, false);
            var px = new Color32[G * G];
            for (int k = 0; k < px.Length; k++)
                px[k] = d.bridgeCells[k] && !d.blocked[k] ? new Color32(250, 205, 60, 255) : !d.blocked[k] ? new Color32(96, 170, 90, 255) : new Color32(200, 64, 60, 255);
            tex.SetPixels32(px); tex.Apply();
            Directory.CreateDirectory("Review/M8/env_sandbox");
            File.WriteAllBytes("Review/M8/env_sandbox/nav_" + d.def.id + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return Connected(d, out _, fill: false) ? "one walkable area" : "SPLIT";
        }

        static Camera NewCamera(string name, Transform parent)
        {
            var c = new GameObject(name).AddComponent<Camera>();
            c.transform.SetParent(parent, false);
            c.enabled = false;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.2f, 0.25f, 0.32f);
            c.farClipPlane = 400f;
            c.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().requiresDepthOption =
                UnityEngine.Rendering.Universal.CameraOverrideOption.On;
            return c;
        }
    }
}
