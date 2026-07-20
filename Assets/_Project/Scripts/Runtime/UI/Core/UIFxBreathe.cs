using BillGameCore;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// PLAY idle breathing: scale 1→1.02 loop 2.4s (Sheet C). Reduced Motion → tắt.
    /// Đặt trên WRAPPER (không cùng object với UIFxPress) để hai tween không ghi cùng transform.
    /// An toàn trước bootstrap: editor installer deactivate object khi Bill chưa chạy —
    /// mọi đường OnEnable/OnDisable đều phải no-op sạch nếu TweenService chưa đăng ký.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIFxBreathe : MonoBehaviour
    {
        private void OnEnable()
        {
            if (!Bill.IsReady || UIFx.ReducedMotion) return;
            BillTween.Scale(transform, 1.02f, 1.2f)
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
