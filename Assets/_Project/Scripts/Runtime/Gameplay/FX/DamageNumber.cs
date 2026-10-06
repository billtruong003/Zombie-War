using UnityEngine;
using TMPro;
using BillGameCore;

namespace ZombieWar
{
    /// <summary>
    /// A pooled, world-space damage number. Spawned through <see cref="DamageNumberSpawner"/>
    /// (Bill.Pool), it floats up, pops in, fades out, then returns itself to the pool - so we
    /// never churn Instantiate/Destroy per hit.
    ///
    /// Uses a 3D <see cref="TextMeshPro"/> (mesh renderer, NOT the UGUI variant) so the number
    /// lives in the scene next to the zombie instead of a screen-space canvas. It billboards to
    /// the active camera each frame so it always reads face-on.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageNumber : PooledObject
    {
        [Header("Lifetime")]
        [Tooltip("Seconds from spawn to auto-return.")]
        [SerializeField] private float lifetime = 0.8f;

        [Header("Motion")]
        [Tooltip("Upward world speed at spawn (units/sec); eases off over the life.")]
        [SerializeField] private float riseSpeed = 1.6f;
        [Tooltip("Random sideways drift so stacked hits fan out instead of overlapping.")]
        [SerializeField] private float horizontalDrift = 0.4f;

        [Header("Scale")]
        [SerializeField] private float baseScale = 1f;
        [Tooltip("Scale used when Show(..., crit:true). No crit rules exist yet - hook only.")]
        [SerializeField] private float critScale = 1.6f;
        [Tooltip("Seconds spent easing from 0 to full scale (the pop-in).")]
        [SerializeField] private float popInDuration = 0.12f;

        [Header("Color")]
        [SerializeField] private Color normalColor = new Color(1f, 0.95f, 0.5f);   // soft yellow
        [Tooltip("Color used for crits. Hook only until a crit system exists.")]
        [SerializeField] private Color critColor = new Color(1f, 0.4f, 0.15f);     // hot orange

        private TextMeshPro _text;
        private Transform _tf;
        private Camera _cam;
        private float _elapsed;
        private float _targetScale;
        private Vector3 _velocity;
        private byte _alpha = 255;

        /// <summary>Numbers on screen now; the spawner caps it.</summary>
        public static int Live { get; private set; }

        private void Awake() => CacheRefs();
        private void OnEnable() => Live++;
        private void OnDisable() => Live--;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Live = 0;

        private void CacheRefs()
        {
            _tf = transform;
            if (_text == null) _text = GetComponent<TextMeshPro>();
        }

        public override void OnSpawnedFromPool() => _elapsed = 0f;

        /// <summary>Configure and (re)start the popup. <paramref name="crit"/> is plumbed for a
        /// future crit system; callers pass false today because no crit rules exist yet.</summary>
        public void Show(float amount, bool crit)
        {
            CacheRefs();
            _elapsed = 0f;

            _text.SetText("{0}", Mathf.Max(1, Mathf.RoundToInt(amount)));   // no string per hit
            _text.color = crit ? critColor : normalColor;
            _text.alpha = 1f;
            _alpha = 255;

            _targetScale = crit ? critScale : baseScale;

            float sign = Random.value < 0.5f ? -1f : 1f;
            _velocity = new Vector3(sign * horizontalDrift, riseSpeed, 0f);

            _tf.localScale = Vector3.zero;
            FaceCamera();
        }

        /// <summary>05/10: a word instead of a number - what the player just picked up ("+20 HP",
        /// "MAGNET"), bigger and slower so it reads over the chaos.</summary>
        public void ShowLabel(string label, Color color)
        {
            CacheRefs();
            _elapsed = 0f;
            _text.SetText(label);
            _text.color = color;
            _text.alpha = 1f;
            _alpha = 255;
            _targetScale = critScale * 1.15f;
            _velocity = new Vector3(0f, riseSpeed * 0.8f, 0f);
            _tf.localScale = Vector3.zero;
            FaceCamera();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _elapsed += dt;

            // Rise, easing the vertical speed off so it decelerates near the top.
            _tf.position += _velocity * dt;
            _velocity.y = Mathf.Max(0f, _velocity.y - dt * riseSpeed * 0.5f);

            // Pop in, then hold at the target scale.
            float popT = popInDuration > 0f ? Mathf.Clamp01(_elapsed / popInDuration) : 1f;
            float s = Mathf.SmoothStep(0f, 1f, popT) * _targetScale;
            _tf.localScale = new Vector3(s, s, s);

            // Full opacity for the first half of life, then linearly fade to zero.
            FadeTo(Mathf.Clamp01(Mathf.InverseLerp(lifetime, lifetime * 0.5f, _elapsed)));

            FaceCamera();

            if (_elapsed >= lifetime)
                ReturnToPool();
        }

        // The fade rewrites the alpha of the vertex colours already built. Setting TMP_Text.alpha
        // re-parsed and rebuilt the whole text mesh every frame of the fade - hundreds of numbers at
        // once in a horde (07/10 audit).
        private void FadeTo(float a)
        {
            byte b = (byte)(a * 255f + 0.5f);
            if (b == _alpha) return;
            _alpha = b;
            var info = _text.textInfo;
            if (info == null || info.meshInfo == null) return;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var colors = info.meshInfo[m].colors32;
                if (colors == null) continue;
                int n = Mathf.Min(info.meshInfo[m].vertexCount, colors.Length);
                for (int i = 0; i < n; i++) colors[i].a = b;
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private void FaceCamera()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
                _tf.forward = _cam.transform.forward;
        }
    }
}
