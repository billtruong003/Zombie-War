using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// M10 Shop v2 catalogue: three daily deals (new each local day, each bought once), the
    /// boutique "look of the week" (a costume set picked by week), real-money packs (through
    /// <see cref="Purchases"/>) and gun skin sets. Guns keep their coin prices from WeaponData.
    /// </summary>
    public static class ShopOffers
    {
        public enum Kind { Shards, Tickets, Coins }
        public enum Pay { Coin, Gem, Ad }

        public readonly struct Deal
        {
            public readonly int slot; public readonly Kind kind; public readonly int amount; public readonly Pay pay; public readonly int price;
            public readonly WeaponData gun;
            public Deal(int s, Kind k, int a, Pay p, int pr, WeaponData g = null) { slot = s; kind = k; amount = a; pay = p; price = pr; gun = g; }
            public string Title => kind switch
            {
                Kind.Shards => $"{(gun != null ? gun.weaponName : "Gun")} shards ×{amount}",
                Kind.Tickets => $"Gacha ticket ×{amount}",
                _ => $"{amount:N0} coins",
            };
        }

        public const int ShardDealAmount = 20, ShardDealPrice = 900, TicketDealAmount = 2, TicketDealPrice = 50, AdDealCoins = 300;

        static PlayerProfile.ProfileData D => PlayerProfile.DailyData;

        static void EnsureDay(int today)
        {
            if (D.dealDay == today) return;
            D.dealDay = today;
            D.dealsBought.Clear();
            PlayerProfile.SaveDaily();
        }

        /// <summary>Today's three deals. The shard deal picks an owned gun that can still gain stars.</summary>
        public static Deal[] DealsFor(int today, IReadOnlyList<WeaponData> guns)
        {
            var owned = guns?.Where(g => g != null && PlayerProfile.IsWeaponOwned(g.WeaponId) && PlayerProfile.GetWeaponLevel(g.WeaponId) < 3)
                            .OrderBy(g => g.WeaponId).ToList() ?? new List<WeaponData>();
            var shard = owned.Count > 0
                ? new Deal(0, Kind.Shards, ShardDealAmount, Pay.Coin, ShardDealPrice, owned[Mathf.Abs(today * 7919) % owned.Count])
                : new Deal(0, Kind.Tickets, 1, Pay.Coin, ShardDealPrice);
            return new[]
            {
                shard,
                new Deal(1, Kind.Tickets, TicketDealAmount, Pay.Gem, TicketDealPrice),
                new Deal(2, Kind.Coins, AdDealCoins, Pay.Ad, 0),
            };
        }

        public static bool IsDealBought(int today, int slot) { EnsureDay(today); return D.dealsBought.Contains(slot.ToString()); }

        /// <summary>Pays for (Coin/Gem) and grants a deal. Ad deals call this after the ad paid out.</summary>
        public static bool BuyDeal(int today, Deal deal)
        {
            EnsureDay(today);
            if (D.dealsBought.Contains(deal.slot.ToString())) return false;
            if (deal.pay == Pay.Coin && !PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Coin, deal.price)) return false;
            if (deal.pay == Pay.Gem && !PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Gem, deal.price)) return false;
            D.dealsBought.Add(deal.slot.ToString());
            PlayerProfile.SaveDaily();
            switch (deal.kind)
            {
                case Kind.Shards: if (deal.gun != null) PlayerProfile.AddWeaponShards(deal.gun.WeaponId, deal.amount); break;
                case Kind.Tickets: PlayerProfile.AddTickets(deal.amount); break;
                case Kind.Coins: PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, deal.amount); break;
            }
            return true;
        }

        /// <summary>Hours:minutes:seconds until tomorrow's deals.</summary>
        public static string RefreshIn(System.DateTime now)
        {
            var left = now.Date.AddDays(1) - now;
            return $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
        }

        // ------------------------------------------------------------------ boutique
        /// <summary>The week's look: a gem-priced costume set sold in the shop, rotating weekly.</summary>
        public static EconomyConfig.CostumeSetEntry LookOfTheWeek(EconomyConfig econ, int today)
        {
            if (econ == null) return null;
            var sets = econ.costumeSets.Where(s => s != null && s.itemIds != null && s.itemIds.Count > 0 &&
                                                   (s.source == AcquireSource.Shop || s.source == AcquireSource.ShopAndGacha))
                                       .OrderBy(s => s.setId).ToList();
            if (sets.Count == 0) return null;
            return sets[Mathf.Abs(today / 7) % sets.Count];
        }

        // ------------------------------------------------------------------ packs
        public sealed class Pack
        {
            public string id, title, price; public bool once;
            public int coins, gems, tickets; public string skin; public bool noAds;
        }

        public static readonly Pack[] Packs =
        {
            new() { id = "pack.starter", title = "Starter", price = "$0.99", once = true, coins = 2000, gems = 100, tickets = 3 },
            new() { id = "pack.gems440", title = "440 gems", price = "$4.99", gems = 440 },
            new() { id = "pack.noads", title = "No ads", price = "$2.99", once = true, noAds = true },
            new() { id = "pack.legend", title = "Legend pack", price = "$9.99", once = true, gems = 500, skin = "gilded" },
        };

        public static Pack FindPack(string id) => Packs.FirstOrDefault(p => p.id == id);
        public static bool IsPackBought(string id) => D.packsBought.Contains(id);
        public static bool CanBuyPack(Pack p) => p != null && !(p.once && IsPackBought(p.id));

        /// <summary>Grants a pack after the store confirmed payment.</summary>
        public static bool GrantPack(Pack p)
        {
            if (!CanBuyPack(p)) return false;
            if (!D.packsBought.Contains(p.id)) D.packsBought.Add(p.id);
            if (p.noAds) D.noAds = true;
            PlayerProfile.SaveDaily();
            if (p.coins > 0) PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, p.coins);
            if (p.gems > 0) PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, p.gems);
            if (p.tickets > 0) PlayerProfile.AddTickets(p.tickets);
            if (!string.IsNullOrEmpty(p.skin)) PlayerProfile.AddSkin(p.skin);
            return true;
        }

        // ------------------------------------------------------------------ skin sets
        public enum SkinSource { Pass, Gems, Gacha, LegendPack }

        public static SkinSource SourceOf(string skinId) => skinId switch
        {
            "frostbite" or "cosmos" or "inferno" => SkinSource.Pass,
            "biohazard" => SkinSource.Gems,
            "neon" => SkinSource.Gacha,
            _ => SkinSource.LegendPack,
        };

        public const int BiohazardGems = 320;

        public static bool BuySkinWithGems(string skinId)
        {
            if (SourceOf(skinId) != SkinSource.Gems || PlayerProfile.IsSkinOwned(skinId)) return false;
            if (!PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Gem, BiohazardGems)) return false;
            PlayerProfile.AddSkin(skinId);
            return true;
        }
    }
}
