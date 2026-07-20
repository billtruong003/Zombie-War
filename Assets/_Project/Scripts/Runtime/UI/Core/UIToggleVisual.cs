using BillGameCore;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Visual cho Toggle pill (Sheet A): track đổi màu ON/OFF, knob trượt trái↔phải.
    /// UIKit chỉ build state ban đầu — component này giữ visual khớp Toggle.isOn suốt runtime.
    /// Knob anchor ML cố định; chỉ tween anchoredPosition.x (OFF=4 ↔ ON=track.width-knob-4).
    /// Tween qua BillTween khi Bill.IsReady và không Reduced Motion; ngược lại snap ngay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIToggleVisual : MonoBehaviour
    {
        public Toggle toggle;
        public Image track;
        public RectTransform knob;
        public Color onColor = new Color(0.298f, 0.686f, 0.431f);   // UITheme.Green
        public Color offColor = new Color(0.137f, 0.169f, 0.227f);  // UITheme.Surface2

        private void OnEnable()
        {
            if (toggle == null) toggle = GetComponent<Toggle>();
            if (toggle == null) return;
            toggle.onValueChanged.AddListener(OnChanged);
            Refresh(toggle.isOn, instant: true);
        }

        private void OnDisable()
        {
            if (toggle != null) toggle.onValueChanged.RemoveListener(OnChanged);
            if (Bill.IsReady) BillTween.KillTarget(this);
        }

        private void OnChanged(bool on) => Refresh(on, instant: false);

        private void Refresh(bool on, bool instant)
        {
            if (track != null) track.color = on ? onColor : offColor;
            if (knob == null) return;

            float trackW = track != null ? track.rectTransform.rect.width : 96f;
            float target = on ? trackW - knob.rect.width - 4f : 4f;

            if (instant || !Bill.IsReady || UIFx.ReducedMotion)
            {
                knob.anchoredPosition = new Vector2(target, 0f);
                return;
            }

            BillTween.KillTarget(this);
            BillTween.Float(knob.anchoredPosition.x, target, 0.15f,
                    v => { if (knob != null) knob.anchoredPosition = new Vector2(v, 0f); })
                ?.SetEase(EaseType.OutQuad).SetUnscaled().SetTarget(this);
        }
    }
}
