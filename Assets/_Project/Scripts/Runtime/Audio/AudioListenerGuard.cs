using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieWar
{
    /// <summary>
    /// M7.3d — guarantees <b>exactly one</b> active <see cref="AudioListener"/> from boot through
    /// gameplay.
    ///
    /// The console reported "There are no audio listeners in the scene" during Menu: the only listener
    /// in the project lives on `MainCamera.prefab`, which does not exist until a gameplay scene loads,
    /// so menu audio played into nothing.
    ///
    /// The obvious fix — drop a second listener somewhere — is worse than the bug: two listeners make
    /// Unity warn and produce undefined 3D panning. So this guard owns a FALLBACK listener that is
    /// active only while no other listener is, and steps aside the moment the camera brings its own.
    ///
    /// Created from code rather than a scene object, because Bootstrap.unity may not be edited.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioListenerGuard : MonoBehaviour
    {
        static AudioListenerGuard _instance;
        AudioListener _fallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("~AudioListenerGuard");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioListenerGuard>();
        }

        void Awake()
        {
            _fallback = gameObject.AddComponent<AudioListener>();
            _fallback.enabled = false;                  // Resolve decides
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Resolve();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (_instance == this) _instance = null;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m) => Resolve();
        void OnSceneUnloaded(Scene s) => Resolve();

        /// <summary>
        /// Exactly one listener wins. A real camera listener always beats the fallback, so 3D panning
        /// stays anchored to the camera during gameplay; the fallback only covers menu-only frames.
        /// </summary>
        void Resolve()
        {
            var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            AudioListener chosen = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == _fallback) continue;
                if (chosen == null) { chosen = all[i]; continue; }
                all[i].enabled = false;                 // never two
            }

            if (chosen != null)
            {
                chosen.enabled = true;
                if (_fallback != null) _fallback.enabled = false;
            }
            else if (_fallback != null)
            {
                _fallback.enabled = true;               // menu-only: never zero
            }
        }

        /// <summary>Diagnostics for the test: how many listeners are currently enabled.</summary>
        public static int ActiveListenerCount()
        {
            var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (all[i].enabled) n++;
            return n;
        }
    }
}
