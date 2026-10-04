using UnityEngine;

namespace ZombieWar
{
    public interface ITargetable
    {
        Transform Transform { get; }
        bool IsTargetable { get; }
        /// <summary>Metres taken off this target's distance when auto-aim picks the nearest: a
        /// supply crate (05/10) wins once the player walks up to it, enemies use 0.</summary>
        float AimBias => 0f;
    }
}
