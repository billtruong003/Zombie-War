#if UNITY_EDITOR
using UnityEditor;

namespace BillGameCore.BillFav
{
    /// <summary>
    /// Menu items under BillGameCore > BillFav.
    /// Clean, readable method names — not "dadsas" gibberish.
    /// </summary>
    public static class BillFavMenuItems
    {
        const string Root = "BillGameCore/BillFav/";

        // ── Shortcuts section ──

        [MenuItem(Root + "Shortcuts/Scroll to change page", false, 1)]
        static void TogglePageScroll() => BillFavPrefs.PageScrollEnabled = !BillFavPrefs.PageScrollEnabled;

        [MenuItem(Root + "Shortcuts/Scroll to change page", true, 1)]
        static bool ValidatePageScroll() { Menu.SetChecked(Root + "Shortcuts/Scroll to change page", BillFavPrefs.PageScrollEnabled); return !BillFavPrefs.PluginDisabled; }

        [MenuItem(Root + "Shortcuts/1-9 keys to change page", false, 2)]
        static void ToggleNumberKeys() => BillFavPrefs.NumberKeysEnabled = !BillFavPrefs.NumberKeysEnabled;

        [MenuItem(Root + "Shortcuts/1-9 keys to change page", true, 2)]
        static bool ValidateNumberKeys() { Menu.SetChecked(Root + "Shortcuts/1-9 keys to change page", BillFavPrefs.NumberKeysEnabled); return !BillFavPrefs.PluginDisabled; }

        [MenuItem(Root + "Shortcuts/Arrow keys navigation", false, 3)]
        static void ToggleArrowKeys() => BillFavPrefs.ArrowKeysEnabled = !BillFavPrefs.ArrowKeysEnabled;

        [MenuItem(Root + "Shortcuts/Arrow keys navigation", true, 3)]
        static bool ValidateArrowKeys() { Menu.SetChecked(Root + "Shortcuts/Arrow keys navigation", BillFavPrefs.ArrowKeysEnabled); return !BillFavPrefs.PluginDisabled; }

        // ── Animations section ──

        [MenuItem(Root + "Animations/Fade animations", false, 101)]
        static void ToggleFade() => BillFavPrefs.FadeAnimations = !BillFavPrefs.FadeAnimations;

        [MenuItem(Root + "Animations/Fade animations", true, 101)]
        static bool ValidateFade() { Menu.SetChecked(Root + "Animations/Fade animations", BillFavPrefs.FadeAnimations); return !BillFavPrefs.PluginDisabled; }

        [MenuItem(Root + "Animations/Page scroll animation", false, 102)]
        static void ToggleScrollAnim() => BillFavPrefs.PageScrollAnimation = !BillFavPrefs.PageScrollAnimation;

        [MenuItem(Root + "Animations/Page scroll animation", true, 102)]
        static bool ValidateScrollAnim() { Menu.SetChecked(Root + "Animations/Page scroll animation", BillFavPrefs.PageScrollAnimation); return !BillFavPrefs.PluginDisabled; }

        // ── Activation key section ──

        [MenuItem(Root + "Open when/Holding Alt", false, 201)]
        static void SetAlt() => BillFavPrefs.ActivationKey = BillFavPrefs.KeyCombination.Alt;

        [MenuItem(Root + "Open when/Holding Alt", true, 201)]
        static bool ValidateAlt() { Menu.SetChecked(Root + "Open when/Holding Alt", BillFavPrefs.ActivationKey == BillFavPrefs.KeyCombination.Alt); return !BillFavPrefs.PluginDisabled; }

        [MenuItem(Root + "Open when/Holding Alt + Shift", false, 202)]
        static void SetAltShift() => BillFavPrefs.ActivationKey = BillFavPrefs.KeyCombination.AltShift;

        [MenuItem(Root + "Open when/Holding Alt + Shift", true, 202)]
        static bool ValidateAltShift() { Menu.SetChecked(Root + "Open when/Holding Alt + Shift", BillFavPrefs.ActivationKey == BillFavPrefs.KeyCombination.AltShift); return !BillFavPrefs.PluginDisabled; }

#if UNITY_EDITOR_OSX
        [MenuItem(Root + "Open when/Holding Cmd + Alt", false, 203)]
#else
        [MenuItem(Root + "Open when/Holding Ctrl + Alt", false, 203)]
#endif
        static void SetCtrlAlt() => BillFavPrefs.ActivationKey = BillFavPrefs.KeyCombination.CtrlAlt;

#if UNITY_EDITOR_OSX
        [MenuItem(Root + "Open when/Holding Cmd + Alt", true, 203)]
#else
        [MenuItem(Root + "Open when/Holding Ctrl + Alt", true, 203)]
#endif
        static bool ValidateCtrlAlt() { var path = Root + "Open when/" +
#if UNITY_EDITOR_OSX
            "Holding Cmd + Alt";
#else
            "Holding Ctrl + Alt";
#endif
            Menu.SetChecked(path, BillFavPrefs.ActivationKey == BillFavPrefs.KeyCombination.CtrlAlt); return !BillFavPrefs.PluginDisabled; }

        // ── Mode toggles ──

        [MenuItem(Root + "Overlay mode (Alt-key)", false, 301)]
        static void ToggleOverlay() => BillFavPrefs.OverlayEnabled = !BillFavPrefs.OverlayEnabled;

        [MenuItem(Root + "Overlay mode (Alt-key)", true, 301)]
        static bool ValidateOverlay() { Menu.SetChecked(Root + "Overlay mode (Alt-key)", BillFavPrefs.OverlayEnabled); return !BillFavPrefs.PluginDisabled; }

        // ── Plugin control ──

        [MenuItem(Root + "Disable BillFav", false, 10000)]
        static void ToggleDisable()
        {
            BillFavPrefs.PluginDisabled = !BillFavPrefs.PluginDisabled;
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        }

        [MenuItem(Root + "Disable BillFav", true, 10000)]
        static bool ValidateDisable() { Menu.SetChecked(Root + "Disable BillFav", BillFavPrefs.PluginDisabled); return true; }
    }
}
#endif
