using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombieWar
{
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;

        public Vector2 Direction { get; private set; }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, eventData.pressEventCamera, out localPoint);

            float radius = background.sizeDelta.x * 0.5f;
            localPoint = Vector2.ClampMagnitude(localPoint, radius);
            handle.anchoredPosition = localPoint;
            Direction = radius > 0f ? localPoint / radius : Vector2.zero;
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            Direction = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
        }
    }
}
