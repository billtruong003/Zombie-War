using System.Collections;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// A boss that adds a straight-line charge to the inherited ground slam.
    ///
    /// The fantasy: a heavyweight that punishes standing still at mid range. It rears up, then
    /// barrels along a locked line - the player sidesteps rather than outruns it. Because it damages
    /// continuously along the path (once per charge, tracked by <see cref="_hitThisCharge"/>) it also
    /// clears anyone hugging the boss.
    ///
    /// It inherits <see cref="ZombieBoss"/>, so the slam it already had still fires when the player
    /// is inside slam range; the charge is what covers the mid range the slam cannot reach.
    /// </summary>
    public sealed class ZombieCharger : ZombieBoss
    {
        private enum Phase { None, Telegraph, Dashing, Recover }

        [Header("Charge")]
        [SerializeField] private float chargeCooldown = 8f;
        [Tooltip("Charge only from mid range - inside this the inherited slam is the better answer.")]
        [SerializeField] private float chargeMinRange = 5f;
        [SerializeField] private float chargeMaxRange = 16f;
        [Tooltip("Rear-up window. Long on purpose: this is a boss, it must be readable.")]
        [SerializeField] private float telegraphDuration = 0.8f;
        [SerializeField] private float chargeSpeed = 14f;
        [SerializeField] private float chargeDuration = 1.1f;
        [SerializeField] private float chargeWidth = 2.4f;
        [SerializeField] private float chargeDamageMultiplier = 1.8f;
        [Tooltip("Stationary window after the charge - the boss's main punish opportunity.")]
        [SerializeField] private float recoverDuration = 1.2f;

        private Phase _phase = Phase.None;
        private float _cooldownTimer;
        private bool _hitThisCharge;
        private Coroutine _charge;

        protected override bool SuppressBaseFsm => _phase != Phase.None;

        protected override void OnSpawned()
        {
            base.OnSpawned();
            StopCharge();
            _phase = Phase.None;
            _cooldownTimer = chargeCooldown * 0.5f;
        }

        protected override void OnDespawned() => StopCharge();

        protected override void OnFullTick(Transform player, float distance)
        {
            if (_phase != Phase.None)
                return;   // mid-charge: the inherited slam must not fire on top of it

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f && !string.IsNullOrEmpty(Data.specialClip)
                && distance >= chargeMinRange && distance <= chargeMaxRange)
            {
                _charge = StartCoroutine(Charge(player));
                return;
            }

            base.OnFullTick(player, distance);   // otherwise the normal boss slam logic
        }

        private IEnumerator Charge(Transform player)
        {
            _cooldownTimer = chargeCooldown;
            _hitThisCharge = false;
            CancelPendingAttack();

            // ---- telegraph: lock the line the boss will travel ----------------------------
            _phase = Phase.Telegraph;
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = true;
            Vat.CrossFade(Data.specialClip, 0.1f);

            Vector3 dir = FlattenY(player.position - transform.position).normalized;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);

            float windup = Data.specialWindup > 0f ? Data.specialWindup : telegraphDuration;
            yield return new WaitForSeconds(windup);
            if (CurrentState == State.Dead) { _phase = Phase.None; yield break; }

            // ---- dash: fixed direction, damage once to anyone caught on the line -----------
            _phase = Phase.Dashing;
            float t = 0f;
            while (t < chargeDuration)
            {
                t += Time.deltaTime;
                if (CurrentState == State.Dead) break;

                if (Agent.enabled && Agent.isOnNavMesh)
                    Agent.Move(dir * chargeSpeed * Time.deltaTime);

                // One hit per charge, no matter how many frames the player stays in the path.
                if (!_hitThisCharge)
                {
                    var p = PlayerMovement.Instance;
                    if (p != null &&
                        Vector3.Distance(FlattenY(p.transform.position), FlattenY(transform.position)) <= chargeWidth)
                    {
                        _hitThisCharge = true;
                        DealAreaDamage(transform.position, chargeWidth, Data.damage * chargeDamageMultiplier);
                    }
                }
                yield return null;
            }

            // ---- recover -------------------------------------------------------------------
            _phase = Phase.Recover;
            Vat.CrossFade(Data.idleClip, 0.15f);
            yield return new WaitForSeconds(recoverDuration);

            _phase = Phase.None;
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = false;
            _charge = null;
        }

        private void StopCharge()
        {
            if (_charge != null) { StopCoroutine(_charge); _charge = null; }
        }
    }
}
