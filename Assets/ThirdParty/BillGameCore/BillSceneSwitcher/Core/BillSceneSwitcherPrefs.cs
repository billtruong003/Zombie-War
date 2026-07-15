#if UNITY_EDITOR
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
    }
}
#endif
