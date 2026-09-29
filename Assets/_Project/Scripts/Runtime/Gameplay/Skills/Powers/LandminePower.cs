using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Landmines — a mine drops behind the player every few metres walked; the first enemy
    /// to step on one sets it off. Rewards kiting a crowd over your own trail.</summary>
    public sealed class LandminePower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            public GameObject model;
            public float modelScale = 1.4f;
            public ParticleSystem blastFx;
            public float blastNativeRadius = 2f;
            public float baseDamage = 30f;
            public float radius = 2.2f;
            public float trigger = 1f;
            public float armSeconds = 0.4f;
        }

        sealed class Mine { public Transform tr; public Vector3 pos; public float armedAt; public bool live; public float born; }

        const int MaxMines = 8;
        static readonly Color Warn = new(1f, 0.3f, 0.2f, 1f);
        readonly List<Mine> _mines = new(MaxMines);
        Vector3 _last;
        bool _seeded;
        float _walked, _scanAt;

        Assets A => Lib != null ? Lib.landmine : null;

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            if (a == null || a.model == null) return;
            bool has = run.Has(SkillCatalogDefs.AutoLandmine);
            if (!_seeded) { _last = p; _seeded = true; }
            Vector3 d = p - _last; d.y = 0f;
            _last = p;
            if (has)
            {
                _walked += d.magnitude;
                float every = Mathf.Max(0.5f, run.LandmineSpacing);
                if (_walked >= every) { _walked = 0f; Drop(p); }
            }

            float now = Time.time;
            for (int i = 0; i < _mines.Count; i++)
            {
                var m = _mines[i];
                if (!m.live) continue;
                // Blink the red light faster once armed; drop-in pop on spawn.
                float age = now - m.born;
                float s = age < 0.18f ? Mathf.Sin(age / 0.18f * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f) : 1f;
                float blink = now >= m.armedAt ? 1f + 0.08f * Mathf.Max(0f, Mathf.Sin(now * 12f)) : 1f;
                m.tr.localScale = Vector3.one * (a.modelScale * s * blink);
            }

            if (now < _scanAt) return;
            _scanAt = now + 0.1f;
            for (int i = 0; i < _mines.Count; i++)
            {
                var m = _mines[i];
                if (!m.live || now < m.armedAt) continue;
                int found = TargetQuery.GatherEnemies(m.pos, a.trigger, Host.EnemyMask);
                if (found == 0) continue;
                Detonate(run, m);
            }
        }

        void Drop(Vector3 p)
        {
            var a = A;
            Mine m = null;
            Mine oldest = null;
            foreach (var x in _mines)
            {
                if (!x.live) { m = x; break; }
                if (oldest == null || x.born < oldest.born) oldest = x;
            }
            if (m == null && _mines.Count < MaxMines) { m = Build(); _mines.Add(m); }
            if (m == null) { m = oldest; }
            p.y = 0f;
            m.pos = p; m.tr.position = p; m.tr.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            m.born = Time.time; m.armedAt = Time.time + a.armSeconds; m.live = true;
            m.tr.gameObject.SetActive(true);
        }

        Mine Build()
        {
            var a = A;
            var holder = new GameObject("mine").transform;
            holder.SetParent(Host.Root, false);
            var go = UnityEngine.Object.Instantiate(a.model, holder);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            holder.gameObject.SetActive(false);
            return new Mine { tr = holder };
        }

        void Detonate(SkillRuntime run, Mine m)
        {
            var a = A;
            m.live = false;
            m.tr.gameObject.SetActive(false);
            float r = a.radius * run.AreaMultiplier;
            PowerKit.PlaySized(a.blastFx, m.pos + Vector3.up * 0.1f, r, a.blastNativeRadius, 1.3f);
            Host.Shockwave(m.pos, 0.3f, r, Warn, 0.4f);
            Host.Sfx("sfx.skill.blast", m.pos, 0.7f, 0.06f);
            Host.Shake(0.08f);
            float damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoLandmine);
            int found = TargetQuery.GatherEnemies(m.pos, r, Host.EnemyMask);
            for (int k = 0; k < found; k++) PowerKit.Hit(TargetQuery.Candidate(k), damage, 1.2f, SkillCatalogDefs.AutoLandmine);
        }

        public override void ResetForRun()
        {
            foreach (var m in _mines) { m.live = false; if (m.tr != null) m.tr.gameObject.SetActive(false); }
            _walked = 0f; _seeded = false;
        }
    }
}
