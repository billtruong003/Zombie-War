using System;
using System.Collections.Generic;

namespace ZombieWar
{
    /// <summary>
    /// Achievements (backlog #22, owner-approved 05/10): the ten milestones the radio already has lines
    /// for. Each pays gems once, says its line on the radio and shows as a badge in the Profile.
    /// What unlocks them is watched by <see cref="AchievementTracker"/>; this class holds the table
    /// and the once-only grant.
    /// </summary>
    public static class Achievements
    {
        public sealed class Def
        {
            public readonly string id, title, description, voice;
            public readonly int gems;
            public Def(string id, string title, string description, int gems, string voice)
            { this.id = id; this.title = title; this.description = description; this.gems = gems; this.voice = voice; }
        }

        public const string FirstAlpha = "first_alpha", Run1000 = "run_1000", Survive10 = "survive_10",
            AllStations = "all_stations", FirstEvolution = "first_evo", Guns10 = "guns_10", Coins50K = "coins_50k",
            Whiteout = "whiteout", Threat10 = "threat_10", NoHit3 = "nohit_3m";

        public static readonly IReadOnlyList<Def> All = new List<Def>
        {
            new(FirstAlpha,     "First Alpha",      "Defeat your first elite",                     20,  "vo_riley_ach_first_alpha"),
            new(Survive10,      "Ten Minutes",      "Survive 10 minutes in one run",               50,  "vo_chen_ach_survive_10"),
            new(Threat10,       "Still Standing",   "Reach threat 10 in one run",                  60,  "vo_riley_ach_threat_10"),
            new(NoHit3,         "Untouchable",      "Go 3 minutes in a run without a hit",         40,  "vo_chen_ach_nohit_3m"),
            new(FirstEvolution, "Evolved",          "Evolve a power for the first time",           30,  "vo_chen_ach_first_evo"),
            new(AllStations,    "Every Station",    "Use every kind of station in one run",        50,  "vo_jiho_ach_all_stations"),
            new(Run1000,        "A Thousand",       "Defeat 1,000 monsters in one run",            80,  "vo_kaito_ach_run_1000"),
            new(Whiteout,       "Whiteout",         "Play a run on the Tundra map",                20,  "vo_kaito_ach_whiteout"),
            new(Guns10,         "Collector",        "Own 10 guns",                                 60,  "vo_lukas_ach_guns_10"),
            new(Coins50K,       "Deep Pockets",     "Hold 50,000 coins",                           40,  "vo_mai_ach_coins_50k"),
        };

        public static Def Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].id == id) return All[i];
            return null;
        }

        /// <summary>Raised after an achievement is granted (the radio line and toast listen).</summary>
        public static event Action<Def> Unlocked;

        public static bool IsUnlocked(string id) => PlayerProfile.HasAchievement(id);
        public static int UnlockedCount => PlayerProfile.AchievementCount;

        /// <summary>Unlocks an achievement once. Its gems wait to be claimed (mockups U8/F4: "claim +50
        /// gems after the run"), so an unlock mid-run never interrupts anything.</summary>
        public static bool Unlock(string id)
        {
            var def = Find(id);
            if (def == null || IsUnlocked(id)) return false;
            PlayerProfile.MarkAchievement(id);
            Unlocked?.Invoke(def);
            return true;
        }

        public static bool IsClaimed(string id) => PlayerProfile.HasClaimedAchievement(id);

        /// <summary>Achievements unlocked whose gems are still to be claimed.</summary>
        public static int ClaimableCount
        {
            get { int n = 0; foreach (var a in All) if (IsUnlocked(a.id) && !IsClaimed(a.id)) n++; return n; }
        }

        /// <summary>Pays an unlocked achievement's gems once (recorded before the gems).</summary>
        public static bool ClaimReward(string id)
        {
            var def = Find(id);
            if (def == null || !IsUnlocked(id) || IsClaimed(id)) return false;
            PlayerProfile.MarkAchievementClaimed(id);
            if (def.gems > 0) PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, def.gems);
            return true;
        }

        /// <summary>0..1 toward an achievement, from what the profile records (0 when it keeps no count).</summary>
        public static float Progress(string id) => id switch
        {
            Survive10 => PlayerProfile.BestSurvivalSeconds / 600f,
            Threat10 => PlayerProfile.PeakThreat / 10f,
            Run1000 => PlayerProfile.BestKills / 1000f,
            Guns10 => PlayerProfile.OwnedWeaponIds.Count / 10f,
            Coins50K => PlayerProfile.Coin / 50000f,
            _ => 0f,
        };

        /// <summary>Profile-side checks that need no run (guns owned, coins held).</summary>
        public static void CheckProfile()
        {
            if (PlayerProfile.OwnedWeaponIds.Count >= 10) Unlock(Guns10);
            if (PlayerProfile.Coin >= 50000) Unlock(Coins50K);
        }

        /// <summary>Station kinds a run must use for <see cref="AllStations"/>.</summary>
        public static int StationKindCount => Enum.GetValues(typeof(ZombieWar.Stations.StationKind)).Length;
    }
}
