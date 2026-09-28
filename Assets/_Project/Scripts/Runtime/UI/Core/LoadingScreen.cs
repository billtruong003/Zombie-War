using System.Collections;
using BillGameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// The Bootstrap splash canvas, reused as the loading screen between Hub and the world (M8 P3).
    ///
    /// Before, PLAY left the Hub on screen and the app froze for several seconds while the world
    /// loaded and warmed its pools. Now the splash comes back over everything with a tip and a bar:
    /// the bar eases toward 90% while the scene loads and fills when the world is ready, then the
    /// splash fades out. The splash canvas lives in Bootstrap.unity, which is never unloaded.
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Slider progress;
        [SerializeField] private TMP_Text status;
        [Tooltip("Key art behind the logo. The first shows at boot; later loads rotate through all.")]
        [SerializeField] private Image background;
        [SerializeField] private Sprite[] backgrounds;

        [Tooltip("One is picked at random for each load.")]
        [SerializeField, TextArea] private string[] tips =
        {
            "Keep moving. Standing still lets the horde close in.",
            "Max a power while you hold its partner card to unlock its evolution.",
            "Every gun brings two cards of its own into the run. See them in Loadout.",
            "Surges come in waves. Clear a path before the next one hits.",
        };

        public static LoadingScreen Instance { get; private set; }

        private float _target;
        private float _shown;
        private bool _loading;
        private Coroutine _routine;

        private int _art;

        private void Awake()
        {
            Instance = this;
            ShowArt(0);
        }

        private void ShowArt(int index)
        {
            if (background == null || backgrounds == null || backgrounds.Length == 0) return;
            _art = ((index % backgrounds.Length) + backgrounds.Length) % backgrounds.Length;
            if (backgrounds[_art] != null) background.sprite = backgrounds[_art];
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// Covers the screen at once (no fade in: the frame after PLAY must already be the loader).
        public static void Begin()
        {
            var s = Instance;
            if (s == null) return;
            s.StopRoutine();
            if (s.group != null) BillTween.KillTarget(s.group);   // the boot splash's own fade-out
            s._loading = true;
            s._target = 0.9f;
            s._shown = 0f;
            s.ShowArt(s._art + 1);   // a different key art each load
            if (s.progress != null) { s.progress.gameObject.SetActive(true); s.progress.value = 0f; }
            if (s.status != null && s.tips != null && s.tips.Length > 0) s.status.text = s.tips[Random.Range(0, s.tips.Length)];
            if (s.group != null) { s.group.alpha = 1f; s.group.blocksRaycasts = true; s.group.interactable = true; }
            s._routine = s.StartCoroutine(s.Run());
        }

        /// The world is ready: fill the bar, hold a beat, fade out.
        public static void Complete()
        {
            var s = Instance;
            if (s == null || !s._loading) return;
            s._loading = false;
            s._target = 1f;
        }

        private IEnumerator Run()
        {
            // Unscaled: the world may start paused, and the load hitch can make one frame very long.
            while (_loading || _shown < 0.999f)
            {
                // While loading, one long hitch frame must not jump the bar; once the world is ready
                // the bar finishes in real time (the first frames after a scene load are slow).
                float dt = _loading ? Mathf.Min(Time.unscaledDeltaTime, 0.05f) : Time.unscaledDeltaTime;
                float speed = _loading ? Mathf.Lerp(1.2f, 0.08f, _shown / 0.9f) : 4f;
                _shown = Mathf.MoveTowards(_shown, _target, speed * dt);
                if (progress != null) progress.value = _shown;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.2f);
            if (group != null)
            {
                bool done = false;
                var tw = BillTween.Fade(group, 0f, 0.35f)?.SetEase(EaseType.InQuad).SetUnscaled().OnComplete(() => done = true);
                if (tw == null) group.alpha = 0f;
                else while (!done) yield return null;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            _routine = null;
        }

        private void StopRoutine()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }
    }
}
