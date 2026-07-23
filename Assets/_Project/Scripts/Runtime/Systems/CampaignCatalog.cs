using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>One campaign stage, authored in the Inspector.</summary>
    [Serializable]
    public class CampaignLevel
    {
        [Tooltip("Stable save key. Never renumber these - completion is persisted against them.")]
        public string levelId = "level.1";
        public string displayName = "Stage 1";
        [Tooltip("Scene name, which must also be present in Build Settings.")]
        public string sceneName = "Map_Level1";
        public WaveData waves;

        [Header("Gate")]
        [Tooltip("Hard requirement. Below this the stage cannot be started.")]
        public int minimumPower = 0;
        [Tooltip("Shown as advice. Not enforced.")]
        public int recommendedPower = 0;

        [Header("Advice")]
        [Tooltip("Weapon families suggested for this stage, e.g. 'Shotgun'.")]
        public string[] suggestedFamilies = Array.Empty<string>();
        [Tooltip("Real weapon IDs from the current roster. Must include at least one attainable " +
                 "option so the stage is never gated behind a single paid weapon.")]
        public string[] suggestedWeaponIds = Array.Empty<string>();

        [Header("Rewards")]
        public int firstClearCoin = 500;
        public int firstClearGold = 0;
        public int firstClearGem = 0;
        public int repeatCoin = 120;

        [Header("Boss")]
        public bool hasBoss;
        [Tooltip("Enemy ID of the stage boss, for UI and Pass missions.")]
        public string bossEnemyId = "";
    }

    /// <summary>
    /// The single authority for the five-stage campaign: order, scenes, waves, power gates, weapon
    /// advice and rewards. One asset instead of five hardcoded branches, so tuning a stage never
    /// means a recompile and the UI can render whatever is authored.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/Campaign Catalog", fileName = "CampaignCatalog")]
    public class CampaignCatalog : ScriptableObject
    {
        [SerializeField] private List<CampaignLevel> levels = new List<CampaignLevel>();

        public IReadOnlyList<CampaignLevel> Levels => levels;
        public int Count => levels.Count;

        public CampaignLevel Get(int index) =>
            index >= 0 && index < levels.Count ? levels[index] : null;

        public CampaignLevel Find(string levelId)
        {
            for (int i = 0; i < levels.Count; i++)
                if (levels[i].levelId == levelId) return levels[i];
            return null;
        }

        public int IndexOf(string levelId)
        {
            for (int i = 0; i < levels.Count; i++)
                if (levels[i].levelId == levelId) return i;
            return -1;
        }

        /// <summary>
        /// Whether the stage may be started, and why not when it may not.
        ///
        /// Two independent gates: the previous stage must be cleared (progression) and the player's
        /// Combat Power must reach the authored floor (capability). Stage 1 is always open so a
        /// fresh profile is never locked out of its own game.
        /// </summary>
        public LevelGate Evaluate(int index, int playerPower)
        {
            var level = Get(index);
            if (level == null) return LevelGate.Locked("Stage does not exist.");

            if (index > 0)
            {
                var previous = Get(index - 1);
                if (previous != null && !PlayerProfile.IsLevelCompleted(previous.levelId))
                    return LevelGate.Locked($"Complete {previous.displayName} first.");
            }

            if (playerPower < level.minimumPower)
                return LevelGate.Underpowered(
                    $"Requires {level.minimumPower} Combat Power (you have {playerPower}).");

            return LevelGate.Open();
        }
    }

    /// <summary>Result of a stage gate check. A failed gate changes no state - it only explains.</summary>
    public readonly struct LevelGate
    {
        public enum Status { Open, Locked, Underpowered }

        public readonly Status State;
        public readonly string Reason;

        private LevelGate(Status state, string reason) { State = state; Reason = reason; }

        public bool CanPlay => State == Status.Open;

        public static LevelGate Open() => new LevelGate(Status.Open, "");
        public static LevelGate Locked(string reason) => new LevelGate(Status.Locked, reason);
        public static LevelGate Underpowered(string reason) => new LevelGate(Status.Underpowered, reason);
    }
}
