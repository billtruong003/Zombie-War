using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Gun crates (owner 05/10 night, backlog 3b): every gun comes from a crate, and a crate gives
    /// SHARDS of one gun. Low tiers come easily and high tiers rarely, so a Legendary shard feels like
    /// a prize. A gun unlocks by itself once its shards reach <see cref="UnlockShards"/> (Lukas says
    /// "Welcome to the family"). Two crates:
    /// <list type="bullet">
    /// <item><b>Gun Crate</b> (coin, the main coin sink) — Common to Epic, never Legendary; 10 Epic
    /// shards are certain within <see cref="CoinPity"/> pulls.</item>
    /// <item><b>Elite Crate</b> (gems or a ticket) — Rare to Legendary shards, and a 0.6 % chance of a
    /// whole Legendary gun; a whole Legendary is certain by pull <see cref="GemPity"/>. One shared pool,
    /// no featured gun (owner).</item>
    /// </list>
    /// Shards lean toward the gun the player is already collecting (<see cref="FocusShare"/>), so they
    /// pile up into a gun instead of spreading thin. Numbers await the owner's approval and the coin
    /// formula (#33); the screens wait for their mockups.
    /// </summary>
    public static class GunCrates
    {
        public enum Crate { Coin, Gem }

        /// <summary>Shards that unlock a gun, by rarity (Common..Legendary).</summary>
        public static readonly int[] UnlockShards = { 20, 30, 40, 60, 100 };

        public const long CoinSingle = 1500, CoinMulti = 13500;
        public const int GemSingle = 30, GemMulti = 270, TicketSingle = 1, TicketMulti = 9;
        public const int CoinPity = 40, GemPity = 90, Multi = 10;
        public const float FocusShare = 0.6f;
        public const string CoinPityKey = "crate.coin", GemPityKey = "crate.gem";

        /// <summary>One line of a crate's table: a rarity, how many shards, and its weight (per 1000).</summary>
        public readonly struct Line
        {
            public readonly WeaponTier tier; public readonly int shards, weight; public readonly bool wholeGun;
            public Line(WeaponTier t, int s, int w, bool whole = false) { tier = t; shards = s; weight = w; wholeGun = whole; }
        }

        public static readonly Line[] CoinTable =
        {
            new(WeaponTier.Common, 5, 500), new(WeaponTier.Uncommon, 5, 300), new(WeaponTier.Rare, 4, 150), new(WeaponTier.Epic, 3, 50),
        };

        public static readonly Line[] GemTable =
        {
            new(WeaponTier.Rare, 6, 550), new(WeaponTier.Epic, 4, 380), new(WeaponTier.Legendary, 5, 64), new(WeaponTier.Legendary, 0, 6, true),
        };

        public readonly struct Result
        {
            public readonly string weaponId; public readonly WeaponTier tier;
            public readonly int shards; public readonly bool wholeGun, unlocked;
            public Result(string id, WeaponTier t, int s, bool whole, bool unlocked)
            { weaponId = id; tier = t; shards = s; wholeGun = whole; this.unlocked = unlocked; }
        }

        public static int UnlockFor(WeaponTier t) => UnlockShards[Mathf.Clamp((int)t, 0, UnlockShards.Length - 1)];

        /// <summary>Pulls <paramref name="count"/> (1 or 10) from a crate, paying with coin (Gun Crate)
        /// or gems / tickets (Elite Crate). One profile transaction; null when it could not be paid.</summary>
        public static List<Result> Pull(Crate crate, int count, bool useTickets, IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            if ((count != 1 && count != Multi) || guns == null || rng == null) return null;
            List<Result> results = null;
            if (!PlayerProfile.Batch(() => results = PullUnbatched(crate, count, useTickets, guns, rng))) return null;
            return results;
        }

        static List<Result> PullUnbatched(Crate crate, int count, bool useTickets, IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            if (!Pay(crate, count, useTickets)) return null;
            var table = crate == Crate.Coin ? CoinTable : GemTable;
            string key = crate == Crate.Coin ? CoinPityKey : GemPityKey;
            int pity = PlayerProfile.GetPity(key);
            var list = new List<Result>(count);
            for (int i = 0; i < count; i++)
            {
                pity++;
                Line line;
                if (crate == Crate.Gem && pity >= GemPity) line = GemTable[3];                       // whole Legendary
                else if (crate == Crate.Coin && pity >= CoinPity) line = new Line(WeaponTier.Epic, 10, 0);
                else line = Roll(table, rng);
                // Only the guaranteed prize resets the counter: a whole Legendary (Elite) or an Epic (Gun Crate).
                bool top = crate == Crate.Gem ? line.wholeGun : line.tier == WeaponTier.Epic;
                if (top) pity = 0;
                var r = Grant(line, guns, rng);
                if (r.weaponId != null) list.Add(r);
            }
            PlayerProfile.SetPityInMemory(key, pity);
            return list;
        }

        static bool Pay(Crate crate, int count, bool useTickets)
        {
            bool single = count == 1;
            if (crate == Crate.Coin) return PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Coin, single ? CoinSingle : CoinMulti);
            if (useTickets) return PlayerProfile.TrySpendTickets(single ? TicketSingle : TicketMulti);
            return PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Gem, single ? GemSingle : GemMulti);
        }

        static Line Roll(Line[] table, GachaService.IRng rng)
        {
            int total = 0; foreach (var l in table) total += l.weight;
            int roll = rng.Range(total), acc = 0;
            foreach (var l in table) { acc += l.weight; if (roll < acc) return l; }
            return table[table.Length - 1];
        }

        static Result Grant(Line line, IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            // A tier with no gun in the catalog falls back to the nearest lower tier, then higher.
            WeaponData gun = null;
            for (int d = 0; d < 5 && gun == null; d++)
            {
                int lo = (int)line.tier - d, hi = (int)line.tier + d;
                if (lo >= 0) gun = PickGun((WeaponTier)lo, guns, rng);
                if (gun == null && d > 0 && hi <= 4) gun = PickGun((WeaponTier)hi, guns, rng);
            }
            if (gun == null) return default;
            string id = gun.WeaponId;
            int need = UnlockFor(gun.tier);
            if (line.wholeGun)
            {
                if (!PlayerProfile.IsWeaponOwned(id)) { PlayerProfile.GrantWeaponInMemory(id); return new Result(id, gun.tier, 0, true, true); }
                PlayerProfile.AddWeaponShardsInMemory(id, need);   // a whole duplicate is a gun's worth of shards
                return new Result(id, gun.tier, need, true, false);
            }
            PlayerProfile.AddWeaponShardsInMemory(id, line.shards);
            bool unlocked = false;
            if (!PlayerProfile.IsWeaponOwned(id) && PlayerProfile.GetWeaponShards(id) >= need)
            {
                PlayerProfile.SpendWeaponShardsInMemory(id, need);
                PlayerProfile.GrantWeaponInMemory(id);
                unlocked = true;
            }
            return new Result(id, gun.tier, line.shards, false, unlocked);
        }

        /// <summary>A gun of the tier: the one being collected (unowned with shards) most of the time,
        /// else any unowned one, else an owned one (its shards buy stars).</summary>
        public static WeaponData PickGun(WeaponTier tier, IReadOnlyList<WeaponData> guns, GachaService.IRng rng)
        {
            var collecting = new List<WeaponData>(); var unowned = new List<WeaponData>(); var owned = new List<WeaponData>();
            foreach (var g in guns)
            {
                if (g == null || g.tier != tier || string.IsNullOrEmpty(g.WeaponId)) continue;
                if (PlayerProfile.IsWeaponOwned(g.WeaponId)) owned.Add(g);
                else if (PlayerProfile.GetWeaponShards(g.WeaponId) > 0) collecting.Add(g);
                else unowned.Add(g);
            }
            if (collecting.Count > 0 && (unowned.Count == 0 || rng.Range(1000) < FocusShare * 1000f))
                return collecting[rng.Range(collecting.Count)];
            if (unowned.Count > 0) return unowned[rng.Range(unowned.Count)];
            return owned.Count > 0 ? owned[rng.Range(owned.Count)] : null;
        }
    }
}
