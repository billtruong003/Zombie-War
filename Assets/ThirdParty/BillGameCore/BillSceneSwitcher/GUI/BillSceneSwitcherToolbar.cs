#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine.SceneManagement;

namespace BillGameCore.BillSceneSwitcher
{
    /// <summary>
    /// Scene Switcher toolbar dropdown using Unity 6 MainToolbar API.
    /// Appears as a dropdown button in the main toolbar showing the active scene name.
    /// </summary>
    public static class BillSceneSwitcherToolbar
    {
        const string k_ElementId = "BillGameCore/Scene Switcher";

        [MainToolbarElement(k_ElementId, defaultDockPosition = MainToolbarDockPosition.Right)]
        static MainToolbarElement CreateDropdown()
        {
            var content = new MainToolbarContent(
                GetSceneDisplayName(),
                EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D,
                "Scene Switcher - Click to switch scenes"
            );

            var dropdown = new MainToolbarDropdown(content, OnDropdownClicked);

            // Respect user prefs for visibility
            dropdown.displayed = BillSceneSwitcherPrefs.Enabled && BillSceneSwitcherPrefs.ShowInToolbar;

            // Listen for scene changes to update the displayed name
            EditorApplication.delayCall += () =>
            {
                SceneManager.activeSceneChanged -= OnSceneChanged;
                SceneManager.activeSceneChanged += OnSceneChanged;
                EditorSceneManager.sceneOpened -= OnSceneOpened;
                EditorSceneManager.sceneOpened += OnSceneOpened;
            };

            return dropdown;
        }

        static void OnDropdownClicked(Rect buttonRect)
        {
            if (!BillSceneSwitcherPrefs.Enabled)
            {
                var menu = new GenericMenu();
                menu.AddDisabledItem(new GUIContent("Scene Switcher is disabled"));
                menu.AddItem(new GUIContent("Enable"), false, () =>
                {
                    BillSceneSwitcherPrefs.Enabled = true;
                    MainToolbar.Refresh(k_ElementId);
                });
                menu.DropDown(buttonRect);
                return;
            }

            // The toolbar callback rect is in GUI-local coordinates.
            // Convert to screen coordinates so ShowAsDropDown positions
            // the window on the correct monitor.
            var screenRect = GUIUtility.GUIToScreenRect(buttonRect);
            BillSceneSwitcherDropdown.Show(screenRect);
        }

        // ───────────────────────────────────────────
        // Scene change listeners → refresh toolbar text
        // ───────────────────────────────────────────

        static void OnSceneChanged(Scene prev, Scene next) => RefreshToolbar();
        static void OnSceneOpened(Scene scene, OpenSceneMode mode) => RefreshToolbar();

        static void RefreshToolbar()
        {
            // Delay to ensure scene is fully loaded
            EditorApplication.delayCall += () => MainToolbar.Refresh(k_ElementId);
        }

        // ───────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────

        static string GetSceneDisplayName()
        {
            var scene = EditorSceneManager.GetActiveScene();
            string name;

            if (!string.IsNullOrEmpty(scene.name))
                name = scene.name;
            else if (!string.IsNullOrEmpty(scene.path))
                name = System.IO.Path.GetFileNameWithoutExtension(scene.path);
            else
                name = "Untitled";

            return name.Length > 18 ? name.Substring(0, 16) + ".." : name;
        }
    }
}
#endif
