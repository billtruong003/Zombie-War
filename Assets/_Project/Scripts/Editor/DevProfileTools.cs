using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>One visible, safe control surface for local profile/economy testing.</summary>
    public sealed class DevProfileTools : EditorWindow
    {
        const string CostumeCatalogPath = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        const string WeaponDataDir = "Assets/_Project/Data/Weapons";
        long _coin = 5000, _gold = 5000, _gem = 500;
        Vector2 _scroll;

        [MenuItem("ZombieWar/Dev/Player & Economy Tools")]
        static void Open() => GetWindow<DevProfileTools>("ZombieWar Dev Tools");

        void OnEnable() => SyncFromProfile();

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("PLAYER PROFILE", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Editor/development only. Every action uses PlayerProfile APIs and saves immediately.", MessageType.Info);

            _coin = EditorGUILayout.LongField("Coin", _coin);
            _gold = EditorGUILayout.LongField("Gold", _gold);
            _gem = EditorGUILayout.LongField("Gem", _gem);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply wallet")) ApplyWallet();
                if (GUILayout.Button("Refresh")) SyncFromProfile();
            }
            if (GUILayout.Button("Add 1,000 Coin + 1,000 Gold + 100 Gem"))
            {
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 1000);
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, 1000);
                PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 100);
                SyncFromProfile();
            }

            Space();
            EditorGUILayout.LabelField("UNLOCK CHEATS", EditorStyles.boldLabel);
            if (GUILayout.Button("Unlock all 25 weapons")) UnlockWeapons();
            if (GUILayout.Button("Unlock all Pro Casual costume items")) UnlockCostumes();

            Space();
            EditorGUILayout.LabelField("RESET", EditorStyles.boldLabel);
            if (GUILayout.Button("Reset costume to starter defaults")) ResetCostume();
            GUI.backgroundColor = new Color(1f, .45f, .45f);
            if (GUILayout.Button("DELETE ENTIRE PLAYER PROFILE"))
            {
                if (EditorUtility.DisplayDialog("Delete player profile?",
                    "This deletes wallet, ownership, loadout, pity, shards and upgrades. Legacy keys are preserved for migration testing.",
                    "Delete profile", "Cancel"))
                {
                    PlayerProfile.ResetForDev();
                    SyncFromProfile();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();
        }

        static void Space() { EditorGUILayout.Space(12); EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); }

        void ApplyWallet()
        {
            PlayerProfile.SetBalanceForDev(PlayerProfile.CurrencyKind.Coin, _coin);
            PlayerProfile.SetBalanceForDev(PlayerProfile.CurrencyKind.Gold, _gold);
            PlayerProfile.SetBalanceForDev(PlayerProfile.CurrencyKind.Gem, _gem);
            SyncFromProfile();
        }

        void SyncFromProfile()
        {
            _coin = PlayerProfile.Coin; _gold = PlayerProfile.Gold; _gem = PlayerProfile.Gem;
            Repaint();
        }

        static ModularCostumeCatalog Catalog() => AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CostumeCatalogPath);

        static void UnlockWeapons()
        {
            var weapons = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponDataDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(x => x != null).OrderBy(x => x.WeaponId).ToList();
            int added = PlayerProfile.UnlockAllWeaponsForDev(weapons);
            Debug.Log($"[DevProfileTools] Weapons: +{added}, owned {PlayerProfile.OwnedWeaponIds.Count}/{weapons.Count}.");
        }

        static void UnlockCostumes()
        {
            var catalog = Catalog();
            if (catalog == null) { Debug.LogError("[DevProfileTools] CasualCostumeCatalog missing."); return; }
            int added = PlayerProfile.UnlockAllCostumes(catalog);
            Debug.Log($"[DevProfileTools] Pro Casual costume: +{added}, catalog total {catalog.TotalParts}.");
        }

        static void ResetCostume()
        {
            var catalog = Catalog();
            if (catalog == null) { Debug.LogError("[DevProfileTools] CasualCostumeCatalog missing."); return; }
            PlayerProfile.ResetCostumeProgressForDev(catalog);
        }
    }
}
