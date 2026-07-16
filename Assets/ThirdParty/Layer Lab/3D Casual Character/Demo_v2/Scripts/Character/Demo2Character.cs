using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public enum PartType
    {
        None,
        //A
        Beard,     // 수염
        Brow,    // 눈썹
        Earring,    // 귀걸이
        Eye,      // 눈
        Eyewear,    // 눈 액세서리
        Hair,       //머리카락
        Mouth,     // 입
        Body,

        //B
        Back,      // 등 (가방)
        Chest,   // 상의
        Foot,     // 신발
        Hand,     //장갑
        Head,      // 머리
        Leg,   // 하의
        Wield_Gear_Left,    // 왼손
        Wield_Gear_Right    // 오른손
    }
    
    public class Demo2Character : MonoBehaviour
    {
        public static Demo2Character Instance { get; private set; }
        private CharacterPart[] _parts;
        private Animator _animator;

        public Action<PartType, int> OnPartChanged;
        public Action<PartType, bool> OnPartActiveChanged;
        public Action OnRandomChanged;
        public Action OnNextPart;
        public Action OnPreviousPart;
        public Action OnPreset;
        
#if UNITY_EDITOR
        public CharacterPrefabSaver CharacterPrefabSaver { get; set; }
#endif

        public CharacterPart CurrentCharacterPartByType(PartType type) => _parts.FirstOrDefault(p => p.PartType == type);
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void Init()
        {
#if UNITY_EDITOR
            CharacterPrefabSaver = GetComponent<CharacterPrefabSaver>();
#endif
            _parts = transform.GetComponentsInChildren<CharacterPart>();
            _animator = transform.GetComponentInChildren<Animator>();
            foreach (var part in _parts) part.SetPart();
        }

 
        /// <summary>
        /// 모든 파츠 딕셔너리 저장 
        /// </summary>
        /// <returns></returns>
        public Dictionary<PartType, int> CurrentPartsTypeAndNameList()
        {
            var saveData = new Dictionary<PartType, int>();
            for (var i = 0; i < _parts.Length; i++)
            {
                saveData.Add(_parts[i].PartType, _parts[i].CurrentIndex); 
            }

            return saveData;
        }
        
        /// <summary>
        /// 타입별 파츠 리스트 가져오기
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public List<GameObject> CurrentPartsType(PartType type)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i].PartType == type)
                {
                    return _parts[i].CurrentPartsObjects;
                }
            }
            return new List<GameObject>();
        }
        
        /// <summary>
        /// 캐릭터 애니메이션 재생
        /// </summary>
        /// <param name="clip"></param>
        public void PlayAnimation(AnimationClip clip)
        {
            _animator.CrossFadeInFixedTime(clip.name, 0.25f);
        }
        
        
        private void OnDisable()
        {
            OnPartChanged = null;
            OnPartActiveChanged = null;
            OnRandomChanged = null;
        }

        private void Update()
        {
            #if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.S))
            {
                CharacterPrefabSaver.SaveAsPrefab();
            }
            #endif
        }
    }
}
