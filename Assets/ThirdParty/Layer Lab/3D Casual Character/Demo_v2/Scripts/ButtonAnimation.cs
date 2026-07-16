using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class ButtonAnimation : MonoBehaviour
    {
        [SerializeField] private TMP_Text textAnimationName;
        [SerializeField] private Button button;
        private int _index;
        public void SetButton(int index, AnimationClip clip)
        {
            _index = index;
            textAnimationName.text = clip.name;
            button.onClick.AddListener(() =>
            {
                UIControl.Instance.PanelAnimation.PlayAnimationByIndex(_index);
            });
        }
    }
}