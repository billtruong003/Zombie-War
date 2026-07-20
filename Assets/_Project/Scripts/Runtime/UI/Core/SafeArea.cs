using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Gắn vào RectTransform con đầu tiên dưới Canvas của mỗi screen —
    /// co anchor theo Screen.safeArea (notch/home bar). Idempotent, chạy lại khi resolution đổi.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class SafeArea : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _applied = Rect.zero;

        private void OnEnable()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (_applied != Screen.safeArea) Apply();
        }

        private void Apply()
        {
            var sa = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var min = sa.position;
            var max = sa.position + sa.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
            _applied = sa;
        }
    }
}
