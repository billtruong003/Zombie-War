using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class CaptureControl : MonoBehaviour
    {
        [field: SerializeField] private ScreenShot ScreenShot { get; set; }
        private List<Target> _capturedObjects = new();
        [SerializeField] private GameObject[] hideObjects;
        [SerializeField] private Transform captureTransform;
        
        private void Start()
        {
            _capturedObjects = captureTransform.GetComponentsInChildren<Target>(true).ToList();

            foreach (var capturedObject in _capturedObjects) capturedObject.gameObject.SetActive(false);
        }

        public async void CaptureScreenshot()
        {
            foreach (var t in hideObjects) t.SetActive(false);
            foreach (var capturedObject in _capturedObjects)
            {
                // 현재 오브젝트 활성화
                capturedObject.gameObject.SetActive(true);

                // 스크린샷 캡처
                await ScreenShot.ScreenShotClickAsync(capturedObject.name);
                Debug.Log($"Screenshot for {capturedObject.name} saved.");
                // 현재 오브젝트 비활성화

                await Task.Yield();
                capturedObject.gameObject.SetActive(false);
                await Task.Yield();
            }

            Debug.Log("All screenshots captured and saved!");
        }
    }
}
