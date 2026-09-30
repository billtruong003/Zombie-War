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

        // The rank pill is cream; its number was cream too and could not be read (2026-09-30).
        static readonly Color PillInk = new(0.12f, 0.14f, 0.19f, 1f);
        static readonly Color PillCream = new(0.96f, 0.95f, 0.92f, 1f);
        Image _pill;

        public void Show(Sprite sprite, string abbreviation, Color color, string rankText, float cooldown01, bool evolved = false)
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
            if (rank != null)
            {
                rank.text = rankText;
                rank.color = PillInk;
                if (_pill == null) _pill = rank.transform.parent != null ? rank.transform.parent.GetComponent<Image>() : null;
                if (_pill != null) _pill.color = evolved ? color : PillCream;   // an evolution's pill is gold
            }
        }

        public void ShowEmpty()
        {
            if (content != null && content.activeSelf) content.SetActive(false);
            if (border != null) border.color = new Color(0.29f, 0.32f, 0.39f, 0.8f);
        }
    }
}
