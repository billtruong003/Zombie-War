using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class AnimationBar : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text textAnimation;
        private bool _isEnter;

        public void Init()
        {
            textAnimation.text = UIControl.Instance.PanelAnimation.CurrentAnimationName;
            UIControl.Instance.PanelAnimation.OnChangeAnimation += OnChangeAnimation;
        }


        private void Update()
        {
            if (!_isEnter)
            {
                if (Input.GetMouseButton(0)) SetAnimationPanel(false);
            }
        }

        private void OnChangeAnimation(string animationName)
        {
            textAnimation.text = animationName;
        }
        
        public void OnClick_Left()
        {
            UIControl.Instance.PanelAnimation.PreviousAnimation();
            SetAnimationPanel(false);
        }

        public void OnClick_Right()
        {
            UIControl.Instance.PanelAnimation.NextAnimation();
            SetAnimationPanel(false);
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (UIControl.Instance.PanelAnimation.IsShow)
            {
                SetAnimationPanel(false);
            }
            else
            {
                SetAnimationPanel(true);
            }
        }

        private void SetAnimationPanel(bool isOn)
        {
            if (isOn)
            {
                UIControl.Instance.PanelAnimation.Show();

            }
            else
            {
                UIControl.Instance.PanelAnimation.Close();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isEnter = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isEnter = false;
        }
    }
}
