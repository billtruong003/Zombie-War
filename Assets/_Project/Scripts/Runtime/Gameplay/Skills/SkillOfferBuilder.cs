using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills
{
    /// <summary>
    /// M7.2 B3 — builds the 1-of-3 level-up offer.
    ///
    /// This is the piece most likely to look right and be subtly wrong, so every rule below is
    /// enforced here and tested directly rather than only through play:
    ///
    /// <list type="bullet">
    /// <item>Slot A prefers a signature card for the EQUIPPED FAMILY (never a weapon id).</item>
    /// <item>Slot B prefers autonomous or universal.</item>
    /// <item>Slot C is any valid card.</item>
    /// <item>At most ONE pure-stat card per offer — the cheapest fix for the Damage→Fire Rate
    /// dominant strategy.</item>
    /// <item>Never an incompatible card, never a max-rank card.</item>
    /// <item>Deterministic from (run seed, level): the same seed produces the same offers.</item>
    /// <item>Early levels favour unlocking new mechanics; later levels favour ranking up what you
    /// already have, so a build converges instead of scattering.</item>
    /// </list>
    /// </summary>
    public static class SkillOfferBuilder
    {
        public const int SlotCount = 3;

        /// <summary>Deterministic hash → the same (seed, level) always yields the same offer.</summary>
        struct Rng
        {
            uint _s;
            public Rng(int seed, int level)
            {
                unchecked { _s = (uint)(seed * 747796405 + level * 2891336453 + 1); }
                if (_s == 0) _s = 1;
            }
            public uint Next()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return _s;
            }
            public int Range(int maxExclusive) => maxExclusive <= 0 ? 0 : (int)(Next() % (uint)maxExclusive);
        }

        static readonly List<SkillDef> Eligible = new(32);
        static readonly List<SkillDef> Scratch = new(32);

        /// <summary>
        /// Every card that could legally be offered right now: compatible with the equipped family,
        /// and not already at max rank.
        /// </summary>
        public static List<SkillDef> EligiblePool(SkillRuntime run, WeaponClass family, List<SkillDef> into = null)
        {
            var list = into ?? new List<SkillDef>();
            list.Clear();
            var all = SkillCatalogDefs.All;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (!def.IsCompatibleWith(family)) continue;
                if (run.RankOf(def.id) >= def.maxRank) continue;
                list.Add(def);
            }
            return list;
        }

        /// <summary>
        /// Builds the offer. Returns fewer than three entries only when the pool genuinely cannot
        /// supply more — an exhausted pool yields an empty list, and the caller must treat that as
        /// "no level-up choice", not as an error.
        /// </summary>
        public static List<SkillDef> Build(SkillRuntime run, WeaponClass family, int runSeed, int level,
                                           List<SkillDef> into = null)
        {
            var offer = into ?? new List<SkillDef>(SlotCount);
            offer.Clear();

            EligiblePool(run, family, Eligible);
            if (Eligible.Count == 0) return offer;

            var rng = new Rng(runSeed, level);
            bool statTaken = false;

            // Early levels want NEW mechanics; later levels want the build to converge.
            bool preferNew = level <= 4;

            // ── Slot A: a signature card for this family ────────────────────────────────
            var pick = PickWeighted(ref rng, run, preferNew, d => d.layer == SkillLayer.Signature);
            if (pick == null) pick = PickWeighted(ref rng, run, preferNew, d => !IsStat(d));
            if (pick == null) pick = PickWeighted(ref rng, run, preferNew, _ => true);
            if (pick != null) { offer.Add(pick); statTaken |= IsStat(pick); Eligible.Remove(pick); }

            // ── Slot B: autonomous or universal ────────────────────────────────────────
            pick = PickWeighted(ref rng, run, preferNew,
                d => d.layer == SkillLayer.Autonomous || d.layer == SkillLayer.Universal);
            if (pick == null) pick = PickWeighted(ref rng, run, preferNew, d => !IsStat(d) || !statTaken);
            if (pick != null) { offer.Add(pick); statTaken |= IsStat(pick); Eligible.Remove(pick); }

            // ── Slot C: anything valid, honouring the one-stat rule ────────────────────
            pick = PickWeighted(ref rng, run, preferNew, d => !IsStat(d) || !statTaken);
            if (pick != null) { offer.Add(pick); Eligible.Remove(pick); }

            return offer;
        }

        static bool IsStat(SkillDef d) => d.layer == SkillLayer.Stat;

        /// <summary>
        /// Weighted pick from the remaining eligible pool. Weight expresses the early/late bias:
        /// an unowned card is worth more early, an owned one is worth more later, so builds start
        /// broad and then deepen.
        /// </summary>
        static SkillDef PickWeighted(ref Rng rng, SkillRuntime run, bool preferNew,
                                     System.Func<SkillDef, bool> filter)
        {
            Scratch.Clear();
            int total = 0;
            for (int i = 0; i < Eligible.Count; i++)
            {
                var d = Eligible[i];
                if (!filter(d)) continue;
                Scratch.Add(d);
                total += Weight(run, d, preferNew);
            }
            if (Scratch.Count == 0 || total <= 0) return null;

            int roll = rng.Range(total);
            for (int i = 0; i < Scratch.Count; i++)
            {
                roll -= Weight(run, Scratch[i], preferNew);
                if (roll < 0) return Scratch[i];
            }
            return Scratch[Scratch.Count - 1];
        }

        static int Weight(SkillRuntime run, SkillDef d, bool preferNew)
        {
            bool owned = run.RankOf(d.id) > 0;
            int w = preferNew ? (owned ? 2 : 6) : (owned ? 6 : 3);
            if (d.status == "LATER") w = Mathf.Max(1, w / 2);   // build-order hint, still offerable
            return w;
        }

        /// <summary>
        /// The ≤30 s pause expired. Picks a VALID card from the offer — never a broken or ineligible
        /// one. Returns null only when the offer itself was empty.
        /// </summary>
        public static SkillDef AutoPick(IReadOnlyList<SkillDef> offer, SkillRuntime run)
        {
            if (offer == null) return null;
            for (int i = 0; i < offer.Count; i++)
            {
                var d = offer[i];
                if (d != null && run.RankOf(d.id) < d.maxRank) return d;
            }
            return null;
        }
    }
}
