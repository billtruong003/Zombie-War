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
        static readonly Vector3 Stage = new(0f, 3000f, 0f);

        [SerializeField] private int textureSize = 768;
        [SerializeField] private float spinSpeed = 24f;

        public RenderTexture Texture { get; private set; }

        GameObject _root, _gun;
        Camera _cam;
        float _yaw = 55f, _pitch = 10f, _idle;

        public void Show(WeaponData data, Skins.WeaponSkins.Set skin)
        {
            Ensure();
            if (_gun != null) Destroy(_gun);
            if (data == null || data.weaponPrefab == null) return;
            _gun = Instantiate(data.weaponPrefab, _root.transform);
            foreach (var b in _gun.GetComponentsInChildren<Behaviour>(true)) if (!(b is Skins.WeaponSkinApplier)) b.enabled = false;
            foreach (var c in _gun.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            _gun.transform.localPosition = Vector3.zero; _gun.transform.localRotation = Quaternion.identity;
            // Centre on the gun's bounds and frame its longest side.
            var rs = _gun.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            var bounds = rs[0].bounds; foreach (var r in rs) bounds.Encapsulate(r.bounds);
            _gun.transform.position += Stage - bounds.center;
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            _cam.orthographicSize = size * 0.3f;
            if (skin != null) _gun.AddComponent<Skins.WeaponSkinApplier>().Apply(skin);
        }

        public void Drag(Vector2 delta)
        {
            _yaw += delta.x * 0.4f;
            _pitch = Mathf.Clamp(_pitch - delta.y * 0.3f, -60f, 70f);
            _idle = 2f;
        }

        void Ensure()
        {
            if (_root != null) return;
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
            if (_idle > 0f) _idle -= Time.unscaledDeltaTime; else _yaw += spinSpeed * Time.unscaledDeltaTime;
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

    /// <summary>Drag on a RawImage to turn the <see cref="GunTurntable"/>.</summary>
    public sealed class TurntableDrag : MonoBehaviour, IDragHandler
    {
        [SerializeField] private GunTurntable turntable;
        public void OnDrag(PointerEventData e) { if (turntable != null) turntable.Drag(e.delta); }
    }
}
