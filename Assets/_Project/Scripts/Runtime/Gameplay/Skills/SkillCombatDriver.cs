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

        [Header("Power FX sizes (M8: effects are scaled to the area they hit)")]
        [SerializeField] private float explosionNativeRadius = 1.5f;
        [SerializeField] private float soulBurstNativeRadius = 1.6f;
        [SerializeField] private float emergencyNativeRadius = 1.5f;
        [SerializeField] private float ordnanceMarkerNativeRadius = 1.2f;
        [Tooltip("Ordnance: seconds between the marker and the shell. The instant blast had no " +
                 "anticipation — the player saw an explosion, never a strike.")]
        [SerializeField] private float ordnanceDelay = 0.45f;

        [Header("Power tuning — ALL VALUES ARE TUNING, not balance claims")]
        [Tooltip("Damage per chain arc (Chain Lightning / Static Build-up).")]
        [SerializeField] private float chainDamage = 14f;
        [Tooltip("Damage at the centre of an Ordnance Core / Soul Burst / Emergency blast.")]
        [SerializeField] private float blastDamage = 34f;
        [Tooltip("Chain jump range in metres, between enemies.")]
        [SerializeField] private float chainJumpRange = 7f;
        [Tooltip("How far the FIRST arc may reach from the player. Measured: at 7 m a crowd standing " +
                 "8 m away got nothing — the power fired, cost its cooldown and drew no bolt.")]
        [SerializeField] private float chainFirstReach = 12f;
        [Tooltip("Radius the driver sweeps to find candidates for any power.")]
        [SerializeField] private float scanRadius = 22f;
        [Tooltip("Broad-phase layers a power may consider. Narrowing this is an optimisation; the " +
                 "correctness guarantee is the ZombieBase filter in DamageAt.")]
        [SerializeField] private LayerMask enemyMask = ~0;

        static readonly int[] ChainBuffer = new int[TargetQuery.MaxChain];

        /// <summary>
        /// How long after a shot the weapon still counts as "firing" for the ramp cards.
        ///
        /// Shots land on a few frames per second, not every frame. Reading "fired this frame" meant a
        /// 10 shots/s gun was idle on five frames out of six, so Bullet Hose and Heavy Pressure rose
        /// for one frame and decayed for five and never ramped. Sustained fire is a window, not a frame.
        /// </summary>
        public const float FiringGraceSeconds = 0.35f;

        Transform _tr;
        Health _health;
        Vector3 _lastPosition;
        float _lastShotTime = float.NegativeInfinity;
        int _explosionsThisFrame;

        /// <summary>Diagnostics for the play-test and for the guardrail tests.</summary>
        public int TotalProcs { get; private set; }
        public int TotalPowerDamageEvents { get; private set; }
        public float TotalPowerDamage { get; private set; }

        SkillArsenal _arsenal;
        System.Predicate<Vector3> _onScreen;

        void Awake()
        {
            _tr = transform;
            _health = GetComponent<Health>();
            _lastPosition = _tr.position;
            _arsenal = GetComponent<SkillArsenal>();
            if (_arsenal == null) _arsenal = gameObject.AddComponent<SkillArsenal>();

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

        float _reaperBudgetAt;

        void OnZombieKilled(ZombieKilledEvent e)
        {
            // Feeds Soul Burst's kill counter. Kill-driven cards were completely dead before this.
            var run = SkillRuntime.Active;
            if (run == null) return;
            run.OnKill();

            // Reaper: a kill can release a soul burst where the enemy fell. Budgeted to 4/s so a
            // chain of kills cannot cascade into a screen-wide wipe in one frame.
            if (run.RollReaper() && Time.time >= _reaperBudgetAt && _arsenal != null)
            {
                _reaperBudgetAt = Time.time + 0.25f;
                _arsenal.ReaperBurst(run, e.Position, run.PowerDamage(blastDamage * 0.5f, SkillCatalogDefs.AutoSoulBurst));
            }
        }

        /// <summary>The level-up screen took an evolution: mark the moment on the player.</summary>
        public void OnEvolutionTaken() => _arsenal?.PlayEvolve();

        void Update()
        {
            var run = SkillRuntime.Active;
            if (run == null) return;

            float dt = Time.deltaTime;
            Vector3 pos = _tr.position;
            bool moving = (pos - _lastPosition).sqrMagnitude > 1e-6f;
            _lastPosition = pos;

            float healthFraction = _health != null && _health.Max > 0f ? _health.Current / _health.Max : 1f;

            bool firing = Time.time - _lastShotTime <= FiringGraceSeconds;
            run.Tick(dt, pos, moving, firing, healthFraction);

            _explosionsThisFrame = 0;
            var procs = run.PollPowers(Time.time, healthFraction);
            for (int i = 0; i < procs.Count; i++) Apply(run, procs[i], pos);
        }

        /// <summary>Called by the weapon when it actually fires, so ramp cards see real trigger fire.</summary>
        public void NotifyFiring() => _lastShotTime = Time.time;

        // ─────────────────────────────────────────────────────────── power application

        void Apply(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            TotalProcs++;

            // Powers that fire whether or not anything is in range: a frost ring on an empty street
            // still reads as "my skill works", and the boomerang/airstrike pick their own targets.
            switch (proc.skillId)
            {
                case SkillCatalogDefs.AutoFrostNova:
                    _arsenal?.FrostNova(run, origin, proc.radius);
                    return;
                case SkillCatalogDefs.AutoBoomerang:
                    _arsenal?.ThrowBoomerangs(run, origin, proc.targets);
                    return;
                case SkillCatalogDefs.AutoAirstrike:
                    _arsenal?.Airstrike(run, origin, proc.targets, proc.radius);
                    return;
            }

            // ONE physics query per proc — the stated guardrail. Every selection below reads the
            // buffer this fills.
            int found = TargetQuery.GatherEnemies(origin, scanRadius, enemyMask);
            if (found == 0) { run.Refund(proc.skillId); return; }

            switch (proc.skillId)
            {
                case SkillCatalogDefs.AutoChainLightning:
                case SkillCatalogDefs.SmgStatic:
                    ApplyChain(run, proc.skillId, origin, proc.targets, found);
                    break;

                case SkillCatalogDefs.AutoOrdnance:
                {
                    // The shell lands where the player can watch it: the densest cluster ON SCREEN.
                    if (_arsenal != null) found = TargetQuery.Compact(found, _onScreen ??= _arsenal.OnScreen);
                    if (found == 0) { run.Refund(proc.skillId); break; }
                    // Densest cluster — the query that did not exist anywhere in the runtime before B1.
                    int best = TargetQuery.DensestCluster(found, proc.radius, out _);
                    if (best < 0) break;
                    Vector3 centre = TargetQuery.CandidatePoint(best);
                    float damage = run.PowerDamage(blastDamage, SkillCatalogDefs.AutoOrdnance);
                    _arsenal?.RequestSfx("sfx.skill.target", centre, 0.5f, 0.2f);

                    if (run.IsEvolved(SkillCatalogDefs.AutoOrdnance))
                    {
                        // Carpet Bomb: three shells in a line across the crowd, landing in sequence.
                        Vector3 toCrowd = centre - origin; toCrowd.y = 0f;
                        Vector3 across = toCrowd.sqrMagnitude > 0.01f
                            ? Vector3.Cross(Vector3.up, toCrowd.normalized) : Vector3.right;
                        for (int k = -1; k <= 1; k++)
                            _arsenal?.ScheduleBlast(centre + across * (k * proc.radius * 1.2f), proc.radius, damage,
                                ordnanceDelay + (k + 1) * 0.12f, explosionFx, explosionNativeRadius,
                                "sfx.skill.blast", 0.2f, 1.3f, ordnanceMarkerFx, ordnanceMarkerNativeRadius);
                    }
                    else
                    {
                        // Marker first, shell after: the player sees WHICH group was chosen, then
                        // watches it get hit.
                        _arsenal?.ScheduleBlast(centre, proc.radius, damage, ordnanceDelay, explosionFx,
                            explosionNativeRadius, "sfx.skill.blast", 0.18f, 1.2f,
                            ordnanceMarkerFx, ordnanceMarkerNativeRadius);
                    }
                    break;
                }

                case SkillCatalogDefs.AutoSoulBurst:
                    // Self-centred, kill-driven. Reads as a pulse coming OUT of the player.
                    ApplyBlast(run, SkillCatalogDefs.AutoSoulBurst, origin, proc.radius, found,
                               soulBurstFx != null ? soulBurstFx : explosionFx, soulBurstNativeRadius,
                               "sfx.skill.soulburst", 0.1f, 1f);
                    break;

                case SkillCatalogDefs.AutoEmergency:
                    // Deliberately a different effect from Soul Burst: this one only ever fires when
                    // the player is nearly dead, so it must not be mistaken for a routine proc.
                    // NO heal, NO invulnerability — it is a shove, not a rescue.
                    ApplyBlast(run, SkillCatalogDefs.AutoEmergency, origin, proc.radius, found,
                               emergencyFx != null ? emergencyFx : explosionFx, emergencyNativeRadius,
                               "sfx.skill.emergency", 0.4f, 3f);
                    break;
            }
        }

        void ApplyChain(SkillRuntime run, string skillId, Vector3 origin, int maxTargets, int found)
        {
            bool storm = skillId == SkillCatalogDefs.AutoChainLightning && run.IsEvolved(skillId);
            float damage = run.PowerDamage(chainDamage, skillId == SkillCatalogDefs.SmgStatic
                ? SkillCatalogDefs.SmgStatic : SkillCatalogDefs.AutoChainLightning);
            // Only the candidates THIS proc gathered. Passing the whole buffer (as before) walked
            // stale entries from earlier queries: arcs to enemies long dead or already pooled.
            int hops = TargetQuery.Chain(found, origin, chainJumpRange,
                                         Mathf.Min(maxTargets, TargetQuery.MaxChain), ChainBuffer, chainFirstReach);
            if (hops == 0) { run.Refund(skillId); return; }
            _arsenal?.RequestSfx(storm ? "sfx.skill.thunderstorm" : "sfx.skill.chain", origin, 0.75f, 0.1f);
            Vector3 from = origin;
            var fx = SkillFxDirector.Instance;

            for (int i = 0; i < hops; i++)
            {
                Vector3 point = TargetQuery.CandidatePoint(ChainBuffer[i]);
                var col = TargetQuery.Candidate(ChainBuffer[i]);

                // The BOLT is what makes this read as a chain. Without a line drawn between
                // successive targets it is just a flash on each enemy, which is not chain lightning.
                // Two lines: a saturated glow and a white core, so it reads on white skeletons too.
                Vector3 a0 = from + Vector3.up * 1.0f, a1 = point + Vector3.up * 1.0f;
                fx?.DrawArc(a0, a1, storm ? new Color(0.45f, 0.35f, 1f, 1f) : new Color(0.2f, 0.55f, 1f, 1f), storm ? 1.8f : 1.3f);
                fx?.DrawArc(a0, a1, Color.white, storm ? 0.6f : 0.45f);
                if (storm && i == 0) _arsenal?.SkyStrike(point);

                DamageAt(col, point, damage, 0.3f);
                PlayFx(chainArcFx, point);          // electric hit at the endpoint
                from = point;                        // next hop starts where this one landed
            }
        }

        void ApplyBlast(SkillRuntime run, string powerId, Vector3 centre, float radius, int found,
                        ParticleSystem visual, float nativeRadius, string sfx, float shake, float push)
        {
            // <=2 concurrent explosions is a stated ceiling; enforced here as well as in the FX pool
            // so the DAMAGE cost is bounded too, not only the visual.
            if (_explosionsThisFrame >= 2) return;
            _explosionsThisFrame++;

            float damage = run.PowerDamage(blastDamage, powerId);
            float r2 = radius * radius;
            for (int i = 0; i < found; i++)
            {
                Vector3 p = TargetQuery.CandidatePoint(i);
                if ((p - centre).sqrMagnitude > r2) continue;
                DamageAt(TargetQuery.Candidate(i), p, damage, push);
            }
            // Sized to the blast: the ring the player sees IS the area that was hit.
            if (visual != null)
                FxPool.Play(visual, centre + Vector3.up * 0.1f, Quaternion.identity, radius / Mathf.Max(0.1f, nativeRadius));
            _arsenal?.RequestSfx(sfx, centre, 0.85f, 0.1f);
            _arsenal?.RequestShake(shake);
            SkillFxDirector.Instance?.Pulse(centre, radius,
                powerId == SkillCatalogDefs.AutoEmergency ? new Color(1f, 0.35f, 0.55f, 1f) : new Color(0.45f, 1f, 0.55f, 1f),
                0.35f, powerId == SkillCatalogDefs.AutoEmergency ? 0.5f : 0.35f);
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
        void DamageAt(Collider col, Vector3 point, float damage, float push = 0f)
        {
            if (col == null) return;
            var enemy = col.GetComponentInParent<ZombieBase>();
            if (enemy == null) return;              // player, scenery, props: never damaged by a power
            enemy.TakeDamage(damage);
            if (push > 0f && !enemy.IsDead) enemy.ApplyPhysicalPush(push);
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
            if (shieldBreakFx != null) FxPool.Play(shieldBreakFx, _tr.position + Vector3.up * 0.9f, Quaternion.identity, 1.3f);
            _arsenal?.RequestSfx("sfx.skill.shield.break", _tr.position, 0.8f, 0.1f);
            _arsenal?.RequestShake(0.12f);
        }

        public static SkillCombatDriver Instance { get; private set; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Start() { Instance = this; }
    }
}
