using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class CharacterPart : MonoBehaviour
    {
        [field: SerializeField] public PartType PartType { get; set; } = PartType.None;
        public List<GameObject> CurrentPartsObjects { get; set; }= new();
        [field: SerializeField] public bool IsOnlyEquip { get; set; }
        [field: SerializeField] public float EquipChance { get; set; } = 50f;
        public int CurrentIndex { get; private set; } = -1;

        public string CurrentName() => CurrentIndex == -1 ? "" : CurrentPartsObjects[CurrentIndex].name;
        private int CurrentMaxIndex => CurrentPartsObjects.Count - 1;
        private List<CharacterPartItem> _characterPartItems = new(); 
        
        private void Awake()
        {
            PartType = Enum.Parse<PartType>(gameObject.name);
            _characterPartItems = transform.GetComponentsInChildren<CharacterPartItem>().ToList();
        }

        public void SetPart()
        {
            foreach (Transform child in transform)
            {
                CurrentPartsObjects.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }

            Demo2Character.Instance.OnPartActiveChanged += OnChangeActive;
            Demo2Character.Instance.OnPartChanged += OnPartChanged;
            Demo2Character.Instance.OnRandomChanged += OnRandomChanged;
            Demo2Character.Instance.OnNextPart += OnNextPart;
            Demo2Character.Instance.OnPreviousPart += OnPreviousPart;
        }

        
        /// <summary>
        /// 파츠 세팅 (CurrentIndex)
        /// </summary>
        public void SetParts()
        {
            HideAllParts();

            if(CurrentIndex < 0) return;
            CurrentPartsObjects[CurrentIndex].SetActive(true);
        }

        /// <summary>
        /// 모든 파츠 끄기
        /// </summary>
        private void HideAllParts()
        {
            foreach (Transform child in transform) child.gameObject.SetActive(false);
        }
        
        
        
        #region Event
        
        /// <summary>
        /// 이전
        /// </summary>
        private void OnNextPart()
        {
            if(UIControl.Instance.FocusPartTYpe != PartType) return;
            if (CurrentIndex < CurrentMaxIndex)
            {
                CurrentIndex++;
            }
            else
            {
                CurrentIndex = 0;   
            }

            
            SetParts();
            Demo2Character.Instance.OnPartChanged.Invoke(PartType, CurrentIndex);
        }
        
        /// <summary>
        /// 다음
        /// </summary>
        private void OnPreviousPart()
        {
            if(UIControl.Instance.FocusPartTYpe != PartType) return;
            if (CurrentIndex > 0)
            {
                CurrentIndex--;
            }
            else
            {
                CurrentIndex = CurrentMaxIndex;   
            }
            
            SetParts();
            Demo2Character.Instance.OnPartChanged.Invoke(PartType, CurrentIndex);
        }
        
        /// <summary>
        /// 파츠 켜고, 끄기 (파츠 눈아이콘으로 켜고끄기)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="active"></param>
        private void OnChangeActive(PartType type, bool active)
        {
            if(type != PartType) return;
            gameObject.SetActive(!active);
        }

        /// <summary>
        /// 파츠 교체 (타입, 인덱스)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="index"></param>
        private void OnPartChanged(PartType type, int index)
        {
            if(type != PartType) return;
            CurrentIndex = index;
            SetParts();
        }

        
        /// <summary>
        /// 랜덤
        /// </summary>
        private void OnRandomChanged()
        {
            try
            {
                HideAllParts();

                if (!IsOnlyEquip && EquipChance * 0.01f <= Random.value)
                {
                    Demo2Character.Instance.OnPartChanged(PartType, -1);
                    CurrentIndex = -1;
                    return;
                }

                CurrentIndex = Random.Range(0, CurrentPartsObjects.Count);

                if (PartType == PartType.Head && _characterPartItems[CurrentIndex].IsOnlyHeadItem && Demo2Character.Instance.CurrentCharacterPartByType(PartType.Hair).CurrentIndex != -1)
                {
                    Demo2Character.Instance.OnPartChanged(PartType.Hair, -1);
                }
            
                SetParts();
                Demo2Character.Instance.OnPartChanged.Invoke(PartType, CurrentIndex);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
           
        }
        #endregion
        
 

    }
}
