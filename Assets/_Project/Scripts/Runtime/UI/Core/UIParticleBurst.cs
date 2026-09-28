using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Particles for Screen Space Overlay UI (the Gacha reveal): Particle Systems do not draw on an
    /// overlay canvas, so these are pooled Images moved in unscaled time (works while the game is
    /// paused). Emit a burst with <see cref="Emit"/>; each particle has speed, gravity, drag, spin,
    /// size over life and fade. Additive-looking glow comes from the soft sprites.
    /// </summary>
    public sealed class UIParticleBurst : MonoBehaviour
    {
        public struct Burst
        {
            public Sprite sprite;
            public int count;
            public Color colorA, colorB;
            public Vector2 speed;          // min..max, px/s (canvas units)
            public Vector2 size;           // min..max start size, px
            public Vector2 life;           // min..max seconds
            public float gravity;          // px/s^2, positive pulls down
            public float drag;             // per second
            public float spin;             // max deg/s either way
            public float spread;           // degrees around direction (360 = all round)
            public float direction;        // degrees, 90 = up
            public float endScale;         // size multiplier at end of life
            public Vector2 area;           // random start offset half-extents
        }

        sealed class P
        {
            public RectTransform rt; public Image img;
            public Vector2 pos, vel; public float age, life, size, endScale, spin, gravity, drag; public Color color;
        }

        readonly List<P> _live = new();
        readonly Stack<P> _pool = new();

        public int Live => _live.Count;

        public void Emit(Vector2 localPos, Burst b)
        {
            for (int i = 0; i < b.count; i++)
            {
                var p = _pool.Count > 0 ? _pool.Pop() : Make();
                p.rt.gameObject.SetActive(true);
                p.img.sprite = b.sprite;
                float ang = (b.direction + Random.Range(-b.spread, b.spread) * 0.5f) * Mathf.Deg2Rad;
                float sp = Random.Range(b.speed.x, b.speed.y);
                p.pos = localPos + new Vector2(Random.Range(-b.area.x, b.area.x), Random.Range(-b.area.y, b.area.y));
                p.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
                p.age = 0f; p.life = Mathf.Max(0.05f, Random.Range(b.life.x, b.life.y));
                p.size = Random.Range(b.size.x, b.size.y); p.endScale = b.endScale;
                p.spin = Random.Range(-b.spin, b.spin); p.gravity = b.gravity; p.drag = b.drag;
                p.color = Color.Lerp(b.colorA, b.colorB, Random.value);
                p.rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                p.rt.SetAsLastSibling();
                _live.Add(p);
                Step(p, 0f);
            }
        }

        public void Clear()
        {
            foreach (var p in _live) { p.rt.gameObject.SetActive(false); _pool.Push(p); }
            _live.Clear();
        }

        P Make()
        {
            var go = new GameObject("p", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>(); img.raycastTarget = false;
            return new P { rt = rt, img = img };
        }

        void Update()
        {
            float dt = GachaSpotlight.DebugStep > 0f ? GachaSpotlight.DebugStep : Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.age += dt;
                if (p.age >= p.life) { p.rt.gameObject.SetActive(false); _pool.Push(p); _live.RemoveAt(i); continue; }
                p.vel.y -= p.gravity * dt;
                p.vel *= Mathf.Exp(-p.drag * dt);
                p.pos += p.vel * dt;
                p.rt.localRotation *= Quaternion.Euler(0, 0, p.spin * dt);
                Step(p, dt);
            }
        }

        static void Step(P p, float dt)
        {
            float k = p.age / p.life;
            float s = p.size * Mathf.Lerp(1f, p.endScale, k);
            p.rt.sizeDelta = new Vector2(s, s * (p.img.sprite != null ? p.img.sprite.rect.height / Mathf.Max(1f, p.img.sprite.rect.width) : 1f));
            p.rt.anchoredPosition = p.pos;
            float o = Mathf.InverseLerp(0.55f, 1f, k); o = o * o * (3f - 2f * o);
            var c = p.color; c.a *= k < 0.15f ? k / 0.15f : 1f - o;
            p.img.color = c;
        }

        void OnDisable() => Clear();
    }
}
