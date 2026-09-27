using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Full-screen animated menu background (MenuBackground shader: gradient, rays, three parallax
    /// pattern layers, bokeh). One material instance per view, coloured by the current theme and
    /// told the view's aspect so the pattern never stretches on any phone or tablet.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class MenuBackgroundView : MonoBehaviour
    {
        static readonly int Top = Shader.PropertyToID("_TopColor"), Bot = Shader.PropertyToID("_BotColor"),
            ShapeC = Shader.PropertyToID("_ShapeColor"), RayC = Shader.PropertyToID("_RayColor"),
            ShapeA = Shader.PropertyToID("_ShapeAlpha"), Rays = Shader.PropertyToID("_RayStrength"),
            Vig = Shader.PropertyToID("_Vignette"), Aspect = Shader.PropertyToID("_Aspect");

        RawImage _img;
        Material _mat;

        void OnEnable()
        {
            _img = GetComponent<RawImage>();
            var src = ThemeService.Set != null ? ThemeService.Set.backgroundMaterial : null;
            // Transparent queue: the shader asks for Background, which would draw before the screen's
            // own fill and be covered by it.
            if (src != null && _mat == null) _mat = new Material(src) { name = "MenuBackground (runtime)", renderQueue = 3000 };
            if (_mat != null) _img.material = _mat;
            ThemeService.Changed += Apply;
            Apply();
        }

        void OnDisable() => ThemeService.Changed -= Apply;

        void OnDestroy() { if (_mat != null) Destroy(_mat); }

        void OnRectTransformDimensionsChange() => SetAspect();

        void Apply()
        {
            var t = ThemeService.Current;
            if (_mat == null || t == null) return;
            _mat.SetColor(Top, t.bgTop); _mat.SetColor(Bot, t.bgBottom);
            _mat.SetColor(ShapeC, t.bgShape); _mat.SetColor(RayC, t.bgShape);
            _mat.SetFloat(ShapeA, t.bgShapeAlpha); _mat.SetFloat(Rays, t.bgRays); _mat.SetFloat(Vig, t.bgVignette);
            _img.color = Color.white;
            SetAspect();
        }

        void SetAspect()
        {
            if (_mat == null) return;
            var r = ((RectTransform)transform).rect;
            if (r.height > 1f) _mat.SetFloat(Aspect, r.width / r.height);
        }
    }
}
