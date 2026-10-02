using UnityEngine;

namespace ZombieWar.World
{
    /// <summary>
    /// Falling snow on snowy maps (2026-10-02): one mesh of flakes drawn by the shader
    /// "HordeCall/Fx/Snowfall", which moves every flake on the GPU (fall, wind, sway, wrap round the
    /// player). The CPU only tells the shader where the player is. Works without compute shaders
    /// (WebGL, older phones). Flake count by graphics tier: 600 / 1000 / 1600.
    /// </summary>
    public sealed class Snowfall : MonoBehaviour
    {
        static readonly int CentreId = Shader.PropertyToID("_ZWSnowCentre");

        MeshFilter _filter;
        Mesh _mesh;

        public static Snowfall Create(Transform parent, Material material)
        {
            // The mesh parts first: AddComponent<Snowfall> runs OnEnable at once, which builds the mesh.
            var go = new GameObject("Snowfall");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.AddComponent<Snowfall>();
        }

        void OnEnable()
        {
            GraphicsTier.Changed += Rebuild;
            Rebuild(GraphicsTier.Current);
        }

        void OnDisable() => GraphicsTier.Changed -= Rebuild;

        void OnDestroy() { if (_mesh != null) Destroy(_mesh); }

        static int CountFor(GraphicsTier.Level level) => level switch { GraphicsTier.Level.Low => 600, GraphicsTier.Level.Mid => 1000, _ => 1600 };

        void Rebuild(GraphicsTier.Level level)
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_filter == null) return;
            if (_mesh != null) Destroy(_mesh);
            int n = CountFor(level);
            var verts = new Vector3[n * 4];
            var corner = new Vector2[n * 4];
            var seedXZ = new Vector2[n * 4];
            var seedYS = new Vector2[n * 4];
            var tris = new int[n * 6];
            var rng = new System.Random(1234);
            for (int i = 0; i < n; i++)
            {
                var xz = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                var ys = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                for (int c = 0; c < 4; c++)
                {
                    int v = i * 4 + c;
                    corner[v] = new Vector2(c == 1 || c == 2 ? 1 : 0, c >= 2 ? 1 : 0);
                    seedXZ[v] = xz; seedYS[v] = ys;
                }
                int t = i * 6, b = i * 4;
                tris[t] = b; tris[t + 1] = b + 2; tris[t + 2] = b + 1; tris[t + 3] = b; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
            }
            _mesh = new Mesh { name = "Snowfall", indexFormat = n * 4 > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            _mesh.SetVertices(verts);
            _mesh.SetUVs(0, corner);
            _mesh.SetUVs(1, seedXZ);
            _mesh.SetUVs(2, seedYS);
            _mesh.SetTriangles(tris, 0);
            // The flakes are placed in the shader: never cull the mesh.
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            _filter.sharedMesh = _mesh;
        }

        void LateUpdate()
        {
            var player = PlayerMovement.Instance;
            Vector3 c = player != null ? player.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            Shader.SetGlobalVector(CentreId, c);
        }
    }
}
