using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar.Skills
{
    /// <summary>
    /// M7.2b — the one thing that ticks the skill system during a run and turns
    /// <see cref="SkillRuntime.PowerProc"/> intent into actual damage on screen.
    ///
    /// Before this existed the card system was complete and entirely inert: the level-up screen
    /// applied ranks, and nothing in combat ever consulted them. There is exactly ONE driver — enemies
    /// do not get an Update, and nothing here allocates per frame.
    ///
    /// Lives on the player so it starts and stops with the run, and so the player's transform is the
    /// natural origin for radial powers.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillCombatDriver : MonoBehaviour
    {
        [Header("Power FX (pooled; each power reads DIFFERENTLY on purpose)")]
        [Tooltip("Electric hit at each chain endpoint. The connecting bolt is drawn by SkillFxDirector.")]
        [SerializeField] private ParticleSystem chainArcFx;
        [Tooltip("Ordnance Core: the blast on the chosen cluster.")]
        [SerializeField] private ParticleSystem explosionFx;
        [Tooltip("Ordnance Core: a marker dropped on the cluster BEFORE it detonates, so the player " +
                 "reads 'that group was chosen' rather than 'something exploded'.")]
        [SerializeField] private ParticleSystem ordnanceMarkerFx;
        [Tooltip("Soul Burst: self-centred burst, fired by kills.")]
        [SerializeField] private ParticleSystem soulBurstFx;
        [Tooltip("Emergency Detonation: must be unmistakable and clearly NOT Soul Burst — it is a " +
                 "panic button that fires when you are nearly dead.")]
        [SerializeField] private ParticleSystem emergencyFx;
        [SerializeField] private ParticleSystem shieldBreakFx;

        [Header("Power tuning — ALL VALUES ARE TUNING, not balance claims")]
        [Tooltip("Damage per chain arc (Chain Lightning / Static Build-up).")]
        [SerializeField] private float chainDamage = 14f;
        [Tooltip("Damage at the centre of an Ordnance Core / Soul Burst / Emergency blast.")]
        [SerializeField] private float blastDamage = 34f;
        [Tooltip("Chain jump range in metres.")]
        [SerializeField] private float chainJumpRange = 7f;
        [Tooltip("Radius the driver sweeps to find candidates for any power.")]
        [SerializeField] private float scanRadius = 22f;
        [Tooltip("Broad-phase layers a power may consider. Narrowing this is an optimisation; the " +
                 "correctness guarantee is the ZombieBase filter in DamageAt.")]
        [SerializeField] private LayerMask enemyMask = ~0;

        static readonly int[] ChainBuffer = new int[TargetQuery.MaxChain];

        Transform _tr;
        Health _health;
        Vector3 _lastPosition;
        bool _wasFiring;
        int _explosionsThisFrame;

        /// <summary>Diagnostics for the play-test and for the guardrail tests.</summary>
        public int TotalProcs { get; private set; }
        public int TotalPowerDamageEvents { get; private set; }
        public float TotalPowerDamage { get; private set; }

        void Awake()
        {
            _tr = transform;
            _health = GetComponent<Health>();
            _lastPosition = _tr.position;

            // The run needs exactly one SkillRuntime and the driver is the thing whose lifetime
            // matches a run, so it provisions one rather than relying on some other system to have
            // done it first. Never replaces an existing runtime (tests inject their own).
            if (SkillRuntime.Active == null) SkillRuntime.Active = new SkillRuntime();
        }

        void OnEnable()
        {
            // Same plumbing PickupManager and MissionTracker already use — no new event system.
            Bill.Events?.Subscribe<ZombieKilledEvent>(OnZombieKilled);
        }

        void OnDisable()
        {
            Bill.Events?.Unsubscribe<ZombieKilledEvent>(OnZombieKilled);
        }

        void OnZombieKilled(ZombieKilledEvent e)
        {
            // Feeds Soul Burst's kill counter. Kill-driven cards were completely dead before this.
            SkillRuntime.Active?.OnKill();
        }

        void Update()
        {
            var run = SkillRuntime.Active;
            if (run == null) return;

            float dt = Time.deltaTime;
            Vector3 pos = _tr.position;
            bool moving = (pos - _lastPosition).sqrMagnitude > 1e-6f;
            _lastPosition = pos;

            float healthFraction = _health != null && _health.Max > 0f ? _health.Current / _health.Max : 1f;

            run.Tick(dt, pos, moving, _wasFiring, healthFraction);
            _wasFiring = false;   // set again by NotifyFiring each frame the weapon shoots

            _explosionsThisFrame = 0;
            var procs = run.PollPowers(Time.time, healthFraction);
            for (int i = 0; i < procs.Count; i++) Apply(procs[i], pos);
        }

        /// <summary>Called by the weapon when it actually fires, so ramp cards see real trigger fire.</summary>
        public void NotifyFiring() => _wasFiring = true;

        // ─────────────────────────────────────────────────────────── power application

        void Apply(in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            TotalProcs++;

            // ONE physics query per proc — the stated guardrail. Every selection below reads the
            // buffer this fills.
            int found = TargetQuery.Gather(origin, scanRadius, enemyMask);
            if (found == 0) return;

            switch (proc.skillId)
            {
                case SkillCatalogDefs.AutoChainLightning:
                case SkillCatalogDefs.SmgStatic:
                    ApplyChain(origin, proc.targets);
                    break;

                case SkillCatalogDefs.AutoOrdnance:
                {
                    // Densest cluster — the query that did not exist anywhere in the runtime before B1.
                    int best = TargetQuery.DensestCluster(found, proc.radius, out _);
                    if (best >= 0)
                    {
                        Vector3 centre = TargetQuery.CandidatePoint(best);
                        // Marker first: the player should see WHICH group was chosen.
                        PlayFx(ordnanceMarkerFx, centre);
                        ApplyBlast(centre, proc.radius, found, explosionFx);
                    }
                    break;
                }

                case SkillCatalogDefs.AutoSoulBurst:
                    // Self-centred, kill-driven. Reads as a pulse coming OUT of the player.
                    ApplyBlast(origin, proc.radius, found, soulBurstFx != null ? soulBurstFx : explosionFx);
                    break;

                case SkillCatalogDefs.AutoEmergency:
                    // Deliberately a different effect from Soul Burst: this one only ever fires when
                    // the player is nearly dead, so it must not be mistaken for a routine proc.
                    // NO heal, NO invulnerability — it is a shove, not a rescue.
                    ApplyBlast(origin, proc.radius, found, emergencyFx != null ? emergencyFx : explosionFx);
                    break;
            }
        }

        void ApplyChain(Vector3 origin, int maxTargets)
        {
            int hops = TargetQuery.Chain(TargetQuery.MaxConsidered, origin, chainJumpRange,
                                         Mathf.Min(maxTargets, TargetQuery.MaxChain), ChainBuffer);
            Vector3 from = origin;
            var fx = SkillFxDirector.Instance;

            for (int i = 0; i < hops; i++)
            {
                Vector3 point = TargetQuery.CandidatePoint(ChainBuffer[i]);
                var col = TargetQuery.Candidate(ChainBuffer[i]);

                // The BOLT is what makes this read as a chain. Without a line drawn between
                // successive targets it is just a flash on each enemy, which is not chain lightning.
                fx?.DrawArc(from + Vector3.up * 1.0f, point + Vector3.up * 1.0f);

                DamageAt(col, point, chainDamage);
                PlayFx(chainArcFx, point);          // electric hit at the endpoint
                from = point;                        // next hop starts where this one landed
            }
        }

        void ApplyBlast(Vector3 centre, float radius, int found, ParticleSystem visual = null)
        {
            // <=2 concurrent explosions is a stated ceiling; enforced here as well as in the FX pool
            // so the DAMAGE cost is bounded too, not only the visual.
            if (_explosionsThisFrame >= 2) return;
            _explosionsThisFrame++;

            float r2 = radius * radius;
            for (int i = 0; i < found; i++)
            {
                Vector3 p = TargetQuery.CandidatePoint(i);
                if ((p - centre).sqrMagnitude > r2) continue;
                DamageAt(TargetQuery.Candidate(i), p, blastDamage);
            }
            PlayFx(visual != null ? visual : explosionFx, centre);
        }

        /// <summary>
        /// Damages an ENEMY only.
        ///
        /// This deliberately resolves <see cref="ZombieBase"/> rather than any <c>IDamageable</c>.
        /// The first play-test of this driver killed the player outright: powers are centred on the
        /// player, the broad-phase sweep returned the player's own collider, and a generic
        /// IDamageable lookup happily applied Chain Lightning to their face. Filtering to the enemy
        /// type is the fix that cannot be re-broken by a mask edit.
        /// </summary>
        void DamageAt(Collider col, Vector3 point, float damage)
        {
            if (col == null) return;
            var enemy = col.GetComponentInParent<ZombieBase>();
            if (enemy == null) return;              // player, scenery, props: never damaged by a power
            enemy.TakeDamage(damage);
            TotalPowerDamageEvents++;
            TotalPowerDamage += damage;
        }

        void PlayFx(ParticleSystem prefab, Vector3 at)
        {
            // FxPool is the project's existing pooled effect path: no Instantiate here, and no
            // runtime material instance.
            if (prefab != null) FxPool.Play(prefab, at, Quaternion.identity);
        }

        /// <summary>Kinetic Shield ate a hit — make it legible, or the card reads as a bug.</summary>
        public void PlayShieldBreak()
        {
            if (shieldBreakFx != null) FxPool.Play(shieldBreakFx, _tr.position, Quaternion.identity);
        }

        public static SkillCombatDriver Instance { get; private set; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Start() { Instance = this; }
    }
}
