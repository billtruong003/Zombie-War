using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Menu toggle that bakes the cheat panel into player builds via the ZW_CHEATS
    /// scripting define. Checked = every build (including release APKs) ships with
    /// cheats; unchecked = cheats exist only in Editor / Development Builds.
    /// </summary>
    public static class CheatBuildToggle
    {
        private const string Define = "ZW_CHEATS";
        private const string MenuPath = "ZombieWar/Cheats In Build";

        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Android,
            NamedBuildTarget.Standalone,
            NamedBuildTarget.iOS,
            NamedBuildTarget.WebGL,
        };

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool enable = !IsEnabled();
            foreach (var target in Targets)
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
                var list = new List<string>(defines);
                list.Remove(Define);
                if (enable) list.Add(Define);
                PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CheatBuildToggle] ZW_CHEATS {(enable ? "ENABLED — cheats will be in every build" : "disabled — cheats only in Editor/Development Builds")}.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, IsEnabled());
            return true;
        }

        private static bool IsEnabled()
        {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out string[] defines);
            return defines.Contains(Define);
        }
    }
}
