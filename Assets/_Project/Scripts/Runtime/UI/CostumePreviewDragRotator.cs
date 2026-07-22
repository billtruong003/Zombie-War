using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombieWar.UI
{
    /// <summary>
    /// Kéo/vuốt ngang trên PreviewCard (RawImage) để xoay nhân vật preview quanh trục Y.
    /// Xoay ROOT nhân vật, không đụng camera/lighting. Chuột (Editor) + chạm (device) qua
    /// IDragHandler. Kéo dọc bỏ qua. Có quán tính giảm nhanh + ngưỡng phân biệt tap vs drag.
    /// KHÔNG poll mỗi frame trong màn; chỉ chạy khi có drag hoặc còn quán tính.
    /// </summary>
    public sealed class CostumePreviewDragRotator : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Root nhân vật preview cần xoay (PreviewPivot hoặc PreviewCharacter).")]
        [SerializeField] private Transform target;
        [SerializeField] private float degreesPerPixel = 0.4f;
        [SerializeField] private float inertiaDamping = 8f;   // càng lớn dừng càng nhanh
        [SerializeField] private float defaultYawOffset = 0f; // lệch so với góc authored (0 = giữ facing gốc)

        private float _yaw;
        private float _authoredYaw;
        private float _velocity;
        private bool _dragging;

        public void SetTarget(Transform t)
        {
            target = t;
            if (t != null) _authoredYaw = t.localEulerAngles.y; // giữ facing authored (chính diện như Hub)
        }

        /// Về góc mặc định (gọi khi mở màn để deterministic) = facing authored + offset.
        public void ResetToDefault()
        {
            _yaw = _authoredYaw + defaultYawOffset;
            _velocity = 0f;
            _dragging = false;
            if (target != null) ApplyYaw();
        }

        private void OnEnable() => ResetToDefault();

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = true;
            _velocity = 0f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (target == null) return;
            float delta = -e.delta.x * degreesPerPixel; // kéo phải -> xoay theo chiều trực giác
            _yaw += delta;
            _velocity = delta / Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            ApplyYaw();
        }

        public void OnEndDrag(PointerEventData e) => _dragging = false;

        private void Update()
        {
            if (_dragging || target == null) return;
            if (Mathf.Abs(_velocity) < 1f) { _velocity = 0f; return; } // đứng yên -> không làm gì
            _yaw += _velocity * Time.unscaledDeltaTime;
            _velocity = Mathf.MoveTowards(_velocity, 0f, inertiaDamping * Mathf.Abs(_velocity) * Time.unscaledDeltaTime + 1f);
            ApplyYaw();
        }

        private void ApplyYaw()
        {
            var eul = target.localEulerAngles;
            eul.y = _yaw;
            target.localEulerAngles = eul;
        }
    }
}
