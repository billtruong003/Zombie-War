using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// <summary>
    /// Stylized one-shot mesh bullet tracer, pooled via Bill.Pool (see <see cref="TracerPool"/>).
    /// The bullet is hitscan-fast, so the streak appears at full length instantly along the
    /// barrel/shot axis and then reads as a <b>smoke light-ray</b> that dissolves away from the
    /// muzzle (pivot) toward the tip. Uses the <c>ZombieWar/FX/TracerSmokeRay</c> shader:
    ///   - length is baked into UV.y (0 = pivot, 1 = tip),
    ///   - <c>_Dissolve</c> 0->1 erases the smoke from the pivot upward,
    ///   - <c>_Seed</c> offsets the noise per shot so no two streaks look identical.
    /// No Instantiate/Destroy per shot; length axis + base length are serialized so the prefab
    /// can be tuned to whatever orientation tracer.fbx imports with.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class MeshTracer : MonoBehaviour
    {
        public enum Axis { X, Y, Z }

        [Header("Mesh")]
        [Tooltip("Transform whose local scale is stretched. Defaults to this transform.")]
        [SerializeField] private Transform stretchRoot;
        [Tooltip("Local axis of the mesh that runs along its length.")]
        [SerializeField] private Axis lengthAxis = Axis.Z;
        [Tooltip("World length (units) the mesh spans at scale 1 along the length axis.")]
        [SerializeField] private float baseMeshLength = 0.01f;

        [Header("Shape")]
        [Tooltip("Cross-section thickness scale (both non-length axes).")]
        [SerializeField] private float thickness = 12f;
        [SerializeField] private Vector2 thicknessJitter = new Vector2(0.85f, 1.2f);
        [Tooltip("Minimum streak length so point-blank shots still show.")]
        [SerializeField] private float minLength = 0.6f;
        [Tooltip("Clamp very long shots so the streak stays believable.")]
        [SerializeField] private float maxLength = 40f;
        [Tooltip("Random roll around the shot axis each shot.")]
        [SerializeField] private bool randomRoll = true;

        [Header("Smoke dissolve")]
        [Tooltip("Seconds for the smoke to fully dissolve from pivot to tip.")]
        [SerializeField] private float lifetime = 0.3f;
        [Tooltip("0->1 dissolve progress over lifetime (drives _Dissolve).")]
        [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private string dissolveProperty = "_Dissolve";
        [SerializeField] private string seedProperty = "_Seed";
        [Tooltip("Per-shot random noise offset range.")]
        [SerializeField] private Vector2 seedRange = new Vector2(0f, 16f);

        [Header("Color")]
        [SerializeField] private Color color = new Color(1f, 0.86f, 0.5f, 1f);
        [Tooltip("Shader color property (TracerSmokeRay = _BaseColor).")]
        [SerializeField] private string colorProperty = "_BaseColor";

        [Header("Smoke (optional, spawned at muzzle)")]
        [SerializeField] private ParticleSystem muzzleSmokePrefab;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private int _colorId;
        private int _dissolveId;
        private int _seedId;
        private bool _init;

        private bool _playing;
        private float _timer;
        private float _lenScale;
        private float _thick;

        private void Awake() => Init();

        private void Init()
        {
            if (_init) return;
            _renderer = GetComponent<MeshRenderer>();
            if (stretchRoot == null) stretchRoot = transform;
            _mpb = new MaterialPropertyBlock();
            _colorId = Shader.PropertyToID(colorProperty);
            _dissolveId = Shader.PropertyToID(dissolveProperty);
            _seedId = Shader.PropertyToID(seedProperty);
            _init = true;
        }

        /// <summary>Fire the tracer between two world-space points (start = muzzle, end = hit).</summary>
        public void Play(Vector3 start, Vector3 end)
        {
            Init();

            Vector3 delta = end - start;
            float length = Mathf.Clamp(delta.magnitude, minLength, maxLength);
            Vector3 dir = delta.sqrMagnitude > 1e-6f ? delta.normalized : transform.forward;

            Quaternion look = Quaternion.LookRotation(dir);
            if (randomRoll) look *= Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            transform.SetPositionAndRotation(start, look);

            _thick = thickness * Random.Range(thicknessJitter.x, thicknessJitter.y);
            _lenScale = length / Mathf.Max(0.0001f, baseMeshLength);
            stretchRoot.localScale = ScaleFor(_lenScale, _thick); // full length instantly

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(_colorId, color);
            _mpb.SetFloat(_dissolveId, 0f);
            _mpb.SetVector(_seedId, new Vector4(
                Random.Range(seedRange.x, seedRange.y),
                Random.Range(seedRange.x, seedRange.y), 0f, 0f));
            _renderer.SetPropertyBlock(_mpb);
            _renderer.enabled = true;

            if (muzzleSmokePrefab != null)
                FxPool.Play(muzzleSmokePrefab, start, look);

            _timer = 0f;
            _playing = true;
        }

        private void Update()
        {
            if (!_playing) return;
            _timer += Time.deltaTime;

            float t = lifetime > 0f ? Mathf.Clamp01(_timer / lifetime) : 1f;
            float d = Mathf.Clamp01(dissolveCurve.Evaluate(t));

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_dissolveId, d);
            _renderer.SetPropertyBlock(_mpb);

            if (t >= 1f)
                Recycle();
        }

        private void Recycle()
        {
            _playing = false;
            _renderer.enabled = false;
            if (Bill.Pool != null) gameObject.ReturnToPool();
            else Destroy(gameObject);
        }

        private Vector3 ScaleFor(float len, float th)
        {
            switch (lengthAxis)
            {
                case Axis.X: return new Vector3(len, th, th);
                case Axis.Y: return new Vector3(th, len, th);
                default:     return new Vector3(th, th, len);
            }
        }
    }
}
