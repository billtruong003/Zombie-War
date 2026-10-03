#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BillGameCore.BillSceneSwitcher
{
    /// <summary>Preferences > Bill Scene Switcher: which folders are "the project", which are hidden.</summary>
    static class BillSceneSwitcherSettings
    {
        [SettingsProvider]
        static SettingsProvider Create() => new SettingsProvider("Preferences/Bill Scene Switcher", SettingsScope.User)
        {
            keywords = new[] { "scene", "switcher", "bootstrap", "recent" },
            guiHandler = _ =>
            {
                EditorGUIUtility.labelWidth = 180;
                EditorGUI.BeginChangeCheck();
                var project = EditorGUILayout.TextField(new GUIContent("Thư mục của project", "Ngăn cách bằng ;. Scene ngoài các thư mục này được coi là demo của asset mua."), BillSceneSwitcherPrefs.ProjectFolders);
                var excluded = EditorGUILayout.TextField(new GUIContent("Thư mục bỏ qua", "Ngăn cách bằng ;. Scene trong các thư mục này không bao giờ hiện."), BillSceneSwitcherPrefs.ExcludedFolders);
                var recent = EditorGUILayout.IntSlider("Số scene gần đây", BillSceneSwitcherPrefs.RecentCount, 0, 10);
                BillSceneSwitcherPrefs.ShowScenePath = EditorGUILayout.Toggle("Hiện đường dẫn scene", BillSceneSwitcherPrefs.ShowScenePath);
                BillSceneSwitcherPrefs.ShowAdditiveButton = EditorGUILayout.Toggle("Hiện nút mở thêm (additive)", BillSceneSwitcherPrefs.ShowAdditiveButton);
                BillSceneSwitcherPrefs.ConfirmSceneSwitch = EditorGUILayout.Toggle("Hỏi lưu khi đổi scene", BillSceneSwitcherPrefs.ConfirmSceneSwitch);
                if (EditorGUI.EndChangeCheck())
                {
                    bool rescan = excluded != BillSceneSwitcherPrefs.ExcludedFolders;
                    bool owners = project != BillSceneSwitcherPrefs.ProjectFolders;
                    BillSceneSwitcherPrefs.ProjectFolders = project;
                    BillSceneSwitcherPrefs.ExcludedFolders = excluded;
                    BillSceneSwitcherPrefs.RecentCount = recent;
                    if (rescan) BillSceneIndex.Invalidate();
                    else if (owners) BillSceneIndex.RefreshOwnership();
                }
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Đã index {BillSceneIndex.All.Count} scene ({BillSceneIndex.LastBuildMs:0} ms).", EditorStyles.miniLabel);
            },
        };
    }
}
#endif
