using BillGameCore;

namespace ZombieWar
{
    /// <summary>Fired whenever the player takes damage. HUD health bars subscribe to this
    /// so the UI never needs a direct reference to the player's Health component.</summary>
    public readonly struct PlayerDamagedEvent : IEvent
    {
        public readonly float Amount;
        public readonly float Current;
        public readonly float Max;

        public PlayerDamagedEvent(float amount, float current, float max)
        {
            Amount = amount;
            Current = current;
            Max = max;
        }

        public float Normalized => Max > 0f ? Current / Max : 0f;
    }

    /// <summary>Fired on every change of the player's current or max health (hits, heals, revive,
    /// Max Health cards, run start). Displays follow this; <see cref="PlayerDamagedEvent"/> stays the
    /// signal for hit reactions (sound, haptics, radio).</summary>
    public readonly struct PlayerHealthChangedEvent : IEvent
    {
        public readonly float Current;
        public readonly float Max;

        public PlayerHealthChangedEvent(float current, float max)
        {
            Current = current;
            Max = max;
        }

        public float Normalized => Max > 0f ? Current / Max : 0f;
    }

    /// <summary>Fired once when the player's health reaches zero. The game-over screen,
    /// audio stinger and wave system all react to this through Bill.Events.</summary>
    public readonly struct PlayerDiedEvent : IEvent { }

    /// <summary>Fired once per enemy death, after the run ledger has already banked the kill.
    /// Pass missions and audio/analytics listen here so they never need a ZombieBase reference.
    /// Carries the ZombieData so subscribers can read archetype/elite without a scene lookup.</summary>
    public readonly struct ZombieKilledEvent : IEvent
    {
        public readonly ZombieData Data;
        /// <summary>Where it died - loot drops here, so the event has to carry it.</summary>
        public readonly UnityEngine.Vector3 Position;
        /// <summary>The enemy that died. It is still active when this fires (the dissolve runs
        /// afterwards), so a listener can identify it but must not assume it is gone yet.</summary>
        public readonly ZombieBase Source;

        public ZombieKilledEvent(ZombieData data, UnityEngine.Vector3 position, ZombieBase source)
        {
            Data = data;
            Position = position;
            Source = source;
        }
    }

    /// <summary>Fired after the player's death sequence (anim + FX + delay) finishes.
    /// The lose screen and zombie/pool cleanup react to THIS, not to PlayerDiedEvent,
    /// so the corpse gets its on-screen moment before the UI takes over.</summary>
    public readonly struct GameOverEvent : IEvent { }
}
