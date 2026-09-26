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
        [Tooltip("Additive ZombieWar/FX/SkillLine material (M8: the old opaque one could not fade).")]
        [SerializeField] private Material arcMaterial;
        [Tooltip("Alpha-blended ZombieWar/FX/SkillLine material for ground rings (additive washes out on sand). " +
                 "Empty = the arc material.")]
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Color arcColor = new(0.35f, 0.75f, 1f, 1f);
        [SerializeField] private float arcWidth = 0.34f;
        [Tooltip("How long a bolt stays: a bright flash, then a fade.")]
        [SerializeField] private float arcLifetime = 0.28f;
        [Tooltip("Sideways jitter per segment, so the bolt reads as electricity rather than a ruler line.")]
        [SerializeField] private float arcJitter = 0.28f;
        [SerializeField] private int arcSegments = 8;
        [Tooltip("How often a live bolt re-draws its zig-zag. Lightning flickers; a fixed shape reads as a pipe.")]
        [SerializeField] private float arcRejagSeconds = 0.05f;
        [Tooltip("Hard ceiling on simultaneously visible arc lines. Each bolt uses up to 4 (glow, core, 2 forks).")]
        [SerializeField] private int arcPoolSize = 64;

        [Header("Status marks (world-space, so no UI prefab is touched)")]
        [Tooltip("M8: the marks had a material but no sprite, so none of them ever showed.")]
        [SerializeField] private Sprite markSprite;
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
            // M8 cone waves: a slice of the ring (arcHalf degrees either side of arcDir). 0 = full ring.
            public float arcHalf;
            public float arcHeading;
        }
        readonly List<RingInstance> _rings = new(24);

        // Timed status tints. A small fixed list scanned once per frame; a re-tint of an enemy
        // already in it just extends the timer.
        struct TintEntry { public ZombieBase enemy; public float until; }
        readonly List<TintEntry> _tints = new(128);
        readonly List<MarkInstance> _marks = new(24);
        Transform _root;

        enum ArcLayer { Glow, Core, Fork }

        class ArcInstance
        {
            public LineRenderer line;
            public float showAt, dieAt, nextJag;
            public bool live;
            // Shape: every layer of one bolt shares from/to/seed, so glow, core and forks follow one zig-zag.
            public ArcLayer layer;
            public Vector3 from, to;
            public float seed, width;
            public Color color;
            public float forkT, forkSide, forkLength;
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
            // Tapered: a bolt is thin where it touches an enemy and full in the middle.
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f),
                                               new Keyframe(0.85f, 1f), new Keyframe(1f, 0.3f));
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
            var m = ringMaterial != null ? ringMaterial : arcMaterial;
            if (m != null) lr.sharedMaterial = m;
            go.SetActive(false);
            return new RingInstance { line = lr };
        }

        /// <summary>
        /// A ring that races out from <paramref name="centre"/> to exactly <paramref name="radius"/>
        /// and fades. Every area power calls it with the radius it actually checked, so what the
        /// player sees is the hitbox — the missing piece that made AoE powers read as "something
        /// flashed somewhere".
        /// </summary>
        public void Pulse(Vector3 centre, float radius, Color color, float duration = 0.35f, float width = 0.2f)
        {
            RingInstance r = null;
            for (int i = 0; i < _rings.Count; i++) if (!_rings[i].live) { r = _rings[i]; break; }
            if (r == null) return;
            centre.y = 0.08f;
            r.centre = centre; r.radius = radius; r.width = width; r.color = color;
            r.bornAt = Time.time; r.duration = Mathf.Max(0.05f, duration);
            r.converge = false;
            r.arcHalf = 0f;
            r.live = true;
            r.line.gameObject.SetActive(true);
            DrawRing(r, 0f);
        }

        /// <summary>
        /// M8 Shockwave Belt: a wave that races out of the muzzle across exactly the cone that was hit
        /// (the card had damage and push but nothing on screen).
        /// </summary>
        public void ConeWave(Vector3 origin, Vector3 direction, float angleDegrees, float range, Color color, float duration = 0.26f)
        {
            RingInstance r = null;
            for (int i = 0; i < _rings.Count; i++) if (!_rings[i].live) { r = _rings[i]; break; }
            if (r == null) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;
            origin.y = 0.6f;
            r.centre = origin; r.radius = range; r.width = 0.4f; r.color = color;
            r.bornAt = Time.time; r.duration = Mathf.Max(0.05f, duration);
            r.converge = false;
            r.arcHalf = Mathf.Clamp(angleDegrees * 0.5f, 5f, 180f);
            r.arcHeading = Mathf.Atan2(direction.z, direction.x);
            r.live = true;
            r.line.gameObject.SetActive(true);
            DrawRing(r, 0f);
        }

        /// <summary>
        /// A target lock: a ring that closes in from wide to exactly <paramref name="radius"/> over
        /// <paramref name="duration"/> and brightens as it lands. Used for anything that will hit a
        /// spot later (airstrike, ordnance) so "something is coming HERE" reads before it arrives.
        /// </summary>
        public void Converge(Vector3 centre, float radius, Color color, float duration, float width = 0.1f)
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
                r.line.loop = true;
                float k = t * t;                                   // slow start, snaps shut
                float rr = Mathf.Lerp(r.radius * 1.15f, r.radius, k);
                for (int i = 0; i < RingSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / RingSegments;
                    r.line.SetPosition(i, r.centre + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr));
                }
                // Fades in as it closes: a telegraph is a hint, not a wall of colour.
                var cc = r.color; cc.a *= Mathf.Lerp(0.1f, 0.7f, t);
                r.line.startColor = r.line.endColor = cc;
                r.line.widthMultiplier = r.width;
                return;
            }
            float e = 1f - (1f - t) * (1f - t) * (1f - t);          // fast out, soft landing
            float radius = Mathf.Lerp(r.radius * 0.15f, r.radius, e);
            bool slice = r.arcHalf > 0f;
            r.line.loop = !slice;
            float half = r.arcHalf * Mathf.Deg2Rad;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = slice
                    ? r.arcHeading - half + 2f * half * i / (RingSegments - 1)
                    : i * Mathf.PI * 2f / RingSegments;
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
            sr.sprite = markSprite;
            if (markMaterial != null) sr.sharedMaterial = markMaterial;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            go.SetActive(false);
            return new MarkInstance { tr = go.transform, sprite = sr, live = false };
        }

        // ───────────────────────────────────────────────────────────── chain arc

        /// <summary>
        /// Draws one lightning bolt from <paramref name="from"/> to <paramref name="to"/>: a wide glow,
        /// a white-hot core and up to two short forks, all re-jagged every arcRejagSeconds so it
        /// flickers like electricity, tapered where it touches each enemy, flashing bright and then
        /// fading. <paramref name="delay"/> lets a chain travel hop by hop instead of appearing at once.
        /// </summary>
        public void DrawArc(Vector3 from, Vector3 to, Color? tint = null, float widthScale = 1f, float delay = 0f, int forks = 2)
        {
            var arc = Take();
            if (arc == null) return;   // pool exhausted: drop the visual rather than allocate
            float seed = Random.Range(0f, 97f);
            float now = Time.time;
            var color = tint ?? arcColor;
            float width = arcWidth * widthScale;
            Arm(arc, ArcLayer.Glow, from, to, seed, width, color, now + delay);

            var core = Take();
            if (core != null) Arm(core, ArcLayer.Core, from, to, seed, width * 0.32f, Color.white, now + delay);

            float length = (to - from).magnitude;
            for (int f = 0; f < forks && length > 2f; f++)
            {
                var fork = Take();
                if (fork == null) break;
                fork.forkT = Random.Range(0.25f, 0.75f);
                fork.forkSide = Random.value < 0.5f ? -1f : 1f;
                fork.forkLength = Mathf.Min(1.6f, length * Random.Range(0.18f, 0.3f));
                Arm(fork, ArcLayer.Fork, from, to, seed, width * 0.45f, color, now + delay);
            }
        }

        void Arm(ArcInstance a, ArcLayer layer, Vector3 from, Vector3 to, float seed, float width, Color color, float showAt)
        {
            a.layer = layer; a.from = from; a.to = to; a.seed = seed; a.width = width; a.color = color;
            a.showAt = showAt; a.dieAt = showAt + arcLifetime; a.nextJag = 0f;
            a.live = true;
            // Hidden until its hop is reached, so the chain visibly travels.
            bool now = showAt <= Time.time;
            a.line.gameObject.SetActive(now);
            if (now) Jag(a, Time.time);
        }

        /// Recomputes a live arc's points from its shared seed and the current flicker frame.
        void Jag(ArcInstance a, float now)
        {
            var lr = a.line;
            Vector3 dir = a.to - a.from;
            float length = dir.magnitude;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
            if (side.sqrMagnitude < 1e-5f) side = Vector3.right;
            int segments = Mathf.Clamp(Mathf.RoundToInt(length / 0.55f), arcSegments, 20);
            float jag = Mathf.Clamp(length * 0.08f, arcJitter, 0.9f);
            float frame = Mathf.Floor(now / Mathf.Max(0.01f, arcRejagSeconds));

            if (a.layer == ArcLayer.Fork)
            {
                // A short branch leaving the main bolt at forkT, from the main bolt's own offset there.
                Vector3 root = MainPoint(a, a.forkT, side, jag, frame);
                Vector3 fdir = (dir.normalized + side * a.forkSide * 1.3f).normalized;
                const int fs = 4;
                lr.positionCount = fs + 1;
                for (int i = 0; i <= fs; i++)
                {
                    float t = i / (float)fs;
                    float wob = (Mathf.PerlinNoise(a.seed * 3.1f + i * 1.9f, frame * 0.77f) - 0.5f) * 0.5f * t;
                    lr.SetPosition(i, root + fdir * (a.forkLength * t) + side * wob + Vector3.down * (0.25f * t * t));
                }
            }
            else
            {
                lr.positionCount = segments + 1;
                for (int i = 0; i <= segments; i++)
                    lr.SetPosition(i, MainPoint(a, i / (float)segments, side, jag, frame));
            }
            lr.widthMultiplier = a.width;
        }

        static Vector3 MainPoint(ArcInstance a, float t, Vector3 side, float jag, float frame)
        {
            Vector3 p = Vector3.Lerp(a.from, a.to, t);
            // Zero jitter at both ends so the bolt visibly TOUCHES each enemy.
            float taper = Mathf.Sin(t * Mathf.PI);
            float n = (Mathf.PerlinNoise(a.seed + t * 7.3f, frame * 0.61f) - 0.5f) * 2f;
            // A second, finer octave: jagged, not wavy.
            n += (Mathf.PerlinNoise(a.seed * 1.7f + t * 23f, frame * 0.93f) - 0.5f) * 0.9f;
            float offset = n * jag * taper;
            return p + side * offset + Vector3.up * (offset * 0.3f);
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
        public void MarkEnemy(Transform enemy, Color color, float duration, float height = 2.0f, float scale = 0.35f)
        {
            if (enemy == null) return;
            MarkInstance m = null;
            for (int i = 0; i < _marks.Count; i++) if (!_marks[i].live) { m = _marks[i]; break; }
            if (m == null) return;

            m.follow = enemy;
            m.tr.position = enemy.position + Vector3.up * height;
            m.tr.localScale = Vector3.one * scale;
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
                if (now < a.showAt) continue;                        // waiting for its hop
                if (!a.line.gameObject.activeSelf) a.line.gameObject.SetActive(true);
                if (now >= a.nextJag) { Jag(a, now); a.nextJag = now + arcRejagSeconds; }
                // A bright strike for the first moment, then a fade: the bolt snaps rather than blinks.
                float age = (now - a.showAt) / Mathf.Max(0.01f, arcLifetime);
                float k = age < 0.15f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (age - 0.15f) / 0.85f);
                var c = a.color; c.a *= k;
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
                m.tr.Rotate(0f, 0f, now * 90f % 360f, Space.Self);   // a slow spin: a lock-on, not a sticker
            }
        }

        /// <summary>Diagnostics for the guardrail tests.</summary>
        public int LiveArcs { get { int n = 0; for (int i = 0; i < _arcs.Count; i++) if (_arcs[i].live) n++; return n; } }
        public int ArcCapacity => _arcs.Count;
    }
}
