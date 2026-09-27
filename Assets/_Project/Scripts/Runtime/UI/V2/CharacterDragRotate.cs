using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombieWar.UI
{
    /// <summary>
    /// Drag on the character view (Home, Studio) to turn the menu character around (owner
    /// 2026-09-27: rotate only, no zoom). Lets go with a little spin that slows down, then eases
    /// back to facing the camera after a few idle seconds. A tap still reaches any Button on the
    /// same view: once a drag starts, the event system stops treating the press as a click.
    /// </summary>
    public sealed class CharacterDragRotate : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private float degreesPerPixel = 0.45f;
        [SerializeField] private float returnAfter = 3f;

        MenuCharacterStage _stage;
        Quaternion _rest;
        bool _hasRest, _dragging;
        float _yaw, _velocity, _idle;

        Transform Target()
        {
            if (_stage == null) _stage = FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            var t = _stage != null ? _stage.CharacterRoot : null;
            if (t != null && !_hasRest) { _rest = t.localRotation; _hasRest = true; }
            return t;
        }

        public void OnBeginDrag(PointerEventData e) { _dragging = true; _velocity = 0f; }

        public void OnDrag(PointerEventData e)
        {
            float d = -e.delta.x * degreesPerPixel * ScreenScale();
            _yaw += d;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-3f);
            _velocity = Mathf.Lerp(_velocity, d / dt, 0.5f);
            Apply();
        }

        public void OnEndDrag(PointerEventData e) { _dragging = false; _idle = 0f; }

        // Pixels differ per device; scale the drag to a 1080-wide reference.
        static float ScreenScale() => 1080f / Mathf.Max(1f, Screen.width);

        void Update()
        {
            if (_dragging || !_hasRest) return;
            float dt = Time.unscaledDeltaTime;
            if (Mathf.Abs(_velocity) > 1f)
            {
                _yaw += _velocity * dt;
                _velocity *= Mathf.Exp(-4f * dt);   // flick slows down
                _idle = 0f;
            }
            else if ((_idle += dt) > returnAfter && Mathf.Abs(Mathf.DeltaAngle(0f, _yaw)) > 0.1f)
                _yaw = Mathf.LerpAngle(_yaw, 0f, 1f - Mathf.Exp(-3f * dt));
            Apply();
        }

        void Apply()
        {
            var t = Target();
            if (t != null) t.localRotation = _rest * Quaternion.Euler(0f, _yaw, 0f);
        }

        void OnDisable()
        {
            // Leave the character facing front for the next screen.
            _yaw = 0f; _velocity = 0f; _dragging = false;
            if (_hasRest) { var t = Target(); if (t != null) t.localRotation = _rest; }
        }
    }
}
