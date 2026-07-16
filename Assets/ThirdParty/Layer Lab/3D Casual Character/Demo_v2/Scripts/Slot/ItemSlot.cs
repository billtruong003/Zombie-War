using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class ItemSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private bool _isHide;
        private Image _imageIcon;
        private Image _imagePicto;
        private Image _imageItem;
        private Image _imageBg;
        [field: SerializeField] public PartType PartType { get; set; }

        private void Awake()
        {
            _imageBg = transform.GetChild(0).GetComponent<Image>();
            _imageItem = _imageBg.transform.GetChild(0).GetComponent<Image>();
            _imagePicto = transform.GetChild(1).GetComponent<Image>();
            _imageIcon = transform.GetChild(2).GetComponent<Image>();
            
            PartType = Enum.Parse<PartType>(gameObject.name);
        }

        public void SetSlot()
        {
            try
            {
                _imageIcon.gameObject.SetActive(!Demo2Character.Instance.CurrentCharacterPartByType(PartType).IsOnlyEquip);
                Demo2Character.Instance.OnPartChanged += SetItemImage;
                Demo2Character.Instance.OnPreset += OnActive;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }

        public void OnActive()
        {
            _isHide = false;
            _imageIcon.sprite = UIControl.Instance.spriteActiveIcons[1];
            Demo2Character.Instance.OnPartActiveChanged.Invoke(PartType, _isHide);
        }
        
        public void OnClick_Active()
        {
            _isHide = !_isHide;
            _imageIcon.sprite = UIControl.Instance.spriteActiveIcons[_isHide ? 0 : 1];
            Demo2Character.Instance.OnPartActiveChanged.Invoke(PartType, _isHide);
        }
        
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && !Demo2Character.Instance.CurrentCharacterPartByType(PartType).IsOnlyEquip)
            {
                Demo2Character.Instance.OnPartChanged.Invoke(PartType, -1);
                return;
            }
            
            if(eventData.button == PointerEventData.InputButton.Right) return;
            
            UIControl.Instance.PanelItem.SetPanel(Demo2Character.Instance.CurrentPartsType(PartType), PartType);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIControl.Instance.SetFocusPart(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UIControl.Instance.SetFocusPart(null);
        }

        
        
        
        private void SetItemImage(PartType partsType, int index)
        {
            if(PartType != partsType) return;

            if (index < 0)
            {
                _imagePicto.gameObject.SetActive(true);
                _imageItem.gameObject.SetActive(false);
                _imageBg.sprite = UIControl.Instance.spriteBgs[0];
                return;
            }
            
            _imagePicto.gameObject.SetActive(false);
            _imageItem.gameObject.SetActive(true);
            _imageBg.sprite = UIControl.Instance.spriteBgs[1];
            _imageItem.sprite = UIControl.GetSprite($"{DemoControl.Instance.ItemImagePath}/ScreenShot/{Demo2Character.Instance.CurrentCharacterPartByType(partsType).CurrentName()}");
        }
    }
    
}