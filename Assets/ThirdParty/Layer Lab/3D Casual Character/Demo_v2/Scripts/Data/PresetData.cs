namespace Layer_lab._3D_Casual_Character.Demo2
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(fileName = "PresetData", menuName = "Character/PresetData")]
    public class PresetData : ScriptableObject
    {
        public List<PresetItem> presetItems = new();

        public void SavePreset(int index, Dictionary<PartType, int> itemList)
        {
            // 기존 데이터 삭제 후 추가
            presetItems.RemoveAll(p => p.index == index);
            presetItems.Add(new PresetItem(index, itemList));
        }

        public Dictionary<PartType, int> LoadPreset(int index)
        {
            var preset = presetItems.Find(p => p.index == index);
            return preset != null ? new Dictionary<PartType, int>(preset.itemList) : new Dictionary<PartType, int>();
        }

        public void ClearPreset(int index)
        {
            presetItems.RemoveAll(p => p.index == index);
        }
    }

    [Serializable]
    public class PresetItem
    {
        public int index;
        public List<PartItem> parts = new();

        public Dictionary<PartType, int> itemList
        {
            get
            {
                Dictionary<PartType, int> dict = new();
                foreach (var part in parts)
                {
                    dict[part.partType] = part.value;
                }
                return dict;
            }
        }

        public PresetItem(int index, Dictionary<PartType, int> itemList)
        {
            this.index = index;
            foreach (var kvp in itemList)
            {
                parts.Add(new PartItem(kvp.Key, kvp.Value));
            }
        }
    }

    [Serializable]
    public class PartItem
    {
        public PartType partType;
        public int value;

        public PartItem(PartType partType, int value)
        {
            this.partType = partType;
            this.value = value;
        }
    }
}