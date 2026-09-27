using System;
using System.Linq;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// M10 Daily (owner decisions 2026-09-27): a 7-day welcome check-in for new players and a
    /// 28-day stamp card for everyone. One claim per local calendar day each. Missed stamp days can
    /// be made up, at most <see cref="MakeUpsPerCycle"/> per cycle, for gems (or a rewarded ad once
    /// ads ship in M11). Days are passed in so the rules are testable; the UI uses <see cref="Today"/>.
    /// </summary>
    public static class DailyRewards
    {
        public const int WelcomeDays = 7, CardDays = 28, MakeUpsPerCycle = 2, MakeUpGemCost = 20;
        public const string StampMasterFrame = "frame.stamp_master";

        public enum Kind { Coin, Gem, Ticket, Gun, Frame }

        public readonly struct Reward
        {
            public readonly Kind kind; public readonly int amount; public readonly string label;
            public Reward(Kind k, int a, string l) { kind = k; amount = a; label = l; }
            /// <summary>Icon name in the Layer Lab item icon set.</summary>
            public string Icon => kind switch
            {
                Kind.Coin => "Money_Coin", Kind.Gem => "Gem_Diamond_Purple", Kind.Ticket => "Ticket_Gold",
                Kind.Gun => "Chest_Premium", _ => "Medal_Gold_1",
            };
        }

        static readonly Reward[] Welcome =
        {
            new(Kind.Coin, 500, "500"), new(Kind.Gem, 10, "10"), new(Kind.Ticket, 1, "1"), new(Kind.Gem, 20, "20"),
            new(Kind.Coin, 1000, "1K"), new(Kind.Ticket, 2, "2"), new(Kind.Gun, 1, "Rare gun"),
        };

        /// <summary>Gems paid instead when every rare-or-better gun is already owned.</summary>
        public const int WelcomeGunFallbackGems = 150;

        public static Reward WelcomeReward(int day1to7) => Welcome[Mathf.Clamp(day1to7, 1, WelcomeDays) - 1];

        public static Reward StampReward(int stamp1to28) => stamp1to28 switch
        {
            7 => new Reward(Kind.Gem, 50, "50"),
            14 => new Reward(Kind.Ticket, 3, "3"),
            21 => new Reward(Kind.Gem, 40, "40"),
            28 => new Reward(Kind.Frame, 1, "Stamp Master frame"),
            _ => new Reward(Kind.Coin, 100, "100"),
        };

        public static bool IsMilestone(int stamp) => stamp % 7 == 0;

        /// <summary>Local calendar day number (days since 2000-01-01).</summary>
        public static int Today => DayOf(DateTime.Now);
        public static int DayOf(DateTime t) => (int)(t.Date - new DateTime(2000, 1, 1)).TotalDays;

        static PlayerProfile.ProfileData D => PlayerProfile.DailyData;

        // ------------------------------------------------------------------ welcome
        public static int WelcomeClaims => D.welcomeClaims;
        public static bool WelcomeActive => D.welcomeClaims < WelcomeDays;
        public static bool CanClaimWelcome(int today) => WelcomeActive && D.welcomeLastDay != today;

        public static bool ClaimWelcome(int today, out Reward reward)
        {
            reward = default;
            if (!CanClaimWelcome(today)) return false;
            reward = WelcomeReward(D.welcomeClaims + 1);
            D.welcomeClaims++;
            D.welcomeLastDay = today;
            Grant(reward);
            PlayerProfile.SaveDaily();
            return true;
        }

        // ------------------------------------------------------------------ stamp card
        /// <summary>Starts a new 28-day cycle the first time, and after the current one ends.</summary>
        public static void EnsureCycle(int today)
        {
            if (D.stampCycleStart != 0 && today >= D.stampCycleStart && today < D.stampCycleStart + CardDays) return;
            D.stampCycleStart = today;
            D.stampCount = 0;
            D.stampLastDay = 0;
            D.stampMakeUps = 0;
            PlayerProfile.SaveDaily();
        }

        public static int Stamps => D.stampCount;
        public static int CycleDay(int today) => Mathf.Clamp(today - D.stampCycleStart + 1, 1, CardDays);
        public static int DaysLeft(int today) => Mathf.Max(0, D.stampCycleStart + CardDays - today);
        public static bool StampedToday(int today) => D.stampLastDay == today;
        public static int MakeUpsLeft => Mathf.Max(0, MakeUpsPerCycle - D.stampMakeUps);

        /// <summary>Days of this cycle before today that have no stamp.</summary>
        public static int Missed(int today)
        {
            int before = CycleDay(today) - 1;
            int stampedBefore = D.stampCount - (StampedToday(today) ? 1 : 0);
            return Mathf.Max(0, before - stampedBefore);
        }

        public static bool CanStamp(int today) => !StampedToday(today) && D.stampCount < CardDays;
        public static bool CanMakeUp(int today) => Missed(today) > 0 && MakeUpsLeft > 0 && D.stampCount < CardDays;

        public static bool Stamp(int today, out Reward reward)
        {
            reward = default;
            EnsureCycle(today);
            if (!CanStamp(today)) return false;
            D.stampLastDay = today;
            reward = AddStamp();
            PlayerProfile.SaveDaily();
            return true;
        }

        /// <summary>Makes up one missed day for <see cref="MakeUpGemCost"/> gems (or free after a
        /// rewarded ad when <paramref name="paidByAd"/>).</summary>
        public static bool MakeUp(int today, bool paidByAd, out Reward reward)
        {
            reward = default;
            EnsureCycle(today);
            if (!CanMakeUp(today)) return false;
            if (!paidByAd && !PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Gem, MakeUpGemCost)) return false;
            D.stampMakeUps++;
            reward = AddStamp();
            PlayerProfile.SaveDaily();
            return true;
        }

        static Reward AddStamp()
        {
            D.stampCount++;
            var r = StampReward(D.stampCount);
            Grant(r);
            return r;
        }

        /// <summary>Something to claim today: drives the Home rail badge.</summary>
        public static int ClaimableCount(int today)
        {
            EnsureCycle(today);
            return (CanClaimWelcome(today) ? 1 : 0) + (CanStamp(today) ? 1 : 0);
        }

        // ------------------------------------------------------------------ grants
        static void Grant(Reward r)
        {
            switch (r.kind)
            {
                case Kind.Coin: PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, r.amount); break;
                case Kind.Gem: PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, r.amount); break;
                case Kind.Ticket: PlayerProfile.AddTickets(r.amount); break;
                case Kind.Frame: PlayerProfile.AddFrame(StampMasterFrame); break;
                case Kind.Gun:
                    var gun = WelcomeGun();
                    if (gun != null) PlayerProfile.AddOwnedWeapon(gun.WeaponId);
                    else PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, WelcomeGunFallbackGems);
                    break;
            }
        }

        /// <summary>The cheapest rare-or-better gun the player does not own yet, or null.</summary>
        public static WeaponData WelcomeGun()
        {
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.DisplayData() : null;
            return all?.Where(w => w != null && w.tier >= WeaponTier.Rare && !PlayerProfile.IsWeaponOwned(w.WeaponId))
                      .OrderBy(w => w.price).FirstOrDefault();
        }
    }
}
