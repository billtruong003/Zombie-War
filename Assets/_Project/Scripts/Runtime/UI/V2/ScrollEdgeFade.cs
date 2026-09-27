using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Horizontal scroll hints (owner 2026-09-28: the FACE tags ran off the screen): a fade on
    /// each edge shows only while there is more content that way, and <see cref="Reveal"/> scrolls
    /// a child (the selected tag) fully into view.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class ScrollEdgeFade : MonoBehaviour
    {
        [SerializeField] private Graphic leftFade;
        [SerializeField] private Graphic rightFade;

        ScrollRect _sr;
        float _target = -1f;

        void Awake() { _sr = GetComponent<ScrollRect>(); }

        void LateUpdate()
        {
            if (_sr == null || _sr.content == null) return;
            var view = _sr.viewport != null ? _sr.viewport : (RectTransform)transform;
            float overflow = _sr.content.rect.width - view.rect.width;
            float pos = _sr.horizontalNormalizedPosition;
            if (_target >= 0f)
            {
                pos = Mathf.Lerp(pos, _target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                _sr.horizontalNormalizedPosition = pos;
                if (Mathf.Abs(pos - _target) < 0.002f || _sr.velocity.sqrMagnitude > 1f) _target = -1f;
            }
            SetAlpha(leftFade, overflow > 1f && pos > 0.01f);
            SetAlpha(rightFade, overflow > 1f && pos < 0.99f);
        }

        static void SetAlpha(Graphic g, bool on)
        {
            if (g == null) return;
            var c = g.canvasRenderer.GetAlpha();
            g.canvasRenderer.SetAlpha(Mathf.MoveTowards(c, on ? 1f : 0f, Time.unscaledDeltaTime * 6f));
        }

        /// <summary>Scrolls so the child is fully visible (centred when it can be).</summary>
        public void Reveal(RectTransform child)
        {
            if (_sr == null) _sr = GetComponent<ScrollRect>();
            if (_sr == null || _sr.content == null || child == null) return;
            Canvas.ForceUpdateCanvases();
            var view = _sr.viewport != null ? _sr.viewport : (RectTransform)transform;
            float overflow = _sr.content.rect.width - view.rect.width;
            if (overflow <= 1f) { _target = -1f; return; }
            // child centre in content space (content pivot is on its left edge)
            float centre = child.anchoredPosition.x + child.rect.width * (0.5f - child.pivot.x);
            float x = centre - view.rect.width * 0.5f;
            _target = Mathf.Clamp01(x / overflow);
        }
    }
}
