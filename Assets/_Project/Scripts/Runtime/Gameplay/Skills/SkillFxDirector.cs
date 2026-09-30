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

        [Header("Chain arc: a pooled line drawn with Epic Toon's lightning material (the pack has no point-to-point beam)")]
        [Tooltip("Epic Toon Materials/Misc/Lightning/lightning1_ADD: the bolt is in the texture, stretched from end to end.")]
        [SerializeField] private Material arcMaterial;
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
        bool TexturedBolt => arcMaterial != null && arcMaterial.mainTexture != null;

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
            if (TexturedBolt) return;   // Epic Toon's texture already is the bolt: no hand-drawn core or forks

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
            // A textured bolt stays nearly straight (the texture carries the zig-zag); only a slight bend
            // on each flicker frame keeps it alive.
            int segments = TexturedBolt ? 2 : Mathf.Clamp(Mathf.RoundToInt(length / 0.55f), arcSegments, 20);
            float jag = TexturedBolt ? Mathf.Min(0.2f, length * 0.04f) : Mathf.Clamp(length * 0.08f, arcJitter, 0.9f);
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
