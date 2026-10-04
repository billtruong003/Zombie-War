using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Owner 05/10, genre rule 9 ("help feels like luck"): kills drop what the player needs right now,
    /// rarely enough that it reads as luck. Four needs are scored on every kill: health, being
    /// surrounded (bomb), being chased by something dangerous (freeze clock) and loot left on the
    /// ground (magnet). A player below <see cref="RescueBelow"/> health is always rescued by a heal
    /// within <see cref="RescueForceAfter"/> seconds, but at most <see cref="MaxRescues"/> times a run
    /// and never twice inside <see cref="RescueCooldown"/>, so it never looks scripted.
    /// Pure state and maths: <see cref="PickupManager"/> gathers the context and spawns the result.
    /// </summary>
    public sealed class NeedDrops
    {
        public struct Context
        {
            public float healthFraction;   // 0..1
            public int crowdNear;          // living enemies within 4 m
            public bool dangerNear;        // an elite, boss or pouncer pack closing in
            public int looseOrbs;          // XP orbs waiting on the ground
            public bool elite;             // the kill was an elite or boss
        }

        public struct Result
        {
            public bool heal;
            public bool rescue;            // the heal is a low-health rescue
            public PickupEffect item;      // Currency = no item
        }

        public const float HealPerMissing = 0.035f;  // per-kill heal chance at 0 health, linear in what is missing
        public const int HealPityKills = 40;         // below half health, this many kills without a heal force one
        public const float RescueBelow = 0.25f;
        public const float RescueAfter = 1.5f;       // seconds low before a kill may rescue
        public const float RescueForceAfter = 8f;    // seconds low before a heal is placed by the player
        public const float RescueCooldown = 15f;
        public const int MaxRescues = 4;
        public const float ItemBase = 0.003f;        // per-kill item chance with no need at all
        public const float ItemPerNeed = 0.025f;     // added at a full need score
        public const float ItemCooldown = 12f;
        public const float EliteHealAbove = 0.2f;    // an elite also drops a heal past this much missing

        int _killsSinceHeal;
        float _lowSince = -1f, _lastRescueAt = -999f, _lastItemAt = -999f;
        int _rescues;

        public int Rescues => _rescues;

        public void Reset()
        {
            _killsSinceHeal = 0;
            _lowSince = -1f; _lastRescueAt = -999f; _lastItemAt = -999f;
            _rescues = 0;
        }

        /// <summary>Follows the player's health between kills. True when a rescue heal must be
        /// placed now because no kill delivered one in time.</summary>
        public bool Tick(float healthFraction, float now)
        {
            if (healthFraction >= RescueBelow || healthFraction <= 0f) { _lowSince = -1f; return false; }
            if (_lowSince < 0f) _lowSince = now;
            if (!RescueReady(now) || now - _lowSince < RescueForceAfter) return false;
            MarkRescued(now);
            return true;
        }

        public Result OnKill(in Context c, float now, float rollHeal, float rollItem)
        {
            var r = new Result { item = PickupEffect.Currency };
            float missing = 1f - Mathf.Clamp01(c.healthFraction);

            _killsSinceHeal++;
            if (c.healthFraction < RescueBelow && _lowSince >= 0f && now - _lowSince >= RescueAfter && RescueReady(now))
            {
                r.heal = r.rescue = true;
                MarkRescued(now);
            }
            else if (rollHeal < HealPerMissing * missing
                     || (c.healthFraction < 0.5f && _killsSinceHeal >= HealPityKills)
                     || (c.elite && missing > EliteHealAbove))
                r.heal = true;
            if (r.heal) _killsSinceHeal = 0;

            var best = BestItem(c, out float need);
            if (c.elite) { r.item = best; _lastItemAt = now; }
            else if (now - _lastItemAt >= ItemCooldown && rollItem < ItemBase + ItemPerNeed * need)
            {
                r.item = best;
                _lastItemAt = now;
            }
            return r;
        }

        /// <summary>The item the player needs most and how much (0..1). With no need, a magnet.</summary>
        public static PickupEffect BestItem(in Context c, out float need)
        {
            float bomb = Mathf.Clamp01((c.crowdNear - 6) / 10f);
            float freeze = c.dangerNear ? 0.8f : 0f;
            float magnet = Mathf.Clamp01((c.looseOrbs - 20) / 60f);
            need = Mathf.Max(bomb, Mathf.Max(freeze, magnet));
            if (need <= 0f) return PickupEffect.Magnet;
            return bomb >= freeze && bomb >= magnet ? PickupEffect.Bomb : freeze >= magnet ? PickupEffect.Freeze : PickupEffect.Magnet;
        }

        bool RescueReady(float now) => _rescues < MaxRescues && now - _lastRescueAt >= RescueCooldown;

        void MarkRescued(float now)
        {
            _rescues++;
            _lastRescueAt = now;
            _lowSince = -1f;
            _killsSinceHeal = 0;
        }
    }
}
