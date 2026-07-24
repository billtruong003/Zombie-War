// Interaction model inspired by Bian-Sh/UniJoystick:
// https://github.com/Bian-Sh/UniJoystick (MIT).
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace BillGameCore
{
    /// <summary>
    /// Pivot-independent mobile joystick for uGUI.
    /// Supports fixed/floating bases, one-pointer ownership, axis filtering,
    /// remapped dead zone, optional direction indicator, and event callbacks.
    /// </summary>
    [DisallowMultipleComponent]
    public class BillVirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum Mode
        {
            Fixed,
            Floating,
        }

        public enum Axis
        {
            Both,
            Horizontal,
            Vertical,
        }

        [Serializable]
        public sealed class Vector2Event : UnityEvent<Vector2>
        {
        }

        [Header("References")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [Tooltip("Optional arrow or marker. Its local +X direction is treated as forward.")]
        [SerializeField] private RectTransform directionIndicator;

        [Header("Behaviour")]
        [Tooltip("Floating centers the background on the first touch and restores it on release.")]
        [SerializeField] private Mode mode = Mode.Floating;
        [SerializeField] private Axis activeAxes = Axis.Both;

        [Tooltip("Normalized input below this threshold is ignored, then the remaining range is remapped to 0..1.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float deadZone = 0.08f;

        [Tooltip("Fraction of the available visual travel used by the handle.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float handleRange = 1f;

        [Tooltip("Keeps the whole handle inside the circular background instead of only clamping its center.")]
        [SerializeField] private bool keepHandleInsideBackground = true;

        [Header("Events")]
        [SerializeField] private Vector2Event onValueChanged = new Vector2Event();
        [SerializeField] private Vector2Event onPointerDown = new Vector2Event();
        [SerializeField] private Vector2Event onPointerUp = new Vector2Event();

        public Vector2 Direction { get; private set; }
        public float Horizontal => Direction.x;
        public float Vertical => Direction.y;
        public bool IsHeld => _activePointerId != Unclaimed;

        public Mode OperatingMode
        {
            get => mode;
            set
            {
                if (mode == value) return;
                ReleaseJoystick();
                mode = value;
            }
        }

        public Axis ActiveAxes
        {
            get => activeAxes;
            set
            {
                if (activeAxes == value) return;
                activeAxes = value;
                ApplyDirection(FilterAxes(Direction));
            }
        }

        public Vector2Event OnValueChanged => onValueChanged;
        public Vector2Event OnPointerDownEvent => onPointerDown;
        public Vector2Event OnPointerUpEvent => onPointerUp;

        private const int Unclaimed = int.MinValue;
        private const float DirectionEpsilon = 0.000001f;

        private int _activePointerId = Unclaimed;
        private Vector2 _backgroundRestPosition;
        private Vector2 _handleRestPosition;
        private bool _restPoseCaptured;

        protected virtual void Awake()
        {
            if (!HasValidReferences())
            {
                Debug.LogError("[BillVirtualJoystick] Background and handle references are required.", this);
                enabled = false;
                return;
            }

            CaptureRestPose();
            SetIndicatorVisible(false);
        }

        protected virtual void OnDisable()
        {
            ReleaseJoystick();
        }

        private void Reset()
        {
            background = transform as RectTransform;
            if (transform.childCount > 0)
                handle = transform.GetChild(0) as RectTransform;
        }

        private void OnValidate()
        {
            deadZone = Mathf.Clamp(deadZone, 0f, 0.5f);
            handleRange = Mathf.Clamp(handleRange, 0.1f, 1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanClaim(eventData) || !HasValidReferences()) return;

            _activePointerId = eventData.pointerId;
            CaptureRestPose();
            background.ForceUpdateRectTransforms();

            if (mode == Mode.Floating)
                MoveBackgroundCenterTo(eventData);

            UpdateInput(eventData);
            onPointerDown?.Invoke(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _activePointerId) return;
            UpdateInput(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _activePointerId) return;

            Vector2 pointerPosition = eventData.position;
            ReleaseJoystick();
            onPointerUp?.Invoke(pointerPosition);
        }

        public void ResetInput()
        {
            ReleaseJoystick();
        }

        private bool CanClaim(PointerEventData eventData)
        {
            return eventData != null
                && eventData.button == PointerEventData.InputButton.Left
                && _activePointerId == Unclaimed;
        }

        private bool HasValidReferences()
        {
            return background != null && handle != null;
        }

        private void CaptureRestPose()
        {
            if (_restPoseCaptured || !HasValidReferences()) return;

            _backgroundRestPosition = background.anchoredPosition;
            _handleRestPosition = handle.anchoredPosition;
            _restPoseCaptured = true;
        }

        private void ReleaseJoystick()
        {
            _activePointerId = Unclaimed;
            ApplyDirection(Vector2.zero);

            if (handle != null && _restPoseCaptured)
                handle.anchoredPosition = _handleRestPosition;

            if (background != null && mode == Mode.Floating && _restPoseCaptured)
                background.anchoredPosition = _backgroundRestPosition;

            SetIndicatorVisible(false);
        }

        private void MoveBackgroundCenterTo(PointerEventData eventData)
        {
            if (!(background.parent is RectTransform parent)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, eventData.position, eventData.pressEventCamera, out Vector2 pointerInParent))
                return;

            Vector3 centerWorld = background.TransformPoint(background.rect.center);
            Vector2 centerInParent = parent.InverseTransformPoint(centerWorld);
            background.anchoredPosition += pointerInParent - centerInParent;
        }

        private void UpdateInput(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out Vector2 pointerLocal))
                return;

            float inputRadius = GetInputRadius();
            if (inputRadius <= Mathf.Epsilon)
            {
                ApplyDirection(Vector2.zero);
                return;
            }

            Vector2 offset = FilterAxes(pointerLocal - background.rect.center);
            float distance = offset.magnitude;
            Vector2 unit = distance > Mathf.Epsilon ? offset / distance : Vector2.zero;
            float normalizedDistance = Mathf.Clamp01(distance / inputRadius);
            float magnitude = normalizedDistance <= deadZone
                ? 0f
                : (normalizedDistance - deadZone) / (1f - deadZone);

            Vector2 nextDirection = unit * magnitude;
            ApplyDirection(nextDirection);
            UpdateHandle(nextDirection, inputRadius);
            UpdateIndicator(nextDirection);
        }

        private Vector2 FilterAxes(Vector2 value)
        {
            switch (activeAxes)
            {
                case Axis.Horizontal:
                    value.y = 0f;
                    break;
                case Axis.Vertical:
                    value.x = 0f;
                    break;
            }

            return value;
        }

        private float GetInputRadius()
        {
            Rect rect = background.rect;
            return Mathf.Min(Mathf.Abs(rect.width), Mathf.Abs(rect.height)) * 0.5f;
        }

        private float GetHandleTravel(float inputRadius)
        {
            float travel = inputRadius;
            if (keepHandleInsideBackground)
            {
                Vector2 scaledHandleSize = Vector2.Scale(handle.rect.size, Abs(handle.localScale));
                float handleRadius = Mathf.Max(scaledHandleSize.x, scaledHandleSize.y) * 0.5f;
                travel = Mathf.Max(0f, inputRadius - handleRadius);
            }

            return travel * handleRange;
        }

        private void UpdateHandle(Vector2 direction, float inputRadius)
        {
            handle.anchoredPosition = _handleRestPosition + direction * GetHandleTravel(inputRadius);
        }

        private void UpdateIndicator(Vector2 direction)
        {
            if (directionIndicator == null) return;

            bool visible = direction.sqrMagnitude > DirectionEpsilon;
            SetIndicatorVisible(visible);
            if (visible)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                directionIndicator.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void SetIndicatorVisible(bool visible)
        {
            if (directionIndicator != null && directionIndicator.gameObject.activeSelf != visible)
                directionIndicator.gameObject.SetActive(visible);
        }

        private void ApplyDirection(Vector2 value)
        {
            value = Vector2.ClampMagnitude(value, 1f);
            if ((Direction - value).sqrMagnitude <= DirectionEpsilon) return;

            Direction = value;
            onValueChanged?.Invoke(Direction);
        }

        private static Vector2 Abs(Vector3 value)
        {
            return new Vector2(Mathf.Abs(value.x), Mathf.Abs(value.y));
        }
    }
}
