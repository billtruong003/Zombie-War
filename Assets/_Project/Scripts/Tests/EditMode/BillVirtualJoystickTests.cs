using BillGameCore;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombieWar.Tests
{
    public class BillVirtualJoystickTests
    {
        private GameObject _canvasObject;
        private GameObject _backgroundObject;
        private GameObject _handleObject;
        private EventSystem _eventSystem;
        private RectTransform _background;
        private RectTransform _handle;
        private BillVirtualJoystick _joystick;

        [SetUp]
        public void SetUp()
        {
            _canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            _eventSystem = eventSystemObject.GetComponent<EventSystem>();

            _backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            _backgroundObject.transform.SetParent(_canvasObject.transform, false);
            _background = _backgroundObject.GetComponent<RectTransform>();
            _background.anchorMin = Vector2.zero;
            _background.anchorMax = Vector2.zero;
            _background.pivot = Vector2.zero;
            _background.anchoredPosition = new Vector2(100f, 100f);
            _background.sizeDelta = new Vector2(200f, 200f);

            _handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            _handleObject.transform.SetParent(_background, false);
            _handle = _handleObject.GetComponent<RectTransform>();
            _handle.anchorMin = new Vector2(0.5f, 0.5f);
            _handle.anchorMax = new Vector2(0.5f, 0.5f);
            _handle.pivot = new Vector2(0.5f, 0.5f);
            _handle.sizeDelta = new Vector2(40f, 40f);

            _joystick = _backgroundObject.AddComponent<BillVirtualJoystick>();
            var serializedJoystick = new SerializedObject(_joystick);
            serializedJoystick.FindProperty("background").objectReferenceValue = _background;
            serializedJoystick.FindProperty("handle").objectReferenceValue = _handle;
            serializedJoystick.ApplyModifiedPropertiesWithoutUndo();

            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            if (_eventSystem != null)
                Object.DestroyImmediate(_eventSystem.gameObject);
            if (_canvasObject != null)
                Object.DestroyImmediate(_canvasObject);
        }

        [Test]
        public void FixedMode_UsesRectCenterWhenBackgroundPivotIsBottomLeft()
        {
            _joystick.OperatingMode = BillVirtualJoystick.Mode.Fixed;
            Vector2 center = ScreenPoint(_background.TransformPoint(_background.rect.center));

            _joystick.OnPointerDown(Pointer(center, 1));
            Assert.That(_joystick.Direction, Is.EqualTo(Vector2.zero));

            _joystick.OnDrag(Pointer(center + Vector2.right * 100f, 1));
            Assert.That(_joystick.Direction.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_joystick.Direction.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FloatingMode_CentersVisualRectOnTouchAndRestoresAuthoredPosition()
        {
            _joystick.OperatingMode = BillVirtualJoystick.Mode.Floating;
            Vector2 authoredPosition = _background.anchoredPosition;
            Vector2 touch = new Vector2(500f, 400f);

            _joystick.OnPointerDown(Pointer(touch, 2));

            Vector2 movedCenter = ScreenPoint(_background.TransformPoint(_background.rect.center));
            Assert.That(movedCenter.x, Is.EqualTo(touch.x).Within(0.01f));
            Assert.That(movedCenter.y, Is.EqualTo(touch.y).Within(0.01f));
            Assert.That(_joystick.Direction, Is.EqualTo(Vector2.zero));

            _joystick.OnPointerUp(Pointer(touch, 2));
            Assert.That(_background.anchoredPosition, Is.EqualTo(authoredPosition));
        }

        [Test]
        public void HorizontalAxis_FiltersVerticalInput()
        {
            _joystick.OperatingMode = BillVirtualJoystick.Mode.Fixed;
            _joystick.ActiveAxes = BillVirtualJoystick.Axis.Horizontal;
            Vector2 center = ScreenPoint(_background.TransformPoint(_background.rect.center));

            _joystick.OnPointerDown(Pointer(center, 3));
            _joystick.OnDrag(Pointer(center + new Vector2(100f, 100f), 3));

            Assert.That(_joystick.Direction.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_joystick.Direction.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SecondPointer_CannotStealActiveJoystick()
        {
            _joystick.OperatingMode = BillVirtualJoystick.Mode.Fixed;
            Vector2 center = ScreenPoint(_background.TransformPoint(_background.rect.center));

            _joystick.OnPointerDown(Pointer(center, 4));
            _joystick.OnDrag(Pointer(center + Vector2.right * 100f, 4));
            _joystick.OnPointerDown(Pointer(center, 5));
            _joystick.OnDrag(Pointer(center + Vector2.up * 100f, 5));

            Assert.That(_joystick.Direction.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_joystick.Direction.y, Is.EqualTo(0f).Within(0.001f));
        }

        private PointerEventData Pointer(Vector2 position, int pointerId)
        {
            return new PointerEventData(_eventSystem)
            {
                position = position,
                pointerId = pointerId,
                button = PointerEventData.InputButton.Left,
            };
        }

        private static Vector2 ScreenPoint(Vector3 worldPoint)
        {
            return RectTransformUtility.WorldToScreenPoint(null, worldPoint);
        }
    }
}
