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

        private float _cooldownTimer;
        private int _bombsRemaining;

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

            Vector3 spawnPosition = throwOrigin.position + aimDirection * throwDistance;
            Instantiate(bombPrefab, spawnPosition, Quaternion.identity);

            _bombsRemaining--;
            _cooldownTimer = cooldown;
        }
    }
}
