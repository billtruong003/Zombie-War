using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class PanelItem : MonoBehaviour
    {
        [field: SerializeField] private ItemSelectSlot ItemSelectSlot { get; set; }
        private readonly List<ItemSelectSlot> _itemSelectSlots = new ();
        
        [field: SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TMP_Text textTitle;
        [SerializeField] private Transform parent;
        [SerializeField] private Transform focus;
        private PartType _myPartType;

        public void Init()
        {
            ItemSelectSlot.gameObject.SetActive(false);
                
            for (var i = 0; i < 200; i++)
            {
                _itemSelectSlots.Add(Instantiate(ItemSelectSlot, parent));
            }
        }

        private void OnPartChanged(PartType partType, int partIndex)
        {
            if(_myPartType != partType) return;
            StartCoroutine(SetFocusCo(partType));
        }
        
        public void SetPanel(List<GameObject> items, PartType partType)
        {
            _myPartType = partType;
            
            focus.gameObject.SetActive(false);
            
            scrollRect.verticalNormalizedPosition = 1;
            textTitle.text = $"{partType}";

            for (var i = 0; i < items.Count; i++)
            {
                _itemSelectSlots[i].SetSlot(i, items[i].name, partType);
            }
            
            gameObject.SetActive(true);
            Demo2Character.Instance.OnPartChanged += OnPartChanged;
            StartCoroutine(SetFocusCo(partType));
            
        }

        private IEnumerator SetFocusCo(PartType partType)
        {
            yield return null;
            var focusIndex = Demo2Character.Instance.CurrentCharacterPartByType(partType).CurrentIndex;
            if (focusIndex == -1)
            {
                focus.gameObject.SetActive(false);
                yield break;
            }
            
            focus.gameObject.SetActive(true);
            focus.transform.position = _itemSelectSlots[focusIndex].transform.position;
        }


        public void Hide()
        {
            Demo2Character.Instance.OnPartChanged -= OnPartChanged;
            foreach (var t in _itemSelectSlots) t.Hide();
            gameObject.SetActive(false);
        }
    }
}