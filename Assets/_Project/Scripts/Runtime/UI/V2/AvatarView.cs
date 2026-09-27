using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// A profile picture: the avatar (live character head, drawn art, or a style placeholder until
    /// the art exists) inside its frame. Follows the player's choice unless told what to show
    /// (picker tiles and preview).
    /// </summary>
    public sealed class AvatarView : MonoBehaviour
    {
        [SerializeField] private bool followProfile = true;
        [SerializeField] private RawImage live;
        [SerializeField] private Image art;
        [SerializeField] private GameObject placeholder;
        [SerializeField] private Image placeholderIcon;
        [SerializeField] private TMP_Text placeholderLabel;
        [SerializeField] private AvatarFrameView frame;
        [Tooltip("Placeholder icon per style: Live, Chibi, Badge, Mascot, Weapon, Sticker.")]
        [SerializeField] private Sprite[] styleIcons = new Sprite[6];

        void OnEnable()
        {
            if (!followProfile) return;
            PlayerProfile.AccountChanged += ShowCurrent;
            ShowCurrent();
        }

        void OnDisable() { if (followProfile) PlayerProfile.AccountChanged -= ShowCurrent; }

        public void ShowCurrent() => Show(AvatarCatalog.CurrentAvatar, AvatarCatalog.CurrentFrame);

        public void Show(AvatarCatalog.Avatar a, AvatarCatalog.Frame f)
        {
            if (a != null)
            {
                bool isLive = a.style == AvatarCatalog.Style.Live;
                var sprite = isLive ? null : a.Art;
                if (live != null)
                {
                    live.gameObject.SetActive(isLive);
                    if (isLive && live.texture == null)
                    {
                        var stage = FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
                        if (stage != null) live.texture = stage.Texture;
                    }
                }
                if (art != null) { art.gameObject.SetActive(!isLive && sprite != null); art.sprite = sprite; }
                if (placeholder != null) placeholder.SetActive(!isLive && sprite == null);
                if (!isLive && sprite == null)
                {
                    int s = (int)a.style;
                    if (placeholderIcon != null && styleIcons != null && s < styleIcons.Length) placeholderIcon.sprite = styleIcons[s];
                    if (placeholderLabel != null) placeholderLabel.text = "ART SOON";
                }
            }
            if (frame != null && f != null) frame.Show(f);
        }
    }
}
