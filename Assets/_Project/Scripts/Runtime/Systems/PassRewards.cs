using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// M10 Pass v2 (owner decisions 2026-09-27): 30 levels, a free and a premium lane, XP only from
    /// missions (<see cref="PlayerProfile.TryClaimMission"/>). Seasons are 28 days and start per
    /// player the first time the pass is seen (there is no server clock yet); a new season resets
    /// XP and claims. Gun skins in the lanes add power (see WeaponSkins).
    /// </summary>
    public static class PassRewards
    {
        public const int MaxLevel = 30, XpPerLevel = 750, SeasonDays = 28;
        public const string PremiumProductId = "pass.premium.s1";

        public enum Kind { Coin, Gem, Ticket, Skin, Frame }

        public readonly struct Reward
        {
            public readonly Kind kind; public readonly int amount; public readonly string id;
            public Reward(Kind k, int a, string i = null) { kind = k; amount = a; id = i; }
            public string Icon => kind switch
            {
                Kind.Coin => "Money_Coin", Kind.Gem => "Gem_Diamond_Purple", Kind.Ticket => "Ticket_Gold",
                Kind.Skin => "Gear_Sword", _ => "Medal_Gold_1",
            };
            public string Label => kind switch
            {
                Kind.Coin => amount >= 1000 ? (amount / 1000f).ToString("0.#") + "K" : amount.ToString(),
                Kind.Skin => SkinName(id), Kind.Frame => "Frame", _ => amount.ToString(),
            };
        }

        public const string FreeSkin = "frostbite", PremiumSkinMid = "cosmos", PremiumSkinTop = "inferno";
        public const string SeasonFrame = "frame.pass_s1";

        public static Reward Free(int level) => level switch
        {
            10 => new Reward(Kind.Skin, 1, FreeSkin),
            20 => new Reward(Kind.Frame, 1, SeasonFrame),
            30 => new Reward(Kind.Gem, 100),
            _ when level % 5 == 0 => new Reward(Kind.Gem, 20),
            _ when level % 5 == 3 => new Reward(Kind.Ticket, 1),
            _ => new Reward(Kind.Coin, 300 + 20 * level),
        };

        public static Reward Premium(int level) => level switch
        {
            15 => new Reward(Kind.Skin, 1, PremiumSkinMid),
            30 => new Reward(Kind.Skin, 1, PremiumSkinTop),
            _ when level % 5 == 0 => new Reward(Kind.Ticket, 3),
            _ when level % 5 == 2 => new Reward(Kind.Gem, 30),
            _ => new Reward(Kind.Coin, 600 + 40 * level),
        };

        static string SkinName(string id)
        {
            foreach (var s in Skins.WeaponSkins.Season1) if (s.id == id) return s.name;
            return id;
        }

        static PlayerProfile.ProfileData D => PlayerProfile.DailyData;

        // ------------------------------------------------------------------ season
        /// <summary>Starts season 1 on first use and rolls to the next season when 28 days pass.</summary>
        public static void EnsureSeason(int today)
        {
            if (D.passSeason == 0) { D.passSeason = 1; D.passSeasonStart = today; PlayerProfile.SaveDaily(); return; }
            if (today < D.passSeasonStart + SeasonDays) return;
            int passed = (today - D.passSeasonStart) / SeasonDays;
            D.passSeason += passed;
            D.passSeasonStart += passed * SeasonDays;
            D.passPremium = false;
            D.passClaimed.Clear();
            PlayerProfile.ResetPassXp();
            PlayerProfile.SaveDaily();
        }

        public static int Season => Mathf.Max(1, D.passSeason);
        public static int DaysLeft(int today) => Mathf.Max(0, D.passSeasonStart + SeasonDays - today);
        public static bool IsPremium => D.passPremium;

        public static int Level => Mathf.Clamp(1 + PlayerProfile.PassXp / XpPerLevel, 1, MaxLevel);
        public static int XpIntoLevel => Level >= MaxLevel ? XpPerLevel : PlayerProfile.PassXp % XpPerLevel;

        public static bool IsClaimed(int level, bool premium) => D.passClaimed.Contains(Key(level, premium));
        public static bool CanClaim(int level, bool premium) =>
            level >= 1 && level <= Level && !IsClaimed(level, premium) && (!premium || D.passPremium);

        public static int ClaimableCount()
        {
            int n = 0;
            for (int l = 1; l <= Level; l++) { if (CanClaim(l, false)) n++; if (CanClaim(l, true)) n++; }
            return n;
        }

        public static bool Claim(int level, bool premium, out Reward reward)
        {
            reward = premium ? Premium(level) : Free(level);
            if (!CanClaim(level, premium)) return false;
            D.passClaimed.Add(Key(level, premium));
            PlayerProfile.SaveDaily();   // record first, then grant (same order as mission claims)
            Grant(reward);
            return true;
        }

        /// <summary>Claims every open reward; returns how many.</summary>
        public static int ClaimAll()
        {
            int n = 0;
            for (int l = 1; l <= Level; l++)
            {
                if (Claim(l, false, out _)) n++;
                if (Claim(l, true, out _)) n++;
            }
            return n;
        }

        /// <summary>Called by the store after a successful premium purchase.</summary>
        public static void UnlockPremium()
        {
            if (D.passPremium) return;
            D.passPremium = true;
            PlayerProfile.SaveDaily();
        }

        static string Key(int level, bool premium) => $"s{Season}.{(premium ? "p" : "f")}{level}";

        static void Grant(Reward r)
        {
            switch (r.kind)
            {
                case Kind.Coin: PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, r.amount); break;
                case Kind.Gem: PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, r.amount); break;
                case Kind.Ticket: PlayerProfile.AddTickets(r.amount); break;
                case Kind.Skin: PlayerProfile.AddSkin(r.id); break;
                case Kind.Frame: PlayerProfile.AddFrame(r.id); break;
            }
        }
    }
}
