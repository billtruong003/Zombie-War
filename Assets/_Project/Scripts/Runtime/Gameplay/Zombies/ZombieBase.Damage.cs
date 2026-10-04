using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// ZombieBase, part: taking damage, hit reactions, pushes, death and dissolve.
    public abstract partial class ZombieBase
    {
        public void TakeDamage(float amount)
        {
            if (IsInvulnerable) return;
            _health.TakeDamage(amount);
        }

        bool _nextHitCrit;
        const float NumberWindow = 0.12f;
        float _pendingNumber, _nextNumberAt;

        void FlushDamageNumber()
        {
            if (_pendingNumber <= 0f) return;
            DamageNumberSpawner.Spawn(_pendingNumber, transform.position + Vector3.up * damageNumberHeight, _nextHitCrit);
            _pendingNumber = 0f;
            _nextHitCrit = false;
            _nextNumberAt = Time.time + NumberWindow;
        }

        /// <summary>Slot in <see cref="ZombieManager.Alive"/> (-1 = not registered).</summary>
        internal int RegistryIndex = -1;

        /// <summary>The next damage number is a crit (gold). Set by the gun right before the hit lands.</summary>
        public void MarkNextHitCrit() => _nextHitCrit = true;

        private void HandleDamaged(float amount)
        {
            if (_state == State.Dead) return;

            if (!string.IsNullOrEmpty(data.impactSfxKey) && Time.time >= _nextImpactAudioTime)
            {
                _nextImpactAudioTime = Time.time + 0.045f;
                Bill.Audio?.PlayCue(data.impactSfxKey, transform.position, SfxPriority.Medium, 0.62f);
            }

            if (!string.IsNullOrEmpty(data.hurtSfxKey)
                && Time.time >= _nextHurtAudioTime
                && Time.time >= _nextHurtVocalTime
                && Random.value <= 0.3f)
            {
                _nextHurtAudioTime = Time.time + 0.5f;
                _nextHurtVocalTime = Time.time + 0.09f;
                // Low: a hurt vocal is the first thing that should lose its voice when the horde is loud.
                Bill.Audio?.PlayCue(data.hurtSfxKey, transform.position, SfxPriority.Low, 0.58f);
            }

            // Floating damage number at chest height. Covers every source (guns, bomb, contact)
            // since it hangs off Health.OnDamaged rather than any single weapon.
            // One number per enemy per NumberWindow: rapid fire and damage-over-time ticks add up into
            // it instead of each spawning a popup. A crit always shows at once (it is the gold one).
            _pendingNumber += amount;
            if (_nextHitCrit || Time.time >= _nextNumberAt) FlushDamageNumber();

            // The flash restarts on EVERY hit (unlike the react anim below) - that per-bullet
            // response is the whole point of it, and it's a shader value so it costs nothing.
            _flashing = true;
            _flashEndsAt = Time.time + hitFlashDuration;
            SetHitFlash(1f);
            if (!_flashListed) { _flashListed = true; Flashing.Add(this); }

            // One-shot hit react: while the hit anim is still playing, further bullets only
            // deal damage/spawn numbers - they do NOT restart the anim or re-knockback, so
            // rapid fire can't lock the zombie into a looping flinch.
            if (_reacting) return;
            StartHitReact();
            ApplyGenericKnockback();
        }

        private void StartHitReact()
        {
            _vatAnimator.Play(data.hitClip);
            float duration = 0.4f;
            if (_vatAnimator.animationData != null &&
                _vatAnimator.animationData.TryGetClipInfo(data.hitClip, out var clip) &&
                clip.duration > 0f)
                duration = clip.duration;
            _reacting = true;
            _reactEndsAt = Time.time + duration;
            if (!_reactListed) { _reactListed = true; Reacting.Add(this); }
        }

        /// <summary>Advances every running hit flash and hit react. Scaled time: a paused game
        /// freezes them with everything else. Called once a frame, by the first enemy to update.</summary>
        internal static void TickTimedVisuals(float now)
        {
            for (int i = Flashing.Count - 1; i >= 0; i--)
            {
                var z = Flashing[i];
                if (z != null && z._flashing)
                {
                    float left = z._flashEndsAt - now;
                    if (left > 0f) { z.SetHitFlash(Mathf.Clamp01(left / Mathf.Max(0.0001f, z.hitFlashDuration))); continue; }
                    z.SetHitFlash(0f);
                    z._flashing = false;
                }
                if (z != null) z._flashListed = false;
                Flashing[i] = Flashing[Flashing.Count - 1];
                Flashing.RemoveAt(Flashing.Count - 1);
            }
            for (int i = Reacting.Count - 1; i >= 0; i--)
            {
                var z = Reacting[i];
                if (z != null && z._reacting)
                {
                    if (now < z._reactEndsAt) continue;
                    z._reacting = false;
                    // Hit clips are baked looping like everything else in the VAT, so once the react
                    // window ends the animator goes back to whatever the FSM is doing.
                    if (z._state != State.Dead && z.isActiveAndEnabled) z.ResumeStateClip();
                }
                if (z != null) z._reactListed = false;
                Reacting[i] = Reacting[Reacting.Count - 1];
                Reacting.RemoveAt(Reacting.Count - 1);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTimedVisuals() { Flashing.Clear(); Reacting.Clear(); }

        private void ResumeStateClip()
        {
            if (_vatAnimator == null || !_vatAnimator.enabled) return;
            // Locomotion resumes at THIS enemy's own phase - returning from a hit or a swing must
            // not snap it back into lockstep with everyone else who reacted at the same moment.
            switch (_state)
            {
                case State.Chase:
                    _vatAnimator.CrossFade(data.moveClip, stateCrossFadeDuration, _locomotionPhase);
                    break;
                default:
                    _vatAnimator.CrossFade(data.idleClip, stateCrossFadeDuration, _locomotionPhase);
                    break;
            }
        }

        /// <summary>Called by ZombieManager when the player dies - stop hunting the corpse.</summary>
        public void OnPlayerLost()
        {
            if (_state == State.Dead) return;
            CancelPendingAttack();   // no posthumous hits on a dead player
            _state = State.Idle;
            if (_motor.enabled) { _motor.IsStopped = true; _motor.ClearDestination(); }
            ResumeStateClip();
        }

        /// <summary>
        /// Pushes this enemy away from the player by an authored distance.
        ///
        /// M4 rewrote the mechanism, not the intent. The old note here explained that
        /// <c>Rigidbody.AddForce</c> could not move a NavMeshAgent-driven enemy because the agent
        /// overwrote the body every frame, so displacement had to be routed through
        /// <c>Agent.Move</c>. With the agent gone there is no longer anything overwriting position,
        /// so knockback is simply an impulse the motor decays - no coroutine, no per-frame lerp, and
        /// no navigation query to keep it on a surface that no longer exists.
        ///
        /// It is a DIRECT consequence of a hit and nothing more: no state, no tag, no duration anyone
        /// else can read, no downstream bonus. It reuses the hit-react gate, so a rapid-fire weapon
        /// cannot restart it every bullet and pin an enemy in place.
        /// </summary>
        public void ApplyPhysicalPush(float distance)
        {
            if (_state == State.Dead || distance <= 0f) return;
            if (!_motor.enabled) return; // Cheap/Inactive tiers own their own motion

            var player = PlayerMovement.Instance;
            Vector3 origin = player != null ? player.transform.position : transform.position - transform.forward;

            // ONE owner for displacement. The generic hit knockback is already in flight by the time a
            // weapon-authored push arrives (TakeDamage runs before ApplyKnockback). Both are impulses
            // on the same motor, so left alone they would sum and the final distance would become
            // pellet-order dependent. The authored push is the intentional, tunable response, so it
            // replaces whatever the generic reaction had queued.
            _motor.ClearImpulse();
            _motor.ApplyKnockback(origin, distance, Mathf.Max(0.05f, knockbackDuration));
        }

        /// <summary>
        /// A5 Gravity Well: drags this enemy toward <paramref name="point"/> by up to
        /// <paramref name="distance"/> (never past it). Same motor impulse as a push, aimed inward.
        /// </summary>
        public void ApplyPull(Vector3 point, float distance, float duration)
        {
            if (_state == State.Dead || distance <= 0f || !_motor.enabled) return;
            Vector3 to = point - transform.position; to.y = 0f;
            float d = to.magnitude;
            if (d < 0.35f) return;
            distance = Mathf.Min(distance, d - 0.3f);
            // ApplyKnockback moves AWAY from its origin: put the origin on the far side.
            _motor.ApplyKnockback(transform.position - to / d, distance, Mathf.Max(0.05f, duration));
        }

        /// <summary>Stock hit reaction: a small shove away from the player on every fresh hit react.</summary>
        private void ApplyGenericKnockback()
        {
            if (!_motor.enabled || knockbackDistance <= 0f) return;

            var player = PlayerMovement.Instance;
            if (player == null) return;

            _motor.ApplyKnockback(player.transform.position, knockbackDistance,
                Mathf.Max(0.05f, knockbackDuration));
        }

        private void HandleDeath()
        {
            // Exactly-once reward guard. Health already fires OnDeath a single time per life, but the
            // reward path must not depend on that: a pooled instance re-subscribes on every respawn,
            // so a double-report here would silently inflate the run's currency.
            if (_state == State.Dead) return;
            ShowEliteAura(false);   // the corpse dissolves without its glow

            // When a PickupManager is present it drops physical coin instead, so the ledger must not
            // also bank it here - the kill and XP still register either way.
            bool pickupsHandleCoin = PickupManager.Instance != null;
            RunState.Current?.RecordKill(data, !pickupsHandleCoin);
            Bill.Events?.Fire(new ZombieKilledEvent(data, transform.position, this));

            // Kill any pending hit react so it can't crossfade over the death anim.
            _reacting = false;
            FlushDamageNumber();   // the killing blow always shows

            // A swing already wound up must not still connect after death.
            CancelPendingAttack();

            // A corpse must not keep flashing white while it dissolves.
            _flashing = false;
            SetHitFlash(0f);

            _state = State.Dead;
            if (!string.IsNullOrEmpty(data.deathSfxKey) && Time.time >= _nextDeathVocalTime)
            {
                _nextDeathVocalTime = Time.time + 0.08f;
                // Death outranks hurt so a kill always reads over the chatter of the living.
                Bill.Audio?.PlayCue(data.deathSfxKey, transform.position, SfxPriority.Medium, 0.68f);
            }
            _motor.IsStopped = true;
            if (TryGetComponent(out Collider col)) col.enabled = false;
            _vatAnimator.CrossFade(data.deathClip, deathCrossFadeDuration);
            StartCoroutine(DissolveAndReturn());
        }

        private IEnumerator DissolveAndReturn()
        {
            float waitBeforeDissolve = Mathf.Max(0f, returnToPoolDelay - dissolveDuration);
            yield return new WaitForSeconds(waitBeforeDissolve);

            float t = 0f;
            while (t < dissolveDuration)
            {
                t += Time.deltaTime;
                SetDissolve(Mathf.Clamp01(t / dissolveDuration));
                yield return null;
            }

            Bill.Pool?.Return(gameObject);
        }

        /// <summary>Dissolves the body and fades the blob shadow out in step with it, so a corpse
        /// that has burnt away never leaves its shadow sitting on the ground.</summary>
        private void SetDissolve(float amount)
        {
            SetRendererFloat(DissolveID, amount);

            // Vệt tiếp đất mờ đi cùng nhịp với thân, nên xác đã tan hết không để lại bóng nằm trên đất.
            // Độ mờ giờ là một giá trị trong mesh gộp (vertex color), không còn là property block của
            // một renderer riêng — đó chính là thứ đã làm mỗi con tốn một draw.
            CharacterContactShadows.Instance?.SetFade(_contactShadowHandle, Mathf.Clamp01(amount));
        }

        private void SetHitFlash(float amount) => SetRendererFloat(HitFlashID, amount);

        // Read-modify-write on the shared block, so dissolve, hit flash and VAT_Animator's own
        // animation-time writes on this same renderer never clobber one another.
        private void SetRendererFloat(int propertyId, float value)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(_dissolvePropertyBlock);
            _dissolvePropertyBlock.SetFloat(propertyId, value);
            bodyRenderer.SetPropertyBlock(_dissolvePropertyBlock);
        }
    }
}
