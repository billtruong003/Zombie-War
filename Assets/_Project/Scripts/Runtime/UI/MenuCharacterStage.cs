using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Thin controller cho MenuCharacterPreviewStage prefab — TOÀN BỘ hạ tầng preview
    /// (character, camera, key light, RenderTexture asset) được author sẵn trong prefab/scene,
    /// designer chỉnh framing trực tiếp bằng Transform/Inspector, KHÔNG dựng gì lúc runtime.
    /// Runtime chỉ: validate references + apply costume đã lưu lên preview character.
    /// RenderTexture là asset persistent — không Release/Destroy theo lifecycle scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuCharacterStage : MonoBehaviour
    {
        [Header("Authored refs (bắt buộc — Validate All UI References kiểm tra)")]
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Light previewLight;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private CharacterModularApplier modularApplier;
        [SerializeField] private RenderTexture previewTexture;
        [SerializeField] private Animator animator;

        /// <summary>Applier của preview character — CostumeScreen dùng cho preview-only apply.</summary>
        public CharacterModularApplier ModularApplier => modularApplier;

        /// <summary>RT asset các RawImage (Hub/Costume) reference trực tiếp trong Inspector.</summary>
        public RenderTexture Texture => previewTexture;

        private void Start()
        {
            if (!ValidateRefs()) return;
            if (previewCamera.targetTexture != previewTexture)
                previewCamera.targetTexture = previewTexture;
            modularApplier.EnsureBoneMap(true);
            modularApplier.ApplySavedParts();
        }

        private bool ValidateRefs()
        {
            bool ok = true;
            if (previewCamera == null) { Debug.LogError("[MenuCharacterStage] thiếu previewCamera", this); ok = false; }
            if (previewTexture == null) { Debug.LogError("[MenuCharacterStage] thiếu previewTexture asset", this); ok = false; }
            if (characterRoot == null) { Debug.LogError("[MenuCharacterStage] thiếu characterRoot", this); ok = false; }
            if (modularApplier == null) { Debug.LogError("[MenuCharacterStage] thiếu modularApplier", this); ok = false; }
            if (previewLight == null) Debug.LogWarning("[MenuCharacterStage] thiếu previewLight", this);
            if (animator == null) Debug.LogWarning("[MenuCharacterStage] thiếu animator", this);
            return ok;
        }
    }
}
