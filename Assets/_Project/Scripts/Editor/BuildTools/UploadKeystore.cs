using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// Signs Android builds with the HordeCall upload key (06/10). The key and its passwords never enter
    /// the repo: they live in ~/.hordecall/ (hordecall-upload.keystore + upload-keystore.json) on the
    /// build machine, and the owner keeps a copy. Unity does not save keystore passwords, so they are
    /// filled in here before every Android build; ReleaseGuard (order -100) then sees a signed build.
    /// Without the files nothing changes (development builds use the debug key).
    /// </summary>
    public sealed class UploadKeystore : IPreprocessBuildWithReport
    {
        public int callbackOrder => -200;

        [Serializable] class Config { public string keystore, alias, storePass, keyPass; }

        public static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hordecall", "upload-keystore.json");

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Apply();
        }

        [MenuItem("HordeCall/Build/Use upload keystore")]
        public static bool Apply()
        {
            if (!File.Exists(ConfigPath)) return false;
            var c = JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath));
            if (c == null || string.IsNullOrEmpty(c.keystore) || !File.Exists(c.keystore))
            {
                Debug.LogWarning("[UploadKeystore] " + ConfigPath + " points to a missing keystore.");
                return false;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = c.keystore;
            PlayerSettings.Android.keyaliasName = c.alias;
            PlayerSettings.Android.keystorePass = c.storePass;
            PlayerSettings.Android.keyaliasPass = c.keyPass;
            return true;
        }
    }
}
