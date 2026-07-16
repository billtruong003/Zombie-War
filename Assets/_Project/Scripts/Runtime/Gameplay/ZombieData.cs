using UnityEngine;

namespace ZombieWar
{
    [CreateAssetMenu(menuName = "ZombieWar/Zombie Data", fileName = "ZD_")]
    public class ZombieData : ScriptableObject
    {
        public string zombieName = "Zombie";
        public GameObject prefab;
        public VAT_AnimationData vatData;

        [Header("Stats")]
        public float maxHealth = 100f;
        public float damage = 10f;
        public float moveSpeed = 3.5f;
        public float attackRange = 1.5f;
        public float attackCooldown = 1.2f;

        [Header("VAT clip names (must match names baked into vatData)")]
        public string idleClip = "Idle";
        public string moveClip = "Move";
        public string attackClip = "Attack";
        public string hitClip = "Hit";
        public string deathClip = "Death";

        [Header("Boss (Level 2)")]
        public bool isBoss;
    }
}
