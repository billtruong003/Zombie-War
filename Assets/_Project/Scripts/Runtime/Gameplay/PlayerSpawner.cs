using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // Spawns the player prefab into the map at a PlayerSpawnPoint at runtime, instead of the player
    // being baked into the map scene. Keeps maps authorable as pure environment + spawn markers, and
    // makes future multi-map / respawn trivial (just add spawn points).
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private bool spawnOnStart = true;

        [Header("Runtime wiring (persistent objects the spawned player needs)")]
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private VirtualJoystick joystick;

        public GameObject Current { get; private set; }

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        public GameObject Spawn()
        {
            if (Current != null) return Current;

            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] No player prefab assigned.");
                return null;
            }

            PlayerSpawnPoint point = ResolveSpawnPoint();
            Vector3 pos = point != null ? point.transform.position : transform.position;
            Quaternion rot = point != null ? point.transform.rotation : transform.rotation;

            Current = Instantiate(playerPrefab, pos, rot);

            // Wire the persistent scene objects into the freshly spawned player.
            if (cameraFollow != null) cameraFollow.Target = Current.transform;

            var movement = Current.GetComponent<PlayerMovement>();
            if (movement != null && joystick != null) movement.SetJoystick(joystick);

            // Loadout chon o menu -> ghi vao Weapon slots truoc khi vao tran.
            var weapon = Current.GetComponentInChildren<Weapon>();
            if (weapon != null) LoadoutState.ApplyTo(weapon);

            // Costume chon o menu -> ap modular parts len skeleton player.
            var costume = Current.GetComponentInChildren<CharacterModularApplier>();
            if (costume != null) costume.ApplySavedParts();

            return Current;
        }

        private static PlayerSpawnPoint ResolveSpawnPoint()
        {
            var points = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (points == null || points.Length == 0) return null;

            foreach (var p in points)
                if (p.IsPrimary) return p;

            return points[0];
        }
    }
}
