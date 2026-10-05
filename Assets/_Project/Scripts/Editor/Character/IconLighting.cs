using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// 05/10 owner: gacha portraits had faces in shadow. The old rigs lit from the side and the
    /// "fill" pointed at the back - and the toon shader reads only the main (brightest) directional
    /// light, so the fill did nothing. Now the key light comes from three-quarter front and above,
    /// placed relative to the capture camera, a warm rim sits behind, and a flat ambient does the
    /// filling. The scene's ambient is restored on Dispose.
    /// </summary>
    public sealed class IconLighting : IDisposable
    {
        public const float KeyIntensity = 1.2f, RimIntensity = 1.0f;
        public static readonly Color Ambient = new(0.54f, 0.55f, 0.62f);

        readonly GameObject _key, _rim;
        readonly AmbientMode _oldMode;
        readonly Color _oldAmbient;
        readonly float _oldIntensity;

        public IconLighting()
        {
            _key = Make("IconKey", KeyIntensity, Color.white);
            _rim = Make("IconRim", RimIntensity, new Color(1f, 0.85f, 0.62f));
            _oldMode = RenderSettings.ambientMode;
            _oldAmbient = RenderSettings.ambientLight;
            _oldIntensity = RenderSettings.ambientIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            RenderSettings.ambientIntensity = 1f;
        }

        /// <summary>Points the lights for this camera pose: key from the camera's upper left, rim
        /// from behind the subject toward the camera.</summary>
        public void Aim(Transform camera)
        {
            Vector3 f = camera.forward;
            _key.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(-30f, Vector3.up) * f + Vector3.down * 0.6f);
            _rim.transform.rotation = Quaternion.LookRotation(-f + Vector3.down * 0.5f);
        }

        public void Dispose()
        {
            if (_key != null) UnityEngine.Object.DestroyImmediate(_key);
            if (_rim != null) UnityEngine.Object.DestroyImmediate(_rim);
            RenderSettings.ambientMode = _oldMode;
            RenderSettings.ambientLight = _oldAmbient;
            RenderSettings.ambientIntensity = _oldIntensity;
        }

        static GameObject Make(string name, float intensity, Color color)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            return go;
        }
    }
}
