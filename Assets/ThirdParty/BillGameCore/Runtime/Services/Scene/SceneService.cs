using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BillGameCore
{
    public class SceneService : ISceneService, IInitializable, IDisposableService
    {
        private bool _loading;
        private readonly List<string> _additiveScenes = new(8);
        private SceneTransitionOverlay _overlay;

        public string CurrentSceneName => SceneManager.GetActiveScene().name;
        public int CurrentBuildIndex => SceneManager.GetActiveScene().buildIndex;
        public bool IsLoading => _loading;
        public IReadOnlyList<string> LoadedAdditiveScenes => _additiveScenes;

        public void Initialize()
        {
            _overlay = new SceneTransitionOverlay();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        // -------------------------------------------------------
        // Single scene loading
        // -------------------------------------------------------

        public void Load(string name)
            => LoadImpl(name, -1, LoadSceneMode.Single, TransitionType.None, 0f, EaseType.Linear, null, null);

        public void Load(string name, TransitionType t, float dur = 0.5f)
            => LoadImpl(name, -1, LoadSceneMode.Single, t, dur, EaseType.OutQuad, null, null);

        public void Load(string name, TransitionType t, float dur, EaseType ease)
            => LoadImpl(name, -1, LoadSceneMode.Single, t, dur, ease, null, null);

        public void Load(int idx)
            => LoadImpl(null, idx, LoadSceneMode.Single, TransitionType.None, 0f, EaseType.Linear, null, null);

        // -------------------------------------------------------
        // Additive scene management
        // -------------------------------------------------------

        public void LoadAdditive(string name, Action onComplete = null)
        {
            if (_additiveScenes.Contains(name))
            {
                Debug.LogWarning($"[Bill.Scene] '{name}' already loaded additively.");
                onComplete?.Invoke();
                return;
            }
            CoroutineRunner.Run(AdditiveLoadRoutine(name, onComplete));
        }

        public void Unload(string name, Action onComplete = null)
        {
            var s = SceneManager.GetSceneByName(name);
            if (!s.isLoaded)
            {
                Debug.LogWarning($"[Bill.Scene] '{name}' not loaded, can't unload.");
                onComplete?.Invoke();
                return;
            }
            CoroutineRunner.Run(AdditiveUnloadRoutine(name, onComplete));
        }

        public void UnloadAllAdditive()
        {
            // Copy list because it mutates during unload
            var copy = new List<string>(_additiveScenes);
            foreach (var name in copy) Unload(name);
        }

        public bool IsAdditiveLoaded(string name) => _additiveScenes.Contains(name);

        // -------------------------------------------------------
        // Async with progress
        // -------------------------------------------------------

        public void LoadAsync(string name, Action<float> onProgress = null, Action onComplete = null)
            => CoroutineRunner.Run(AsyncRoutine(name, onProgress, onComplete));

        public void LoadWithTransition(string name, TransitionType transition, float duration, EaseType ease,
            Action<float> onProgress = null, Action onComplete = null)
            => LoadImpl(name, -1, LoadSceneMode.Single, transition, duration, ease, onProgress, onComplete);

        // -------------------------------------------------------
        // Navigation
        // -------------------------------------------------------

        public void Reload() => Load(CurrentBuildIndex);

        public void LoadNext()
        {
            int n = CurrentBuildIndex + 1;
            if (n < SceneManager.sceneCountInBuildSettings) Load(n);
        }

        public void LoadPrevious()
        {
            int p = CurrentBuildIndex - 1;
            if (p >= 0) Load(p);
        }

        // -------------------------------------------------------
        // Core load routine
        // -------------------------------------------------------

        private void LoadImpl(string name, int idx, LoadSceneMode mode, TransitionType trans, float dur,
            EaseType ease, Action<float> onProgress, Action onComplete)
        {
            if (_loading)
            {
                Debug.LogWarning("[Bill.Scene] Already loading, request ignored.");
                return;
            }
            CoroutineRunner.Run(LoadRoutine(name, idx, mode, trans, dur, ease, onProgress, onComplete));
        }

        private IEnumerator LoadRoutine(string name, int idx, LoadSceneMode mode, TransitionType trans,
            float dur, EaseType ease, Action<float> onProgress, Action onComplete)
        {
            _loading = true;
            string sceneName = name ?? $"BuildIndex:{idx}";
            Bill.Events?.Fire(new SceneLoadStartEvent { SceneName = sceneName });

            // === Transition IN (fade to black) ===
            if (trans != TransitionType.None && dur > 0f)
            {
                yield return CoroutineRunner.Instance.StartCoroutine(
                    _overlay.TransitionIn(trans, dur * 0.5f, ease));
            }

            // === Load scene ===
            AsyncOperation op = !string.IsNullOrEmpty(name)
                ? SceneManager.LoadSceneAsync(name, mode)
                : SceneManager.LoadSceneAsync(idx, mode);

            if (op == null)
            {
                Debug.LogError($"[Bill.Scene] Failed to load: {sceneName}");
                _loading = false;
                _overlay.ForceHide();
                yield break;
            }

            // Track progress
            while (!op.isDone)
            {
                onProgress?.Invoke(op.progress / 0.9f);
                Bill.Events?.Fire(new SceneLoadProgressEvent
                {
                    SceneName = sceneName,
                    Progress = Mathf.Clamp01(op.progress / 0.9f)
                });
                yield return null;
            }
            onProgress?.Invoke(1f);

            // Single mode clears additive list
            if (mode == LoadSceneMode.Single) _additiveScenes.Clear();

            // === Transition OUT (fade from black) ===
            if (trans != TransitionType.None && dur > 0f)
            {
                yield return CoroutineRunner.Instance.StartCoroutine(
                    _overlay.TransitionOut(trans, dur * 0.5f, ease));
            }

            _loading = false;
            onComplete?.Invoke();
            Bill.Events?.Fire(new SceneLoadCompleteEvent { SceneName = SceneManager.GetActiveScene().name });
        }

        // -------------------------------------------------------
        // Additive routines
        // -------------------------------------------------------

        private IEnumerator AdditiveLoadRoutine(string name, Action onComplete)
        {
            var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[Bill.Scene] Failed to load additive: {name}");
                yield break;
            }

            Bill.Events?.Fire(new SceneLoadStartEvent { SceneName = name });

            while (!op.isDone)
            {
                Bill.Events?.Fire(new SceneLoadProgressEvent { SceneName = name, Progress = op.progress / 0.9f });
                yield return null;
            }

            _additiveScenes.Add(name);
            onComplete?.Invoke();
            Bill.Events?.Fire(new SceneLoadCompleteEvent { SceneName = name });
        }

        private IEnumerator AdditiveUnloadRoutine(string name, Action onComplete)
        {
            var s = SceneManager.GetSceneByName(name);
            if (!s.isLoaded) { onComplete?.Invoke(); yield break; }

            var op = SceneManager.UnloadSceneAsync(s);
            if (op == null) { onComplete?.Invoke(); yield break; }

            while (!op.isDone) yield return null;

            onComplete?.Invoke();
        }

        // -------------------------------------------------------
        // Async with activation hold
        // -------------------------------------------------------

        private IEnumerator AsyncRoutine(string name, Action<float> onProgress, Action onComplete)
        {
            _loading = true;
            var op = SceneManager.LoadSceneAsync(name);
            if (op == null) { _loading = false; yield break; }

            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                float p = op.progress / 0.9f;
                onProgress?.Invoke(p);
                Bill.Events?.Fire(new SceneLoadProgressEvent { SceneName = name, Progress = p });
                yield return null;
            }

            onProgress?.Invoke(1f);
            op.allowSceneActivation = true;

            while (!op.isDone) yield return null;

            _loading = false;
            _additiveScenes.Clear();
            onComplete?.Invoke();
        }

        // -------------------------------------------------------
        // Cleanup
        // -------------------------------------------------------

        private void OnSceneUnloaded(Scene scene)
        {
            _additiveScenes.Remove(scene.name);
        }

        public void Cleanup()
        {
            _loading = false;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _additiveScenes.Clear();
            _overlay?.Destroy();
        }
    }

    // -------------------------------------------------------
    // Transition overlay - BillTween powered
    // -------------------------------------------------------

    internal class SceneTransitionOverlay
    {
        private readonly CanvasGroup _group;
        private readonly Canvas _canvas;
        private readonly GameObject _root;

        public SceneTransitionOverlay()
        {
            // Create overlay via Canvas (not UI Toolkit) for maximum compatibility
            _root = new GameObject("[Bill.Transition]");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999;

            _group = _root.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Full-screen black image
            var imgGo = new GameObject("Fade");
            imgGo.transform.SetParent(_root.transform, false);

            var rt = imgGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = imgGo.AddComponent<UnityEngine.UI.Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            _root.SetActive(false);
        }

        public IEnumerator TransitionIn(TransitionType type, float duration, EaseType ease)
        {
            _root.SetActive(true);
            _group.alpha = 0f;
            _group.blocksRaycasts = true;

            // Use BillTween for smooth eased fade
            bool done = false;
            var tween = BillTween.Float(0f, 1f, duration, v => _group.alpha = v)?
                .SetEase(ease)
                .SetUnscaled()
                .OnComplete(() => done = true);

            if (tween == null)
            {
                // Fallback if TweenService not ready
                _group.alpha = 1f;
                yield break;
            }

            while (!done) yield return null;
            _group.alpha = 1f;
        }

        public IEnumerator TransitionOut(TransitionType type, float duration, EaseType ease)
        {
            _group.alpha = 1f;

            bool done = false;
            var tween = BillTween.Float(1f, 0f, duration, v => _group.alpha = v)?
                .SetEase(ease)
                .SetUnscaled()
                .OnComplete(() => done = true);

            if (tween == null)
            {
                _group.alpha = 0f;
                _root.SetActive(false);
                yield break;
            }

            while (!done) yield return null;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _root.SetActive(false);
        }

        public void ForceHide()
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _root.SetActive(false);
        }

        public void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }

    // -------------------------------------------------------
    // New event
    // -------------------------------------------------------

    public struct SceneLoadProgressEvent : IEvent
    {
        public string SceneName;
        public float Progress;
    }
}
