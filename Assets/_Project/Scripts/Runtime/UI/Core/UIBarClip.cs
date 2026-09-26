using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// The value part of a bar: this rect is scaled through anchorMax.x (by screen code or a Slider)
    /// and clips its child graphic, which always keeps the full track size.
    ///
    /// A sliced pill that is itself shrunk to 5% of the track gets squashed into a sliver; clipping a
    /// full-width pill keeps the rounded start and a clean cut at the value. Hierarchy:
    /// Track (Image) > Fill (this + RectMask2D) > Graphic (Image, sized here to the track).
    [ExecuteAlways]
    [RequireComponent(typeof(RectMask2D))]
    [DisallowMultipleComponent]
    public sealed class UIBarClip : UIBehaviour
    {
        [SerializeField] private RectTransform graphic;

        public RectTransform Graphic { get => graphic; set { graphic = value; Match(); } }

        protected override void OnEnable()
        {
            base.OnEnable();
            Match();
        }

        protected override void OnRectTransformDimensionsChange() => Match();

        /// Sets a bar's value whether given the clip or its graphic (older references point at either).
        public static void Set(RectTransform barPart, float value01)
        {
            if (barPart == null) return;
            var clip = barPart.GetComponent<UIBarClip>();
            if (clip == null && barPart.parent != null) clip = barPart.parent.GetComponent<UIBarClip>();
            var rt = clip != null ? (RectTransform)clip.transform : barPart;
            var max = rt.anchorMax;
            max.x = Mathf.Clamp01(value01);
            rt.anchorMax = max;
        }

        private void Match()
        {
            if (graphic == null) return;
            var track = transform.parent as RectTransform;
            if (track == null) return;
            var self = (RectTransform)transform;
            // Graphic spans the track: left-anchored, the track's width, the clip's height.
            graphic.anchorMin = new Vector2(0f, 0f);
            graphic.anchorMax = new Vector2(0f, 1f);
            graphic.pivot = new Vector2(0f, 0.5f);
            float left = self.anchorMin.x * track.rect.width + self.offsetMin.x;
            graphic.anchoredPosition = new Vector2(-left, 0f);
            graphic.sizeDelta = new Vector2(track.rect.width, 0f);
        }
    }
}
