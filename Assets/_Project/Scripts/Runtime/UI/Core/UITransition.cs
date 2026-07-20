using System;
using System.Collections;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Helper transition không phụ thuộc tween lib: fade CanvasGroup + slide-up nhẹ.
    /// Dùng qua coroutine của screen chủ (StartCoroutine(UITransition.Show(...))).
    /// </summary>
    public static class UITransition
    {
        /// <summary>Fade 0→1 + slide từ dưới lên SlidePixels. unscaled time (chạy được khi pause).</summary>
        public static IEnumerator Show(CanvasGroup cg, RectTransform rt, Action onDone = null)
        {
            var basePos = rt.anchoredPosition;
            var from = basePos + Vector2.down * UITheme.SlidePixels;
            float t = 0f;
            cg.alpha = 0f;
            rt.anchoredPosition = from;
            while (t < UITheme.FadeTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Ease(Mathf.Clamp01(t / UITheme.FadeTime));
                cg.alpha = k;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, basePos, k);
                yield return null;
            }
            cg.alpha = 1f;
            rt.anchoredPosition = basePos;
            onDone?.Invoke();
        }

        /// <summary>Fade 1→0 (không slide để cảm giác đóng nhanh).</summary>
        public static IEnumerator Hide(CanvasGroup cg, Action onDone = null)
        {
            float t = 0f;
            float start = cg.alpha;
            while (t < UITheme.FadeTime)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / UITheme.FadeTime));
                yield return null;
            }
            cg.alpha = 0f;
            onDone?.Invoke();
        }

        /// <summary>ease-out-quad — khớp cảm giác wireframe "premium polish".</summary>
        private static float Ease(float x) => 1f - (1f - x) * (1f - x);
    }
}
