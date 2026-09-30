using UnityEngine;

namespace ZombieWar
{
    /// Put on a prop (or any parent of its collider) so bullets that hit it answer in its material.
    [DisallowMultipleComponent]
    public sealed class SurfaceMaterial : MonoBehaviour
    {
        public SurfaceKind kind = SurfaceKind.Stone;
    }
}
