using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// M8 P3: how a button answers a finger. While held, a lip button's face sinks onto its lip (the
    /// mockup's pressed look); on click it plays its UI sound. Added to every button by M8UiPolish.
    [DisallowMultipleComponent]
    public sealed class UIPressFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public enum Sound { Tap, Confirm, Back, None }

        [SerializeField] private Sound sound = Sound.Tap;
        [Tooltip("The face of a lip button; empty for flat buttons.")]
        [SerializeField] private RectTransform face;
        [SerializeField, Min(0f)] private float sink = 10f;

        private Selectable _selectable;
        private Vector2 _min, _max;
        private bool _down;

        public void Configure(Sound s, RectTransform f, float sinkPx) { sound = s; face = f; sink = sinkPx; }

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (face != null) { _min = face.offsetMin; _max = face.offsetMax; }
        }

        private bool Interactable => _selectable == null || _selectable.IsInteractable();

        public void OnPointerDown(PointerEventData e)
        {
            if (!Interactable || face == null || _down) return;
            _down = true;
            var d = new Vector2(0f, sink);
            face.offsetMin = _min - d;
            face.offsetMax = _max - d;
        }

        public void OnPointerUp(PointerEventData e) => Release();

        private void OnDisable() => Release();

        private void Release()
        {
            if (!_down) return;
            _down = false;
            if (face != null) { face.offsetMin = _min; face.offsetMax = _max; }
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable) return;
            switch (sound)
            {
                case Sound.Tap: UIFeedback.Tap(); break;
                case Sound.Confirm: UIFeedback.Confirm(); break;
                case Sound.Back: UIFeedback.Back(); break;
            }
        }
    }
}
