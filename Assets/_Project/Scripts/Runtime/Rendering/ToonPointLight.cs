using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// A point light of the game's own toon lighting (no URP Light): colour, intensity and range,
    /// with an optional flicker for fire and lava. <see cref="ToonPointLights"/> decides each frame
    /// whether it is among the lights the shaders evaluate.
    /// </summary>
    public class ToonPointLight : MonoBehaviour
    {
        public Color colour = new Color(1f, 0.7f, 0.4f);
        [Min(0f)] public float intensity = 1f;
        [Min(0.1f)] public float range = 5f;
        [Tooltip("0 = steady; fire and lava ~0.25.")]
        [Range(0f, 1f)] public float flicker;
        public float flickerSpeed = 7f;
        [Tooltip("Lights with a higher priority win a place in the lit set over nearer ones.")]
        public float priority;
        public Vector3 offset;

        public Vector3 Position => transform.position + offset;

        public float FlickeredIntensity(float time)
        {
            if (flicker <= 0f) return intensity;
            float n = Mathf.PerlinNoise(time * flickerSpeed, GetInstanceID() * 0.137f);
            return intensity * (1f - flicker + flicker * 2f * n);
        }

        void OnEnable() => ToonPointLights.Register(this);
        void OnDisable() => ToonPointLights.Unregister(this);
    }
}
