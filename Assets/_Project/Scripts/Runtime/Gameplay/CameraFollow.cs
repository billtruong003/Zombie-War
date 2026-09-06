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
        [Tooltip("UNUSED since M7.3d. Camera shake is procedural Perlin; this texture was Crunch-" +
                 "compressed, which made GetPixelBilinear invalid and spammed a warning every frame. " +
                 "Kept as a field only so existing prefab/scene references do not break.")]
        [SerializeField] private Texture2D noiseTexture;
        [SerializeField] private float traumaDecayPerSecond = 1.5f;
        [SerializeField] private float maxShakeOffset = 0.5f;
        [SerializeField] private float shakeFrequency = 25f;

        private Vector3 _velocity;
        private Vector3 _basePosition;
        private bool _hasBase;
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
            ApplyOcclusionPolicy();
        }

        /// <summary>
        /// Tắt Occlusion Culling trên camera gameplay, bằng CODE chứ không phải bằng một ô tick trong scene.
        ///
        /// Đây là nguyên nhân THẬT của lỗi mất mặt đất (chủ dự án tái hiện được): thế giới này là
        /// procedural và tái dụng chunk root liên tục, nên một renderer vừa được cấp cho toạ độ mới có
        /// thể bị loại bằng dữ liệu occlusion bake từ bố cục scene cũ — mặt đất biến mất trong khi
        /// lease, bounds và renderer đều đúng.
        ///
        /// Frustum culling KHÔNG bị ảnh hưởng và vẫn chạy bình thường; chỉ phần dựa vào dữ liệu bake
        /// bị bỏ. Đặt ở đây để chính sách này không thể bị một lần lưu scene vô ý bật lại, và để nó
        /// đúng cho mọi map dùng camera bám người chơi.
        /// </summary>
        private void ApplyOcclusionPolicy()
        {
            var cam = GetComponent<Camera>();
            if (cam != null) cam.useOcclusionCulling = false;
        }

        public void Shake(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        // ONE authoritative base pose, with shake composed on top of it each frame.
        //
        // The previous version added the shake offset INTO transform.position after SmoothDamp, so
        // the next frame's damping started from a contaminated pose. Under sustained fire the noise
        // forcing turned SmoothDamp's internal velocity into a runaway (measured live: 76 m of
        // drift in 15 s of automatic fire, and 150 m -> 654 m while PAUSED, because a frozen
        // Time.time froze the noise into a constant vector that kept being re-added every frame).
        // Composing from _basePosition makes the displacement bounded by construction: the transform
        // can never sit further than maxShakeOffset from the follow pose, and pause cannot
        // accumulate anything because the base is recomputed, not incremented.
        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;
            if (!_hasBase)
            {
                _basePosition = desiredPosition;
                _hasBase = true;
            }
            _basePosition = Vector3.SmoothDamp(_basePosition, desiredPosition, ref _velocity, smoothTime);

            Vector3 shakeOffset = Vector3.zero;
            if (_trauma > 0f)
            {
                shakeOffset = GetShakeOffset();
                // Unscaled decay, deliberately: a shake that lands on the same frame as a pause
                // (bomb + level-up) must die out during the pause instead of freezing at full
                // trauma and popping when the game resumes.
                _trauma = Mathf.Max(0f, _trauma - traumaDecayPerSecond * Time.unscaledDeltaTime);
            }

            transform.position = _basePosition + shakeOffset;
        }

        // Squared falloff reads as more natural than linear - shake tapers off quickly near zero instead of lingering.
        private Vector3 GetShakeOffset()
        {
            float shake = _trauma * _trauma;
            // Unscaled time keeps the noise animating while paused (scaled Time.time freezes, which
            // would hold one constant offset for the whole pause).
            float noiseTime = Time.unscaledTime * shakeFrequency;

            // M7.3d — the texture path is GONE, procedural Perlin is now the only source.
            //
            // The assigned noise texture is Crunch-compressed, so GetPixelBilinear on it is invalid:
            // it logged a warning EVERY LateUpdate and returned garbage. The old Perlin fallback did
            // not rescue it either — that only triggered on an exact Vector2.zero, and garbage is not
            // zero, so shake was being driven by junk while spamming the console.
            //
            // Perlin wins on merit regardless: allocation-free, deterministic from the seed, needs no
            // asset, and the texture was buying nothing a gradient noise function does not.
            var noise = new Vector2(
                Mathf.PerlinNoise(_noiseSeed, noiseTime) * 2f - 1f,
                Mathf.PerlinNoise(_noiseSeed + 37f, noiseTime) * 2f - 1f);

            return new Vector3(noise.x * maxShakeOffset * shake, noise.y * maxShakeOffset * shake, 0f);
        }
    }
}
