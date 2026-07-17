using System;
using BillGameCore;
using UnityEngine.SceneManagement;

namespace ZombieWar
{
    /// Central navigation for the app's scene/state flow. Bootstrap is the persistent base scene
    /// (services live on the DontDestroyOnLoad [BillGameCore] root). Menu and the gameplay map are
    /// loaded ADDITIVELY on top of it, so swapping menu <-> map never tears down services.
    public static class GameFlow
    {
        public const string MenuScene = "Menu";
        public const string GameplayScene = "Map_Level1";

        /// Called once from BootstrapEntry after Bill services are ready.
        public static void EnterMenu()
        {
            Bill.State.GoTo<MenuState>();
            if (!Bill.Scene.IsAdditiveLoaded(MenuScene))
                Bill.Scene.LoadAdditive(MenuScene);
        }

        /// Menu "Play" button -> unload menu, additive-load the map, make it the active scene
        /// (so runtime Instantiate/lighting/navmesh resolve against it), then enter GameplayState.
        public static void StartGameplay()
        {
            Bill.State.GoTo<LoadingState>();

            if (Bill.Scene.IsAdditiveLoaded(MenuScene))
                Bill.Scene.Unload(MenuScene);

            if (Bill.Scene.IsAdditiveLoaded(GameplayScene))
            {
                ActivateAndPlay();
                return;
            }

            Bill.Scene.LoadAdditive(GameplayScene, ActivateAndPlay);
        }

        /// Gameplay -> back to menu (e.g. game over / quit to menu).
        public static void ReturnToMenu()
        {
            if (Bill.Scene.IsAdditiveLoaded(GameplayScene))
                Bill.Scene.Unload(GameplayScene);
            EnterMenu();
        }

        private static void ActivateAndPlay()
        {
            Scene sc = SceneManager.GetSceneByName(GameplayScene);
            if (sc.IsValid() && sc.isLoaded)
                SceneManager.SetActiveScene(sc);
            Bill.State.GoTo<GameplayState>();
        }
    }
}
