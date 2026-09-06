using BillGameCore;
using UnityEngine.SceneManagement;

namespace ZombieWar
{
    /// Central navigation for the app's scene/state flow. Bootstrap is the persistent base scene
    /// (services live on the DontDestroyOnLoad [BillGameCore] root). Menu and the gameplay map are
    /// loaded ADDITIVELY on top of it, so swapping menu <-> map never tears down services.
    ///
    /// The gameplay scene is no longer a constant: the campaign selector picks a stage and every
    /// load/restart/unload works against THAT scene. <see cref="ActiveGameplayScene"/> is the single
    /// record of which map is currently loaded, which is what stops a restart from reloading Stage 1
    /// while Stage 3 is still in memory (two maps loaded, orphan Player).
    public static class GameFlow
    {
        public const string MenuScene = "Menu";
        public const string DefaultGameplayScene = "Map_Level1";

        /// <summary>Campaign id that belongs to <see cref="DefaultGameplayScene"/>. A direct
        /// Play-from-editor start is Stage 1, not an anonymous run - without this the run banked no
        /// stage completion and no first-clear reward.</summary>
        public const string DefaultLevelId = "level.1";

        /// <summary>The campaign level the player selected. Null means "not chosen" and the flow
        /// falls back to the default scene, so a direct Play-from-editor still works.</summary>
        public static CampaignLevel SelectedLevel { get; private set; }

        /// <summary>Which gameplay scene is actually loaded right now. Empty when none is.</summary>
        public static string ActiveGameplayScene { get; private set; } = "";

        public static string PendingGameplayScene =>
            SelectedLevel != null && !string.IsNullOrEmpty(SelectedLevel.sceneName)
                ? SelectedLevel.sceneName
                : DefaultGameplayScene;

        /// <summary>The campaign id for the run that <see cref="StartGameplay"/> would begin now.</summary>
        public static string PendingLevelId => LevelIdForScene(PendingGameplayScene);

        /// <summary>
        /// Resolves which campaign stage a gameplay scene represents.
        ///
        /// Derived from the SCENE rather than straight from <see cref="SelectedLevel"/> on purpose.
        /// Restart reloads <see cref="ActiveGameplayScene"/>, which is not always the selected level -
        /// reading the selection there could bank a clear against a stage the player is not on. Tying
        /// both the scene and the id to the same argument makes that disagreement unrepresentable.
        ///
        /// Returns "" for a scene with no campaign entry (a test map). That is a valid run: it simply
        /// earns no stage completion, and RunDirector still reports it as finished.
        /// </summary>
        public static string LevelIdForScene(string scene)
        {
            if (SelectedLevel != null && SelectedLevel.sceneName == scene &&
                !string.IsNullOrEmpty(SelectedLevel.levelId))
                return SelectedLevel.levelId;

            return scene == DefaultGameplayScene ? DefaultLevelId : "";
        }

        public static void SelectLevel(CampaignLevel level)
        {
            SelectedLevel = level;
            if (level != null) PlayerProfile.LastSelectedLevelId = level.levelId;
        }

        /// Called once from BootstrapEntry after Bill services are ready.
        public static void EnterMenu()
        {
            Bill.State.GoTo<MenuState>();
            if (!Bill.Scene.IsAdditiveLoaded(MenuScene))
                Bill.Scene.LoadAdditive(MenuScene);
        }

        /// Campaign "CHƠI" -> unload menu, additive-load the selected map, make it the active scene
        /// (so runtime Instantiate/lighting/navmesh resolve against it), then enter GameplayState.
        public static void StartGameplay()
        {
            string scene = PendingGameplayScene;
            Bill.State.GoTo<LoadingState>();

            if (Bill.Scene.IsAdditiveLoaded(MenuScene))
                Bill.Scene.Unload(MenuScene);

            // Switching stages without going through Home: drop the old map first so two campaign
            // scenes can never be live at once.
            if (!string.IsNullOrEmpty(ActiveGameplayScene) && ActiveGameplayScene != scene
                && Bill.Scene.IsAdditiveLoaded(ActiveGameplayScene))
            {
                string stale = ActiveGameplayScene;
                ActiveGameplayScene = "";
                Bill.Scene.Unload(stale, () => LoadGameplay(scene));
                return;
            }

            if (Bill.Scene.IsAdditiveLoaded(scene))
            {
                ActivateAndPlay(scene);
                return;
            }

            LoadGameplay(scene);
        }

        /// Game over "CHƠI LẠI" -> tear the ACTIVE map down and load it fresh (services untouched).
        /// Always reloads the stage that was being played, never the default.
        public static void RestartGameplay()
        {
            string scene = !string.IsNullOrEmpty(ActiveGameplayScene) ? ActiveGameplayScene : PendingGameplayScene;

            if (!Bill.Scene.IsAdditiveLoaded(scene)) { StartGameplay(); return; }

            Bill.State.GoTo<LoadingState>();
            ActiveGameplayScene = "";
            Bill.Scene.Unload(scene, () => LoadGameplay(scene));
        }

        /// Gameplay -> back to menu. Unloads whichever campaign scene is loaded, and abandons the
        /// run so its unbanked currency is discarded rather than paid out.
        public static void ReturnToMenu()
        {
            RunState.Abandon();

            if (!string.IsNullOrEmpty(ActiveGameplayScene) && Bill.Scene.IsAdditiveLoaded(ActiveGameplayScene))
                Bill.Scene.Unload(ActiveGameplayScene);
            ActiveGameplayScene = "";

            EnterMenu();
        }

        private static void LoadGameplay(string scene) =>
            Bill.Scene.LoadAdditive(scene, () => ActivateAndPlay(scene));

        private static void ActivateAndPlay(string scene)
        {
            Scene sc = SceneManager.GetSceneByName(scene);
            if (sc.IsValid() && sc.isLoaded)
                SceneManager.SetActiveScene(sc);

            ActiveGameplayScene = scene;
            RunState.Begin(LevelIdForScene(scene));
            Bill.State.GoTo<GameplayState>();
        }
    }
}
