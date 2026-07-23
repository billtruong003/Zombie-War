using BillGameCore;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Animation tokens Sheet C — đóng trên BillTween (KHÔNG DOTween).
    /// Components: UIFxBreathe / UIFxPulse / UIFxPress (mỗi class 1 file — Unity yêu cầu để serialize vào scene).
    /// Mọi tween unscaled + SetTarget để KillTarget khi disable — không callback vào object đã destroy.
    /// Reduced Motion (A11Y): PlayerPrefs "reduced_motion" = 1 → motion tắt, chỉ còn fade.
    /// </summary>
    public static class UIFx
    {
        public const string ReducedMotionKey = "reduced_motion";
        public static bool ReducedMotion => PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1;

        /// <summary>Value-changed feedback (currency tick, claim): scale punch 1→1.12→1.
        /// Reduced Motion / trước bootstrap → bỏ qua.</summary>
        public static void Punch(Transform t)
        {
            if (t == null || ReducedMotion || !Bill.IsReady) return;
            BillTween.KillTarget(t);
            t.localScale = Vector3.one;
            BillTween.Scale(t, 1.12f, 0.09f)
                ?.SetLoops(2, LoopType.Yoyo).SetEase(EaseType.OutQuad)
                .OnComplete(() => t.localScale = Vector3.one)
                .SetUnscaled().SetTarget(t);
        }

        /// <summary>Error/thiếu tiền: shake ±4px (Sheet C). Reduced Motion / trước bootstrap → bỏ qua.</summary>
        public static void Shake(RectTransform rt)
        {
            if (rt == null || ReducedMotion || !Bill.IsReady) return;
            BillTween.KillTarget(rt);
            var origin = rt.anchoredPosition;
            BillTween.Float(0f, 1f, 0.3f,
                    t => rt.anchoredPosition = origin + new Vector2(Mathf.Sin(t * 40f) * 4f * (1f - t), 0f))
                .OnComplete(() => rt.anchoredPosition = origin)
                .SetUnscaled().SetTarget(rt);
        }
    }
}
