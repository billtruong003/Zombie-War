using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Navigation and fallback rules for the campaign selector, with no UI and no Unity lifecycle so
    /// they can be tested directly.
    ///
    /// Everything here is derived from the catalog at call time - count, order and lock state are
    /// read, never cached or assumed. That is what keeps the selector data-driven: adding, removing
    /// or reordering a catalog entry changes the selector with no code change.
    /// </summary>
    public static class CampaignSelection
    {
        /// <summary>Sentinel for "this catalog has nothing selectable".</summary>
        public const int NoSelection = -1;

        /// <summary>
        /// Where the selector should open.
        ///
        /// Prefers the saved selection, but only while it is still real AND still playable - a saved
        /// id can outlive the entry it named (catalog edited) or point at a stage that is no longer
        /// reachable (profile reset). Either way the fallback is the furthest stage the player has
        /// actually earned, and ultimately Stage 1, so the selector can never open on a locked or
        /// missing stage.
        /// </summary>
        public static int ResolveInitialIndex(CampaignCatalog catalog, string savedLevelId, int playerPower)
        {
            if (catalog == null || catalog.Count == 0) return NoSelection;

            if (!string.IsNullOrEmpty(savedLevelId))
            {
                int saved = catalog.IndexOf(savedLevelId);
                if (saved >= 0 && catalog.Evaluate(saved, playerPower).CanPlay) return saved;
            }

            return HighestPlayableIndex(catalog, playerPower);
        }

        /// <summary>
        /// The furthest stage currently playable. Locks are sequential, so this is the end of the
        /// unlocked run rather than a search of the whole list - but it still stops at the first
        /// locked entry so a data error cannot hand back an unreachable stage.
        /// </summary>
        public static int HighestPlayableIndex(CampaignCatalog catalog, int playerPower)
        {
            if (catalog == null || catalog.Count == 0) return NoSelection;

            int best = NoSelection;
            for (int i = 0; i < catalog.Count; i++)
            {
                if (!catalog.Evaluate(i, playerPower).CanPlay) break;
                best = i;
            }

            // Stage 1 is always open by design; if even that failed the catalog is malformed, and
            // reporting no selection is safer than launching something unauthored.
            return best;
        }

        /// <summary>
        /// One step in <paramref name="direction"/>, or the current index unchanged.
        ///
        /// Deliberately returns the current index rather than clamping to a different valid stage:
        /// at a boundary, or against a locked stage, the correct behaviour is that the press does
        /// nothing. No wrapping - the last stage's right arrow is inert, not a jump back to Stage 1.
        /// </summary>
        public static int Step(CampaignCatalog catalog, int current, int direction, int playerPower)
        {
            if (catalog == null || catalog.Count == 0) return NoSelection;
            if (direction == 0) return current;

            int target = current + (direction > 0 ? 1 : -1);
            if (target < 0 || target >= catalog.Count) return current;      // no wrap
            if (!catalog.Evaluate(target, playerPower).CanPlay) return current;  // locked: refuse

            return target;
        }

        /// <summary>Whether the arrow for <paramref name="direction"/> should read as usable.</summary>
        public static bool CanStep(CampaignCatalog catalog, int current, int direction, int playerPower)
            => Step(catalog, current, direction, playerPower) != current;

        /// <summary>
        /// Clamps a held selection back into something valid. Used when returning to the Hub: a
        /// selection made earlier is still fine, but the catalog or profile may have moved under it.
        /// </summary>
        public static int Revalidate(CampaignCatalog catalog, int current, int playerPower)
        {
            if (catalog == null || catalog.Count == 0) return NoSelection;
            if (current >= 0 && current < catalog.Count && catalog.Evaluate(current, playerPower).CanPlay)
                return current;

            return HighestPlayableIndex(catalog, playerPower);
        }

        /// <summary>How a stage should read in the dot row. Presentation reads this rather than
        /// re-deriving the rules, so the view and the gate can never disagree.</summary>
        public enum DotState { Locked, Available, Completed, Selected }

        public static DotState StateFor(CampaignCatalog catalog, int index, int selectedIndex, int playerPower)
        {
            var level = catalog?.Get(index);
            if (level == null) return DotState.Locked;

            // Selected wins over completed/available: the player needs to see where they are before
            // they see what they have done.
            if (index == selectedIndex) return DotState.Selected;
            if (!catalog.Evaluate(index, playerPower).CanPlay) return DotState.Locked;
            if (PlayerProfile.IsLevelCompleted(level.levelId)) return DotState.Completed;

            return DotState.Available;
        }
    }
}
