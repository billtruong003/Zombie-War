using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// M9 account level: every run pays account XP, and features open by level so a new player
    /// meets one system at a time (owner-approved Home first-day mockup: LV2 Pass, LV3 Gacha and
    /// events, LV5 gun stars). Pure numbers here; the save lives in <see cref="PlayerProfile"/>.
    /// </summary>
    public static class AccountProgress
    {
        public enum Feature { Missions, Pass, Gacha, Events, GunStars }

        public const int BaseCost = 40;     // LV1 → LV2
        public const int CostStep = 30;     // each later level costs this much more
        public const int MaxLevel = 99;

        /// <summary>Account XP one run pays: half a point per second survived plus a fifth per kill.</summary>
        public static int XpForRun(float secondsSurvived, int kills) =>
            Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, secondsSurvived) * 0.5f + Mathf.Max(0, kills) * 0.2f));

        /// <summary>XP needed to go from <paramref name="level"/> to the next level.</summary>
        public static int CostOf(int level) => BaseCost + CostStep * (Mathf.Max(1, level) - 1);

        /// <summary>Total XP needed to reach <paramref name="level"/> from level 1.</summary>
        public static int TotalFor(int level)
        {
            int total = 0;
            for (int l = 1; l < Mathf.Min(level, MaxLevel); l++) total += CostOf(l);
            return total;
        }

        public static int LevelFor(int xp)
        {
            int level = 1;
            while (level < MaxLevel && xp >= TotalFor(level + 1)) level++;
            return level;
        }

        /// <summary>Progress inside the current level, 0..1, for the Home XP ring.</summary>
        public static float Progress01(int xp)
        {
            int level = LevelFor(xp);
            if (level >= MaxLevel) return 1f;
            return Mathf.Clamp01((xp - TotalFor(level)) / (float)CostOf(level));
        }

        public static int RequiredLevel(Feature f) => f switch
        {
            Feature.Missions or Feature.Pass => 2,
            Feature.Gacha or Feature.Events => 3,
            Feature.GunStars => 5,
            _ => 1,
        };

        public static bool IsUnlocked(Feature f, int accountLevel) => accountLevel >= RequiredLevel(f);
        public static bool IsUnlocked(Feature f) => IsUnlocked(f, PlayerProfile.AccountLevel);
    }
}
