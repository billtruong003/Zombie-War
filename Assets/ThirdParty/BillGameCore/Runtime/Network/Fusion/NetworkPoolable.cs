#if PHOTON_FUSION
using UnityEngine;

namespace BillGameCore
{
    /// <summary>
    /// Marker: put this on a networked prefab you want <see cref="PooledNetworkObjectProvider"/> to RECYCLE
    /// instead of Instantiate/Destroy. Only prefabs carrying it are pooled; everything else uses Fusion's
    /// default path. Keep the opt-in narrow (projectiles, transient FX) — do NOT pool players/scene objects.
    ///
    /// A pooled prefab MUST reset its own per-life plain (non-[Networked]) fields in <c>Spawned()</c>, since a
    /// reused instance keeps them from its previous life.
    /// </summary>
    public sealed class NetworkPoolable : MonoBehaviour { }
}
#endif
