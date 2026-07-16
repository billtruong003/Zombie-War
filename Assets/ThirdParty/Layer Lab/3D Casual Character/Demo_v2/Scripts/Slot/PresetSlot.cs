using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Layer_lab._3D_Casual_Character.Demo2
{
    public class PresetSlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image imageCharacter;
        private Dictionary<PartType, int> _itemList = new();
        private int _index;
        public bool isPreset;

        public void InitSlot(int index)
        {
            _index = index;
            LoadPreset();
            gameObject.SetActive(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
#if UNITY_EDITOR
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                ClearPreset();
                return;
            }

            if (!isPreset)
            {
                isPreset = true;
                TakePreset();
                return;
            }
#endif

            foreach (var item in _itemList)
            {
                Demo2Character.Instance.OnPreset.Invoke();
                Demo2Character.Instance.OnPartChanged.Invoke(item.Key, item.Value);
            }

            Debug.Log($"Preset {_index} equiped successfully: {string.Join(", ", _itemList)}");
        }

        public async void TakePreset()
        {
            await ScreenShot.Instance.ScreenShotClickAsync($"{_index}");

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif

            _itemList = Demo2Character.Instance.CurrentPartsTypeAndNameList();
            imageCharacter.sprite = UIControl.GetSprite($"{ScreenShot.Instance.projectNameFolderName}/Preset/{_index}");
            DemoControl.Instance.PresetData.SavePreset(_index, _itemList);
        }

        private Sprite LoadSprite(string path)
        {
            if (!System.IO.File.Exists(path)) return null;

            byte[] fileData = System.IO.File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);

            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        private void LoadPreset()
        {
            _itemList = DemoControl.Instance.PresetData.LoadPreset(_index);
            if (_itemList.Count > 0)
            {
                isPreset = true;
                imageCharacter.sprite = UIControl.GetSprite($"{DemoControl.Instance.ItemImagePath}/Preset/{_index}");
                Debug.Log($"Preset {_index} loaded successfully: {string.Join(", ", _itemList)}");
            }
            else
            {
                isPreset = false;
                Debug.LogWarning($"No preset found for index {_index}");
            }
        }

        private void ClearPreset()
        {
            isPreset = false;
            imageCharacter.sprite = null;
            DemoControl.Instance.PresetData.ClearPreset(_index);
        }
    }
}
