using UnityEngine;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class ItemFocusSlot : MonoBehaviour
    {
        public void OnClick_Next()
        {
            Demo2Character.Instance.OnNextPart.Invoke();
        }

        public void OnClick_Previous()
        {
            Demo2Character.Instance.OnPreviousPart.Invoke();
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}