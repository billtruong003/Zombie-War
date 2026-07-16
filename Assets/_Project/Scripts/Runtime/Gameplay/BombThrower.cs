using UnityEngine;

namespace ZombieWar
{
    public class BombThrower : MonoBehaviour
    {
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private Transform throwOrigin;
        [SerializeField] private float throwDistance = 3f;
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
            Vector3 spawnPosition = throwOrigin.position + _pendingThrowDirection * throwDistance;
            Instantiate(bombPrefab, spawnPosition, Quaternion.identity);
        }
    }
}
