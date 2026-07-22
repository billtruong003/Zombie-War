using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// View mỏng cho 1 ô costume part trong pool cố định (CostumeScreen rebind theo page).
    /// Catalog rất lớn (~nghìn part) nên KHÔNG bake mỗi part 1 GO — pool 18 cell + paging.
    /// </summary>
    public sealed class CostumeItemCardView : MonoBehaviour
    {
        public Button button;
        public Image icon;
        public TMP_Text nameLabel;
        public Image border;
        public GameObject selectedMarker;

        /// <summary>Slot + part đang bind (để click apply preview). Null nếu cell trống.</summary>
        [HideInInspector] public string slotName;
        [HideInInspector] public string partGuid;
        [HideInInspector] public string partName;

        public void Bind(string slot, in ModularCostumeCatalog.PartEntry entry, Sprite iconSprite)
        {
            slotName = slot;
            partGuid = entry.guid;
            partName = entry.name;
            if (nameLabel != null) nameLabel.text = entry.name;
            if (icon != null)
            {
                // Gan VO DIEU KIEN — cell pool rebind khong duoc giu sprite cua item truoc.
                icon.sprite = iconSprite;
                icon.color = Color.white;
            }
            gameObject.SetActive(true);
        }

        /// <summary>Bind option player-facing (part/virtual/body) — label + icon. State
        /// (owned/selected tint) do CostumeScreen set truc tiep tren icon/nameLabel.</summary>
        public void BindOption(string label, Sprite iconSprite)
        {
            if (nameLabel != null) nameLabel.text = label;
            if (icon != null)
            {
                icon.sprite = iconSprite; // vo dieu kien — cell pool rebind khong giu icon cu
                icon.color = Color.white;
                icon.enabled = iconSprite != null;
            }
            gameObject.SetActive(true);
        }

        public void Clear()
        {
            slotName = null;
            partGuid = null;
            partName = null;
            if (icon != null) icon.sprite = null;
            if (nameLabel != null) nameLabel.text = "";
            if (selectedMarker != null) selectedMarker.SetActive(false);
            gameObject.SetActive(false);
        }

        public void SetSelected(bool on)
        {
            if (selectedMarker != null) selectedMarker.SetActive(on);
        }
    }
}
