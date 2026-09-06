using System.Collections;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    public enum ZombieTier
    {
        // Full state machine + planar steering motor + VAT crossfade + attack checks. Only zombies
        // close to (or on-screen near) the player run this - driven by their own Update().
        Full,
        // No steering, no target search, no animation crossfade - just a straight-line lerp toward a
        // point near the player. NOT driven by this object's own Update(): ZombieManager calls
        // CheapTick() directly on a throttled interval across every Cheap-tier zombie in one pass.
        Cheap,
        // Belongs to a chunk outside the player's active grid - GameObject fully disabled, no logic
        // at all until ZombieManager reactivates it.
        Inactive
    }

    // Shared machinery for EVERY zombie type: tiered LOD, planar steering chase, VAT animation,
    // health/damage, knockback, dissolve + pool return. Concrete subtypes only override the two things
    // that actually differ between zombies - HOW they approach the player (Chase) and HOW they attack
    // (PerformAttack) - so adding a new variant is a small focused subclass, not a fork of the FSM.
    // VAT_Animator lives on a child "Visual" GameObject (not required on the root) so the mesh can be
    // rotated/offset independently without disturbing the root's collider + motor orientation.
    //
    // M4: NavMeshAgent is gone. The streamed world is a flat open surface with almost no authored
    // blockers, so navigation reduces to pursue + separate + slide, which PlanarEnemyMotor does
    // without needing baked mesh data to exist before an enemy may move.
    [RequireComponent(typeof(PlanarEnemyMotor), typeof(Health))]
    public abstract class ZombieBase : MonoBehaviour, IDamageable, ITargetable
    {
        protected enum State { Idle, Chase, Attack, Dead }

        /// <summary>Attack exits back to Chase only beyond EngageRange × this factor - separate
        /// enter/exit thresholds so the range boundary cannot thrash the state machine.</summary>
        private const float EngageExitFactor = 1.3f;

        [SerializeField] private ZombieData data;
        [SerializeField] private Renderer bodyRenderer;

        [Tooltip("Độ đậm vệt tiếp đất của con này trong mesh bóng dùng chung.")]
        [Range(0f, 1f)]
        [SerializeField] private float contactShadowOpacity = 0.45f;

        private int _contactShadowHandle = -1;
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

        private PlanarEnemyMotor _motor;
        private Health _health;
        private VAT_Animator _vatAnimator;
        private MaterialPropertyBlock _dissolvePropertyBlock;
        private State _state;
        private ZombieTier _tier = ZombieTier.Full;
        private float _attackCooldownTimer;
        private bool _holdsAttackSlot;

        /// <summary>
        /// True while this enemy is pressed against the player but has no attack slot. Exposed so the
        /// crowd is inspectable: a waiting enemy must read as WAITING, not as frozen or broken.
        /// </summary>
        public bool IsWaitingForAttackSlot { get; private set; }
        private bool _waitingForAttackSlot
        {
            get => IsWaitingForAttackSlot;
            set => IsWaitingForAttackSlot = value;
        }
        private Coroutine _hitReactRoutine;
        private Coroutine _hitFlashRoutine;
        private Coroutine _attackRoutine;
        private float _nextHurtAudioTime;

        /// <summary>
        /// This instance's fixed place in the looping locomotion cycle, in [0,1).
        ///
        /// Derived from the pooled instance's identity, so it is stable for the object's whole life
        /// and survives idle↔move transitions - the enemy's own walk stays coherent while the crowd
        /// stops sharing one phase. Deliberately NOT re-rolled per state change (that would make a
        /// single enemy stutter) and NOT random per frame.
        /// </summary>
        private float _locomotionPhase;

        // Horde audio is deliberately sampled globally. Bill.Audio has a finite source pool, and
        // allowing every pellet against sixty enemies to speak would erase guns and warnings.
        private static float _nextImpactAudioTime;
        private static float _nextHurtVocalTime;
        private static float _nextAttackVocalTime;
        private static float _nextDeathVocalTime;

        public ZombieData Data => data;

        /// <summary>
        /// True when a Boss Beacon spawned this enemy. The leash must never recycle it: an authored
        /// encounter disappearing because the player walked away would read as a bug, and the beacon
        /// ledger would be left holding an encounter slot for a boss that no longer exists.
        /// Cleared on despawn so a pooled instance cannot inherit it.
        /// </summary>
        public bool IsBeaconOwned { get; set; }
        public ZombieTier Tier => _tier;

        protected PlanarEnemyMotor Motor => _motor;
        protected Health Health => _health;
        protected VAT_Animator Vat => _vatAnimator;
        protected State CurrentState => _state;
        protected Renderer BodyRenderer => bodyRenderer;

        /// <summary>Shows/hides the body and its blob shadow together. Used by tiering and by the
        /// burrower's underground phase - the two must never disagree.</summary>
        protected void SetVisible(bool visible)
        {
            if (bodyRenderer != null) bodyRenderer.enabled = visible;

            CharacterContactShadows.Instance?.SetVisible(_contactShadowHandle, visible);
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
            _motor = GetComponent<PlanarEnemyMotor>();
            _health = GetComponent<Health>();
            // VAT_Animator lives on the child "Visual" mesh, not the root - search children (incl. inactive).
            _vatAnimator = GetComponentInChildren<VAT_Animator>(true);
            if (_vatAnimator == null)
                Debug.LogError($"[{name}] VAT_Animator missing in children - zombie animation disabled.", this);
            _dissolvePropertyBlock = new MaterialPropertyBlock();

            _locomotionPhase = LocomotionPhase.Next();
        }

        /// <summary>
        /// Kích thước vệt tiếp đất, đo một lần từ collider chứ không đọc bounds animation mỗi frame.
        /// </summary>
        private void ResolveContactShadowSize(out float halfWidth, out float halfLength)
        {
            float radius = 0.35f;
            if (TryGetComponent(out CapsuleCollider capsule)) radius = capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            else if (TryGetComponent(out SphereCollider sphere)) radius = sphere.radius * transform.lossyScale.x;

            halfWidth = Mathf.Clamp(radius * 1.35f, 0.18f, 3.5f);
            halfLength = halfWidth;
        }

        /// <summary>
        /// Nhận một chỗ trong mesh bóng gộp.
        ///
        /// Không còn renderer bóng riêng cho từng con. Kiến trúc cũ (`ShadowBlob` + `M_BlobShadow`,
        /// queue trong suốt 3000) là toàn bộ phần chi phí render còn tăng tuyến tính: đo được 30 quái
        /// = 30 draw, vì hình trong suốt sắp xếp theo từng object nên không bao giờ gộp được.
        /// </summary>
        private void AcquireContactShadow()
        {

            var batch = CharacterContactShadows.EnsureInstance();
            if (batch == null) return;

            if (_contactShadowHandle >= 0) batch.Unregister(_contactShadowHandle);

            ResolveContactShadowSize(out float hw, out float hl);
            _contactShadowHandle = batch.Register(transform, hw, hl, contactShadowOpacity);
        }

        private void ReleaseContactShadow()
        {
            var batch = CharacterContactShadows.Instance;
            if (batch != null && _contactShadowHandle >= 0) batch.Unregister(_contactShadowHandle);
            _contactShadowHandle = -1;
        }

        private void OnEnable()
        {
            TargetRegistry.Register(this);
            ZombieManager.Register(this);
            AcquireContactShadow();
            _health.OnDamaged += HandleDamaged;
            _health.OnDeath += HandleDeath;

            // Data is the source of truth for a type's stats - push them into the shared components
            // on (re)spawn so pooled instances don't keep the previous occupant's tuning.
            _health.Configure(data.maxHealth);
            _motor.ConfigureFromData(data.moveSpeed);
            _motor.ResetMotion();
            _state = State.Idle;
            _tier = ZombieTier.Full;
            _attackCooldownTimer = 0f;
            ReleaseAttackSlotIfHeld();
            _hitReactRoutine = null; // coroutines died with the pooled deactivation
                        _hitFlashRoutine = null;
            _attackRoutine = null;
            _nextHurtAudioTime = 0f;
            _cheapBlockedTime = 0f;
            _forceFullUntil = 0f; // a pooled instance must not inherit its previous life's promotion
            _motor.IsStopped = false;
            if (TryGetComponent(out Collider col)) col.enabled = true;
            SetDissolve(0f);
            SetHitFlash(0f); // a pooled instance must not reappear still lit from its last death
            OnSpawned();
        }

        private void OnDisable()
        {
            // Enemies are POOLED. Statuses are keyed by transform instance id, so without this a
            // recycled enemy inherits the Exposed / slow / mark of whatever died in its slot — and
            // that surfaces weeks later as unexplained damage spikes.
            ZombieWar.Skills.StatusCarrier.Clear(transform.GetInstanceID());
            IsBeaconOwned = false;   // a recycled instance must not inherit the last occupant's role
            ReleaseAttackSlotIfHeld();   // dying mid-swing must not leak a slot
            IsWaitingForAttackSlot = false;

            TargetRegistry.Unregister(this);
            ZombieManager.Unregister(this);
            ReleaseContactShadow();
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
                    _motor.enabled = true;
                    _vatAnimator.enabled = true;
                    SetVisible(true);
                    break;
                case ZombieTier.Cheap:
                    _motor.enabled = false;
                    _vatAnimator.enabled = true;
                    SetVisible(true);
                    break;
                case ZombieTier.Inactive:
                    _motor.enabled = false;
                    _vatAnimator.enabled = false;
                    SetVisible(false);
                    break;
            }
        }

        // How long a Cheap-tier zombie may fail to make progress before it earns a temporary
        // Full-tier promotion (real steering, separation and obstacle sliding), and how long that
        // promotion lasts before the manager may demote it again.
        private const float CheapBlockedPromoteAfter = 1.5f;
        private const float CheapPromoteDuration = 4f;

        /// <summary>Progress below this fraction of the intended step counts as blocked.</summary>
        private const float CheapProgressFraction = 0.35f;

        private float _cheapBlockedTime;
        private float _forceFullUntil;

        /// <summary>True while a blocked Cheap-tier zombie has requested real steering.
        /// ZombieManager honours this inside the cheap radius so the straight-line tier cannot
        /// pin a zombie against an obstacle forever.</summary>
        public bool NeedsFullTier => Time.time < _forceFullUntil;

        // Called by ZombieManager - cheap on purpose: no steering, no target search, no animation
        // change, just a straight-line step on the gameplay plane.
        //
        // M4: the NavMesh.Raycast clamp is gone with the mesh. Blockage is now detected from the
        // MEASURED step instead of from a mesh edge - if the zombie wanted to move and barely did,
        // something is in the way and it asks for a Full-tier promotion so real steering can slide
        // around it. That keeps the original escape hatch without needing navigation data.
        public void CheapTick(Vector3 towardPosition)
        {
            if (_state == State.Dead || _tier != ZombieTier.Cheap) return;

            Vector3 from = transform.position;
            float step = data.moveSpeed * Time.deltaTime;

            Vector3 desired = Vector3.MoveTowards(from, towardPosition, step);
            desired.y = 0f;
            transform.position = desired;

            float wanted = Mathf.Min(step, Vector3.Distance(FlattenY(from), FlattenY(towardPosition)));
            float achieved = Vector3.Distance(FlattenY(from), FlattenY(transform.position));

            if (wanted > 0.0001f && achieved < wanted * CheapProgressFraction)
            {
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
        }

        /// <summary>
        /// Watchdog that keeps a zombie on the shared gameplay plane.
        ///
        /// M4: there is no mesh to fall off any more, so the failure this guards against changed
        /// shape. What can still go wrong is vertical drift - an interrupted leap, an external
        /// displacement, a pooled instance revived at an odd height - which would leave the enemy
        /// floating or buried and unable to reach the player. A NaN or absurd position is
        /// unrecoverable and returns the instance to the pool rather than holding a wave open.
        /// </summary>
        public void RecoverIfStranded()
        {
            if (_state == State.Dead || _tier != ZombieTier.Full) return;

            Vector3 position = transform.position;

            if (float.IsNaN(position.x) || float.IsNaN(position.z) ||
                Mathf.Abs(position.x) > 1e6f || Mathf.Abs(position.z) > 1e6f)
            {
                Debug.LogWarning($"[{name}] position became invalid - returning to pool.", this);
                Bill.Pool?.Return(gameObject);
                return;
            }

            if (Mathf.Abs(position.y) > 0.01f)
            {
                position.y = 0f;
                transform.position = position;
            }
        }

        private void Update()
        {
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
            if (_state == State.Attack && _hitReactRoutine == null)
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
            target.GetComponentInParent<IDamageable>()?.TakeDamage(data.damage);
        }

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
            ApplyGenericKnockback();
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

        protected static Vector3 FlattenY(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
