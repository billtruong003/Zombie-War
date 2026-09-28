using UnityEditor;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Avatar art dropped into Assets/Resources/UI/Avatars (see Review/Avatars/AVATAR_PROMPTS.md)
    /// imports as a UI sprite: no mipmaps, at most 512 px, high-quality compression.
    /// </summary>
    public sealed class AvatarImportSettings : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/UI/Avatars/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.maxTextureSize = 512;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
