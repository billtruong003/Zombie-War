using System.Collections;
using BillGameCore;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar
{
    public partial class RunOverlays
    {
        // ------------------------------------------------------------ pause
        public void ShowPause()
        {
            if (TerminalOverlayActive) return;
            Time.timeScale = 0f;
            Show(pauseRoot, true);
            UIFx.ModalIn(pauseRoot != null ? pauseRoot.transform : null);
        }

        // The app went to the background (home button, a call): the run waits on the pause menu
        // instead of playing on unseen. A choice screen that already holds the game stays as it is.
        private void OnAppPause(AppPauseEvent e)
        {
            if (!e.IsPaused || RunState.Current == null || Time.timeScale == 0f) return;
            ShowPause();
        }

        private void ResumeWithCountdown()
        {
            Show(pauseRoot, false);
            Restart(CoResume());
        }

        private IEnumerator CoResume()
        {
            if (resumeCountText != null)
            {
                resumeCountText.gameObject.SetActive(true);
                for (int i = 3; i >= 1; i--)
                {
                    resumeCountText.text = i.ToString();
                    UIFx.PopIn(resumeCountText.transform, 0f, 1.4f, 0.25f);
                    yield return new WaitForSecondsRealtime(0.6f);
                }
                resumeCountText.gameObject.SetActive(false);
            }
            Time.timeScale = 1f;
            TryShowLevelUp();   // level-ups earned before/during the pause were held back
        }

        // "End run" from the pause menu. The run closes as a walk-away and the result screen shows
        // what was forfeited; Home on that screen is what actually leaves the world.
        private void EndRun()
        {
            Show(confirmRoot, false);
            Show(pauseRoot, false);
            if (RunState.Current == null || RunState.Current.IsOver)
            {
                Time.timeScale = 1f;
                GameFlow.ReturnToMenu();
                return;
            }
            Bill.Events?.Fire(new RunAbandonRequestedEvent());
        }

        private void OpenSettings()
        {
            Show(settingsRoot, true);
            UIFx.ModalIn(settingsRoot != null ? settingsRoot.transform : null);
        }
    }
}
