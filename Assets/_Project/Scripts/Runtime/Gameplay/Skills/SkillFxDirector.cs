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
        [SerializeField] private float arcWidth = 0.12f;
        [SerializeField] private float arcLifetime = 0.18f;
        [Tooltip("Sideways jitter per segment, so the bolt reads as electricity rather than a ruler line.")]
        [SerializeField] private float arcJitter = 0.22f;
        [SerializeField] private int arcSegments = 6;
        [Tooltip("Hard ceiling on simultaneously visible arcs. 6 arcs/proc x 2 procs/s needs no more.")]
        [SerializeField] private int arcPoolSize = 12;

        [Header("Status marks (world-space, so no UI prefab is touched)")]
        [SerializeField] private Material markMaterial;
        [SerializeField] private int markPoolSize = 24;

        readonly List<ArcInstance> _arcs = new(12);
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
        public void DrawArc(Vector3 from, Vector3 to, Color? tint = null)
        {
            var arc = Take();
            if (arc == null) return;   // pool exhausted: drop the visual rather than allocate

            var lr = arc.line;
            Vector3 dir = to - from;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
            if (side.sqrMagnitude < 1e-5f) side = Vector3.right;

            for (int i = 0; i <= arcSegments; i++)
            {
                float t = i / (float)arcSegments;
                Vector3 p = Vector3.Lerp(from, to, t);
                // Zero jitter at both ends so the bolt visibly TOUCHES each enemy; jitter in between
                // so it reads as electricity.
                float taper = Mathf.Sin(t * Mathf.PI);
                float offset = (Mathf.PerlinNoise(i * 3.17f, Time.time * 12f) - 0.5f) * 2f * arcJitter * taper;
                lr.SetPosition(i, p + side * offset + Vector3.up * (offset * 0.35f));
            }

            var c = tint ?? arcColor;
            lr.startColor = lr.endColor = c;
            lr.gameObject.SetActive(true);
            arc.live = true;
            arc.dieAt = Time.time + arcLifetime;
        }

        ArcInstance Take()
        {
            for (int i = 0; i < _arcs.Count; i++) if (!_arcs[i].live) return _arcs[i];
            return null;
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
