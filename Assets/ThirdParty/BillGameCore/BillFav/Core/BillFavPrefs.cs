#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BillGameCore.BillFav
{
    /// <summary>
    /// All BillFav user preferences in one place. Backed by EditorPrefs.
    /// </summary>
    public static class BillFavPrefs
    {
        // ── Activation ──
        public static KeyCombination ActivationKey
        {
            get => (KeyCombination)EditorPrefs.GetInt("BillFav.ActivationKey", 0);
            set => EditorPrefs.SetInt("BillFav.ActivationKey", (int)value);
        }

        // ── Shortcuts ──
        public static bool PageScrollEnabled
        {
            get => EditorPrefs.GetBool("BillFav.PageScroll", true);
            set => EditorPrefs.SetBool("BillFav.PageScroll", value);
        }

        public static bool NumberKeysEnabled
        {
            get => EditorPrefs.GetBool("BillFav.NumberKeys", true);
            set => EditorPrefs.SetBool("BillFav.NumberKeys", value);
        }

        public static bool ArrowKeysEnabled
        {
            get => EditorPrefs.GetBool("BillFav.ArrowKeys", true);
            set => EditorPrefs.SetBool("BillFav.ArrowKeys", value);
        }

        // ── Animations ──
        public static bool FadeAnimations
        {
            get => EditorPrefs.GetBool("BillFav.FadeAnim", true);
            set => EditorPrefs.SetBool("BillFav.FadeAnim", value);
        }

        public static bool PageScrollAnimation
        {
            get => EditorPrefs.GetBool("BillFav.PageScrollAnim", true);
            set => EditorPrefs.SetBool("BillFav.PageScrollAnim", value);
        }

        // ── State ──
        public static bool PluginDisabled
        {
            get => EditorPrefs.GetBool("BillFav.Disabled", false);
            set => EditorPrefs.SetBool("BillFav.Disabled", value);
        }

        public static bool OverlayEnabled
        {
            get => EditorPrefs.GetBool("BillFav.Overlay", true);
            set => EditorPrefs.SetBool("BillFav.Overlay", value);
        }

        // ── Enums ──
        public enum KeyCombination
        {
            Alt = 0,
            AltShift = 1,
            CtrlAlt = 2
        }

        /// <summary>
        /// Check if the configured activation shortcut is currently held.
        /// </summary>
        public static bool IsShortcutHeld(Event evt)
        {
            if (evt == null) return false;

            switch (ActivationKey)
            {
                case KeyCombination.Alt:
                    return evt.alt && !evt.shift && !evt.control && !evt.command;

                case KeyCombination.AltShift:
                    return evt.alt && evt.shift;

                case KeyCombination.CtrlAlt:
                    return Application.platform == RuntimePlatform.OSXEditor
                        ? evt.command && evt.alt
                        : evt.control && evt.alt;

                default: return false;
            }
        }
    }
}
#endif
