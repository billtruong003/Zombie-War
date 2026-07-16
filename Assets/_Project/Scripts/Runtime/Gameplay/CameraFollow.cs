using UnityEngine;

namespace ZombieWar
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);
        [SerializeField] private Vector3 lookEulerAngles = new Vector3(60f, 0f, 0f);
        [SerializeField] private float smoothTime = 0.15f;

        [Header("Shake")]
        [SerializeField] private Texture2D noiseTexture;
        [SerializeField] private float traumaDecayPerSecond = 1.5f;
        [SerializeField] private float maxShakeOffset = 0.5f;
        [SerializeField] private float shakeFrequency = 25f;

        private Vector3 _velocity;
        private float _trauma;
        private float _noiseSeed;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void Awake()
        {
            transform.rotation = Quaternion.Euler(lookEulerAngles);
            _noiseSeed = Random.value * 100f;
        }

        public void Shake(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, smoothTime);

            if (_trauma > 0f)
            {
                transform.position += GetShakeOffset();
                _trauma = Mathf.Max(0f, _trauma - traumaDecayPerSecond * Time.deltaTime);
            }
        }

        // Squared falloff reads as more natural than linear - shake tapers off quickly near zero instead of lingering.
        private Vector3 GetShakeOffset()
        {
            float shake = _trauma * _trauma;
            Vector2 noise = NoiseTextureSampler.Sample(noiseTexture, Time.time * shakeFrequency, _noiseSeed);
            return new Vector3(noise.x * maxShakeOffset * shake, noise.y * maxShakeOffset * shake, 0f);
        }
    }
}
