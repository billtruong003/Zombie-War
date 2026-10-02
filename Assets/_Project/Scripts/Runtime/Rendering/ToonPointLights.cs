using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// The game's point lights (2026-10-02), its own rather than URP lights: no Light components, no
    /// Forward+ light loop, no shadow maps. Every frame the CPU culls the lights against the view
    /// (a sphere test against the six frustum planes), ranks the rest by distance to the hero and
    /// hands the best <see cref="GraphicsTier.PointLights"/> to the toon shaders as two small global
    /// arrays (ToonPointLights.hlsl). A light entering or leaving the lit set fades over
    /// <see cref="FadeSeconds"/> so none pops. Short flashes (muzzle, explosions) come from a fixed
    /// pool. Nothing allocates per frame.
    /// </summary>
    public static class ToonPointLights
    {
        public const int MaxLights = 16;          // ZW_MAX_POINT_LIGHTS
        public const float Falloff = 0.55f;       // lower = brighter further out
        public const float FadeSeconds = 0.25f;
        const int FlashPool = 32;

        static readonly int PositionsId = Shader.PropertyToID("_ZWPointLightPos");
        static readonly int ColoursId = Shader.PropertyToID("_ZWPointLightColor");
        static readonly int CountId = Shader.PropertyToID("_ZWPointLightCount");
        static readonly int FalloffId = Shader.PropertyToID("_ZWPointFalloff");

        sealed class Entry
        {
            public ToonPointLight light;          // null for a flash
            public Vector3 position;
            public Color colour;
            public float intensity, range, priority;
            public float age, duration;           // flashes only
            public float score, weight;
            public bool wanted, live;
        }

        static readonly List<Entry> Lights = new();
        static readonly Dictionary<ToonPointLight, Entry> ByLight = new();
        static readonly Entry[] Flashes = new Entry[FlashPool];
        static readonly List<Entry> Ranked = new();
        static readonly Plane[] Frustum = new Plane[6];
        static readonly Vector4[] Positions = new Vector4[MaxLights];
        static readonly Vector4[] Colours = new Vector4[MaxLights];
        static readonly System.Comparison<Entry> ByScore = (a, b) => a.score.CompareTo(b.score);
        static readonly System.Comparison<Entry> ByWeight = (a, b) => (b.weight * b.intensity).CompareTo(a.weight * a.intensity);
        static int _nextFlash;

        /// <summary>Where "near" is measured from; the hero when set, else the ground under the view.</summary>
        public static Transform Focus;

        static ToonPointLights()
        {
            for (int i = 0; i < FlashPool; i++) Flashes[i] = new Entry();
        }

        public static void Register(ToonPointLight light)
        {
            if (light == null || ByLight.ContainsKey(light)) return;
            var e = new Entry { light = light, live = true };
            ByLight[light] = e;
            Lights.Add(e);
            ToonPointLightDriver.Ensure();
        }

        public static void Unregister(ToonPointLight light)
        {
            if (light == null || !ByLight.TryGetValue(light, out var e)) return;
            ByLight.Remove(light);
            Lights.Remove(e);
        }

        /// <summary>A short light: full at once, fading out over <paramref name="duration"/> seconds.</summary>
        public static void Flash(Vector3 position, Color colour, float range, float intensity, float duration)
        {
            if (GraphicsTier.PointLights <= 0) return;
            var e = Flashes[_nextFlash];
            _nextFlash = (_nextFlash + 1) % FlashPool;
            e.position = position; e.colour = colour; e.range = range; e.intensity = intensity;
            e.duration = Mathf.Max(0.02f, duration); e.age = 0f; e.live = true; e.weight = 1f;
            e.priority = 0.5f;   // a flash beats a far steady light for its few frames
            ToonPointLightDriver.Ensure();
        }

        /// <summary>Culls, ranks and fades the lights and hands this frame's lit set to the shaders.</summary>
        internal static void Tick(Camera camera, float dt, float unscaledDt)
        {
            int budget = Mathf.Min(GraphicsTier.PointLights, MaxLights);
            if (camera == null || budget <= 0) { Publish(0); return; }

            GeometryUtility.CalculateFrustumPlanes(camera, Frustum);
            Vector3 focus = FocusPoint(camera);
            float time = Time.time;

            Ranked.Clear();
            for (int i = 0; i < Lights.Count; i++)
            {
                var e = Lights[i];
                var l = e.light;
                e.wanted = false;
                if (l == null || !l.isActiveAndEnabled) continue;
                e.position = l.Position; e.colour = l.colour; e.range = l.range; e.priority = l.priority;
                e.intensity = l.FlickeredIntensity(time);
                if (e.intensity <= 0f || !InView(e.position, e.range)) continue;
                e.score = (e.position - focus).sqrMagnitude - e.priority * 10000f;
                Ranked.Add(e);
            }
            for (int i = 0; i < FlashPool; i++)
            {
                var f = Flashes[i];
                if (!f.live) continue;
                f.age += dt;
                if (f.age >= f.duration) { f.live = false; f.weight = 0f; continue; }
                f.wanted = false;
                if (!InView(f.position, f.range)) continue;
                f.score = (f.position - focus).sqrMagnitude - f.priority * 10000f;
                Ranked.Add(f);
            }

            Ranked.Sort(ByScore);
            for (int i = 0; i < Ranked.Count && i < budget; i++) Ranked[i].wanted = true;

            // Fading is presentation, so it runs on real time: a light switched on while the run is
            // paused (level-up cards) still fades in. Flashes age on game time and wait with it.
            float step = unscaledDt / FadeSeconds;
            Ranked.Clear();
            for (int i = 0; i < Lights.Count; i++)
            {
                var e = Lights[i];
                e.weight = Mathf.MoveTowards(e.weight, e.wanted ? 1f : 0f, step);
                if (e.weight > 0.001f) Ranked.Add(e);
            }
            for (int i = 0; i < FlashPool; i++)
            {
                var f = Flashes[i];
                if (!f.live || !f.wanted) continue;
                float k = 1f - f.age / f.duration;
                f.weight = k * k;                       // quick bright start, soft tail
                Ranked.Add(f);
            }

            Ranked.Sort(ByWeight);
            int count = Mathf.Min(Ranked.Count, budget);
            for (int i = 0; i < count; i++)
            {
                var e = Ranked[i];
                Positions[i] = new Vector4(e.position.x, e.position.y, e.position.z, e.range);
                Color c = e.colour * (e.intensity * e.weight);
                Colours[i] = new Vector4(c.r, c.g, c.b, 1f);
            }
            Publish(count);
        }

        static void Publish(int count)
        {
            Shader.SetGlobalVectorArray(PositionsId, Positions);
            Shader.SetGlobalVectorArray(ColoursId, Colours);
            Shader.SetGlobalFloat(CountId, count);
            Shader.SetGlobalFloat(FalloffId, Falloff);
        }

        static Vector3 FocusPoint(Camera camera)
        {
            if (Focus != null) return Focus.position;
            var t = camera.transform;
            float down = -t.forward.y;
            return down > 0.05f ? t.position + t.forward * (t.position.y / down) : t.position + t.forward * 10f;
        }

        static bool InView(Vector3 position, float range)
        {
            for (int i = 0; i < 6; i++)
                if (Frustum[i].GetDistanceToPoint(position) < -range) return false;
            return true;
        }

        // Domain reload is off in this project: statics survive Play Mode exit.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForNewSession()
        {
            Lights.Clear(); ByLight.Clear(); Ranked.Clear(); Focus = null;
            for (int i = 0; i < FlashPool; i++) { Flashes[i].live = false; Flashes[i].weight = 0f; }
            Publish(0);
        }
    }

    /// <summary>Runs <see cref="ToonPointLights.Tick"/> once per frame, after the camera has moved.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class ToonPointLightDriver : MonoBehaviour
    {
        static ToonPointLightDriver _instance;

        internal static void Ensure()
        {
            if (_instance != null || !Application.isPlaying) return;
            var go = new GameObject("ToonPointLights");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ToonPointLightDriver>();
        }

        void LateUpdate() => ToonPointLights.Tick(Camera.main, Time.deltaTime, Time.unscaledDeltaTime);

        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
