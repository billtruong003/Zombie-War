#if PHOTON_FUSION
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace BillGameCore
{
    /// <summary>
    /// Fusion object provider that recycles despawned instances of <see cref="NetworkPoolable"/> prefabs instead
    /// of destroying them — killing the Instantiate/Destroy GC churn (and the "spent projectile never freed" leak)
    /// for high-frequency spawns. On despawn the instance is deactivated and pushed to a per-prefab free stack;
    /// the next spawn of that prefab reactivates one instead of allocating. Non-pooled prefabs and scene objects
    /// use the default path unchanged.
    ///
    /// Pooled prefabs MUST reset their own per-life plain (non-[Networked]) state in <c>Spawned()</c> — a reused
    /// instance keeps stale fields from its previous life ([Networked] state is reset by Fusion, plain fields are not).
    /// </summary>
    public class PooledNetworkObjectProvider : NetworkObjectProviderDefault
    {
        private readonly Dictionary<NetworkObject, Stack<NetworkObject>> _free = new();
        private readonly Dictionary<NetworkObject, NetworkObject> _instanceToPrefab = new();

        protected override NetworkObject InstantiatePrefab(NetworkRunner runner, NetworkObject prefab)
        {
            if (prefab.GetComponent<NetworkPoolable>() == null)
                return base.InstantiatePrefab(runner, prefab); // not opted in — default behaviour

            if (_free.TryGetValue(prefab, out var stack) && stack.Count > 0)
            {
                NetworkObject reused = stack.Pop();
                if (reused != null)
                {
                    reused.gameObject.SetActive(true);
                    return reused;
                }
            }

            NetworkObject inst = Instantiate(prefab);
            _instanceToPrefab[inst] = prefab;
            return inst;
        }

        protected override void DestroyPrefabInstance(NetworkRunner runner, NetworkPrefabId prefabId, NetworkObject instance)
        {
            if (instance != null && _instanceToPrefab.TryGetValue(instance, out NetworkObject prefab))
            {
                instance.gameObject.SetActive(false);
                if (!_free.TryGetValue(prefab, out var stack))
                {
                    stack = new Stack<NetworkObject>();
                    _free[prefab] = stack;
                }
                stack.Push(instance);
                return;
            }
            base.DestroyPrefabInstance(runner, prefabId, instance); // not from pool
        }

        /// <summary>Diagnostics: total pooled (free) instances across all prefabs.</summary>
        public int FreeCount
        {
            get
            {
                int n = 0;
                foreach (var kv in _free) n += kv.Value.Count;
                return n;
            }
        }
    }
}
#endif
