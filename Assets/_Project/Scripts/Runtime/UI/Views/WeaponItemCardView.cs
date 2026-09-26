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
        [Tooltip("M8: solid rarity tile behind the icon.")]
        public Image tile;
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

        /// M8 icons: the gun's real look once owned, a pale silhouette before; always on its rarity tile.
        public void BindIcon(UIPrototypeCatalog catalog, bool owned)
        {
            if (data == null) return;
            if (icon != null && catalog != null)
            {
                var sprite = catalog.GetWeaponIcon(data, owned);
                if (sprite != null) { icon.sprite = sprite; icon.color = Color.white; icon.preserveAspect = true; }
            }
            if (tile != null) tile.color = data.TileColor;
            // The prefab bakes a border colour; the tier can change after baking, so it is set here too.
            if (border != null) border.color = data.TierColor;
            if (glow != null) { var c = data.TierColor; c.a = glow.color.a; glow.color = c; }
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
