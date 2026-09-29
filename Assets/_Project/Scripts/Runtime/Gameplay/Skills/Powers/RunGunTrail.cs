using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Run &amp; Gun (sidearm): cyan footprints of speed while its ramp is up.</summary>
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
            Host.ShowDisc(p, 0.5f, 0.25f, new Color(0.55f, 0.92f, 1f, 0.3f + 0.25f * ramp), 0.35f, false, 0.25f);
        }
    }
}
