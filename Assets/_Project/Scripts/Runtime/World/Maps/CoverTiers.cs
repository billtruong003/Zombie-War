using UnityEngine;

namespace ZombieWar.World
{
    /// <summary>
    /// Ground cover of one baked chunk split by graphics tier (2026-10-02): the baker deals every grass
    /// tuft to one of three meshes per cell, and only the tiers the device can afford are drawn. A low
    /// phone gets the base layer; mid adds the second; high draws all (the dense meadow the owner asked
    /// for).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoverTiers : MonoBehaviour
    {
        [Tooltip("Cover renderers drawn from this tier up: 0 = every device, 1 = mid and high, 2 = high only.")]
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [SerializeField] private int[] tiers = new int[0];

        public void Set(Renderer[] r, int[] t) { renderers = r; tiers = t; }

        void OnEnable()
        {
            Apply(GraphicsTier.Current);
            GraphicsTier.Changed += Apply;
        }

        void OnDisable() => GraphicsTier.Changed -= Apply;

        void Apply(GraphicsTier.Level level)
        {
            for (int i = 0; i < renderers.Length && i < tiers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = tiers[i] <= (int)level;
        }
    }
}
