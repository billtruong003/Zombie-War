using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// <summary>One square of the HUD skill bar: icon (or a two-letter badge until the art exists),
    /// a coloured border, a radial cooldown shade and a rank badge.</summary>
    public class SkillSlotView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text badge;
        [SerializeField] private Image border;
        [SerializeField] private Image cooldown;   // Filled, Radial360, clockwise from top
        [SerializeField] private TMP_Text rank;
        [SerializeField] private GameObject content;

        public void Show(Sprite sprite, string abbreviation, Color color, string rankText, float cooldown01)
        {
            if (content != null && !content.activeSelf) content.SetActive(true);
            if (icon != null)
            {
                icon.enabled = sprite != null;
                if (sprite != null) icon.sprite = sprite;
            }
            if (badge != null)
            {
                badge.enabled = sprite == null;
                badge.text = abbreviation;
                badge.color = color;
            }
            if (border != null) border.color = color;
            if (cooldown != null) cooldown.fillAmount = Mathf.Clamp01(cooldown01);
            if (rank != null) rank.text = rankText;
        }

        public void ShowEmpty()
        {
            if (content != null && content.activeSelf) content.SetActive(false);
            if (border != null) border.color = new Color(0.29f, 0.32f, 0.39f, 0.8f);
        }
    }
}
