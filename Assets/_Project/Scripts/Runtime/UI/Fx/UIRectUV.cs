using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// Feeds UI shaders the graphic's own rect, independent of sprite UVs (9-slice, atlases):
    /// uv1 = position inside the rect (0..1), uv2 = rect size in canvas pixels. Used by the FTUE
    /// shaders (Shine, Reticle, Spotlight, HazardStripe, Waveform) so they stay crisp on any size.
    [RequireComponent(typeof(Graphic))]
    public sealed class UIRectUV : BaseMeshEffect
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var r = ((RectTransform)transform).rect;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.uv1 = new Vector4((v.position.x - r.xMin) / Mathf.Max(r.width, 1f), (v.position.y - r.yMin) / Mathf.Max(r.height, 1f), 0, 0);
                v.uv2 = new Vector4(r.width, r.height, 0, 0);
                vh.SetUIVertex(v, i);
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }
}
