using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Draws one avatar frame (AvatarFrame shader) on a plain quad that covers the avatar plus a
    /// glow margin. One material copy per view; <see cref="Show"/> sets the frame's recipe.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class AvatarFrameView : BaseMeshEffect
    {
        static readonly int ColorA = Shader.PropertyToID("_ColorA"), ColorB = Shader.PropertyToID("_ColorB"),
            GlowColor = Shader.PropertyToID("_GlowColor"), GlowStrength = Shader.PropertyToID("_GlowStrength"),
            Width = Shader.PropertyToID("_Width"), Margin = Shader.PropertyToID("_Margin"), Sheen = Shader.PropertyToID("_Sheen"),
            Rainbow = Shader.PropertyToID("_Rainbow"), Motion = Shader.PropertyToID("_Motion");

        /// <summary>Share of the quad on each side kept for the glow (the avatar sits inside it).</summary>
        public const float MarginShare = 0.12f;

        [SerializeField] private Material baseMaterial;
        Material _mat;
        AvatarCatalog.Frame _frame;

        public Material BaseMaterial { get => baseMaterial; set => baseMaterial = value; }

        public void Show(AvatarCatalog.Frame f)
        {
            _frame = f;
            if (graphic == null || baseMaterial == null || f == null) return;
            if (_mat == null) _mat = new Material(baseMaterial) { name = "AvatarFrame (instance)", hideFlags = HideFlags.DontSave };
            _mat.SetColor(ColorA, f.a); _mat.SetColor(ColorB, f.b); _mat.SetColor(GlowColor, f.glow);
            _mat.SetFloat(GlowStrength, f.glowStrength); _mat.SetFloat(Width, f.width); _mat.SetFloat(Margin, MarginShare);
            _mat.SetFloat(Sheen, f.sheen); _mat.SetFloat(Rainbow, f.rainbow ? 1f : 0f);
            _mat.SetFloat(Motion, Application.isPlaying && GameSettings.Quality == GameSettings.Graphics.Low ? 0.3f : 1f);
            graphic.material = _mat;
            graphic.color = Color.white;
        }

        protected override void OnEnable() { base.OnEnable(); if (_frame != null) Show(_frame); }
        protected override void OnDestroy() { if (_mat != null) { if (Application.isPlaying) Destroy(_mat); else DestroyImmediate(_mat); } base.OnDestroy(); }

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
