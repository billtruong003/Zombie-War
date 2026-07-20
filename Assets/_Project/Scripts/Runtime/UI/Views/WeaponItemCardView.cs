using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// View mỏng cho card súng (Shop/Loadout) — mọi child do installer bake sẵn vào prefab,
    /// runtime CHỈ update các serialized reference này. Không Find, không Instantiate.
    /// </summary>
    public sealed class WeaponItemCardView : MonoBehaviour
    {
        public WeaponData data;
        public Button button;
        public Image icon;
        public TMP_Text nameLabel;
        public Image border;
        public Image glow;
        public GameObject priceChip;
        public TMP_Text priceLabel;
        public GameObject ownedBadge;
        public GameObject lockOverlay;
        public GameObject selectedMarker;

        public void Bind(WeaponData weapon, Sprite iconSprite, bool owned)
        {
            data = weapon;
            if (icon != null && iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.color = Color.white;
            }
            if (nameLabel != null && weapon != null) nameLabel.text = weapon.weaponName;
            if (border != null && weapon != null) border.color = weapon.TierColor;
            if (glow != null && weapon != null)
            {
                var c = weapon.TierColor; c.a = glow.color.a;
                glow.color = c;
            }
            SetOwned(owned, weapon != null ? Mathf.Max(weapon.price, weapon.unlockCost) : 0);
        }

        public void SetOwned(bool owned, int price)
        {
            if (ownedBadge != null) ownedBadge.SetActive(owned);
            if (lockOverlay != null) lockOverlay.SetActive(!owned);
            if (priceChip != null) priceChip.SetActive(!owned && price > 0);
            if (priceLabel != null) priceLabel.text = price.ToString("N0");
        }

        public void SetSelected(bool on)
        {
            if (selectedMarker != null) selectedMarker.SetActive(on);
            if (glow != null) glow.gameObject.SetActive(on);
        }
    }
}
