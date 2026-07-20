using BillGameCore;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombieWar.UI
{
    /// <summary>
    /// Press feedback mọi button: scale .96 100ms (Sheet C). Reduced Motion → không scale.
    /// Không đặt cùng object với UIFxBreathe (PLAY dùng wrapper cho breathing).
    /// An toàn trước bootstrap (xem UIFxBreathe).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIFxPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData e)
        {
            if (!Bill.IsReady || UIFx.ReducedMotion) return;
            BillTween.KillTarget(this);
            BillTween.Scale(transform, 0.96f, 0.1f)?.SetEase(EaseType.OutQuad).SetUnscaled().SetTarget(this);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!Bill.IsReady || UIFx.ReducedMotion) return;
            BillTween.KillTarget(this);
            BillTween.Scale(transform, 1f, 0.1f)?.SetEase(EaseType.OutQuad).SetUnscaled().SetTarget(this);
        }

        private void OnDisable()
        {
            if (Bill.IsReady) BillTween.KillTarget(this);
            transform.localScale = Vector3.one;
        }
    }
}
