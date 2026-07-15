#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Toolbars;

namespace BillGameCore.BillSceneSwitcher
{
    /// <summary>
    /// Menu items under BillGameCore > Scene Switcher.
    /// Toggle preferences and open the panel.
    /// </summary>
    public static class BillSceneSwitcherMenuItems
    {
        const string Root = "BillGameCore/Scene Switcher/";

        // ── Open panel ──

        [MenuItem("BillGameCore/Scene Switcher %#s", false, 0)]
        static void OpenSceneSwitcher()
        {
            // Show the dropdown at center of screen
            var screenCenter = new UnityEngine.Rect(
                UnityEngine.Screen.width / 2f - 180,
                120, 360, 0);
            BillSceneSwitcherDropdown.Show(screenCenter);
        }

        // ── Toggle settings ──

        [MenuItem(Root + "Show Toolbar Button", false, 100)]
        static void ToggleToolbar()
        {
            BillSceneSwitcherPrefs.ShowInToolbar = !BillSceneSwitcherPrefs.ShowInToolbar;
            MainToolbar.Refresh("BillGameCore/Scene Switcher");
        }

        [MenuItem(Root + "Show Toolbar Button", true, 100)]
        static bool ValidateToolbar()
        {
            Menu.SetChecked(Root + "Show Toolbar Button", BillSceneSwitcherPrefs.ShowInToolbar);
            return BillSceneSwitcherPrefs.Enabled;
        }

        [MenuItem(Root + "Show Additive Load Button", false, 101)]
        static void ToggleAdditive() => BillSceneSwitcherPrefs.ShowAdditiveButton = !BillSceneSwitcherPrefs.ShowAdditiveButton;

        [MenuItem(Root + "Show Additive Load Button", true, 101)]
        static bool ValidateAdditive()
        {
            Menu.SetChecked(Root + "Show Additive Load Button", BillSceneSwitcherPrefs.ShowAdditiveButton);
            return BillSceneSwitcherPrefs.Enabled;
        }

        [MenuItem(Root + "Show Scene Path", false, 102)]
        static void TogglePath() => BillSceneSwitcherPrefs.ShowScenePath = !BillSceneSwitcherPrefs.ShowScenePath;

        [MenuItem(Root + "Show Scene Path", true, 102)]
        static bool ValidatePath()
        {
            Menu.SetChecked(Root + "Show Scene Path", BillSceneSwitcherPrefs.ShowScenePath);
            return BillSceneSwitcherPrefs.Enabled;
        }

        [MenuItem(Root + "Confirm Scene Switch", false, 103)]
        static void ToggleConfirm() => BillSceneSwitcherPrefs.ConfirmSceneSwitch = !BillSceneSwitcherPrefs.ConfirmSceneSwitch;

        [MenuItem(Root + "Confirm Scene Switch", true, 103)]
        static bool ValidateConfirm()
        {
            Menu.SetChecked(Root + "Confirm Scene Switch", BillSceneSwitcherPrefs.ConfirmSceneSwitch);
            return BillSceneSwitcherPrefs.Enabled;
        }

        // ── Enable/Disable ──

        [MenuItem(Root + "Disable Scene Switcher", false, 10000)]
        static void ToggleDisable()
        {
            BillSceneSwitcherPrefs.Enabled = !BillSceneSwitcherPrefs.Enabled;
            MainToolbar.Refresh("BillGameCore/Scene Switcher");
        }

        [MenuItem(Root + "Disable Scene Switcher", true, 10000)]
        static bool ValidateDisable()
        {
            Menu.SetChecked(Root + "Disable Scene Switcher", !BillSceneSwitcherPrefs.Enabled);
            return true;
        }
    }
}
#endif
