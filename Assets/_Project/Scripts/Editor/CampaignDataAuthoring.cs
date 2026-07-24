using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Authors the five stages' WaveData assets and the CampaignCatalog from one place.
    ///
    /// The point of generating these rather than hand-building 5 assets in the Inspector is that the
    /// enemy introduction schedule is a DESIGN RULE ("at most 2-4 new concepts per stage, no boss
    /// before its prefab exists"), and a rule is easier to keep correct in one readable table than
    /// across five separate assets. The output is still ordinary editable assets - a designer can
    /// retune any number afterwards; re-running just resets them to the authored baseline.
    ///
    /// Menu: Tools/ZombieWar/Author Campaign Data.
    /// </summary>
    public static class CampaignDataAuthoring
    {
        const string WaveDir = "Assets/_Project/Data/Waves";
        const string CampaignDir = "Assets/_Project/Data/Campaign";
        const string ZombieDir = "Assets/_Project/Data/Zombies";

        /// <summary>One authored wave: which enemies, how many, and its pacing.</summary>
        struct WaveSpec
        {
            public string label;
            public (string enemyId, int count)[] entries;
            public float interval;
            public int maxConcurrent;
            public float rest;
        }

        struct StageSpec
        {
            public string levelId, displayName, sceneName, waveAsset;
            public int minimumPower, recommendedPower;
            public string[] families, weaponIds;
            public int firstCoin, firstGold, firstGem, repeatCoin;
            public string bossEnemyId;
            public WaveSpec[] waves;
        }

        // Enemy shorthand -> stable ID.
        const string Pup = "enemy.cute.dog_pup";
        const string Meow = "enemy.cute.cat_meow";
        const string Skel = "enemy.cute.skeleton";
        const string Bark = "enemy.cute.dog_bark";
        const string Cacti = "enemy.cute.cacti";
        const string Cactus = "enemy.cute.cactus";
        const string Burrow = "enemy.cute.burrow";
        const string CactusBoss = "enemy.cute.cactus_boss";
        const string Mage = "enemy.cute.skeleton_mage";
        const string Giant = "enemy.cute.skeleton_giant";
        const string Bolt = "enemy.cute.cat_bolt";
        const string Lightning = "enemy.cute.cat_lightning";
        const string Bowwow = "enemy.cute.dog_bowwow";
        const string Mole = "enemy.cute.mole_rat";
        const string MoleKing = "enemy.cute.mole_rat_king";

        static readonly StageSpec[] Stages =
        {
            // ---- Stage 1: First Outbreak ------------------------------------------------------
            // Only walkers, then one runner late. Teaches movement and shooting, nothing else.
            new StageSpec
            {
                levelId = "level.1", displayName = "Outbreak", sceneName = "Map_Level1",
                waveAsset = "WD_Level1", minimumPower = 0, recommendedPower = 300,
                families = new[] { "Pistol", "SMG" },
                weaponIds = new[] { "weapon.sidearm.pistol_a", "weapon.smg.generic" },
                firstCoin = 400, firstGold = 0, firstGem = 0, repeatCoin = 100,
                bossEnemyId = "",
                // Density prototype (2026-07-24): ~104 total, cap ramps 12->24, interval
                // 0.6->0.45s, rests 2.0-2.5s. Keeps the walker-only onboarding of wave 1 but the
                // field should never be empty around the player.
                waves = new[]
                {
                    new WaveSpec { label = "Scouting",  entries = new[]{(Pup,12)},                     interval=0.6f, maxConcurrent=12, rest=2.5f },
                    new WaveSpec { label = "Small Pack",entries = new[]{(Pup,12),(Meow,8)},            interval=0.55f,maxConcurrent=16, rest=2.5f },
                    new WaveSpec { label = "Dry Bones", entries = new[]{(Meow,10),(Skel,8)},           interval=0.5f, maxConcurrent=20, rest=2f },
                    new WaveSpec { label = "Thickening",entries = new[]{(Pup,12),(Skel,12)},           interval=0.5f, maxConcurrent=22, rest=2f },
                    new WaveSpec { label = "Runners",   entries = new[]{(Skel,16),(Bark,6),(Pup,8)},   interval=0.45f,maxConcurrent=24, rest=0f },
                },
            },

            // ---- Stage 2: Thorn Fields --------------------------------------------------------
            // Introduces ranged pressure and the first burrower, then a boss. Three new concepts.
            new StageSpec
            {
                levelId = "level.2", displayName = "Thorn Fields", sceneName = "Map_Level2",
                waveAsset = "WD_Level2", minimumPower = 700, recommendedPower = 950,
                families = new[] { "Shotgun", "AR" },
                weaponIds = new[] { "weapon.shotgun.generic", "weapon.assault_rifle.generic", "weapon.smg.generic" },
                firstCoin = 700, firstGold = 5, firstGem = 0, repeatCoin = 180,
                bossEnemyId = CactusBoss,
                waves = new[]
                {
                    new WaveSpec { label = "Small Thorns", entries = new[]{(Pup,8),(Cacti,3)},            interval=0.9f, maxConcurrent=10, rest=5f },
                    new WaveSpec { label = "Sharpshooters",entries = new[]{(Cacti,6),(Bark,4)},           interval=0.85f,maxConcurrent=12, rest=5f },
                    new WaveSpec { label = "Ambush",       entries = new[]{(Burrow,4),(Meow,8)},          interval=0.85f,maxConcurrent=12, rest=6f },
                    new WaveSpec { label = "Thorn Forest", entries = new[]{(Cactus,5),(Cacti,6),(Bark,4)},interval=0.8f, maxConcurrent=14, rest=6f },
                    new WaveSpec { label = "Siege",        entries = new[]{(Burrow,5),(Cactus,6),(Skel,8)},interval=0.75f,maxConcurrent=16,rest=8f },
                    new WaveSpec { label = "BOSS",      entries = new[]{(CactusBoss,1),(Cacti,4)},     interval=1.2f, maxConcurrent=8,  rest=0f },
                },
            },

            // ---- Stage 3: Bone Yard -----------------------------------------------------------
            // Caster spacing + a heavy that punishes standing still. Boss is the Giant.
            new StageSpec
            {
                levelId = "level.3", displayName = "Bone Graveyard", sceneName = "Map_Level3",
                waveAsset = "WD_Level3", minimumPower = 1100, recommendedPower = 1400,
                families = new[] { "AR", "Marksman", "Shotgun" },
                weaponIds = new[] { "weapon.assault_rifle.m4a1", "weapon.marksman.sniper_generic", "weapon.shotgun.generic" },
                firstCoin = 1100, firstGold = 10, firstGem = 0, repeatCoin = 260,
                bossEnemyId = Giant,
                waves = new[]
                {
                    new WaveSpec { label = "Bones Rising",entries = new[]{(Skel,12)},                    interval=0.85f,maxConcurrent=12, rest=5f },
                    new WaveSpec { label = "Mages",       entries = new[]{(Skel,10),(Mage,3)},           interval=0.8f, maxConcurrent=14, rest=5f },
                    new WaveSpec { label = "Keep Away",   entries = new[]{(Mage,5),(Bark,6)},            interval=0.8f, maxConcurrent=14, rest=6f },
                    new WaveSpec { label = "Heavyweight", entries = new[]{(Giant,1),(Skel,10)},          interval=0.8f, maxConcurrent=14, rest=6f },
                    new WaveSpec { label = "Joint Force", entries = new[]{(Mage,5),(Cactus,5),(Skel,10)},interval=0.75f,maxConcurrent=16, rest=6f },
                    new WaveSpec { label = "Overwhelm",   entries = new[]{(Giant,1),(Mage,4),(Bark,8)},  interval=0.7f, maxConcurrent=16, rest=8f },
                    new WaveSpec { label = "BOSS",       entries = new[]{(Giant,2),(Skel,8)},           interval=1.1f, maxConcurrent=10, rest=0f },
                },
            },

            // ---- Stage 4: Wild Pack -----------------------------------------------------------
            // Speed and burrow pressure, deliberately little ranged noise so the pack reads clearly.
            new StageSpec
            {
                levelId = "level.4", displayName = "Wild Pack", sceneName = "Map_Level4",
                waveAsset = "WD_Level4", minimumPower = 1500, recommendedPower = 1800,
                families = new[] { "SMG", "AR", "Shotgun" },
                weaponIds = new[] { "weapon.smg.generic", "weapon.assault_rifle.scar_l", "weapon.shotgun.benelli_m4" },
                firstCoin = 1600, firstGold = 15, firstGem = 1, repeatCoin = 340,
                bossEnemyId = MoleKing,
                // Density prototype (2026-07-24): ~187 total, cap ramps 20->32, interval
                // 0.45->0.3s, rests 1.5-2.0s. The boss wave keeps a 30-alive minion swarm running
                // so the finale is louder than the wave before it, not quieter.
                waves = new[]
                {
                    new WaveSpec { label = "Swift Claws", entries = new[]{(Bolt,10),(Bark,10)},                        interval=0.45f,maxConcurrent=20, rest=2f },
                    new WaveSpec { label = "Thunderbolt", entries = new[]{(Lightning,10),(Bolt,10)},                   interval=0.4f, maxConcurrent=24, rest=2f },
                    new WaveSpec { label = "Burrowers",   entries = new[]{(Mole,6),(Bolt,14)},                         interval=0.4f, maxConcurrent=26, rest=2f },
                    new WaveSpec { label = "Heavy Press", entries = new[]{(Bowwow,6),(Lightning,14),(Bark,8)},         interval=0.35f,maxConcurrent=28, rest=1.5f },
                    new WaveSpec { label = "The Swarm",   entries = new[]{(Bolt,16),(Lightning,12),(Mole,6)},          interval=0.35f,maxConcurrent=30, rest=1.5f },
                    new WaveSpec { label = "Predators",   entries = new[]{(Bowwow,8),(Mole,8),(Bark,14)},              interval=0.3f, maxConcurrent=32, rest=1.5f },
                    new WaveSpec { label = "BOSS",       entries = new[]{(MoleKing,1),(Mole,8),(Bolt,16),(Lightning,10)},interval=0.35f,maxConcurrent=30, rest=0f },
                },
            },

            // ---- Stage 5: Titan Siege ---------------------------------------------------------
            // A curated mix, not all 15 at once, climaxing on the charger boss.
            new StageSpec
            {
                levelId = "level.5", displayName = "Titan Siege", sceneName = "Map_Level5",
                waveAsset = "WD_Level5", minimumPower = 1900, recommendedPower = 2200,
                families = new[] { "AR", "Shotgun", "Marksman" },
                weaponIds = new[] { "weapon.assault_rifle.ak_47", "weapon.shotgun.aa_12", "weapon.marksman.sniper_generic" },
                firstCoin = 2400, firstGold = 25, firstGem = 3, repeatCoin = 450,
                bossEnemyId = CactusBoss,
                waves = new[]
                {
                    new WaveSpec { label = "Vanguard",    entries = new[]{(Skel,10),(Bolt,6)},               interval=0.75f,maxConcurrent=14, rest=5f },
                    new WaveSpec { label = "Firepower",   entries = new[]{(Mage,5),(Cactus,5)},              interval=0.75f,maxConcurrent=14, rest=5f },
                    new WaveSpec { label = "Fast & Deep", entries= new[]{(Mole,5),(Lightning,8)},            interval=0.7f, maxConcurrent=16, rest=6f },
                    new WaveSpec { label = "Elites",      entries = new[]{(Giant,1),(Bowwow,4),(Mage,4)},    interval=0.7f, maxConcurrent=16, rest=6f },
                    new WaveSpec { label = "Full Assault",entries = new[]{(Bolt,10),(Skel,10),(Cacti,6)},    interval=0.65f,maxConcurrent=18, rest=6f },
                    new WaveSpec { label = "Twin Elites", entries = new[]{(Giant,1),(MoleKing,1),(Bark,8)},  interval=0.8f, maxConcurrent=14, rest=8f },
                    new WaveSpec { label = "FINAL BOSS",  entries = new[]{(CactusBoss,1),(Cactus,4),(Mage,3)},interval=1.2f,maxConcurrent=10, rest=0f },
                },
            },
        };

        [MenuItem("Tools/ZombieWar/Author Campaign Data")]
        public static void AuthorAll()
        {
            EnsureFolder(WaveDir);
            EnsureFolder(CampaignDir);

            var zombies = AssetDatabase.FindAssets("t:ZombieData", new[] { ZombieDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ZombieData>)
                .Where(z => z != null && !string.IsNullOrEmpty(z.enemyId))
                .ToDictionary(z => z.enemyId, z => z);

            var catalog = LoadOrCreate<CampaignCatalog>($"{CampaignDir}/CampaignCatalog.asset");
            var catalogSo = new SerializedObject(catalog);
            var levelsProp = catalogSo.FindProperty("levels");
            levelsProp.arraySize = Stages.Length;

            var missing = new HashSet<string>();

            for (int i = 0; i < Stages.Length; i++)
            {
                var stage = Stages[i];
                var waveData = LoadOrCreate<WaveData>($"{WaveDir}/{stage.waveAsset}.asset");
                var waves = new List<WaveData.Wave>(stage.waves.Length);

                foreach (var spec in stage.waves)
                {
                    var entries = new List<WaveData.SpawnEntry>(spec.entries.Length);
                    foreach (var (enemyId, count) in spec.entries)
                    {
                        if (!zombies.TryGetValue(enemyId, out var z)) { missing.Add(enemyId); continue; }
                        entries.Add(new WaveData.SpawnEntry { zombie = z, count = count });
                    }
                    waves.Add(new WaveData.Wave
                    {
                        label = spec.label,
                        entries = entries.ToArray(),
                        spawnInterval = spec.interval,
                        maxConcurrent = spec.maxConcurrent,
                        restAfterClear = spec.rest,
                    });
                }

                waveData.waves = waves.ToArray();
                EditorUtility.SetDirty(waveData);

                var e = levelsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("levelId").stringValue = stage.levelId;
                e.FindPropertyRelative("displayName").stringValue = stage.displayName;
                e.FindPropertyRelative("sceneName").stringValue = stage.sceneName;
                e.FindPropertyRelative("waves").objectReferenceValue = waveData;
                e.FindPropertyRelative("minimumPower").intValue = stage.minimumPower;
                e.FindPropertyRelative("recommendedPower").intValue = stage.recommendedPower;
                SetStringArray(e.FindPropertyRelative("suggestedFamilies"), stage.families);
                SetStringArray(e.FindPropertyRelative("suggestedWeaponIds"), stage.weaponIds);
                e.FindPropertyRelative("firstClearCoin").intValue = stage.firstCoin;
                e.FindPropertyRelative("firstClearGold").intValue = stage.firstGold;
                e.FindPropertyRelative("firstClearGem").intValue = stage.firstGem;
                e.FindPropertyRelative("repeatCoin").intValue = stage.repeatCoin;
                e.FindPropertyRelative("hasBoss").boolValue = !string.IsNullOrEmpty(stage.bossEnemyId);
                e.FindPropertyRelative("bossEnemyId").stringValue = stage.bossEnemyId ?? "";
            }

            catalogSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (missing.Count > 0)
                Debug.LogError($"[CampaignData] Unresolved enemy IDs (bake them first): {string.Join(", ", missing)}");

            Debug.Log($"[CampaignData] Authored {Stages.Length} stages -> {CampaignDir}/CampaignCatalog.asset " +
                      $"and {Stages.Length} WaveData assets in {WaveDir}.");
        }

        static void SetStringArray(SerializedProperty prop, string[] values)
        {
            prop.arraySize = values?.Length ?? 0;
            for (int i = 0; i < prop.arraySize; i++)
                prop.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
