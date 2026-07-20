using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>View mỏng cho slot vũ khí equipped ở Loadout (3 slot bake sẵn).</summary>
    public sealed class LoadoutSlotView : MonoBehaviour
    {
        public Button button;
        public Image icon;
        public Image border;
        public Image glow;
        public GameObject emptyState;    // dashed + mờ khi slot trống
        public GameObject selectedMarker;

        [HideInInspector] public WeaponData data;

        public void Bind(WeaponData weapon, Sprite iconSprite)
        {
            data = weapon;
            bool has = weapon != null;
            if (emptyState != null) emptyState.SetActive(!has);
            if (icon != null)
            {
                icon.gameObject.SetActive(has);
                if (has && iconSprite != null) { icon.sprite = iconSprite; icon.color = Color.white; }
            }
            if (border != null && has) border.color = weapon.TierColor;
            if (glow != null && has)
            {
                var c = weapon.TierColor; c.a = glow.color.a;
                glow.color = c;
            }
        }

        public void SetSelected(bool on)
        {
            if (selectedMarker != null) selectedMarker.SetActive(on);
        }
    }
}
