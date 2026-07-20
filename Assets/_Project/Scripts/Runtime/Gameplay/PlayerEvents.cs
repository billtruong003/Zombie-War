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

    /// <summary>Fired once when the player's health reaches zero. The game-over screen,
    /// audio stinger and wave system all react to this through Bill.Events.</summary>
    public readonly struct PlayerDiedEvent : IEvent { }

    /// <summary>Fired after the player's death sequence (anim + FX + delay) finishes.
    /// The lose screen and zombie/pool cleanup react to THIS, not to PlayerDiedEvent,
    /// so the corpse gets its on-screen moment before the UI takes over.</summary>
    public readonly struct GameOverEvent : IEvent { }
}
