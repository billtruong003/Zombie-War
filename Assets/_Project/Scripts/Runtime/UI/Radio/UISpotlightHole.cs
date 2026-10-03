using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// Keeps the Spotlight overlay's hole on a target rect (in the overlay's own pixels), every frame,
    /// so it follows scrolling and layout. The overlay needs UIRectUV and the Spotlight shader.
    [ExecuteAlways, RequireComponent(typeof(Graphic))]
    public sealed class UISpotlightHole : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Material baseMaterial;
        [SerializeField] private float padding = 16f;

        Graphic _graphic;
        Material _mat;
        readonly Vector3[] _corners = new Vector3[4];
        static readonly int HoleId = Shader.PropertyToID("_Hole");

        public RectTransform Target { get => target; set => target = value; }

        void OnEnable() => EnsureMaterial();

        void EnsureMaterial()
        {
            if (_mat != null || baseMaterial == null) return;
            _graphic = GetComponent<Graphic>();
            _mat = new Material(baseMaterial) { hideFlags = HideFlags.DontSave };
            _graphic.material = _mat;
        }

        void OnDisable()
        {
            if (_graphic != null) _graphic.material = baseMaterial;
            if (_mat == null) return;
            if (Application.isPlaying) Destroy(_mat); else DestroyImmediate(_mat);
            _mat = null;
        }

        void LateUpdate()
        {
            EnsureMaterial();
            if (_mat == null || target == null) return;
            var self = (RectTransform)transform;
            target.GetWorldCorners(_corners);
            Vector2 a = self.InverseTransformPoint(_corners[0]);
            Vector2 b = self.InverseTransformPoint(_corners[2]);
            var r = self.rect;
            var centre = (a + b) * 0.5f - r.min;
            var size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) + Vector2.one * padding * 2;
            _mat.SetVector(HoleId, new Vector4(centre.x, centre.y, size.x, size.y));
        }
    }
}
