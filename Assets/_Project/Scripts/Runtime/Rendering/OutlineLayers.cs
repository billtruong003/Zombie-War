using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// The rendering layers that pick what the outline draws, looked up by NAME
    /// (Project Settings ▸ Tags and Layers ▸ Rendering Layers). Code never hard-codes the bit:
    /// renaming or moving a layer there is the only place to change.
    ///
    /// 2026-10-02: the old selection was a GameObject <c>LayerMask</c> holding rendering-layer bits.
    /// GameObject layer 3 is unnamed, so Unity dropped bit 3 (the player) whenever the outline profile
    /// loaded — the saved mask 30 came back as 22 and the player lost its outline.
    /// </summary>
    public static class OutlineLayers
    {
        public const string Weapon = "Outline Weapon";
        public const string Enemy = "Outline Enemy";
        public const string Player = "Outline Player";
        public const string Environment = "Outline Environment";

        /// <summary>Every layer the gameplay outline draws.</summary>
        public static readonly string[] Selected = { Weapon, Enemy, Player, Environment };

        public static uint WeaponBit => Bit(Weapon);
        public static uint EnemyBit => Bit(Enemy);
        public static uint PlayerBit => Bit(Player);
        public static uint EnvironmentBit => Bit(Environment);

        /// <summary>Weapon | Enemy | Player — the character half of the selection.</summary>
        public static uint CharacterMask => WeaponBit | EnemyBit | PlayerBit;

        /// <summary>Everything the production outline selects.</summary>
        public static uint SelectionMask => CharacterMask | EnvironmentBit;

        /// <summary>The bit of a named rendering layer, or 0 (with an error) if no layer has that name.</summary>
        public static uint Bit(string name)
        {
            int index = RenderingLayerMask.NameToRenderingLayer(name);
            if (index < 0)
            {
                Debug.LogError($"[Outline] No rendering layer named '{name}' (Project Settings ▸ Tags and Layers ▸ Rendering Layers).");
                return 0u;
            }
            return 1u << index;
        }
    }
}
