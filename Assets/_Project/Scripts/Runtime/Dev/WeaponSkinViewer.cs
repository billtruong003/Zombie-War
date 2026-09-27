#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieWar.Skins;

namespace ZombieWar.Dev
{
    /// <summary>
    /// M8 skin review bench (owner: look at a gun skin from every angle in the sandbox). A turntable
    /// with its own camera and lights, drawn over the sandbox: pick a gun and a skin set, drag to
    /// orbit, scroll to zoom, or jump to a preset angle. Started by ZombieWar/Dev/Play Skin Viewer.
    /// </summary>
    public sealed class WeaponSkinViewer : MonoBehaviour
    {
        static readonly Vector3 Stage = new(0f, 800f, 0f);
        static readonly (string name, float yaw, float pitch)[] Presets =
        {
            ("Side", 90f, 0f), ("3/4 front", 55f, 12f), ("3/4 back", 125f, 12f), ("Top", 90f, 70f), ("Muzzle", 0f, 5f), ("Player view", 150f, 35f),
        };

        List<WeaponData> _guns;
        int _gun, _set = 0;
        GameObject _inst, _camGo;
        Camera _cam;
        float _yaw = 55f, _pitch = 12f, _dist = 1.5f, _size = 1f;
        bool _spin = true;
        Vector3 _center;
        readonly List<Canvas> _hidden = new();

        void Start()
        {
            var catalog = Resources.Load<WeaponCatalog>("WeaponCatalog");
            _guns = catalog != null
                ? catalog.Entries.Select(e => e.data).Where(d => d != null && d.weaponPrefab != null).Distinct().ToList()
                : new List<WeaponData>();

            _camGo = new GameObject("SkinViewerCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.depth = 100;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.1f, 0.11f, 0.14f);
            _cam.fieldOfView = 30f;
            _cam.nearClipPlane = 0.01f;
            // The bench is for looking at the gun: hide the run HUD and close the sandbox panel.
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.enabled) { c.enabled = false; _hidden.Add(c); }
            var sandbox = GetComponent<SkillSandbox>();
            sandbox?.GetType().GetField("_panelOpen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(sandbox, false);
            AddLight(new Vector3(30f, 150f, 0f), 1.4f, new Color(1f, 0.96f, 0.9f));
            AddLight(new Vector3(20f, -40f, 0f), 0.6f, new Color(0.75f, 0.85f, 1f));
            Spawn();
        }

        void AddLight(Vector3 euler, float intensity, Color c)
        {
            var go = new GameObject("SkinViewerLight");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = c;
        }

        void Spawn()
        {
            if (_inst != null) Destroy(_inst);
            if (_guns.Count == 0) return;
            var wd = _guns[(_gun % _guns.Count + _guns.Count) % _guns.Count];
            _inst = Instantiate(wd.weaponPrefab, Stage, Quaternion.identity);
            foreach (var ps in _inst.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            foreach (var mb in _inst.GetComponentsInChildren<MonoBehaviour>(true)) if (!(mb is WeaponSkinApplier)) mb.enabled = false;
            var rs = _inst.GetComponentsInChildren<Renderer>(true);
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(Stage, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            _center = b.center;
            _size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            _dist = _size * 3.4f;
            _inst.AddComponent<WeaponSkinApplier>().Apply(WeaponSkins.Season1[_set]);
        }

        void Update()
        {
            if (_spin) _yaw += 25f * Time.unscaledDeltaTime;
            if (Input.GetMouseButton(0) && Input.mousePosition.x > 240f)
            {
                _spin = false;
                _yaw += Input.GetAxis("Mouse X") * 6f;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 4f, -80f, 80f);
            }
            _dist = Mathf.Clamp(_dist * (1f - Input.mouseScrollDelta.y * 0.1f), _size * 0.6f, _size * 5f);
            if (_cam == null) return;
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _cam.transform.position = _center + rot * (Vector3.back * _dist);
            _cam.transform.LookAt(_center);
        }

        void OnGUI()
        {
            GUI.skin.button.fontSize = 22;
            GUI.skin.label.fontSize = 22;
            GUILayout.BeginArea(new Rect(12, 12, 220, Screen.height - 24));
            var set = WeaponSkins.Season1[_set];
            var gun = _guns.Count > 0 ? _guns[(_gun % _guns.Count + _guns.Count) % _guns.Count] : null;
            GUILayout.Label($"<b>{set.name}</b>\n{set.theme}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Skin")) { _set = (_set + WeaponSkins.Season1.Length - 1) % WeaponSkins.Season1.Length; _inst?.GetComponent<WeaponSkinApplier>()?.Apply(WeaponSkins.Season1[_set]); }
            if (GUILayout.Button("Skin >")) { _set = (_set + 1) % WeaponSkins.Season1.Length; _inst?.GetComponent<WeaponSkinApplier>()?.Apply(WeaponSkins.Season1[_set]); }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label(gun != null ? gun.weaponName : "no guns");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Gun")) { _gun--; Spawn(); }
            if (GUILayout.Button("Gun >")) { _gun++; Spawn(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            foreach (var p in Presets)
                if (GUILayout.Button(p.name)) { _spin = false; _yaw = p.yaw; _pitch = p.pitch; }
            _spin = GUILayout.Toggle(_spin, " Spin");
            GUILayout.Label("Drag to orbit, scroll to zoom");
            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            foreach (var c in _hidden) if (c != null) c.enabled = true;
            if (_inst != null) Destroy(_inst);
            if (_camGo != null) Destroy(_camGo);
        }
    }
}
#endif
