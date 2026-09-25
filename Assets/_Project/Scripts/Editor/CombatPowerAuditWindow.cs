using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Read-only audit of the power curve: every weapon's effective DPS and power, strongest first.
    /// It is the quick check that a newly onboarded weapon lands in the band its tier claims.
    /// It changes nothing.
    ///
    /// Menu: Tools/ZombieWar/Combat Power Audit.
    /// </summary>
    public class CombatPowerAuditWindow : EditorWindow
    {
        Vector2 _scroll;
        List<WeaponData> _weapons;

        [MenuItem("Tools/ZombieWar/Combat Power Audit")]
        public static void Open() => GetWindow<CombatPowerAuditWindow>("Combat Power");

        void OnEnable() => Reload();

        void Reload()
        {
            // A5: catalog-driven. The old scan was project-wide (no folder filter), so it also swept
            // any stray WeaponData outside the roster.
            _weapons = ZombieWar.EditorTools.WeaponCatalogAccess.AllWeaponsForBuild()
                .Where(w => w != null)
                .OrderByDescending(w => CombatPower.EffectiveDps(w, 1))
                .ToList();
        }

        void OnGUI()
        {
            if (GUILayout.Button("Reload")) Reload();
            if (_weapons == null) return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

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
