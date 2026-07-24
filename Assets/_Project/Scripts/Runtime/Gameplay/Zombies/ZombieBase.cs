using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using BillGameCore;

namespace ZombieWar
{
    public enum ZombieTier
    {
        // Full state machine + NavMeshAgent + VAT crossfade + attack checks. Only zombies close to
        // (or on-screen near) the player run this - driven by their own Update().
        Full,
        // No NavMesh, no target search, no animation crossfade - just a straight-line lerp toward a
        // point near the player. NOT driven by this object's own Update(): ZombieManager calls
        // CheapTick() directly on a throttled interval across every Cheap-tier zombie in one pass.
        Cheap,
        // Belongs to a chunk outside the player's active grid - GameObject fully disabled, no logic
        // at all until ZombieManager reactivates it.
        Inactive
    }

    // Shared machinery for EVERY zombie type: tiered LOD, NavMesh chase, VAT animation, health/
    // damage, knockback, dissolve + pool return. Concrete subtypes only override the two things that
    // actually differ between zombies - HOW they approach the player (Chase) and HOW they attack
    // (PerformAttack) - so adding a new variant is a small focused subclass, not a fork of the FSM.
    // VAT_Animator lives on a child "Visual" GameObject (not required on the root) so the mesh can be
    // rotated/offset independently without disturbing the root's collider + NavMeshAgent orientation.
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public abstract class ZombieBase : MonoBehaviour, IDamageable, ITargetable
    {
        protected enum State { Idle, Chase, Attack, Dead }

        [SerializeField] private ZombieData data;
        [SerializeField] private Renderer bodyRenderer;
        [Tooltip("Flat blob quad standing in for a real cast shadow. Follows the body's visibility " +
                 "so a hidden or burrowed enemy never leaves a shadow floating on the ground.")]
        [SerializeField] private Renderer shadowRenderer;
        [SerializeField] private float stateCrossFadeDuration = 0.2f;
        [SerializeField] private float deathCrossFadeDuration = 0.1f;
        [SerializeField] private float dissolveDuration = 0.7f;
        [SerializeField] private float returnToPoolDelay = 1.5f;
        [SerializeField] private float knockbackDistance = 0.3f;
        [SerializeField] private float knockbackDuration = 0.15f;
        [Tooltip("World height above the zombie's origin where the floating damage number pops.")]
        [SerializeField] private float damageNumberHeight = 1.7f;
        [Tooltip("How long the white hit flash stays visible. Short by design - it must read on a " +
                 "fast-firing weapon without strobing.")]
        [SerializeField] private float hitFlashDuration = 0.12f;

        private static readonly int DissolveID = Shader.PropertyToID("_Dissolve");
        private static readonly int HitFlashID = Shader.PropertyToID("_HitFlash");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorID = Shader.PropertyToID("_Color");

        private NavMeshAgent _agent;
        private Health _health;
        private VAT_Animator _vatAnimator;
        private MaterialPropertyBlock _dissolvePropertyBlock;
        private MaterialPropertyBlock _shadowPropertyBlock;
        private Color _shadowBaseColor = new Color(0f, 0f, 0f, 0.45f);
        private State _state;
        private ZombieTier _tier = ZombieTier.Full;
        private float _attackCooldownTimer;
        private Coroutine _hitReactRoutine;
        private Coroutine _hitFlashRoutine;
        private Coroutine _attackRoutine;

        public ZombieData Data => data;
        public ZombieTier Tier => _tier;

        protected NavMeshAgent Agent => _agent;
        protected Health Health => _health;
        protected VAT_Animator Vat => _vatAnimator;
        protected State CurrentState => _state;
        protected Renderer BodyRenderer => bodyRenderer;
        protected Renderer ShadowRenderer => shadowRenderer;

        /// <summary>Shows/hides the body and its blob shadow together. Used by tiering and by the
        /// burrower's underground phase - the two must never disagree.</summary>
        protected void SetVisible(bool visible)
        {
            if (bodyRenderer != null) bodyRenderer.enabled = visible;
            if (shadowRenderer != null) shadowRenderer.enabled = visible;
        }

        /// <summary>Set by subtypes that are driving their own movement phase this frame, so the
        /// shared chase/attack FSM steps aside. See <see cref="Update"/>.</summary>
        protected virtual bool SuppressBaseFsm => false;

        // Distance at which the zombie stops chasing and switches to attacking. Melee uses the
        // data's attack range; ranged types widen this so they open fire from well outside it.
        protected virtual float EngageRange => data.attackRange;

        Transform ITargetable.Transform => transform;
        bool ITargetable.IsTargetable => CanBeTargeted;

        /// <summary>Whether the player's auto-aim may lock onto this zombie. Subtypes hide while they
        /// are physically unreachable - a burrower underground must not soak up the player's fire.</summary>
        protected virtual bool CanBeTargeted => _state != State.Dead;

        /// <summary>While true every incoming hit is ignored. Kept separate from
        /// <see cref="CanBeTargeted"/> because splash damage does not go through targeting.</summary>
        protected virtual bool IsInvulnerable => false;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            // VAT_Animator lives on the child "Visual" mesh, not the root - search children (incl. inactive).
            _vatAnimator = GetComponentInChildren<VAT_Animator>(true);
            if (_vatAnimator == null)
                Debug.LogError($"[{name}] VAT_Animator missing in children - zombie animation disabled.", this);
            _dissolvePropertyBlock = new MaterialPropertyBlock();
            _shadowPropertyBlock = new MaterialPropertyBlock();
            // Cache the authored blob tint once, so the per-frame fade multiplies a constant rather
            // than compounding against whatever it wrote last frame.
            if (shadowRenderer != null && shadowRenderer.sharedMaterial != null)
            {
                var m = shadowRenderer.sharedMaterial;
                if (m.HasProperty(BaseColorID)) _shadowBaseColor = m.GetColor(BaseColorID);
                else if (m.HasProperty(ColorID)) _shadowBaseColor = m.GetColor(ColorID);
            }
        }

        private void OnEnable()
        {
            TargetRegistry.Register(this);
            ZombieManager.Register(this);
            _health.OnDamaged += HandleDamaged;
            _health.OnDeath += HandleDeath;

            // Data is the source of truth for a type's stats - push them into the shared components
            // on (re)spawn so pooled instances don't keep the previous occupant's tuning.
            _health.Configure(data.maxHealth);
            _agent.speed = data.moveSpeed;
            _state = State.Idle;
            _tier = ZombieTier.Full;
            _attackCooldownTimer = 0f;
            _hitReactRoutine = null; // coroutines died with the pooled deactivation
            _hitFlashRoutine = null;
            _attackRoutine = null;
            _cheapBlockedTime = 0f;
            _forceFullUntil = 0f; // a pooled instance must not inherit its previous life's promotion
            _agent.isStopped = false;
            if (TryGetComponent(out Collider col)) col.enabled = true;
            SetDissolve(0f);
            SetHitFlash(0f); // a pooled instance must not reappear still lit from its last death
            OnSpawned();
        }

        private void OnDisable()
        {
            TargetRegistry.Unregister(this);
            ZombieManager.Unregister(this);
            _health.OnDamaged -= HandleDamaged;
            _health.OnDeath -= HandleDeath;
            StopAllCoroutines();
            OnDespawned();
        }

        // Per-type spawn/despawn hooks (reset special-attack timers, etc.). Run after the base has
        // reset the shared state, so overrides see a clean slate.
        protected virtual void OnSpawned() { }
        protected virtual void OnDespawned() { }

        public void TakeDamage(float amount)
        {
            if (IsInvulnerable) return;
            _health.TakeDamage(amount);
        }

        // Deliberately never calls gameObject.SetActive(false) here - that would fire OnDisable(),
        // which unregisters from ZombieManager, and an Inactive zombie would then never be found
        // again to reactivate when the player comes back. Instead, Inactive disables the actually
        // expensive components (agent, VAT playback, rendering) while the behaviour itself (and its
        // registration) stays alive - true SetActive(false) is reserved for the pool-return
        // lifecycle in DissolveAndReturn(), a separate concern from tiering.
        public void SetTier(ZombieTier tier)
        {
            if (_state == State.Dead || _tier == tier) return;

            _tier = tier;
            switch (tier)
            {
                case ZombieTier.Full:
                    _agent.enabled = true;
                    _vatAnimator.enabled = true;
                    SetVisible(true);
                    break;
                case ZombieTier.Cheap:
                    _agent.enabled = false;
                    _vatAnimator.enabled = true;
                    SetVisible(true);
                    break;
                case ZombieTier.Inactive:
                    _agent.enabled = false;
                    _vatAnimator.enabled = false;
                    SetVisible(false);
                    break;
            }
        }

        // How long a Cheap-tier zombie may push against a NavMesh edge before it earns a temporary
        // Full-tier promotion (real pathfinding) to walk around the obstacle, and how long that
        // promotion lasts before the manager may demote it again.
        private const float CheapBlockedPromoteAfter = 1.5f;
        private const float CheapPromoteDuration = 4f;

        private float _cheapBlockedTime;
        private float _forceFullUntil;

        /// <summary>True while a blocked Cheap-tier zombie has requested real pathfinding.
        /// ZombieManager honours this inside the cheap radius so the straight-line tier cannot
        /// pin a zombie against an obstacle forever.</summary>
        public bool NeedsFullTier => Time.time < _forceFullUntil;

        // Called by ZombieManager - cheap on purpose: no pathing, no target search, no animation
        // change. The step is clamped to the carved NavMesh via NavMesh.Raycast so straight-line
        // movement can never tunnel through an obstacle footprint; a zombie held at an edge for
        // CheapBlockedPromoteAfter seconds asks for a temporary Full-tier promotion instead.
        public void CheapTick(Vector3 towardPosition)
        {
            if (_state == State.Dead || _tier != ZombieTier.Cheap) return;

            Vector3 desired = Vector3.MoveTowards(
                transform.position, towardPosition, data.moveSpeed * Time.deltaTime);

            if (NavMesh.Raycast(transform.position, desired, out NavMeshHit hit, NavMesh.AllAreas))
            {
                desired = hit.hit ? hit.position : transform.position;
                _cheapBlockedTime += Time.deltaTime;
                if (_cheapBlockedTime >= CheapBlockedPromoteAfter)
                {
                    _forceFullUntil = Time.time + CheapPromoteDuration;
                    _cheapBlockedTime = 0f;
                }
            }
            else
            {
                _cheapBlockedTime = 0f;
            }

            transform.position = desired;
        }

        /// <summary>Watchdog recovery for a Full-tier zombie whose agent lost the mesh (legacy
        /// off-mesh drift, external displacement). Warps to the nearest mesh point when one is
        /// close; a truly stranded zombie is returned to the pool so it can never hold a wave
        /// open while being unreachable and unable to attack.</summary>
        public void RecoverIfOffMesh()
        {
            if (_state == State.Dead || _tier != ZombieTier.Full) return;
            if (!_agent.enabled || _agent.isOnNavMesh) return;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"[{name}] stranded off the NavMesh - returning to pool.", this);
                Bill.Pool?.Return(gameObject);
            }
        }

        private void Update()
        {
            if (_tier != ZombieTier.Full || _state == State.Dead) return;

            var player = PlayerMovement.Instance;
            if (player == null) return;

            if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.deltaTime;

            float distance = Vector3.Distance(transform.position, player.transform.position);

            // A subtype running its own movement phase (a burrower underground, a boss mid-dash)
            // still gets its per-frame tick, but the shared chase/attack FSM stands down so the two
            // cannot fight over the agent's destination.
            if (SuppressBaseFsm)
            {
                OnFullTick(player.transform, distance);
                return;
            }

            SwitchState(distance <= EngageRange ? State.Attack : State.Chase);

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
                    _vatAnimator.CrossFade(data.moveClip, stateCrossFadeDuration);
                    _agent.isStopped = false;
                    break;
                case State.Attack:
                    _agent.isStopped = true;
                    break;
            }
        }

        // Default approach: NavMesh pathfind straight at the player. Runners override to lunge;
        // ranged types override to hold their distance.
        protected virtual void Chase(Transform target)
        {
            if (_agent.enabled) _agent.SetDestination(target.position);
        }

        private void FaceAndAttack(Transform target)
        {
            transform.rotation = Quaternion.LookRotation(FlattenY(target.position - transform.position));
            if (_attackCooldownTimer > 0f || _attackRoutine != null) return;

            _attackCooldownTimer = data.attackCooldown;
            _vatAnimator.CrossFade(data.attackClip, stateCrossFadeDuration);
            _attackRoutine = StartCoroutine(AttackAfterWindup(target));
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

            _attackRoutine = null;
            if (_state == State.Dead || target == null) yield break;

            PerformAttack(target);
        }

        /// <summary>Cancels an in-flight swing so a dying or despawning zombie cannot still deal its
        /// damage a few frames later.</summary>
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
            target.GetComponentInParent<IDamageable>()?.TakeDamage(data.damage);
        }

        private void HandleDamaged(float amount)
        {
            if (_state == State.Dead) return;

            // Floating damage number at chest height. Covers every source (guns, bomb, contact)
            // since it hangs off Health.OnDamaged rather than any single weapon.
            DamageNumberSpawner.Spawn(amount, transform.position + Vector3.up * damageNumberHeight);

            // The flash restarts on EVERY hit (unlike the react anim below) - that per-bullet
            // response is the whole point of it, and it's a shader value so it costs nothing.
            if (_hitFlashRoutine != null) StopCoroutine(_hitFlashRoutine);
            _hitFlashRoutine = StartCoroutine(HitFlash());

            // One-shot hit react: while the hit anim is still playing, further bullets only
            // deal damage/spawn numbers - they do NOT restart the anim or re-knockback, so
            // rapid fire can't lock the zombie into a looping flinch.
            if (_hitReactRoutine != null) return;
            _hitReactRoutine = StartCoroutine(HitReact());
            StartCoroutine(Knockback());
        }

        private IEnumerator HitReact()
        {
            _vatAnimator.Play(data.hitClip);

            float duration = 0.4f;
            if (_vatAnimator.animationData != null &&
                _vatAnimator.animationData.TryGetClipInfo(data.hitClip, out var clip) &&
                clip.duration > 0f)
                duration = clip.duration;

            yield return new WaitForSeconds(duration);
            _hitReactRoutine = null;

            // Hit clips are baked looping like everything else in the VAT, so once the react
            // window ends we must hand the animator back to whatever the FSM is doing.
            if (_state == State.Dead) yield break;
            ResumeStateClip();
        }

        // Fades the shader's white flash back out. Deliberately unscaled-time-free: it uses regular
        // deltaTime so a paused game freezes the flash along with everything else.
        private IEnumerator HitFlash()
        {
            float t = 0f;
            while (t < hitFlashDuration)
            {
                t += Time.deltaTime;
                SetHitFlash(1f - Mathf.Clamp01(t / hitFlashDuration));
                yield return null;
            }
            SetHitFlash(0f);
            _hitFlashRoutine = null;
        }

        private void ResumeStateClip()
        {
            if (_vatAnimator == null || !_vatAnimator.enabled) return;
            switch (_state)
            {
                case State.Chase:
                    _vatAnimator.CrossFade(data.moveClip, stateCrossFadeDuration);
                    break;
                default:
                    _vatAnimator.CrossFade(data.idleClip, stateCrossFadeDuration);
                    break;
            }
        }

        /// <summary>Called by ZombieManager when the player dies - stop hunting the corpse.</summary>
        public void OnPlayerLost()
        {
            if (_state == State.Dead) return;
            CancelPendingAttack();   // no posthumous hits on a dead player
            _state = State.Idle;
            if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
            ResumeStateClip();
        }

        private IEnumerator Knockback()
        {
            var player = PlayerMovement.Instance;
            if (player == null) yield break;

            Vector3 away = FlattenY(transform.position - player.transform.position).normalized;
            Vector3 from = transform.position;
            Vector3 to = from + away * knockbackDistance;

            float t = 0f;
            while (t < knockbackDuration)
            {
                t += Time.deltaTime;
                if (_agent.enabled) _agent.Move(Vector3.Lerp(from, to, t / knockbackDuration) - transform.position);
                yield return null;
            }
        }

        private void HandleDeath()
        {
            // Exactly-once reward guard. Health already fires OnDeath a single time per life, but the
            // reward path must not depend on that: a pooled instance re-subscribes on every respawn,
            // so a double-report here would silently inflate the run's currency.
            if (_state == State.Dead) return;

            // When a PickupManager is present it drops physical coin instead, so the ledger must not
            // also bank it here - the kill and XP still register either way.
            bool pickupsHandleCoin = PickupManager.Instance != null;
            RunState.Current?.RecordKill(data, !pickupsHandleCoin);
            Bill.Events?.Fire(new ZombieKilledEvent(data, transform.position));

            // Kill any pending hit react so it can't crossfade over the death anim.
            if (_hitReactRoutine != null)
            {
                StopCoroutine(_hitReactRoutine);
                _hitReactRoutine = null;
            }

            // A swing already wound up must not still connect after death.
            CancelPendingAttack();

            // A corpse must not keep flashing white while it dissolves.
            if (_hitFlashRoutine != null)
            {
                StopCoroutine(_hitFlashRoutine);
                _hitFlashRoutine = null;
            }
            SetHitFlash(0f);

            _state = State.Dead;
            _agent.isStopped = true;
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

            if (shadowRenderer == null) return;
            // The blob uses the shared M_BlobShadow material, so the alpha MUST go through a
            // property block - writing the material colour directly would fade every enemy at once.
            shadowRenderer.GetPropertyBlock(_shadowPropertyBlock);
            var tint = _shadowBaseColor;
            tint.a *= 1f - Mathf.Clamp01(amount);
            _shadowPropertyBlock.SetColor(BaseColorID, tint);
            _shadowPropertyBlock.SetColor(ColorID, tint);
            shadowRenderer.SetPropertyBlock(_shadowPropertyBlock);
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

        protected static Vector3 FlattenY(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
