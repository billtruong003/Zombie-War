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

    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(VAT_Animator))]
    public class ZombieAI : MonoBehaviour, IDamageable, ITargetable
    {
        private enum State { Idle, Chase, Attack, Dead }

        [SerializeField] private ZombieData data;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private float stateCrossFadeDuration = 0.2f;
        [SerializeField] private float deathCrossFadeDuration = 0.1f;
        [SerializeField] private float dissolveDuration = 0.7f;
        [SerializeField] private float returnToPoolDelay = 1.5f;
        [SerializeField] private float knockbackDistance = 0.3f;
        [SerializeField] private float knockbackDuration = 0.15f;

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

        Transform ITargetable.Transform => transform;
        bool ITargetable.IsTargetable => _state != State.Dead;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _vatAnimator = GetComponent<VAT_Animator>();
            _dissolvePropertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            TargetRegistry.Register(this);
            ZombieManager.Register(this);
            _health.OnDamaged += HandleDamaged;
            _health.OnDeath += HandleDeath;

            _health.ResetHealth();
            _state = State.Idle;
            _tier = ZombieTier.Full;
            _agent.isStopped = false;
            if (TryGetComponent(out Collider col)) col.enabled = true;
            SetDissolve(0f);
        }

        private void OnDisable()
        {
            TargetRegistry.Unregister(this);
            ZombieManager.Unregister(this);
            _health.OnDamaged -= HandleDamaged;
            _health.OnDeath -= HandleDeath;
            StopAllCoroutines();
        }

        public void TakeDamage(float amount) => _health.TakeDamage(amount);

        // Deliberately never calls gameObject.SetActive(false) here - that would fire OnDisable(),
        // which unregisters from ZombieManager, and an Inactive zombie would then never be found
        // again to reactivate when the player comes back. Instead, Inactive disables the actually
        // expensive components (agent, VAT playback, rendering) while the ZombieAI behaviour itself
        // (and its registration) stays alive - true SetActive(false) is reserved for the pool-return
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
            SwitchState(distance <= data.attackRange ? State.Attack : State.Chase);
            RunCurrentState(player.transform);
        }

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

        private void RunCurrentState(Transform target)
        {
            switch (_state)
            {
                case State.Chase:
                    _agent.SetDestination(target.position);
                    break;
                case State.Attack:
                    transform.rotation = Quaternion.LookRotation(FlattenY(target.position - transform.position));
                    TryAttack(target);
                    break;
            }
        }

        private void TryAttack(Transform target)
        {
            if (_attackCooldownTimer > 0f) return;

            _attackCooldownTimer = data.attackCooldown;
            _vatAnimator.CrossFade(data.attackClip, stateCrossFadeDuration);
            target.GetComponentInParent<IDamageable>()?.TakeDamage(data.damage);
        }

        private void HandleDamaged(float amount)
        {
            if (_state == State.Dead) return;

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

        private static Vector3 FlattenY(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
