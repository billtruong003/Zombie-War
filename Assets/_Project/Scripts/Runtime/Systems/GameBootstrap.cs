using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// <summary>
    /// Scene-level entry point for a gameplay scene.
    ///
    /// BillGameCore auto-boots its service layer (see Bill.Boot, a RuntimeInitializeOnLoadMethod)
    /// and lands in <see cref="BootState"/>; the game never re-implements that. This component is
    /// the one game-specific hook the framework can't know about: it drives the state machine into
    /// <see cref="GameplayState"/> and optionally pre-warms explicitly-keyed pools (zombies, bombs)
    /// so the first wave spawns without instantiation hitches.
    ///
    /// Pools are optional here - FxPool and the spawners lazy-register on first use, so an empty
    /// list is still correct; warming just front-loads the allocation cost.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [System.Serializable]
        public struct PoolWarmup
        {
            public string key;
            public GameObject prefab;
            [Min(0)] public int warmCount;
        }

        [Tooltip("Pools to register + pre-warm on start. Keys must match what the spawners request.")]
        [SerializeField] private PoolWarmup[] prewarmPools;

        [SerializeField] private bool enterGameplayStateOnStart = true;

        private void Start()
        {
            var pool = Bill.Pool;
            if (pool != null && prewarmPools != null)
            {
                foreach (var warmup in prewarmPools)
                {
                    if (warmup.prefab == null || string.IsNullOrEmpty(warmup.key)) continue;
                    pool.Register(warmup.key, warmup.prefab, Mathf.Max(0, warmup.warmCount));
                }
            }

            if (enterGameplayStateOnStart)
                Bill.State?.GoTo<GameplayState>();
        }
    }
}
