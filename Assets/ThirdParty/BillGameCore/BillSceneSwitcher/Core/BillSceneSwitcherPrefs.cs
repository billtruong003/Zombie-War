#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace BillGameCore.BillSceneSwitcher
{
    /// <summary>
    /// All Scene Switcher user preferences. Backed by EditorPrefs.
    /// </summary>
    public static class BillSceneSwitcherPrefs
    {
        public static bool Enabled
        {
            get => EditorPrefs.GetBool("BillSceneSwitcher.Enabled", true);
            set => EditorPrefs.SetBool("BillSceneSwitcher.Enabled", value);
        }

        public static bool ShowInToolbar
        {
            get => EditorPrefs.GetBool("BillSceneSwitcher.Toolbar", true);
            set => EditorPrefs.SetBool("BillSceneSwitcher.Toolbar", value);
        }

        public static bool ShowAdditiveButton
        {
            get => EditorPrefs.GetBool("BillSceneSwitcher.Additive", true);
            set => EditorPrefs.SetBool("BillSceneSwitcher.Additive", value);
        }

        public static bool ConfirmSceneSwitch
        {
            get => EditorPrefs.GetBool("BillSceneSwitcher.Confirm", true);
            set => EditorPrefs.SetBool("BillSceneSwitcher.Confirm", value);
        }

        public static bool ShowScenePath
        {
            get => EditorPrefs.GetBool("BillSceneSwitcher.ShowPath", true);
            set => EditorPrefs.SetBool("BillSceneSwitcher.ShowPath", value);
        }

        /// <summary>Folders whose scenes count as "the project"; everything else is a bought asset's demo.</summary>
        public static string ProjectFolders
        {
            get => EditorPrefs.GetString("BillSceneSwitcher.ProjectFolders", "Assets/_Project;Assets/Scenes");
            set => EditorPrefs.SetString("BillSceneSwitcher.ProjectFolders", value);
        }

        public static IReadOnlyList<string> ProjectFolderList => Split(ProjectFolders);

        /// <summary>Folders never listed (Unity's crash-recovery scenes by default).</summary>
        public static string ExcludedFolders
        {
            get => EditorPrefs.GetString("BillSceneSwitcher.ExcludedFolders", "Assets/_Recovery");
            set => EditorPrefs.SetString("BillSceneSwitcher.ExcludedFolders", value);
        }

        public static IReadOnlyList<string> ExcludedFolderList => Split(ExcludedFolders);

        static IReadOnlyList<string> Split(string value)
        {
            var list = new List<string>();
            foreach (var part in value.Split(';'))
                if (!string.IsNullOrWhiteSpace(part)) list.Add(part.Trim().Replace('\\', '/'));
            return list;
        }

        public static int RecentCount
        {
            get => EditorPrefs.GetInt("BillSceneSwitcher.RecentCount", 3);
            set => EditorPrefs.SetInt("BillSceneSwitcher.RecentCount", value);
        }

        public static int LastTab
        {
            get => EditorPrefs.GetInt("BillSceneSwitcher.LastTab", 0);
            set => EditorPrefs.SetInt("BillSceneSwitcher.LastTab", value);
        }

        // Out-of-build tab filter chips.
        public static bool FilterProject
        {
            get => EditorPrefs.GetBool("BillSceneSwitcher.FilterProject", true);
            set => EditorPrefs.SetBool("BillSceneSwitcher.FilterProject", value);
        }

        public static bool FilterVendor
        {
            get => EditorPrefs.GetBool("BillSceneSwitcher.FilterVendor", false);
            set => EditorPrefs.SetBool("BillSceneSwitcher.FilterVendor", value);
        }

        public static bool FilterLoaded
        {
            get => EditorPrefs.GetBool("BillSceneSwitcher.FilterLoaded", false);
            set => EditorPrefs.SetBool("BillSceneSwitcher.FilterLoaded", value);
        }
    }
}
#endif
