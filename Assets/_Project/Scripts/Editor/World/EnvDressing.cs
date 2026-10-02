using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Map dressing without overlaps (2026-10-01). Pieces are placed in layers, biggest first, on the
    /// map's 0.5 m occupancy grid, and every piece claims the ground under its real footprint (measured
    /// from its mesh), so no two bases ever overlap:
    ///  1. reserved ground: basins and their banks, bridge approaches;
    ///  2. landmarks;  3. groves of trees (canopies may touch, trunks never);  4. vignettes (small
    ///  composed clusters of props);  5. single pieces;  6. ground cover (grass, flowers, leaves),
    ///  in noise patches on the themes that have it, kept off every base.
    /// Big pieces (rocks, trunks, large crates, cliffs) block movement: they get a collider on the
    /// NavObstacle layer, and a piece that would cut off part of the map is taken back out.
    /// Each grove and vignette is its own group object, so a station can hide what stands on it.
    /// </summary>
    public static partial class EnvSandboxBuilder
    {
        sealed class Footprint { public float baseR, canopyR, height; public int tris; public bool tall, blocks; }

        static readonly Dictionary<string, Footprint> Footprints = new();

        /// Measured from the (converted) prefab at the scale the builder places it.
        static Footprint FootprintOf(string key)
        {
            if (Footprints.TryGetValue(key, out var f)) return f;
            f = new Footprint { baseR = 0.3f, canopyR = 0.4f, height = 0.5f };
            var prefab = ConvertedPrefab(key) ?? FindVendorPrefab(key);
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = Vector3.zero;
                go.transform.localScale *= PackScale(key.Substring(0, 2)) * PieceScale(key);
                var lod = go.GetComponentInChildren<LODGroup>();
                var renderers = new HashSet<Renderer>();
                if (lod != null && lod.GetLODs().Length > 0) foreach (var r in lod.GetLODs()[0].renderers) renderers.Add(r);
                float minY = float.MaxValue, maxY = float.MinValue;
                var pts = new List<Vector3>();
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    var r = mf.GetComponent<Renderer>();
                    if (mf.sharedMesh == null || (renderers.Count > 0 && !renderers.Contains(r))) continue;
                    f.tris += mf.sharedMesh.triangles.Length / 3;
                    foreach (var v in mf.sharedMesh.vertices)
                    {
                        var w = mf.transform.TransformPoint(v);
                        pts.Add(w);
                        minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
                    }
                }
                if (pts.Count > 0)
                {
                    float baseR = 0f, canopyR = 0f;
                    foreach (var p in pts)
                    {
                        float d = new Vector2(p.x, p.z).magnitude;
                        canopyR = Mathf.Max(canopyR, d);
                        if (p.y < minY + 0.5f) baseR = Mathf.Max(baseR, d);
                    }
                    f.height = maxY - Mathf.Max(0f, minY);
                    f.canopyR = Mathf.Max(0.2f, canopyR);
                    // Trees: the trunk is what stands on the ground; a flared root or a low branch
                    // should not count as the whole base.
                    f.baseR = Mathf.Clamp(baseR, 0.15f, f.canopyR);
                    f.tall = f.height > 2.5f;
                    f.blocks = f.height > 0.9f && f.baseR > 0.3f;
                }
                Object.DestroyImmediate(go);
            }
            Footprints[key] = f;
            return f;
        }

        sealed class Occupancy
        {
            public readonly int G; public readonly float M;
            public readonly byte[] cell;        // 0 free, 1 reserved (basin, bank, bridge), 2 base of a piece
            public Occupancy(int g, float m) { G = g; M = m; cell = new byte[g * g]; }
            int Index(int i, int j) => ((j % G + G) % G) * G + ((i % G + G) % G);
            public bool Free(Vector2 p, float r)
            {
                int ci = Mathf.FloorToInt(p.x / Cell), cj = Mathf.FloorToInt(p.y / Cell), n = Mathf.CeilToInt(r / Cell);
                // The cell under the point itself always counts: with a radius under half a cell no
                // cell centre is in reach, and grass grew on the water and the bridge decks.
                if (cell[Index(ci, cj)] != 0) return false;
                for (int dj = -n; dj <= n; dj++)
                    for (int di = -n; di <= n; di++)
                    {
                        var c = new Vector2((ci + di + 0.5f) * Cell, (cj + dj + 0.5f) * Cell);
                        if ((c - p).sqrMagnitude > r * r) continue;
                        if (cell[Index(ci + di, cj + dj)] != 0) return false;
                    }
                return true;
            }
            public void Claim(Vector2 p, float r, byte v)
            {
                int ci = Mathf.FloorToInt(p.x / Cell), cj = Mathf.FloorToInt(p.y / Cell), n = Mathf.CeilToInt(r / Cell);
                for (int dj = -n; dj <= n; dj++)
                    for (int di = -n; di <= n; di++)
                    {
                        var c = new Vector2((ci + di + 0.5f) * Cell, (cj + dj + 0.5f) * Cell);
                        if ((c - p).sqrMagnitude <= r * r) cell[Index(ci + di, cj + dj)] = v;
                    }
            }
        }

        /// Points kept in 8 m buckets (wrapping), for the canopy and cover spacing tests.
        sealed class SpatialList
        {
            readonly float M; readonly int B; readonly List<(Vector2 p, float r)>[] buckets;
            public SpatialList(float m) { M = m; B = Mathf.Max(1, Mathf.RoundToInt(m / 8f)); buckets = new List<(Vector2, float)>[B * B]; }
            int Bucket(int i, int j) => ((j % B + B) % B) * B + ((i % B + B) % B);
            public void Add(Vector2 p, float r)
            {
                int k = Bucket(Mathf.FloorToInt(p.x / 8f), Mathf.FloorToInt(p.y / 8f));
                (buckets[k] ??= new List<(Vector2, float)>()).Add((p, r));
            }
            public bool Clear(Vector2 p, float r, float factor)
            {
                int bi = Mathf.FloorToInt(p.x / 8f), bj = Mathf.FloorToInt(p.y / 8f);
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        var list = buckets[Bucket(bi + di, bj + dj)];
                        if (list == null) continue;
                        foreach (var (q, qr) in list)
                            if (WrapDelta(p - q, M).magnitude < factor * (r + qr)) return false;
                    }
                return true;
            }
        }

        sealed class PropSpot { public string key; public Vector2 p; public float yaw, scale; public string group; public bool blocks; public float baseR; }

        sealed class DressingReport { public int landmarks, trees, groves, vignettes, vignetteProps, singles, cover, blockers, removedForPaths, rejected; }

        static DressingReport _lastDressing;

        static List<PropSpot> MapProps(MapData d)
        {
            var z = d.z; var def = d.def;
            var rng = new System.Random(("dress" + def.id).GetHashCode());
            float R() => (float)rng.NextDouble();
            var list = new List<PropSpot>();
            var rep = new DressingReport();
            var occ = new Occupancy(d.G, d.M);
            var canopies = new SpatialList(d.M);
            var cover = new SpatialList(d.M);

            // 1. Reserved: basins with a 1.5 m bank, bridges with their approaches.
            for (int j = 0; j < d.G; j++)
                for (int i = 0; i < d.G; i++)
                    if (Basin(d, CellCentre(i, j)) > 0f) occ.Claim(CellCentre(i, j), 1.5f, 1);
            foreach (var b in d.bridges)
                for (float s = -b.span * 0.5f - 3f; s <= b.span * 0.5f + 3f; s += 0.5f)
                    occ.Claim(b.c + b.along * s, BridgeWidth * 0.5f + 1.2f, 1);

            var tall = new List<string>(); var low = new List<string>();
            foreach (var k in z.outer) (FootprintOf(k).tall ? tall : low).Add(k);
            var mid = new List<string>(); foreach (var k in z.mid) mid.Add(k);
            mid.AddRange(low);
            var coverKit = new List<string>();
            foreach (var k in z.scatter) if (FootprintOf(k).tris <= 2000) coverKit.Add(k);   // no 9 900-triangle flower clumps
            if (tall.Count == 0) tall.AddRange(z.outer);

            float scale() => 0.9f + R() * 0.2f;
            bool TryPlace(string key, Vector2 p, float s, string group, bool isTree)
            {
                p = new Vector2(Wrap(p.x, d.M), Wrap(p.y, d.M));
                var f = FootprintOf(key);
                float baseR = f.baseR * s, canopyR = f.canopyR * s;
                if (!occ.Free(p, baseR + 0.3f)) { rep.rejected++; return false; }
                if (isTree && !canopies.Clear(p, canopyR, def.canopyOverlap)) { rep.rejected++; return false; }
                occ.Claim(p, baseR, 2);
                if (isTree) canopies.Add(p, canopyR);
                list.Add(new PropSpot { key = key, p = p, yaw = R() * 360f, scale = s, group = group, blocks = f.blocks, baseR = baseR });
                return true;
            }

            // 2. Landmarks: one try per 64 m cell.
            if (z.landmark != null && z.landmark.Length > 0)
                for (float lx = 32f; lx < d.M; lx += 64f)
                    for (float lz = 32f; lz < d.M; lz += 64f)
                        for (int t = 0; t < 12; t++)
                            if (TryPlace(z.landmark[rng.Next(z.landmark.Length)], new Vector2(lx + (R() - 0.5f) * 30f, lz + (R() - 0.5f) * 30f), 1f, "Landmark", true)) { rep.landmarks++; break; }

            // 3. Groves: centres spread apart, kept where the grove noise is high; trees fill each
            // grove from the middle out. A few lone trees between groves.
            var groveCentres = new List<Vector2>();
            for (int t = 0; t < 4000; t++)
            {
                var c = new Vector2(R() * d.M, R() * d.M);
                if (PNoise(c, d.M, 0.03f, 41.7f, 9.3f) < def.groveNoise) continue;
                bool far = true;
                foreach (var o in groveCentres) if (WrapDelta(c - o, d.M).magnitude < def.groveSpacing) { far = false; break; }
                if (far) groveCentres.Add(c);
            }
            foreach (var c in groveCentres)
            {
                string group = "Grove_" + rep.groves++;
                float radius = Mathf.Lerp(def.groveRadius.x, def.groveRadius.y, R());
                int attempts = Mathf.RoundToInt(radius * radius * 0.9f);
                for (int t = 0; t < attempts; t++)
                {
                    float a = R() * Mathf.PI * 2f, rr = radius * Mathf.Sqrt(R());
                    if (TryPlace(tall[rng.Next(tall.Count)], c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, scale(), group, true)) rep.trees++;
                }
                // Undergrowth at the grove's edge.
                for (int t = 0; t < attempts / 2 && mid.Count > 0; t++)
                {
                    float a = R() * Mathf.PI * 2f, rr = radius * (0.8f + 0.4f * R());
                    TryPlace(mid[rng.Next(mid.Count)], c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, scale(), group, false);
                }
            }
            int lone = Mathf.RoundToInt(def.loneTrees * d.M * d.M / 1000f);
            for (int t = 0; t < lone * 4 && lone > 0; t++)
                if (TryPlace(tall[rng.Next(tall.Count)], new Vector2(R() * d.M, R() * d.M), scale(), "Lone", true) && --lone <= 0) break;

            // 4. Vignettes: 2–4 pieces set tight around one spot (touching, never overlapping).
            int vignettes = Mathf.RoundToInt(def.vignettes * d.M * d.M / 1000f);
            for (int v = 0, tries = 0; v < vignettes && tries < vignettes * 6 && mid.Count > 0; tries++)
            {
                var c = new Vector2(R() * d.M, R() * d.M);
                string group = "Vignette_" + rep.vignettes;
                string first = mid[rng.Next(mid.Count)];
                if (!TryPlace(first, c, scale(), group, false)) continue;
                v++; rep.vignettes++; rep.vignetteProps++;
                float r0 = FootprintOf(first).baseR;
                int extra = 1 + rng.Next(3);
                for (int e = 0, et = 0; e < extra && et < 12; et++)
                {
                    string k = mid[rng.Next(mid.Count)];
                    float a = R() * Mathf.PI * 2f, dist = r0 + FootprintOf(k).baseR + 0.35f + R() * 0.3f;
                    if (TryPlace(k, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist, scale(), group, false)) { e++; rep.vignetteProps++; }
                }
            }

            // 5. Singles.
            int singles = Mathf.RoundToInt(def.singles * d.M * d.M / 1000f);
            for (int t = 0, placed = 0; placed < singles && t < singles * 5 && mid.Count > 0; t++)
                if (TryPlace(mid[rng.Next(mid.Count)], new Vector2(R() * d.M, R() * d.M), scale(), "Singles", false)) { placed++; rep.singles++; }

            // 6. Ground cover in noise patches, off every base, loosely spaced.
            int coverTries = Mathf.RoundToInt(def.coverPer100 * d.M * d.M / 100f);
            for (int t = 0; t < coverTries && coverKit.Count > 0; t++)
            {
                var p = new Vector2(R() * d.M, R() * d.M);
                if (PNoise(p, d.M, def.coverNoise, 23.1f, 71.9f) < def.coverPatch) continue;
                string k = coverKit[rng.Next(coverKit.Count)];
                var f = FootprintOf(k);
                float sc = Mathf.Lerp(def.coverScale.x, def.coverScale.y, R());
                float r = Mathf.Max(0.2f, f.canopyR * 0.6f) * sc;
                if (!occ.Free(p, Mathf.Min(r, 0.4f)) || !cover.Clear(p, r, def.coverSpacing)) continue;
                cover.Add(p, r);
                list.Add(new PropSpot { key = k, p = p, yaw = R() * 360f, scale = sc, group = "Cover" });
                rep.cover++;
            }

            // Blockers must never cut the map: rasterise them, and take back any that shut off an area.
            RemoveCuttingBlockers(d, list, rep);
            rep.blockers = list.FindAll(s => s.blocks).Count;
            _lastDressing = rep;
            return list;
        }

        static void RemoveCuttingBlockers(MapData d, List<PropSpot> list, DressingReport rep)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                var blocked = (bool[])d.blocked.Clone();
                var mark = new int[blocked.Length];
                for (int s = 0; s < list.Count; s++)
                {
                    if (!list[s].blocks) continue;
                    var p = list[s].p; float r = list[s].baseR;
                    int ci = Mathf.FloorToInt(p.x / Cell), cj = Mathf.FloorToInt(p.y / Cell), n = Mathf.CeilToInt(r / Cell);
                    for (int dj = -n; dj <= n; dj++)
                        for (int di = -n; di <= n; di++)
                        {
                            var c = new Vector2((ci + di + 0.5f) * Cell, (cj + dj + 0.5f) * Cell);
                            if ((c - p).sqrMagnitude > r * r) continue;
                            int k = (((cj + dj) % d.G + d.G) % d.G) * d.G + (((ci + di) % d.G + d.G) % d.G);
                            blocked[k] = true; mark[k] = s + 1;
                        }
                }
                var saved = d.blocked;
                d.blocked = blocked;
                bool one = Connected(d, out _, fill: false);
                if (one) { d.blocked = saved; return; }
                // Find the cells of the smaller areas and drop every blocker that borders them.
                var comp = Components(d, out int main);
                d.blocked = saved;
                var drop = new HashSet<int>();
                int G = d.G;
                for (int k = 0; k < comp.Length; k++)
                {
                    if (comp[k] == 0 || comp[k] == main) continue;
                    int i = k % G, j = k / G;
                    foreach (int nk in new[] { j * G + (i + 1) % G, j * G + (i + G - 1) % G, ((j + 1) % G) * G + i, ((j + G - 1) % G) * G + i })
                        if (mark[nk] > 0) drop.Add(mark[nk] - 1);
                }
                if (drop.Count == 0) return;
                var keep = new List<PropSpot>();
                for (int s = 0; s < list.Count; s++) if (!drop.Contains(s)) keep.Add(list[s]);
                rep.removedForPaths += list.Count - keep.Count;
                list.Clear(); list.AddRange(keep);
            }
        }

        const float CoverCell = 8f;

        sealed class MeshBuild
        {
            public readonly List<Vector3> v = new(); public readonly List<Vector3> n = new(); public readonly List<Color> c = new();
            public readonly List<Vector2> uv = new(); public readonly List<Vector2> uv1 = new(); public readonly List<int> t = new();
        }

        /// Ground cover of one chunk merged into one mesh per 8 m cell, material and graphics tier, each
        /// culled by its own bounds. Every clump keeps its own wind phase. Each tuft is dealt to a tier
        /// (CoverTiers): low phones draw the base share, mid adds the next, high draws everything.
        static void BakeCover(Transform parent, List<PropSpot> props, Vector2 corner, Vector2 centre, string meshPath)
        {
            var builds = new Dictionary<(int, int, Material, int), MeshBuild>();
            var rng = new System.Random(meshPath.GetHashCode());
            foreach (var s in props)
            {
                if (s.group != "Cover") continue;
                if (s.p.x < corner.x || s.p.y < corner.y || s.p.x >= corner.x + ChunkSize || s.p.y >= corner.y + ChunkSize) continue;
                var prefab = ConvertedPrefab(s.key) ?? FindVendorPrefab(s.key);
                if (prefab == null) continue;
                var local = s.p - centre;
                var place = Matrix4x4.TRS(new Vector3(local.x, 0f, local.y), Quaternion.Euler(0f, s.yaw, 0f),
                    Vector3.one * s.scale * PackScale(s.key.Substring(0, 2)) * PieceScale(s.key));
                var lod = prefab.GetComponentInChildren<LODGroup>();
                var lod0 = new HashSet<Renderer>();
                if (lod != null && lod.GetLODs().Length > 0) foreach (var r in lod.GetLODs()[0].renderers) lod0.Add(r);
                float phase = (float)rng.NextDouble();
                double deal = rng.NextDouble();
                int tier = deal < CoverShareLow ? 0 : deal < CoverShareMid ? 1 : 2;
                var cellKey = (Mathf.Clamp(Mathf.FloorToInt((local.x + ChunkSize * 0.5f) / CoverCell), 0, 3), Mathf.Clamp(Mathf.FloorToInt((local.y + ChunkSize * 0.5f) / CoverCell), 0, 3));
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    if (mf.sharedMesh == null || r == null || (lod0.Count > 0 && !lod0.Contains(r))) continue;
                    var mesh = mf.sharedMesh;
                    // The prefab's own root rotation and scale stay: a single-mesh FBX (the MegaKit) keeps
                    // its Z-up to Y-up turn on the root, and dropping it laid every grass tuft flat.
                    var rootRS = Matrix4x4.TRS(Vector3.zero, prefab.transform.localRotation, prefab.transform.localScale);
                    var m = place * rootRS * prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    var mv = mesh.vertices; var mn = mesh.normals; var mc = mesh.colors; var muv = mesh.uv;
                    var muv1 = new List<Vector2>(); mesh.GetUVs(1, muv1);
                    for (int sub = 0; sub < mesh.subMeshCount && sub < r.sharedMaterials.Length; sub++)
                    {
                        var mat = r.sharedMaterials[sub];
                        if (mat == null) continue;
                        var bk = (cellKey.Item1, cellKey.Item2, mat, tier);
                        if (!builds.TryGetValue(bk, out var b)) builds[bk] = b = new MeshBuild();
                        var remap = new Dictionary<int, int>();
                        foreach (int i in mesh.GetTriangles(sub))
                        {
                            if (!remap.TryGetValue(i, out int k))
                            {
                                k = b.v.Count; remap[i] = k;
                                b.v.Add(m.MultiplyPoint3x4(mv[i]));
                                b.n.Add(mn.Length > 0 ? m.MultiplyVector(mn[i]).normalized : Vector3.up);
                                b.c.Add(mc.Length > 0 ? mc[i] : new Color(0.5f, 0.5f, 0.5f, 0f));
                                b.uv.Add(muv.Length > 0 ? muv[i] : Vector2.zero);
                                b.uv1.Add(new Vector2(phase, muv1.Count > 0 ? muv1[i].y : 1f));
                            }
                            b.t.Add(k);
                        }
                    }
                }
            }
            if (builds.Count == 0) return;
            var root = new GameObject("CoverClusters").transform;
            root.SetParent(parent, false);
            int idx = 0;
            var tierRenderers = new List<Renderer>();
            var tierOf = new List<int>();
            foreach (var kv in builds)
            {
                var b = kv.Value;
                var mesh = new Mesh { name = $"Cover_{kv.Key.Item1}_{kv.Key.Item2}_{idx++}_t{kv.Key.Item4}" };
                if (b.v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(b.v); mesh.SetNormals(b.n); mesh.SetColors(b.c); mesh.SetUVs(0, b.uv); mesh.SetUVs(1, b.uv1);
                mesh.SetTriangles(b.t, 0); mesh.RecalculateBounds();
                mesh = StoreMesh(mesh, meshPath + "_Meshes.asset");
                var go = new GameObject(mesh.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key.Item3;
                tierRenderers.Add(mr); tierOf.Add(kv.Key.Item4);
            }
            root.gameObject.AddComponent<ZombieWar.World.CoverTiers>().Set(tierRenderers.ToArray(), tierOf.ToArray());
        }

        // Share of the cover every device draws, and the share mid phones draw (the rest is high only).
        const double CoverShareLow = 0.35, CoverShareMid = 0.65;

        static int[] Components(MapData d, out int main)
        {
            int G = d.G, n = G * G;
            var comp = new int[n];
            var sizes = new List<int> { 0 };
            var stack = new Stack<int>();
            for (int s = 0; s < n; s++)
            {
                if (d.blocked[s] || comp[s] != 0) continue;
                int id = sizes.Count, size = 0;
                sizes.Add(0); comp[s] = id; stack.Push(s);
                while (stack.Count > 0)
                {
                    int k = stack.Pop(); size++;
                    int i = k % G, j = k / G;
                    foreach (int nk in new[] { j * G + (i + 1) % G, j * G + (i + G - 1) % G, ((j + 1) % G) * G + i, ((j + G - 1) % G) * G + i })
                        if (!d.blocked[nk] && comp[nk] == 0) { comp[nk] = id; stack.Push(nk); }
                }
                sizes[id] = size;
            }
            main = 1;
            for (int i = 2; i < sizes.Count; i++) if (sizes[i] > sizes[main]) main = i;
            return comp;
        }

        /// Props of one chunk, under one object per group (grove, vignette, singles, cover), with a
        /// NavObstacle collider on every blocking piece.
        static void PlaceChunkProps(Transform parent, List<PropSpot> props, Vector2 corner, Vector2 centre, string meshPath)
        {
            BakeCover(parent, props, corner, centre, meshPath);
            var groups = new Dictionary<string, Transform>();
            var index = new List<ZombieWar.World.EnvDecorIndex.Piece>();
            foreach (var s in props)
            {
                if (s.p.x < corner.x || s.p.y < corner.y || s.p.x >= corner.x + ChunkSize || s.p.y >= corner.y + ChunkSize) continue;
                if (s.group == "Cover") continue;          // baked into cluster meshes
                if (!groups.TryGetValue(s.group, out var g))
                {
                    g = new GameObject(s.group).transform;
                    g.SetParent(parent, false);
                    groups[s.group] = g;
                }
                var go = PlaceObject(s.key, g, s.p - centre, s.yaw, s.scale);
                if (go == null) continue;
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                var entry = new ZombieWar.World.EnvDecorIndex.Piece { go = go, local = s.p - centre, radius = Mathf.Max(s.baseR, 0.3f) };
                if (!s.blocks) { index.Add(entry); continue; }
                var holder = new GameObject("Block");
                holder.transform.SetParent(go.transform.parent, false);
                holder.transform.localPosition = go.transform.localPosition;
                holder.layer = NavObstacleLayer;
                var cap = holder.AddComponent<CapsuleCollider>();
                cap.radius = s.baseR; cap.height = 2f; cap.center = new Vector3(0f, 1f, 0f);
                entry.blocker = holder;
                index.Add(entry);
            }
            // So a station appearing at play time can hide what stands on its spot.
            var idx = parent.gameObject.AddComponent<ZombieWar.World.EnvDecorIndex>();
            idx.pieces = index.ToArray();
            idx.halfSize = ChunkSize * 0.5f;
        }
    }
}
