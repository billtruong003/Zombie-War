using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Development builds report to Firebase project hordecall-dev, release builds to hordecall-prod.
    /// Both google-services.json files live in Config/firebase/&lt;env&gt;/ (outside Assets); before an
    /// Android build the right one is copied to <see cref="Target"/>, where the Firebase editor
    /// plugin turns it into the Android resources. The editor itself keeps the dev copy.
    /// </summary>
    public sealed class FirebaseConfigSwitch : IPreprocessBuildWithReport
    {
        public const string Target = "Assets/_Project/Settings/Firebase/google-services.json";
        public int callbackOrder => -100;

        public static string SourceFor(bool development) =>
            Path.Combine("Config", "firebase", development ? "dev" : "prod", "google-services.json");

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android && report.summary.platform != BuildTarget.iOS) return;
            Use((report.summary.options & BuildOptions.Development) != 0);
        }

        [MenuItem("ZombieWar/Build/Firebase - use dev config")]
        static void UseDev() => Use(true);

        [MenuItem("ZombieWar/Build/Firebase - use prod config")]
        static void UseProd() => Use(false);

        public static void Use(bool development)
        {
            string src = SourceFor(development);
            if (!File.Exists(src)) throw new BuildFailedException("Missing " + src);
            string text = File.ReadAllText(src);
            if (File.Exists(Target) && File.ReadAllText(Target) == text) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            File.WriteAllText(Target, text);
            AssetDatabase.ImportAsset(Target, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Firebase] Using the " + (development ? "dev" : "prod") + " project config.");
        }
    }
}
