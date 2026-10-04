using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Writes any coalesced profile change when the app goes to the background or quits. Coalesced
    /// writes (gems, mission progress, voice flags) wait a moment before hitting the disk; without
    /// this an Android kill or a closed WebGL tab in that window lost them.
    /// Created from code (like AudioListenerGuard) because Bootstrap.unity is not edited.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProfileAutoSave : MonoBehaviour
    {
        static ProfileAutoSave _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("~ProfileAutoSave");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ProfileAutoSave>();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) PlayerProfile.FlushIfDirty();
        }

        void OnApplicationFocus(bool focused)
        {
            // WebGL: a tab switch blurs the page before the browser may discard it.
            if (!focused && Application.platform == RuntimePlatform.WebGLPlayer) PlayerProfile.FlushIfDirty();
        }

        void OnApplicationQuit() => PlayerProfile.FlushIfDirty();

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
