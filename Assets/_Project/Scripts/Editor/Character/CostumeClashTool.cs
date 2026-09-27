using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Measures which costume pieces clip through each other (owner 2026-09-27: Random must not
    /// dress the character in pieces that overlap). Every piece is skinned to the same skeleton, so
    /// each one is baked once in the same pose and its surface is voxelised (1.2 cm cells). Two
    /// pieces from slots that can clash (hat/hair/mask/glasses..., gloves/watch/bracelet...) clip
    /// when their surfaces share many cells. Each slot pair has its own normal amount of layering
    /// (a cap always touches hair), so a pair is flagged only when it stands out from its slot
    /// pair: score > median + 3 MAD and above an absolute floor.
    /// Also stores each piece's main colour (texture sampled at the mesh UVs) for colour rules.
    /// Output: Assets/Resources/Character/CostumeCompat.json, read by CostumeRandomizer.
    /// Runs in steps (Begin, Step until done, Finish) so no single call blocks the editor long.
    /// </summary>
    public static class CostumeClashTool
    {
        const string StagePrefab = "Assets/_Project/UI/Prefabs/Preview/MenuCharacterPreviewStage.prefab";
        const string CatalogPath = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        public const string OutPath = "Assets/Resources/Character/CostumeCompat.json";
        const float Voxel = 0.012f;

        /// Slots whose pieces can visibly clip into each other, by body region.
        static readonly string[][] Regions =
        {
            new[] { "Head", "Hair", "HairAccessory", "Mask", "Eyewear", "Earring", "Beard" },
            new[] { "Chest", "Back", "Hands", "Bracelet", "Watch", "HandAccessory" },
        };

        struct Piece { public string id, slot; public HashSet<long> cells; public Color color; }

        static List<(string slot, ModularCostumeCatalog.PartEntry part)> _todo;
        static List<Piece> _done;
        static GameObject _stage;
        static CharacterModularApplier _applier;
        static readonly Dictionary<Texture, Texture2D> _readable = new();

        [MenuItem("HordeCall/Costume/Measure Clashes (full run)")]
        public static void RunAll()
        {
            Begin();
            while (Step(400) > 0) { }
            Debug.Log(Finish());
        }

        public static string Begin()
        {
            var cat = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CatalogPath);
            _todo = new List<(string, ModularCostumeCatalog.PartEntry)>();
            foreach (var region in Regions)
                foreach (var slot in region)
                {
                    var s = cat.GetSlot(slot);
                    if (s == null) continue;
                    foreach (var p in s.parts) if (p.skinnedMesh != null && !string.IsNullOrEmpty(p.itemId)) _todo.Add((slot, p));
                }
            _done = new List<Piece>();
            if (_stage != null) Object.DestroyImmediate(_stage);
            _stage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefab));
            _stage.hideFlags = HideFlags.DontSave;
            _stage.transform.position = new Vector3(0, -500, 0);
            _applier = _stage.GetComponentInChildren<CharacterModularApplier>(true);
            _applier.SetCatalog(cat);
            _applier.EnsureBoneMap(true);
            return $"{_todo.Count} pieces to bake";
        }

        /// <summary>Bakes up to n pieces; returns how many are left.</summary>
        public static int Step(int n)
        {
            if (_todo == null) return 0;
            var mesh = new Mesh();
            for (int k = 0; k < n && _todo.Count > 0; k++)
            {
                var (slot, part) = _todo[_todo.Count - 1];
                _todo.RemoveAt(_todo.Count - 1);
                if (!_applier.Apply(slot, part)) continue;
                var smr = _stage.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Costume_" + slot);
                if (smr == null) continue;
                smr.BakeMesh(mesh, true);
                var piece = new Piece { id = part.itemId, slot = slot, cells = Cells(mesh, smr.transform.localToWorldMatrix), color = MainColor(mesh, part.materials) };
                _done.Add(piece);
                _applier.Clear(slot);
            }
            Object.DestroyImmediate(mesh);
            return _todo.Count;
        }

        public static string Finish()
        {
            if (_stage != null) Object.DestroyImmediate(_stage);
            foreach (var t in _readable.Values) if (t != null) Object.DestroyImmediate(t);
            _readable.Clear();

            var bySlot = _done.GroupBy(p => p.slot).ToDictionary(g => g.Key, g => g.ToList());
            var clashes = new List<(string a, string b, float s)>();
            var report = new StringBuilder();
            int pairsChecked = 0;
            foreach (var region in Regions)
                for (int i = 0; i < region.Length; i++)
                    for (int j = i + 1; j < region.Length; j++)
                    {
                        if (!bySlot.TryGetValue(region[i], out var A) || !bySlot.TryGetValue(region[j], out var B)) continue;
                        // Two views of a clash: the share of the smaller piece's surface that is shared
                        // (a whole piece inside another), and the raw count of shared cells (a small
                        // part of a big piece poking through, e.g. hair buns through a helmet).
                        var scores = new List<(Piece a, Piece b, float s, int n)>();
                        foreach (var a in A) foreach (var b in B) { int n = Shared(a.cells, b.cells); scores.Add((a, b, n / (float)Mathf.Max(1, Mathf.Min(a.cells.Count, b.cells.Count)), n)); }
                        pairsChecked += scores.Count;
                        var (med, cut) = Cut(scores.Select(x => x.s).ToList(), 0.005f, 0.1f);   // 0.06 flagged a beret over a visible earring
                        var (medN, cutN) = Cut(scores.Select(x => (float)x.n).ToList(), 4f, 45f);
                        int flagged = 0;
                        foreach (var x in scores) if (x.s > cut || x.n > cutN) { clashes.Add((x.a.id, x.b.id, x.s)); flagged++; }
                        report.AppendLine($"{region[i]} x {region[j]}: {scores.Count} pairs, share median {med:F3} cut {cut:F3}, cells median {medN:F0} cut {cutN:F0}, clash {flagged}");
                    }

            var sb = new StringBuilder();
            sb.Append("{\"voxel\":").Append(Voxel.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",\"parts\":[");
            for (int i = 0; i < _done.Count; i++)
            {
                var p = _done[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":\"").Append(p.id).Append("\",\"slot\":\"").Append(p.slot).Append("\",\"color\":\"").Append(ColorUtility.ToHtmlStringRGB(p.color)).Append("\"}");
            }
            sb.Append("],\"clash\":[");
            for (int i = 0; i < clashes.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"a\":\"").Append(clashes[i].a).Append("\",\"b\":\"").Append(clashes[i].b).Append("\",\"s\":")
                  .Append(clashes[i].s.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append('}');
            }
            sb.Append("]}");
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            File.WriteAllText(OutPath, sb.ToString());
            AssetDatabase.ImportAsset(OutPath);
            File.WriteAllText("Review/Avatars/../CostumeClashReport.txt", report.ToString());
            return $"{_done.Count} pieces, {pairsChecked} pairs, {clashes.Count} clashes\n{report}";
        }

        static int Shared(HashSet<long> a, HashSet<long> b)
        {
            if (a.Count == 0 || b.Count == 0) return 0;
            var small = a.Count < b.Count ? a : b; var big = small == a ? b : a;
            int n = 0; foreach (var c in small) if (big.Contains(c)) n++;
            return n;
        }

        /// Median, and the outlier cut median + 3 MAD (never below floor).
        static (float med, float cut) Cut(List<float> vals, float minMad, float floor)
        {
            vals.Sort();
            float med = vals[vals.Count / 2];
            var dev = vals.Select(v => Mathf.Abs(v - med)).OrderBy(x => x).ToList();
            float mad = Mathf.Max(dev[dev.Count / 2], minMad);
            return (med, Mathf.Max(med + 3f * mad, floor));
        }

        static long Key(Vector3 p)
        {
            long x = Mathf.FloorToInt(p.x / Voxel) + 100000, y = Mathf.FloorToInt(p.y / Voxel) + 100000, z = Mathf.FloorToInt(p.z / Voxel) + 100000;
            return (x << 40) | (y << 20) | z;
        }

        /// Surface cells: every triangle sampled at half-voxel spacing.
        static HashSet<long> Cells(Mesh m, Matrix4x4 toWorld)
        {
            var set = new HashSet<long>();
            var v = m.vertices; var t = m.triangles;
            for (int i = 0; i < v.Length; i++) v[i] = toWorld.MultiplyPoint3x4(v[i]);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                float len = Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude));
                int steps = Mathf.Clamp(Mathf.CeilToInt(len / (Voxel * 0.5f)), 1, 64);
                for (int u = 0; u <= steps; u++)
                    for (int w = 0; w <= steps - u; w++)
                    {
                        float fu = u / (float)steps, fw = w / (float)steps;
                        set.Add(Key(a + (b - a) * fu + (c - a) * fw));
                    }
            }
            return set;
        }

        /// Average texture colour over the mesh UVs (vertex-weighted), times the material colour.
        static Color MainColor(Mesh m, Material[] mats)
        {
            var mat = mats != null && mats.Length > 0 ? mats[0] : null;
            if (mat == null) return Color.gray;
            Color tint = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.HasProperty("_Color") ? mat.color : Color.white;
            var tex = mat.mainTexture;
            var uv = m.uv;
            if (tex == null || uv == null || uv.Length == 0) return tint;
            var rt = Readable(tex);
            if (rt == null) return tint;
            Color sum = Color.clear; int n = 0;
            for (int i = 0; i < uv.Length; i += Mathf.Max(1, uv.Length / 400))
            {
                sum += rt.GetPixelBilinear(uv[i].x, uv[i].y); n++;
            }
            return n > 0 ? sum / n * tint : tint;
        }

        static Texture2D Readable(Texture t)
        {
            if (_readable.TryGetValue(t, out var r)) return r;
            var tmp = RenderTexture.GetTemporary(Mathf.Min(t.width, 512), Mathf.Min(t.height, 512), 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(t, tmp);
            var prev = RenderTexture.active; RenderTexture.active = tmp;
            r = new Texture2D(tmp.width, tmp.height, TextureFormat.RGBA32, false);
            r.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0); r.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(tmp);
            _readable[t] = r;
            return r;
        }
    }
}
