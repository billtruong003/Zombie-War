using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieWar.World
{
    /// <summary>
    /// Trails in the snow (2026-10-02, tundra; MinionsArt's interactive snow without moving vertices):
    /// a small render texture wraps over the world (world XZ / window size, repeating), so it never has
    /// to scroll with the player. Every frame it fades a little and every walker the grass benders know
    /// (the player and the enemies nearest to it) presses a soft disc into it. The tundra ground shader
    /// turns that into dents with lit and shaded rims; the ground only reads it near the player, where
    /// the wrapped texture cannot repeat.
    /// Cost: one fade quad and up to 16 small quads into a 1024² (high), 512² (mid) or 256² (low) R8
    /// target.
    /// </summary>
    public sealed class SnowTrails : MonoBehaviour
    {
        const float Window = 48f;            // metres the texture covers before it repeats
        const float FadeSeconds = 9f;        // a trail is gone after about this long
        const float FootRadius = 0.45f;

        static readonly int TrailId = Shader.PropertyToID("_ZWSnowTrail");
        static readonly int TrailSTId = Shader.PropertyToID("_ZWSnowTrailST");
        static readonly int CentreId = Shader.PropertyToID("_ZWSnowTrailCentre");
        static readonly int DecayId = Shader.PropertyToID("_Decay");

        Material _mat;
        RenderTexture _rt;
        CommandBuffer _cmd;
        Mesh _quad;
        readonly Matrix4x4[] _stamps = new Matrix4x4[64];

        public static SnowTrails Create(Transform parent, Shader shader)
        {
            var go = new GameObject("SnowTrails");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<SnowTrails>();
            t._mat = new Material(shader) { enableInstancing = true };
            return t;
        }

        void OnEnable()
        {
            GraphicsTier.Changed += OnTier;
            Build();
        }

        void OnDisable()
        {
            GraphicsTier.Changed -= OnTier;
            Shader.SetGlobalVector(CentreId, Vector4.zero);
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            _cmd?.Release(); _cmd = null;
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_quad != null) Destroy(_quad);
        }

        void OnTier(GraphicsTier.Level _) { OnDisable(); OnEnable(); }

        void Build()
        {
            int res = GraphicsTier.Current switch { GraphicsTier.Level.Low => 256, GraphicsTier.Level.Mid => 512, _ => 1024 };
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
            _rt = new RenderTexture(res, res, 0, format) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "SnowTrails" };
            _rt.Create();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt; GL.Clear(false, true, Color.clear); RenderTexture.active = prev;
            _cmd = new CommandBuffer { name = "SnowTrails" };
            if (_quad == null) _quad = Quad();
            Shader.SetGlobalTexture(TrailId, _rt);
            Shader.SetGlobalVector(TrailSTId, new Vector4(1f / Window, 1f / res, Window, 0f));
        }

        static Mesh Quad()
        {
            var m = new Mesh { name = "SnowTrailQuad" };
            m.SetVertices(new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) });
            m.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            return m;
        }

        void LateUpdate()
        {
            if (_rt == null || _mat == null) return;
            var player = PlayerMovement.Instance;
            if (player == null) return;
            Vector3 p = player.transform.position;
            // The ground reads trails only inside this circle round the player.
            Shader.SetGlobalVector(CentreId, new Vector4(p.x, p.z, Window * 0.42f, 1f));

            _cmd.Clear();
            _cmd.SetRenderTarget(_rt);
            _cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            _mat.SetFloat(DecayId, Mathf.Pow(0.01f, Time.deltaTime / FadeSeconds));
            _cmd.DrawMesh(_quad, Matrix4x4.identity, _mat, 0, 0);

            int n = 0;
            var walkers = GrassBenders.LastFrame;
            for (int i = 0; i < GrassBenders.LastCount && n < _stamps.Length - 4; i++)
            {
                Vector4 w = walkers[i];
                if (w.y > p.y + 0.4f) continue;            // flyers leave no trail
                // Each press a little bigger or smaller and a little off the line, so a walker leaves
                // uneven prints rather than a ruled trench (owner 02/10).
                float r = FootRadius * Mathf.Clamp(w.w, 0.6f, 1.6f) * Random.Range(0.75f, 1.25f);
                var jitter = Random.insideUnitCircle * r * 0.35f;
                w.x += jitter.x; w.z += jitter.y;
                Stamp(w.x, w.z, r, ref n);
            }
            if (n > 0) _cmd.DrawMeshInstanced(_quad, 0, _mat, 1, _stamps, n);
            Graphics.ExecuteCommandBuffer(_cmd);
        }

        // One disc, plus its copies across the wrap seam when it straddles it.
        void Stamp(float x, float z, float r, ref int n)
        {
            float u = Mathf.Repeat(x / Window, 1f), v = Mathf.Repeat(z / Window, 1f);
            float s = r / Window * 2f;   // half size in clip units (clip spans 2)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    float cu = u + dx, cv = v + dy;
                    if (cu < -r / Window || cu > 1f + r / Window || cv < -r / Window || cv > 1f + r / Window) continue;
                    if (n >= _stamps.Length) return;
                    float cx = cu * 2f - 1f, cy = cv * 2f - 1f;
                    if (SystemInfo.graphicsUVStartsAtTop) cy = -cy;
                    _stamps[n++] = Matrix4x4.TRS(new Vector3(cx, cy, 0f), Quaternion.identity, new Vector3(s, s, 1f));
                }
        }
    }
}
