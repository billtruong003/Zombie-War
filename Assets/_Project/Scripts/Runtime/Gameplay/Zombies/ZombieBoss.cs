using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // The Kaiju (Level 2 boss): a heavy melee zombie that periodically slams the ground for AoE
    // damage when the player is close. High HP comes from its ZombieData; the slam is the one bit of
    // behaviour it adds on top of the shared FSM.
    public sealed class ZombieBoss : ZombieBase
    {
        [SerializeField] private float specialCooldown = 6f;
        [SerializeField] private float specialRadius = 4f;
        [SerializeField] private float specialDamageMultiplier = 2f;
        [SerializeField] private string slamClip = "Special";
        [SerializeField] private string slamVfxKey = "";

        private float _specialTimer;

        protected override void OnSpawned() => _specialTimer = specialCooldown;

        protected override void OnFullTick(Transform player, float distance)
        {
            _specialTimer -= Time.deltaTime;
            if (_specialTimer > 0f || distance > specialRadius) return;

            _specialTimer = specialCooldown;
            Vat.CrossFade(slamClip, 0.1f);
            if (!string.IsNullOrEmpty(slamVfxKey))
                Bill.Pool?.Spawn(slamVfxKey, transform.position, Quaternion.identity);

            player.GetComponentInParent<IDamageable>()?.TakeDamage(Data.damage * specialDamageMultiplier);
        }

        protected override void PerformAttack(Transform target) => DealContactDamage(target);
    }
}
