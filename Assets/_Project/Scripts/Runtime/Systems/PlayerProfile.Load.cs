using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// PlayerProfile, part: Load, normalize and schema migration.
    public static partial class PlayerProfile
    {
        // ===== Load/normalize/migration =====

        /// Sua du lieu load ve trang thai an toan: list null -> rong, owned trung lap -> bo,
        /// tien am -> 0 (canh bao), version <= 0 -> version hien tai.
        private static ProfileData Normalize(ProfileData d)
        {
            d.ownedWeaponIds = DedupeNonEmpty(d.ownedWeaponIds);
            d.ownedCostumeGuids = DedupeNonEmpty(d.ownedCostumeGuids);
            d.equippedParts ??= new List<LoadoutState.PartSel>();
            d.weaponUpgrades ??= new List<WeaponUpgradeEntry>();
            d.weaponShards ??= new List<WeaponShardEntry>();
            d.gunStats ??= new List<GunStatEntry>();
            d.ownedBodyColors = DedupeNonEmpty(d.ownedBodyColors);
            d.ownedBodyEars = DedupeNonEmpty(d.ownedBodyEars);
            d.gachaPity ??= new List<GachaPityEntry>();
            d.unseenItems = DedupeNonEmpty(d.unseenItems);
            d.ownedFrames = DedupeNonEmpty(d.ownedFrames);
            d.ownedSkins = DedupeNonEmpty(d.ownedSkins);
            d.looks ??= new List<SavedLook>();
            d.passClaimed = DedupeNonEmpty(d.passClaimed);
            d.equippedSkins = DedupeNonEmpty(d.equippedSkins);
            d.dealsBought = DedupeNonEmpty(d.dealsBought);
            d.packsBought = DedupeNonEmpty(d.packsBought);
            if (d.tickets < 0) d.tickets = 0;
            if (float.IsNaN(d.bestSurvivalSeconds) || d.bestSurvivalSeconds < 0f) d.bestSurvivalSeconds = 0f;
            d.missionProgress ??= new List<MissionProgressEntry>();
            d.claimedMissionIds = DedupeNonEmpty(d.claimedMissionIds);
            MigrateToSingleWeapon(d);
            MigrateVoFlags(d);
            // The old premium flag was cleared at every season change, so one still set is this season's.
            if (d.passPremium && d.passPremiumSeason == 0) d.passPremiumSeason = Math.Max(1, d.passSeason);
            d.passPremium = false;
            if (d.passXp < 0) d.passXp = 0;
            if (d.accountXp < 0) d.accountXp = 0;
            if (string.IsNullOrEmpty(d.playerId)) d.playerId = UnityEngine.Random.Range(10000000, 99999999).ToString();
            if (string.IsNullOrWhiteSpace(d.displayName)) d.displayName = "Survivor " + d.playerId.Substring(4);
            if (d.runsPlayed < 0) d.runsPlayed = 0;
            if (d.bestKills < 0) d.bestKills = 0;
            if (d.totalKills < 0) d.totalKills = 0;
            if (d.bossesDefeated < 0) d.bossesDefeated = 0;
            if (d.peakThreat < 0) d.peakThreat = 0;
            if (float.IsNaN(d.totalSeconds) || d.totalSeconds < 0f) d.totalSeconds = 0f;
            d.bodyColor ??= "";
            d.bodyEar ??= "";

            if (d.coin < 0 || d.gold < 0 || d.gem < 0)
            {
                Debug.LogWarning($"[PlayerProfile] So du am trong save (coin={d.coin}, gold={d.gold}, gem={d.gem}) — clamp ve 0.");
                d.coin = Math.Max(0, d.coin);
                d.gold = Math.Max(0, d.gold);
                d.gem = Math.Max(0, d.gem);
            }

            if (d.version <= 0) d.version = SchemaVersion;
            else if (d.version > SchemaVersion)
                Debug.LogWarning($"[PlayerProfile] Profile version {d.version} moi hon build ({SchemaVersion}) — doc theo schema hien tai.");
            // version < SchemaVersion: v1 -> v2 is MigrateToSingleWeapon, v2 -> v3 MigrateVoFlags (both idempotent).
            d.version = Math.Max(d.version, SchemaVersion);
            return d;
        }

        /// v2 -> v3: radio-line flags lived in ftueSteps as "vo.&lt;id&gt;" and grew it by hundreds of entries
        /// that every FTUE check scanned. They move to voFlags without the prefix.
        private static void MigrateVoFlags(ProfileData d)
        {
            d.ftueSteps = DedupeNonEmpty(d.ftueSteps);
            d.voFlags = DedupeNonEmpty(d.voFlags);
            for (int i = d.ftueSteps.Count - 1; i >= 0; i--)
            {
                var step = d.ftueSteps[i];
                if (!step.StartsWith("vo.", StringComparison.Ordinal)) continue;
                var flag = step.Substring(3);
                if (!d.voFlags.Contains(flag)) d.voFlags.Add(flag);
                d.ftueSteps.RemoveAt(i);
            }
        }

        /// v1 -> v2: the run weapon is the first filled legacy slot, long guns first - a player who
        /// equipped a rifle chose it over the pistol they were handed. The slots are then cleared so
        /// they can never be read again.
        private static void MigrateToSingleWeapon(ProfileData d)
        {
            d.weapon ??= "";
            if (d.weapon.Length == 0)
                d.weapon = !string.IsNullOrEmpty(d.longA) ? d.longA
                         : !string.IsNullOrEmpty(d.longB) ? d.longB
                         : d.pistol ?? "";
            d.pistol = d.longA = d.longB = "";
        }

        private static List<string> DedupeNonEmpty(List<string> list)
        {
            var result = new List<string>(list?.Count ?? 0);
            if (list == null) return result;
            for (int i = 0; i < list.Count; i++)
                if (!string.IsNullOrEmpty(list[i]) && !result.Contains(list[i]))
                    result.Add(list[i]);
            return result;
        }

        // Mirror cua LoadoutState.SaveData cu — chi de parse "zw.loadout", khong dung cho ghi.
        [Serializable]
        private class LegacyLoadout
        {
            public string pistol = "";
            public string longA = "";
            public string longB = "";
            public List<LoadoutState.PartSel> parts = new();
        }

        /// Import du lieu cu thanh profile moi. Weapon id copy NGUYEN VAN (co the la ten asset cu) —
        /// EnsureValidLoadout se canonical hoa o lan ApplyTo dau tien vi luc do moi co arsenal.
        /// Costume: chi giu entry slot+guid hop le; guid dang trang bi duoc seed lam owned de giu
        /// nguyen ngoai hinh da luu (KHONG cap toan bo catalog).
        /// <summary>Unprefixed PlayerPrefs keys the profile migrates from (pre-profile builds).</summary>
        private static readonly string[] LegacyPrefKeys = { "zw.loadout", "wallet_coin", "wallet_gold", "wallet_gem" };

        private static ProfileData MigrateFromLegacy()
        {
            var p = new ProfileData();

            string json = LegacyReadString("zw.loadout");
            if (!string.IsNullOrEmpty(json))
            {
                LegacyLoadout old = null;
                try { old = JsonUtility.FromJson<LegacyLoadout>(json); }
                catch { /* JSON hong — xu ly nhu khong co */ }

                if (old != null)
                {
                    p.pistol = old.pistol ?? "";
                    p.longA = old.longA ?? "";
                    p.longB = old.longB ?? "";
                    if (old.parts != null)
                    {
                        foreach (var part in old.parts)
                        {
                            if (string.IsNullOrEmpty(part.slot) || string.IsNullOrEmpty(part.guid)) continue;
                            if (p.equippedParts.Exists(x => x.slot == part.slot)) continue;
                            p.equippedParts.Add(part);
                            if (!p.ownedCostumeGuids.Contains(part.guid)) p.ownedCostumeGuids.Add(part.guid);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[PlayerProfile] 'zw.loadout' hong/khong parse duoc — bo qua phan loadout, key cu giu nguyen.");
                }
            }

            p.coin = ReadLegacyCurrency("wallet_coin");
            p.gold = ReadLegacyCurrency("wallet_gold");
            p.gem = ReadLegacyCurrency("wallet_gem");
            return p;
        }

        private static long ReadLegacyCurrency(string key)
        {
            int value = LegacyReadInt(key);
            if (value < 0)
            {
                Debug.LogWarning($"[PlayerProfile] Legacy '{key}' am ({value}) — import ve 0.");
                return 0;
            }
            return value;
        }
    }
}
