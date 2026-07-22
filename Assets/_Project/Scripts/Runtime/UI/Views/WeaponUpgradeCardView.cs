using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>Editor-authored card for a single owned weapon's permanent star upgrade.</summary>
    public sealed class WeaponUpgradeCardView : MonoBehaviour
    {
        public WeaponData data;
        public Button button;
        public Image icon;
        public Image border;
        public TMP_Text nameLabel;
        public TMP_Text levelLabel;
        public TMP_Text statLabel;
        public TMP_Text resourceLabel;

        public void Clear()
        {
            data = null;
            gameObject.SetActive(false);
        }
    }
}
