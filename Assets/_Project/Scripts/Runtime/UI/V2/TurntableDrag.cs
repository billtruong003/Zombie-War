using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombieWar.UI
{
    /// <summary>
    /// Touch area for a <see cref="GunTurntable"/> view: drag turns the gun (with a flick that
    /// slows down), two fingers pinch to zoom, the mouse wheel zooms in the editor. Pointers are
    /// tracked through the event system, so it works with either input backend.
    /// Lives in its own file: Unity only serializes a MonoBehaviour whose file has its name (it
    /// used to sit inside GunTurntable.cs and every prefab lost it as a "missing script").
    /// </summary>
    public sealed class TurntableDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        [SerializeField] private GunTurntable turntable;
        [Tooltip("Pinch and wheel zoom (the big 360 view); off for small previews inside a scroll.")]
        [SerializeField] private bool allowZoom = true;

        readonly Dictionary<int, Vector2> _pointers = new();
        float _pinchDistance;

        public GunTurntable Turntable { get => turntable; set => turntable = value; }
        public bool AllowZoom { get => allowZoom; set => allowZoom = value; }

        public void OnPointerDown(PointerEventData e)
        {
            _pointers[e.pointerId] = e.position;
            if (_pointers.Count == 2) _pinchDistance = PinchDistance();
        }

        public void OnPointerUp(PointerEventData e)
        {
            _pointers.Remove(e.pointerId);
            if (_pointers.Count == 0 && turntable != null) turntable.Release();
        }

        public void OnBeginDrag(PointerEventData e) { }

        public void OnDrag(PointerEventData e)
        {
            if (turntable == null) return;
            _pointers[e.pointerId] = e.position;
            if (_pointers.Count >= 2)
            {
                if (!allowZoom) return;
                float d = PinchDistance();
                if (_pinchDistance > 1f && d > 1f) turntable.Zoom(d / _pinchDistance);
                _pinchDistance = d;
                return;
            }
            turntable.Drag(e.delta * (1080f / Mathf.Max(1f, Screen.width)));
        }

        public void OnEndDrag(PointerEventData e) { if (_pointers.Count <= 1 && turntable != null) turntable.Release(); }

        public void OnScroll(PointerEventData e)
        {
            if (!allowZoom)
            {
                // A small preview inside a scroll view: the wheel scrolls the page, as before.
                if (transform.parent != null) ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, e, ExecuteEvents.scrollHandler);
                return;
            }
            if (turntable != null && Mathf.Abs(e.scrollDelta.y) > 0.01f)
                turntable.Zoom(1f + Mathf.Clamp(e.scrollDelta.y, -3f, 3f) * 0.08f);
        }

        void OnDisable() { _pointers.Clear(); }

        float PinchDistance()
        {
            Vector2 a = default, b = default; int i = 0;
            foreach (var p in _pointers.Values) { if (i == 0) a = p; else if (i == 1) b = p; i++; }
            return Vector2.Distance(a, b);
        }
    }
}
