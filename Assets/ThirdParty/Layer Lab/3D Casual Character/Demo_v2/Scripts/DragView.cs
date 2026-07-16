using UnityEngine;
using UnityEngine.EventSystems;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class DragView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private float rotationSpeed = 0.18f;
        private bool _isDragging;
        private Vector2 _startDragPos;
        [SerializeField] private Transform target;


        public void OnPointerDown(PointerEventData eventData)
        {
            _isDragging = true;
            _startDragPos = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            
            var currentDragPos = eventData.position;
            var dragDelta = currentDragPos - _startDragPos;

            var rotationX = dragDelta.x * rotationSpeed;
            target.transform.Rotate(Vector3.up, -rotationX, Space.World);

            _startDragPos = currentDragPos;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
        }
    }
}
