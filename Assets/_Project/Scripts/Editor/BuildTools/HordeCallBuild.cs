using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZombieWar.Editor.Build
{
    /// <summary>
    /// M9: two build flavours and a guard between them.
    /// - Development: cheats, BuildTour and the dev console come from DEVELOPMENT_BUILD, so no define
    ///   is needed and nothing can leak into a store build by accident.
    /// - Release: refuses to build while ZW_CHEATS is defined or the bundle id is still Unity's default;
    ///   for Android also without an upload keystore, an app icon, the HordeCall name, or a pinned
    ///   target API (2026-10-01, the first-upload checklist).
    /// </summary>
    public static class HordeCallBuild
    {
        public const string CheatDefine = "ZW_CHEATS";
        // Compared ignoring case: the template id is "com.UnityTechnologies.…", which a case-sensitive
        // "com.unity" let through.
        static readonly string[] DefaultIdPrefixes = { "com.DefaultCompany", "com.Company", "com.unity" };
        public const string ReleaseProductName = "HordeCall";

        /// <summary>Problems that must block a store build. Empty for a development build.</summary>
        public static List<string> ReleaseProblems(NamedBuildTarget target, bool development)
        {
            var problems = new List<string>();
            if (development) return problems;
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            if (defines.Contains(CheatDefine))
                problems.Add($"{CheatDefine} is defined for {target.TargetName}: the cheat panel would ship.");
            string id = PlayerSettings.GetApplicationIdentifier(target);
            if (string.IsNullOrEmpty(id) || DefaultIdPrefixes.Any(p => id.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)))
                problems.Add($"Bundle id for {target.TargetName} is '{id}'. Set the real one before a store build.");
            if (target == NamedBuildTarget.Android)
            {
                if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
                    problems.Add("No upload keystore: the bundle would be signed with the debug key, which Google Play refuses.");
                var icons = PlayerSettings.GetIcons(target, IconKind.Any);
                if (icons == null || icons.Length == 0 || icons.All(i => i == null))
                    problems.Add("No app icon is set in Player Settings.");
                if (PlayerSettings.productName != ReleaseProductName)
                    problems.Add($"Product name is '{PlayerSettings.productName}', not '{ReleaseProductName}': the launcher would show it.");
                if (PlayerSettings.Android.targetSdkVersion == AndroidSdkVersions.AndroidApiLevelAuto)
                    problems.Add("Target API is 'highest installed': pin the level Google Play currently requires.");
            }
            return problems;
        }

        static string[] Scenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        [MenuItem("HordeCall/Build/Android Development (cheats)")]
        public static void AndroidDevelopment() => Build(BuildOptions.Development | BuildOptions.AllowDebugging, "Builds/Android/HordeCall-dev.apk", false);

        [MenuItem("HordeCall/Build/Android Release (store)")]
        public static void AndroidRelease() => Build(BuildOptions.None, "Builds/Android/HordeCall.aab", true);

        /// <summary>A development bundle laid out like the store one, to measure the base module and the
        /// asset packs before a real upload (it cannot be uploaded: development, debug-signed).</summary>
        [MenuItem("HordeCall/Build/Android Size Check (dev .aab)")]
        public static void AndroidSizeCheck() => Build(BuildOptions.Development, "Builds/Android/HordeCall-sizecheck.aab", true);

        static void Build(BuildOptions options, string path, bool appBundle)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUtility.DisplayDialog("HordeCall build", "The active platform is not Android. Switching reimports assets and can take a long time. Continue?", "Switch and build", "Cancel"))
                return;
            EditorUserBuildSettings.buildAppBundle = appBundle;
            // A store bundle keeps code and libraries in the base module and moves the game data into
            // an install-time asset pack (06/10): Google Play caps the base module's download size.
            PlayerSettings.Android.splitApplicationBinary = appBundle;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes(), locationPathName = path, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = options,
            });
            Debug.Log($"[HordeCallBuild] {report.summary.result} → {path} ({report.summary.totalSize / 1048576f:0.0} MB)");
        }

        /// <summary>One-time clean-up: cheats come from development builds now, not from a define.</summary>
        [MenuItem("HordeCall/Build/Remove ZW_CHEATS define")]
        public static void RemoveCheatDefine()
        {
            foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.Standalone, NamedBuildTarget.iOS, NamedBuildTarget.WebGL })
            {
                PlayerSettings.GetScriptingDefineSymbols(t, out string[] defines);
                PlayerSettings.SetScriptingDefineSymbols(t, defines.Where(d => d != CheatDefine).ToArray());
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[HordeCallBuild] ZW_CHEATS removed; cheats stay in the Editor and development builds.");
        }
    }

    /// <summary>Blocks any non-development build that would ship cheats or a placeholder bundle id.</summary>
    sealed class ReleaseGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool dev = (report.summary.options & BuildOptions.Development) != 0;
            var target = NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup);
            var problems = HordeCallBuild.ReleaseProblems(target, dev);
            if (problems.Count > 0)
                throw new BuildFailedException("[ReleaseGuard] " + string.Join(" ", problems));
        }
    }
}
