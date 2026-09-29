using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>
    /// What a power module may ask of the skill host (<see cref="SkillArsenal"/>): the shared, budgeted
    /// services every power uses, so no power owns its own shake, sound gate or ground disc pool.
    /// </summary>
    public interface IPowerHost
    {
        /// Parent for everything a power builds in the world (cleared with the run).
        Transform Root { get; }
        /// The player.
        Transform Player { get; }
        SkillFxLibrary Library { get; }
        LayerMask EnemyMask { get; }

        /// One shared shake budget: three powers landing together shake like one big hit.
        void Shake(float amount);
        /// A power's sound, at most once per <paramref name="minGap"/> seconds per key.
        void Sfx(string key, Vector3 at, float volume = 0.8f, float minGap = 0.08f);
        bool OnScreen(Vector3 world);
        /// A flat soft disc on the ground that grows (or shrinks) and fades (or fades in).
        void ShowDisc(Vector3 at, float fromRadius, float toRadius, Color color, float duration, bool fadeIn, float ring = 0f);
        /// A toon ground shockwave: a ring that races from one radius to another while it erodes away.
        void Shockwave(Vector3 at, float fromRadius, float toRadius, Color color, float duration);
        /// A pooled flat quad with the disc material, for powers that draw their own ground ring.
        MeshRenderer MakeGroundRenderer(string name);
        /// Plays an effect later, in its authored orientation.
        void PlayDelayed(ParticleSystem fx, Vector3 pos, float scale, float delay);
        /// Runs an action later (a sky strike landing when its arc arrives).
        void Delay(float delay, System.Action<Vector3> action, Vector3 at);
        /// A blast that lands after a delay, telegraphed by a closing ring and a falling bomb's shadow.
        void ScheduleBlast(in BlastSpec spec);
        /// A kill's soul flying into the player.
        void SoulWisp(Vector3 from);
        MaterialPropertyBlock Block { get; }
    }

    /// <summary>A delayed area hit (Airstrike, Ordnance, Carpet Bomb).</summary>
    public struct BlastSpec
    {
        public Vector3 pos;
        public float radius, damage, delay, shake, push;
        public ParticleSystem fx;
        public float fxNativeRadius;
        public string sfx;
        /// Optional bomb that falls into the mark in the last moments.
        public ParticleSystem bomb;
        public ParticleSystem decal;
        /// The bombing run's flight line (bombs fall in along it).
        public Vector3 flight;
        /// Ledger source (card id).
        public string source;
        /// A2: a toon shockwave in this colour where it lands (alpha 0 = none).
        public Color wave;
    }

    /// <summary>
    /// One power, in its own file: its state, its visuals, its damage. The host ticks every module
    /// each frame and routes each proc to the module that owns its card id. A new power is a new
    /// module, a section in <see cref="SkillFxLibrary"/> and one line in the host's registry.
    ///
    /// Modules keep the arsenal's contract: visuals sized to the hitbox, a throttled sound, a
    /// telegraph for anything that lands later, a push or tint on what is hit, shakes from the shared
    /// budget, and no allocation per frame in steady state.
    /// </summary>
    public abstract class PowerModule
    {
        protected IPowerHost Host { get; private set; }
        protected SkillFxLibrary Lib => Host.Library;

        /// Card ids whose procs this module handles (empty for purely continuous powers).
        public virtual string[] ProcIds => System.Array.Empty<string>();

        public void Attach(IPowerHost host) { Host = host; OnAttach(); }

        /// Build pooled visuals. Called once, after the host has its root.
        protected virtual void OnAttach() { }

        /// Every unpaused frame.
        public virtual void Tick(SkillRuntime run, Vector3 player, float dt) { }

        /// A proc for one of <see cref="ProcIds"/>. One physics query per proc at most (the module's
        /// own); a targeted power that finds nothing calls <see cref="SkillRuntime.Refund"/>.
        public virtual void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin) { }

        public virtual void OnKill(SkillRuntime run, Vector3 at) { }

        /// The run ended or restarted: drop every live effect.
        public virtual void ResetForRun() { }
    }
}
