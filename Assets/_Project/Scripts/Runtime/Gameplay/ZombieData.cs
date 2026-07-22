using UnityEngine;

namespace ZombieWar
{
    /// <summary>Broad gameplay role, used for wave authoring, Pass missions and reward tiers.
    /// This is the behaviour family - not the concrete component - so several species share one.</summary>
    public enum ZombieArchetype
    {
        Walker,
        Runner,
        Ranged,
        Burrower,
        Heavy,
        Boss
    }

    [CreateAssetMenu(menuName = "ZombieWar/Zombie Data", fileName = "ZD_")]
    public class ZombieData : ScriptableObject
    {
        [Tooltip("Stable identity used by waves, save data and Pass missions. e.g. enemy.cute.dog_pup")]
        public string enemyId = "";
        public string zombieName = "Zombie";
        public GameObject prefab;
        public VAT_AnimationData vatData;

        [Header("Role")]
        public ZombieArchetype archetype = ZombieArchetype.Walker;
        [Tooltip("Elites and bosses count toward milestone missions and richer reward drops.")]
        public bool isElite;

        [Header("Stats")]
        public float maxHealth = 100f;
        public float damage = 10f;
        public float moveSpeed = 3.5f;
        public float attackRange = 1.5f;
        public float attackCooldown = 1.2f;

        [Header("Attack timing")]
        [Tooltip("Seconds from the attack clip starting to the damage/projectile actually landing. " +
                 "VAT has no Mecanim events, so the hit is scheduled off this measured wind-up.")]
        public float attackWindup = 0.3f;

        [Header("Rewards")]
        public int coinReward = 1;
        public int xpReward = 1;

        [Header("VAT clip names (must match names baked into vatData)")]
        public string idleClip = "Idle";
        public string moveClip = "Move";
        public string attackClip = "Attack";
        public string hitClip = "Hit";
        public string deathClip = "Death";

        [Header("Optional special clips (empty = ability unavailable)")]
        [Tooltip("Runner/Pouncer lunge, boss dash or heavy slam - played instead of attackClip.")]
        public string specialClip = "";
        public float specialWindup = 0.35f;
        [Tooltip("Burrowers: idle -> underground, looping underground, underground -> idle.")]
        public string burrowInClip = "";
        public string burrowLoopClip = "";
        public string burrowOutClip = "";
    }
}
