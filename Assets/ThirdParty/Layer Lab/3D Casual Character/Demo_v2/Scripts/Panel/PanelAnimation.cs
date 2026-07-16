using System;
using UnityEngine;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class PanelAnimation : MonoBehaviour
    {
        [SerializeField] private AnimationClip[] animationClip;
        [SerializeField] private ButtonAnimation buttonAnimation;
        [SerializeField] private Transform buttonParent; 
        public bool IsShow { get; set; }
        private int CurrentAnimationIndex { get; set; }
        public Action<string> OnChangeAnimation { get; set; }
        public string CurrentAnimationName => animationClip[CurrentAnimationIndex].name;
        
        public void Init()
        {
            for (var i = 0; i < animationClip.Length; i++)
            {
                var button = Instantiate(buttonAnimation, buttonParent);
                button.SetButton(i, animationClip[i]);
            }
    
            buttonAnimation.gameObject.SetActive(false);
        }

        public void PlayAnimationByIndex(int index)
        {
            CurrentAnimationIndex = index;
            
            //애니메이션 교체
            ChangeAnimation();
            Close();
        }

        public void PreviousAnimation()
        {
            if (CurrentAnimationIndex <= 0)
            {
                CurrentAnimationIndex = animationClip.Length - 1;
            }
            else
            {
                CurrentAnimationIndex--;
            }
            
            //애니메이션 교체
            ChangeAnimation();
        }
        
        public void NextAnimation()
        {
            if (CurrentAnimationIndex >= animationClip.Length - 1)
            {   
                CurrentAnimationIndex = 0;
            }
            else
            {
                CurrentAnimationIndex++;
            }

            //애니메이션 교체
            ChangeAnimation();
        }

        
        /// <summary>
        /// 애니메이션 교체 
        /// </summary>
        private void ChangeAnimation()
        {
            OnChangeAnimation.Invoke(animationClip[CurrentAnimationIndex].name);
            Demo2Character.Instance.PlayAnimation(animationClip[CurrentAnimationIndex]);
        }
        
        private void SetActive()
        {
            IsShow = !IsShow;
            gameObject.SetActive(IsShow);
        }
        
        
        public void Show()
        {
            gameObject.SetActive(IsShow = true);
        }
        
        public void Close()
        {
            gameObject.SetActive(IsShow = false);
        }
    }
}

