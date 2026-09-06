using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ZombieWar.Rendering.BillSSOutline
{
    /// <summary>
    /// Makes every scene Volume work on a RUNTIME CLONE of its profile for the whole play session
    /// (M5.1.2 CP4).
    ///
    /// The production outline profile (`Assets/Settings/SampleSceneProfile.asset`) was once found
    /// runtime-mutated (selection mask 30→22, debugMode on) after a play/capture session. An
    /// exhaustive search found no project code writing those fields, so the writer could not be
    /// eliminated at its source. This guard removes the entire class of failure instead: scene
    /// volumes are wired with <c>sharedProfile</c>, and touching <c>Volume.profile</c> here swaps
    /// the volume onto an instantiated copy. Any later write - project code, debug tooling, an
    /// inspector tweak during play - lands on the clone, which Unity discards when Play Mode ends.
    /// The imported asset can no longer change during gameplay or capture.
    /// </summary>
    public static class OutlineProfileRuntimeGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewSession()
        {
            // Domain reload is disabled in this project, so a static subscription survives Play
            // Mode exit and would stack one more callback on every subsequent session.
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            CloneLoadedVolumes();
            // Idempotent by construction: unsubscribe-then-subscribe can never register twice, no
            // matter how the session was entered.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CloneLoadedVolumes();

        private static void CloneLoadedVolumes()
        {
            foreach (Volume volume in Object.FindObjectsByType<Volume>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume.sharedProfile == null || volume.HasInstantiatedProfile()) continue;

                // The getter clones sharedProfile into an instantiated per-volume profile and makes
                // the volume evaluate from it. That single touch is the whole guard.
                _ = volume.profile;
            }
        }
    }
}
