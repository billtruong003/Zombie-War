using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Storm Cloud — a thundercloud trails above the player and strikes the nearest enemy
    /// below it with lightning, over and over.</summary>
    public sealed class StormCloudPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            public GameObject model;
            public float modelScale = 1.1f;
            public ParticleSystem strikeFx;
            public float baseDamage = 16f;
            public float range = 7f;
            public float height = 3.4f;
        }

        static readonly Color Cyan = new(0.4f, 0.8f, 1f, 1f);
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RingId = Shader.PropertyToID("_Ring");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        Transform _cloud;
        Vector3 _vel;
        float _nextStrike;
        MeshRenderer _shadow;

        Assets A => Lib != null ? Lib.stormCloud : null;

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            bool on = a != null && a.model != null && run.Has(SkillCatalogDefs.AutoStormCloud);
            if (!on) { Show(false); return; }
            if (_cloud == null) Build();
            if (!_cloud.gameObject.activeSelf) { Show(true); _cloud.position = p + Vector3.up * a.height; }

            // Lags behind the player a little and bobs: weather, not a hat.
            Vector3 goal = p + new Vector3(Mathf.Sin(Time.time * 0.7f) * 0.6f, a.height + Mathf.Sin(Time.time * 1.3f) * 0.15f, 0.8f);
            _cloud.position = Vector3.SmoothDamp(_cloud.position, goal, ref _vel, 0.45f);
            _cloud.Rotate(0f, 12f * dt, 0f, Space.World);
            if (_shadow != null)
            {
                _shadow.transform.position = new Vector3(_cloud.position.x, 0.05f, _cloud.position.z);
                var mpb = Host.Block; mpb.Clear();
                mpb.SetColor(ColorId, new Color(0.05f, 0.07f, 0.12f, 0.35f));
                mpb.SetFloat(RingId, 0f); mpb.SetFloat(FillId, 1f);
                _shadow.SetPropertyBlock(mpb);
            }

            float now = Time.time;
            if (now < _nextStrike) return;
            Vector3 below = new(_cloud.position.x, 0f, _cloud.position.z);
            int found = TargetQuery.GatherEnemies(below, a.range, Host.EnemyMask);
            int best = TargetQuery.Nearest(found, below);
            var target = best >= 0 ? TargetQuery.CandidateEnemy(best) : null;
            if (target == null || target.IsDead) { _nextStrike = now + 0.15f; return; }
            _nextStrike = now + run.StormCloudInterval;

            Vector3 at = target.transform.position;
            FxPool.Play(a.strikeFx, at, PowerKit.Flat(a.strikeFx), 1f);
            SkillFxDirector.Instance?.DrawArc(_cloud.position, at + Vector3.up * 1f, Cyan, 0.9f, 0f, 1);
            SkillFxDirector.Instance?.Pulse(at, 1f, Cyan, 0.2f, 0.12f);
            Host.Sfx("sfx.skill.chain", at, 0.5f, 0.12f);
            PowerKit.Hit(target, run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoStormCloud), 0.3f, SkillCatalogDefs.AutoStormCloud);
        }

        void Build()
        {
            var a = A;
            var holder = new GameObject("stormCloud").transform;
            holder.SetParent(Host.Root, false);
            var m = UnityEngine.Object.Instantiate(a.model, holder);
            m.transform.localScale = Vector3.one * a.modelScale;
            foreach (var c in m.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            foreach (var r in m.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            _cloud = holder;
            _shadow = Host.MakeGroundRenderer("stormShadow");
            if (_shadow != null) _shadow.transform.localScale = new Vector3(2f, 1f, 2f);
        }

        void Show(bool on)
        {
            if (_cloud != null && _cloud.gameObject.activeSelf != on) _cloud.gameObject.SetActive(on);
            if (_shadow != null && _shadow.gameObject.activeSelf != on) _shadow.gameObject.SetActive(on);
        }

        public override void ResetForRun() => Show(false);
    }
}
