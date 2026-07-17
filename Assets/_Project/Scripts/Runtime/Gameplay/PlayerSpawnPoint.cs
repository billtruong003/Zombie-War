using UnityEngine;

namespace ZombieWar
{
    // Placed in each map scene as the authored spawn location. The player is NOT baked into the map;
    // PlayerSpawner instantiates the player prefab here at runtime, so any map can be authored just
    // by dropping one of these in the scene (and later, multiple for multi-spawn / respawn).
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private bool isPrimary = true;
        public bool IsPrimary => isPrimary;

        private void OnDrawGizmos()
        {
            Gizmos.color = isPrimary ? new Color(0.2f, 1f, 0.4f, 0.9f) : new Color(1f, 0.8f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
    }
}
