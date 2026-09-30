using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Guardian Angel's moment: a fatal hit became a heal. It must be unmistakable — the
    /// player just did not die — so it is the biggest heal on screen, gold, with a ring and a shake.</summary>
    public sealed class GuardianAngelPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            public ParticleSystem healFx;
            public ParticleSystem novaFx;
        }

        static readonly Color Gold = new(1f, 0.86f, 0.35f, 1f);

        public void Save()
        {
            var a = Lib?.guardian;
            var p = Host.Player.position;
            if (a != null)
            {
                FxPool.Play(a.healFx, p + Vector3.up * 0.1f, PowerKit.Flat(a.healFx), 1.1f);
                FxPool.Play(a.novaFx, p + Vector3.up * 0.3f, PowerKit.Flat(a.novaFx), 1f);
            }
            Host.Sfx("sfx.skill.evolve", p, 1f, 0.5f);
            Host.Shake(0.3f);
        }
    }
}
