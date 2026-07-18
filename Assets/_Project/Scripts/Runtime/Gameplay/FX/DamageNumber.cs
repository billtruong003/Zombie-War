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

        private void Awake() => CacheRefs();

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

            _text.text = Mathf.Max(1, Mathf.RoundToInt(amount)).ToString();
            _text.color = crit ? critColor : normalColor;
            _text.alpha = 1f;

            _targetScale = crit ? critScale : baseScale;

            float sign = Random.value < 0.5f ? -1f : 1f;
            _velocity = new Vector3(sign * horizontalDrift, riseSpeed, 0f);

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
            _text.alpha = Mathf.Clamp01(Mathf.InverseLerp(lifetime, lifetime * 0.5f, _elapsed));

            FaceCamera();

            if (_elapsed >= lifetime)
                ReturnToPool();
        }

        private void FaceCamera()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
                _tf.forward = _cam.transform.forward;
        }
    }
}
