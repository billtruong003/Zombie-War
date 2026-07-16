using UnityEngine;

namespace ZombieWar
{
    public interface ITargetable
    {
        Transform Transform { get; }
        bool IsTargetable { get; }
    }
}
