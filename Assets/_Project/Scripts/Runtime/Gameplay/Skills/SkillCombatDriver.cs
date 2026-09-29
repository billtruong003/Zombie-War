using UnityEngine;
using BillGameCore;

namespace ZombieWar.Skills
{
    /// <summary>
    /// The one thing that ticks the skill system during a run: it feeds <see cref="SkillRuntime"/>
    /// the player's state, polls the powers, and hands each proc to the power module that owns it
    /// (through <see cref="SkillArsenal.Dispatch"/>). Kills, the shield's hits and overflow cards go
    /// through here too. Nothing here allocates per frame.
    ///
    /// Lives on the player so it starts and stops with the run, and so the player's transform is the
    /// natural origin for radial powers.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillCombatDriver : MonoBehaviour
    {
        /// <summary>
        /// How long after a shot the weapon still counts as "firing" for the ramp cards.
        ///
        /// Shots land on a few frames per second, not every frame. Reading "fired this frame" meant a
        /// 10 shots/s gun was idle on five frames out of six, so Bullet Hose and Heavy Pressure rose
        /// for one frame and decayed for five and never ramped. Sustained fire is a window, not a frame.
        /// </summary>
        public const float FiringGraceSeconds = 0.35f;

        public static SkillCombatDriver Instance { get; private set; }

        /// <summary>Procs handed to a power module, for the play-test and the guardrail tests.</summary>
        public int TotalProcs { get; private set; }

        Transform _tr;
        Health _health;
        Vector3 _lastPosition;
        float _lastShotTime = float.NegativeInfinity;
        SkillArsenal _arsenal;

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

        void Start()
        {
            Instance = this;
            // Cards above the player's account level are not offered this run (the sandbox lifts this).
            if (SkillRuntime.Active != null) SkillRuntime.Active.UnlockLevel = PlayerProfile.AccountLevel;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable() => Bill.Events?.Subscribe<ZombieKilledEvent>(OnZombieKilled);
        void OnDisable() => Bill.Events?.Unsubscribe<ZombieKilledEvent>(OnZombieKilled);

        void OnZombieKilled(ZombieKilledEvent e)
        {
            var run = SkillRuntime.Active;
            if (run == null) return;
            run.OnKill();                    // Soul Burst's kill counter
            _arsenal?.OnKill(run, e.Position);
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
            bool firing = Time.time - _lastShotTime <= FiringGraceSeconds;
            run.Tick(dt, pos, moving, firing, healthFraction);

            var procs = run.PollPowers(Time.time, healthFraction);
            for (int i = 0; i < procs.Count; i++) Apply(run, procs[i], pos);

            ApplyOverflow(run, pos);
            ApplyRegen(run, dt);
        }

        float _regenOwed;

        void ApplyRegen(SkillRuntime run, float dt)
        {
            float rate = run.RegenPerSecond;
            if (rate <= 0f || _health == null || _health.IsDead) { _regenOwed = 0f; return; }
            _regenOwed += rate * dt;
            if (_regenOwed < rate) return;          // pay once a second's worth has built up
            if (_health.Current < _health.Max) _health.Heal(_health.Max * _regenOwed);
            _regenOwed = 0f;
        }

        /// <summary>One proc to the module that owns its card (sandbox capture calls this too).</summary>
        void Apply(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            TotalProcs++;
            if (_arsenal == null || !_arsenal.Dispatch(run, proc, origin))
                Debug.LogWarning($"[Skills] no power module handles {proc.skillId}");
        }

        /// <summary>
        /// Overflow cards taken this frame (from a level-up, and later a chest or a station) act here,
        /// whatever screen handed them out.
        /// </summary>
        void ApplyOverflow(SkillRuntime run, Vector3 pos)
        {
            float heal = run.ConsumeHealFraction();
            if (heal > 0f && _health != null)
            {
                _health.Heal(_health.Max * heal);
                SkillFxDirector.Instance?.Pulse(pos, 1.8f, new Color(0.35f, 1f, 0.5f, 0.9f), 0.45f, 0.25f);
            }
            int coin = run.ConsumeCoin();
            if (coin > 0) RunState.Current?.AddCurrency(PlayerProfile.CurrencyKind.Coin, coin);
            if (run.ConsumeMagnet()) PickupManager.BeginMagnetSweep();
        }

        /// <summary>Called by the weapon when it actually fires, so ramp cards see real trigger fire.</summary>
        public void NotifyFiring() => _lastShotTime = Time.time;

        /// <summary>The level-up screen took an evolution: mark the moment on the player.</summary>
        public void OnEvolutionTaken() => _arsenal?.PlayEvolve();

        /// <summary>Kinetic Shield ate a hit — make it legible, or the card reads as a bug.</summary>
        public void PlayShieldBreak() => _arsenal?.OnShieldBlocked();

        /// <summary>Guardian Angel turned a fatal hit into a heal.</summary>
        public void PlayGuardianAngel() => _arsenal?.OnGuardianAngel();
    }
}
