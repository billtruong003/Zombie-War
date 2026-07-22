using System.Collections;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// A runner that closes the last stretch with a leap instead of jogging into melee.
    ///
    /// The fantasy: a pack animal that commits. The pounce covers ground the player cannot outrun,
    /// but it is telegraphed and it ends in a recovery window where the beast is stationary and
    /// takes hits - so baiting a pounce and stepping aside is the counterplay.
    ///
    /// Sequence: Crouch (telegraph, stationary) -> Leap (fast, committed, damages on arrival)
    ///           -> Recover (stationary, vulnerable) -> cooldown.
    ///
    /// Motion is driven through NavMeshAgent.Move rather than a transform lerp, so a leap can never
    /// punch the creature through a wall or off the mesh.
    /// </summary>
    public sealed class ZombiePouncer : ZombieRunner
    {
        private enum Phase { None, Crouch, Leap, Recover }

        [Header("Pounce")]
        [SerializeField] private float pounceCooldown = 5f;
        [Tooltip("Band the player must be in to trigger a pounce - too close and it just bites.")]
        [SerializeField] private float pounceMinRange = 3f;
        [SerializeField] private float pounceMaxRange = 8f;
        [Tooltip("Stationary wind-up. This is the player's read on the leap.")]
        [SerializeField] private float crouchDuration = 0.35f;
        [SerializeField] private float leapSpeed = 12f;
        [SerializeField] private float leapDuration = 0.45f;
        [Tooltip("Stationary, fully vulnerable window after landing. The punish opportunity.")]
        [SerializeField] private float recoverDuration = 0.5f;
        [SerializeField] private float pounceRadius = 1.9f;
        [SerializeField] private float pounceDamageMultiplier = 1.5f;

        private Phase _phase = Phase.None;
        private float _cooldownTimer;
        private Coroutine _pounce;

        protected override bool SuppressBaseFsm => _phase != Phase.None;

        protected override void OnSpawned()
        {
            StopPounce();
            _phase = Phase.None;
            _cooldownTimer = pounceCooldown * 0.5f;   // don't let a fresh spawn pounce instantly
        }

        protected override void OnDespawned() => StopPounce();

        protected override void OnFullTick(Transform player, float distance)
        {
            if (_phase != Phase.None) return;

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer > 0f) return;
            if (string.IsNullOrEmpty(Data.specialClip)) return;
            if (distance < pounceMinRange || distance > pounceMaxRange) return;

            _pounce = StartCoroutine(Pounce(player));
        }

        private IEnumerator Pounce(Transform player)
        {
            _cooldownTimer = pounceCooldown;
            CancelPendingAttack();

            // ---- crouch: commit to a direction, then stop steering -------------------------
            _phase = Phase.Crouch;
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = true;
            Vat.CrossFade(Data.specialClip, 0.1f);

            Vector3 aim = FlattenY(player.position - transform.position).normalized;
            if (aim.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(aim);

            float windup = Data.specialWindup > 0f ? Data.specialWindup : crouchDuration;
            yield return new WaitForSeconds(windup);
            if (CurrentState == State.Dead) { _phase = Phase.None; yield break; }

            // ---- leap: committed to the direction chosen at crouch, no mid-air steering ----
            _phase = Phase.Leap;
            float t = 0f;
            while (t < leapDuration)
            {
                t += Time.deltaTime;
                if (CurrentState == State.Dead) break;
                if (Agent.enabled && Agent.isOnNavMesh)
                    Agent.Move(aim * leapSpeed * Time.deltaTime);
                yield return null;
            }

            if (CurrentState != State.Dead)
                DealAreaDamage(transform.position, pounceRadius, Data.damage * pounceDamageMultiplier);

            // ---- recover: the punish window ------------------------------------------------
            _phase = Phase.Recover;
            Vat.CrossFade(Data.idleClip, 0.12f);
            yield return new WaitForSeconds(recoverDuration);

            _phase = Phase.None;
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = false;
            _pounce = null;
        }

        private void StopPounce()
        {
            if (_pounce != null) { StopCoroutine(_pounce); _pounce = null; }
        }
    }
}
