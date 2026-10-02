using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Collider audit of the baked maps (owner 02/10: some colliders felt wrong in play). For every
    /// chunk of every map it compares each prop's blocker with the prop's real footprint (its mesh
    /// vertices within 0.5 m of its lowest point, seen from above) and flags:
    ///  - OFFSET   the blocker stands away from the footprint's centre (pivot not at the base);
    ///  - OVERSIZE the blocker covers far more ground than the footprint (long pieces as one circle);
    ///  - UNDER    the footprint reaches well outside the blocker (walk-through edges);
    ///  - MISSING  a piece big enough to stop a player has no blocker at all.
    /// Writes Review/M8/colliders/report.txt and one top-down picture per map: footprints grey, prop
    /// blockers orange, water / lava boxes blue, flagged pieces magenta.
    /// </summary>
    public static class ColliderAudit
    {
        const string Out = "Review/M8/colliders/";
        const float Px = 0.1f;   // metres per pixel of the pictures
        static readonly string[] Maps = { "meadow", "forest", "swamp", "volcano", "tundra", "desert" };

        public struct Finding
        {
            public string map, chunk, piece, kind;
            public Vector2 at;           // map metres
            public float value;          // metres (offset, overhang) or area ratio
            public string Line => $"{map,-8} {kind,-8} {value,6:0.00}  {piece}  @({at.x:0.0}, {at.y:0.0})  in {chunk}";
        }

        [MenuItem("HordeCall/World/Audit Map Colliders")]
        public static string Run()
        {
            Directory.CreateDirectory(Out);
            var all = new List<Finding>();
            var summary = new StringBuilder();
            foreach (var id in Maps)
            {
                var theme = AssetDatabase.LoadAssetAtPath<ZombieWar.World.MapTheme>("Assets/Resources/MapThemes/MapTheme_" + id + ".asset");
                if (theme == null) continue;
                var found = AuditMap(theme, out int blockers, out int pieces);
                all.AddRange(found);
                summary.Append($"{id}: {pieces} pieces, {blockers} blockers; ");
                foreach (var k in new[] { "OFFSET", "OVERSIZE", "UNDER", "MISSING", "DRYBANK" })
                    summary.Append(k).Append(' ').Append(k == "DRYBANK" ? (int)found.Find(f => f.kind == k).value : found.FindAll(f => f.kind == k).Count).Append(", ");
                summary.AppendLine();
            }
            var report = new StringBuilder();
            report.AppendLine("Collider audit " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            report.AppendLine(summary.ToString());
            all.Sort((a, b) => b.value.CompareTo(a.value));
            foreach (var f in all) report.AppendLine(f.Line);
            File.WriteAllText(Out + "report.txt", report.ToString());
            return summary.ToString();
        }

        sealed class Shape { public Vector2 c; public float r; public bool box; public Rect rect; }

        static List<Finding> AuditMap(ZombieWar.World.MapTheme theme, out int blockerCount, out int pieceCount)
        {
            var found = new List<Finding>();
            int n = theme.chunksPerSide;
            float size = theme.chunkSize, M = n * size;
            int W = Mathf.RoundToInt(M / Px);
            var px = new Color32[W * W];
            for (int k = 0; k < px.Length; k++) px[k] = new Color32(232, 228, 216, 255);
            blockerCount = 0; pieceCount = 0; int dryBlocked = 0;

            for (int cz = 0; cz < n; cz++)
                for (int cx = 0; cx < n; cx++)
                {
                    var chunk = theme.chunks[cz * n + cx];
                    if (chunk == null) continue;
                    var origin = new Vector2(cx * size + size * 0.5f, cz * size + size * 0.5f);   // chunk centre in map metres
                    Vector2 ToMap(Vector3 local) => origin + new Vector2(local.x, local.z);

                    // Water / lava obstacle boxes.
                    var obs = chunk.transform.Find("Obstacles");
                    if (obs != null)
                        foreach (var b in obs.GetComponents<BoxCollider>())
                        {
                            var c = ToMap(obs.TransformPoint(b.center));
                            FillRect(px, W, new Rect(c.x - b.size.x * 0.5f, c.y - b.size.z * 0.5f, b.size.x, b.size.z), new Color32(110, 160, 230, 255));
                        }

                    // Dry ground inside the water / lava obstacles: the surface shows where the basin
                    // value passes ~0.25 (fluid at 35 % of the basin depth), so ground below that
                    // value is dry bank. Blocked dry bank is an invisible wall round the water.
                    var groundMf = chunk.transform.Find("Ground")?.GetComponent<MeshFilter>();
                    if (obs != null && groundMf != null && groundMf.sharedMesh != null)
                    {
                        var gm = groundMf.sharedMesh; var guv = new List<Vector2>(); gm.GetUVs(0, guv);
                        var gv = gm.vertices; var boxes = obs.GetComponents<BoxCollider>();
                        for (int v = 0; v < gv.Length && v < guv.Count; v++)
                        {
                            if (guv[v].x <= 0.02f || guv[v].x >= 0.25f) continue;   // flat ground, or under the surface
                            var lp = groundMf.transform.localPosition + gv[v];
                            foreach (var b in boxes)
                            {
                                var lc = obs.localPosition + b.center;
                                // Strictly inside (5 cm in): ground vertices lie on the 0.5 m cell lines, and one
                                // on the edge of a box is the bank's edge, not blocked bank.
                                if (Mathf.Abs(lp.x - lc.x) < b.size.x * 0.5f - 0.05f && Mathf.Abs(lp.z - lc.z) < b.size.z * 0.5f - 0.05f)
                                {
                                    dryBlocked++;
                                    var m = ToMap(lp);
                                    FillRect(px, W, new Rect(m.x - 0.1f, m.y - 0.1f, 0.2f, 0.2f), new Color32(230, 40, 200, 255));
                                    break;
                                }
                            }
                        }
                    }

                    var props = chunk.transform.Find("Props");
                    if (props == null) continue;
                    foreach (Transform group in props)
                    {
                        if (group.name == "CoverClusters") continue;
                        // Blockers sit beside their piece, at the same local position.
                        var blockers = new List<Transform>();
                        var pieces = new List<Transform>();
                        foreach (Transform t in group) (t.name == "Block" ? blockers : pieces).Add(t);
                        foreach (var piece in pieces)
                        {
                            pieceCount++;
                            if (!Footprint(piece, out var pts, out float height)) continue;
                            var mapPts = new List<Vector2>(pts.Count);
                            foreach (var p in pts) mapPts.Add(ToMap(p));
                            foreach (var p in mapPts) Plot(px, W, p, new Color32(150, 150, 150, 255));
                            Vector2 centre = Vector2.zero; foreach (var p in mapPts) centre += p; centre /= mapPts.Count;
                            var bb = Bounds2(mapPts);

                            Transform blocker = null;
                            foreach (var b in blockers) if ((b.localPosition - piece.localPosition).sqrMagnitude < 1e-4f) { blocker = b; break; }
                            string name = piece.name;
                            var at = centre;
                            if (blocker == null)
                            {
                                if (EnvSandboxBuilder.IsSoftPlant(name)) continue;   // walked through on purpose
                                // A player is about 0.8 m wide: anything knee high and over half a metre
                                // across stops them in the eye but not in the game.
                                float area = bb.width * bb.height;
                                if (height > 0.45f && area > 0.5f && Mathf.Max(bb.width, bb.height) > 0.8f)
                                {
                                    found.Add(new Finding { map = theme.id, chunk = chunk.name, piece = name, kind = "MISSING", at = at, value = area });
                                    Outline(px, W, bb, new Color32(230, 40, 200, 255));
                                }
                                continue;
                            }
                            blockerCount++;
                            var cap = blocker.GetComponent<CapsuleCollider>();
                            var box = blocker.GetComponent<BoxCollider>();
                            Vector2 bc; float reach; // centre and how far it reaches along the worst direction
                            if (cap != null)
                            {
                                bc = ToMap(props.InverseTransformPoint(blocker.TransformPoint(cap.center)) + props.localPosition);
                                float r = cap.radius * Mathf.Max(blocker.lossyScale.x, blocker.lossyScale.z);
                                reach = r;
                                Circle(px, W, bc, r, new Color32(240, 140, 30, 255));
                                // Offset of the circle from the footprint, overhang of the footprint
                                // outside the circle, and how much more ground the circle covers.
                                float off = (bc - centre).magnitude;
                                float over = 0f; foreach (var p in mapPts) over = Mathf.Max(over, (p - bc).magnitude - r);
                                float ratio = Mathf.PI * r * r / Mathf.Max(0.05f, bb.width * bb.height);
                                bool flagged = false;
                                // Off centre only matters when the footprint pokes out of the circle.
                                if (off > 0.35f && over > 0.15f) { found.Add(new Finding { map = theme.id, chunk = chunk.name, piece = name, kind = "OFFSET", at = at, value = off }); flagged = true; }
                                if (over > 0.3f) { found.Add(new Finding { map = theme.id, chunk = chunk.name, piece = name, kind = "UNDER", at = at, value = over }); flagged = true; }
                                if (ratio > 2.5f && r > 0.6f) { found.Add(new Finding { map = theme.id, chunk = chunk.name, piece = name, kind = "OVERSIZE", at = at, value = ratio }); flagged = true; }
                                if (flagged) Outline(px, W, bb, new Color32(230, 40, 200, 255));
                            }
                            else if (box != null)
                            {
                                // Footprint points in the box's own frame: overhang outside it, and
                                // how much more ground it covers than the footprint.
                                float over = 0f;
                                var inBox = new List<Vector2>();
                                foreach (var p in pts)
                                {
                                    var l = blocker.InverseTransformPoint(props.TransformPoint(p - props.localPosition)) - box.center;
                                    float ox = Mathf.Abs(l.x) - box.size.x * 0.5f, oz = Mathf.Abs(l.z) - box.size.z * 0.5f;
                                    over = Mathf.Max(over, Mathf.Max(ox, oz));
                                    inBox.Add(new Vector2(l.x, l.z));
                                }
                                var fb = Bounds2(inBox);
                                float ratio = box.size.x * box.size.z / Mathf.Max(0.05f, fb.width * fb.height);
                                // The box drawn as its four corners joined.
                                var corners = new Vector3[4];
                                for (int k = 0; k < 4; k++)
                                {
                                    var lc = box.center + new Vector3((k == 0 || k == 3 ? -0.5f : 0.5f) * box.size.x, 0f, (k < 2 ? -0.5f : 0.5f) * box.size.z);
                                    corners[k] = props.InverseTransformPoint(blocker.TransformPoint(lc)) + props.localPosition;
                                }
                                for (int k = 0; k < 4; k++) Line(px, W, ToMap(corners[k]), ToMap(corners[(k + 1) % 4]), new Color32(240, 140, 30, 255));
                                bool flagged = false;
                                if (over > 0.3f) { found.Add(new Finding { map = theme.id, chunk = chunk.name, piece = name, kind = "UNDER", at = at, value = over }); flagged = true; }
                                // Thin pieces get a 0.3 m thick box on purpose (thinner and players slip through).
                                if (ratio > 2.5f && Mathf.Min(box.size.x, box.size.z) > 0.31f) { found.Add(new Finding { map = theme.id, chunk = chunk.name, piece = name, kind = "OVERSIZE", at = at, value = ratio }); flagged = true; }
                                if (flagged) Outline(px, W, bb, new Color32(230, 40, 200, 255));
                            }
                        }
                    }
                }

            if (dryBlocked > 0) found.Add(new Finding { map = theme.id, chunk = "(whole map)", piece = "dry bank inside water/lava obstacles (ground vertices)", kind = "DRYBANK", at = Vector2.zero, value = dryBlocked });
            var tex = new Texture2D(W, W, TextureFormat.RGBA32, false);
            tex.SetPixels32(px); tex.Apply();
            File.WriteAllBytes(Out + "colliders_" + theme.id + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return found;
        }

        // The piece's footprint: its mesh vertices within 0.5 m of its lowest point, in the chunk's
        // Props space (x, z), plus its height. LOD0 only.
        static bool Footprint(Transform piece, out List<Vector3> pts, out float height)
        {
            pts = new List<Vector3>(); height = 0f;
            var props = piece.parent.parent;
            var lod = piece.GetComponentInChildren<LODGroup>();
            var lod0 = new HashSet<Renderer>();
            if (lod != null && lod.GetLODs().Length > 0) foreach (var r in lod.GetLODs()[0].renderers) lod0.Add(r);
            var all = new List<Vector3>();
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var mf in piece.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var r = mf.GetComponent<Renderer>();
                if (lod0.Count > 0 && !lod0.Contains(r)) continue;
                if (!mf.sharedMesh.isReadable) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var p = props.InverseTransformPoint(mf.transform.TransformPoint(v)) + props.localPosition;
                    all.Add(p); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
            }
            if (all.Count == 0) return false;
            height = maxY - Mathf.Max(minY, 0f);
            foreach (var p in all) if (p.y < minY + 0.5f) pts.Add(p);
            return pts.Count > 0;
        }

        static Rect Bounds2(List<Vector2> pts)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in pts) { x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        static void Plot(Color32[] px, int W, Vector2 m, Color32 c)
        {
            int x = Mathf.FloorToInt(m.x / Px), y = Mathf.FloorToInt(m.y / Px);
            if (x < 0 || y < 0 || x >= W || y >= W) return;
            px[y * W + x] = c;
        }

        static void FillRect(Color32[] px, int W, Rect r, Color32 c)
        {
            for (float y = r.yMin; y < r.yMax; y += Px)
                for (float x = r.xMin; x < r.xMax; x += Px) Plot(px, W, new Vector2(x, y), c);
        }

        static void Outline(Color32[] px, int W, Rect r, Color32 c)
        {
            for (float x = r.xMin; x <= r.xMax; x += Px * 0.5f) { Plot(px, W, new Vector2(x, r.yMin), c); Plot(px, W, new Vector2(x, r.yMax), c); }
            for (float y = r.yMin; y <= r.yMax; y += Px * 0.5f) { Plot(px, W, new Vector2(r.xMin, y), c); Plot(px, W, new Vector2(r.xMax, y), c); }
        }

        static void Line(Color32[] px, int W, Vector2 a, Vector2 b, Color32 c)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt((b - a).magnitude / (Px * 0.5f)));
            for (int i = 0; i <= steps; i++) Plot(px, W, Vector2.Lerp(a, b, i / (float)steps), c);
        }

        static void Circle(Color32[] px, int W, Vector2 c, float r, Color32 col)
        {
            int steps = Mathf.Max(24, Mathf.RoundToInt(r * 60f));
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                Plot(px, W, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col);
            }
        }
    }
}
