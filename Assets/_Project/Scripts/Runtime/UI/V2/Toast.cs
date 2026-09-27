using System.Collections;
using TMPro;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 one-line message over every screen ("Unlocks at level 3", "Not enough coins"). Lives as
    /// the last child of UIRoot so it draws above any screen; <see cref="Show"/> is static.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class Toast : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        static Toast _instance;
        CanvasGroup _group;
        Coroutine _run;

        private void Awake()
        {
            _instance = this;
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        public static void Show(string message, float seconds = 1.8f)
        {
            // The toast lives in the Menu scene, which is switched off during a run.
            if (_instance == null || !_instance.isActiveAndEnabled) { Debug.Log("[Toast] " + message); return; }
            _instance.Play(message, seconds);
        }

        void Play(string message, float seconds)
        {
            if (label != null) label.text = message;
            transform.SetAsLastSibling();
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Run(seconds));
        }

        IEnumerator Run(float seconds)
        {
            for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime) { _group.alpha = t / 0.15f; yield return null; }
            _group.alpha = 1f;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { _group.alpha = 1f - t / 0.25f; yield return null; }
            _group.alpha = 0f;
            _run = null;
        }
    }
}
