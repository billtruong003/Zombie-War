using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// M10 Gacha v2. Banners are data (a list the remote config can replace later).
    ///
    /// Event banner (owner, 2026-09-27 after the first playtest): the top prize is very rare
    /// (<see cref="Banner.legendaryPercent"/>), a Legendary is certain by pull
    /// <see cref="Banner.hardPity"/>, and it is a 50/50: when a Legendary is not the featured prize
    /// ("off-rate"), the next Legendary is guaranteed to be it. Pity and the guarantee carry over to
    /// the next banner with the same featured prize. Gems are earned by playing, so a hard rate is
    /// still fair to a free player. x10 opens ten boxes one by one plus one bonus box (11), and
    /// always holds an Epic or better. Duplicates turn into tickets; every rate is public.
    ///
    /// The outfit and shard banners reuse <see cref="GachaService"/> with gem prices.
    /// </summary>
    public static class GachaBanners
    {
        public enum Kind { Event, Outfits, Shards }

        public sealed class Rate { public string label; public float percent; public WeaponTier tier; public string icon; }

        public sealed class Banner
        {
            public string id, title, subtitle, featuredSkin, pityKey;
            public Kind kind;
            public int days;              // length of one run of the banner
            public int gemSingle = 30, gemMulti = 270, ticketSingle = 1, ticketMulti = 10;
            public float legendaryPercent = 0.6f;
            public int hardPity = 90;
            public Rate[] rates;          // index 0 = Legendary (featured / off-rate)
            public string GuaranteeKey => pityKey + ".guaranteed";
        }

        /// <summary>Boxes in a multi pull: ten paid plus one bonus.</summary>
        public const int MultiPaid = 10, MultiBoxes = 11;

        /// <summary>Event-banner prizes below Epic: the rate labels and the grants read the same numbers.</summary>
        public const int EventShards = 10, EventTickets = 1, EventCoins = 300, NoGunCoins = 600;

        /// <summary>Season 1 banners. Order = tab order.</summary>
        public static readonly Banner[] All =
        {
            new()
            {
                id = "event.neon", title = "NEON NIGHTS", subtitle = "EVENT", kind = Kind.Event, days = 14,
                featuredSkin = "neon", pityKey = "gacha.featured.neon",
                rates = new[]
                {
                    new Rate { label = "Legendary: Neon Circuit skin set (50%)", percent = 0.6f, tier = WeaponTier.Legendary, icon = "Gear_Sword" },
                    new Rate { label = "Epic outfit piece", percent = 5.1f, tier = WeaponTier.Epic, icon = "Gear_Armor_Top" },
                    new Rate { label = $"Gun shards ×{EventShards}", percent = 13f, tier = WeaponTier.Rare, icon = "Chest_Gold" },
                    new Rate { label = "Gacha ticket", percent = 25f, tier = WeaponTier.Uncommon, icon = "Ticket_Gold" },
                    new Rate { label = $"{EventCoins} coins", percent = 56.3f, tier = WeaponTier.Common, icon = "Money_Coin" },
                },
            },
            new() { id = "outfits", title = "STREET", subtitle = "OUTFITS", kind = Kind.Outfits, days = 28, pityKey = "gacha.costume" },
            new() { id = "shards", title = "SHARDS", subtitle = "ALWAYS", kind = Kind.Shards, days = 0, pityKey = "gacha.weapon" },
        };

        public const int FeaturedDupeTickets = 5, OutfitDupeTickets = 1, OffRateFallbackTickets = 10;

        /// <summary>What a prize is, so the reveal can show the item's own icon (owner 2026-09-28).</summary>
        public enum Prize { Other, Skin, Costume, CostumeSet, Gun, Ticket, Coin }

        public readonly struct Result
        {
            public readonly string label, icon; public readonly WeaponTier tier; public readonly bool isNew, bonus, offRate;
            public readonly int tickets;
            /// <summary>Item behind the prize: skin id, costume itemId, costume setId or WeaponId.</summary>
            public readonly string id; public readonly Prize prize;
            public Result(string l, WeaponTier t, bool n, int tk, string i = null, bool b = false, bool off = false, Prize p = Prize.Other, string itemId = null)
            { label = l; tier = t; isNew = n; tickets = tk; icon = i; bonus = b; offRate = off; prize = p; id = itemId; }
            public Result AsBonus() => new(label, tier, isNew, tickets, icon, true, offRate, prize, id);
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

        /// <summary>Pulls until a Legendary is certain.</summary>
        public static int PullsToLegendary(Banner b) => Mathf.Max(1, b.hardPity - PlayerProfile.GetPity(b.pityKey));
        /// <summary>True when the last Legendary was off-rate: the next one is the featured prize.</summary>
        public static bool FeaturedGuaranteed(Banner b) => PlayerProfile.GetPity(b.GuaranteeKey) > 0;

        // ------------------------------------------------------------------ cost
        public enum Pay { Free, Tickets, Gems }

        public static bool CanPay(Banner b, int count, Pay pay, int today) => pay switch
        {
            Pay.Free => count == 1 && FreePullReady(today),
            Pay.Tickets => PlayerProfile.Tickets >= (count == 1 ? b.ticketSingle : b.ticketMulti),
            _ => PlayerProfile.Gem >= (count == 1 ? b.gemSingle : b.gemMulti),
        };

        /// <summary>The free daily pull, then tickets when the player has enough, otherwise gems.</summary>
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
        /// <summary>
        /// Pays, rolls and grants. <paramref name="count"/> is 1 or 10; a 10 opens
        /// <see cref="MultiBoxes"/> boxes, the last one flagged as the bonus. Null when unpaid.
        /// </summary>
        public static List<Result> Pull(Banner b, int count, Pay pay, int today, EconomyConfig econ,
                                        IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            // One transaction: the payment, every box grant and the pity are written once (a x10 used
            // to save ~13 times and fire ~11 refreshes), and a failure leaves the profile untouched.
            if (!Online.RemoteConfig.GachaOn) return null;   // switched off on the server
            List<Result> results = null;
            if (!PlayerProfile.Batch(() => results = PullUnbatched(b, count, pay, today, econ, guns, rng))) return null;
            if (results != null && results.Count > 0)
                Online.GameAnalytics.Log("gacha_pull", ("banner", b.kind.ToString().ToLowerInvariant()), ("count", count),
                    ("pay", pay.ToString().ToLowerInvariant()), ("best_rarity", results.Max(r => r.tier).ToString().ToLowerInvariant()),
                    ("new_items", results.Count(r => r.isNew)));
            return results;
        }

        static List<Result> PullUnbatched(Banner b, int count, Pay pay, int today, EconomyConfig econ,
                                          IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            if (b == null || (count != 1 && count != MultiPaid)) return null;
            if (count == 1 && pay == Pay.Free && FirstPullGun(guns) is { } gift)
            {
                if (!CanPay(b, 1, pay, today) || !Spend(b, 1, pay, today)) return null;
                PlayerProfile.AddOwnedWeapon(gift.WeaponId);
                Ftue.Complete(Ftue.FirstPull);
                PlayerProfile.SaveDaily();
                return new List<Result> { new(gift.weaponName, gift.tier, true, 0, "Gear_Sword", p: Prize.Gun, itemId: gift.WeaponId) };
            }
            if (b.kind != Kind.Event) return PullPool(b, count, pay, today, econ, guns, rng);
            if (!CanPay(b, count, pay, today) || !Spend(b, count, pay, today)) return null;

            int boxes = count == 1 ? 1 : MultiBoxes;
            var results = new List<Result>(boxes);
            int pity = PlayerProfile.GetPity(b.pityKey);
            bool guaranteed = FeaturedGuaranteed(b);
            bool epicSeen = false;
            for (int n = 0; n < boxes; n++)
            {
                pity++;
                int pick = Roll(b, rng);
                if (pity >= b.hardPity) pick = 0;                                            // hard pity
                if (count == MultiPaid && n == MultiPaid - 1 && !epicSeen && pick > 1) pick = 1; // x10 holds an Epic+
                if (pick <= 1) epicSeen = true;

                Result r;
                if (pick == 0)
                {
                    pity = 0;
                    bool featured = guaranteed || rng.Range(2) == 0;                            // 50/50
                    guaranteed = !featured;
                    r = featured ? GrantFeatured(b) : GrantOffRate(b, econ, rng);
                }
                else r = Grant(b, pick, econ, guns, rng);
                results.Add(n == MultiPaid ? r.AsBonus() : r);
            }
            PlayerProfile.SetPityInMemory(b.pityKey, pity);
            PlayerProfile.SetPityInMemory(b.GuaranteeKey, guaranteed ? 1 : 0);
            PlayerProfile.SaveDaily();
            return results;
        }

        /// <summary>
        /// The first free pull ever is a gun (owner 04/10): a ticket or coins was a flat first taste.
        /// The cheapest Uncommon-or-better gun the player does not own; null once the step is done or
        /// nothing is left to give, and the pull rolls as usual.
        /// </summary>
        public static WeaponData FirstPullGun(IReadOnlyList<WeaponData> guns)
        {
            if (guns == null || Ftue.Done(Ftue.FirstPull)) return null;
            string starter = WeaponCatalog.Active?.Starter?.weaponId;
            WeaponData pick = null;
            foreach (var g in guns)
            {
                if (g == null || g.tier < WeaponTier.Uncommon || g.WeaponId == starter || PlayerProfile.IsWeaponOwned(g.WeaponId)) continue;
                if (pick == null || g.price < pick.price) pick = g;
            }
            return pick;
        }

        // ------------------------------------------------------------------ copy from the rules
        // Every sentence about what a banner gives is built here from the same constants the pulls
        // use (QA 04/10: the LV3 popup said duplicates give shards while the Gacha said tickets).

        /// <summary>What a duplicate turns into on this banner.</summary>
        public static string DuplicateRule(Banner b) => b.kind switch
        {
            Kind.Event => $"A duplicate skin set turns into {FeaturedDupeTickets} tickets, a duplicate outfit into {Tickets(OutfitDupeTickets)}.",
            Kind.Outfits => $"A duplicate outfit turns into {Tickets(OutfitDupeTickets)}.",
            _ => "A duplicate gun turns into shards.",
        };

        /// <summary>How the banner's guarantee works, in one sentence.</summary>
        public static string PityRule(Banner b, EconomyConfig econ)
        {
            if (b.kind == Kind.Event)
                return $"A Legendary is certain by pull {b.hardPity}. It is the featured prize 50% of the time; if not, the next Legendary is. Pity carries over.";
            var pool = PoolFor(b, econ);
            return pool == null ? "" : $"An {pool.pityMinRarity} or better is certain by pull {PityThreshold(pool) + 1}.";
        }

        /// <summary>Pulls until a pool banner's Epic-or-better is certain (the forced pull included).</summary>
        public static int PullsToPoolPity(Banner b, EconomyConfig econ)
        {
            var pool = PoolFor(b, econ);
            if (pool == null) return 0;
            // GachaService forces the rarity on the pull made with pity == threshold.
            return Mathf.Max(1, PityThreshold(pool) + 1 - PlayerProfile.GetPity(b.pityKey));
        }

        static int PityThreshold(EconomyConfig.GachaPool pool) => Mathf.Max(0, pool.pityThreshold);

        public static string MultiRule => $"x{MultiPaid} opens {MultiPaid} boxes + {MultiBoxes - MultiPaid} bonus box.";

        /// <summary>The LV3 unlock popup's line about the Gacha.</summary>
        public static string IntroLine() =>
            $"Your first pull is free and always a gun. {DuplicateRule(All[All.Length - 1])} A Legendary is certain within {All[0].hardPity} pulls.";

        static string Tickets(int n) => n == 1 ? "a ticket" : $"{n} tickets";

        static int Roll(Banner b, GachaService.IRng rng)
        {
            int roll = rng.Range(100000);   // 0.001% resolution
            float acc = 0f;
            for (int i = 0; i < b.rates.Length; i++) { acc += b.rates[i].percent * 1000f; if (roll < acc) return i; }
            return b.rates.Length - 1;
        }

        static Result GrantFeatured(Banner b)
        {
            var set = Skins.WeaponSkins.Find(b.featuredSkin);
            string name = (set != null ? set.name : b.featuredSkin) + " skin set";
            if (PlayerProfile.IsSkinOwned(b.featuredSkin))
            {
                PlayerProfile.AddTickets(FeaturedDupeTickets);
                return new Result(name, WeaponTier.Legendary, false, FeaturedDupeTickets, "Gear_Sword", p: Prize.Skin, itemId: b.featuredSkin);
            }
            PlayerProfile.AddSkin(b.featuredSkin);
            return new Result(name, WeaponTier.Legendary, true, 0, "Gear_Sword", p: Prize.Skin, itemId: b.featuredSkin);
        }

        /// <summary>Lost 50/50: a gacha-only Legendary outfit piece, or tickets once all are owned.</summary>
        static Result GrantOffRate(Banner b, EconomyConfig econ, GachaService.IRng rng)
        {
            var pieces = econ?.costumeItems?.Where(c => c.source == AcquireSource.Gacha && c.rarity >= WeaponTier.Legendary
                                                        && !string.IsNullOrEmpty(c.itemId) && !PlayerProfile.IsCostumeItemOwned(c.itemId)).ToList();
            if (pieces != null && pieces.Count > 0)
            {
                var p = pieces[rng.Range(pieces.Count)];
                PlayerProfile.GrantCostumeInMemory(p.itemId);
                return new Result(p.displayName, WeaponTier.Legendary, true, 0, "Gear_Armor_Top", false, true, Prize.Costume, p.itemId);
            }
            PlayerProfile.AddTickets(OffRateFallbackTickets);
            return new Result($"{OffRateFallbackTickets} tickets", WeaponTier.Legendary, true, 0, "Ticket_Gold", false, true, Prize.Ticket);
        }

        static Result Grant(Banner b, int pick, EconomyConfig econ, IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            var rate = b.rates[pick];
            switch (pick)
            {
                case 1:
                    // Gacha-only Epic pieces first (owner: some outfits only come from the gacha).
                    var only = econ?.costumeItems?.Where(c => c.source == AcquireSource.Gacha && c.rarity == WeaponTier.Epic
                                                            && !string.IsNullOrEmpty(c.itemId) && !PlayerProfile.IsCostumeItemOwned(c.itemId)).ToList();
                    if (only != null && only.Count > 0)
                    {
                        var piece = only[rng.Range(only.Count)];
                        PlayerProfile.GrantCostumeInMemory(piece.itemId);
                        return new Result(piece.displayName, rate.tier, true, 0, rate.icon, p: Prize.Costume, itemId: piece.itemId);
                    }
                    var sets = econ?.costumeSets?.Where(s => s != null && s.rarity >= WeaponTier.Epic && s.itemIds != null && s.itemIds.Count > 0).ToList();
                    if (sets == null || sets.Count == 0) { PlayerProfile.AddTickets(OutfitDupeTickets); return new Result("Gacha ticket", rate.tier, false, OutfitDupeTickets, "Ticket_Gold", p: Prize.Ticket); }
                    var set = sets[rng.Range(sets.Count)];
                    if (PlayerProfile.IsCostumeSetOwned(set)) { PlayerProfile.AddTickets(OutfitDupeTickets); return new Result(set.displayName, rate.tier, false, OutfitDupeTickets, rate.icon, p: Prize.CostumeSet, itemId: set.setId); }
                    foreach (var id in set.itemIds) PlayerProfile.GrantCostumeInMemory(id);
                    return new Result(set.displayName, rate.tier, true, 0, rate.icon, p: Prize.CostumeSet, itemId: set.setId);
                case 2:
                    var owned = guns?.Where(g => g != null && PlayerProfile.IsWeaponOwned(g.WeaponId)).ToList();
                    if (owned == null || owned.Count == 0) { PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, NoGunCoins); return new Result($"{NoGunCoins} coins", rate.tier, true, 0, "Money_Coin", p: Prize.Coin); }
                    var gun = owned[rng.Range(owned.Count)];
                    PlayerProfile.AddWeaponShards(gun.WeaponId, EventShards);
                    return new Result($"{gun.weaponName} shards ×{EventShards}", rate.tier, true, 0, rate.icon, p: Prize.Gun, itemId: gun.WeaponId);
                case 3:
                    PlayerProfile.AddTickets(EventTickets);
                    return new Result(rate.label, rate.tier, true, 0, rate.icon, p: Prize.Ticket);
                default:
                    PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, EventCoins);
                    return new Result(rate.label, rate.tier, true, 0, rate.icon, p: Prize.Coin);
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
            int boxes = count == 1 ? 1 : MultiBoxes;
            var raw = GachaService.Pull(econ, pool, items, boxes, rng);
            if (raw == null) return null;
            if (pay != Pay.Gems) PlayerProfile.SaveDaily();
            var list = new List<Result>(raw.Count);
            for (int i = 0; i < raw.Count; i++)
            {
                var r = raw[i];
                int tickets = 0;
                if (!r.isNew && !r.isWeapon) { tickets = OutfitDupeTickets; PlayerProfile.AddTickets(tickets); }
                string label = r.isWeapon && !r.isNew ? $"{r.displayName} shards ×{r.weaponShards}" : r.displayName;
                bool isSet = !r.isWeapon && econ?.costumeSets != null && econ.costumeSets.Exists(s => s != null && s.setId == r.id);
                var res = new Result(label, r.rarity, r.isNew, tickets, r.isWeapon ? "Gear_Sword" : "Gear_Armor_Top", false, false,
                                     r.isWeapon ? Prize.Gun : isSet ? Prize.CostumeSet : Prize.Costume, r.id);
                list.Add(i == MultiPaid ? res.AsBonus() : res);
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
            string icon = b.kind == Kind.Shards ? "Gear_Sword" : "Gear_Armor_Top";
            return Enumerable.Range(0, Mathf.Min(5, pool.rarityWeights.Length)).Reverse()
                .Where(i => pool.rarityWeights[i] > 0)
                .Select(i => new Rate { label = $"{names[i]} {what}", percent = 100f * pool.rarityWeights[i] / total, tier = (WeaponTier)i, icon = icon }).ToArray();
        }
    }
}
