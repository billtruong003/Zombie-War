using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Env Sandbox map tiles (2026-10-01). The world is cut into 32 × 32 m tiles, the game's chunk
    /// size, and every tile is baked here in the editor: sunken ground, fluid, bridges, props and the
    /// obstacle colliders. Nothing is generated while the game runs; streaming only picks a baked tile.
    ///
    /// Basins full of water, toxic, lava or ice are obstacles: players and enemies walk around them
    /// (colliders on the NavObstacle layer) while bullets fly over. They never shut a player in:
    ///  - A river may only leave a tile through the middle of an edge (a "port"), running straight
    ///    across the edge, so the tile next to it continues the same river with the same bank.
    ///    Tiles are chosen so that ports match (Wang tiles); the edge between two tiles is otherwise
    ///    open ground on both sides.
    ///  - Every river gets plank bridges, and the tile's walkable cells are checked to form one
    ///    connected area. A layout that fails is thrown away and generated again. Walkable pockets
    ///    too small to matter are filled in, so nobody gets stuck in one.
    ///  - Noise (bank wobble, ground texture patches) repeats every 32 m, so it matches across any
    ///    two tiles, whichever variant sits next to which.
    /// </summary>
    public static partial class EnvSandboxBuilder
    {
        const float Tile = 32f, TH = 16f;
        const float Cell = 0.5f;
        const int NavN = 64;                 // walk grid cells per side (0.5 m)
        const int GroundN = 64;              // ground mesh quads per side (0.5 m)
        const float BlockAt = 0.12f;         // basin value from which a cell is not walkable
        const float BridgeWidth = 2.6f;
        const int TileGrid = 3;              // demo grid per theme
        const float TileGridSpacing = 110f, TileGridZ = 150f;
        const int NavObstacleLayer = 10;     // ProjectSettings/TagManager.asset: "NavObstacle"

        // Edge ports: bit 0 = east (+x), 1 = north (+z), 2 = west (-x), 3 = south (-z).
        const int E = 1, N = 2, W = 4, S = 8;

        sealed class Layout
        {
            public string kind;
            public int ports;                                   // canonical orientation
            public List<Vector2[]> rivers = new();
            public List<Vector3> pools = new();                 // x, z, radius
        }

        sealed class TileBake
        {
            public Layout layout;
            public int rot;
            public int Ports => RotatePorts(layout.ports, rot);
            public string name;
            public GameObject prefab;
            public List<Vector3> bridges = new();              // local x, z, and yaw (for the demo camera)
            public bool[] blocked, bridgeCells;                // walk grid after bridges and pocket fill
        }

        static int RotatePorts(int ports, int rot)
        {
            int r = 0;
            for (int i = 0; i < 4; i++) if ((ports & (1 << i)) != 0) r |= 1 << ((i + rot) % 4);
            return r;
        }

        // Canonical → placed: rotate counter-clockwise (seen from above) by 90° × rot.
        static Vector2 Rot(Vector2 p, int rot)
        {
            for (int i = 0; i < rot; i++) p = new Vector2(-p.y, p.x);
            return p;
        }

        static Vector2 Unrot(Vector2 q, int rot) => Rot(q, (4 - rot) % 4);

        /// Perlin noise that repeats every tile: four samples a tile apart, blended across the tile.
        static float PNoise(Vector2 q, float scale, float ox, float oy)
        {
            float x = q.x + TH, y = q.y + TH;
            float u = x / Tile, v = y / Tile;
            float F(float a, float b) => Mathf.PerlinNoise(a * scale + ox, b * scale + oy);
            float a0 = Mathf.Lerp(F(x, y), F(x - Tile, y), u);
            float a1 = Mathf.Lerp(F(x, y - Tile), F(x - Tile, y - Tile), u);
            return Mathf.Lerp(a0, a1, v);
        }

        // ── layouts (canonical orientation; ports on the west / south side where the kind needs them)

        static Layout MakeLayout(string kind, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var l = new Layout { kind = kind };
            switch (kind)
            {
                case "cross":   // west → east
                    l.ports = W | E;
                    l.rivers.Add(new[] { V(-18, 0), V(-12, 0), V(-4, R(-5, 5)), V(4, R(-5, 5)), V(12, 0), V(18, 0) });
                    break;
                case "bend":    // west → south
                    l.ports = W | S;
                    l.rivers.Add(new[] { V(-18, 0), V(-12, 0), V(R(-6, -2), R(-5, -2)), V(0, -12), V(0, -18) });
                    break;
                case "tee":     // west → east, a branch from the south joins it
                    l.ports = W | E | S;
                    float a = R(-3, 4), b = R(-3, 4);
                    l.rivers.Add(new[] { V(-18, 0), V(-12, 0), V(-4, a), V(4, b), V(12, 0), V(18, 0) });
                    l.rivers.Add(new[] { V(0, -18), V(0, -12), V(R(-2, 2), -6), V(0, (a + b) * 0.5f) });
                    break;
                case "end":     // a stream from the west ends in a pond
                    l.ports = W;
                    var pond = new Vector3(R(2, 7), 0, R(4.5f, 6f));
                    pond.y = R(-5, 5);
                    l.rivers.Add(new[] { V(-18, 0), V(-12, 0), V(-4, R(-3, 3)), V(pond.x - pond.z * 0.5f, pond.y) });
                    l.pools.Add(new Vector3(pond.x, pond.y, pond.z));
                    break;
                case "pools":
                case "lake":
                    int count = kind == "lake" ? 1 + rng.Next(2) : 1 + rng.Next(3);
                    for (int tries = 0; l.pools.Count < count && tries < 60; tries++)
                    {
                        float r = kind == "lake" ? R(5f, 7.5f) : R(2.8f, 5.5f);
                        float lim = TH - r - 2.5f;
                        var c = new Vector3(R(-lim, lim), R(-lim, lim), r);
                        bool clear = true;
                        foreach (var o in l.pools)
                            if (Vector2.Distance(new Vector2(c.x, c.y), new Vector2(o.x, o.y)) < c.z + o.z + 3f) clear = false;
                        if (clear) l.pools.Add(c);
                    }
                    break;
                // "none": open ground
            }
            return l;
        }

        static readonly Dictionary<string, (string kind, float weight)[]> TileMix = new()
        {
            ["meadow"] = new[] { ("none", 3f), ("pools", 2f), ("end", 1.5f), ("cross", 1.5f), ("bend", 1f) },
            ["forest"] = new[] { ("none", 3f), ("pools", 1.5f), ("end", 1.5f), ("cross", 1.5f), ("bend", 1.2f) },
            ["volcano"] = new[] { ("pools", 2f), ("cross", 2f), ("bend", 1.5f), ("tee", 1f), ("end", 1f), ("none", 1f) },
            ["swamp"] = new[] { ("pools", 4f), ("end", 1.5f), ("cross", 1f), ("bend", 1f), ("none", 0.5f) },
            ["tundra"] = new[] { ("lake", 2f), ("pools", 1f), ("none", 2f) },
        };

        // ── basin / height in the placed frame

        static float TileBasin(Zone z, Layout l, int rot, Vector2 q)
        {
            var p = Unrot(q, rot);
            float warp = PNoise(q, 0.17f, 11.3f, 5.1f) - 0.5f;
            float v = 0f;
            foreach (var c in l.pools)
            {
                float d = Vector2.Distance(p, new Vector2(c.x, c.y)) + warp * c.z * 0.7f;
                v = Mathf.Max(v, 1f - d / c.z);
            }
            foreach (var r in l.rivers)
            {
                float d = DistToPolyline(p, r) + warp * z.riverWidth * 0.8f;
                v = Mathf.Max(v, (1f - d / z.riverWidth) * 0.85f);
            }
            return Mathf.Clamp01(v);
        }

        // ── one tile

        static TileBake BakeTile(Zone z, Layout l, int rot, string name, Material ground, Material fluid)
        {
            var t = new TileBake { layout = l, rot = rot, name = name };
            string dir = Art + "Tiles/" + z.id + "/";
            Directory.CreateDirectory(dir);
            var go = new GameObject(name);
            var rng = new System.Random(name.GetHashCode());

            // Walk grid: blocked where the basin starts to sink.
            var blocked = new bool[NavN * NavN];
            for (int j = 0; j < NavN; j++)
                for (int i = 0; i < NavN; i++)
                    blocked[j * NavN + i] = TileBasin(z, l, rot, CellCenter(i, j)) > BlockAt;

            // Bridges over every river, in the canonical frame, then carved out of the grid.
            var bridgeRects = new List<(Vector2 c, Vector2 along, float span)>();
            foreach (var river in l.rivers)
                foreach (var b in BridgeSpots(z, l, rot, river))
                {
                    bridgeRects.Add(b);
                    Carve(blocked, b.c, b.along, b.span + 1f, BridgeWidth, false);
                }

            if (!Connected(blocked, out int filled)) { Object.DestroyImmediate(go); return null; }
            t.blocked = blocked;
            t.bridgeCells = new bool[blocked.Length];
            foreach (var b in bridgeRects) Carve(t.bridgeCells, b.c, b.along, b.span + 1f, BridgeWidth, true);

            // Ground.
            var g = new GameObject("Ground");
            g.transform.SetParent(go.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = SaveMesh(TileGroundMesh(z, l, rot, name), dir + name + "_Ground.asset");
            g.AddComponent<MeshRenderer>().sharedMaterial = ground;

            // Fluid: only the cells that hold it.
            var fm = TileFluidMesh(z, l, rot, name);
            if (fm != null)
            {
                var f = new GameObject("Fluid_" + z.fluid);
                f.transform.SetParent(go.transform, false);
                f.AddComponent<MeshFilter>().sharedMesh = SaveMesh(fm, dir + name + "_Fluid.asset");
                f.AddComponent<MeshRenderer>().sharedMaterial = fluid;
            }

            // Bridges.
            var br = new GameObject("Bridges").transform;
            br.SetParent(go.transform, false);
            foreach (var b in bridgeRects)
            {
                BuildBridge(br, b.c, b.along, b.span);
                t.bridges.Add(new Vector3(b.c.x, b.c.y, Mathf.Atan2(b.along.x, b.along.y) * Mathf.Rad2Deg));
            }

            // Obstacles: blocked cells merged into boxes, 1.2 m tall. When tiles reach the game, the
            // weapons' hit mask leaves this layer out so bullets fly over the water.
            var obs = new GameObject("Obstacles");
            obs.transform.SetParent(go.transform, false);
            obs.layer = NavObstacleLayer;
            foreach (var r in MergeRects(blocked))
            {
                var box = obs.AddComponent<BoxCollider>();
                box.center = new Vector3(-TH + (r.x + r.width * 0.5f) * Cell, 0.6f, -TH + (r.y + r.height * 0.5f) * Cell);
                box.size = new Vector3(r.width * Cell, 1.2f, r.height * Cell);
            }

            // Props, kept off the banks and out of the bridge approaches; decoration never collides.
            var props = new GameObject("Props").transform;
            props.SetParent(go.transform, false);
            bool Free(Vector2 q)
            {
                foreach (var o in new[] { Vector2.zero, new Vector2(1.5f, 0f), new Vector2(-1.5f, 0f), new Vector2(0f, 1.5f), new Vector2(0f, -1.5f) })
                    if (TileBasin(z, l, rot, q + o) > 0f) return false;
                foreach (var b in bridgeRects)
                {
                    var d = q - b.c;
                    var side = new Vector2(b.along.y, -b.along.x);
                    if (Mathf.Abs(Vector2.Dot(d, b.along)) < b.span * 0.5f + 3f && Mathf.Abs(Vector2.Dot(d, side)) < BridgeWidth * 0.5f + 1.2f) return false;
                }
                return true;
            }
            float step = z.gridStep;
            for (float gx = -TH + step * 0.5f; gx < TH; gx += step)
                for (float gz = -TH + step * 0.5f; gz < TH; gz += step)
                {
                    var c = new Vector2(gx + (float)(rng.NextDouble() * 3 - 1.5), gz + (float)(rng.NextDouble() * 3 - 1.5));
                    if (!Free(c)) continue;
                    if (rng.NextDouble() < 0.3) Scatter(z, props, rng, new[] { c }, z.outer, 1, 0f);
                    else Scatter(z, props, rng, new[] { c }, z.mid, 1 + rng.Next(2), 1.4f);
                }
            int loose = Mathf.RoundToInt(z.scatterCount * (Tile * Tile) / (40f * 40f));
            for (int i = 0; i < loose; i++)
            {
                var q = new Vector2((float)(rng.NextDouble() * 2 - 1) * TH, (float)(rng.NextDouble() * 2 - 1) * TH);
                if (TileBasin(z, l, rot, q) > 0f) continue;
                Place(z.scatter[rng.Next(z.scatter.Length)], props, q, (float)rng.NextDouble() * 360f, 1f);
            }
            foreach (var col in props.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            if (z.snow) SnowCover(go.transform);

            t.prefab = PrefabUtility.SaveAsPrefabAsset(go, dir + name + ".prefab");
            Object.DestroyImmediate(go);
            if (filled > 0) Debug.Log($"[EnvTiles] {name}: filled {filled} cut-off cells");
            return t;
        }

        static Vector2 CellCenter(int i, int j) => new(-TH + (i + 0.5f) * Cell, -TH + (j + 0.5f) * Cell);

        /// Where a river gets bridges: evenly along the part of it inside the tile (away from the
        /// edges, where the neighbour tile's bridges would crowd them), square across the flow.
        static IEnumerable<(Vector2 c, Vector2 along, float span)> BridgeSpots(Zone z, Layout l, int rot, Vector2[] river)
        {
            // Sample the polyline, keep the stretch inside |x|, |z| < 11.
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < river.Length; i++)
                for (int s = 0; s < 20; s++)
                {
                    var p = Vector2.Lerp(river[i], river[i + 1], s / 20f);
                    if (Mathf.Abs(p.x) < 11f && Mathf.Abs(p.y) < 11f) pts.Add(p);
                }
            if (pts.Count < 2) yield break;
            float len = 0f;
            for (int i = 1; i < pts.Count; i++) len += Vector2.Distance(pts[i - 1], pts[i]);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 13f));
            for (int k = 0; k < n; k++)
            {
                float target = len * (k + 0.5f) / n, acc = 0f;
                for (int i = 1; i < pts.Count; i++)
                {
                    float d = Vector2.Distance(pts[i - 1], pts[i]);
                    if (acc + d < target) { acc += d; continue; }
                    var cCanon = pts[i];
                    var flow = (pts[i] - pts[i - 1]).normalized;
                    var c = Rot(cCanon, rot);
                    var along = Rot(new Vector2(-flow.y, flow.x), rot);     // across the river
                    // March out to both banks.
                    float s1 = 0f, s2 = 0f;
                    while (s1 < 12f && TileBasin(z, l, rot, c + along * s1) > 0.03f) s1 += 0.25f;
                    while (s2 < 12f && TileBasin(z, l, rot, c - along * s2) > 0.03f) s2 += 0.25f;
                    var centre = c + along * ((s1 - s2) * 0.5f);
                    yield return (centre, along, s1 + s2 + 0.8f);
                    break;
                }
            }
        }

        static void Carve(bool[] blocked, Vector2 c, Vector2 along, float length, float width, bool value)
        {
            var side = new Vector2(along.y, -along.x);
            for (int j = 0; j < NavN; j++)
                for (int i = 0; i < NavN; i++)
                {
                    var d = CellCenter(i, j) - c;
                    if (Mathf.Abs(Vector2.Dot(d, along)) <= length * 0.5f && Mathf.Abs(Vector2.Dot(d, side)) <= width * 0.5f)
                        blocked[j * NavN + i] = value;
                }
        }

        /// One walkable area? Pockets under ~4 m² are filled in; any larger second area fails.
        static bool Connected(bool[] blocked, out int filled)
        {
            filled = 0;
            var comp = new int[blocked.Length];
            var sizes = new List<int> { 0 };
            var stack = new Stack<int>();
            for (int s = 0; s < blocked.Length; s++)
            {
                if (blocked[s] || comp[s] != 0) continue;
                int id = sizes.Count, size = 0;
                sizes.Add(0);
                comp[s] = id; stack.Push(s);
                while (stack.Count > 0)
                {
                    int k = stack.Pop(); size++;
                    int i = k % NavN, j = k / NavN;
                    void Try(int ni, int nj)
                    {
                        if (ni < 0 || nj < 0 || ni >= NavN || nj >= NavN) return;
                        int nk = nj * NavN + ni;
                        if (blocked[nk] || comp[nk] != 0) return;
                        comp[nk] = id; stack.Push(nk);
                    }
                    Try(i + 1, j); Try(i - 1, j); Try(i, j + 1); Try(i, j - 1);
                }
                sizes[id] = size;
            }
            int main = 0;
            for (int i = 1; i < sizes.Count; i++) if (main == 0 || sizes[i] > sizes[main]) main = i;
            for (int i = 1; i < sizes.Count; i++)
                if (i != main && sizes[i] >= 16) return false;
            for (int k = 0; k < blocked.Length; k++)
                if (!blocked[k] && comp[k] != main) { blocked[k] = true; filled++; }
            return true;
        }

        /// Greedy merge of blocked cells into rectangles (x, y, width, height in cells).
        static List<RectInt> MergeRects(bool[] blocked)
        {
            var used = new bool[blocked.Length];
            var rects = new List<RectInt>();
            for (int j = 0; j < NavN; j++)
                for (int i = 0; i < NavN; i++)
                {
                    int k = j * NavN + i;
                    if (!blocked[k] || used[k]) continue;
                    int w = 1;
                    while (i + w < NavN && blocked[k + w] && !used[k + w]) w++;
                    int h = 1;
                    for (; j + h < NavN; h++)
                    {
                        bool row = true;
                        for (int x = 0; x < w && row; x++) { int kk = (j + h) * NavN + i + x; row = blocked[kk] && !used[kk]; }
                        if (!row) break;
                    }
                    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) used[(j + y) * NavN + i + x] = true;
                    rects.Add(new RectInt(i, j, w, h));
                }
            return rects;
        }

        static Mesh SaveMesh(Mesh m, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static float TileHeight(Zone z, Layout l, int rot, Vector2 q) => Sink(z, TileBasin(z, l, rot, q));

        static Mesh TileGroundMesh(Zone z, Layout l, int rot, string name)
        {
            int n1 = GroundN + 1;
            var verts = new Vector3[n1 * n1];
            var norms = new Vector3[verts.Length];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            const float e = 0.25f;
            for (int j = 0; j < n1; j++)
                for (int i = 0; i < n1; i++)
                {
                    int k = j * n1 + i;
                    var q = new Vector2(-TH + i * Tile / GroundN, -TH + j * Tile / GroundN);
                    float basin = TileBasin(z, l, rot, q);
                    verts[k] = new Vector3(q.x, Sink(z, basin), q.y);
                    // Normals from the height function, not the triangles: the same on both sides of a
                    // tile edge, so a river crossing it shades without a seam.
                    float hx = TileHeight(z, l, rot, q - new Vector2(e, 0)) - TileHeight(z, l, rot, q + new Vector2(e, 0));
                    float hz = TileHeight(z, l, rot, q - new Vector2(0, e)) - TileHeight(z, l, rot, q + new Vector2(0, e));
                    norms[k] = new Vector3(hx, 2f * e, hz).normalized;
                    uvs[k] = new Vector2(basin, 0f);
                    var w = new float[4];
                    w[z.primary] = 1f;
                    w[z.secondary] += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.7f, PNoise(q, 0.08f, 3.1f, 7.7f)));
                    w[z.bankChannel] += Mathf.InverseLerp(0f, 0.3f, basin) * 3f;
                    float sum = w[0] + w[1] + w[2] + w[3];
                    cols[k] = new Color(w[0] / sum, w[1] / sum, w[2] / sum, w[3] / sum);
                }
            var tris = new int[GroundN * GroundN * 6];
            int t = 0;
            for (int j = 0; j < GroundN; j++)
                for (int i = 0; i < GroundN; i++)
                {
                    int a = j * n1 + i, b = a + n1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            var m = new Mesh { name = name + "_Ground", vertices = verts, normals = norms, colors = cols, uv = uvs, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        /// The fluid surface, only over the cells where the ground sinks (plus one cell of margin).
        static Mesh TileFluidMesh(Zone z, Layout l, int rot, string name)
        {
            if (l.rivers.Count == 0 && l.pools.Count == 0) return null;
            const int F = 32;                  // 1 m cells
            float level = z.basinDepth * -0.35f;
            var wet = new bool[F * F];
            for (int j = 0; j < F; j++)
                for (int i = 0; i < F; i++)
                {
                    float x0 = -TH + i, z0 = -TH + j;
                    float v = 0f;
                    for (int s = 0; s <= 2; s++)
                        for (int r = 0; r <= 2; r++)
                            v = Mathf.Max(v, TileBasin(z, l, rot, new Vector2(x0 + s * 0.5f, z0 + r * 0.5f)));
                    wet[j * F + i] = v > 0.05f;
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
                    verts.Add(new Vector3(-TH + i, level, -TH + j));
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
                        {
                            int ni = i + di, nj = j + dj;
                            if (ni >= 0 && nj >= 0 && ni < F && nj < F && wet[nj * F + ni]) any = true;
                        }
                    if (!any) continue;
                    int a = Vert(i, j), b = Vert(i, j + 1), c = Vert(i + 1, j), d = Vert(i + 1, j + 1);
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(c); tris.Add(b); tris.Add(d);
                }
            if (tris.Count == 0) return null;
            var m = new Mesh { name = name + "_Fluid" };
            m.SetVertices(verts); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static GameObject _plank;

        /// Plank bridge: boards across the walking direction on two beams, a post at each corner.
        static void BuildBridge(Transform parent, Vector2 c, Vector2 along, float span)
        {
            if (_plank == null) _plank = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)/Prefabs/Wood_Plank_A.prefab");
            if (_plank == null) return;
            var root = new GameObject("Bridge").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(c.x, 0f, c.y);
            root.localRotation = Quaternion.LookRotation(new Vector3(along.x, 0f, along.y));   // local z = walking direction
            const float plankLen = 1.5f, plankW = 0.4f;   // KayKit Wood_Plank_A: long along its z
            void Piece(Vector3 pos, Quaternion rot, Vector3 scale)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(_plank, root);
                go.transform.localPosition = pos;
                go.transform.localRotation = rot;
                go.transform.localScale = scale;
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            }
            var rng = new System.Random((int)(c.x * 1000 + c.y * 7));
            // Boards across the walking direction, a hand's gap apart, each a little askew.
            for (float s = -span * 0.5f; s <= span * 0.5f; s += plankW + 0.05f)
            {
                float jitter = (float)(rng.NextDouble() - 0.5) * 5f;
                Piece(new Vector3((float)(rng.NextDouble() - 0.5) * 0.15f, 0.02f, s), Quaternion.Euler(0f, 90f + jitter, 0f), new Vector3(1f, 1f, BridgeWidth / plankLen));
            }
            // Two beams underneath along the span, a short post at each corner.
            foreach (float x in new[] { -BridgeWidth * 0.38f, BridgeWidth * 0.38f })
            {
                Piece(new Vector3(x, -0.12f, 0f), Quaternion.identity, new Vector3(0.8f, 1.2f, (span + 0.8f) / plankLen));
                foreach (float s in new[] { -span * 0.5f - 0.15f, span * 0.5f + 0.15f })
                    Piece(new Vector3(x * 1.22f, 0.3f, s), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.6f, 1.2f, 0.45f));
            }
        }

        // ── demo grids in the sandbox: a 3 × 3 patch per theme, tiles chosen so every port matches

        static string BuildTileGrids(Transform root)
        {
            var log = new System.Text.StringBuilder();
            AssetDatabase.DeleteAsset(Art + "Tiles");     // variants are rebaked every build
            var zones = Zones();
            int col = 0;
            foreach (var kv in TileMix)
            {
                var z = zones.Find(x => x.id == kv.Key);
                if (z == null) continue;
                var origin = new Vector3(col * TileGridSpacing, 0f, TileGridZ);
                col++;
                log.Append(BuildTileGrid(z, kv.Value, root, origin)).Append("; ");
            }
            return log.ToString();
        }

        static string BuildTileGrid(Zone z, (string kind, float weight)[] mix, Transform root, Vector3 origin)
        {
            var ground = new Material(GroundMaterial(z));
            ground.SetFloat("_BasinFromUV", 1f);
            string gp = Art + "Materials/M_TileGround_" + z.id + ".mat";
            AssetDatabase.DeleteAsset(gp);
            AssetDatabase.CreateAsset(ground, gp);
            var fluid = string.IsNullOrEmpty(z.fluid) ? null : FluidMaterial(z, origin);

            var gridRoot = new GameObject("Tiles_" + z.id).transform;
            gridRoot.SetParent(root, false);
            gridRoot.position = origin;
            var rng = new System.Random(("tiles" + z.id).GetHashCode());
            var placed = new TileBake[TileGrid, TileGrid];
            var cache = new Dictionary<string, TileBake>();
            int bakes = 0, rejects = 0, missing = 0;
            Vector3? cameraAt = null;

            for (int gz = 0; gz < TileGrid; gz++)
                for (int gx = 0; gx < TileGrid; gx++)
                {
                    int needW = gx > 0 ? (((placed[gx - 1, gz]?.Ports ?? 0) & E) != 0 ? 1 : 0) : -1;
                    int needS = gz > 0 ? (((placed[gx, gz - 1]?.Ports ?? 0) & N) != 0 ? 1 : 0) : -1;
                    TileBake pick = null;
                    for (int attempt = 0; attempt < 200 && pick == null; attempt++)
                    {
                        string kind = PickKind(mix, rng);
                        int rot = rng.Next(4);
                        int ports = RotatePorts(MakeLayout(kind, new System.Random(0)).ports, rot);
                        if (needW >= 0 && ((ports & W) != 0 ? 1 : 0) != needW) continue;
                        if (needS >= 0 && ((ports & S) != 0 ? 1 : 0) != needS) continue;
                        // Two variants of each kind per theme; a variant is baked once per rotation.
                        int variant = rng.Next(2);
                        string name = $"Tile_{z.id}_{kind}{variant}_r{rot}";
                        if (!cache.TryGetValue(name, out pick))
                        {
                            for (int seed = 0; seed < 12 && pick == null; seed++)
                            {
                                var layout = MakeLayout(kind, new System.Random((z.id + kind + variant).GetHashCode() + seed * 7919));
                                pick = BakeTile(z, layout, rot, name, ground, fluid);
                                if (pick == null) rejects++;
                            }
                            if (pick != null) { cache[name] = pick; bakes++; }
                        }
                    }
                    if (pick == null) { missing++; continue; }
                    placed[gx, gz] = pick;
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(pick.prefab, gridRoot);
                    var local = new Vector3((gx - (TileGrid - 1) * 0.5f) * Tile, 0f, (gz - (TileGrid - 1) * 0.5f) * Tile);
                    inst.transform.localPosition = local;
                    if (cameraAt == null && pick.bridges.Count > 0)
                        cameraAt = origin + local + new Vector3(pick.bridges[0].x, 0f, pick.bridges[0].y);
                }

            string nav = NavReport(z.id, placed);

            // Cameras: the game view over a bridge, a wide angle, and straight down over the patch.
            var at = cameraAt ?? origin;
            var cam = NewCamera("Cam_T_" + z.id, gridRoot);
            cam.transform.position = at + new Vector3(0f, 12f, -8f);
            cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            cam.fieldOfView = 60f;
            var hero = NewCamera("Cam_T_" + z.id + "_Hero", gridRoot);
            hero.transform.position = origin + new Vector3(28f, 55f, -88f);
            hero.transform.LookAt(origin + new Vector3(0f, 0f, 4f));
            hero.fieldOfView = 42f;
            var top = NewCamera("Cam_T_" + z.id + "_Top", gridRoot);
            // Perspective from high up, not orthographic: the fluids read depth as eye distance.
            top.transform.position = origin + new Vector3(0f, 340f, 0f);
            top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            top.fieldOfView = 30f;
            top.farClipPlane = 600f;

            // Pathing demo: a crowd crosses the patch from the south edge to the north edge.
            if (z.id == "volcano" || z.id == "meadow")
            {
                var demo = new GameObject("NavDemo").AddComponent<ZombieWar.WorldNav.EnvNavDemo>();
                demo.transform.SetParent(gridRoot, false);
                float edge = Tile * TileGrid * 0.5f;
                demo.spawnCentre = new Vector3(0f, 0f, -edge + 3f);
                demo.spawnHalfWidth = edge - 4f;
                demo.target = new Vector3(0f, 0f, edge - 5f);
                demo.window = Tile * TileGrid + 8f;
                var navTop = NewCamera("Cam_Nav_" + z.id, gridRoot);
                navTop.transform.position = origin + new Vector3(0f, 340f, 0f);
                navTop.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                navTop.fieldOfView = 30f;
                navTop.farClipPlane = 600f;
            }

            var label = new GameObject("Label").AddComponent<TMPro.TextMeshPro>();
            label.transform.SetParent(gridRoot, false);
            label.transform.localPosition = new Vector3(0f, 0.2f, -Tile * TileGrid * 0.5f - 3f);
            label.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            label.text = z.name + " · ô 32 m";
            label.fontSize = 30f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(80f, 8f);
            label.color = Color.white;
            return $"tiles {z.id}: {bakes} baked, {rejects} rejected, {missing} missing, {nav}";
        }

        /// The walk grid of the whole patch as a picture (green = walkable, red = obstacle, yellow =
        /// bridge) and one flood fill over it: the patch must be a single walkable area.
        static string NavReport(string id, TileBake[,] placed)
        {
            int G = NavN * TileGrid;
            var walk = new bool[G * G];
            var bridge = new bool[G * G];
            for (int ty = 0; ty < TileGrid; ty++)
                for (int tx = 0; tx < TileGrid; tx++)
                {
                    var t = placed[tx, ty];
                    for (int j = 0; j < NavN; j++)
                        for (int i = 0; i < NavN; i++)
                        {
                            int k = (ty * NavN + j) * G + tx * NavN + i;
                            walk[k] = t == null || !t.blocked[j * NavN + i];
                            bridge[k] = t != null && t.bridgeCells[j * NavN + i];
                        }
                }
            var seen = new bool[G * G];
            int areas = 0, biggest = 0, total = 0;
            var stack = new Stack<int>();
            for (int s = 0; s < walk.Length; s++)
            {
                if (!walk[s] || seen[s]) continue;
                areas++;
                int size = 0;
                seen[s] = true; stack.Push(s);
                while (stack.Count > 0)
                {
                    int k = stack.Pop(); size++;
                    int i = k % G, j = k / G;
                    if (i + 1 < G && walk[k + 1] && !seen[k + 1]) { seen[k + 1] = true; stack.Push(k + 1); }
                    if (i > 0 && walk[k - 1] && !seen[k - 1]) { seen[k - 1] = true; stack.Push(k - 1); }
                    if (j + 1 < G && walk[k + G] && !seen[k + G]) { seen[k + G] = true; stack.Push(k + G); }
                    if (j > 0 && walk[k - G] && !seen[k - G]) { seen[k - G] = true; stack.Push(k - G); }
                }
                total += size; biggest = Mathf.Max(biggest, size);
            }
            var tex = new Texture2D(G, G, TextureFormat.RGB24, false);
            var px = new Color32[G * G];
            for (int k = 0; k < px.Length; k++)
                px[k] = bridge[k] ? new Color32(250, 205, 60, 255) : walk[k] ? new Color32(96, 170, 90, 255) : new Color32(200, 64, 60, 255);
            tex.SetPixels32(px); tex.Apply();
            Directory.CreateDirectory("Review/M8/env_sandbox");
            File.WriteAllBytes("Review/M8/env_sandbox/nav_" + id + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"{areas} walkable area(s), {100f * biggest / Mathf.Max(1, total):F1}% in the largest";
        }

        static string PickKind((string kind, float weight)[] mix, System.Random rng)
        {
            float total = 0f;
            foreach (var m in mix) total += m.weight;
            float r = (float)rng.NextDouble() * total;
            foreach (var m in mix) { r -= m.weight; if (r <= 0f) return m.kind; }
            return mix[mix.Length - 1].kind;
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
