namespace ZombieWar
{
    /// PlayerProfile, part: unlocked achievements (backlog #22).
    public static partial class PlayerProfile
    {
        public static bool HasAchievement(string id) => !string.IsNullOrEmpty(id) && Data.achievements.Contains(id);
        public static int AchievementCount => Data.achievements.Count;
        public static bool HasClaimedAchievement(string id) => !string.IsNullOrEmpty(id) && Data.achievementsClaimed.Contains(id);

        public static void MarkAchievementClaimed(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.achievementsClaimed.Contains(id)) return;
            Data.achievementsClaimed.Add(id);
            SaveNow();
        }

        /// <summary>Records an achievement. A synchronous save: it pays gems right after.</summary>
        public static void MarkAchievement(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.achievements.Contains(id)) return;
            Data.achievements.Add(id);
            SaveNow();
            Notify(Change.Account);
        }
    }
}
