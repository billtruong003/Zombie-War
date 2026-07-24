using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BillGameCore;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using ZombieWar.Audio;

namespace ZombieWar.Editor.Audio
{
    public static class ZombieWarAddressableAudioCatalogBuilder
    {
        private const string CatalogId = "ZW_SFX_FULL_V1";
        private const int ExpectedVariantCount = 970;
        private const int ExpectedCueCount = 330;
        private const string ManifestPath =
            "Assets/_Project/Audio/Manifests/zombiewar_sfx_manifest.v1.json";
        private const string CatalogPath =
            "Assets/Resources/Audio/AddressableAudioCatalog.asset";
        private const string RuntimeLibraryPath =
            "Assets/Resources/Audio/ZombieWarRuntimeAudioLibrary.asset";

        private static readonly string[] PreloadLabels =
        {
            "zw-audio-core",
            "zw-audio-weapons",
            "zw-audio-world",
            "zw-audio-zombies",
            "zw-audio-ambience",
        };

        [MenuItem("Zombie War/Audio/Build Runtime Addressables Catalog")]
        public static void Build()
        {
            var manifest = ReadManifest();
            EnsureFolder("Assets/Resources/Audio");
            ConfigurePlayerBuild();

            var catalog = AssetDatabase.LoadAssetAtPath<AddressableAudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AddressableAudioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var variants = manifest.entries.Select(ToVariant).ToArray();
            catalog.SetEditorData(CatalogId, PreloadLabels, variants);
            EditorUtility.SetDirty(catalog);

            var config = AssetDatabase.LoadAssetAtPath<BillBootstrapConfig>(
                "Assets/Resources/BillBootstrapConfig.asset");
            if (config == null)
                throw new InvalidOperationException("BillBootstrapConfig asset is missing.");

            var runtimeLibrary = AssetDatabase.LoadAssetAtPath<AudioLibrary>(RuntimeLibraryPath);
            if (runtimeLibrary == null)
            {
                AssetDatabase.DeleteAsset(RuntimeLibraryPath);
                runtimeLibrary = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(runtimeLibrary, RuntimeLibraryPath);
            }
            runtimeLibrary.ReplaceEntries(Array.Empty<AudioLibrary.Entry>());
            EditorUtility.SetDirty(runtimeLibrary);

            config.defaultAudioLibrary = runtimeLibrary;
            EditorUtility.SetDirty(config);

            var cueKeys = variants.Select(variant => variant.cueKey)
                .ToHashSet(StringComparer.Ordinal);
            UpdateWeaponKeys(cueKeys);
            UpdateBombKey(cueKeys);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log(
                $"[ZombieWar Audio] Runtime Addressables catalog built: "
                + $"{ExpectedCueCount} cues / {ExpectedVariantCount} variants.");
        }

        [MenuItem("Zombie War/Audio/Validate Runtime Addressables Catalog")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AddressableAudioCatalog>(CatalogPath)
                ?? throw new InvalidDataException("Runtime audio catalog is missing.");
            var config = AssetDatabase.LoadAssetAtPath<BillBootstrapConfig>(
                "Assets/Resources/BillBootstrapConfig.asset")
                ?? throw new InvalidDataException("BillBootstrapConfig is missing.");

            if (catalog.CatalogId != CatalogId
                || catalog.Variants.Length != ExpectedVariantCount
                || catalog.Variants.Select(variant => variant.cueKey).Distinct().Count() != ExpectedCueCount)
            {
                throw new InvalidDataException("Runtime catalog count or ID mismatch.");
            }
            if (catalog.PreloadLabels.Length != PreloadLabels.Length)
                throw new InvalidDataException("Runtime preload label count mismatch.");
            var runtimeLibrary = AssetDatabase.LoadAssetAtPath<AudioLibrary>(RuntimeLibraryPath);
            if (runtimeLibrary == null || config.defaultAudioLibrary != runtimeLibrary)
                throw new InvalidDataException("BillBootstrapConfig runtime AudioLibrary reference is invalid.");
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null
                || settings.BuildAddressablesWithPlayerBuild
                    != AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer)
            {
                throw new InvalidDataException("Addressables is not configured to build with the Player.");
            }

            var cueKeys = catalog.Variants.Select(variant => variant.cueKey)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Project/Data/Weapons" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var serialized = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
                var fire = serialized.FindProperty("fireSfxKey")?.stringValue;
                var reload = serialized.FindProperty("reloadSfxKey")?.stringValue;
                if (!cueKeys.Contains(fire) || !cueKeys.Contains(reload))
                    throw new InvalidDataException($"Weapon audio keys are invalid: {path}");
            }

            Debug.Log(
                $"[ZombieWar Audio] Runtime validation PASS: "
                + $"{ExpectedCueCount} cues / {ExpectedVariantCount} variants.");
        }

        private static AddressableAudioCatalog.Variant ToVariant(ManifestEntry entry)
        {
            var profile = entry.profile;
            return new AddressableAudioCatalog.Variant
            {
                cueKey = entry.cue_key,
                address = Path.GetFileNameWithoutExtension(entry.runtime_name),
                label = LabelFor(entry.runtime_path),
                profile = profile,
                volume = VolumeFor(profile),
                pitch = 1f,
                pitchVariation = PitchVariationFor(profile),
                loop = profile == "ambience_loop",
            };
        }

        private static void UpdateWeaponKeys(HashSet<string> cueKeys)
        {
            var fireTokens = cueKeys
                .Where(key => key.StartsWith("sfx.weapon.", StringComparison.Ordinal)
                    && key.EndsWith(".fire", StringComparison.Ordinal))
                .Select(key => key["sfx.weapon.".Length..^".fire".Length])
                .ToArray();

            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Project/Data/Weapons" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                var serialized = new SerializedObject(asset);
                var token = ResolveWeaponToken(Path.GetFileNameWithoutExtension(path), fireTokens);
                var fireKey = $"sfx.weapon.{token}.fire";
                var reloadKey = $"sfx.weapon.{token}.reload";
                if (!cueKeys.Contains(fireKey) || !cueKeys.Contains(reloadKey))
                    throw new InvalidDataException($"No approved audio cue pair for {path}.");

                serialized.FindProperty("fireSfxKey").stringValue = fireKey;
                serialized.FindProperty("reloadSfxKey").stringValue = reloadKey;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        private static string ResolveWeaponToken(string assetName, IEnumerable<string> tokens)
        {
            var explicitToken = assetName switch
            {
                "WD_AssaultRifle_Generic" => "ar_generic",
                "WD_Marksman_SniperGeneric" => "sniper_generic",
                "WD_Shotgun_Generic" => "shotgun_generic",
                "WD_LMG_Generic" => "lmg_generic",
                "WD_SMG_Generic" => "smg_generic",
                _ => null,
            };
            if (explicitToken != null)
                return explicitToken;

            var comparableName = Alphanumeric(assetName);
            var match = tokens
                .OrderByDescending(token => token.Length)
                .FirstOrDefault(token => comparableName.Contains(Alphanumeric(token)));
            return match ?? throw new InvalidDataException(
                $"Cannot map weapon asset '{assetName}' to an approved audio cue.");
        }

        private static void UpdateBombKey(HashSet<string> cueKeys)
        {
            const string key = "sfx.player.bomb.explode";
            if (!cueKeys.Contains(key))
                throw new InvalidDataException($"Approved cue is missing: {key}");

            const string prefabPath = "Assets/_Project/Prefabs/Gameplay/Bomb.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var updated = false;
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                        continue;
                    var serialized = new SerializedObject(behaviour);
                    var property = serialized.FindProperty("explosionSfxKey");
                    if (property == null)
                        continue;
                    property.stringValue = key;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    updated = true;
                }
                if (!updated)
                    throw new InvalidDataException("Bomb explosionSfxKey property was not found.");
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Manifest ReadManifest()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.catalog_id != CatalogId
                || manifest.entries == null || manifest.entries.Length != ExpectedVariantCount)
            {
                throw new InvalidDataException("Approved SFX manifest is missing or invalid.");
            }
            return manifest;
        }

        private static void ConfigurePlayerBuild()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false)
                ?? throw new InvalidDataException("Addressables settings are missing.");
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(settings);
        }

        private static string LabelFor(string runtimePath)
        {
            var normalized = runtimePath.Replace('\\', '/');
            if (normalized.StartsWith("Ambience/", StringComparison.OrdinalIgnoreCase))
                return "zw-audio-ambience";
            if (normalized.StartsWith("SFX/Weapons/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("SFX/Reload/", StringComparison.OrdinalIgnoreCase))
                return "zw-audio-weapons";
            if (normalized.StartsWith("SFX/Zombies/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("SFX/HordeBeds/", StringComparison.OrdinalIgnoreCase))
                return "zw-audio-zombies";
            if (normalized.StartsWith("SFX/Impacts/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("SFX/Interactives/", StringComparison.OrdinalIgnoreCase))
                return "zw-audio-world";
            return "zw-audio-core";
        }

        private static float VolumeFor(string profile) => profile switch
        {
            "ambience_loop" => 0.65f,
            "ambience_detail" => 0.7f,
            "ui" => 0.85f,
            "stinger" => 0.9f,
            "creature" => 0.9f,
            _ => 1f,
        };

        private static float PitchVariationFor(string profile) => profile switch
        {
            "creature" => 0.06f,
            "player_movement" => 0.05f,
            "impact" => 0.035f,
            "weapon_mechanical" => 0.025f,
            "weapon_fire" => 0.015f,
            "ui" => 0.01f,
            _ => 0f,
        };

        private static string Alphanumeric(string value) =>
            new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        [Serializable]
        private sealed class Manifest
        {
            public string catalog_id;
            public ManifestEntry[] entries;
        }

        [Serializable]
        private sealed class ManifestEntry
        {
            public string cue_key;
            public string runtime_name;
            public string runtime_path;
            public string profile;
        }
    }
}
