using System;
using UnityEngine;

namespace ZombieWar.Stations
{
    /// <summary>
    /// Phase A9b (owner 2026-09-30): the look of a station body. The glowing lines (children named
    /// "*_Glow", HordeCall/Station/Energy) carry the type colour, an energy pulse that runs faster
    /// while the player stands in the zone, and the progress, which fills the lines. A "*_Spin"
    /// child (the heal crystal) turns and bobs. The station materialises when it streams in and
    /// dissolves away when it has paid out: it no longer stays on the map as a spent landmark.
    ///
    /// At rest the body keeps its toon-lit palette material (shadows, SRP batching); only the
    /// ~1 s transitions swap it to the dissolve material. Per-renderer values go through one
    /// property block, never a material instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StationVisual : MonoBehaviour
    {
        public const float AppearSeconds = 0.9f;
        public const float VanishSeconds = 1.3f;

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int PulseId = Shader.PropertyToID("_PulseSpeed");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int EdgeId = Shader.PropertyToID("_EdgeColor");
        static readonly int HeightId = Shader.PropertyToID("_Height");

        Renderer[] _body = Array.Empty<Renderer>();
        Material[][] _bodyMaterials = Array.Empty<Material[]>();
        Renderer[] _glow = Array.Empty<Renderer>();
        Transform _spin;
        Vector3 _spinHome;
        MaterialPropertyBlock _mpb;
        Material _dissolveMaterial;

        Color _color = Color.white;
        float _fill, _pulse = 0.6f, _dissolve, _height = 1f;
        enum Phase { Appearing, Resting, Vanishing, Gone }
        Phase _phase = Phase.Resting;
        float _phaseAt;
        Action _onGone;

        public bool Gone => _phase == Phase.Gone;
        public bool Vanishing => _phase == Phase.Vanishing;
        public float Fill => _fill;
        public float Dissolve => _dissolve;

        void Awake() => Collect();

        void Collect()
        {
            if (_mpb != null) return;
            _mpb = new MaterialPropertyBlock();
            var body = new System.Collections.Generic.List<Renderer>();
            var glow = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.EndsWith("_Glow") || r.name.EndsWith("_Spin")) glow.Add(r);
                else body.Add(r);
                if (r.name.EndsWith("_Spin")) { _spin = r.transform; _spinHome = _spin.localPosition; }
            }
            _body = body.ToArray();
            _glow = glow.ToArray();
            _bodyMaterials = new Material[_body.Length][];
            for (int i = 0; i < _body.Length; i++) _bodyMaterials[i] = _body[i].sharedMaterials;
            float top = 0.5f;
            foreach (var r in _body)
                if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                    top = Mathf.Max(top, mf.sharedMesh.bounds.max.y);   // object space, as the shader reads it
            _height = top;
        }

        /// <summary>Colours the station for its type and starts it materialising.</summary>
        public void Init(Color color, Material dissolveMaterial, bool appear = true)
        {
            Collect();
            _color = color;
            _dissolveMaterial = dissolveMaterial;
            _fill = 0f;
            if (appear && _dissolveMaterial != null) Begin(Phase.Appearing);
            else { _dissolve = 0f; Begin(Phase.Resting); }
            Push();
        }

        public void SetFill(float t) { _fill = Mathf.Clamp01(t); }

        /// <summary>The pulse runs faster while someone is charging the station.</summary>
        public void SetCharging(bool on) { _pulse = on ? 2.2f : 0.6f; }

        /// <summary>Paid out: dissolve away, then call <paramref name="onGone"/>.</summary>
        public void Vanish(Action onGone = null)
        {
            if (_phase == Phase.Vanishing || _phase == Phase.Gone) return;
            _onGone = onGone;
            Begin(Phase.Vanishing);
        }

        void Begin(Phase phase)
        {
            _phase = phase;
            _phaseAt = Time.time;
            bool transition = phase == Phase.Appearing || phase == Phase.Vanishing;
            for (int i = 0; i < _body.Length; i++)
            {
                if (_body[i] == null) continue;
                if (transition && _dissolveMaterial != null)
                {
                    var mats = new Material[_bodyMaterials[i].Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = _dissolveMaterial;
                    _body[i].sharedMaterials = mats;
                }
                else _body[i].sharedMaterials = _bodyMaterials[i];
            }
            if (phase == Phase.Appearing) _dissolve = 1f;
        }

        void Update()
        {
            float now = Time.time;
            switch (_phase)
            {
                case Phase.Appearing:
                    _dissolve = 1f - Mathf.Clamp01((now - _phaseAt) / AppearSeconds);
                    if (_dissolve <= 0f) Begin(Phase.Resting);
                    break;
                case Phase.Vanishing:
                    _dissolve = Mathf.Clamp01((now - _phaseAt) / VanishSeconds);
                    if (_dissolve >= 1f)
                    {
                        _phase = Phase.Gone;
                        foreach (var r in _body) if (r != null) r.enabled = false;
                        foreach (var r in _glow) if (r != null) r.enabled = false;
                        var done = _onGone; _onGone = null;
                        done?.Invoke();
                        return;
                    }
                    break;
                case Phase.Gone:
                    return;
            }
            if (_spin != null)
            {
                _spin.localRotation = Quaternion.Euler(0f, now * 40f % 360f, 0f);
                _spin.localPosition = _spinHome + Vector3.up * (Mathf.Sin(now * 1.6f) * 0.08f);
            }
            Push();
        }

        void Push()
        {
            if (_mpb == null) return;
            for (int i = 0; i < _glow.Length; i++)
            {
                var r = _glow[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(TintId, _color);
                // The crystal lights up with the charge; the lines fill along their length.
                _mpb.SetFloat(FillId, r.transform == _spin ? Mathf.Lerp(0.3f, 1f, _fill) : _fill);
                _mpb.SetFloat(PulseId, _pulse);
                _mpb.SetFloat(DissolveId, _dissolve);
                r.SetPropertyBlock(_mpb);
            }
            if (_phase != Phase.Appearing && _phase != Phase.Vanishing) return;
            for (int i = 0; i < _body.Length; i++)
            {
                var r = _body[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(DissolveId, _dissolve);
                _mpb.SetFloat(HeightId, _height);
                _mpb.SetColor(EdgeId, _color * 2.2f);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
