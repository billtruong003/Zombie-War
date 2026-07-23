using UnityEngine;
using UnityEngine.EventSystems;

namespace BillGameCore
{
    /// <summary>
    /// Virtual joystick chuẩn mobile cho mọi game BillGameCore.
    ///
    /// Kỹ thuật (theo các implementation joystick tốt nhất cho uGUI):
    /// - Floating origin: background NHẢY ĐẾN điểm chạm đầu tiên (trong vùng hit của component)
    ///   rồi mới tính drag từ đó — hết hẳn cảm giác "lệch" khi chạm rìa vùng điều khiển.
    /// - Pointer-id lock: chỉ ngón đặt xuống đầu tiên điều khiển; ngón thứ hai (bấm bomb,
    ///   pause...) không cướp/teleport joystick.
    /// - Radius theo rect thật (rect.width, không phải sizeDelta) — đúng cả khi anchor stretch
    ///   hoặc canvas scale khác 1.
    /// - Dead zone: dưới ngưỡng trả zero, trên ngưỡng remap 0..1 mượt (không nhảy bậc).
    /// - Handle clamp trong local space của background qua ScreenPointToLocalPointInRectangle
    ///   với đúng camera của event — đúng cho cả Overlay lẫn Screen Space Camera.
    ///
    /// Zero allocation mỗi frame; không Update — chỉ event-driven.
    /// </summary>
    [DisallowMultipleComponent]
    public class BillVirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum Mode
        {
            /// <summary>Background đứng yên tại chỗ author.</summary>
            Fixed,
            /// <summary>Background nhảy đến điểm chạm, thả tay trở về chỗ author.</summary>
            Floating,
        }

        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;

        [Tooltip("Floating = joystick hiện tại điểm chạm (khuyên dùng cho survivor/top-down).")]
        [SerializeField] private Mode mode = Mode.Floating;

        [Tooltip("Bán kính chết: |input| dưới ngưỡng này (0..1) trả zero — chống trôi do rung tay.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float deadZone = 0.08f;

        [Tooltip("Handle đi tối đa bao nhiêu phần bán kính background (1 = chạm mép).")]
        [Range(0.5f, 1f)]
        [SerializeField] private float handleRange = 1f;

        /// <summary>Hướng input đã qua dead-zone, độ dài 0..1.</summary>
        public Vector2 Direction { get; private set; }

        /// <summary>Đang có ngón tay giữ joystick không.</summary>
        public bool IsHeld => _activePointerId != Unclaimed;

        private const int Unclaimed = int.MinValue;

        private int _activePointerId = Unclaimed;
        private Vector2 _restPosition;
        private bool _restCaptured;

        protected virtual void Awake()
        {
            if (background == null || handle == null)
            {
                Debug.LogError("[BillVirtualJoystick] Thiếu background/handle — joystick tắt.", this);
                enabled = false;
                return;
            }
            CaptureRestPosition();
        }

        private void CaptureRestPosition()
        {
            if (_restCaptured) return;
            _restPosition = background.anchoredPosition;
            _restCaptured = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != Unclaimed) return;   // ngón khác đang giữ
            _activePointerId = eventData.pointerId;
            CaptureRestPosition();

            if (mode == Mode.Floating)
                MoveBackgroundTo(eventData);

            // Fixed: chạm là bắt đầu kéo từ điểm chạm (không teleport handle như bản cũ);
            // Floating: background vừa nhảy đến ngón tay nên input mở đầu = zero.
            UpdateInput(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            UpdateInput(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            ReleaseJoystick();
        }

        protected virtual void OnDisable() => ReleaseJoystick();

        private void ReleaseJoystick()
        {
            _activePointerId = Unclaimed;
            Direction = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
            if (mode == Mode.Floating && _restCaptured)
                background.anchoredPosition = _restPosition;
        }

        private void MoveBackgroundTo(PointerEventData eventData)
        {
            var parent = background.parent as RectTransform;
            if (parent == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, eventData.position, eventData.pressEventCamera, out var local))
            {
                // anchoredPosition tính từ anchor; local tính từ pivot của parent — quy đổi qua
                // hiệu giữa vị trí hiện tại và local point hiện tại của background.
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, ScreenPointOf(background, eventData.pressEventCamera), eventData.pressEventCamera, out var currentLocal);
                background.anchoredPosition += local - currentLocal;
            }
        }

        private static Vector2 ScreenPointOf(RectTransform rt, Camera cam)
            => RectTransformUtility.WorldToScreenPoint(cam, rt.position);

        private void UpdateInput(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out var local))
                return;

            float radius = background.rect.width * 0.5f;
            if (radius <= 0f) return;

            Vector2 raw = local / radius;
            float magnitude = raw.magnitude;

            if (magnitude < deadZone)
            {
                Direction = Vector2.zero;
                handle.anchoredPosition = Vector2.zero;
                return;
            }

            // Remap [deadZone..1] → [0..1] để input không nhảy bậc khi vừa qua ngưỡng.
            Vector2 unit = raw / magnitude;
            float mapped = Mathf.Min(1f, (magnitude - deadZone) / (1f - deadZone));
            Direction = unit * mapped;
            handle.anchoredPosition = unit * (mapped * radius * handleRange);
        }
    }
}
