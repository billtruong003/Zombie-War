using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BillGameCore;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using ZombieWar.Audio;

namespace ZombieWar.Editor.Audio
{
    /// <summary>Scans every imported source pack, addresses the useful runtime set, applies mobile
    /// import profiles, builds Bill.Audio's runtime catalog, and writes the human audit CSVs.</summary>
    public static class ZombieWarCuratedAudioBuilder
    {
        private const string CatalogId = "ZW_CURATED_V1";
        private const string CatalogPath = "Assets/Resources/Audio/AddressableAudioCatalog.asset";
        private const string RuntimeLibraryPath = "Assets/Resources/Audio/ZombieWarRuntimeAudioLibrary.asset";
        private const string BootstrapConfigPath = "Assets/Resources/BillBootstrapConfig.asset";
        private const string InventoryReport = "Docs/Reference/Audio/AUDIO_SOURCE_INVENTORY.csv";
        private const string MappingReport = "Docs/Reference/Audio/AUDIO_RUNTIME_MAPPING.csv";

        private static readonly string[] SourceRoots =
        {
            "Assets/FreeWeaponSounds",
            "Assets/Deadly Kombat Free version",
            "Assets/ZombieHorrorPackageFree",
            "Assets/ThirdParty/Epic Toon FX/Sound",
            "Assets/Footsteps Pack Expanded",
        };

        private static readonly string[] GroupNames =
        {
            "ZW_Audio_Core",
            "ZW_Audio_Weapons",
            "ZW_Audio_World",
            "ZW_Audio_Zombies",
            "ZW_Audio_Footsteps",
        };

        private static readonly string[] PreloadLabels =
        {
            "zw-audio-core",
            "zw-audio-weapons",
            "zw-audio-world",
            "zw-audio-zombies",
            "zw-audio-footsteps",
        };

        [MenuItem("Zombie War/Audio/Build Curated Runtime Audio")]
        public static void Build()
        {
            var inventory = ScanInventory();
            var selected = inventory.Where(item => item.selected).ToArray();
            if (selected.Length == 0) throw new InvalidDataException("No curated audio assets were selected.");

            ConfigureImporters(selected);
            ConfigureAddressables(selected);
            BuildRuntimeCatalog(selected);
            UpdateWeaponKeys();
            UpdateZombieKeys();
            UpdateBombKey();
            WriteReports(inventory, selected);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();

            int variants = selected.Sum(item => item.cueKeys.Count);
            Debug.Log($"[ZombieWar Audio] Curated build PASS: {selected.Length} addressed clips / {variants} cue variants. "
                + $"Audited {inventory.Count} source clips.");
        }

        [MenuItem("Zombie War/Audio/Validate Curated Runtime Audio")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AddressableAudioCatalog>(CatalogPath)
                ?? throw new InvalidDataException("Runtime audio catalog is missing.");
            if (catalog.CatalogId != CatalogId || catalog.Variants.Length == 0)
                throw new InvalidDataException("Curated runtime audio catalog is empty or stale.");

            var keys = catalog.Variants.Select(item => item.cueKey).ToHashSet(StringComparer.Ordinal);
            string[] required =
            {
                "music.hub", "music.run.stage1", "stinger.run.start", "stinger.wave.start",
                "stinger.wave.clear", "stinger.victory", "stinger.defeat",
                "sfx.player.hurt", "sfx.player.death", "sfx.player.bomb.throw",
                "sfx.player.bomb.bounce", "sfx.player.bomb.explode",
                "sfx.pickup.coin", "sfx.pickup.gem", "sfx.pickup.health", "sfx.pickup.bomb",
                "sfx.prop.crate.hit", "sfx.prop.crate.break", "sfx.prop.barrel.hit",
                "sfx.prop.barrel.explode", "sfx.footstep.player.earth",
            };
            foreach (string key in required)
                if (!keys.Contains(key)) throw new InvalidDataException($"Required runtime cue is missing: {key}");

            // A5: the CATALOG is the roster, not the folder. AllWeaponsForBuild() throws when the
            // two disagree, so a weapon added without a catalog entry fails the build here rather
            // than losing its audio silently the first time a player fires it.
            foreach (var data in ZombieWar.EditorTools.WeaponCatalogAccess.WeaponsRequiringAudio())
            {
                if (data == null || !keys.Contains(data.fireSfxKey) || !keys.Contains(data.reloadSfxKey))
                    throw new InvalidDataException(
                        $"Weapon audio mapping is invalid: {data?.name ?? "(null)"} " +
                        $"(fire='{data?.fireSfxKey}', reload='{data?.reloadSfxKey}')");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ZombieData", new[] { "Assets/_Project/Data/Zombies" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<ZombieData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null || !keys.Contains(data.attackSfxKey) || !keys.Contains(data.hurtSfxKey)
                    || !keys.Contains(data.deathSfxKey) || !keys.Contains(data.impactSfxKey))
                    throw new InvalidDataException($"Creature audio mapping is invalid: {data?.name ?? guid}");
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false)
                ?? throw new InvalidDataException("Addressables settings are missing.");
            int addressed = GroupNames.Sum(name => settings.FindGroup(name)?.entries.Count ?? 0);
            if (addressed == 0) throw new InvalidDataException("No curated Addressables entries exist.");

            var config = AssetDatabase.LoadAssetAtPath<BillBootstrapConfig>(BootstrapConfigPath);
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(RuntimeLibraryPath);
            if (config == null || library == null || config.defaultAudioLibrary != library)
                throw new InvalidDataException("Bill.Audio runtime library reference is invalid.");

            Debug.Log($"[ZombieWar Audio] Validation PASS: {addressed} addressed clips / "
                + $"{catalog.Variants.Length} cue variants / {keys.Count} runtime keys.");
        }

        private static List<AudioAssetPlan> ScanInventory()
        {
            var result = new List<AudioAssetPlan>(3400);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", SourceRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!seen.Add(path)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                var plan = new AudioAssetPlan
                {
                    path = path,
                    clipName = clip.name,
                    profile = ProfileFor(path),
                    selected = ShouldSelect(path),
                };
                plan.reason = plan.selected ? "runtime-mapped" : "audited-only: footstep breadth cut from mobile build";
                if (plan.selected)
                {
                    plan.cueKeys.Add(LibraryKey(path));
                    AddSemanticAliases(plan);
                }
                result.Add(plan);
            }
            return result.OrderBy(item => item.path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool ShouldSelect(string path)
        {
            if (!path.StartsWith("Assets/Footsteps Pack Expanded/", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!path.Contains("/SingleSteps/", StringComparison.OrdinalIgnoreCase)) return false;
            string normalized = path.Replace('\\', '/');
            bool usefulSurface = normalized.Contains("/EarthgroundSteps/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/ConcreteSteps/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/MetalSteps/", StringComparison.OrdinalIgnoreCase);
            if (!usefulSurface) return false;

            var match = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)$");
            return match.Success && int.TryParse(match.Groups[1].Value, out int take) && take is >= 1 and <= 8;
        }

        private static string ProfileFor(string path)
        {
            string p = path.ToLowerInvariant();
            string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (p.Contains("/bgm/") && (n.Contains("music") || n.Contains("hubmusic"))) return "music";
            if (p.Contains("/bgm/") && (n.Contains("victory") || n.Contains("defeat")
                || n is "startgame" or "wavestart" or "clearwave")) return "stinger";
            if (p.Contains("footsteps pack expanded")) return "player_movement";
            if (p.Contains("/gunshots/") || n is "gl_fire") return "weapon_fire";
            if (p.Contains("/foley/") && p.Contains("freeweaponsounds")) return "weapon_mechanical";
            if (n.StartsWith("0") && p.Contains("grenadelauncher")) return "weapon_mechanical";
            if (p.Contains("/vo/") || p.Contains("/bgm/dog") || p.Contains("/bgm/furry")
                || p.Contains("/bgm/puppet")) return "creature";
            if (p.Contains("deadly kombat") || p.Contains("/impact/") || p.Contains("/blood/")
                || p.Contains("epic toon fx")) return "impact";
            return "creature";
        }

        private static void AddSemanticAliases(AudioAssetPlan plan)
        {
            string p = plan.path.ToLowerInvariant();
            string n = Path.GetFileNameWithoutExtension(plan.path).ToLowerInvariant();

            if (p.Contains("/bgm/"))
            {
                if (n.StartsWith("hubmusic")) Add(plan, "music.hub");
                else if (n.StartsWith("gameplaymusic")) Add(plan, "music.run.stage1");
                else if (n.StartsWith("victory")) Add(plan, "stinger.victory");
                else if (n.StartsWith("defeat")) Add(plan, "stinger.defeat");
                else if (n == "startgame") Add(plan, "stinger.run.start");
                else if (n == "wavestart") Add(plan, "stinger.wave.start");
                else if (n == "clearwave") Add(plan, "stinger.wave.clear");
                else if (n.StartsWith("dogattack")) AddFamilies(plan, "attack", "canine_small", "canine_heavy");
                else if (n.StartsWith("furryattack")) AddFamilies(plan, "attack", "feline_small", "feline_fast");
                else if (n.StartsWith("puppethurt"))
                {
                    AddFamilies(plan, "hurt", "canine_small", "canine_heavy", "feline_small", "feline_fast",
                        "burrow_small", "burrow_heavy", "burrow_boss");
                    Add(plan, "sfx.player.hurt");
                }
                else if (n.StartsWith("puppetdeath"))
                {
                    AddFamilies(plan, "death", "canine_small", "canine_heavy", "feline_small", "feline_fast",
                        "burrow_small", "burrow_heavy", "burrow_boss");
                    Add(plan, "sfx.player.death");
                }
                return;
            }

            if (p.Contains("freeweaponsounds"))
            {
                string family = p.Contains("/handgun/") ? "handgun"
                    : p.Contains("/assaultrifle/") ? "rifle"
                    : p.Contains("/shotgun/") ? "shotgun"
                    : p.Contains("/grenadelauncher/") ? "grenade" : "";
                if (n.Contains("gunshot") && !n.Contains("sil_") && !n.Contains("tail")) Add(plan, $"sfx.weapon.{family}.fire");
                else if (n.Contains("sil_gunshot")) Add(plan, $"sfx.weapon.{family}.silenced.fire");
                else if (n.Contains("tail")) Add(plan, $"sfx.weapon.{family}.tail");
                else if (n == "gl_fire") Add(plan, "sfx.weapon.grenade.fire");
                else if (n == "gl_explosion")
                {
                    Add(plan, "sfx.player.bomb.explode");
                    Add(plan, "sfx.prop.barrel.explode");
                }
                else if (n.Contains("reload") || n.Contains("chamber") || n.Contains("slide_action"))
                    Add(plan, $"sfx.weapon.{family}.reload");
                else if (n.Contains("equip")) Add(plan, $"sfx.weapon.{family}.equip");
                return;
            }

            if (p.Contains("zombiehorrorpackagefree"))
            {
                if (n.Contains("_attack_") || p.Contains("/bite/"))
                {
                    AddFamilies(plan, "attack", "flesh", "burrow_small", "burrow_heavy", "burrow_boss");
                }
                if (n.Contains("_hurt_")) AddFamilies(plan, "hurt", "flesh");
                if (p.Contains("/bodyfall/")) AddFamilies(plan, "death", "flesh");
                if (p.Contains("/impact/") || p.Contains("/blood/"))
                    Add(plan, n.Contains("gory") || p.Contains("/blood/") ? "sfx.impact.flesh.heavy" : "sfx.impact.flesh.light");
                if (p.Contains("/footsteps/")) Add(plan, "sfx.creature.flesh.step");
                if (p.Contains("/foley/") || p.Contains("/eat/")) Add(plan, "sfx.horde.movement_bed");
                return;
            }

            if (p.Contains("deadly kombat"))
            {
                if (n.StartsWith("body_hit_small") || n.StartsWith("face_hit_small"))
                {
                    Add(plan, "sfx.impact.fur.light"); Add(plan, "sfx.impact.flesh.light");
                }
                else if (n.Contains("finisher") || n.Contains("large") || n.StartsWith("guts_and_gore"))
                {
                    Add(plan, "sfx.impact.fur.heavy"); Add(plan, "sfx.impact.flesh.heavy");
                }
                if (n.StartsWith("block_"))
                {
                    Add(plan, "sfx.impact.bone.light");
                    AddFamilies(plan, "hurt", "skeleton_small", "skeleton_mage", "skeleton_giant");
                }
                if (n.StartsWith("bone_breaking"))
                {
                    Add(plan, "sfx.impact.bone.heavy");
                    AddFamilies(plan, "death", "skeleton_small", "skeleton_mage", "skeleton_giant");
                }
                if (n.Contains("whoosh"))
                {
                    Add(plan, "sfx.player.bomb.throw");
                    AddFamilies(plan, "attack", "skeleton_small", "skeleton_mage", "skeleton_giant");
                }
                if (n.StartsWith("wood_bat"))
                {
                    Add(plan, "sfx.impact.plant.light"); Add(plan, "sfx.prop.crate.hit");
                    Add(plan, "sfx.prop.crate.break");
                    AddFamilies(plan, "hurt", "plant_small", "plant_heavy", "plant_boss");
                    AddFamilies(plan, "death", "plant_small", "plant_heavy", "plant_boss");
                }
                if (n.StartsWith("metal_punch"))
                {
                    Add(plan, "sfx.prop.barrel.hit");
                    Add(plan, "sfx.player.bomb.bounce");
                }
                return;
            }

            if (p.Contains("epic toon fx"))
            {
                if (n is "etfx_target_hit") { Add(plan, "sfx.pickup.coin"); Add(plan, "sfx.pickup.gem"); }
                if (n is "etfx_pop_balloon") { Add(plan, "sfx.pickup.health"); Add(plan, "sfx.pickup.bomb"); }
                if (n.Contains("explosion_grenade") || n.Contains("explosion_rocket") || n.Contains("explosion_bullet"))
                {
                    Add(plan, "sfx.player.bomb.explode"); Add(plan, "sfx.prop.barrel.explode");
                }
                if (n.Contains("shoot_acid") || n.Contains("shoot_gas") || n.Contains("shoot_fireball"))
                    AddFamilies(plan, "attack", "plant_small", "plant_heavy", "plant_boss");
                if (n.Contains("shoot_lightning")) Add(plan, "sfx.weapon.energy.fire");
                if (n.Contains("shoot_laser")) Add(plan, "sfx.weapon.laser.fire");
                if (n.Contains("shoot_rocket")) Add(plan, "sfx.weapon.grenade.fire");
                return;
            }

            if (p.Contains("footsteps pack expanded"))
            {
                if (p.Contains("/earthgroundsteps/")) Add(plan, "sfx.footstep.player.earth");
                else if (p.Contains("/concretesteps/")) Add(plan, "sfx.footstep.player.concrete");
                else if (p.Contains("/metalsteps/")) Add(plan, "sfx.footstep.player.metal");
            }
        }

        private static void AddFamilies(AudioAssetPlan plan, string action, params string[] families)
        {
            foreach (string family in families) Add(plan, $"sfx.creature.{family}.{action}");
        }

        private static void Add(AudioAssetPlan plan, string key)
        {
            if (!string.IsNullOrEmpty(key)) plan.cueKeys.Add(key);
        }

        private static void ConfigureImporters(IEnumerable<AudioAssetPlan> plans)
        {
            var unique = plans.GroupBy(item => item.path, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
            try
            {
                for (int i = 0; i < unique.Length; i++)
                {
                    var plan = unique[i];
                    if (AssetImporter.GetAtPath(plan.path) is not AudioImporter importer) continue;
                    EditorUtility.DisplayProgressBar("Zombie War Audio", $"Optimizing {i + 1}/{unique.Length}", (i + 1f) / unique.Length);
                    ApplyImporterProfile(importer, plan.profile);
                    importer.SaveAndReimport();
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private static void ApplyImporterProfile(AudioImporter importer, string profile)
        {
            bool music = profile == "music";
            bool stereo = music || profile == "stinger";
            bool latencyCritical = profile is "weapon_fire" or "impact" or "weapon_mechanical";
            importer.forceToMono = !stereo;
            importer.loadInBackground = music || profile == "creature";
            importer.ambisonic = false;

            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = latencyCritical ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.68f : profile == "creature" ? 0.64f : 0.76f;
            settings.preloadAudioData = !music;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            importer.defaultSampleSettings = settings;
        }

        private static void ConfigureAddressables(AudioAssetPlan[] plans)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true)
                ?? throw new InvalidOperationException("Addressables settings could not be created.");
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;

            var groups = GroupNames.ToDictionary(name => name, name => GetOrCreateGroup(settings, name));
            foreach (var group in groups.Values)
                foreach (var entry in group.entries.ToArray()) settings.RemoveAssetEntry(entry.guid, false);

            foreach (var plan in plans)
            {
                string guid = AssetDatabase.AssetPathToGUID(plan.path);
                var entry = settings.CreateOrMoveEntry(guid, groups[GroupFor(plan)], false, false);
                entry.address = plan.clipName;
                entry.SetLabel(LabelFor(plan), true, true, false);
                entry.SetLabel("zw-audio", true, true, false);
                entry.SetLabel($"zw-profile-{plan.profile.Replace('_', '-')}", true, true, false);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, groups.Values.ToArray(), true, true);
            EditorUtility.SetDirty(settings);
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundle = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
            bundle.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundle.IncludeInBuild = true;
            EditorUtility.SetDirty(bundle);
            var update = group.GetSchema<ContentUpdateGroupSchema>() ?? group.AddSchema<ContentUpdateGroupSchema>();
            update.StaticContent = false;
            EditorUtility.SetDirty(update);
            return group;
        }

        private static void BuildRuntimeCatalog(AudioAssetPlan[] plans)
        {
            EnsureFolder("Assets/Resources/Audio");
            var catalog = AssetDatabase.LoadAssetAtPath<AddressableAudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AddressableAudioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var variants = plans.SelectMany(plan => plan.cueKeys.Select(key =>
            {
                var tier = TierShapeFor(key);
                return new AddressableAudioCatalog.Variant
                {
                    cueKey = key,
                    address = plan.clipName,
                    label = LabelFor(plan),
                    profile = plan.profile,
                    volume = VolumeFor(plan.profile) * tier.volumeScale,
                    pitch = tier.pitch,
                    pitchVariation = PitchFor(plan.profile),
                    loop = plan.profile == "music",
                };
            })).ToArray();
            catalog.SetEditorData(CatalogId, PreloadLabels, variants);
            EditorUtility.SetDirty(catalog);

            var config = AssetDatabase.LoadAssetAtPath<BillBootstrapConfig>(BootstrapConfigPath)
                ?? throw new InvalidDataException("BillBootstrapConfig asset is missing.");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(RuntimeLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, RuntimeLibraryPath);
            }
            library.ReplaceEntries(Array.Empty<AudioLibrary.Entry>());
            config.defaultAudioLibrary = library;
            EditorUtility.SetDirty(library);
            EditorUtility.SetDirty(config);
        }

        private static void UpdateWeaponKeys()
        {
            // A5: catalog-driven. Every weapon in the catalog gets keys, including newly onboarded
            // bodies, so audio is assigned at onboarding time rather than at first fire.
            foreach (var data in ZombieWar.EditorTools.WeaponCatalogAccess.AllWeaponsForBuild())
            {
                if (data == null) continue;
                string family = data.weaponClass switch
                {
                    WeaponClass.Sidearm => "handgun",
                    WeaponClass.Shotgun => "shotgun",
                    WeaponClass.Rocket => "grenade",
                    WeaponClass.Tesla => "energy",
                    WeaponClass.Laser => "laser",
                    _ => "rifle",
                };
                data.fireSfxKey = $"sfx.weapon.{family}.fire";
                data.reloadSfxKey = family is "energy" or "laser"
                    ? "sfx.weapon.rifle.reload"
                    : $"sfx.weapon.{family}.reload";
                EditorUtility.SetDirty(data);
            }
        }

        private static void UpdateZombieKeys()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ZombieData", new[] { "Assets/_Project/Data/Zombies" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<ZombieData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                string id = data.enemyId ?? "";
                string family = id.Contains("dog_pup") ? "canine_small"
                    : id.Contains("dog_") ? "canine_heavy"
                    : id.Contains("cat_meow") ? "feline_small"
                    : id.Contains("cat_") ? "feline_fast"
                    : id.Contains("skeleton_mage") ? "skeleton_mage"
                    : id.Contains("skeleton_giant") ? "skeleton_giant"
                    : id.Contains("skeleton") ? "skeleton_small"
                    : id.Contains("cactus_boss") ? "plant_boss"
                    : id.Contains("cacti") ? "plant_small"
                    : id.Contains("cactus") ? "plant_heavy"
                    : id.Contains("mole_rat_king") ? "burrow_boss"
                    : id.Contains("mole_rat") ? "burrow_heavy"
                    : id.Contains("burrow") ? "burrow_small"
                    : "flesh";
                data.attackSfxKey = $"sfx.creature.{family}.attack";
                data.hurtSfxKey = $"sfx.creature.{family}.hurt";
                data.deathSfxKey = $"sfx.creature.{family}.death";
                data.impactSfxKey = family.StartsWith("skeleton", StringComparison.Ordinal) ? "sfx.impact.bone.light"
                    : family.StartsWith("plant", StringComparison.Ordinal) ? "sfx.impact.plant.light"
                    : family == "flesh" ? "sfx.impact.flesh.light" : "sfx.impact.fur.light";
                EditorUtility.SetDirty(data);
            }
        }

        private static void UpdateBombKey()
        {
            const string path = "Assets/_Project/Prefabs/Gameplay/Bomb.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var bomb = root.GetComponentInChildren<Bomb>(true)
                    ?? throw new InvalidDataException("Bomb component is missing from Bomb.prefab.");
                var serialized = new SerializedObject(bomb);
                serialized.FindProperty("explosionSfxKey").stringValue = "sfx.player.bomb.explode";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static string GroupFor(AudioAssetPlan plan) => plan.profile switch
        {
            "music" or "stinger" => "ZW_Audio_Core",
            "weapon_fire" or "weapon_mechanical" => "ZW_Audio_Weapons",
            "creature" => "ZW_Audio_Zombies",
            "player_movement" => "ZW_Audio_Footsteps",
            _ => "ZW_Audio_World",
        };

        private static string LabelFor(AudioAssetPlan plan) => GroupFor(plan) switch
        {
            "ZW_Audio_Core" => "zw-audio-core",
            "ZW_Audio_Weapons" => "zw-audio-weapons",
            "ZW_Audio_Zombies" => "zw-audio-zombies",
            "ZW_Audio_Footsteps" => "zw-audio-footsteps",
            _ => "zw-audio-world",
        };

        private static float VolumeFor(string profile) => profile switch
        {
            "music" => 0.34f,
            "stinger" => 0.82f,
            "creature" => 0.72f,
            "player_movement" => 0.55f,
            "impact" => 0.78f,
            _ => 0.88f,
        };

        /// <summary>
        /// Size shaping for creature cues.
        ///
        /// The curated set has ONE clip pool per creature kind - one "dog attack" pool, not one per
        /// dog - so every family in a size ladder resolves to a byte-identical clip list. Before this,
        /// a pup, a bark hound, a skeleton and a skeleton giant were literally indistinguishable at
        /// playback. Pitch is the separator that costs no new audio: down-pitched reads bigger and
        /// slower, up-pitched reads smaller and faster, and the small volume trim keeps the heavy
        /// tiers dominant when forty enemies are speaking at once.
        ///
        /// Order matters - the checks run most specific first, because "_small" and "_heavy" would
        /// otherwise swallow the boss and giant tiers.
        /// </summary>
        private static (float pitch, float volumeScale) TierShapeFor(string cueKey)
        {
            if (!cueKey.Contains(".creature.", StringComparison.Ordinal)) return (1f, 1f);

            if (cueKey.Contains("_boss", StringComparison.Ordinal)) return (0.74f, 1.18f);
            if (cueKey.Contains("_giant", StringComparison.Ordinal)) return (0.80f, 1.14f);
            if (cueKey.Contains("_heavy", StringComparison.Ordinal)) return (0.86f, 1.08f);
            // The mage sits just under neutral: distinct from the plain skeleton beside it without
            // reading as a heavyweight, which its silhouette does not support.
            if (cueKey.Contains("_mage", StringComparison.Ordinal)) return (0.97f, 1.00f);
            if (cueKey.Contains("_fast", StringComparison.Ordinal)) return (1.10f, 0.94f);
            if (cueKey.Contains("_small", StringComparison.Ordinal)) return (1.14f, 0.92f);

            return (1f, 1f);
        }

        private static float PitchFor(string profile) => profile switch
        {
            "creature" => 0.055f,
            "player_movement" => 0.045f,
            "impact" => 0.035f,
            "weapon_fire" => 0.015f,
            "weapon_mechanical" => 0.025f,
            _ => 0f,
        };

        private static string LibraryKey(string path)
        {
            string relative = Path.ChangeExtension(path["Assets/".Length..], null) ?? path;
            var chars = relative.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '.').ToArray();
            return "library." + Regex.Replace(new string(chars), @"\.+", ".").Trim('.');
        }

        private static void WriteReports(IReadOnlyList<AudioAssetPlan> inventory, IReadOnlyList<AudioAssetPlan> selected)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(InventoryReport) ?? "Docs");
            using (var writer = new StreamWriter(InventoryReport, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("asset_path,clip_name,profile,status,reason,runtime_keys");
                foreach (var item in inventory)
                    writer.WriteLine(string.Join(",", Csv(item.path), Csv(item.clipName), Csv(item.profile),
                        Csv(item.selected ? "ADDRESSABLE" : "SOURCE_ONLY"), Csv(item.reason),
                        Csv(string.Join("|", item.cueKeys.OrderBy(key => key, StringComparer.Ordinal)))));
            }

            using (var writer = new StreamWriter(MappingReport, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("runtime_key,asset_path,addressable_label,profile,use_case");
                foreach (var item in selected)
                foreach (string key in item.cueKeys.OrderBy(key => key, StringComparer.Ordinal))
                    writer.WriteLine(string.Join(",", Csv(key), Csv(item.path), Csv(LabelFor(item)),
                        Csv(item.profile), Csv(UseCaseFor(key))));
            }
        }

        private static string UseCaseFor(string key)
        {
            if (key.StartsWith("library.", StringComparison.Ordinal)) return "catalogued source; direct runtime use not required";
            if (key.StartsWith("music.", StringComparison.Ordinal)) return "state-driven BGM";
            if (key.StartsWith("stinger.", StringComparison.Ordinal)) return "run or wave gameplay cue";
            if (key.Contains(".weapon.", StringComparison.Ordinal)) return "weapon fire reload or tail";
            if (key.Contains(".creature.", StringComparison.Ordinal)) return "3D creature attack hurt death or movement";
            if (key.Contains(".impact.", StringComparison.Ordinal)) return "3D material impact";
            if (key.Contains(".pickup.", StringComparison.Ordinal)) return "pickup feedback";
            if (key.Contains(".prop.", StringComparison.Ordinal)) return "destructible world prop";
            if (key.Contains(".footstep.", StringComparison.Ordinal)) return "player movement";
            return "gameplay SFX";
        }

        private static string Csv(string value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private sealed class AudioAssetPlan
        {
            public string path;
            public string clipName;
            public string profile;
            public bool selected;
            public string reason;
            public readonly HashSet<string> cueKeys = new(StringComparer.Ordinal);
        }
    }
}
