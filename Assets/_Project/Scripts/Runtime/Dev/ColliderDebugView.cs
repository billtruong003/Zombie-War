#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieWar.Dev
{
    /// <summary>
    /// Collider debug view (QA panel, MAP tab, 02/10): draws every collider within reach of the
    /// player as wire lines over the game view, so wrong blockers can be seen while playing.
    ///   blue    water / lava / ice obstacles (axis-aligned NavObstacle boxes)
    ///   orange  prop blockers (NavObstacle capsules and turned boxes)
    ///   green   the player          red   enemies          yellow  anything else solid
    ///   cyan    triggers (pickups, stations)
    /// Optionally the enemies' flow field cells that count as blocked (NAV CELLS). Dev builds only;
    /// drawn with GL lines after each camera, nothing is created per collider.
    /// </summary>
    public sealed class ColliderDebugView : MonoBehaviour
    {
        public static bool On { get; private set; }
        public static float Radius = 22f;

        static ColliderDebugView _instance;
        static Material _mat;
        static readonly Collider[] Hits = new Collider[2048];
        int _navLayer = -1, _count;

        public static void Toggle()
        {
            On = !On;
            if (On && _instance == null)
            {
                var go = new GameObject("ColliderDebugView");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ColliderDebugView>();
            }
            if (_instance != null) _instance.enabled = On;
        }

        void OnEnable()
        {
            _navLayer = LayerMask.NameToLayer("NavObstacle");
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                _mat.SetInt("_ZTest", (int)CompareFunction.Always);   // through everything
                _mat.SetInt("_ZWrite", 0);
                _mat.SetInt("_Cull", 0);
            }
            RenderPipelineManager.endCameraRendering += Draw;
        }

        void OnDisable() => RenderPipelineManager.endCameraRendering -= Draw;

        void LateUpdate()
        {
            var player = PlayerMovement.Instance;
            Vector3 c = player != null ? player.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            _count = Physics.OverlapSphereNonAlloc(c, Radius, Hits, ~0, QueryTriggerInteraction.Collide);
        }

        void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam.cameraType != CameraType.Game || cam != Camera.main || _mat == null) return;
            _mat.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            // Thick lines: each segment is a thin quad facing the camera, about 3 px wide.
            _view = cam.transform.forward;
            _width = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 3f / Mathf.Max(1, cam.pixelHeight);
            _camPos = cam.transform.position;
            GL.Begin(GL.QUADS);
            var player = PlayerMovement.Instance;
            for (int i = 0; i < _count; i++)
            {
                var col = Hits[i];
                if (col == null || !col.enabled) continue;
                GL.Color(ColourOf(col, player));
                switch (col)
                {
                    case BoxCollider b: Box(b.transform.localToWorldMatrix, b.center, b.size); break;
                    case CapsuleCollider k: Capsule(k); break;
                    case SphereCollider s: Sphere(s); break;
                    case CharacterController cc: CapsuleLike(cc.transform, cc.center, cc.radius, cc.height); break;
                    default: Box(Matrix4x4.identity, col.bounds.center, col.bounds.size); break;
                }
            }
            GL.End();
            GL.PopMatrix();
        }

        Color ColourOf(Collider col, PlayerMovement player)
        {
            if (col.isTrigger) return new Color(0.2f, 0.95f, 1f, 0.8f);
            if (player != null && col.transform.IsChildOf(player.transform)) return new Color(0.3f, 1f, 0.3f);
            if (col.gameObject.layer == _navLayer)
                return col is BoxCollider && col.transform.rotation == Quaternion.identity && col.transform.name == "Obstacles"
                    ? new Color(0.35f, 0.6f, 1f) : new Color(1f, 0.55f, 0.1f);
            if (col.GetComponentInParent<ZombieBase>() != null) return new Color(1f, 0.25f, 0.25f);
            return new Color(1f, 0.95f, 0.2f);
        }

        static Vector3 _view, _camPos;
        static float _width;

        // A segment as a quad, its width scaled with the distance so it stays ~3 px on screen.
        static void Seg(Vector3 a, Vector3 b)
        {
            var dir = b - a;
            var side = Vector3.Cross(dir, _view);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(dir, Vector3.up);
            float w = _width * Vector3.Distance(_camPos, (a + b) * 0.5f) * 0.5f;
            side = side.normalized * w;
            GL.Vertex(a - side); GL.Vertex(a + side); GL.Vertex(b + side); GL.Vertex(b - side);
        }

        static void Box(Matrix4x4 m, Vector3 c, Vector3 s)
        {
            var h = s * 0.5f;
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
                p[i] = m.MultiplyPoint3x4(c + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z));
            int[] e = { 0, 1, 1, 3, 3, 2, 2, 0, 4, 5, 5, 7, 7, 6, 6, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int i = 0; i < e.Length; i += 2) Seg(p[e[i]], p[e[i + 1]]);
        }

        static void Capsule(CapsuleCollider k)
        {
            // Unity capsules here stand on Y (direction 1); the others are drawn as their bounds.
            if (k.direction != 1) { Box(Matrix4x4.identity, k.bounds.center, k.bounds.size); return; }
            CapsuleLike(k.transform, k.center, k.radius, k.height);
        }

        static void CapsuleLike(Transform t, Vector3 centre, float radius, float height)
        {
            var s = t.lossyScale;
            float r = radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
            var c = t.TransformPoint(centre);
            float half = Mathf.Max(0f, height * Mathf.Abs(s.y) * 0.5f);
            var bottom = c - Vector3.up * (half - Mathf.Min(r, half));
            var top = c + Vector3.up * (half - Mathf.Min(r, half));
            Ring(bottom - Vector3.up * Mathf.Min(r, half) * 0.0f, r);
            Ring(top, r);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                Seg(bottom + o, top + o);
            }
        }

        static void Sphere(SphereCollider s)
        {
            var c = s.transform.TransformPoint(s.center);
            var sc = s.transform.lossyScale;
            float r = s.radius * Mathf.Max(Mathf.Abs(sc.x), Mathf.Max(Mathf.Abs(sc.y), Mathf.Abs(sc.z)));
            Ring(c, r);
        }

        static void Ring(Vector3 c, float r)
        {
            const int N = 24;
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
                Seg(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r);
            }
        }

        // Domain reload is off in this project: statics survive leaving Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { On = false; _instance = null; }
    }
}
#endif
