using BillGameCore;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Notification dot: pulse scale 1→1.25 loop 1.8s (Sheet C). Reduced Motion → tắt.
    /// An toàn trước bootstrap (xem UIFxBreathe).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIFxPulse : MonoBehaviour
    {
        private void OnEnable()
        {
            if (!Bill.IsReady || UIFx.ReducedMotion) return;
            BillTween.Scale(transform, 1.25f, 0.9f)
                ?.SetLoops(-1, LoopType.Yoyo).SetEase(EaseType.InOutSine)
                .SetUnscaled().SetTarget(this);
        }

        private void OnDisable()
        {
            if (Bill.IsReady) BillTween.KillTarget(this);
            transform.localScale = Vector3.one;
        }
    }
}
