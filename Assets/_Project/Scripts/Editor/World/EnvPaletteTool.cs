using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// One gradient texture for every solid decoration piece (2026-10-01). The KayKit Bits texture is
    /// an 8 × 4 grid of vertical colour gradients; every other pack's solid meshes get their UVs
    /// rewritten into it, so the whole decoration set shares one material and a baked cluster can
    /// merge into one mesh. Done by data and code, never by hand per asset:
    ///  1. Scan the palette: each cell's UV rectangle and its colours bottom to top
    ///     (EnvPalette/palette_map.json).
    ///  2. Scan the meshes: every triangle's current colour (texture under its UVs times the
    ///     material tint, or the material colour when there is no texture), grouped per mesh
    ///     (EnvPalette/mesh_colors.json).
    ///  3. Match: each colour group gets the palette cell and height with the closest colour (CIELAB).
    ///     Colours the palette lacks fill KayKit's empty cells as new gradients. A hand-edited
    ///     palette_overrides.json wins over the automatic choice (EnvPalette/palette_assign.json).
    ///  4. Apply: new mesh assets with rewritten UVs (along the cell's gradient by the vertex's height,
    ///     lighter at the top, the way KayKit shades), submeshes merged, and converted prefabs that the
    ///     sandbox builder uses in place of the vendor ones. Vendor files are never modified.
    /// Foliage (textures whose alpha carries the leaf shape) keeps its own material for now.
    /// </summary>
    public static partial class EnvSandboxBuilder
    {
        const string Pal = "Assets/_Project/Art/EnvPalette/";
        const string KayKitTex = "Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)/Textures/resource_bits_texture.png";
        const string KayKitMat = "Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)/Materials/resource.mat";
        const int PalCols = 8, PalRows = 4, PalSize = 1024;
        const float MissingDeltaE = 11f;      // worse than this, the colour gets a cell of its own
        const float HeightShade = 0.3f;       // share of a cell's gradient spanned from an object's foot to its top
        // Cells KayKit leaves free for users ("space reserved… add your own colours"): (column, row from the bottom).
        static readonly Vector2Int[] FreeCells = { new(4, 1), new(5, 1), new(6, 1), new(7, 1), new(5, 0), new(6, 0), new(7, 0) };

        [System.Serializable] public class PalCell { public int col, row; public float u, v0, v1; public string[] ramp; public bool free; }
        [System.Serializable] public class PalMap { public List<PalCell> cells = new(); }
        [System.Serializable] public class ColGroup { public string hex; public int tris; public string material; }
        [System.Serializable] public class MeshEntry { public string key, prefab, mesh; public int solidTris, foliageTris; public List<ColGroup> groups = new(); }
        [System.Serializable] public class MeshColors { public List<MeshEntry> meshes = new(); }
        [System.Serializable] public class Assign { public string hex; public int col, row; public float v; public float deltaE; public string how; public int tris; }
        [System.Serializable] public class AssignList { public List<Assign> entries = new(); }

        // ── colour maths

        static Vector3 Lab(Color c)
        {
            float L(float x) => x <= 0.04045f ? x / 12.92f : Mathf.Pow((x + 0.055f) / 1.055f, 2.4f);
            float r = L(c.r), g = L(c.g), b = L(c.b);
            float X = (r * 0.4124f + g * 0.3576f + b * 0.1805f) / 0.95047f;
            float Y = r * 0.2126f + g * 0.7152f + b * 0.0722f;
            float Z = (r * 0.0193f + g * 0.1192f + b * 0.9505f) / 1.08883f;
            float F(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
            return new Vector3(116f * F(Y) - 16f, 500f * (F(X) - F(Y)), 200f * (F(Y) - F(Z)));
        }

        static float DeltaE(Color a, Color b) => Vector3.Distance(Lab(a), Lab(b));
        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
        static Color FromHex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static Color[] ReadTexture(Texture t, int size, out int w)
        {
            w = size;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(t, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tx = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tx.ReadPixels(new Rect(0, 0, size, size), 0, 0); tx.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = tx.GetPixels(); Object.DestroyImmediate(tx);
            return px;
        }

        static Color Sample(Color[] px, int w, Vector2 uv)
        {
            float u = uv.x - Mathf.Floor(uv.x), v = uv.y - Mathf.Floor(uv.y);
            return px[Mathf.Clamp((int)(v * w), 0, w - 1) * w + Mathf.Clamp((int)(u * w), 0, w - 1)];
        }

        // ── 1. the palette

        static PalMap ScanPalette(Color[] px, int w)
        {
            var map = new PalMap();
            for (int row = 0; row < PalRows; row++)
                for (int col = 0; col < PalCols; col++)
                {
                    var cell = new PalCell { col = col, row = row, u = (col + 0.5f) / PalCols, v0 = row / (float)PalRows, v1 = (row + 1) / (float)PalRows };
                    cell.free = FreeCells.Contains(new Vector2Int(col, row));
                    cell.ramp = new string[16];
                    for (int s = 0; s < 16; s++)
                        cell.ramp[s] = Hex(Sample(px, w, new Vector2(cell.u, Mathf.Lerp(cell.v0, cell.v1, (s + 0.5f) / 16f))));
                    map.cells.Add(cell);
                }
            return map;
        }

        // ── 2. the meshes

        sealed class Part { public Mesh mesh; public Material[] mats; public string key, prefab; }

        /// Every solid-texture test: a texture whose alpha shapes the surface (leaves, grass cards,
        /// decals) is foliage and keeps its own material.
        static readonly Dictionary<Texture, bool> AlphaCache = new();
        static bool IsFoliage(Material m)
        {
            if (m == null) return false;
            var t = MainTex(m);
            if (t == null) return false;
            if (!AlphaCache.TryGetValue(t, out bool f))
            {
                var px = ReadTexture(t, 128, out _);
                int holes = 0;
                foreach (var c in px) if (c.a < 0.5f) holes++;
                f = holes > px.Length * 0.08f;
                AlphaCache[t] = f;
            }
            return f;
        }

        static Texture MainTex(Material m)
        {
            foreach (var p in new[] { "_BaseMap", "_MainTex", "_Albedo_Map", "_Base_Texture" })
                if (m.HasProperty(p) && m.GetTexture(p) != null) return m.GetTexture(p);
            return null;
        }

        static Color Tint(Material m)
        {
            foreach (var p in new[] { "_Albedo_Tint", "_Color_Tint", "_BaseColor", "_Color" })
                if (m.HasProperty(p)) { var c = m.GetColor(p); c.a = 1f; return c; }
            return Color.white;
        }

        static readonly Dictionary<Texture, Color[]> TexCache = new();

        /// Colour of each triangle of one submesh.
        static Color[] TriangleColours(Mesh mesh, int sub, Material m)
        {
            var tri = mesh.GetTriangles(sub);
            var cols = new Color[tri.Length / 3];
            var tint = Tint(m);
            // Synty's triplanar shader colours by face direction, not by UV: grass on top, dirt on
            // the sides. Use each texture's average colour, picked by the triangle's normal.
            var topTex = m.HasProperty("_Triplanar_Texture_Top") ? m.GetTexture("_Triplanar_Texture_Top") : null;
            if (topTex != null)
            {
                var sideTex = m.GetTexture("_Triplanar_Texture_Side") ?? MainTex(m) ?? topTex;
                Color top = Average(topTex) * tint, side = Average(sideTex) * tint;
                top.a = side.a = 1f;
                var vs = mesh.vertices;
                for (int i = 0; i < cols.Length; i++)
                {
                    var nrm = Vector3.Cross(vs[tri[i * 3 + 1]] - vs[tri[i * 3]], vs[tri[i * 3 + 2]] - vs[tri[i * 3]]).normalized;
                    cols[i] = nrm.y > 0.6f ? top : side;
                }
                return cols;
            }
            var t = MainTex(m);
            var uv = mesh.uv;
            if (t == null || uv == null || uv.Length == 0)
            {
                for (int i = 0; i < cols.Length; i++) cols[i] = tint;
                return cols;
            }
            if (!TexCache.TryGetValue(t, out var px)) { px = ReadTexture(t, 512, out _); TexCache[t] = px; }
            for (int i = 0; i < cols.Length; i++)
            {
                var c = Sample(px, 512, (uv[tri[i * 3]] + uv[tri[i * 3 + 1]] + uv[tri[i * 3 + 2]]) / 3f) * tint;
                c.a = 1f;
                cols[i] = c;
            }
            return cols;
        }

        static readonly Dictionary<Texture, Color> AverageCache = new();
        static Color Average(Texture t)
        {
            if (AverageCache.TryGetValue(t, out var c)) return c;
            var px = ReadTexture(t, 64, out _);
            var sum = Vector4.zero;
            foreach (var p in px) sum += (Vector4)p;
            c = sum / px.Length; c.a = 1f;
            AverageCache[t] = c;
            return c;
        }

        /// Groups colours closer than a small ΔE, so a mesh has a handful of groups, not one per triangle.
        static int GroupOf(List<Color> reps, Color c, float within = 4f)
        {
            for (int i = 0; i < reps.Count; i++) if (DeltaE(reps[i], c) < within) return i;
            reps.Add(c);
            return reps.Count - 1;
        }

        /// Every vendor prefab the sandbox uses, keyed like the builder's kits ("SG/SM_Gen_Env_Rock_03").
        static List<(string key, GameObject prefab)> KitPrefabs()
        {
            var keys = new SortedSet<string>();
            foreach (var z in Zones())
                foreach (var list in new[] { z.mid, z.outer, z.landmark, z.scatter })
                    if (list != null) foreach (var k in list) keys.Add(k);
            keys.Add("KK/Wood_Plank_A");
            var result = new List<(string, GameObject)>();
            foreach (var key in keys)
            {
                var prefab = FindVendorPrefab(key);
                if (prefab != null) result.Add((key, prefab));
            }
            return result;
        }

        static GameObject FindVendorPrefab(string key)
        {
            string pack = key.Substring(0, 2), name = key.Substring(3);
            foreach (var g in AssetDatabase.FindAssets(name + " t:Prefab", new[] { PackDir[pack] }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }

        static string ConvertedPrefabPath(string key) => Pal + "Prefabs/" + key.Replace("/", "_") + ".prefab";

        // ── the whole run

        [MenuItem("HordeCall/World/Palette/Build Palette Kit")]
        public static string BuildPaletteKit()
        {
            Directory.CreateDirectory(Pal + "Meshes");
            Directory.CreateDirectory(Pal + "Prefabs");
            TexCache.Clear(); AlphaCache.Clear(); AverageCache.Clear();
            var log = new System.Text.StringBuilder();

            // 1. Palette.
            var kkTex = AssetDatabase.LoadAssetAtPath<Texture2D>(KayKitTex);
            var palPx = ReadTexture(kkTex, PalSize, out int pw);
            var map = ScanPalette(palPx, pw);
            File.WriteAllText(Pal + "palette_map.json", JsonUtility.ToJson(map, true));

            // 2. Meshes: colour groups per mesh, and the global list of distinct colours.
            var kit = KitPrefabs();
            var colours = new MeshColors();
            var global = new List<Color>();
            var globalTris = new List<int>();
            var parts = new List<Part>();
            var seen = new HashSet<Mesh>();
            foreach (var (key, prefab) in kit)
            {
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    if (mf.sharedMesh == null || r == null || !seen.Add(mf.sharedMesh)) continue;
                    var mesh = mf.sharedMesh;
                    var entry = new MeshEntry { key = key, prefab = AssetDatabase.GetAssetPath(prefab), mesh = mesh.name };
                    var local = new List<Color>(); var localTris = new List<int>(); var localMat = new List<string>();
                    for (int sub = 0; sub < mesh.subMeshCount && sub < r.sharedMaterials.Length; sub++)
                    {
                        var m = r.sharedMaterials[sub];
                        int n = mesh.GetTriangles(sub).Length / 3;
                        if (m == null) continue;
                        if (IsFoliage(m)) { entry.foliageTris += n; continue; }
                        entry.solidTris += n;
                        foreach (var c in TriangleColours(mesh, sub, m))
                        {
                            int g = GroupOf(local, c);
                            if (g == localTris.Count) { localTris.Add(0); localMat.Add(m.name); }
                            localTris[g]++;
                            int gg = GroupOf(global, c, 3f);
                            if (gg == globalTris.Count) globalTris.Add(0);
                            globalTris[gg]++;
                        }
                    }
                    for (int i = 0; i < local.Count; i++) entry.groups.Add(new ColGroup { hex = Hex(local[i]), tris = localTris[i], material = localMat[i] });
                    colours.meshes.Add(entry);
                    parts.Add(new Part { mesh = mesh, mats = r.sharedMaterials, key = key, prefab = entry.prefab });
                }
            }
            File.WriteAllText(Pal + "mesh_colors.json", JsonUtility.ToJson(colours, true));
            log.Append($"kit {kit.Count} prefabs, {parts.Count} meshes, {global.Count} colours; ");

            // 3. Match: closest (cell, height) in the palette; the rest become new cells.
            var assign = new AssignList();
            var missing = new List<(Color c, int tris)>();
            for (int i = 0; i < global.Count; i++)
            {
                var best = BestMatch(map, global[i], out float dE);
                if (dE <= MissingDeltaE) assign.entries.Add(new Assign { hex = Hex(global[i]), col = best.col, row = best.row, v = best.v, deltaE = dE, how = "auto", tris = globalTris[i] });
                else missing.Add((global[i], globalTris[i]));
            }
            var newCells = NewCells(missing, FreeCells.Length);
            for (int k = 0; k < newCells.Count; k++)
            {
                var cell = map.cells.First(c => c.col == FreeCells[k].x && c.row == FreeCells[k].y);
                PaintCell(palPx, pw, cell, newCells[k]);
            }
            foreach (var (c, tris) in missing)
            {
                var best = BestMatch(map, c, out float dE, includeNew: true);
                assign.entries.Add(new Assign { hex = Hex(c), col = best.col, row = best.row, v = best.v, deltaE = dE, how = dE <= MissingDeltaE ? "new cell" : "new cell (far)", tris = tris });
            }
            ApplyOverrides(assign);
            File.WriteAllText(Pal + "palette_map.json", JsonUtility.ToJson(map, true));
            File.WriteAllText(Pal + "palette_assign.json", JsonUtility.ToJson(assign, true));
            var palTex = WritePaletteTexture(palPx, pw);
            var solid = SolidMaterial(palTex);
            int far = assign.entries.Count(e => e.deltaE > MissingDeltaE);
            float avg = assign.entries.Sum(e => e.deltaE * e.tris) / Mathf.Max(1, assign.entries.Sum(e => e.tris));
            log.Append($"{newCells.Count} new cells, mean ΔE {avg:F1}, {far} colours still far; ");

            // 3b. Foliage atlas: every (texture, tint) pair of leaf / grass materials gets a slot.
            var foliage = BuildFoliageAtlas(parts, out var foliageMat);
            log.Append($"foliage atlas {foliage.Count} slots; ");

            // 4. Apply: converted meshes and prefabs.
            var lookup = new List<(Color c, Assign a)>();
            foreach (var e in assign.entries) lookup.Add((FromHex(e.hex), e));
            var converted = new Dictionary<Mesh, (Mesh mesh, bool[] foliageSub)>();
            foreach (var p in parts) converted[p.mesh] = ConvertMesh(p.mesh, p.mats, lookup, foliage);
            int prefabs = 0;
            foreach (var (key, prefab) in kit)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || !converted.TryGetValue(mf.sharedMesh, out var cv)) continue;
                    var r = mf.GetComponent<MeshRenderer>();
                    var mats = new List<Material> { solid };
                    for (int s = 0; s < cv.foliageSub.Length; s++)
                        if (cv.foliageSub[s]) mats.Add(foliage.ContainsKey(SlotKey(r.sharedMaterials[s])) ? foliageMat : r.sharedMaterials[s]);
                    mf.sharedMesh = cv.mesh;
                    r.sharedMaterials = mats.ToArray();
                }
                inst.name = key.Replace("/", "_");
                PrefabUtility.SaveAsPrefabAsset(inst, ConvertedPrefabPath(key));
                Object.DestroyImmediate(inst);
                prefabs++;
            }
            AssetDatabase.SaveAssets();
            log.Append($"{prefabs} prefabs converted");
            return log.ToString();
        }

        struct PalPick { public int col, row; public float v; }

        static PalPick BestMatch(PalMap map, Color c, out float best, bool includeNew = false)
        {
            best = float.MaxValue;
            var pick = new PalPick();
            foreach (var cell in map.cells)
            {
                if (cell.free && !includeNew) continue;
                if (cell.free && cell.ramp.All(h => h == cell.ramp[0])) continue;   // unused free cell
                for (int s = 1; s < 15; s++)       // stay off the cell's very top and bottom
                {
                    float d = DeltaE(FromHex(cell.ramp[s]), c);
                    if (d < best) { best = d; pick = new PalPick { col = cell.col, row = cell.row, v = Mathf.Lerp(cell.v0, cell.v1, (s + 0.5f) / 16f) }; }
                }
            }
            return pick;
        }

        /// The colours the palette lacks, merged down to at most <paramref name="max"/> by tris-weighted
        /// agglomeration in CIELAB (the most-used colours keep their own cell).
        static List<Color> NewCells(List<(Color c, int tris)> missing, int max)
        {
            var clusters = missing.Select(m => (lab: Lab(m.c), col: m.c, w: (float)m.tris)).ToList();
            while (clusters.Count > max)
            {
                float bd = float.MaxValue; int bi = 0, bj = 1;
                for (int i = 0; i < clusters.Count; i++)
                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        float d = Vector3.Distance(clusters[i].lab, clusters[j].lab) * Mathf.Min(clusters[i].w, clusters[j].w);
                        if (d < bd) { bd = d; bi = i; bj = j; }
                    }
                var a = clusters[bi]; var b = clusters[bj];
                float w = a.w + b.w;
                var col = a.w >= b.w ? a.col : b.col;          // keep the dominant colour, not a muddy average
                clusters[bi] = (Lab(col), col, w);
                clusters.RemoveAt(bj);
            }
            return clusters.OrderByDescending(c => c.w).Select(c => c.col).ToList();
        }

        /// A KayKit-style gradient: the colour in the middle, lighter toward the top, darker toward the bottom.
        static void PaintCell(Color[] px, int w, PalCell cell, Color mid)
        {
            Color.RGBToHSV(mid, out float h, out float s, out float v);
            int x0 = cell.col * w / PalCols, x1 = (cell.col + 1) * w / PalCols;
            int y0 = cell.row * w / PalRows, y1 = (cell.row + 1) * w / PalRows;
            for (int y = y0; y < y1; y++)
            {
                float t = (y - y0 + 0.5f) / (y1 - y0);                 // 0 bottom, 1 top
                float vv = Mathf.Clamp01(v * Mathf.Lerp(0.62f, 1.22f, t));
                float ss = Mathf.Clamp01(s * Mathf.Lerp(1.08f, 0.85f, t));
                var c = Color.HSVToRGB(h, ss, vv); c.a = 1f;
                for (int x = x0; x < x1; x++) px[y * w + x] = c;
            }
            for (int s2 = 0; s2 < 16; s2++)
                cell.ramp[s2] = Hex(Sample(px, w, new Vector2(cell.u, Mathf.Lerp(cell.v0, cell.v1, (s2 + 0.5f) / 16f))));
        }

        /// palette_overrides.json: { "entries": [ { "hex": "#4A3829", "col": 0, "row": 2, "v": 0.6 } ] }
        static void ApplyOverrides(AssignList assign)
        {
            string path = Pal + "palette_overrides.json";
            if (!File.Exists(path)) return;
            var over = JsonUtility.FromJson<AssignList>(File.ReadAllText(path));
            foreach (var o in over.entries)
                foreach (var e in assign.entries)
                    if (DeltaE(FromHex(e.hex), FromHex(o.hex)) < 6f) { e.col = o.col; e.row = o.row; e.v = o.v; e.how = "manual"; }
        }

        static Texture2D WritePaletteTexture(Color[] px, int w)
        {
            string path = Pal + "T_EnvPalette.png";
            var tex = new Texture2D(w, w, TextureFormat.RGBA32, false);
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            var src = (TextureImporter)AssetImporter.GetAtPath(KayKitTex);
            imp.sRGBTexture = true;
            imp.filterMode = src.filterMode;
            imp.mipmapEnabled = src.mipmapEnabled;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material SolidMaterial(Texture2D palette)
        {
            string path = Pal + "M_EnvSolid.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>(KayKitMat)); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("HordeCall/Env/Solid");     // Toon Lit + see-through dither
            m.SetTexture("_BaseMap", palette);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", palette);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// New mesh: solid submeshes merged into submesh 0 with palette UVs; foliage submeshes follow,
        /// untouched. Vertices shared between colour groups are split.
        static (Mesh, bool[]) ConvertMesh(Mesh src, Material[] mats, List<(Color c, Assign a)> lookup, Dictionary<string, Rect> foliage)
        {
            var verts = src.vertices; var norms = src.normals; var tans = src.tangents; var cols = src.colors;
            var uvs = src.uv; var bw = src.boneWeights;
            float minY = src.bounds.min.y, spanY = Mathf.Max(0.001f, src.bounds.size.y);
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nc = new List<Color>();
            var nuv = new List<Vector2>(); var nbw = new List<BoneWeight>(); var nuv1 = new List<Vector2>(); var nfc = new List<Color>();
            // Trees sway less than grass; the tallest pieces are trees.
            float windAmp = spanY > 2.5f ? 0.35f : 1f;
            var map = new Dictionary<(int, int), int>();
            var solidTris = new List<int>();
            var foliageLists = new List<List<int>>();
            var foliageSub = new bool[src.subMeshCount];

            int Add(int i, int group, Vector2 uv, bool leaf = false)
            {
                if (map.TryGetValue((i, group), out int k)) return k;
                k = nv.Count;
                // Foliage vertex data for the foliage shader: tint 0.5 (none), stiffness by height,
                // wind phase 0 (clusters set their own), amplitude by kind.
                nfc.Add(leaf ? new Color(0.5f, 0.5f, 0.5f, Mathf.Clamp01((verts[i].y - minY) / spanY)) : (cols.Length > 0 ? cols[i] : Color.white));
                nuv1.Add(leaf ? new Vector2(0f, windAmp) : Vector2.zero);
                nv.Add(verts[i]);
                if (norms.Length > 0) nn.Add(norms[i]);
                if (tans.Length > 0) nt.Add(tans[i]);
                if (cols.Length > 0) nc.Add(cols[i]);
                if (bw.Length > 0) nbw.Add(bw[i]);
                nuv.Add(uv);
                map[(i, group)] = k;
                return k;
            }

            for (int sub = 0; sub < src.subMeshCount; sub++)
            {
                var m = sub < mats.Length ? mats[sub] : null;
                var tri = src.GetTriangles(sub);
                if (m != null && IsFoliage(m))
                {
                    foliageSub[sub] = true;
                    var list = new List<int>();
                    bool atlas = foliage.TryGetValue(SlotKey(m), out var slot);
                    foreach (int i in tri)
                    {
                        var uv = uvs.Length > 0 ? uvs[i] : Vector2.zero;
                        if (atlas) uv = new Vector2(slot.x + Mathf.Clamp01(uv.x) * slot.width, slot.y + Mathf.Clamp01(uv.y) * slot.height);
                        list.Add(Add(i, -1 - sub, uv, atlas));
                    }
                    foliageLists.Add(list);
                    continue;
                }
                var tc = m != null ? TriangleColours(src, sub, m) : Enumerable.Repeat(Color.gray, tri.Length / 3).ToArray();
                for (int t = 0; t < tc.Length; t++)
                {
                    // Nearest assigned colour (the global groups were built with the same colours).
                    int best = 0; float bd = float.MaxValue;
                    for (int j = 0; j < lookup.Count; j++) { float d = DeltaE(lookup[j].c, tc[t]); if (d < bd) { bd = d; best = j; } }
                    var a = lookup[best].a;
                    float v0 = a.row / (float)PalRows, v1 = (a.row + 1) / (float)PalRows;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int i = tri[t * 3 + corner];
                        float h = (verts[i].y - minY) / spanY - 0.5f;
                        float v = Mathf.Clamp(a.v + h * HeightShade * (v1 - v0), v0 + (v1 - v0) * 0.04f, v1 - (v1 - v0) * 0.04f);
                        solidTris.Add(Add(i, best, new Vector2((a.col + 0.5f) / PalCols, v)));
                    }
                }
            }
            var mesh = new Mesh { name = src.name + "_Pal" };
            if (nv.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(nv);
            if (nn.Count == nv.Count) mesh.SetNormals(nn);
            if (nt.Count == nv.Count) mesh.SetTangents(nt);
            mesh.SetColors(nfc);
            mesh.SetUVs(0, nuv);
            mesh.SetUVs(1, nuv1);
            if (nbw.Count == nv.Count) { mesh.boneWeights = nbw.ToArray(); mesh.bindposes = src.bindposes; }
            mesh.subMeshCount = 1 + foliageLists.Count;
            mesh.SetTriangles(solidTris, 0);
            for (int f = 0; f < foliageLists.Count; f++) mesh.SetTriangles(foliageLists[f], 1 + f);
            mesh.RecalculateBounds();
            // Stable name: the source asset path and mesh name (instance ids change between sessions).
            uint hash = 2166136261;
            foreach (char ch in AssetDatabase.GetAssetPath(src) + "|" + src.name) hash = (hash ^ ch) * 16777619;
            string path = Pal + "Meshes/" + src.name.Replace("/", "_") + "_" + hash.ToString("x8") + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(mesh, old); Object.DestroyImmediate(mesh); mesh = old; }
            else AssetDatabase.CreateAsset(mesh, path);
            return (mesh, foliageSub);
        }

        const int AtlasSize = 2048, SlotSize = 512, SlotPad = 8;

        static string SlotKey(Material m)
        {
            var t = m != null ? MainTex(m) : null;
            return t == null ? "" : AssetDatabase.GetAssetPath(t) + "|" + Hex(Tint(m));
        }

        /// Packs every foliage texture (times its material tint) into one 2048² atlas of 512² slots.
        /// A material whose UVs tile outside 0–1 cannot live in an atlas slot and keeps its own material.
        static Dictionary<string, Rect> BuildFoliageAtlas(List<Part> parts, out Material material)
        {
            var slots = new Dictionary<string, Rect>();
            var sources = new List<(string key, Material m)>();
            var rejected = new HashSet<string>();
            foreach (var p in parts)
                for (int sub = 0; sub < p.mesh.subMeshCount && sub < p.mats.Length; sub++)
                {
                    var m = p.mats[sub];
                    if (m == null || !IsFoliage(m)) continue;
                    string key = SlotKey(m);
                    if (key == "" || rejected.Contains(key)) continue;
                    var uv = p.mesh.uv;
                    foreach (int i in p.mesh.GetTriangles(sub))
                        if (uv[i].x < -0.02f || uv[i].x > 1.02f || uv[i].y < -0.02f || uv[i].y > 1.02f) { rejected.Add(key); break; }
                    if (!rejected.Contains(key) && !sources.Exists(x => x.key == key)) sources.Add((key, m));
                }
            sources.RemoveAll(x => rejected.Contains(x.key));
            int per = AtlasSize / SlotSize;
            if (sources.Count > per * per) sources.RemoveRange(per * per, sources.Count - per * per);
            var px = new Color[AtlasSize * AtlasSize];
            for (int n = 0; n < sources.Count; n++)
            {
                int sx = n % per, sy = n / per;
                var src = ReadTexture(MainTex(sources[n].m), SlotSize - 2 * SlotPad, out int w);
                var tint = Tint(sources[n].m);
                for (int y = -SlotPad; y < SlotSize - SlotPad; y++)
                    for (int x = -SlotPad; x < SlotSize - SlotPad; x++)
                    {
                        // Edge pixels repeat into the padding so mip levels do not bleed neighbours in.
                        var c = src[Mathf.Clamp(y, 0, w - 1) * w + Mathf.Clamp(x, 0, w - 1)];
                        c.r *= tint.r; c.g *= tint.g; c.b *= tint.b;
                        px[(sy * SlotSize + y + SlotPad) * AtlasSize + sx * SlotSize + x + SlotPad] = c;
                    }
                float inner = (SlotSize - 2f * SlotPad) / AtlasSize;
                slots[sources[n].key] = new Rect((sx * SlotSize + SlotPad) / (float)AtlasSize, (sy * SlotSize + SlotPad) / (float)AtlasSize, inner, inner);
            }
            string path = Pal + "T_EnvFoliage.png";
            var tex = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = true; imp.alphaIsTransparency = true; imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true; imp.alphaTestReferenceValue = 0.5f;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            string mp = Pal + "M_EnvFoliage.mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (material == null) { material = new Material(Shader.Find("HordeCall/Env/Foliage")); AssetDatabase.CreateAsset(material, mp); }
            material.shader = Shader.Find("HordeCall/Env/Foliage");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            if (rejected.Count > 0) Debug.Log("[EnvPalette] foliage kept on its own material (tiling UVs): " + string.Join(", ", rejected));
            return slots;
        }

        /// The sandbox builder places the converted prefab when one exists.
        static GameObject ConvertedPrefab(string key) => AssetDatabase.LoadAssetAtPath<GameObject>(ConvertedPrefabPath(key));

        // ── before / after sheets

        [MenuItem("HordeCall/World/Palette/Render Before-After Sheets")]
        public static string RenderPaletteSheets()
        {
            const int T = 200;
            var byPack = new SortedDictionary<string, List<string>>();
            foreach (var (key, _) in KitPrefabs())
            {
                if (ConvertedPrefab(key) == null) continue;
                string pack = key.Substring(0, 2);
                if (!byPack.TryGetValue(pack, out var l)) byPack[pack] = l = new List<string>();
                l.Add(key);
            }
            Directory.CreateDirectory("Review/M8/palette");
            var util = new PreviewRenderUtility();
            util.camera.fieldOfView = 30f;
            util.camera.clearFlags = CameraClearFlags.SolidColor;
            util.camera.backgroundColor = new Color(0.82f, 0.85f, 0.8f);
            util.camera.nearClipPlane = 0.05f; util.camera.farClipPlane = 500f;
            util.lights[0].intensity = 1.2f; util.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            util.lights[1].intensity = 0.6f;
            util.ambientColor = new Color(0.5f, 0.52f, 0.55f);
            int sheets = 0;
            try
            {
                foreach (var kv in byPack)
                {
                    int n = kv.Value.Count;
                    var sheet = new Texture2D(T * n, T * 2, TextureFormat.RGB24, false);
                    for (int i = 0; i < n; i++)
                    {
                        string key = kv.Value[i];
                        for (int row = 0; row < 2; row++)
                        {
                            var prefab = row == 1 ? FindVendorPrefab(key) : ConvertedPrefab(key);   // top = before
                            var img = RenderThumb(util, prefab, T);
                            sheet.SetPixels(i * T, row * T, T, T, img.GetPixels());
                            Object.DestroyImmediate(img);
                        }
                    }
                    sheet.Apply();
                    File.WriteAllBytes($"Review/M8/palette/{kv.Key}.png", sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);
                    sheets++;
                }
            }
            finally { util.Cleanup(); }
            return sheets + " sheets";
        }

        static Texture2D RenderThumb(PreviewRenderUtility util, GameObject prefab, int size)
        {
            util.BeginPreview(new Rect(0, 0, size, size), GUIStyle.none);
            var go = util.InstantiatePrefabInScene(prefab);
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            float radius = Mathf.Max(0.2f, b.extents.magnitude);
            util.camera.transform.position = b.center + Quaternion.Euler(28f, -35f, 0f) * new Vector3(0f, 0f, -1.15f * radius / Mathf.Sin(15f * Mathf.Deg2Rad));
            util.camera.transform.LookAt(b.center);
            util.camera.Render();
            var rt = (RenderTexture)util.EndPreview();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            Object.DestroyImmediate(go);
            return tex;
        }
    }
}
