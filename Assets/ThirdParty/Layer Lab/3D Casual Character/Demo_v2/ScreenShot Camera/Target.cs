using UnityEngine;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class Target : MonoBehaviour
    {
        [SerializeField] private GameObject[] turnOnOfftarget; 
        private void OnEnable()
        {
            foreach (var t in turnOnOfftarget)
            {
                t.SetActive(true);
            }
        }

        private void OnDisable()
        {
            foreach (var t in turnOnOfftarget)
            {
                t.SetActive(false);
            }
        }
    }
}
