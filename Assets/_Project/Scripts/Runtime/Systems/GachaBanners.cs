using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// M10 Gacha v2 (owner decisions 2026-09-27): banners are data (a list the remote config can
    /// replace later), the weapon-shard banner is permanent and paid in gems, an event banner
    /// features a gun skin set with a guaranteed hit after <see cref="Banner.guarantee"/> pulls,
    /// its pity carries over to the next banner with the same featured prize, duplicates turn
    /// into tickets, and every rate is public. The outfit and shard banners reuse
    /// <see cref="GachaService"/> with gem prices.
    /// </summary>
    public static class GachaBanners
    {
        public enum Kind { Event, Outfits, Shards }

        public sealed class Rate { public string label; public float percent; public WeaponTier tier; }

        public sealed class Banner
        {
            public string id, title, subtitle, featuredSkin, pityKey;
            public Kind kind;
            public int days;              // length of one run of the banner
            public int gemSingle = 30, gemMulti = 270, ticketSingle = 1, ticketMulti = 10;
            public int guarantee = 80;    // event: featured prize by this pull at the latest
            public Rate[] rates;
        }

        /// <summary>Season 1 banners. Order = tab order.</summary>
        public static readonly Banner[] All =
        {
            new()
            {
                id = "event.neon", title = "NEON NIGHTS", subtitle = "EVENT", kind = Kind.Event, days = 14,
                featuredSkin = "neon", pityKey = "gacha.featured.neon",
                rates = new[]
                {
                    new Rate { label = "Neon Circuit skin set", percent = 0.8f, tier = WeaponTier.Legendary },
                    new Rate { label = "Epic outfit", percent = 3f, tier = WeaponTier.Epic },
                    new Rate { label = "Gun shards ×10", percent = 12f, tier = WeaponTier.Rare },
                    new Rate { label = "Gacha ticket", percent = 25f, tier = WeaponTier.Uncommon },
                    new Rate { label = "300 coins", percent = 59.2f, tier = WeaponTier.Common },
                },
            },
            new() { id = "outfits", title = "STREET", subtitle = "OUTFITS", kind = Kind.Outfits, days = 28, pityKey = "gacha.costume" },
            new() { id = "shards", title = "SHARDS", subtitle = "ALWAYS", kind = Kind.Shards, days = 0, pityKey = "gacha.weapon" },
        };

        public const int FeaturedDupeTickets = 5, OutfitDupeTickets = 1;

        public readonly struct Result
        {
            public readonly string label; public readonly WeaponTier tier; public readonly bool isNew; public readonly int tickets;
            public Result(string l, WeaponTier t, bool n, int tk) { label = l; tier = t; isNew = n; tickets = tk; }
        }

        static PlayerProfile.ProfileData D => PlayerProfile.DailyData;

        // ------------------------------------------------------------------ timing
        /// <summary>Days left in this run of the banner; runs repeat back to back from the first visit.</summary>
        public static int DaysLeft(Banner b, int today)
        {
            if (b.days <= 0) return -1;
            if (D.gachaEpoch == 0) { D.gachaEpoch = today; PlayerProfile.SaveDaily(); }
            int into = Mathf.Max(0, today - D.gachaEpoch) % b.days;
            return b.days - into;
        }

        public static bool FreePullReady(int today) => D.gachaFreeDay != today;

        public static int PullsToGuarantee(Banner b) => Mathf.Max(0, b.guarantee - PlayerProfile.GetPity(b.pityKey));

        // ------------------------------------------------------------------ cost
        public enum Pay { Free, Tickets, Gems }

        public static bool CanPay(Banner b, int count, Pay pay, int today) => pay switch
        {
            Pay.Free => count == 1 && FreePullReady(today),
            Pay.Tickets => PlayerProfile.Tickets >= (count == 1 ? b.ticketSingle : b.ticketMulti),
            _ => PlayerProfile.Gem >= (count == 1 ? b.gemSingle : b.gemMulti),
        };

        /// <summary>Tickets when the player has enough, otherwise gems.</summary>
        public static Pay BestPay(Banner b, int count, int today) =>
            count == 1 && FreePullReady(today) ? Pay.Free
            : PlayerProfile.Tickets >= (count == 1 ? b.ticketSingle : b.ticketMulti) ? Pay.Tickets : Pay.Gems;

        static bool Spend(Banner b, int count, Pay pay, int today)
        {
            switch (pay)
            {
                case Pay.Free: if (!FreePullReady(today) || count != 1) return false; D.gachaFreeDay = today; return true;
                case Pay.Tickets: return PlayerProfile.TrySpendTickets(count == 1 ? b.ticketSingle : b.ticketMulti);
                default: return PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Gem, count == 1 ? b.gemSingle : b.gemMulti);
            }
        }

        // ------------------------------------------------------------------ pull
        /// <summary>Pays, rolls and grants. Null when the player cannot pay.</summary>
        public static List<Result> Pull(Banner b, int count, Pay pay, int today, EconomyConfig econ,
                                        IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            if (b == null || (count != 1 && count != 10)) return null;
            if (b.kind != Kind.Event) return PullPool(b, count, pay, today, econ, guns, rng);
            if (!CanPay(b, count, pay, today) || !Spend(b, count, pay, today)) return null;

            var results = new List<Result>(count);
            int pity = PlayerProfile.GetPity(b.pityKey);
            bool epicInTen = false;
            for (int n = 0; n < count; n++)
            {
                pity++;
                int roll = rng.Range(10000);
                float acc = 0f; int pick = b.rates.Length - 1;
                for (int i = 0; i < b.rates.Length; i++) { acc += b.rates[i].percent * 100f; if (roll < acc) { pick = i; break; } }
                if (pity >= b.guarantee) pick = 0;                                    // hard guarantee
                if (count == 10 && n == 9 && !epicInTen && pick > 1) pick = 1;       // x10: at least one Epic+
                if (pick <= 1) epicInTen = true;
                if (pick == 0) pity = 0;
                results.Add(Grant(b, pick, econ, guns, rng));
            }
            PlayerProfile.SetPityInMemory(b.pityKey, pity);
            PlayerProfile.SaveDaily();
            return results;
        }

        static Result Grant(Banner b, int pick, EconomyConfig econ, IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            var rate = b.rates[pick];
            switch (pick)
            {
                case 0:
                    if (PlayerProfile.IsSkinOwned(b.featuredSkin)) { PlayerProfile.AddTickets(FeaturedDupeTickets); return new Result(rate.label, rate.tier, false, FeaturedDupeTickets); }
                    PlayerProfile.AddSkin(b.featuredSkin);
                    return new Result(rate.label, rate.tier, true, 0);
                case 1:
                    // Gacha-only pieces first (owner: some outfits only come from the gacha).
                    var only = econ?.costumeItems?.Where(c => c.source == AcquireSource.Gacha && !string.IsNullOrEmpty(c.itemId)
                                                            && !PlayerProfile.IsCostumeItemOwned(c.itemId)).ToList();
                    if (only != null && only.Count > 0)
                    {
                        var piece = only[rng.Range(only.Count)];
                        PlayerProfile.GrantCostumeInMemory(piece.itemId);
                        return new Result(piece.displayName, piece.rarity, true, 0);
                    }
                    var sets = econ?.costumeSets?.Where(s => s != null && s.rarity >= WeaponTier.Epic && s.itemIds != null && s.itemIds.Count > 0).ToList();
                    if (sets == null || sets.Count == 0) { PlayerProfile.AddTickets(OutfitDupeTickets); return new Result("Gacha ticket", rate.tier, false, OutfitDupeTickets); }
                    var set = sets[rng.Range(sets.Count)];
                    if (PlayerProfile.IsCostumeSetOwned(set)) { PlayerProfile.AddTickets(OutfitDupeTickets); return new Result(set.displayName, rate.tier, false, OutfitDupeTickets); }
                    foreach (var id in set.itemIds) PlayerProfile.GrantCostumeInMemory(id);
                    return new Result(set.displayName, rate.tier, true, 0);
                case 2:
                    var owned = guns?.Where(g => g != null && PlayerProfile.IsWeaponOwned(g.WeaponId)).ToList();
                    if (owned == null || owned.Count == 0) { PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 600); return new Result("600 coins", rate.tier, true, 0); }
                    var gun = owned[rng.Range(owned.Count)];
                    PlayerProfile.AddWeaponShards(gun.WeaponId, 10);
                    return new Result($"{gun.weaponName} shards ×10", rate.tier, true, 0);
                case 3:
                    PlayerProfile.AddTickets(1);
                    return new Result(rate.label, rate.tier, true, 0);
                default:
                    PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 300);
                    return new Result(rate.label, rate.tier, true, 0);
            }
        }

        /// <summary>Outfit and shard banners: <see cref="GachaService"/> with this banner's gem price.</summary>
        static List<Result> PullPool(Banner b, int count, Pay pay, int today, EconomyConfig econ, IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            var src = PoolFor(b, econ);
            if (src == null) return null;
            if (!CanPay(b, count, pay, today)) return null;
            // GachaService charges the pool's own currency; pay with tickets or the free pull by
            // spending those first and running the pool at zero cost.
            var pool = Copy(src, b);
            var items = GachaService.BuildPool(pool, econ, guns, new HashSet<string> { WeaponCatalog.Active?.Starter?.weaponId });
            if (items.Count == 0) return null;   // check before anything is spent
            if (pay != Pay.Gems) { if (!Spend(b, count, pay, today)) return null; pool.singleCost = 0; pool.multiCost = 0; }
            var raw = GachaService.Pull(econ, pool, items, count, rng);
            if (raw == null) return null;
            if (pay != Pay.Gems) PlayerProfile.SaveDaily();
            var list = new List<Result>(raw.Count);
            foreach (var r in raw)
            {
                int tickets = 0;
                if (!r.isNew && !r.isWeapon) { tickets = OutfitDupeTickets; PlayerProfile.AddTickets(tickets); }
                string label = r.isWeapon && !r.isNew ? $"{r.displayName} shards ×{r.weaponShards}" : r.displayName;
                list.Add(new Result(label, r.rarity, r.isNew, tickets));
            }
            return list;
        }

        public static EconomyConfig.GachaPool PoolFor(Banner b, EconomyConfig econ) =>
            econ == null ? null : b.kind == Kind.Shards ? econ.weaponPool : b.kind == Kind.Outfits ? econ.costumePool : null;

        static EconomyConfig.GachaPool Copy(EconomyConfig.GachaPool p, Banner b) => new()
        {
            poolId = p.poolId, displayName = p.displayName, enabled = p.enabled, kind = p.kind,
            currency = WalletCurrency.Gem, singleCost = b.gemSingle, multiCost = b.gemMulti, multiCount = p.multiCount,
            rarityWeights = p.rarityWeights, pityThreshold = p.pityThreshold, pityMinRarity = p.pityMinRarity,
            dupCurrency = WalletCurrency.Coin, dupCompensation = new long[5], weaponDuplicateShards = p.weaponDuplicateShards,
        };

        /// <summary>Public rates for any banner (pool banners derive them from rarity weights).</summary>
        public static Rate[] RatesFor(Banner b, EconomyConfig econ)
        {
            if (b.rates != null) return b.rates;
            var pool = PoolFor(b, econ);
            if (pool?.rarityWeights == null) return new Rate[0];
            float total = pool.rarityWeights.Sum();
            string[] names = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
            string what = b.kind == Kind.Shards ? "gun (dupes give shards)" : "outfit";
            return Enumerable.Range(0, Mathf.Min(5, pool.rarityWeights.Length)).Reverse()
                .Where(i => pool.rarityWeights[i] > 0)
                .Select(i => new Rate { label = $"{names[i]} {what}", percent = 100f * pool.rarityWeights[i] / total, tier = (WeaponTier)i }).ToArray();
        }
    }
}
