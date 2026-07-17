using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar
{
    /// Wires the menu scene's buttons to GameFlow. Kept dumb on purpose - all the scene/state
    /// transition logic lives in GameFlow so the menu is just a view.
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(GameFlow.StartGameplay);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
