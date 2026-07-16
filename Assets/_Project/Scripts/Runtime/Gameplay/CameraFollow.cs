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
        [SerializeField] private float traumaDecayPerSecond = 1.5f;
        [SerializeField] private float maxShakeOffset = 0.5f;
        [SerializeField] private float shakeFrequency = 25f;

        private Vector3 _velocity;
        private float _trauma;
        private float _shakeSeedX;
        private float _shakeSeedY;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void Awake()
        {
            transform.rotation = Quaternion.Euler(lookEulerAngles);
            _shakeSeedX = Random.value * 100f;
            _shakeSeedY = Random.value * 100f;
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
            float x = (Mathf.PerlinNoise(_shakeSeedX, Time.time * shakeFrequency) * 2f - 1f) * maxShakeOffset * shake;
            float y = (Mathf.PerlinNoise(_shakeSeedY, Time.time * shakeFrequency) * 2f - 1f) * maxShakeOffset * shake;
            return new Vector3(x, y, 0f);
        }
    }
}
