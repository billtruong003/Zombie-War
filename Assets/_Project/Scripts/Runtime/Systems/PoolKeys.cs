using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Pool keys per prefab, built once. FX, tracers and zombie spawns used to concatenate
    /// "prefix" + instanceId on every play/shot/spawn - a fresh string each time in the hottest paths.
    /// </summary>
    public static class PoolKeys
    {
        static readonly Dictionary<(string prefix, int id), string> Map = new(128);

        public static string For(string prefix, Object prefab)
        {
            int id = prefab.GetInstanceID();
            if (!Map.TryGetValue((prefix, id), out var key)) Map[(prefix, id)] = key = prefix + id;
            return key;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Map.Clear();
    }
}
