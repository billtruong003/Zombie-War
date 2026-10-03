using BillGameCore;
using UnityEngine;

namespace ZombieWar.UI
{
    /// The FTUE pointing hand: taps toward its fingertip (up-left) on a yoyo BillTween loop.
    public sealed class TapHand : MonoBehaviour
    {
        [SerializeField] private Vector2 tap = new Vector2(-16f, 16f);
        [SerializeField] private float duration = 0.45f;

        Vector2 _rest;

        void OnEnable()
        {
            var rt = (RectTransform)transform;
            _rest = rt.anchoredPosition;
            if (!Application.isPlaying) return;
            BillTween.Float(0f, 1f, duration, t => rt.anchoredPosition = _rest + tap * t)
                ?.SetEase(EaseType.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo)
                .SetTarget(this);
        }

        void OnDisable()
        {
            if (Application.isPlaying) BillTween.KillTarget(this);
            ((RectTransform)transform).anchoredPosition = _rest;
        }
    }
}
