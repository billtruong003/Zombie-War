using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Chip chọn logical slot trong màn Costume (hàng chip scroll ngang dưới tab Đầu/Thân/Chân).
    /// View mỏng bake sẵn trong prefab — runtime chỉ rebind label/selected, không Instantiate.
    /// </summary>
    public sealed class CostumeSlotChipView : MonoBehaviour
    {
        public Button button;
        public Image fill;
        public TMP_Text label;

        public void Bind(string text, bool selected, Color activeColor, Color dimLabelColor)
        {
            if (label != null)
            {
                label.text = text;
                label.color = selected ? Color.white : dimLabelColor;
            }
            if (fill != null) fill.color = selected ? activeColor : Color.clear;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
