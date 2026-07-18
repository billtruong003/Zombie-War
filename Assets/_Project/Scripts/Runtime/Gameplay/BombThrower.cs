using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    public class BombThrower : MonoBehaviour
    {
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private Transform throwOrigin;
        [Tooltip("Horizontal launch speed along aim; combined with throwUpSpeed makes the arc.")]
        [SerializeField] private float throwHorizontalSpeed = 9f;
        [Tooltip("Upward launch speed - higher = taller lob.")]
        [SerializeField] private float throwUpSpeed = 4.5f;
        [SerializeField] private float cooldown = 3f;
        [SerializeField] private int maxBombs = 3;

        [Header("Throw animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string throwAnimTrigger = "Throw";
        // Matches the wind-up before the release frame in whatever throw clip gets picked -
        // tune once the real clip is in (see Docs/EDITOR_SETUP_CHECKLIST.md).
        [SerializeField] private float releaseDelay = 0.3f;

        private float _cooldownTimer;
        private int _bombsRemaining;
        private Vector3 _pendingThrowDirection;

        public int BombsRemaining => _bombsRemaining;
        public int MaxBombs => maxBombs;
        public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);

        private void Awake()
        {
            _bombsRemaining = maxBombs;
        }

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
        }

        public void TryThrow(Vector3 aimDirection)
        {
            if (_cooldownTimer > 0f || _bombsRemaining <= 0 || bombPrefab == null) return;

            _bombsRemaining--;
            _cooldownTimer = cooldown;
            _pendingThrowDirection = aimDirection;

            if (animator != null) animator.SetTrigger(throwAnimTrigger);
            Invoke(nameof(ReleaseBomb), releaseDelay);
        }

        private void ReleaseBomb()
        {
            if (bombPrefab == null) return;

            // Spawn from the hand/origin and lob along the (flattened) aim so it arcs instead of
            // dropping straight down. Vertical component + gravity give the parabola; the bomb's
            // Rigidbody + bouncy PhysicMaterial handle the tumble/bounce on landing.
            Vector3 originPos = throwOrigin != null ? throwOrigin.position : transform.position;
            Vector3 flatDir = _pendingThrowDirection;
            flatDir.y = 0f;
            flatDir = flatDir.sqrMagnitude > 0.0001f ? flatDir.normalized : transform.forward;
            Vector3 launchVelocity = flatDir * throwHorizontalSpeed + Vector3.up * throwUpSpeed;

            GameObject spawned;
            var pool = Bill.Pool;
            if (pool == null)
            {
                spawned = Instantiate(bombPrefab, originPos, Quaternion.identity);
            }
            else
            {
                string key = "bomb_" + bombPrefab.GetInstanceID();
                pool.Register(key, bombPrefab);
                spawned = pool.Spawn(key, originPos, Quaternion.identity);
            }

            if (spawned != null && spawned.TryGetComponent(out Bomb bomb))
                bomb.Launch(launchVelocity);
        }
    }
}
