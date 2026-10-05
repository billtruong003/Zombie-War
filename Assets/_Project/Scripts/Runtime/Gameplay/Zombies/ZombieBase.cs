using System.Collections;
using System.Collections.Generic;
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
    public abstract partial class ZombieBase : MonoBehaviour, IDamageable, ITargetable
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
        private static readonly int StatusTintID = Shader.PropertyToID("_StatusTint");

        private PlanarEnemyMotor _motor;
        private float _statScale = 1f;

        /// <summary>This enemy's hit damage: authored damage times the threat scale it spawned with.
        /// Every attack - contact, pounce, slam, charge, spit - must read this, not data.damage.</summary>
        protected float Damage => data.damage * _statScale * _timeDamage;
        private float _timeDamage = 1f;
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
        // Hit flash and hit react are timestamps advanced once a frame (TickTimedVisuals), not a
        // coroutine per hit: every bullet started a flash coroutine (iterator + Coroutine object) and
        // every react a WaitForSeconds, which added up to hundreds of allocations a second in a horde.
        private bool _reacting, _flashing, _flashListed, _reactListed;
        private float _reactEndsAt, _flashEndsAt;
        private static readonly List<ZombieBase> Flashing = new(256), Reacting = new(256);
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

            // A boss or elite with a planar shadow keeps its blob hidden.
            CharacterContactShadows.Instance?.SetVisible(_contactShadowHandle, visible && !_planarShadow);
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

        /// <summary>True from the killing blow until the pool takes the instance back.</summary>
        public bool IsDead => _state == State.Dead;

        /// <summary>
        /// Colours the body to show a status (frost blue, burning orange). Alpha is the strength; a
        /// clear colour removes it. Per-instance on the same property block as hit flash, so the
        /// whole crowd still shares one material.
        /// </summary>
        public void SetStatusTint(Color tint)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(_dissolvePropertyBlock);
            _dissolvePropertyBlock.SetVector(StatusTintID, tint);
            bodyRenderer.SetPropertyBlock(_dissolvePropertyBlock);
        }

        /// <summary>While true every incoming hit is ignored. Kept separate from
        /// <see cref="CanBeTargeted"/> because splash damage does not go through targeting.</summary>
        protected virtual bool IsInvulnerable => false;

        // Collider -> enemy, filled once per instance (pooled enemies keep theirs). A raycast or
        // sweep hit resolves its enemy with one lookup instead of a GetComponentInParent walk - the
        // gun did 4-6 of those per pellet.
        static readonly Dictionary<int, ZombieBase> ByCollider = new(1024);
        Collider[] _colliders;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetColliderMap() => ByCollider.Clear();

        /// <summary>The enemy a collider belongs to, or null (props, ground, the player).</summary>
        public static ZombieBase FromCollider(Collider c) =>
            c != null && ByCollider.TryGetValue(c.GetInstanceID(), out var z) && z != null ? z : null;

        /// <summary>This enemy's Health (read by the hit pipeline).</summary>
        internal Health Life => _health;

        private void OnDestroy()
        {
            if (_colliders == null) return;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) ByCollider.Remove(_colliders[i].GetInstanceID());
        }

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < _colliders.Length; i++) ByCollider[_colliders[i].GetInstanceID()] = this;
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
            // on (re)spawn so pooled instances don't keep the previous occupant's tuning. Late-run
            // threat scales health and damage; it is read ONCE here, so an enemy keeps the stats it
            // arrived with instead of growing mid-fight.
            _statScale = Threat.ThreatDirector.EnemyStatMultiplier;
            _timeDamage = Threat.ThreatDirector.EnemyTimeDamage;
            // Greed's price is paid by enemies that arrive after it is taken (read once, like threat).
            _health.Configure(data.maxHealth * _statScale * Threat.ThreatDirector.EnemyTimeHealth
                              * (Skills.SkillRuntime.Active?.EnemyHealthMultiplier ?? 1f));
            _motor.ConfigureFromData(data.moveSpeed);
            _motor.ResetMotion();
            _state = State.Idle;
            _tier = ZombieTier.Full;
            _attackCooldownTimer = 0f;
            ReleaseAttackSlotIfHeld();
            _pendingNumber = 0f; _nextNumberAt = 0f;
            _reacting = false;   // a new life starts with no flash or react in progress
            _flashing = false;
            SetHitFlash(0f);     // a recycled enemy pulled mid-flash must not come back white
            _attackRoutine = null;
            _nextHurtAudioTime = 0f;
            _cheapBlockedTime = 0f;
            _forceFullUntil = 0f; // a pooled instance must not inherit its previous life's promotion
            _motor.IsStopped = false;
            if (TryGetComponent(out Collider col)) col.enabled = true;
            SetDissolve(0f);
            SetHitFlash(0f); // a pooled instance must not reappear still lit from its last death
            SetStatusTint(Color.clear); // nor still frozen or burning
            ShowEliteAura(true);
            UsePlanarShadow(true);
            OnSpawned();
        }

        // Elites (and bosses) wear a soft gold aura from Epic Toon (2026-10-01): they carry the
        // chests, gems and items, and hit harder, so they must read at a glance in the crowd. One
        // instance per pooled enemy, made the first time it spawns as an elite, hidden on death.
        static GameObject _eliteAuraPrefab;
        static bool _eliteAuraLoaded;
        GameObject _eliteAura;

        void ShowEliteAura(bool on)
        {
            on &= data != null && data.isElite;
            if (!on) { if (_eliteAura != null) _eliteAura.SetActive(false); return; }
            if (!_eliteAuraLoaded) { _eliteAuraPrefab = Resources.Load<GameObject>("FX/EliteAura"); _eliteAuraLoaded = true; }
            if (_eliteAuraPrefab == null) return;
            if (_eliteAura == null)
            {
                _eliteAura = Instantiate(_eliteAuraPrefab, transform);
                _eliteAura.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                float height = TryGetComponent(out CapsuleCollider capsule) ? capsule.height : 2f;   // bosses get a wider ring
                _eliteAura.transform.localScale = _eliteAuraPrefab.transform.localScale * Mathf.Clamp(height / 2f, 1f, 1.8f);
                var glow = _eliteAura.AddComponent<ToonPointLight>();
                glow.colour = new Color(1f, 0.82f, 0.35f);
                glow.range = 3.5f * Mathf.Clamp(height / 2f, 1f, 1.8f);
                glow.intensity = 0.8f;
                glow.flicker = 0.15f;
                glow.offset = Vector3.up * 1f;
            }
            _eliteAura.SetActive(true);
        }

        // Bosses and elites cast a real (planar) shadow (owner 02/10), at every graphics tier, when the
        // map has its baked light; the crowd keeps the blob. The body renderer is drawn with its
        // VAT material's planar pass by PlanarShadowCastersFeature.
        bool _planarShadow;
        static readonly int PlanarShadowOnId = Shader.PropertyToID("_ZWPlanarShadowOn");

        void UsePlanarShadow(bool on)
        {
            on &= bodyRenderer != null && ((data != null && data.isElite) || this is ZombieBoss)
                  && Shader.GetGlobalFloat(PlanarShadowOnId) > 0.5f;
            if (on == _planarShadow) return;
            _planarShadow = on;
            if (on) PlanarShadowCasters.Add(bodyRenderer); else PlanarShadowCasters.Remove(bodyRenderer);
            CharacterContactShadows.Instance?.SetVisible(_contactShadowHandle, !on && (bodyRenderer == null || bodyRenderer.enabled));
        }

        private void OnDisable()
        {
            // Enemies are POOLED. Statuses are keyed by transform instance id, so without this a
            // recycled enemy inherits the Exposed / slow / mark of whatever died in its slot — and
            // that surfaces weeks later as unexplained damage spikes.
            ZombieWar.Skills.StatusCarrier.Clear(transform.GetInstanceID());
            // Release BEFORE clearing the role: the slot counter decides by this enemy's role, and a
            // beacon boss cleared first would be counted as an ordinary attacker on the way out.
            ReleaseAttackSlotIfHeld();   // dying mid-swing must not leak a slot
            IsBeaconOwned = false;   // a recycled instance must not inherit the last occupant's role
            IsWaitingForAttackSlot = false;
            _nextHitCrit = false;    // a crit marked on a blocked hit must not show on the next occupant

            TargetRegistry.Unregister(this);
            ZombieManager.Unregister(this);
            UsePlanarShadow(false);
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
            float step = data.moveSpeed * _motor.SlowFactor() * ZombieManager.PursuitMultiplier(from) * Time.deltaTime;

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

        static int _visualsTickedFrame = -1;

        protected static Vector3 FlattenY(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
