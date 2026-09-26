using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills
{
    /// <summary>
    /// M7.2c — makes skills legible.
    ///
    /// The owner's complaint was precise: you cannot tell when a skill triggers, and the VFX does not
    /// represent the skill. Both are fixed here rather than in the cards.
    ///
    /// <b>Why this class has to exist at all:</b> Epic Toon FX ships 49 prefabs named `Lightning*` and
    /// every one of them is an explosion. There is no beam, no arc, nothing that connects two points.
    /// A "chain" drawn with the pack alone is a flash on each enemy with nothing between them, which
    /// is not chain lightning. So the arc is built here from pooled LineRenderers.
    ///
    /// Everything is pooled and shares one material asset — no runtime material instances, no
    /// per-proc allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillFxDirector : MonoBehaviour
    {
        public static SkillFxDirector Instance { get; private set; }

        [Header("Chain arc (built, not from the pack — the pack has no beam)")]
        [SerializeField] private Material arcMaterial;
        [SerializeField] private Color arcColor = new(0.55f, 0.85f, 1f, 1f);
        [SerializeField] private float arcWidth = 0.2f;
        [Tooltip("M8: 0.18 s was gone before the eye found it; long enough to follow the chain.")]
        [SerializeField] private float arcLifetime = 0.3f;
        [Tooltip("Sideways jitter per segment, so the bolt reads as electricity rather than a ruler line.")]
        [SerializeField] private float arcJitter = 0.22f;
        [SerializeField] private int arcSegments = 6;
        [Tooltip("Hard ceiling on simultaneously visible arcs. Each hop draws two lines (glow + core).")]
        [SerializeField] private int arcPoolSize = 36;

        [Header("Status marks (world-space, so no UI prefab is touched)")]
        [SerializeField] private Material markMaterial;
        [SerializeField] private int markPoolSize = 24;

        readonly List<ArcInstance> _arcs = new(12);

        // Expanding ground rings: the readable edge of every area power.
        const int RingSegments = 48;
        class RingInstance
        {
            public LineRenderer line;
            public Vector3 centre;
            public float radius, width, bornAt, duration;
            public Color color;
            public bool live;
            public bool converge;
        }
        readonly List<RingInstance> _rings = new(24);

        // Timed status tints. A small fixed list scanned once per frame; a re-tint of an enemy
        // already in it just extends the timer.
        struct TintEntry { public ZombieBase enemy; public float until; }
        readonly List<TintEntry> _tints = new(128);
        readonly List<MarkInstance> _marks = new(24);
        Transform _root;

        class ArcInstance
        {
            public LineRenderer line;
            public float dieAt;
            public bool live;
        }

        class MarkInstance
        {
            public Transform tr;
            public SpriteRenderer sprite;
            public Transform follow;
            public float dieAt;
            public bool live;
        }

        void Awake()
        {
            Instance = this;
            _root = new GameObject("~SkillFx").transform;
            _root.SetParent(null);

            for (int i = 0; i < arcPoolSize; i++) _arcs.Add(CreateArc());
            for (int i = 0; i < 24; i++) _rings.Add(CreateRing());
            for (int i = 0; i < markPoolSize; i++) _marks.Add(CreateMark());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_root != null) Destroy(_root.gameObject);
        }

        ArcInstance CreateArc()
        {
            var go = new GameObject("arc");
            go.transform.SetParent(_root);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = arcSegments + 1;
            lr.widthMultiplier = arcWidth;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            // sharedMaterial, never material — assigning .material would clone it per arc, which is
            // exactly the "runtime material instance" the budget forbids.
            if (arcMaterial != null) lr.sharedMaterial = arcMaterial;
            lr.startColor = lr.endColor = arcColor;
            go.SetActive(false);
            return new ArcInstance { line = lr, live = false };
        }

        RingInstance CreateRing()
        {
            var go = new GameObject("ring");
            go.transform.SetParent(_root);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = RingSegments;
            lr.numCornerVertices = 0;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (arcMaterial != null) lr.sharedMaterial = arcMaterial;
            go.SetActive(false);
            return new RingInstance { line = lr };
        }

        /// <summary>
        /// A ring that races out from <paramref name="centre"/> to exactly <paramref name="radius"/>
        /// and fades. Every area power calls it with the radius it actually checked, so what the
        /// player sees is the hitbox — the missing piece that made AoE powers read as "something
        /// flashed somewhere".
        /// </summary>
        public void Pulse(Vector3 centre, float radius, Color color, float duration = 0.35f, float width = 0.3f)
        {
            RingInstance r = null;
            for (int i = 0; i < _rings.Count; i++) if (!_rings[i].live) { r = _rings[i]; break; }
            if (r == null) return;
            centre.y = 0.08f;
            r.centre = centre; r.radius = radius; r.width = width; r.color = color;
            r.bornAt = Time.time; r.duration = Mathf.Max(0.05f, duration);
            r.converge = false;
            r.live = true;
            r.line.gameObject.SetActive(true);
            DrawRing(r, 0f);
        }

        /// <summary>
        /// A target lock: a ring that closes in from wide to exactly <paramref name="radius"/> over
        /// <paramref name="duration"/> and brightens as it lands. Used for anything that will hit a
        /// spot later (airstrike, ordnance) so "something is coming HERE" reads before it arrives.
        /// </summary>
        public void Converge(Vector3 centre, float radius, Color color, float duration, float width = 0.16f)
        {
            RingInstance r = null;
            for (int i = 0; i < _rings.Count; i++) if (!_rings[i].live) { r = _rings[i]; break; }
            if (r == null) return;
            centre.y = 0.08f;
            r.centre = centre; r.radius = radius; r.width = width; r.color = color;
            r.bornAt = Time.time; r.duration = Mathf.Max(0.05f, duration);
            r.converge = true;
            r.live = true;
            r.line.gameObject.SetActive(true);
            DrawRing(r, 0f);
        }

        static void DrawRing(RingInstance r, float t)
        {
            if (r.converge)
            {
                float k = t * t;                                   // slow start, snaps shut
                float rr = Mathf.Lerp(r.radius * 1.35f, r.radius, k);
                for (int i = 0; i < RingSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / RingSegments;
                    r.line.SetPosition(i, r.centre + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr));
                }
                var cc = r.color; cc.a *= Mathf.Lerp(0.35f, 1f, t);
                r.line.startColor = r.line.endColor = cc;
                r.line.widthMultiplier = r.width;
                return;
            }
            float e = 1f - (1f - t) * (1f - t) * (1f - t);          // fast out, soft landing
            float radius = Mathf.Lerp(r.radius * 0.15f, r.radius, e);
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                r.line.SetPosition(i, r.centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            var c = r.color; c.a *= 1f - t * t;
            r.line.startColor = r.line.endColor = c;
            r.line.widthMultiplier = r.width * (1f - 0.5f * t);
        }

        MarkInstance CreateMark()
        {
            var go = new GameObject("mark");
            go.transform.SetParent(_root);
            var sr = go.AddComponent<SpriteRenderer>();
            if (markMaterial != null) sr.sharedMaterial = markMaterial;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            go.SetActive(false);
            return new MarkInstance { tr = go.transform, sprite = sr, live = false };
        }

        // ───────────────────────────────────────────────────────────── chain arc

        /// <summary>
        /// Draws one visible bolt from <paramref name="from"/> to <paramref name="to"/>. Called once
        /// per hop, so a 4-target chain draws 4 connected segments the player can follow.
        /// </summary>
        public void DrawArc(Vector3 from, Vector3 to, Color? tint = null, float widthScale = 1f)
        {
            var arc = Take();
            if (arc == null) return;   // pool exhausted: drop the visual rather than allocate

            var lr = arc.line;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
            if (side.sqrMagnitude < 1e-5f) side = Vector3.right;

            // Segments and jag scale with length: a 12 m bolt drawn with 6 segments and a fixed
            // 0.22 m wobble read as a laser. The noise seed comes from the endpoints, so the glow
            // line and the white core drawn for the same hop land on exactly the same zig-zag.
            int segments = Mathf.Clamp(Mathf.RoundToInt(length / 0.7f), arcSegments, 18);
            float jag = Mathf.Clamp(length * 0.07f, arcJitter, 0.8f);
            float seed = (from.x * 0.37f + from.z * 0.71f + to.x * 0.13f + to.z * 0.53f) % 97f;
            float frame = Mathf.Floor(Time.time * 20f);          // re-jag 20x a second, not every frame
            lr.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(from, to, t);
                // Zero jitter at both ends so the bolt visibly TOUCHES each enemy.
                float taper = Mathf.Sin(t * Mathf.PI);
                float offset = (Mathf.PerlinNoise(seed + i * 1.37f, frame * 0.61f) - 0.5f) * 2f * jag * taper;
                lr.SetPosition(i, p + side * offset + Vector3.up * (offset * 0.35f));
            }

            var c = tint ?? arcColor;
            lr.startColor = lr.endColor = c;
            lr.widthMultiplier = arcWidth * widthScale;
            lr.gameObject.SetActive(true);
            arc.live = true;
            arc.dieAt = Time.time + arcLifetime;
        }

        ArcInstance Take()
        {
            for (int i = 0; i < _arcs.Count; i++) if (!_arcs[i].live) return _arcs[i];
            return null;
        }

        // ───────────────────────────────────────────────────────────── status tints

        /// <summary>Colours an enemy for <paramref name="seconds"/> (frost, burn), then clears it.</summary>
        public void TintEnemy(ZombieBase enemy, Color tint, float seconds)
        {
            if (enemy == null) return;
            float until = Time.time + seconds;
            for (int i = 0; i < _tints.Count; i++)
            {
                if (_tints[i].enemy != enemy) continue;
                if (until > _tints[i].until) _tints[i] = new TintEntry { enemy = enemy, until = until };
                enemy.SetStatusTint(tint);
                return;
            }
            if (_tints.Count >= 192) return;   // a full list drops the visual, never allocates
            _tints.Add(new TintEntry { enemy = enemy, until = until });
            enemy.SetStatusTint(tint);
        }

        // ───────────────────────────────────────────────────────────── status marks

        /// <summary>
        /// A world-space mark that follows an enemy carrying a status. World-space is deliberate:
        /// the owner owns every UI prefab, so nothing here may live in the HUD.
        /// </summary>
        public void MarkEnemy(Transform enemy, Color color, float duration, float height = 2.0f)
        {
            if (enemy == null) return;
            MarkInstance m = null;
            for (int i = 0; i < _marks.Count; i++) if (!_marks[i].live) { m = _marks[i]; break; }
            if (m == null) return;

            m.follow = enemy;
            m.tr.position = enemy.position + Vector3.up * height;
            m.tr.localScale = Vector3.one * 0.35f;
            if (m.sprite != null) m.sprite.color = color;
            m.tr.gameObject.SetActive(true);
            m.live = true;
            m.dieAt = Time.time + duration;
        }

        void LateUpdate()
        {
            float now = Time.time;

            for (int i = 0; i < _rings.Count; i++)
            {
                var r = _rings[i];
                if (!r.live) continue;
                float t = (now - r.bornAt) / r.duration;
                if (t >= 1f) { r.live = false; r.line.gameObject.SetActive(false); continue; }
                DrawRing(r, t);
            }

            for (int i = _tints.Count - 1; i >= 0; i--)
            {
                var t = _tints[i];
                if (t.enemy != null && now < t.until && !t.enemy.IsDead) continue;
                if (t.enemy != null) t.enemy.SetStatusTint(Color.clear);
                _tints.RemoveAt(i);
            }

            for (int i = 0; i < _arcs.Count; i++)
            {
                var a = _arcs[i];
                if (!a.live) continue;
                if (now >= a.dieAt) { a.line.gameObject.SetActive(false); a.live = false; continue; }
                // Fade out so the bolt snaps rather than blinks.
                float k = Mathf.InverseLerp(a.dieAt, a.dieAt - arcLifetime, now);
                var c = a.line.startColor; c.a = k;
                a.line.startColor = a.line.endColor = c;
            }

            for (int i = 0; i < _marks.Count; i++)
            {
                var m = _marks[i];
                if (!m.live) continue;
                if (now >= m.dieAt || m.follow == null)
                {
                    m.tr.gameObject.SetActive(false); m.live = false; m.follow = null; continue;
                }
                m.tr.position = m.follow.position + Vector3.up * 2.0f;
                if (Camera.main != null) m.tr.forward = Camera.main.transform.forward;
            }
        }

        /// <summary>Diagnostics for the guardrail tests.</summary>
        public int LiveArcs { get { int n = 0; for (int i = 0; i < _arcs.Count; i++) if (_arcs[i].live) n++; return n; } }
        public int ArcCapacity => _arcs.Count;
    }
}
