using BillGameCore;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Backlog #9 (mockup U1 05/10): below a quarter of health the screen edge beats red like a
    /// heart, faster the closer the player is to death. It says "get out" without a number.
    /// Sits on a full-screen image behind every HUD widget; never takes a tap.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class LowHealthVignette : MonoBehaviour
    {
        public const float Threshold = 0.25f;
        [SerializeField] private float maxAlpha = 0.85f;   // linear-space UI: a mockup 0.7 reads weak in game
        [SerializeField] private float fadeSeconds = 0.35f;

        Image _image;
        float _health01 = 1f, _intensity;

        void Awake()
        {
            _image = GetComponent<Image>();
            _image.raycastTarget = false;
            // Its own canvas, and the beat through the renderer alpha: a colour write every frame
            // rebuilt the HUD's single canvas for as long as health was low (07/10 audit).
            if (!TryGetComponent<Canvas>(out _)) gameObject.AddComponent<Canvas>();
            var c = _image.color; c.a = 1f; _image.color = c;
            Apply(0f);
        }

        void OnEnable()
        {
            _health01 = 1f; _intensity = 0f; Apply(0f);
            Bill.Events?.Subscribe<PlayerHealthChangedEvent>(OnHealth);
        }

        void OnDisable() => Bill.Events?.Unsubscribe<PlayerHealthChangedEvent>(OnHealth);

        void OnHealth(PlayerHealthChangedEvent e) => _health01 = e.Normalized;

        void Update()
        {
            bool on = _health01 > 0f && _health01 < Threshold;
            _intensity = Mathf.MoveTowards(_intensity, on ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
            Apply(_intensity <= 0f ? 0f : _intensity * maxAlpha * Beat(Time.time, _health01));
        }

        void Apply(float alpha)
        {
            if (_image == null) return;
            _image.enabled = alpha > 0.001f;
            _image.canvasRenderer.SetAlpha(alpha);
        }

        /// <summary>Beats per minute: 70 at the threshold, 130 at the edge of death.</summary>
        public static float Bpm(float health01) => Mathf.Lerp(130f, 70f, Mathf.Clamp01(health01 / Threshold));

        /// <summary>
        /// 0.35..1 brightness over one heartbeat: a strong thump and a softer second one ("lub-dub"),
        /// then a rest, so it reads as a pulse and not a flicker.
        /// </summary>
        public static float Beat(float time, float health01)
        {
            float phase = Mathf.Repeat(time * Bpm(health01) / 60f, 1f);
            float pulse = Thump(phase) + 0.6f * Thump(phase - 0.18f);
            return 0.35f + 0.65f * Mathf.Clamp01(pulse);
        }

        static float Thump(float x) => x < -0.05f ? 0f : Mathf.Exp(-(x / 0.07f) * (x / 0.07f));
    }
}
