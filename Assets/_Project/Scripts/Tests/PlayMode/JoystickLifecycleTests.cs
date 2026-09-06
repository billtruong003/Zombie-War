using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using BillGameCore;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5.1.1 CP7 - mobile input lifecycle. A swallowed pointer-up (focus loss, app pause) must
    /// never leave a stale movement vector steering the player, and a fresh pointer must work
    /// normally afterward. The focus hooks are protected virtuals, so a test subclass exposes the
    /// exact component contract without adding public API for tests.
    /// </summary>
    public class JoystickLifecycleTests
    {
        private sealed class TestableJoystick : VirtualJoystick
        {
            public void SimulateFocusLoss() => OnApplicationFocus(false);
            public void SimulateAppPause() => OnApplicationPause(true);
        }

        private GameObject _canvasGo;
        private TestableJoystick _joystick;

        [SetUp]
        public void SetUp()
        {
            _canvasGo = new GameObject("TestCanvas", typeof(Canvas));
            var jgo = new GameObject("Joystick", typeof(RectTransform));
            jgo.transform.SetParent(_canvasGo.transform, false);
            ((RectTransform)jgo.transform).sizeDelta = new Vector2(300f, 300f);

            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(jgo.transform, false);
            ((RectTransform)handleGo.transform).sizeDelta = new Vector2(100f, 100f);

            // The joystick validates its references in Awake, so wire them while inactive.
            jgo.SetActive(false);
            _joystick = jgo.AddComponent<TestableJoystick>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(BillVirtualJoystick).GetField("background", flags)
                .SetValue(_joystick, (RectTransform)jgo.transform);
            typeof(BillVirtualJoystick).GetField("handle", flags)
                .SetValue(_joystick, (RectTransform)handleGo.transform);
            jgo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        }

        private PointerEventData Drag(Vector2 screenPos, int pointerId = 0)
        {
            var data = new PointerEventData(EventSystem.current) { pointerId = pointerId, position = screenPos };
            return data;
        }

        private void PressAndDrag()
        {
            var center = (Vector2)_joystick.transform.position;
            _joystick.OnPointerDown(Drag(center));
            _joystick.OnDrag(Drag(center + new Vector2(120f, 0f)));
        }

        [UnityTest]
        public IEnumerator FocusLoss_ClearsHeldDirection()
        {
            PressAndDrag();
            Assert.Greater(_joystick.Direction.magnitude, 0.1f, "drag did not register");

            _joystick.SimulateFocusLoss();
            yield return null;

            Assert.AreEqual(Vector2.zero, _joystick.Direction,
                "focus loss must clear the held movement vector");
            Assert.IsFalse(_joystick.IsHeld, "focus loss must release the pointer claim");
        }

        [UnityTest]
        public IEnumerator AppPause_ClearsHeldDirection()
        {
            PressAndDrag();
            Assert.Greater(_joystick.Direction.magnitude, 0.1f);

            _joystick.SimulateAppPause();
            yield return null;

            Assert.AreEqual(Vector2.zero, _joystick.Direction,
                "app pause must clear the held movement vector");
        }

        [UnityTest]
        public IEnumerator FreshPointer_WorksAfterFocusLossRelease()
        {
            PressAndDrag();
            _joystick.SimulateFocusLoss();
            yield return null;

            PressAndDrag();   // brand-new press must claim and steer normally
            Assert.Greater(_joystick.Direction.magnitude, 0.1f,
                "a new pointer must be able to claim the joystick after a focus-loss release");
        }
    }
}
