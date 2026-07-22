using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Read-only audit of the power curve: every weapon's effective DPS at each star level, the
    /// player's current Combat Power, and whether each campaign stage's gate passes or fails.
    ///
    /// Exists because the stage gates are numbers that are easy to author into an impossible curve.
    /// Seeing "Stage 4 needs 3200, your best possible loadout is 2900" in one place is what catches
    /// a circular progression before a player does. It changes nothing - the only writes available
    /// are the pre-existing dev cheats elsewhere.
    ///
    /// Menu: Tools/ZombieWar/Combat Power Audit.
    /// </summary>
    public class CombatPowerAuditWindow : EditorWindow
    {
        Vector2 _scroll;
        CampaignCatalog _catalog;
        List<WeaponData> _weapons;

        [MenuItem("Tools/ZombieWar/Combat Power Audit")]
        public static void Open() => GetWindow<CombatPowerAuditWindow>("Combat Power");

        void OnEnable() => Reload();

        void Reload()
        {
            _weapons = AssetDatabase.FindAssets("t:WeaponData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<WeaponData>)
                .Where(w => w != null)
                .OrderByDescending(w => CombatPower.EffectiveDps(w, 1))
                .ToList();

            _catalog = AssetDatabase.FindAssets("t:CampaignCatalog")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CampaignCatalog>)
                .FirstOrDefault();
        }

        void OnGUI()
        {
            if (GUILayout.Button("Reload")) Reload();
            if (_weapons == null) return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int current = CombatPower.Current(_weapons);
            EditorGUILayout.LabelField("Equipped", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Current Combat Power: {current}");

            // What the player could reach with the best three weapons they own, all fully starred.
            // This is the ceiling a stage gate must stay under to remain reachable.
            var ownedBest = _weapons
                .Where(w => PlayerProfile.IsWeaponOwned(w.WeaponId))
                .OrderByDescending(w => CombatPower.WeaponPower(w, 3))
                .Take(3).ToList();
            int ownedCeiling = CombatPower.Evaluate(ownedBest);

            var allBest = _weapons.OrderByDescending(w => CombatPower.WeaponPower(w, 3)).Take(3).ToList();
            int globalCeiling = CombatPower.Evaluate(allBest);

            EditorGUILayout.LabelField($"Ceiling with OWNED weapons at 3★: {ownedCeiling}");
            EditorGUILayout.LabelField($"Ceiling with ANY weapons at 3★:   {globalCeiling}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stage gates", EditorStyles.boldLabel);
            if (_catalog == null) EditorGUILayout.HelpBox("No CampaignCatalog found.", MessageType.Warning);
            else
            {
                for (int i = 0; i < _catalog.Count; i++)
                {
                    var level = _catalog.Get(i);
                    var gate = _catalog.Evaluate(i, current);

                    string status = gate.CanPlay ? "OPEN" : gate.State.ToString().ToUpperInvariant();
                    EditorGUILayout.LabelField(
                        $"{level.levelId}  {level.displayName}",
                        $"min {level.minimumPower} / rec {level.recommendedPower} -> {status}");

                    if (!gate.CanPlay)
                        EditorGUILayout.LabelField(" ", gate.Reason, EditorStyles.miniLabel);

                    // The check that matters: an unreachable gate is a dead end, not a challenge.
                    if (level.minimumPower > globalCeiling)
                        EditorGUILayout.HelpBox(
                            $"UNREACHABLE: needs {level.minimumPower} but the best possible loadout in the " +
                            $"entire roster is {globalCeiling}.", MessageType.Error);
                    else if (level.minimumPower > ownedCeiling)
                        EditorGUILayout.HelpBox(
                            $"Requires buying weapons: needs {level.minimumPower}, currently-owned ceiling " +
                            $"is {ownedCeiling}.", MessageType.Info);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Weapons by effective DPS", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("id", "1★ dps / 3★ dps / 3★ power", EditorStyles.miniBoldLabel);
            foreach (var w in _weapons)
            {
                EditorGUILayout.LabelField(
                    $"{w.WeaponId}{(PlayerProfile.IsWeaponOwned(w.WeaponId) ? "  (owned)" : "")}",
                    $"{CombatPower.EffectiveDps(w, 1):F0} / {CombatPower.EffectiveDps(w, 3):F0} / " +
                    $"{CombatPower.WeaponPower(w, 3):F0}");
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
