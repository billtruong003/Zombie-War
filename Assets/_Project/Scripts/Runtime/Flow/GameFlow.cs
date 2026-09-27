using System.Collections.Generic;
using BillGameCore;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieWar.UI;

namespace ZombieWar
{
    /// Central navigation for the app's scene/state flow. Bootstrap is the persistent base scene
    /// (services live on the DontDestroyOnLoad [BillGameCore] root). Menu and the gameplay world are
    /// loaded ADDITIVELY on top of it, so swapping menu <-> world never tears down services.
    ///
    /// There is exactly one gameplay scene: the endless procedural world. Nothing selects a stage.
    public static class GameFlow
    {
        public const string MenuScene = "Menu";
        public const string GameplayScene = "Map_Level1";

        /// <summary>True while the gameplay scene is loaded.</summary>
        public static bool InGameplay { get; private set; }

        /// Called once from BootstrapEntry after Bill services are ready.
        public static void EnterMenu()
        {
            Bill.State.GoTo<MenuState>();
            if (!Bill.Scene.IsAdditiveLoaded(MenuScene))
                Bill.Scene.LoadAdditive(MenuScene, LoadingScreen.Complete);   // no-op at boot (not loading)
            else
            {
                SetMenuActive(true);
                UIManager.Instance?.Replace<HubScreen>();   // same as a fresh load: Hub alone, refreshed
                LoadingScreen.Complete();
            }
        }

        // M8-F (owner: back to the menu took ~5 s): the Menu scene stays loaded under the world and
        // is switched off during a run. Reloading it rebuilt ~930 dependencies (the Shop alone holds
        // hundreds of cards) in one 1.5-3.5 s frame; switching it back on is a single frame. Its
        // assets were never unloaded anyway (nothing calls UnloadUnusedAssets).
        static readonly List<GameObject> _menuRoots = new();

        static void SetMenuActive(bool on)
        {
            var scene = SceneManager.GetSceneByName(MenuScene);
            if (!scene.isLoaded) return;
            if (!on)
            {
                _menuRoots.Clear();
                foreach (var root in scene.GetRootGameObjects())
                    if (root.activeSelf) { _menuRoots.Add(root); root.SetActive(false); }
                return;
            }
            foreach (var root in _menuRoots)
                if (root != null) root.SetActive(true);
            _menuRoots.Clear();
        }

        /// Hub PLAY -> unload menu, additive-load the world, make it the active scene (so runtime
        /// Instantiate/lighting resolve against it), then enter GameplayState.
        public static void StartGameplay()
        {
            LoadingScreen.Begin();
            Bill.State.GoTo<LoadingState>();

            SetMenuActive(false);

            if (Bill.Scene.IsAdditiveLoaded(GameplayScene))
            {
                ActivateAndPlay();
                return;
            }

            LoadGameplay();
        }

        /// Result "PLAY AGAIN" -> tear the world down and load it fresh (services untouched).
        public static void RestartGameplay()
        {
            if (!Bill.Scene.IsAdditiveLoaded(GameplayScene)) { StartGameplay(); return; }

            LoadingScreen.Begin();
            Bill.State.GoTo<LoadingState>();
            InGameplay = false;
            Bill.Scene.Unload(GameplayScene, LoadGameplay);
        }

        /// Gameplay -> back to menu. A run that was not closed (the scene is left some other way than
        /// the result screen) is dropped unpaid, which is exactly what walking away banks anyway.
        public static void ReturnToMenu()
        {
            LoadingScreen.Begin();
            RunState.Abandon();

            InGameplay = false;
            // The menu comes back once the world is gone, so its camera, light and audio listener
            // never overlap the world's.
            if (Bill.Scene.IsAdditiveLoaded(GameplayScene))
                Bill.Scene.Unload(GameplayScene, EnterMenu);
            else
                EnterMenu();
        }

#if UNITY_EDITOR
        public const string SandboxScenePath = "Assets/_Project/Scenes/Dev/SkillSandbox.unity";

        /// Editor only (ZombieWar/Dev/Play Skill Sandbox): the skill test bench instead of the menu.
        /// The sandbox is not in the build, so it is loaded through the editor scene API.
        public static void StartSandbox()
        {
            LoadingScreen.Begin();
            Bill.State.GoTo<LoadingState>();
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                SandboxScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            if (op == null) { UnityEngine.Debug.LogError("[GameFlow] Sandbox scene missing: " + SandboxScenePath); return; }
            op.completed += _ => ActivateAndPlay(SceneManager.GetSceneByPath(SandboxScenePath));
        }
#endif

        private static void LoadGameplay() =>
            Bill.Scene.LoadAdditive(GameplayScene, ActivateAndPlay);

        private static void ActivateAndPlay() => ActivateAndPlay(SceneManager.GetSceneByName(GameplayScene));

        private static void ActivateAndPlay(Scene sc)
        {
            if (sc.IsValid() && sc.isLoaded)
                SceneManager.SetActiveScene(sc);

            InGameplay = true;
            RunState.Begin();
            Bill.State.GoTo<GameplayState>();
            LoadingScreen.Complete();
        }
    }
}
