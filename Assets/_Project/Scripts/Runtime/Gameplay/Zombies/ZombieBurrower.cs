using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieWar
{
    /// <summary>
    /// The digger. Instead of walking at the player it repeatedly dives underground, travels there
    /// fast and untouchable, then bursts out next to them.
    ///
    /// The fantasy: you cannot kite it and you cannot shoot it while it is moving - the counterplay
    /// is reading the emerge telegraph and stepping off the mound before it erupts.
    ///
    /// Cycle: Surface -> Diving -> Underground -> Emerging -> Surface
    ///   * Surface    - ordinary ZombieBase chase/attack, fully vulnerable. Dives again on a timer.
    ///   * Diving     - plays the burrow-in clip; already untargetable so it cannot be shot mid-dive.
    ///   * Underground- invulnerable, hidden, moves at a multiple of its normal speed toward a point
    ///                  near the player. This is the only phase that closes real distance.
    ///   * Emerging   - surfaces at a spot held at least emergeMinDistance away, telegraphs for
    ///                  emergeTelegraph seconds (mound visible, still untouchable), then erupts for
    ///                  area damage and returns to the surface state.
    ///
    /// Safety rules this honours: it never surfaces on top of the player (emergeMinDistance), the
    /// eruption is always telegraphed, and the whole cycle is abandoned on death or pool return.
    /// Cheap/Inactive tiers never run it because the base Update gates on Full tier.
    /// </summary>
    public class ZombieBurrower : ZombieBase
    {
        private enum Phase { Surface, Diving, Underground, Emerging }

        [Header("Dig cycle")]
        [Tooltip("Seconds on the surface before it dives again.")]
        [SerializeField] private float surfaceDuration = 6f;
        [Tooltip("Won't bother diving if the player is already this close - it just attacks.")]
        [SerializeField] private float minDiveDistance = 6f;
        [SerializeField] private float undergroundSpeedMultiplier = 2.4f;
        [Tooltip("Safety valve: never stay under longer than this even if the path is blocked.")]
        [SerializeField] private float maxUndergroundDuration = 5f;

        [Header("Emerge")]
        [Tooltip("Closest it may surface to the player. Prevents an unreactable spawn-on-top.")]
        [SerializeField] private float emergeMinDistance = 2.5f;
        [Tooltip("Warning window between arriving and erupting. This is the player's dodge window.")]
        [SerializeField] private float emergeTelegraph = 0.7f;
        [SerializeField] private float emergeRadius = 2.2f;
        [SerializeField] private float emergeDamageMultiplier = 1.4f;

        private Phase _phase = Phase.Surface;
        private float _phaseTimer;
        private Coroutine _cycle;

        // Underground and mid-dive it is neither shootable nor auto-aim-able. Emerging stays
        // untargetable so the telegraph reads as "not yet here" rather than a free damage window.
        protected override bool CanBeTargeted => base.CanBeTargeted && _phase == Phase.Surface;
        protected override bool IsInvulnerable => _phase != Phase.Surface;
        protected override bool SuppressBaseFsm => _phase != Phase.Surface;

        protected override void OnSpawned()
        {
            // Pooled reuse must always come back up top, visible and solid.
            StopCycle();
            _phase = Phase.Surface;
            _phaseTimer = surfaceDuration;
            SetHidden(false);
        }

        protected override void OnDespawned() => StopCycle();

        protected override void PerformAttack(Transform target) => DealContactDamage(target);

        protected override void OnFullTick(Transform player, float distance)
        {
            if (_phase != Phase.Surface) return;

            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;
            // Pointless to dive when already in melee - it would just give the player a free window.
            if (distance < minDiveDistance) { _phaseTimer = 1f; return; }

            _cycle = StartCoroutine(DigCycle());
        }

        private IEnumerator DigCycle()
        {
            // ---- dive ---------------------------------------------------------------------
            _phase = Phase.Diving;
            CancelPendingAttack();
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = true;
            yield return PlayAndWait(Data.burrowInClip, 0.6f);

            // ---- underground --------------------------------------------------------------
            _phase = Phase.Underground;
            SetHidden(true);
            if (!string.IsNullOrEmpty(Data.burrowLoopClip)) Vat.Play(Data.burrowLoopClip);

            float speed = Data.moveSpeed * undergroundSpeedMultiplier;
            if (Agent.enabled) { Agent.isStopped = false; Agent.speed = speed; }

            float elapsed = 0f;
            while (elapsed < maxUndergroundDuration)
            {
                elapsed += Time.deltaTime;
                var player = PlayerMovement.Instance;
                if (player == null) break;

                Vector3 spot = EmergeSpot(player.transform.position);
                if (Agent.enabled && Agent.isOnNavMesh) Agent.SetDestination(spot);

                if (Vector3.Distance(FlattenY(transform.position), FlattenY(spot)) <= 0.6f) break;
                yield return null;
            }

            // ---- emerge -------------------------------------------------------------------
            _phase = Phase.Emerging;
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = true;
            Agent.speed = Data.moveSpeed;

            // Telegraph: back on screen and clearly about to erupt, but still not damageable, so the
            // player reads the mound and moves rather than trading shots with an invulnerable target.
            SetHidden(false);
            if (!string.IsNullOrEmpty(Data.burrowOutClip)) Vat.Play(Data.burrowOutClip);
            yield return new WaitForSeconds(Mathf.Max(0f, emergeTelegraph));

            DealAreaDamage(transform.position, emergeRadius, Data.damage * emergeDamageMultiplier);

            // ---- back to normal -----------------------------------------------------------
            _phase = Phase.Surface;
            _phaseTimer = surfaceDuration;
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = false;
            Vat.CrossFade(Data.idleClip, 0.15f);
            _cycle = null;
        }

        /// <summary>A point near the player but never closer than <see cref="emergeMinDistance"/>,
        /// snapped onto the NavMesh so it cannot surface inside geometry.</summary>
        private Vector3 EmergeSpot(Vector3 playerPos)
        {
            Vector3 away = FlattenY(transform.position - playerPos);
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            Vector3 wanted = playerPos + away.normalized * emergeMinDistance;

            return NavMesh.SamplePosition(wanted, out var hit, 3f, NavMesh.AllAreas)
                ? hit.position
                : wanted;
        }

        private IEnumerator PlayAndWait(string clip, float fallbackDuration)
        {
            if (string.IsNullOrEmpty(clip)) yield break;
            Vat.Play(clip);

            float duration = fallbackDuration;
            if (Vat.animationData != null && Vat.animationData.TryGetClipInfo(clip, out var info) && info.duration > 0f)
                duration = info.duration;
            yield return new WaitForSeconds(duration);
        }

        /// <summary>Underground means literally not rendered - cheaper than sinking the mesh and it
        /// cannot clip through the ground plane.</summary>
        private void SetHidden(bool hidden)
        {
            // Body AND blob shadow together - a shadow left on the ground would give away a
            // creature that is supposed to be invisible underground.
            SetVisible(!hidden);
            if (TryGetComponent(out Collider col)) col.enabled = !hidden;
        }

        private void StopCycle()
        {
            if (_cycle != null) { StopCoroutine(_cycle); _cycle = null; }
        }
    }
}
