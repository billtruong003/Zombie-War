using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BillGameCore
{
    /// <summary>
    /// Splash / startup screen for Bootstrap scene.
    /// Attach to a GameObject in Scene 0. Wire up UI references in the Inspector.
    ///
    /// Flow:
    ///   1. Logo scales from small -> default size (set in Editor)
    ///   2. Slider fills as loading steps execute
    ///   3. Status text shows current step
    ///   4. When all steps done -> fade out -> load next scene
    ///
    /// Usage:
    ///   var startup = FindObjectOfType&lt;BillStartup&gt;();
    ///   startup.AddStep("Load Config", () => { ... return true; });
    ///   startup.AddStepAsync("Connect DB", (log) => MyCoroutine(log));
    /// </summary>
    public class BillStartup : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Logo Image - set at default scale in Editor. Script shrinks it first then scales up.")]
        public Image logo;

        [Tooltip("Slider for progress (0-1). Style it however you want in Editor.")]
        public Slider progressSlider;

        [Tooltip("TMP text showing current loading step")]
        public TMP_Text statusText;

        [Tooltip("CanvasGroup on root for fade out at the end")]
        public CanvasGroup rootCanvasGroup;

        [Header("Settings")]
        [Tooltip("Scene to load after startup completes")]
        public string nextScene = "";

        [Tooltip("Transition type when loading next scene")]
        public TransitionType transition = TransitionType.Fade;

        [Tooltip("Transition duration in seconds")]
        public float transitionDuration = 0.5f;

        [Header("Logo Animation")]
        [Range(0f, 1f)]
        [Tooltip("Scale multiplier for initial small state (0.3 = 30% of default)")]
        public float logoStartScale = 0.3f;

        [Tooltip("Duration of logo scale-up animation")]
        public float logoScaleDuration = 0.8f;

        [Tooltip("Easing for logo animation")]
        public EaseType logoEase = EaseType.OutBack;

        [Tooltip("Hold logo visible for this long after scale completes")]
        public float logoHoldDuration = 1f;

        [Header("Fade Out")]
        [Tooltip("Duration of fade out before loading next scene")]
        public float fadeOutDuration = 0.5f;

        // -------------------------------------------------------
        // Step system
        // -------------------------------------------------------

        public delegate IEnumerator AsyncStep(Action<string> log);
        public delegate bool SyncStep();

        private struct LoadStep
        {
            public string Name;
            public SyncStep Sync;
            public AsyncStep Async;
            public bool IsAsync;
        }

        private readonly List<LoadStep> _steps = new(16);
        private bool _running;

        // -------------------------------------------------------
        // Public API
        // -------------------------------------------------------

        /// <summary>Add a synchronous step. Return true = success, false = fail (continues anyway).</summary>
        public void AddStep(string name, SyncStep action)
        {
            _steps.Add(new LoadStep { Name = name, Sync = action, IsAsync = false });
        }

        /// <summary>Add a synchronous step with simple Action (always succeeds).</summary>
        public void AddStep(string name, Action action)
        {
            _steps.Add(new LoadStep
            {
                Name = name,
                Sync = () => { action(); return true; },
                IsAsync = false
            });
        }

        /// <summary>Add an async step (coroutine). Receives a log callback for status updates.</summary>
        public void AddStepAsync(string name, AsyncStep action)
        {
            _steps.Add(new LoadStep { Name = name, Async = action, IsAsync = true });
        }

        // -------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------

        void Start()
        {
            if (!Bill.IsReady)
            {
                Bill.Events.Subscribe<GameReadyEvent>(OnGameReady);
                return;
            }
            Begin();
        }

        private void OnGameReady(GameReadyEvent _)
        {
            Bill.Events.Unsubscribe<GameReadyEvent>(OnGameReady);
            Begin();
        }

        private void Begin()
        {
            if (_running) return;
            _running = true;
            StartCoroutine(RunStartup());
        }

        // -------------------------------------------------------
        // Main startup routine
        // -------------------------------------------------------

        private IEnumerator RunStartup()
        {
            // --- Init UI ---
            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
                progressSlider.value = 0f;
                progressSlider.interactable = false;
            }
            SetStatus("");
            if (rootCanvasGroup != null) { rootCanvasGroup.alpha = 1f; rootCanvasGroup.blocksRaycasts = true; }

            // --- Logo animation ---
            yield return StartCoroutine(AnimateLogo());

            // --- Run loading steps ---
            if (_steps.Count > 0)
            {
                for (int i = 0; i < _steps.Count; i++)
                {
                    var step = _steps[i];

                    SetStatus(step.Name);
                    Debug.Log($"[BillStartup] > {step.Name}...");

                    if (step.IsAsync)
                    {
                        yield return StartCoroutine(step.Async(msg => Debug.Log($"[BillStartup]   {msg}")));
                    }
                    else
                    {
                        try
                        {
                            bool ok = step.Sync();
                            if (!ok) Debug.LogWarning($"[BillStartup] {step.Name} returned false");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[BillStartup] {step.Name}: {e.Message}");
                            Debug.LogException(e);
                        }
                    }

                    float progress = (float)(i + 1) / _steps.Count;
                    SetProgress(progress);
                    Debug.Log($"[BillStartup] {step.Name} done.");

                    yield return null;
                }
            }
            else
            {
                // No steps - animate slider fill
                float elapsed = 0f;
                float dur = 0.5f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    SetProgress(elapsed / dur);
                    yield return null;
                }
            }

            SetProgress(1f);
            SetStatus("Ready!");

            yield return new WaitForSeconds(0.3f);

            // --- Fade out ---
            yield return StartCoroutine(FadeOut());

            // --- Load next scene ---
            if (!string.IsNullOrEmpty(nextScene))
                Bill.Scene.Load(nextScene, transition, transitionDuration);
        }

        // -------------------------------------------------------
        // Logo animation
        // -------------------------------------------------------

        private IEnumerator AnimateLogo()
        {
            if (logo == null) yield break;

            Vector3 defaultScale = logo.transform.localScale;
            logo.transform.localScale = defaultScale * logoStartScale;
            logo.gameObject.SetActive(true);

            bool done = false;
            BillTween.Float(logoStartScale, 1f, logoScaleDuration, v =>
            {
                if (logo != null) logo.transform.localScale = defaultScale * v;
            })
            ?.SetEase(logoEase)
            .SetUnscaled()
            .OnComplete(() => done = true);

            while (!done) yield return null;

            if (logoHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(logoHoldDuration);
        }

        // -------------------------------------------------------
        // Fade out
        // -------------------------------------------------------

        private IEnumerator FadeOut()
        {
            if (rootCanvasGroup == null || fadeOutDuration <= 0f) yield break;

            bool done = false;
            BillTween.Fade(rootCanvasGroup, 0f, fadeOutDuration)
                ?.SetEase(EaseType.InQuad)
                .SetUnscaled()
                .OnComplete(() => done = true);

            while (!done) yield return null;
            rootCanvasGroup.blocksRaycasts = false;
        }

        // -------------------------------------------------------
        // UI helpers
        // -------------------------------------------------------

        private void SetProgress(float value)
        {
            if (progressSlider == null) return;
            // Smooth tween to target value
            BillTween.KillTarget(progressSlider);
            BillTween.Float(progressSlider.value, Mathf.Clamp01(value), 0.2f,
                v => { if (progressSlider != null) progressSlider.value = v; })
                ?.SetEase(EaseType.OutQuad)
                .SetTarget(progressSlider);
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }
    }
}
