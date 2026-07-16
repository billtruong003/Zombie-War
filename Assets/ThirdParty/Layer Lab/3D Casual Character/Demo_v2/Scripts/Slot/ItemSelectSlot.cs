using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class ItemSelectSlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image imageItem; 
        private PartType _partType;
        private int _index;

        public void SetSlot(int index, string partsName, PartType partType)
        {
            _index = index;
            _partType = partType;

            imageItem.sprite = UIControl.GetSprite($"{DemoControl.Instance.ItemImagePath}/ScreenShot/{partsName}");
            gameObject.SetActive(true);
        }
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Demo2Character.Instance.OnPartChanged.Invoke(_partType, _index);
        }
    }
}