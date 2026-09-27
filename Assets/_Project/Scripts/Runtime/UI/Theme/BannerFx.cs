using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Living background for a premium banner (BannerFx shader): gradient, rays behind the item,
    /// drifting pattern, sparkles, sheen and an optional rim. Each banner owns one material copy
    /// (there are only a handful); <see cref="SetPalette"/> recolours it at runtime (Gacha tabs).
    /// Motion drops on Low graphics.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class BannerFx : BaseMeshEffect
    {
        static readonly int ColorA = Shader.PropertyToID("_ColorA"), ColorB = Shader.PropertyToID("_ColorB"),
            Accent = Shader.PropertyToID("_Accent"), RayCenter = Shader.PropertyToID("_RayCenter"),
            RimStrength = Shader.PropertyToID("_RimStrength"), Motion = Shader.PropertyToID("_Motion"),
            Aspect = Shader.PropertyToID("_Aspect"), Sparkle = Shader.PropertyToID("_Sparkle"), Sheen = Shader.PropertyToID("_Sheen");

        [SerializeField] private Material baseMaterial;
        [SerializeField] private Color colorA = new(0.46f, 0.39f, 0.87f);
        [SerializeField] private Color colorB = new(0.9f, 0.28f, 0.49f);
        [SerializeField] private Color accent = new(1f, 0.85f, 0.5f);
        [Tooltip("Where the rays and glow sit, 0..1 across the banner (behind the item).")]
        [SerializeField] private Vector2 rayCenter = new(0.75f, 0.55f);
        [SerializeField, Range(0f, 1.5f)] private float rim;
        [SerializeField, Range(0f, 1f)] private float sparkle = 0.8f;
        [SerializeField, Range(0f, 1f)] private float sheen = 0.6f;

        Material _mat;

        public Material BaseMaterial { get => baseMaterial; set { baseMaterial = value; Apply(); } }

        public void Configure(Color a, Color b, Color glow, Vector2 center, float rimStrength, float sparkles = 0.8f, float sheenStrength = 0.6f)
        {
            colorA = a; colorB = b; accent = glow; rayCenter = center; rim = rimStrength; sparkle = sparkles; sheen = sheenStrength;
            Apply();
        }

        /// <summary>Recolour (e.g. per Gacha banner) keeping the other settings.</summary>
        public void SetPalette(Color a, Color b, Color glow) { colorA = a; colorB = b; accent = glow; Apply(); }

        protected override void OnEnable() { base.OnEnable(); Apply(); }
        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); SetAspect(); }
        protected override void OnDestroy() { if (_mat != null) { if (Application.isPlaying) Destroy(_mat); else DestroyImmediate(_mat); } base.OnDestroy(); }

        void Apply()
        {
            if (graphic == null || baseMaterial == null) return;
            if (_mat == null || _mat.shader != baseMaterial.shader) _mat = new Material(baseMaterial) { name = "BannerFx (instance)", hideFlags = HideFlags.DontSave };
            _mat.SetColor(ColorA, colorA); _mat.SetColor(ColorB, colorB); _mat.SetColor(Accent, accent);
            _mat.SetVector(RayCenter, new Vector4(rayCenter.x, rayCenter.y, 0, 0));
            _mat.SetFloat(RimStrength, rim); _mat.SetFloat(Sparkle, sparkle); _mat.SetFloat(Sheen, sheen);
            // Low graphics: keep the look, nearly still (fewer shader changes per frame on weak phones).
            _mat.SetFloat(Motion, Application.isPlaying && GameSettings.Quality == GameSettings.Graphics.Low ? 0.25f : 1f);
            graphic.material = _mat;
            if (graphic.color != Color.white) graphic.color = Color.white;
            SetAspect();
        }

        void SetAspect()
        {
            if (_mat == null) return;
            var r = ((RectTransform)transform).rect;
            if (r.height > 1f) _mat.SetFloat(Aspect, r.width / r.height);
        }

        /// <summary>Writes the banner's 0..1 rect position into uv1 for the shader.</summary>
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var r = ((RectTransform)transform).rect;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.uv1 = new Vector4((v.position.x - r.xMin) / Mathf.Max(r.width, 1f), (v.position.y - r.yMin) / Mathf.Max(r.height, 1f), 0, 0);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
