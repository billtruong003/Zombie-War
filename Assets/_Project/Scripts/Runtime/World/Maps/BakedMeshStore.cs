using UnityEngine;

namespace ZombieWar.World
{
    /// <summary>
    /// A binary file of baked meshes (2026-10-01): one per map chunk, one for the palette kit. The
    /// project serializes assets as text, which writes every vertex out as YAML (one forest chunk was
    /// 19 MB); meshes added to this asset are saved binary with it, so the baked maps fit in git (LFS).
    /// It holds nothing itself; the meshes are its sub-assets.
    /// </summary>
    [PreferBinarySerialization]
    public sealed class BakedMeshStore : ScriptableObject { }
}
