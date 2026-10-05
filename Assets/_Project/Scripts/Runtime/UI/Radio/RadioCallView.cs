using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// The radio card an agent speaks through (FTUE and in-run chatter): hologram portrait, channel
    /// label with "speaking" bars, a yellow title and a body that types itself in. While the body is
    /// typing the waveform moves; it settles when the line is done.
    public sealed class RadioCallView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text channel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Graphic waveform;
        [SerializeField] private Material waveformMaterial;
        [SerializeField] private float charsPerSecond = 38f;
        [Tooltip("Body line spacing (TMP em/100). Cairo's line height is ~1.9x its size, which spread a " +
                 "two-line body like two paragraphs.")]
        [SerializeField] private float bodyLineSpacing = -36f;

        /// Typing speed of the body; the radio overlay sets it so the text keeps pace with the voice.
        public float CharsPerSecond { get => charsPerSecond; set => charsPerSecond = value; }

        RectTransform _rect;
        float _baseHeight, _bodyBaseHeight;
        Vector2 _bodyBaseOffsetMin;
        bool _measured;
        Material _wave;
        float _shown, _speaking = -1f;
        bool _typing;

        /// <summary>The voice line this card shows (its waveform follows that line's loudness).</summary>
        public string VoiceId { get; private set; }

        /// <summary>True while the card waits for its voice to start before typing.</summary>
        public bool WaitingForVoice { get; private set; }
        static readonly int SpeakingId = Shader.PropertyToID("_Speaking");

        void OnEnable()
        {
            // Poking the portrait is an easter egg (Ji-ho at 5 taps, Mai at 10).
            if (portrait != null && !portrait.TryGetComponent(out RadioPortraitTaps _))
            {
                portrait.raycastTarget = true;
                portrait.gameObject.AddComponent<RadioPortraitTaps>();
            }
            // An own instance, so two cards on screen do not share one "speaking" state.
            if (waveform != null && waveformMaterial != null)
            {
                _wave = new Material(waveformMaterial) { hideFlags = HideFlags.DontSave };
                waveform.material = _wave;
            }
            _speaking = -1f;   // re-applied on the next Update
        }

        void OnDisable()
        {
            if (_wave == null) return;
            if (waveform != null) waveform.material = waveformMaterial;
            if (Application.isPlaying) Destroy(_wave); else DestroyImmediate(_wave);
            _wave = null;
        }

        void Measure()
        {
            if (_measured || body == null) return;
            _measured = true;
            _rect = (RectTransform)transform;
            _baseHeight = _rect.rect.height;
            _bodyBaseHeight = body.rectTransform.rect.height;
            _bodyBaseOffsetMin = body.rectTransform.offsetMin;
        }

        /// The card grows by whatever the body needs beyond its authored box, so a two-line body is
        /// never cut off at the card's bottom edge (QA 04/10: the first-chest card).
        void FitBody()
        {
            Measure();
            body.lineSpacing = bodyLineSpacing;
            body.ForceMeshUpdate();
            float extra = Mathf.Max(0f, body.preferredHeight - _bodyBaseHeight);
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _baseHeight + extra);
            body.rectTransform.offsetMin = new Vector2(_bodyBaseOffsetMin.x, _bodyBaseOffsetMin.y - extra);
        }

        /// Shows a line. In Play the body types in; in the editor it appears at once. With
        /// <paramref name="waitForVoice"/> the title shows now and the body waits for
        /// <see cref="StartTyping"/> (called when the card's voice line starts), so text and voice
        /// begin together.
        public void Say(string channelText, string titleText, string bodyText, Sprite face = null,
                        string voiceId = null, bool waitForVoice = false)
        {
            VoiceId = voiceId;
            if (channel != null) channel.text = channelText;
            if (title != null) title.text = titleText;
            if (face != null && portrait != null) portrait.sprite = face;
            if (body == null) return;
            body.text = bodyText;
            FitBody();
            WaitingForVoice = waitForVoice && Application.isPlaying;
            _typing = Application.isPlaying && !WaitingForVoice;
            _shown = 0;
            body.maxVisibleCharacters = Application.isPlaying ? 0 : int.MaxValue;
        }

        /// <summary>Starts typing the body at <paramref name="cps"/> characters a second.</summary>
        public void StartTyping(float cps)
        {
            if (body == null || !Application.isPlaying) return;
            WaitingForVoice = false;
            charsPerSecond = cps;
            _shown = 0;
            _typing = true;
        }

        void Update()
        {
            // The bars follow the card's own voice; a line without a voice keeps the old "typing" motion.
            float speak = VoiceId != null && ZombieWar.Audio.RadioVoice.PlayingId == VoiceId ? ZombieWar.Audio.RadioVoice.Level
                        : _typing ? 1f : 0f;
            if (Mathf.Abs(speak - _speaking) > 0.01f || (speak == 0f && _speaking != 0f)) SetSpeaking(speak);

            if (!_typing || body == null) return;
            _shown += Time.unscaledDeltaTime * charsPerSecond;
            int total = body.textInfo.characterCount > 0 ? body.textInfo.characterCount : body.text.Length;
            body.maxVisibleCharacters = Mathf.FloorToInt(_shown);
            if (_shown < total) return;
            _typing = false;
            body.maxVisibleCharacters = int.MaxValue;
        }

        void SetSpeaking(float v)
        {
            _speaking = v;
            if (_wave != null) _wave.SetFloat(SpeakingId, v);
            // The card sits under a Mask: uGUI renders a stencil copy of the material, made once, so
            // a value set on _wave alone never reached the screen (04/10: the bars never moved).
            if (waveform != null)
            {
                var rendered = waveform.materialForRendering;
                if (rendered != null && rendered != _wave) rendered.SetFloat(SpeakingId, v);
            }
        }
    }
}
