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
                // Without the stage's texture the RawImage draws a flat tinted square (QA B4): show
                // the style placeholder until the stage exists, and retry each frame meanwhile.
                bool liveReady = isLive && TryLinkStage();
                _waitingForStage = isLive && !liveReady;
                if (live != null) live.gameObject.SetActive(liveReady);
                if (art != null) { art.gameObject.SetActive(!isLive && sprite != null); art.sprite = sprite; }
                if (placeholder != null) placeholder.SetActive(!liveReady && sprite == null);
                if (!liveReady && sprite == null)
                {
                    int s = (int)a.style;
                    if (placeholderIcon != null && styleIcons != null && s < styleIcons.Length) placeholderIcon.sprite = styleIcons[s];
                    if (placeholderLabel != null) placeholderLabel.text = "ART SOON";
                }
            }
            if (frame != null && f != null) frame.Show(f);
            _shown = a; _shownFrame = f;
        }

        bool _waitingForStage;
        AvatarCatalog.Avatar _shown;
        AvatarCatalog.Frame _shownFrame;

        bool TryLinkStage()
        {
            if (live == null) return false;
            if (live.texture != null) return true;
            var stage = FindFirstObjectByType<MenuCharacterStage>(FindObjectsInactive.Include);
            if (stage == null || stage.Texture == null) return false;
            live.texture = stage.Texture;
            return true;
        }

        void Update()
        {
            if (_waitingForStage && Time.frameCount % 10 == 0 && TryLinkStage()) Show(_shown, _shownFrame);
        }
    }
}
