using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// Keeps a sliced image's 9-slice border inside its rect at every size.
    ///
    /// When the border is larger than the rect on one axis, Unity squashes only that axis, so round
    /// corners turn into points (thin bars, small pills). This raises pixelsPerUnitMultiplier just
    /// enough that both axes fit, which scales the corners uniformly instead. It never goes below the
    /// authored multiplier, so bigger elements keep their designed look.
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class UISliceFit : UIBehaviour
    {
        [Tooltip("The authored multiplier. Fitting only ever raises it.")]
        [SerializeField, Min(0.01f)] private float baseMultiplier = 1f;

        private Image _image;

        public float BaseMultiplier { get => baseMultiplier; set { baseMultiplier = Mathf.Max(0.01f, value); Fit(); } }

        protected override void OnEnable()
        {
            base.OnEnable();
            Fit();
        }

        protected override void OnRectTransformDimensionsChange() => Fit();

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Fit();
        }
#endif

        /// Multiplier needed for a sprite border to fit a rect (1 = authored size already fits).
        public static float Needed(Sprite sprite, Vector2 size, float referencePpu)
        {
            if (sprite == null || size.x <= 0f || size.y <= 0f) return 0f;
            var b = sprite.border; // x=left y=bottom z=right w=top, in sprite pixels
            float unit = sprite.pixelsPerUnit / Mathf.Max(0.01f, referencePpu);
            return Mathf.Max((b.x + b.z) / (unit * size.x), (b.y + b.w) / (unit * size.y));
        }

        public void Fit()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null || _image.sprite == null || _image.type != Image.Type.Sliced || !_image.hasBorder) return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float refPpu = _image.canvas != null ? _image.canvas.referencePixelsPerUnit : 100f;
            float m = Mathf.Max(baseMultiplier, Needed(_image.sprite, size, refPpu));
            if (Mathf.Abs(_image.pixelsPerUnitMultiplier - m) > 0.001f) _image.pixelsPerUnitMultiplier = m;
        }
    }
}
