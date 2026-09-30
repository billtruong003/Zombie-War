using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Run &amp; Gun (sidearm): Epic Toon dust puffs at the feet while its ramp is up.</summary>
    public sealed class RunGunTrail : PowerModule
    {
        Vector3 _last;
        float _next;

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            float ramp = run.RunGunRamp;
            bool moving = (p - _last).sqrMagnitude > 0.0004f;
            _last = p;
            if (ramp < 0.3f || !moving || Time.time < _next) return;
            _next = Time.time + 0.12f;
            var dust = Lib != null ? Lib.shared.dustFx : null;
            if (dust != null) FxPool.Play(dust, p, PowerKit.Flat(dust), 0.35f + 0.2f * ramp);
        }
    }
}
