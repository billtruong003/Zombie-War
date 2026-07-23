using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>Editor-authored card pool used by the real costume shop.</summary>
    public sealed class ShopCostumeCardView : MonoBehaviour
    {
        public Button button;
        public Image icon;
        public Image border;
        public TMP_Text nameLabel;
        public TMP_Text priceLabel;
        public GameObject ownedBadge;

        [HideInInspector] public string offerId;
        [HideInInspector] public bool isSet;

        public void Bind(string id, bool setOffer, string displayName, Sprite sprite,
            WeaponTier rarity, WalletCurrency currency, long price, bool owned)
        {
            offerId = id;
            isSet = setOffer;
            if (nameLabel != null) nameLabel.text = displayName;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.color = Color.white;
            }
            if (border != null) border.color = UITheme.RarityColor((int)rarity);
            if (priceLabel != null) priceLabel.text = owned ? "OWNED" : $"{CurrencyTag(currency)} {price:N0}";
            if (ownedBadge != null) ownedBadge.SetActive(owned);
            gameObject.SetActive(true);
        }

        public void Clear()
        {
            offerId = null;
            isSet = false;
            gameObject.SetActive(false);
        }

        private static string CurrencyTag(WalletCurrency currency) =>
            currency == WalletCurrency.Gem ? "Gem" : currency == WalletCurrency.Gold ? "Gold" : "Coin";
    }
}
