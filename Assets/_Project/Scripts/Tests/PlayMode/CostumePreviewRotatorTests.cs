using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using ZombieWar.UI;

namespace ZombieWar.Tests
{
    /// PlayMode: drag rotator xoay yaw quanh Y, KHÔNG dời vị trí root; ResetToDefault về facing authored.
    public class CostumePreviewRotatorTests
    {
        [Test]
        public void Drag_RotatesYaw_KeepsPosition()
        {
            var target = new GameObject("PreviewRoot");
            target.transform.position = new Vector3(1, 2, 3);
            target.transform.localEulerAngles = new Vector3(0, 45, 0); // authored facing

            var rotGO = new GameObject("Rotator");
            var rot = rotGO.AddComponent<CostumePreviewDragRotator>();
            rot.SetTarget(target.transform);
            rot.ResetToDefault();

            Assert.AreEqual(45f, target.transform.localEulerAngles.y, 0.5f, "Reset về facing authored (45).");
            Assert.AreEqual(new Vector3(1, 2, 3), target.transform.position, "Vị trí không đổi.");

            var ped = new PointerEventData(EventSystem.current) { delta = new Vector2(100, 0) };
            rot.OnBeginDrag(ped);
            rot.OnDrag(ped);
            rot.OnEndDrag(ped);

            Assert.AreNotEqual(45f, target.transform.localEulerAngles.y, "Kéo ngang phải đổi yaw.");
            Assert.AreEqual(new Vector3(1, 2, 3), target.transform.position, "Kéo KHÔNG dời vị trí (chỉ xoay).");

            Object.Destroy(target); Object.Destroy(rotGO);
        }

        [Test]
        public void VerticalDrag_DoesNotTilt()
        {
            var target = new GameObject("PreviewRoot2");
            target.transform.localEulerAngles = Vector3.zero;
            var rot = new GameObject("Rotator2").AddComponent<CostumePreviewDragRotator>();
            rot.SetTarget(target.transform);
            rot.ResetToDefault();

            var ped = new PointerEventData(EventSystem.current) { delta = new Vector2(0, 200) };
            rot.OnBeginDrag(ped); rot.OnDrag(ped); rot.OnEndDrag(ped);

            Assert.AreEqual(0f, target.transform.localEulerAngles.x, 0.01f, "Kéo dọc không nghiêng (chỉ Y).");
            Assert.AreEqual(0f, target.transform.localEulerAngles.z, 0.01f);

            Object.Destroy(target.gameObject); Object.Destroy(rot.gameObject);
        }
    }
}
