using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>Owner rule (2026-09-27): features that need a server are designed but not built.</summary>
    public static class FeatureFlags
    {
        /// <summary>Leaderboard, friends, server mail, cloud save. Off until a backend exists.</summary>
        public const bool Backend = false;
    }

    /// <summary>
    /// M10 bottom tab bar (HOME ARSENAL SHOP GACHA PASS). Tabs are peers: a tab returns to the
    /// Home root and opens its screen on top, so Back from any tab lands on Home. Locked tabs
    /// (account level) show a toast instead of opening.
    /// </summary>
    public sealed class NavBarV2 : MonoBehaviour
    {
        [SerializeField] private Button[] tabs = new Button[5];
        [SerializeField] private UIScreen[] targets = new UIScreen[5];
        [SerializeField] private Image[] dots = new Image[5];

        static readonly AccountProgress.Feature?[] Gates = { null, null, null, AccountProgress.Feature.Gacha, AccountProgress.Feature.Pass };

        private void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                if (tabs[i] != null) tabs[i].onClick.AddListener(() => Open(idx));
            }
        }

        public void Open(int index)
        {
            var ui = UIManager.Instance;
            if (ui == null || index < 0 || index >= targets.Length) return;
            var gate = Gates[index];
            if (gate.HasValue && !AccountProgress.IsUnlocked(gate.Value))
            {
                UIFeedback.Error();
                Toast.Show($"Unlocks at level {AccountProgress.RequiredLevel(gate.Value)}");
                return;
            }
            UIFeedback.Tap();
            var target = targets[index];
            ui.PopTo<HomeScreen>();
            if (index != 0 && target != null) ui.Push(target);
        }

        public void SetDot(int index, bool on)
        {
            if (index >= 0 && index < dots.Length && dots[index] != null) dots[index].gameObject.SetActive(on);
        }
    }
}
