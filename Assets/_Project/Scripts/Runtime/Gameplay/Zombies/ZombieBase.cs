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
        [SerializeField] private float stateCrossFadeDuration = 0.2f;
        [SerializeField] private float deathCrossFadeDuration = 0.1f;
        [SerializeField] private float dissolveDuration = 0.7f;
        [SerializeField] private float returnToPoolDelay = 1.5f;
        [SerializeField] private float knockbackDistance = 0.3f;
        [SerializeField] private float knockbackDuration = 0.15f;
        [Tooltip("World height above the zombie's origin where the floating damage number pops.")]
        [SerializeField] private float damageNumberHeight = 1.7f;

        private static readonly int DissolveID = Shader.PropertyToID("_Dissolve");

        private NavMeshAgent _agent;
        private Health _health;
        private VAT_Animator _vatAnimator;
        private MaterialPropertyBlock _dissolvePropertyBlock;
        private State _state;
        private ZombieTier _tier = ZombieTier.Full;
        private float _attackCooldownTimer;

        public ZombieData Data => data;
        public ZombieTier Tier => _tier;

        protected NavMeshAgent Agent => _agent;
        protected Health Health => _health;
        protected VAT_Animator Vat => _vatAnimator;
        protected State CurrentState => _state;

        // Distance at which the zombie stops chasing and switches to attacking. Melee uses the
        // data's attack range; ranged types widen this so they open fire from well outside it.
        protected virtual float EngageRange => data.attackRange;

        Transform ITargetable.Transform => transform;
        bool ITargetable.IsTargetable => _state != State.Dead;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            // VAT_Animator lives on the child "Visual" mesh, not the root - search children (incl. inactive).
            _vatAnimator = GetComponentInChildren<VAT_Animator>(true);
            if (_vatAnimator == null)
                Debug.LogError($"[{name}] VAT_Animator missing in children - zombie animation disabled.", this);
            _dissolvePropertyBlock = new MaterialPropertyBlock();
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
            _agent.isStopped = false;
            if (TryGetComponent(out Collider col)) col.enabled = true;
            SetDissolve(0f);
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

        public void TakeDamage(float amount) => _health.TakeDamage(amount);

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
                    if (bodyRenderer != null) bodyRenderer.enabled = true;
                    break;
                case ZombieTier.Cheap:
                    _agent.enabled = false;
                    _vatAnimator.enabled = true;
                    if (bodyRenderer != null) bodyRenderer.enabled = true;
                    break;
                case ZombieTier.Inactive:
                    _agent.enabled = false;
                    _vatAnimator.enabled = false;
                    if (bodyRenderer != null) bodyRenderer.enabled = false;
                    break;
            }
        }

        // Called by ZombieManager at a throttled interval - NOT every frame, NOT via this object's
        // own Update(). Cheap on purpose: no pathing, no target search, no animation change.
        public void CheapTick(Vector3 towardPosition)
        {
            if (_state == State.Dead || _tier != ZombieTier.Cheap) return;
            transform.position = Vector3.MoveTowards(transform.position, towardPosition, data.moveSpeed * Time.deltaTime);
        }

        private void Update()
        {
            if (_tier != ZombieTier.Full || _state == State.Dead) return;

            var player = PlayerMovement.Instance;
            if (player == null) return;

            if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.deltaTime;

            float distance = Vector3.Distance(transform.position, player.transform.position);
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
            if (_attackCooldownTimer > 0f) return;

            _attackCooldownTimer = data.attackCooldown;
            _vatAnimator.CrossFade(data.attackClip, stateCrossFadeDuration);
            PerformAttack(target);
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

            _vatAnimator.Play(data.hitClip);
            StartCoroutine(Knockback());
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

        private void SetDissolve(float amount)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(_dissolvePropertyBlock);
            _dissolvePropertyBlock.SetFloat(DissolveID, amount);
            bodyRenderer.SetPropertyBlock(_dissolvePropertyBlock);
        }

        protected static Vector3 FlattenY(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
