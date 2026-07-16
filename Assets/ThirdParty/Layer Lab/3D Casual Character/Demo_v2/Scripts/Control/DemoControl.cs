using UnityEngine;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class DemoControl : MonoBehaviour
    {
        public static DemoControl Instance { get; set; }
        
        [field: SerializeField] public string ItemImagePath { get; set; }
        [field: SerializeField] public PresetData PresetData { get; set; } // ScriptableObject 참조
        
        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Demo2Character.Instance.Init();
            UIControl.Instance.Init();
            Demo2Character.Instance.OnRandomChanged.Invoke();
        }
    }
}