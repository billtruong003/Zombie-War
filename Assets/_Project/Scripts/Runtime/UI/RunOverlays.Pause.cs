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
            // A card or chest choice already holds the game; Resume from a pause over it would set
            // the time running under the open choice.
            if ((levelUpRoot != null && levelUpRoot.activeSelf) || ChestOpen) return;
            // Paused again during the 3-2-1: that countdown must not unpause under the menu (07/10).
            Restart(null);
            if (resumeCountText != null) resumeCountText.gameObject.SetActive(false);
            Time.timeScale = 0f;
            Show(pauseRoot, true);
            UIFx.ModalIn(pauseRoot != null ? pauseRoot.transform : null);
        }

        // The app went to the background (home button, a call): the run waits on the pause menu
        // instead of playing on unseen. A choice screen that already holds the game stays as it is.
        private void OnAppPause(AppPauseEvent e)
        {
            RunInterruption.NotePause(e.IsPaused, RunState.Current);
            if (!e.IsPaused || RunState.Current == null || Time.timeScale == 0f) return;
            ShowPause();
        }

        // Android Back in a run (07/10: it did nothing - the menu's UIManager is off during a run):
        // closes settings or the end-run confirm, resumes from the pause menu, otherwise pauses.
        private void HandleBack()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (settingsRoot != null && settingsRoot.activeSelf) { Show(settingsRoot, false); return; }
            if (confirmRoot != null && confirmRoot.activeSelf) { Show(confirmRoot, false); Show(pauseRoot, true); return; }
            if (pauseRoot != null && pauseRoot.activeSelf) { ResumeWithCountdown(); return; }
            if (RunState.Current != null && !RunState.Current.IsOver) ShowPause();   // guards choices and the end screens
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
