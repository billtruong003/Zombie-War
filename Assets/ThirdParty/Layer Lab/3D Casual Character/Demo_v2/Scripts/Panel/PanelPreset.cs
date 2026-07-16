using System.Collections.Generic;
using UnityEngine;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class PanelPreset : MonoBehaviour
    {
        [field: SerializeField] private PresetSlot PresetSlot { get; set; }
        private List<PresetSlot> _presetSlots = new();
        [SerializeField] private Transform parent;
        
        public void Init()
        {
            PresetSlot.gameObject.SetActive(false);
            for (var i = 0; i < 10; i++)
            {
                var preset = Instantiate(PresetSlot, parent);
                preset.InitSlot(i);
            }
        }
        
        public void RemovePreset(int index)
        {
            Destroy(_presetSlots[index].gameObject);
            _presetSlots.RemoveAt(index);
        }
    }
}