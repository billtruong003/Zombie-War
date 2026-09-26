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

        /// <summary>M8: an element arrives — scale from <paramref name="from"/> to 1 with a small
        /// overshoot, after <paramref name="delay"/> (unscaled, so it plays over a paused world).</summary>
        public static void PopIn(Transform t, float delay = 0f, float from = 0.85f, float duration = 0.28f)
        {
            if (t == null) return;
            if (ReducedMotion || !Bill.IsReady) { t.localScale = Vector3.one; return; }
            BillTween.KillTarget(t);
            t.localScale = new Vector3(from, from, from);
            var tw = BillTween.Scale(t, 1f, duration)?.SetEase(EaseType.OutBack).SetUnscaled().SetTarget(t)
                .OnComplete(() => { if (t != null) t.localScale = Vector3.one; });
            if (tw == null) t.localScale = Vector3.one;
            else if (delay > 0f) tw.SetDelay(delay);
        }

        static readonly System.Collections.Generic.Dictionary<int, float> AuthoredAlpha = new();

        /// <summary>M8: a graphic (a modal's dim) fades from clear to its authored alpha.</summary>
        public static void FadeIn(UnityEngine.UI.Graphic g, float duration = 0.18f)
        {
            if (g == null || !Bill.IsReady) return;
            BillTween.KillTarget(g);
            var c = g.color;
            // The authored alpha is remembered on first use: a fade cut short by a quick close must
            // not become the next open's target.
            int id = g.GetInstanceID();
            if (!AuthoredAlpha.TryGetValue(id, out float target)) AuthoredAlpha[id] = target = c.a;
            c.a = 0f; g.color = c;
            var tw = BillTween.Float(0f, target, duration, v => { if (g != null) { var k = g.color; k.a = v; g.color = k; } })
                ?.SetEase(EaseType.OutQuad).SetUnscaled().SetTarget(g);
            if (tw == null) { c.a = target; g.color = c; }
        }

        /// <summary>M8: a modal arrives — its "Dim" fades in and its "Panel" pops.</summary>
        public static void ModalIn(Transform modal)
        {
            if (modal == null) return;
            FadeIn(modal.Find("Dim")?.GetComponent<UnityEngine.UI.Graphic>());
            PopIn(modal.Find("Panel"), 0f, 0.9f, 0.24f);
        }

        /// <summary>M8: a number counts up to its value instead of appearing (result screen).</summary>
        public static void CountUp(TMPro.TMP_Text label, long to, float duration, System.Func<long, string> format, float delay = 0f)
        {
            if (label == null) return;
            if (ReducedMotion || !Bill.IsReady || to <= 0) { label.text = format(to); return; }
            BillTween.KillTarget(label);
            label.text = format(0);
            var tw = BillTween.Float(0f, to, duration, v => { if (label != null) label.text = format((long)v); })
                ?.SetEase(EaseType.OutCubic).SetUnscaled().SetTarget(label)
                .OnComplete(() => { if (label != null) label.text = format(to); });
            if (tw == null) label.text = format(to);
            else if (delay > 0f) tw.SetDelay(delay);
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
