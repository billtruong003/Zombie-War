using System.Collections;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// <summary>
    /// A heavyweight that periodically slams the ground for area damage when the player is close.
    /// High HP comes from its ZombieData; the slam is the one bit of behaviour it adds on top of the
    /// shared FSM.
    ///
    /// The slam is telegraphed rather than instant: it plays the wind-up, holds for the authored
    /// window, and only then applies damage. That gives the player a readable "step out of the
    /// circle" beat instead of losing health on the frame the animation starts.
    ///
    /// Base class for ZombieCharger, which keeps this slam for close range and adds a dash for mid.
    /// </summary>
    public class ZombieBoss : ZombieBase
    {
        [Header("Slam")]
        [SerializeField] private float specialCooldown = 6f;
        [SerializeField] private float specialRadius = 4f;
        [SerializeField] private float specialDamageMultiplier = 2f;
        [Tooltip("Leave empty to use ZombieData.specialClip, which the baker fills from the real bake.")]
        [SerializeField] private string slamClip = "";
        [SerializeField] private string slamVfxKey = "";

        private float _specialTimer;
        private Coroutine _slam;

        protected override bool SuppressBaseFsm => _slam != null;

        protected override void OnSpawned()
        {
            StopSlam();
            _specialTimer = specialCooldown;
        }

        protected override void OnDespawned() => StopSlam();

        protected override void OnFullTick(Transform player, float distance)
        {
            if (_slam != null) return;

            _specialTimer -= Time.deltaTime;
            if (_specialTimer > 0f || distance > specialRadius) return;

            _slam = StartCoroutine(Slam());
        }

        private IEnumerator Slam()
        {
            _specialTimer = specialCooldown;
            CancelPendingAttack();
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = true;

            string clip = !string.IsNullOrEmpty(slamClip) ? slamClip : Data.specialClip;
            if (!string.IsNullOrEmpty(clip)) Vat.CrossFade(clip, 0.1f);

            // Wind-up before the shockwave, so the player can still get clear of it.
            float windup = Data.specialWindup > 0f ? Data.specialWindup : 0.4f;
            yield return new WaitForSeconds(windup);

            if (CurrentState != State.Dead)
            {
                if (!string.IsNullOrEmpty(slamVfxKey))
                    Bill.Pool?.Spawn(slamVfxKey, transform.position, Quaternion.identity);

                DealAreaDamage(transform.position, specialRadius, Data.damage * specialDamageMultiplier);
            }

            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = false;
            _slam = null;
        }

        private void StopSlam()
        {
            if (_slam != null) { StopCoroutine(_slam); _slam = null; }
        }

        protected override void PerformAttack(Transform target) => DealContactDamage(target);
    }
}
