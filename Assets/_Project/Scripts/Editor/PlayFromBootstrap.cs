using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Pressing Play always boots through Bootstrap.unity, whatever scene is open.
    ///
    /// Services live on the Bootstrap root and every other scene loads additively on top of it, so
    /// playing Menu or the world directly used to spam "SERVICE NOT FOUND". Unity's
    /// playModeStartScene makes the editor enter play mode from Bootstrap while the open scenes stay
    /// open for editing. Toggle with Tools/ZombieWar/Play From Bootstrap.
    ///
    /// The PlayMode test runner enters play mode from its own generated "InitTestScene"; forcing
    /// Bootstrap there would hijack the run, so the start scene is cleared for exactly that entry.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBootstrap
    {
        private const string BootstrapPath = "Assets/_Project/Scenes/Bootstrap.unity";
        private const string MenuPath = "Tools/ZombieWar/Play From Bootstrap";
        private const string PrefKey = "ZombieWar.PlayFromBootstrap";
        private const string TestInitScenePrefix = "InitTestScene";

        static PlayFromBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += Apply;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode &&
                EditorSceneManager.GetActiveScene().name.StartsWith(TestInitScenePrefix))
                EditorSceneManager.playModeStartScene = null;
            else if (change == PlayModeStateChange.EnteredEditMode)
                Apply();
        }

        private static void Apply() =>
            EditorSceneManager.playModeStartScene =
                Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapPath) : null;
    }
}
