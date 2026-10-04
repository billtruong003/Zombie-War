using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// ZombieBase, part: the per-frame state machine, chase and attacks.
    public abstract partial class ZombieBase
    {
        private void Update()
        {
            // The first enemy to update each frame advances every flash and react (no manager needed,
            // so sandbox scenes without a ZombieManager behave the same).
            if (_visualsTickedFrame != Time.frameCount) { _visualsTickedFrame = Time.frameCount; TickTimedVisuals(Time.time); }
            if (_tier != ZombieTier.Full || _state == State.Dead) return;

            var player = PlayerMovement.Instance;
            if (player == null) return;

            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
                // The slot is held for the whole cooldown, not just the windup. Releasing it at the
                // moment of impact (the M7.4b first cut) bounded how many enemies could be MID-SWING
                // while leaving the attack RATE unbounded: a 0.3 s windup against a 1.2 s cooldown
                // recycled each of the 4 slots four times per cooldown, so 18 crowding enemies still
                // landed ~15 hits/s. Measured 72 DPS capped vs 63 DPS uncapped — the cap did nothing.
                // Holding for the cooldown makes the ceiling cap/cooldown swings per second, which is
                // the quantity the player actually feels.
                if (_attackCooldownTimer <= 0f) ReleaseAttackSlotIfHeld();
            }

            float distance = Vector3.Distance(transform.position, player.transform.position);

            // A subtype running its own movement phase (a burrower underground, a boss mid-dash)
            // still gets its per-frame tick, but the shared chase/attack FSM stands down so the two
            // cannot fight over the agent's destination.
            if (SuppressBaseFsm)
            {
                OnFullTick(player.transform, distance);
                return;
            }

            // Hysteresis: enter Attack at EngageRange, but only fall back to Chase once the target
            // is meaningfully beyond it. A single shared threshold thrashed Chase<->Attack every few
            // frames when crowd separation jostled an attacker across the boundary (M5.1 CP2),
            // spamming move-clip crossfades and resetting swings.
            State desired = _state == State.Attack
                ? (distance > EngageRange * EngageExitFactor ? State.Chase : State.Attack)
                : (distance <= EngageRange ? State.Attack : State.Chase);
            SwitchState(desired);

            if (_state == State.Chase) Chase(player.transform);
            else FaceAndAttack(player.transform);

            OnFullTick(player.transform, distance);
        }

        // Extra per-frame behaviour for Full-tier zombies (e.g. a boss's special-attack timer).
        // Runs after the base FSM so overrides can rely on the current state being resolved.
        protected virtual void OnFullTick(Transform player, float distance) { }

        private void SwitchState(State next)
        {
            if (_state == next) return;
            _state = next;

            switch (_state)
            {
                case State.Chase:
                    _vatAnimator.CrossFade(data.moveClip, stateCrossFadeDuration, _locomotionPhase);
                    _motor.IsStopped = false;
                    break;
                case State.Attack:
                    _motor.IsStopped = true;
                    // Standing between swings must not keep playing the move clip (moonwalk-in-place).
                    // The swing itself crossfades the attack clip when it actually starts.
                    if (_attackRoutine == null)
                        _vatAnimator.CrossFade(data.idleClip, stateCrossFadeDuration, _locomotionPhase);
                    break;
            }
        }

        // Default approach: steer straight at the player. Runners override to lunge;
        // ranged types override to hold their distance.
        protected virtual void Chase(Transform target)
        {
            if (_motor.enabled) _motor.SetDestination(target.position);
        }

        private void FaceAndAttack(Transform target)
        {
            // Rate-limited turn toward the target at the motor's authored turn speed. The old
            // instant LookRotation snap teleported facing every frame against a moving player, and
            // fought the motor's own velocity-facing in the same frame (M5.1 CP2 - the motor now
            // yields facing entirely while stopped, making this the single writer in Attack).
            Vector3 to = FlattenY(target.position - transform.position);
            if (to.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(to), _motor.TurnSpeed * Time.deltaTime);

            if (_attackCooldownTimer > 0f || _attackRoutine != null) return;

            // M7.4b — the attacker cap. Only N enemies may swing at once; the rest keep crowding and
            // facing the player (this method has already turned them to face), so the horde still
            // reads as a horde and the player can see who is actually committing.
            //
            // Bosses and elites are never denied a slot.
            if (!ZombieManager.TryClaimAttackSlot(this))
            {
                _waitingForAttackSlot = true;
                return;
            }
            _waitingForAttackSlot = false;
            _holdsAttackSlot = true;

            _attackCooldownTimer = data.attackCooldown;
            _vatAnimator.CrossFade(data.attackClip, stateCrossFadeDuration);
            PlayAttackAudio();
            _attackRoutine = StartCoroutine(AttackAfterWindup(target));
        }

        /// <param name="priority">
        /// Medium for an ordinary swing. Boss slams, charges and emerges pass High: they are rare,
        /// telegraphed and lethal, so they must not lose their voice to the fortieth walker's attack.
        /// Defaulted so the existing call sites keep working unchanged.
        /// </param>
        protected void PlayAttackAudio(SfxPriority priority = SfxPriority.Medium)
        {
            if (string.IsNullOrEmpty(data.attackSfxKey) || Time.time < _nextAttackVocalTime) return;
            // A High-priority special is worth interrupting the shared vocal cooldown for.
            if (priority < SfxPriority.High) _nextAttackVocalTime = Time.time + 0.12f;
            Bill.Audio?.PlayCue(data.attackSfxKey, transform.position, priority, 0.72f);
        }

        /// <summary>
        /// Lands the hit on the animation's actual contact frame instead of the instant the clip
        /// starts. VAT has no Mecanim events, so the delay comes from the authored
        /// <see cref="ZombieData.attackWindup"/> measured off the real clip.
        ///
        /// Exactly one hit per swing: the routine handle doubles as the "already swinging" guard in
        /// <see cref="FaceAndAttack"/>, and death or a pool return cancels it before it can land.
        /// </summary>
        private IEnumerator AttackAfterWindup(Transform target)
        {
            float windup = Mathf.Max(0f, data.attackWindup);
            if (windup > 0f) yield return new WaitForSeconds(windup);

            if (_state == State.Dead || target == null) { _attackRoutine = null; yield break; }

            PerformAttack(target);
            // NOT released here — see the cooldown tick in Update. Handing the slot back on impact
            // is what made the cap cosmetic.

            // Follow-through: hold the swing until the attack clip has played out once, then hand
            // the (looping) VAT back to idle. Without this the baked attack clip replayed phantom
            // swings for the whole cooldown, and - because VAT_Animator.CrossFade ignores a fade to
            // the clip already playing - the NEXT real swing never visually restarted (M5.1 CP3).
            float clipDuration = 0.6f;
            if (_vatAnimator.animationData != null &&
                _vatAnimator.animationData.TryGetClipInfo(data.attackClip, out var clip) &&
                clip.duration > 0f)
                clipDuration = clip.duration;

            float followThrough = Mathf.Max(0f, clipDuration - windup);
            if (followThrough > 0f) yield return new WaitForSeconds(followThrough);

            _attackRoutine = null;
            if (_state == State.Dead) yield break;
            if (_state == State.Attack && !_reacting)
                _vatAnimator.CrossFade(data.idleClip, stateCrossFadeDuration);
        }

        /// <summary>Cancels an in-flight swing so a dying or despawning zombie cannot still deal its
        /// damage a few frames later.</summary>
        /// <summary>Returns this enemy's attack slot exactly once, whatever path it exits by.</summary>
        private void ReleaseAttackSlotIfHeld()
        {
            if (!_holdsAttackSlot) return;
            _holdsAttackSlot = false;
            ZombieManager.ReleaseAttackSlot(this);
        }

        protected void CancelPendingAttack()
        {
            if (_attackRoutine == null) return;
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        /// <summary>Shared AoE helper for slams, dashes and emerges. Uses a non-allocating overlap
        /// query and damages the player once - enemies are never friendly-fire targets here.</summary>
        protected void DealAreaDamage(Vector3 center, float radius, float damage)
        {
            var player = PlayerMovement.Instance;
            if (player == null) return;
            if (Vector3.Distance(FlattenY(player.transform.position), FlattenY(center)) > radius) return;
            player.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
        }

        // The actual hit. Melee deals contact damage; ranged spawns a projectile; boss adds AoE.
        protected abstract void PerformAttack(Transform target);

        // Shared helper for melee-style subtypes.
        protected void DealContactDamage(Transform target)
        {
            target.GetComponentInParent<IDamageable>()?.TakeDamage(Damage);
        }
    }
}
