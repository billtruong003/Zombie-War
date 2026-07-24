using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ZombieWar.Editor.Audio
{
    public static class ZombieWarAudioContentImporter
    {
        private const string CatalogId = "ZW_SFX_FULL_V1";
        private const int ExpectedAssetCount = 970;
        private const string SourceRoot = @"D:\Project\AI-SFX-Studio\exports\ZombieWar\v1";
        private const string TargetRoot = "Assets/_Project/Audio/Generated";
        private const string ManifestTarget = "Assets/_Project/Audio/Manifests/zombiewar_sfx_manifest.v1.json";
        private const string ReportTarget = "Assets/_Project/Audio/Manifests/zombiewar_audio_import_report.v1.json";

        private static readonly string[] GroupNames =
        {
            "ZW_Audio_Core",
            "ZW_Audio_Weapons",
            "ZW_Audio_World",
            "ZW_Audio_Zombies",
            "ZW_Audio_Ambience",
        };

        [MenuItem("Zombie War/Audio/Import Approved SFX Library")]
        public static void ImportApprovedLibrary()
        {
            var sourceManifest = Path.Combine(SourceRoot, "_Manifests", "zombiewar_sfx_manifest.v1.json");
            var manifest = ReadAndValidateManifest(sourceManifest);
            var importedPaths = CopyApprovedFiles(manifest, sourceManifest);
            ConfigureAudioImporters(manifest, importedPaths);
            var groupCounts = ConfigureAddressables(manifest, importedPaths);
            ValidateImportedLibrary(manifest, importedPaths, groupCounts);
            WriteReport(groupCounts);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[ZombieWar Audio] Imported and addressed {importedPaths.Count}/{ExpectedAssetCount} approved clips.");
        }

        [MenuItem("Zombie War/Audio/Validate Approved SFX Library")]
        public static void ValidateApprovedLibrary()
        {
            var sourceManifest = Path.Combine(SourceRoot, "_Manifests", "zombiewar_sfx_manifest.v1.json");
            var manifest = ReadAndValidateManifest(sourceManifest);
            var paths = manifest.entries.ToDictionary(
                entry => entry.runtime_path,
                entry => $"{TargetRoot}/{entry.runtime_path}".Replace('\\', '/'));
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var counts = settings == null
                ? new Dictionary<string, int>()
                : GroupNames.ToDictionary(
                    name => name,
                    name => settings.FindGroup(name)?.entries.Count ?? 0);
            ValidateImportedLibrary(manifest, paths, counts);
            Debug.Log($"[ZombieWar Audio] Validation PASS: {ExpectedAssetCount} clips.");
        }

        private static AudioManifest ReadAndValidateManifest(string sourceManifest)
        {
            if (!File.Exists(sourceManifest))
                throw new FileNotFoundException("Approved audio manifest is missing.", sourceManifest);

            var manifest = JsonUtility.FromJson<AudioManifest>(File.ReadAllText(sourceManifest));
            if (manifest == null || manifest.catalog_id != CatalogId)
                throw new InvalidDataException($"Expected catalog {CatalogId}.");
            if (manifest.entries == null || manifest.entries.Length != ExpectedAssetCount)
                throw new InvalidDataException(
                    $"Expected {ExpectedAssetCount} manifest entries, found {manifest.entries?.Length ?? 0}.");
            if (manifest.entries.Select(entry => entry.runtime_path).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != ExpectedAssetCount)
                throw new InvalidDataException("Manifest contains duplicate runtime paths.");
            return manifest;
        }

        private static Dictionary<string, string> CopyApprovedFiles(
            AudioManifest manifest,
            string sourceManifest)
        {
            var targetAbsolute = ToAbsoluteProjectPath(TargetRoot);
            Directory.CreateDirectory(targetAbsolute);

            var approvedDestinations = manifest.entries
                .Select(entry => Path.GetFullPath(Path.Combine(targetAbsolute, entry.runtime_path)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in Directory.EnumerateFiles(targetAbsolute, "*.wav", SearchOption.AllDirectories))
            {
                if (approvedDestinations.Contains(Path.GetFullPath(existing)))
                    continue;
                File.Delete(existing);
                var meta = existing + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);
            }

            var importedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.entries)
            {
                var source = Path.GetFullPath(Path.Combine(SourceRoot, entry.runtime_path));
                EnsureWithinRoot(source, Path.GetFullPath(SourceRoot));
                if (!File.Exists(source))
                    throw new FileNotFoundException($"Approved clip is missing: {entry.runtime_path}", source);

                var assetPath = $"{TargetRoot}/{entry.runtime_path}".Replace('\\', '/');
                var destination = ToAbsoluteProjectPath(assetPath);
                EnsureWithinRoot(destination, targetAbsolute);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)
                    ?? throw new InvalidDataException($"Invalid destination: {destination}"));
                File.Copy(source, destination, true);
                importedPaths.Add(entry.runtime_path, assetPath);
            }

            var manifestAbsolute = ToAbsoluteProjectPath(ManifestTarget);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestAbsolute)
                ?? throw new InvalidDataException($"Invalid manifest destination: {manifestAbsolute}"));
            File.Copy(sourceManifest, manifestAbsolute, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return importedPaths;
        }

        private static void ConfigureAudioImporters(
            AudioManifest manifest,
            IReadOnlyDictionary<string, string> importedPaths)
        {
            try
            {
                for (var index = 0; index < manifest.entries.Length; index++)
                {
                    var entry = manifest.entries[index];
                    var assetPath = importedPaths[entry.runtime_path];
                    if (AssetImporter.GetAtPath(assetPath) is not AudioImporter importer)
                        throw new InvalidDataException($"AudioImporter unavailable: {assetPath}");

                    EditorUtility.DisplayProgressBar(
                        "Zombie War Audio",
                        $"Applying {entry.profile} importer profile ({index + 1}/{manifest.entries.Length})",
                        (index + 1f) / manifest.entries.Length);
                    ApplyImporterProfile(importer, entry.profile);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ApplyImporterProfile(AudioImporter importer, string profile)
        {
            var isWide = profile is "ui" or "stinger" or "ambience_detail" or "ambience_loop";
            var isAmbience = profile is "ambience_detail" or "ambience_loop";
            var isCreature = profile == "creature";

            importer.forceToMono = !isWide;
            importer.loadInBackground = isAmbience || isCreature;
            importer.ambisonic = false;

            var settings = importer.defaultSampleSettings;
            settings.loadType = isAmbience
                ? AudioClipLoadType.Streaming
                : isCreature
                    ? AudioClipLoadType.CompressedInMemory
                    : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = isAmbience || isCreature
                ? AudioCompressionFormat.Vorbis
                : AudioCompressionFormat.ADPCM;
            settings.quality = isAmbience ? 0.72f : isCreature ? 0.64f : 1f;
            settings.preloadAudioData = !isAmbience && !isCreature;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }

        private static Dictionary<string, int> ConfigureAddressables(
            AudioManifest manifest,
            IReadOnlyDictionary<string, string> importedPaths)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true)
                ?? throw new InvalidOperationException("Addressables settings could not be created.");
            var groups = GroupNames.ToDictionary(
                name => name,
                name => GetOrCreateGroup(settings, name));

            foreach (var group in groups.Values)
            {
                foreach (var existing in group.entries.ToArray())
                    settings.RemoveAssetEntry(existing.guid, false);
            }

            foreach (var entry in manifest.entries)
            {
                var assetPath = importedPaths[entry.runtime_path];
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                    throw new InvalidDataException($"Asset has no GUID: {assetPath}");

                var groupName = GroupFor(entry.runtime_path);
                var addressableEntry = settings.CreateOrMoveEntry(guid, groups[groupName], false, false);
                addressableEntry.address = Path.GetFileNameWithoutExtension(entry.runtime_name);
                addressableEntry.SetLabel("zw-audio", true, true, false);
                addressableEntry.SetLabel($"zw-audio-{groupName["ZW_Audio_".Length..].ToLowerInvariant()}", true, true, false);
                addressableEntry.SetLabel($"zw-profile-{entry.profile.Replace('_', '-')}", true, true, false);
            }

            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.BatchModification,
                groups.Values.ToArray(),
                true,
                true);
            EditorUtility.SetDirty(settings);
            return groups.ToDictionary(pair => pair.Key, pair => pair.Value.entries.Count);
        }

        private static AddressableAssetGroup GetOrCreateGroup(
            AddressableAssetSettings settings,
            string groupName)
        {
            var group = settings.FindGroup(groupName)
                ?? settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    false,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));

            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>()
                ?? group.AddSchema<BundledAssetGroupSchema>();
            bundleSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
            bundleSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundleSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundleSchema.UseAssetBundleCache = true;
            bundleSchema.UseAssetBundleCrc = true;
            bundleSchema.UseAssetBundleCrcForCachedBundles = false;
            bundleSchema.IncludeInBuild = true;
            EditorUtility.SetDirty(bundleSchema);

            var updateSchema = group.GetSchema<ContentUpdateGroupSchema>()
                ?? group.AddSchema<ContentUpdateGroupSchema>();
            updateSchema.StaticContent = false;
            EditorUtility.SetDirty(updateSchema);
            return group;
        }

        private static string GroupFor(string runtimePath)
        {
            var normalized = runtimePath.Replace('\\', '/');
            if (normalized.StartsWith("Ambience/", StringComparison.OrdinalIgnoreCase))
                return "ZW_Audio_Ambience";
            if (normalized.StartsWith("SFX/Weapons/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("SFX/Reload/", StringComparison.OrdinalIgnoreCase))
                return "ZW_Audio_Weapons";
            if (normalized.StartsWith("SFX/Zombies/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("SFX/HordeBeds/", StringComparison.OrdinalIgnoreCase))
                return "ZW_Audio_Zombies";
            if (normalized.StartsWith("SFX/Impacts/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("SFX/Interactives/", StringComparison.OrdinalIgnoreCase))
                return "ZW_Audio_World";
            return "ZW_Audio_Core";
        }

        private static void ValidateImportedLibrary(
            AudioManifest manifest,
            IReadOnlyDictionary<string, string> importedPaths,
            IReadOnlyDictionary<string, int> groupCounts)
        {
            var wavCount = Directory.EnumerateFiles(
                ToAbsoluteProjectPath(TargetRoot), "*.wav", SearchOption.AllDirectories).Count();
            if (wavCount != ExpectedAssetCount)
                throw new InvalidDataException($"Imported WAV count is {wavCount}, expected {ExpectedAssetCount}.");

            foreach (var entry in manifest.entries)
            {
                var assetPath = importedPaths[entry.runtime_path];
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null)
                    throw new InvalidDataException($"AudioClip failed to import: {assetPath}");
                if (clip.frequency != 44_100)
                    throw new InvalidDataException($"Unexpected sample rate {clip.frequency}: {assetPath}");
                if (AssetImporter.GetAtPath(assetPath) is not AudioImporter importer)
                    throw new InvalidDataException($"AudioImporter unavailable during validation: {assetPath}");
                ValidateImporterProfile(importer, entry.profile, assetPath);
            }

            if (groupCounts.Count != GroupNames.Length)
                throw new InvalidDataException("One or more Addressables audio groups are missing.");
            if (groupCounts.Values.Sum() != ExpectedAssetCount)
                throw new InvalidDataException(
                    $"Addressables entry count is {groupCounts.Values.Sum()}, expected {ExpectedAssetCount}.");
        }

        private static void ValidateImporterProfile(
            AudioImporter importer,
            string profile,
            string assetPath)
        {
            var isWide = profile is "ui" or "stinger" or "ambience_detail" or "ambience_loop";
            var isAmbience = profile is "ambience_detail" or "ambience_loop";
            var isCreature = profile == "creature";
            var expectedLoadType = isAmbience
                ? AudioClipLoadType.Streaming
                : isCreature
                    ? AudioClipLoadType.CompressedInMemory
                    : AudioClipLoadType.DecompressOnLoad;
            var expectedFormat = isAmbience || isCreature
                ? AudioCompressionFormat.Vorbis
                : AudioCompressionFormat.ADPCM;
            var sampleSettings = importer.defaultSampleSettings;
            if (importer.forceToMono == isWide
                || importer.loadInBackground != (isAmbience || isCreature)
                || sampleSettings.loadType != expectedLoadType
                || sampleSettings.compressionFormat != expectedFormat
                || sampleSettings.preloadAudioData != (!isAmbience && !isCreature))
            {
                throw new InvalidDataException($"Importer profile mismatch ({profile}): {assetPath}");
            }
        }

        private static void WriteReport(IReadOnlyDictionary<string, int> groupCounts)
        {
            var report = new ImportReport
            {
                catalog_id = CatalogId,
                imported_assets = ExpectedAssetCount,
                addressable_assets = groupCounts.Values.Sum(),
                addressables_package = "2.7.6",
                groups = groupCounts
                    .Select(pair => new GroupCount { name = pair.Key, count = pair.Value })
                    .ToArray(),
            };
            var absolute = ToAbsoluteProjectPath(ReportTarget);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)
                ?? throw new InvalidDataException($"Invalid report destination: {absolute}"));
            File.WriteAllText(absolute, JsonUtility.ToJson(report, true));
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static void EnsureWithinRoot(string path, string root)
        {
            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var normalizedPath = Path.GetFullPath(path);
            if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Path escapes approved root: {path}");
        }

        [Serializable]
        private sealed class AudioManifest
        {
            public string catalog_id;
            public AudioEntry[] entries;
        }

        [Serializable]
        private sealed class AudioEntry
        {
            public string runtime_path;
            public string runtime_name;
            public string profile;
        }

        [Serializable]
        private sealed class ImportReport
        {
            public string catalog_id;
            public int imported_assets;
            public int addressable_assets;
            public string addressables_package;
            public GroupCount[] groups;
        }

        [Serializable]
        private sealed class GroupCount
        {
            public string name;
            public int count;
        }
    }
}
