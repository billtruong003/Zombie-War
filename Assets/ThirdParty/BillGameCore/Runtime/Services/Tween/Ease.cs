using System.Runtime.CompilerServices;
using UnityEngine;

namespace BillGameCore
{
    public enum EaseType : byte
    {
        Linear,
        InSine, OutSine, InOutSine,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        InQuart, OutQuart, InOutQuart,
        InQuint, OutQuint, InOutQuint,
        InExpo, OutExpo, InOutExpo,
        InCirc, OutCirc, InOutCirc,
        InBack, OutBack, InOutBack,
        InElastic, OutElastic, InOutElastic,
        InBounce, OutBounce, InOutBounce,
    }

    public static class Ease
    {
        private const float PI = Mathf.PI;
        private const float HALF_PI = Mathf.PI * 0.5f;
        private const float B1 = 1.70158f;
        private const float B2 = B1 * 1.525f;
        private const float E1 = 2f * PI / 3f;
        private const float E2 = 2f * PI / 4.5f;
        private const float N1 = 7.5625f;
        private const float D1 = 2.75f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Evaluate(EaseType type, float t)
        {
            switch (type)
            {
                case EaseType.Linear: return t;

                // Sine
                case EaseType.InSine: return 1f - Cos(t * HALF_PI);
                case EaseType.OutSine: return Sin(t * HALF_PI);
                case EaseType.InOutSine: return -(Cos(PI * t) - 1f) * 0.5f;

                // Quad
                case EaseType.InQuad: return t * t;
                case EaseType.OutQuad: return 1f - (1f - t) * (1f - t);
                case EaseType.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Pow(-2f * t + 2f, 2) * 0.5f;

                // Cubic
                case EaseType.InCubic: return t * t * t;
                case EaseType.OutCubic: return 1f - Pow(1f - t, 3);
                case EaseType.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Pow(-2f * t + 2f, 3) * 0.5f;

                // Quart
                case EaseType.InQuart: return t * t * t * t;
                case EaseType.OutQuart: return 1f - Pow(1f - t, 4);
                case EaseType.InOutQuart: return t < 0.5f ? 8f * t * t * t * t : 1f - Pow(-2f * t + 2f, 4) * 0.5f;

                // Quint
                case EaseType.InQuint: return t * t * t * t * t;
                case EaseType.OutQuint: return 1f - Pow(1f - t, 5);
                case EaseType.InOutQuint: return t < 0.5f ? 16f * t * t * t * t * t : 1f - Pow(-2f * t + 2f, 5) * 0.5f;

                // Expo
                case EaseType.InExpo: return t <= 0f ? 0f : Pow(2f, 10f * t - 10f);
                case EaseType.OutExpo: return t >= 1f ? 1f : 1f - Pow(2f, -10f * t);
                case EaseType.InOutExpo:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f ? Pow(2f, 20f * t - 10f) * 0.5f : (2f - Pow(2f, -20f * t + 10f)) * 0.5f;

                // Circ
                case EaseType.InCirc: return 1f - Sqrt(1f - t * t);
                case EaseType.OutCirc: return Sqrt(1f - (t - 1f) * (t - 1f));
                case EaseType.InOutCirc:
                    return t < 0.5f
                        ? (1f - Sqrt(1f - 4f * t * t)) * 0.5f
                        : (Sqrt(1f - Pow(-2f * t + 2f, 2)) + 1f) * 0.5f;

                // Back
                case EaseType.InBack: return (B1 + 1f) * t * t * t - B1 * t * t;
                case EaseType.OutBack: { float u = t - 1f; return 1f + (B1 + 1f) * u * u * u + B1 * u * u; }
                case EaseType.InOutBack:
                    return t < 0.5f
                        ? 4f * t * t * ((B2 + 1f) * 2f * t - B2) * 0.5f
                        : (Pow(2f * t - 2f, 2) * ((B2 + 1f) * (2f * t - 2f) + B2) + 2f) * 0.5f;

                // Elastic
                case EaseType.InElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return -Pow(2f, 10f * t - 10f) * Sin((t * 10f - 10.75f) * E1);
                case EaseType.OutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return Pow(2f, -10f * t) * Sin((t * 10f - 0.75f) * E1) + 1f;
                case EaseType.InOutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f
                        ? -(Pow(2f, 20f * t - 10f) * Sin((20f * t - 11.125f) * E2)) * 0.5f
                        : Pow(2f, -20f * t + 10f) * Sin((20f * t - 11.125f) * E2) * 0.5f + 1f;

                // Bounce
                case EaseType.InBounce: return 1f - BounceOut(1f - t);
                case EaseType.OutBounce: return BounceOut(t);
                case EaseType.InOutBounce:
                    return t < 0.5f
                        ? (1f - BounceOut(1f - 2f * t)) * 0.5f
                        : (1f + BounceOut(2f * t - 1f)) * 0.5f;

                default: return t;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float BounceOut(float t)
        {
            if (t < 1f / D1) return N1 * t * t;
            if (t < 2f / D1) { t -= 1.5f / D1; return N1 * t * t + 0.75f; }
            if (t < 2.5f / D1) { t -= 2.25f / D1; return N1 * t * t + 0.9375f; }
            t -= 2.625f / D1; return N1 * t * t + 0.984375f;
        }

        // Inline math to avoid Mathf overhead
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sin(float x) => Mathf.Sin(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Cos(float x) => Mathf.Cos(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Pow(float b, float e) => Mathf.Pow(b, e);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sqrt(float x) => Mathf.Sqrt(x);
    }
}
