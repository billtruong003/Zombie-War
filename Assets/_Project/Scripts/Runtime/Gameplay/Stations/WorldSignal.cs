using UnityEngine;

namespace ZombieWar.Stations
{
    /// <summary>What kind of station this is. Each type owns a colour and keeps it everywhere.</summary>
    public enum StationKind { SignalRelay = 0, SupplyCache = 1, BossBeacon = 2 }

    /// <summary>Idle → Active → Completed, plus Cooldown for repeatable stations.</summary>
    public enum SignalState { Idle, Active, Completed, Cooldown, Failed }

    /// <summary>
    /// M7.3 Part 1 — the World Signal Language.
    ///
    /// Built <b>before</b> any station and deliberately independent of every prop: the prop carries
    /// the mass, this carries the meaning. Whatever the owner drops in later gets dressed by the same
    /// six elements, so a prop swap is a cosmetic change rather than a redesign.
    ///
    /// <list type="bullet">
    /// <item><b>ground ring</b> — the footprint, and where you must stand if it needs holding</item>
    /// <item><b>vertical beam</b> — visible off-screen; this is what makes a player turn and walk</item>
    /// <item><b>floating icon</b> — which kind of station, at a glance</item>
    /// <item><b>emissive accent</b> — idle / active / completed / cooldown</item>
    /// <item><b>progress indicator</b> — in world space on the station, never a HUD corner</item>
    /// <item><b>one colour per type</b> — Relay teal, Cache amber, Beacon crimson</item>
    /// </list>
    ///
    /// Everything is drawn with LineRenderers and a SpriteRenderer sharing project-owned materials.
    /// No runtime material instances, no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldSignal : MonoBehaviour
    {
        // ── the colour contract. One per type, used by ring, beam, icon and progress alike, so the
        //    player learns "teal = relay" once and it holds everywhere.
        public static Color ColorOf(StationKind kind) => kind switch
        {
            StationKind.SignalRelay => new Color(0.20f, 0.95f, 0.85f),   // teal
            StationKind.SupplyCache => new Color(1.00f, 0.72f, 0.20f),   // amber
            StationKind.BossBeacon  => new Color(1.00f, 0.20f, 0.25f),   // crimson
            _ => Color.white,
        };

        /// Distinct silhouettes, so type is readable even for a colour-blind player: the icon is a
        /// polygon whose SIDE COUNT differs per station. Colour alone is never the only signal.
        public static int IconSidesFor(StationKind kind) => kind switch
        {
            StationKind.SignalRelay => 3,    // triangle — "transmit"
            StationKind.SupplyCache => 4,    // square    — "crate"
            StationKind.BossBeacon  => 6,    // hexagon   — "warning"
            _ => 8,
        };

        [Header("Materials (project-owned assets — never instanced at runtime)")]
        [SerializeField] private Material lineMaterial;

        [Header("Proportions (TUNING)")]
        [SerializeField] private float ringRadius = 3.5f;
        [SerializeField] private int ringSegments = 48;
        [SerializeField] private float ringWidth = 0.18f;
        [SerializeField] private float beamHeight = 14f;
        [SerializeField] private float beamWidth = 0.55f;
        [SerializeField] private float iconHeight = 5.0f;
        [SerializeField] private float iconRadius = 1.6f;    // was 0.75 — too small to read

        LineRenderer _ring, _beam, _progress, _icon;
        StationKind _kind;
        SignalState _state = SignalState.Idle;
        float _progress01;
        Transform _tr;

        /// <summary>Assigns the shared line material. Used by the director because this component is
        /// created at runtime rather than authored on a prefab. Shared, never instanced.</summary>
        public void SetMaterial(Material m)
        {
            lineMaterial = m;
            if (_ring != null) _ring.sharedMaterial = m;
            if (_beam != null) _beam.sharedMaterial = m;
            if (_progress != null) _progress.sharedMaterial = m;
            if (_icon != null) _icon.sharedMaterial = m;
        }

        public StationKind Kind => _kind;
        public SignalState State => _state;
        public float Progress01 => _progress01;
        public float RingRadius => ringRadius;

        void Awake()
        {
            _tr = transform;
            _ring = MakeLine("ring", ringSegments + 1, ringWidth, false);
            _beam = MakeLine("beam", 2, beamWidth, true);   // view-aligned: a true vertical column
            _progress = MakeLine("progress", ringSegments + 1, ringWidth * 1.6f, false);
            _icon = MakeLine("icon", 9, 0.30f, true);   // thicker stroke survives at distance
        }

        LineRenderer MakeLine(string name, int points, float width, bool viewAligned)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = points;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.alignment = viewAligned ? LineAlignment.View : LineAlignment.TransformZ;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            // sharedMaterial: assigning .material would clone per instance, which the budget forbids.
            if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
            return lr;
        }

        /// <summary>Dresses this signal for a station type. Safe to call before or after Awake.</summary>
        public void Configure(StationKind kind, float radius)
        {
            if (_ring == null) Awake();
            _kind = kind;
            ringRadius = radius;

            BuildCircle(_ring, ringRadius, 0.05f, ringSegments);
            BuildCircle(_progress, ringRadius * 0.86f, 0.07f, ringSegments);
            _beam.SetPosition(0, new Vector3(0f, 0.1f, 0f));
            _beam.SetPosition(1, new Vector3(0f, beamHeight, 0f));
            BuildPolygon(_icon, IconSidesFor(kind), iconRadius, iconHeight);

            SetState(SignalState.Idle);
            SetProgress(0f);
        }

        static void BuildCircle(LineRenderer lr, float radius, float y, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
            }
        }

        /// <summary>
        /// The icon is built in the VERTICAL (XY) plane, not the ground plane.
        ///
        /// The first capture of this language showed why: a polygon laid flat is seen edge-on from
        /// the gameplay camera and reads as a meaningless sliver. Upright, it is a badge the player
        /// can actually identify — and the billboard in LateUpdate then keeps it facing the camera.
        /// </summary>
        static void BuildPolygon(LineRenderer lr, int sides, float radius, float y)
        {
            lr.positionCount = sides + 1;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f + Mathf.PI * 0.5f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, 0f));
            }
        }

        /// <summary>
        /// The emissive accent. Each state is a different brightness AND a different beam length, so
        /// "done" is readable at gameplay distance without reading a number.
        /// </summary>
        public void SetState(SignalState state)
        {
            _state = state;
            Color baseColor = ColorOf(_kind);

            // Completed used to be intensity 0.30 at alpha 0.35 with a 12% beam stub — near-invisible,
            // and the owner read it as "broken" rather than "done". A finished station now keeps a
            // BRIGHT, fully-closed ring and simply drops its beam: the call-to-action goes away, the
            // landmark stays. That is what "finished" should look like.
            float intensity;
            float beamScale;
            switch (state)
            {
                case SignalState.Idle:      intensity = 0.60f; beamScale = 0.55f; break;
                case SignalState.Active:    intensity = 1.00f; beamScale = 1.00f; break;
                case SignalState.Completed: intensity = 0.85f; beamScale = 0.00f; break;
                case SignalState.Cooldown:  intensity = 0.40f; beamScale = 0.18f; break;
                default:                    intensity = 0.70f; beamScale = 0.35f; break;   // Failed
            }

            Color c = baseColor * intensity;
            c.a = 1f;   // never faded — a faded signal reads as a rendering fault, not a state
            Tint(_ring, c);
            Tint(_beam, c);
            Tint(_icon, c);

            _beam.SetPosition(1, new Vector3(0f, beamHeight * beamScale, 0f));
            _beam.enabled = beamScale > 0.001f;

            // The closed progress ring IS the completion mark, so it stays on when done.
            _progress.enabled = state == SignalState.Active || state == SignalState.Completed;
            if (state == SignalState.Completed) SetProgress(1f);
        }

        static void Tint(LineRenderer lr, Color c) { if (lr != null) lr.startColor = lr.endColor = c; }

        /// <summary>World-space progress: the inner ring closes as the objective completes.</summary>
        public void SetProgress(float t)
        {
            _progress01 = Mathf.Clamp01(t);
            if (_progress == null) return;

            int shown = Mathf.Max(1, Mathf.RoundToInt(ringSegments * _progress01));
            for (int i = 0; i <= ringSegments; i++)
            {
                float a = Mathf.Min(i, shown) / (float)ringSegments * Mathf.PI * 2f;
                float r = ringRadius * 0.86f;
                _progress.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0.07f, Mathf.Sin(a) * r));
            }
            Color c = ColorOf(_kind);
            Tint(_progress, c);
        }

        void LateUpdate()
        {
            // Billboard only the icon; the ring and beam are world-oriented on purpose.
            if (_icon != null && Camera.main != null)
            {
                Vector3 f = Camera.main.transform.forward; f.y = 0f;
                if (f.sqrMagnitude > 1e-4f)
                    _icon.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            }
        }

        /// <summary>True when the player stands inside the footprint — the hold test.</summary>
        public bool Contains(Vector3 worldPosition)
        {
            Vector3 d = worldPosition - _tr.position;
            d.y = 0f;
            return d.sqrMagnitude <= ringRadius * ringRadius;
        }
    }
}
