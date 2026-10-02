using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ZombieWar.World;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Bakes a map's light texture (2026-10-02): shadows and ambient occlusion without realtime
    /// shadows or SSAO. The map's chunks (plus a ring of wrapped neighbours, so shadows cross the
    /// seams) are laid out in a temporary scene and rendered twice — from the sun of the map
    /// (MapTheme.sunEuler) and from straight above — with a shader that writes each pixel's depth
    /// along the view and world height. From those the CPU fills one RGBA texel per ~0.19 m:
    ///   R  sun visibility (3x3 filtered shadow test)        G  ambient occlusion (heightfield horizon)
    ///   B  height of the shadowing caster (/12 m)            A  ground height ((y + 4) / 8)
    /// The toon shaders read it once per pixel (MapLight.hlsl), wrapping with the map.
    /// </summary>
    public static class MapLightBaker
    {
        const int Size = 1024;            // texels over the whole map
        const int LightRes = 2048;        // sun view resolution
        const int TopRes = 1024;          // top view resolution (= Size: one height per texel)
        const float CasterTop = 12f;
        const float NoHit = -1000f;

        [MenuItem("HordeCall/World/Bake Map Light (all themes)")]
        public static string BakeAll()
        {
            var log = new System.Text.StringBuilder();
            foreach (var id in new[] { "meadow", "forest", "swamp", "volcano", "tundra" }) log.Append(Bake(id)).Append("; ");
            return log.ToString();
        }

        public static string Bake(string themeId)
        {
            var theme = MapTheme.Load(themeId);
            if (theme == null) return themeId + ": no theme";
            float mapSize = theme.MapSize, chunk = theme.chunkSize;
            int n = theme.chunksPerSide;

            var prev = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var bakeShader = Shader.Find("Hidden/HordeCall/MapLightBake");
            var mats = new Dictionary<Material, Material>();
            try
            {
                var root = new GameObject("MapLightBake");
                for (int cz = -1; cz <= n; cz++)
                    for (int cx = -1; cx <= n; cx++)
                    {
                        var prefab = theme.ChunkAt(cx, cz);
                        if (prefab == null) continue;
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                        go.transform.position = new Vector3((cx + 0.5f) * chunk, 0f, (cz + 0.5f) * chunk);
                    }

                var ground = new List<Renderer>();
                var casters = new List<Renderer>();
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!(r is MeshRenderer) || !r.enabled) continue;
                    string nm = r.name;
                    if (nm == "Ground" || nm.StartsWith("Fluid")) { ground.Add(r); continue; }
                    if (IsGroundCover(nm)) continue;
                    casters.Add(r);
                    // A bridge deck is the floor where it stands: occlusion is measured from it, not
                    // from the water under it (which darkened the whole deck).
                    if (r.transform.parent != null && r.transform.parent.name == "Bridge") ground.Add(r);
                }

                var sunRot = Quaternion.Euler(theme.sunEuler);
                Vector3 sunDir = sunRot * Vector3.forward;   // the way the light travels

                // ── from the sun: casters only ──
                var lightView = ViewMatrix(sunRot, Vector3.zero);
                var bounds = new Bounds(new Vector3(mapSize * 0.5f, 4f, mapSize * 0.5f), new Vector3(mapSize + 2f * chunk, 24f, mapSize + 2f * chunk));
                var (lmin, lmax) = ViewSpaceExtents(lightView, bounds);
                float[] light = Render(casters, mats, bakeShader, lightView, lmin, lmax, LightRes, sunDir);

                // ── from above: casters (highest surface) and ground (visible floor) ──
                var topRot = Quaternion.Euler(90f, 0f, 0f);
                var topView = ViewMatrix(topRot, Vector3.zero);
                var mapBounds = new Bounds(new Vector3(mapSize * 0.5f, 4f, mapSize * 0.5f), new Vector3(mapSize, 24f, mapSize));
                var (tmin, tmax) = ViewSpaceExtents(topView, mapBounds);
                float[] topCasters = Render(casters, mats, bakeShader, topView, tmin, tmax, TopRes, Vector3.down);
                float[] topGround = Render(ground, mats, bakeShader, topView, tmin, tmax, TopRes, Vector3.down);

                var px = Compute(mapSize, light, lightView, lmin, lmax, topCasters, topGround, tmin, tmax, topView);
                string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(theme.chunks[0])).Replace('\\', '/') + "/T_MapLight_" + themeId + ".png";
                Write(path, px);
                theme.mapLight = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                EditorUtility.SetDirty(theme);
                AssetDatabase.SaveAssetIfDirty(theme);
                return $"{themeId}: {casters.Count} casters, {ground.Count} ground -> {path}";
            }
            finally
            {
                foreach (var m in mats.Values) Object.DestroyImmediate(m);
                if (prev.IsValid()) SceneManager.SetActiveScene(prev);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Grass, flowers, pebbles and the cover clusters are too small to cast a shadow that reads.
        static bool IsGroundCover(string name)
        {
            if (name.StartsWith("Cover_")) return true;
            string n = name.ToLowerInvariant();
            foreach (var w in new[] { "grass", "flower", "clover", "petal", "pebble", "plant_7", "fern" })
                if (n.Contains(w)) return true;
            return false;
        }

        // World -> view (camera looks down -Z), Unity's camera convention.
        static Matrix4x4 ViewMatrix(Quaternion rot, Vector3 pos) => Matrix4x4.Inverse(Matrix4x4.TRS(pos, rot, new Vector3(1f, 1f, -1f)));

        static (Vector3 min, Vector3 max) ViewSpaceExtents(Matrix4x4 view, Bounds b)
        {
            var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var v = view.MultiplyPoint(c);
                min = Vector3.Min(min, v); max = Vector3.Max(max, v);
            }
            return (min, max);
        }

        /// Renders the renderers into an RGFloat target: R = distance along viewDir, G = world height.
        static float[] Render(List<Renderer> renderers, Dictionary<Material, Material> mats, Shader shader, Matrix4x4 view,
                              Vector3 vmin, Vector3 vmax, int res, Vector3 viewDir)
        {
            var proj = Matrix4x4.Ortho(vmin.x, vmax.x, vmin.y, vmax.y, -vmax.z - 1f, -vmin.z + 1f);
            var rt = new RenderTexture(res, res, 24, RenderTextureFormat.RGFloat) { filterMode = FilterMode.Point };
            rt.Create();
            Shader.SetGlobalVector("_BakeViewDir", viewDir);
            var cmd = new CommandBuffer { name = "MapLightBake" };
            cmd.SetRenderTarget(rt);
            cmd.ClearRenderTarget(true, true, new Color(1e6f, NoHit, 0f, 0f));
            cmd.SetViewProjectionMatrices(view, proj);
            foreach (var r in renderers)
            {
                var src = r.sharedMaterials;
                for (int s = 0; s < src.Length; s++)
                {
                    var m = src[s];
                    if (m == null) continue;
                    if (!mats.TryGetValue(m, out var bm))
                    {
                        bm = new Material(shader);
                        bool leaf = m.shader.name.Contains("Foliage") && m.HasProperty("_BaseMap");
                        if (leaf) { bm.SetTexture("_BaseMap", m.GetTexture("_BaseMap")); bm.SetFloat("_Cutoff", m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f); }
                        mats[m] = bm;
                    }
                    cmd.DrawRenderer(r, bm, s, 0);
                }
            }
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(res, res, TextureFormat.RGFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var raw = tex.GetPixels();
            Object.DestroyImmediate(tex);
            rt.Release(); Object.DestroyImmediate(rt);
            var outp = new float[res * res * 2];
            for (int i = 0; i < raw.Length; i++) { outp[i * 2] = raw[i].r; outp[i * 2 + 1] = raw[i].g; }
            return outp;
        }

        static Color32[] Compute(float mapSize, float[] light, Matrix4x4 lightView, Vector3 lmin, Vector3 lmax,
                                 float[] topCasters, float[] topGround, Vector3 tmin, Vector3 tmax, Matrix4x4 topView)
        {
            var sun = new float[Size * Size];
            var ao = new float[Size * Size];
            var hit = new float[Size * Size];
            var gyArr = new float[Size * Size];
            float texel = mapSize / Size;

            // The top render's pixel for a world XZ (top view looks down; its view x = world x).
            float TopSample(float[] buf, float wx, float wz)
            {
                var v = topView.MultiplyPoint(new Vector3(wx, 0f, wz));
                int x = Mathf.Clamp((int)((v.x - tmin.x) / (tmax.x - tmin.x) * TopRes), 0, TopRes - 1);
                int y = Mathf.Clamp((int)((v.y - tmin.y) / (tmax.y - tmin.y) * TopRes), 0, TopRes - 1);
                return buf[(y * TopRes + x) * 2 + 1];
            }
            float Wrap(float a) => Mathf.Repeat(a, mapSize);

            // Sixteen directions, turned by a different angle at every pixel and sampled at radii that
            // drift with it: eight fixed rays drew a star around every small prop.
            const int Dirs = 16;
            float[] radii = { 0.25f, 0.55f, 0.95f, 1.45f, 2.1f, 3f };

            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    int k = j * Size + i;
                    float wx = (i + 0.5f) * texel, wz = (j + 0.5f) * texel;
                    float gy = TopSample(topGround, wx, wz);
                    if (gy <= NoHit + 1f) gy = 0f;
                    gyArr[k] = gy;

                    // Sun: 3x3 shadow test in the sun view.
                    var lp = lightView.MultiplyPoint(new Vector3(wx, gy, wz));
                    float receiver = -lp.z;   // distance along the light (view looks down -Z)
                    float fx = (lp.x - lmin.x) / (lmax.x - lmin.x) * LightRes;
                    float fy = (lp.y - lmin.y) / (lmax.y - lmin.y) * LightRes;
                    int lit = 0, taps = 0; float top = 0f;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int x = Mathf.Clamp((int)fx + ox, 0, LightRes - 1), y = Mathf.Clamp((int)fy + oy, 0, LightRes - 1);
                            int idx = (y * LightRes + x) * 2;
                            float casterDist = light[idx], casterY = light[idx + 1];
                            taps++;
                            if (casterY > NoHit + 1f && casterDist < receiver - 0.25f) top = Mathf.Max(top, casterY);
                            else lit++;
                        }
                    sun[k] = lit / (float)taps;
                    hit[k] = top;

                    // AO: how high the props around rise above this spot. Props only: the ground's own
                    // slope into a basin is not occlusion in this style (it ringed every pond with a dark
                    // band), and a rise under 0.15 m is a plank or a pebble, not a wall.
                    float occl = 0f;
                    float spin = Hash(i, j) * Mathf.PI * 2f / Dirs;
                    float jitter = 0.85f + 0.3f * Hash(j, i);
                    for (int d = 0; d < Dirs; d++)
                    {
                        float ang = spin + d * Mathf.PI * 2f / Dirs;
                        float dx = Mathf.Cos(ang), dz = Mathf.Sin(ang);
                        float horizon = 0f;
                        foreach (float r0 in radii)
                        {
                            float r = r0 * jitter;
                            float sx = Wrap(wx + dx * r), sz = Wrap(wz + dz * r);
                            float h = TopSample(topCasters, sx, sz) - gy;
                            if (h > 0.15f) horizon = Mathf.Max(horizon, h / Mathf.Sqrt(h * h + r * r));
                        }
                        occl += horizon;
                    }
                    float a = 1f - occl / Dirs * 0.85f;
                    float above = TopSample(topCasters, wx, wz) - gy;          // under a canopy or an overhang
                    if (above > 0.05f) a *= Mathf.Lerp(0.7f, 0.92f, Mathf.Clamp01(above / 8f));
                    ao[k] = Mathf.Clamp(a, 0.3f, 1f);
                }

            Blur(sun); Blur(ao); Blur(ao);
            var px = new Color32[Size * Size];
            for (int k = 0; k < px.Length; k++)
                px[k] = new Color32((byte)(sun[k] * 255f), (byte)(ao[k] * 255f),
                                    (byte)(Mathf.Clamp01(hit[k] / CasterTop) * 255f), (byte)(Mathf.Clamp01((gyArr[k] + 4f) / 8f) * 255f));
            return px;
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        // 3x3 box blur, wrapping (the map tiles).
        static void Blur(float[] a)
        {
            var b = (float[])a.Clone();
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    float s = 0f;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                            s += b[((j + oy + Size) % Size) * Size + (i + ox + Size) % Size];
                    a[j * Size + i] = s / 9f;
                }
        }

        static void Write(string path, Color32[] px)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(px); tex.Apply();
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            AssetDatabase.ReleaseCachedFileHandles();
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = false; imp.mipmapEnabled = false; imp.alphaIsTransparency = false;
            imp.wrapMode = TextureWrapMode.Repeat; imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
        }
    }
}
