using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using ZombieWar.Audio;
using ZombieWar.World;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// G12.10 (owner 04/10: before launch): the map themes and the radio voice-over are Addressables,
    /// one bundle per asset, loaded when needed. In Resources they were all part of the WebGL first
    /// download (every map's chunks, 82 MB of voice sources). Idempotent: moves the assets out of
    /// Resources the first time, then (re)creates the groups and entries.
    ///  - maps: group ZW_Maps, address "map/&lt;id&gt;", label "maptheme" (MapTheme.Preload).
    ///  - voice: group ZW_Voice, address "vo/&lt;line id&gt;" (RadioVoice). The subtitles JSON stays in
    ///    Resources/VO: it is small and read at boot.
    /// </summary>
    public static class ContentAddressables
    {
        const string OldMaps = "Assets/Resources/MapThemes";
        const string OldVoice = "Assets/_Project/Audio/VO/Resources/VO";
        public const string VoiceFolder = "Assets/_Project/Audio/VO/Clips";
        public const string MapsGroup = "ZW_Maps", VoiceGroup = "ZW_Voice";

        [MenuItem("ZombieWar/Build/Content Addressables (maps + voice)")]
        public static void Setup()
        {
            MoveOut(OldMaps, MapTheme.AssetFolder.TrimEnd('/'), p => p.EndsWith(".asset"));
            MoveOut(OldVoice, VoiceFolder, p => !p.EndsWith(".json"));

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var maps = Group(settings, MapsGroup);
            var voice = Group(settings, VoiceGroup);
            int m = 0, v = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:MapTheme", new[] { MapTheme.AssetFolder.TrimEnd('/') }))
            {
                var theme = AssetDatabase.LoadAssetAtPath<MapTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (theme != null && Register(settings, maps, theme)) m++;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { VoiceFolder }))
            {
                var e = settings.CreateOrMoveEntry(guid, voice, false, false);
                e.address = RadioVoice.AddressPrefix + Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                v++;
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ContentAddressables] {m} maps in {MapsGroup}, {v} voice clips in {VoiceGroup}.");
        }

        /// The map baker calls this for every theme it writes.
        public static void RegisterMapTheme(MapTheme theme)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (Register(settings, Group(settings, MapsGroup), theme))
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        }

        static bool Register(AddressableAssetSettings settings, AddressableAssetGroup group, MapTheme theme)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(theme));
            if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(theme.id)) return false;
            var e = settings.CreateOrMoveEntry(guid, group, false, false);
            e.address = MapTheme.AddressPrefix + theme.id;
            e.SetLabel(MapTheme.Label, true, true, false);
            return true;
        }

        static AddressableAssetGroup Group(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundle = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
            bundle.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;   // one map / one line per bundle
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundle.IncludeInBuild = true;
            EditorUtility.SetDirty(bundle);
            var update = group.GetSchema<ContentUpdateGroupSchema>() ?? group.AddSchema<ContentUpdateGroupSchema>();
            update.StaticContent = false;
            EditorUtility.SetDirty(update);
            return group;
        }

        static void MoveOut(string from, string to, Func<string, bool> take)
        {
            if (!AssetDatabase.IsValidFolder(from)) return;
            EnsureFolder(to);
            foreach (var file in Directory.GetFiles(from))
            {
                string path = file.Replace('\\', '/');
                if (path.EndsWith(".meta") || !take(path)) continue;
                string error = AssetDatabase.MoveAsset(path, to + "/" + Path.GetFileName(path));
                if (!string.IsNullOrEmpty(error)) Debug.LogError($"[ContentAddressables] {path}: {error}");
            }
            if (Array.TrueForAll(Directory.GetFiles(from, "*", SearchOption.AllDirectories), f => f.EndsWith(".meta")))
                AssetDatabase.DeleteAsset(from);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
