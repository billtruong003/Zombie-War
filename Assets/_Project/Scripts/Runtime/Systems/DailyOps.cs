using System;
using System.Collections.Generic;

namespace ZombieWar
{
    /// <summary>
    /// Daily Ops (backlog #19, owner-approved 05/10): four missions a day, one or two of them tied to a
    /// gun family the player OWNS (no loaner guns: trying an unowned gun is its own mode). Every
    /// mission pays coin, pass XP and 5 shards of the gun that finished it; finishing all four opens the
    /// daily chest (<see cref="PlayerProfile.TryClaimDailyChest"/>).
    ///
    /// A mission's id encodes the whole mission ("ops.kill.Shotgun.160"), so yesterday's ids still
    /// decode after the day rolls (the mission window clears them by scope) and nothing is stored but
    /// progress. The set is deterministic per day, player and owned families.
    /// </summary>
    public static class DailyOps
    {
        public const int Count = 4;
        public const int ShardsPerMission = 5;
        const string Prefix = "ops.";

        public enum Template { Kill, Survive, Card, Station, Elite, Runs }

        /// <summary>A mission's gun family, or null for "any gun".</summary>
        public static WeaponClass? FamilyOf(PassMission m) => m is DailyOp op ? op.family : null;

        /// <summary>One Daily Ops mission: a pass mission with an optional gun family.</summary>
        public sealed class DailyOp : PassMission
        {
            public readonly WeaponClass? family;
            public readonly Template template;

            public DailyOp(string id, string title, MissionMetric metric, int target, int passXp, int coin, WeaponClass? family, Template template)
                : base(id, title, MissionScope.Daily, metric, target, passXp, coin)
            {
                this.family = family; this.template = template;
            }
        }

        /// <summary>Today's four missions for this player.</summary>
        public static List<PassMission> For(int dayKey, int seed, IReadOnlyList<WeaponClass> ownedFamilies, int accountLevel)
        {
            var families = new List<WeaponClass>();
            if (ownedFamilies != null)
                foreach (var f in ownedFamilies) if (!families.Contains(f)) families.Add(f);
            if (families.Count == 0) families.Add(WeaponClass.Sidearm);
            families.Sort();

            int level = Math.Max(1, accountLevel);
            var list = new List<PassMission>(Count);

            // 1. A gun mission. Kill and survive alternate by day, so two days never deal the same set.
            var first = families[(int)(Hash(dayKey, seed, 1) % (uint)families.Count)];
            bool killFirst = (dayKey & 1) == 0;
            list.Add(Make(killFirst ? Template.Kill : Template.Survive, first, level));

            // 2. A second gun mission on another family (some days, when there is one), else a build mission.
            if (families.Count >= 2 && Hash(dayKey, seed, 2) % 2u == 0u)
            {
                var others = families.FindAll(f => f != first);
                var second = others[(int)(Hash(dayKey, seed, 3) % (uint)others.Count)];
                list.Add(Make(killFirst ? Template.Survive : Template.Kill, second, level));
            }
            else list.Add(Make(Template.Card, null, level));

            // 3. A world mission, 4. a light one that always fits a five-minute session.
            list.Add(Make(Hash(dayKey, seed, 4) % 2u == 0u ? Template.Station : Template.Elite, null, level));
            list.Add(Make(Template.Runs, null, level));
            return list;
        }

        /// <summary>Builds a mission from its template; the id round-trips through <see cref="Decode"/>.</summary>
        public static DailyOp Make(Template t, WeaponClass? family, int level)
        {
            int target = TargetFor(t, level);
            return Build(t, family, target);
        }

        static DailyOp Build(Template t, WeaponClass? family, int target)
        {
            string fam = family.HasValue ? family.Value.ToString() : "any";
            string id = $"{Prefix}{t.ToString().ToLowerInvariant()}.{fam}.{target}";
            string gun = family.HasValue ? FamilyName(family.Value) : null;
            bool gunOp = family.HasValue;
            int xp = gunOp ? 150 : 100, coin = gunOp ? 300 : 200;
            return t switch
            {
                Template.Kill => new DailyOp(id, gun != null ? $"Kill {target} monsters with a {gun}" : $"Kill {target} monsters", MissionMetric.KillAny, target, xp, coin, family, t),
                Template.Survive => new DailyOp(id, gun != null ? $"Survive {target} minutes with a {gun}" : $"Survive {target} minutes in one run", MissionMetric.SurviveMinutes, target, xp, coin, family, t),
                Template.Card => new DailyOp(id, $"Pick {target} level-up cards", MissionMetric.ChooseCard, target, xp, coin, family, t),
                Template.Station => new DailyOp(id, $"Activate {target} stations", MissionMetric.CompleteStation, target, xp, coin, family, t),
                Template.Elite => new DailyOp(id, target == 1 ? "Defeat an elite" : $"Defeat {target} elites", MissionMetric.KillElite, target, xp, coin, family, t),
                _ => new DailyOp(id, $"Play {target} runs", MissionMetric.FinishRun, target, xp, coin, family, t),
            };
        }

        /// <summary>Targets grow with the account (genre rule: a newcomer finishes all four in a session or two).</summary>
        public static int TargetFor(Template t, int level)
        {
            int l = Math.Min(Math.Max(1, level), 10);
            return t switch
            {
                Template.Kill => (100 + 15 * l) / 10 * 10,
                Template.Survive => 3 + (level >= 6 ? 1 : 0) + (level >= 12 ? 1 : 0),
                Template.Card => 5,
                Template.Station => 2,
                Template.Elite => level >= 8 ? 2 : 1,
                _ => 2,
            };
        }

        /// <summary>The mission an "ops." id names, or null.</summary>
        public static DailyOp Decode(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            var parts = id.Substring(Prefix.Length).Split('.');
            if (parts.Length != 3) return null;
            if (!Enum.TryParse(parts[0], true, out Template t)) return null;
            WeaponClass? family = null;
            if (parts[1] != "any")
            {
                if (!Enum.TryParse(parts[1], false, out WeaponClass f)) return null;
                family = f;
            }
            if (!int.TryParse(parts[2], out int target) || target <= 0) return null;
            return Build(t, family, target);
        }

        public static string FamilyName(WeaponClass f) => f switch
        {
            WeaponClass.Sidearm => "pistol",
            WeaponClass.SMG => "SMG",
            WeaponClass.AssaultRifle => "rifle",
            WeaponClass.Shotgun => "shotgun",
            WeaponClass.LMG => "machine gun",
            WeaponClass.Marksman => "sniper",
            WeaponClass.Railgun => "railgun",
            WeaponClass.Flamethrower => "flamethrower",
            WeaponClass.Tesla => "tesla gun",
            WeaponClass.Laser => "laser",
            _ => "rocket launcher",
        };

        static uint Hash(int day, int seed, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)day) * 16777619u;
                h = (h ^ (uint)seed) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return h;
            }
        }

        /// <summary>A stable per-player number from the profile's player id.</summary>
        public static int SeedFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return 0;
            unchecked { int h = 17; foreach (char c in playerId) h = h * 31 + c; return h; }
        }
    }
}
