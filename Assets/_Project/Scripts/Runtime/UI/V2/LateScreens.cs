using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieWar.UI
{
    /// <summary>
    /// Screens added after Menu.unity was authored (05/10: Daily Ops, gun mastery, achievements).
    /// They live as prefabs in Resources/UI/Late and are put under the scene's UIManager when the menu
    /// loads, so the owner's scene file is never touched.
    /// </summary>
    public static class LateScreens
    {
        static readonly string[] Prefabs = { "UI/Late/UI_V2_DailyOps", "UI/Late/UI_V2_Mastery", "UI/Late/UI_V2_Achievements" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Install();
        }

        static void OnLoaded(Scene _, LoadSceneMode __) => Install();

        static void Install()
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            foreach (var path in Prefabs)
            {
                var prefab = Resources.Load<UIScreen>(path);
                if (prefab == null || ui.Get(prefab.GetType()) != null) continue;
                var screen = Object.Instantiate(prefab, ui.transform, false);
                screen.name = prefab.name;
                // Stretch to UIRoot like the scene's screens (the prefab root keeps its design size).
                if (screen.transform is RectTransform rt)
                {
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = rt.offsetMax = Vector2.zero; rt.anchoredPosition = Vector2.zero;
                }
                ui.Register(screen);
            }
        }

        /// <summary>Opens a late screen, installing it first if the menu loaded before it existed.</summary>
        public static T Open<T>() where T : UIScreen
        {
            var ui = UIManager.Instance;
            if (ui == null) return null;
            if (ui.Get<T>() == null) Install();
            var s = ui.Get<T>();
            if (s != null) ui.Push(s);
            return s;
        }
    }
}
