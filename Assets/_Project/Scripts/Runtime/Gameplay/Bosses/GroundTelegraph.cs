using UnityEngine;

namespace ZombieWar.Bosses
{
    /// <summary>
    /// A red warning drawn on the ground before a boss attack (genre rule 8: threats are telegraphed).
    /// A circle (slam) or a strip (charge): a faint fill that grows from the centre to the edge over the
    /// wind-up, inside a bright rim, so the player reads both where and when. Procedural meshes, one
    /// shared transparent material; no decal projector needed on mobile.
    /// </summary>
    public sealed class GroundTelegraph : MonoBehaviour
    {
        public enum Shape { Circle, Strip }

        static Mesh _disc, _ring, _quad, _frame;
        static Material _fillMat, _rimMat;

        MeshRenderer _fill, _rim;
        float _start, _duration;
        Shape _shape;
        Vector2 _size;

        /// <summary>Draws a warning at <paramref name="at"/> that fills over <paramref name="seconds"/>
        /// and removes itself <paramref name="linger"/> seconds after it completes.</summary>
        public static GroundTelegraph Show(Shape shape, Vector3 at, Quaternion facing, Vector2 size, float seconds, float linger = 0.15f)
        {
            Ensure();
            var go = new GameObject("Telegraph");
            go.transform.SetPositionAndRotation(new Vector3(at.x, 0.06f, at.z), facing);
            var t = go.AddComponent<GroundTelegraph>();
            t._shape = shape; t._size = size; t._duration = Mathf.Max(0.05f, seconds); t._start = Time.time;
            t._rim = Part(go.transform, "Rim", shape == Shape.Circle ? _ring : _frame, _rimMat);
            t._fill = Part(go.transform, "Fill", shape == Shape.Circle ? _disc : _quad, _fillMat);
            t.Apply(0f);
            Destroy(go, seconds + linger);
            return t;
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.enabled = mat != null;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void Update() => Apply(Mathf.Clamp01((Time.time - _start) / _duration));

        void Apply(float k)
        {
            if (_shape == Shape.Circle)
            {
                _rim.transform.localScale = new Vector3(_size.x, 1f, _size.x);
                _fill.transform.localScale = new Vector3(_size.x * k, 1f, _size.x * k);
            }
            else
            {
                // A strip from the boss forward: x = width, y = length; the fill runs down its length.
                _rim.transform.localScale = new Vector3(_size.x, 1f, _size.y);
                _fill.transform.localScale = new Vector3(_size.x, 1f, _size.y * k);
            }
        }

        static void Ensure()
        {
            if (_disc != null) return;
            _disc = Disc(48, 0f, 1f);
            _ring = Disc(64, 0.9f, 1f);
            _quad = Strip(0f);
            _frame = Frame(0.08f);
            // A transparent URP Unlit material asset (built by BossLabBuilder): Shader.Find would not
            // ship the shader in a build (RuntimeShaderInclusionTests).
            var baseMat = Resources.Load<Material>(MaterialPath);
            _fillMat = Tinted(baseMat, new Color(1f, 0.12f, 0.08f, 0.32f));
            _rimMat = Tinted(baseMat, new Color(1f, 0.2f, 0.12f, 0.9f));
        }

        public const string MaterialPath = "FX/M_Telegraph";

        static Material Tinted(Material baseMat, Color c)
        {
            if (baseMat == null) { Debug.LogWarning("[GroundTelegraph] missing Resources/" + MaterialPath); return null; }
            var m = new Material(baseMat) { name = "Telegraph" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>A flat annulus of radius 1 (inner = 0 gives a disc), facing up.</summary>
        static Mesh Disc(int segments, float inner, float outer)
        {
            var v = new Vector3[segments * 2]; var tri = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d * inner; v[i * 2 + 1] = d * outer;
                int n = (i + 1) % segments;
                tri[i * 6] = i * 2; tri[i * 6 + 1] = n * 2 + 1; tri[i * 6 + 2] = i * 2 + 1;
                tri[i * 6 + 3] = i * 2; tri[i * 6 + 4] = n * 2; tri[i * 6 + 5] = n * 2 + 1;
            }
            var m = new Mesh { name = "TelegraphDisc", vertices = v, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>A unit strip: x -0.5..0.5, z 0..1 (starts at the boss, runs forward).</summary>
        static Mesh Strip(float _)
        {
            var m = new Mesh { name = "TelegraphStrip" };
            m.vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(-0.5f, 0, 1), new Vector3(0.5f, 0, 1) };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>The strip's outline, <paramref name="w"/> of the width thick.</summary>
        static Mesh Frame(float w)
        {
            float x0 = -0.5f, x1 = 0.5f, z0 = 0f, z1 = 1f, t = w;
            var quads = new[]
            {
                (x0, z0, x0 + t, z1), (x1 - t, z0, x1, z1), (x0, z0, x1, z0 + t * 0.4f), (x0, z1 - t * 0.4f, x1, z1),
            };
            var v = new Vector3[16]; var tri = new int[24];
            for (int i = 0; i < 4; i++)
            {
                var (a, b, c, d) = quads[i];
                v[i * 4] = new Vector3(a, 0, b); v[i * 4 + 1] = new Vector3(c, 0, b); v[i * 4 + 2] = new Vector3(a, 0, d); v[i * 4 + 3] = new Vector3(c, 0, d);
                int o = i * 4;
                tri[i * 6] = o; tri[i * 6 + 1] = o + 2; tri[i * 6 + 2] = o + 1; tri[i * 6 + 3] = o + 1; tri[i * 6 + 4] = o + 2; tri[i * 6 + 5] = o + 3;
            }
            var m = new Mesh { name = "TelegraphFrame", vertices = v, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
