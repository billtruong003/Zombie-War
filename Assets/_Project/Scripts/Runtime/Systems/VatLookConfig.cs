using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// One shared look for every VAT enemy in the game.
    ///
    /// Each baked enemy gets its own material (it has to - the position/normal maps differ per
    /// creature), which means "make the specular a bit tighter" would otherwise be 15 edits that
    /// drift apart. This asset is the single authored source for everything that is NOT per-creature,
    /// and the applier pushes it onto every enemy material at once.
    ///
    /// Per-creature data (albedo, position map, normal map) is never touched by this.
    ///
    /// Apply with: Tools/ZombieWar/Apply VAT Look.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/VAT Look Config", fileName = "VatLookConfig")]
    public class VatLookConfig : ScriptableObject
    {
        [Header("Stepped specular")]
        [Tooltip("How many hard bands the highlight is cut into. 1.5 gives two bands.")]
        [Range(1f, 5f)] public float specSteps = 1.5f;
        [Tooltip("Width of the highlight window. Larger = highlight covers more of the surface.")]
        [Range(0f, 1f)] public float specSize = 0.25f;
        [Range(0f, 3f)] public float specIntensity = 0.6f;

        [Header("Dissolve")]
        [Tooltip("Greyscale noise sampled on the R channel. Leave empty to use the built-in " +
                 "procedural noise instead.")]
        public Texture2D dissolveNoise;
        [Tooltip("Noise tiling per axis. X and Y are separate because the enemy UV islands are not " +
                 "square - a single scalar stretches the burn along whichever axis the unwrap " +
                 "squashed, which is what makes it read as stripes.")]
        public Vector2 dissolveNoiseTiling = new Vector2(14f, 14f);
        [ColorUsage(true, true)] public Color dissolveEdgeColor = new Color(1f, 0.35f, 0.1f, 1f);
        [Tooltip("Thickness of the glowing burn edge.")]
        [Range(0.001f, 0.3f)] public float dissolveEdgeWidth = 0.08f;
        [Tooltip("Seconds for the death dissolve to run 0 -> 1. Written into every enemy prefab.")]
        [Range(0.05f, 4f)] public float dissolveDuration = 0.7f;

        [Header("Hit flash")]
        [ColorUsage(true, true)] public Color hitFlashColor = Color.white;
        [Tooltip("Seconds for the white flash to fade out. Written into every enemy prefab.")]
        [Range(0.02f, 1f)] public float hitFlashDuration = 0.12f;
    }
}
