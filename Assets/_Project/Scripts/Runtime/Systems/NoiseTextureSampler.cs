using UnityEngine;

namespace ZombieWar
{
    // Shake/recoil "noise" comes from an assigned Texture2D (artist-authored, e.g. a Perlin/Simplex
    // strip), not a procedural generator - scrolling through the texture over time gives an
    // art-directable, repeatable pattern instead of Mathf.PerlinNoise/Random.
    public static class NoiseTextureSampler
    {
        public static Vector2 Sample(Texture2D noiseTexture, float time, float seed)
        {
            if (noiseTexture == null) return Vector2.zero;

            float u = Mathf.Repeat(time, 1f);
            float v = Mathf.Repeat(seed, 1f);
            Color pixel = noiseTexture.GetPixelBilinear(u, v);
            return new Vector2(pixel.r * 2f - 1f, pixel.g * 2f - 1f);
        }
    }
}
