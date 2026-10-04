using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// The null-safe binding one-liners every screen used to copy for itself (G12.4). Screens import
    /// them with <c>using static ZombieWar.UI.UIBind;</c> and call <c>Set(label, text)</c>.
    /// </summary>
    public static class UIBind
    {
        public static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }
        public static void On(Button b, UnityAction a) { if (b != null) b.onClick.AddListener(a); }
        /// <summary>SetActive on a node that may be missing ("Show" would hide UIScreen.Show).</summary>
        public static void Active(GameObject g, bool on) { if (g != null) g.SetActive(on); }
    }
}
