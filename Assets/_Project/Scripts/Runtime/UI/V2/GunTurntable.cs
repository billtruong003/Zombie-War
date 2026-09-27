using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombieWar.UI
{
    /// <summary>
    /// M10 Arsenal 360 view: renders one gun, with its skin, on a private stage far from the menu
    /// into a RenderTexture that UI RawImages show. It spins slowly on its own; dragging any
    /// <see cref="TurntableDrag"/> that points at it turns it by hand.
    /// </summary>
    public sealed class GunTurntable : MonoBehaviour
    {
        // Each turntable gets its own stage far from the menu and from the others: two screens
        // (Arsenal, Gacha) sharing one spot showed each other's gun.
        static int _count;
        Vector3 Stage;

        [SerializeField] private int textureSize = 768;
        [SerializeField] private float spinSpeed = 24f;

        public RenderTexture Texture { get; private set; }

        GameObject _root, _gun;
        Camera _cam;
        const float RestYaw = 55f;
        float _yaw = RestYaw, _pitch = 10f, _idle, _t;
        float _spin, _frame = 1f, _zoom = 1f;
        bool _held;

        public void Show(WeaponData data, Skins.WeaponSkins.Set skin)
        {
            Ensure();
            if (_gun != null) Destroy(_gun);
            if (data == null || data.weaponPrefab == null) return;
            _gun = Instantiate(data.weaponPrefab, _root.transform);
            foreach (var b in _gun.GetComponentsInChildren<Behaviour>(true)) if (!(b is Skins.WeaponSkinApplier)) b.enabled = false;
            foreach (var c in _gun.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            _gun.transform.localPosition = Vector3.zero; _gun.transform.localRotation = Quaternion.identity;
            // Centre on the gun's bounds.
            var rs = _gun.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            var bounds = rs[0].bounds; foreach (var r in rs) bounds.Encapsulate(r.bounds);
            _gun.transform.position += Stage - bounds.center;
            // Frame the gun's bounding sphere with a margin, so it fits the square view at any turn
            // (the old 0.3 x longest side cropped long guns and filled the tile with short ones).
            float radius = bounds.extents.magnitude;
            _frame = radius * 1.15f; _zoom = 1f;
            ApplyZoom();
            if (skin != null) _gun.AddComponent<Skins.WeaponSkinApplier>().Apply(skin);
            PlaceCamera();   // right away: the first frame must not look out from inside the gun
        }

        public void Drag(Vector2 delta)
        {
            _yaw += delta.x * 0.4f;
            _pitch = Mathf.Clamp(_pitch - delta.y * 0.3f, -60f, 70f);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-3f);
            _spin = Mathf.Lerp(_spin, delta.x * 0.4f / dt, 0.5f);
            _held = true;
            _idle = 2.5f;
        }

        /// <summary>Finger lifted: the gun keeps turning for a moment, slowing down.</summary>
        public void Release() { _held = false; }

        /// <summary>Pinch / wheel zoom, as a factor on the framed size (clamped 0.45..1.6).</summary>
        public void Zoom(float factor)
        {
            _zoom = Mathf.Clamp(_zoom / Mathf.Max(0.01f, factor), 0.45f, 1.6f);
            _idle = 2.5f;
            ApplyZoom();
        }

        void ApplyZoom() { if (_cam != null) _cam.orthographicSize = _frame * _zoom; }

        void Ensure()
        {
            if (_root != null) return;
            Stage = new Vector3(_count++ * 50f, 3000f, 0f);
            _root = new GameObject("GunTurntableStage");
            _root.transform.position = Stage;
            Texture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "GunTurntable" };
            var camGo = new GameObject("TurntableCamera");
            camGo.transform.SetParent(_root.transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.nearClipPlane = 0.01f; _cam.farClipPlane = 20f;
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0, 0, 0, 0);
            _cam.targetTexture = Texture;
            var light = new GameObject("TurntableKey").AddComponent<Light>();
            light.transform.SetParent(_root.transform, false);
            light.type = LightType.Point; light.range = 12f; light.intensity = 3f;
            light.transform.localPosition = new Vector3(2f, 3f, -3f);
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            // A full spin shows the muzzle end-on (a thin black line) half the time; rock around the
            // three-quarter view instead, and ease back to it after the player lets go.
            Rock();
            PlaceCamera();
        }

        void Rock()
        {
            float dt = Time.unscaledDeltaTime;
            if (!_held && Mathf.Abs(_spin) > 2f)
            {
                _yaw += _spin * dt;
                _spin *= Mathf.Exp(-3.5f * dt);   // a flick keeps turning, then settles
                _idle = 2.5f;
                return;
            }
            if (_idle > 0f) _idle -= dt;
            else
            {
                _t += Time.unscaledDeltaTime * spinSpeed / 60f;
                float target = RestYaw + Mathf.Sin(_t) * 30f;
                _yaw = Mathf.LerpAngle(_yaw, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
                _pitch = Mathf.Lerp(_pitch, 10f, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            }
        }

        void PlaceCamera()
        {
            if (_cam == null) return;
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _cam.transform.position = Stage + rot * new Vector3(0f, 0f, -5f);
            _cam.transform.rotation = rot;
        }

        void OnEnable() { if (_root != null) _root.SetActive(true); }
        void OnDisable() { if (_root != null) _root.SetActive(false); }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root);
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }
    }

}
