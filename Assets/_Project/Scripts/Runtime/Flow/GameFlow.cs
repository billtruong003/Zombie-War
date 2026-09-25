using BillGameCore;
using UnityEngine.SceneManagement;

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
                Bill.Scene.LoadAdditive(MenuScene);
        }

        /// Hub PLAY -> unload menu, additive-load the world, make it the active scene (so runtime
        /// Instantiate/lighting resolve against it), then enter GameplayState.
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

            LoadGameplay();
        }

        /// Result "PLAY AGAIN" -> tear the world down and load it fresh (services untouched).
        public static void RestartGameplay()
        {
            if (!Bill.Scene.IsAdditiveLoaded(GameplayScene)) { StartGameplay(); return; }

            Bill.State.GoTo<LoadingState>();
            InGameplay = false;
            Bill.Scene.Unload(GameplayScene, LoadGameplay);
        }

        /// Gameplay -> back to menu. A run that was not closed (the scene is left some other way than
        /// the result screen) is dropped unpaid, which is exactly what walking away banks anyway.
        public static void ReturnToMenu()
        {
            RunState.Abandon();

            if (Bill.Scene.IsAdditiveLoaded(GameplayScene))
                Bill.Scene.Unload(GameplayScene);
            InGameplay = false;

            EnterMenu();
        }

        private static void LoadGameplay() =>
            Bill.Scene.LoadAdditive(GameplayScene, ActivateAndPlay);

        private static void ActivateAndPlay()
        {
            Scene sc = SceneManager.GetSceneByName(GameplayScene);
            if (sc.IsValid() && sc.isLoaded)
                SceneManager.SetActiveScene(sc);

            InGameplay = true;
            RunState.Begin();
            Bill.State.GoTo<GameplayState>();
        }
    }
}
