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

        Material _wave;
        float _shown;
        bool _typing;
        static readonly int SpeakingId = Shader.PropertyToID("_Speaking");

        void OnEnable()
        {
            // An own instance, so two cards on screen do not share one "speaking" state.
            if (waveform != null && waveformMaterial != null)
            {
                _wave = new Material(waveformMaterial) { hideFlags = HideFlags.DontSave };
                waveform.material = _wave;
            }
            SetSpeaking(_typing ? 1 : 0);
        }

        void OnDisable()
        {
            if (_wave == null) return;
            if (waveform != null) waveform.material = waveformMaterial;
            if (Application.isPlaying) Destroy(_wave); else DestroyImmediate(_wave);
            _wave = null;
        }

        /// Shows a line. In Play the body types in; in the editor it appears at once.
        public void Say(string channelText, string titleText, string bodyText, Sprite face = null)
        {
            if (channel != null) channel.text = channelText;
            if (title != null) title.text = titleText;
            if (face != null && portrait != null) portrait.sprite = face;
            if (body == null) return;
            body.text = bodyText;
            _typing = Application.isPlaying;
            _shown = 0;
            body.maxVisibleCharacters = _typing ? 0 : int.MaxValue;
            SetSpeaking(_typing ? 1 : 0);
        }

        void Update()
        {
            if (!_typing || body == null) return;
            _shown += Time.unscaledDeltaTime * charsPerSecond;
            int total = body.textInfo.characterCount > 0 ? body.textInfo.characterCount : body.text.Length;
            body.maxVisibleCharacters = Mathf.FloorToInt(_shown);
            if (_shown < total) return;
            _typing = false;
            body.maxVisibleCharacters = int.MaxValue;
            SetSpeaking(0);
        }

        void SetSpeaking(float v)
        {
            if (_wave != null) _wave.SetFloat(SpeakingId, v);
        }
    }
}
