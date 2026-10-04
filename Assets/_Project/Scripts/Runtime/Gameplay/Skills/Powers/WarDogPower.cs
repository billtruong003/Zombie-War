using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>
    /// War Dog — a companion that runs to the nearest enemy and bites, then comes back to heel when
    /// nothing is close (two dogs at rank 5). Reuses the DogPup's baked VAT body and clips, recoloured
    /// steel blue with a cyan ring at its feet so it is never mistaken for an enemy pup.
    /// </summary>
    public sealed class WarDogPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("The DogPup's VAT 'Visual' (mesh + VAT_Animator), with the War Dog material.")]
            public GameObject body;
            public float bodyScale = 1.35f;
            public ParticleSystem biteFx;
            public float biteDamage = 18f;
            public float speed = 7.5f;
            public float leash = 11f;
            public float biteReach = 1.1f;
        }

        sealed class Dog
        {
            public Transform tr;
            public VAT_Animator anim;
            public MeshRenderer ring;
            public ZombieBase target;
            public float nextBite, biteUntil;
            public string clip;
        }

        const int MaxDogs = 3;
        const float AlphaHeal = 0.004f;   // share of max health per bite
        Health _health;
        const string Idle = "Idle", Run = "Walk Forward In Place", Bite = "Bite Attack";
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RingId = Shader.PropertyToID("_Ring");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        readonly List<Dog> _dogs = new(MaxDogs);

        Assets A => Lib != null ? Lib.warDog : null;

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            int want = a == null || a.body == null ? 0 : run.WarDogCount;
            while (_dogs.Count < want) _dogs.Add(Build(p, _dogs.Count));
            for (int i = 0; i < _dogs.Count; i++)
            {
                var d = _dogs[i];
                bool on = i < want;
                if (d.tr.gameObject.activeSelf != on)
                {
                    d.tr.gameObject.SetActive(on);
                    if (on) d.tr.position = p + new Vector3(i == 0 ? 1f : -1f, 0f, -0.5f);
                }
                if (on) Step(run, d, i, p, dt);
            }
        }

        void Step(SkillRuntime run, Dog d, int index, Vector3 p, float dt)
        {
            var a = A;
            float now = Time.time;
            Vector3 pos = d.tr.position;

            // Keep a live target near the player; drop one that wandered off the leash.
            if (d.target == null || d.target.IsDead || !d.target.isActiveAndEnabled || (d.target.transform.position - p).sqrMagnitude > a.leash * a.leash)
            {
                d.target = null;
                int found = TargetQuery.GatherEnemies(pos, a.leash);
                int best = TargetQuery.Nearest(found, pos);
                if (best >= 0) d.target = TargetQuery.CandidateEnemy(best);
            }

            Vector3 goal = d.target != null ? d.target.transform.position
                                            : p + new Vector3(index == 0 ? 1.2f : -1.2f, 0f, -0.8f);
            Vector3 to = goal - pos; to.y = 0f;
            float dist = to.magnitude;
            bool biting = now < d.biteUntil;
            if (!biting)
            {
                float stop = d.target != null ? a.biteReach * 0.8f : 0.4f;
                if (dist > stop)
                {
                    pos += to / dist * Mathf.Min(dist - stop, a.speed * dt);
                    d.tr.position = new Vector3(pos.x, 0f, pos.z);
                }
                if (dist > 0.05f)
                    d.tr.rotation = Quaternion.Slerp(d.tr.rotation, Quaternion.LookRotation(to / dist), 1f - Mathf.Exp(-12f * dt));
                Play(d, dist > stop + 0.05f ? Run : Idle);
            }

            if (d.target != null && dist <= a.biteReach && now >= d.nextBite)
            {
                d.nextBite = now + 1f / Mathf.Max(0.2f, run.WarDogBitesPerSecond);
                d.biteUntil = now + 0.35f;
                Play(d, Bite, true);
                bool alpha = run.IsEvolved(SkillCatalogDefs.AutoWarDog);
                PowerKit.Hit(d.target, run.PowerDamage(a.biteDamage, SkillCatalogDefs.AutoWarDog), 0.4f, alpha ? SkillCatalogDefs.EvoAlphaPack : SkillCatalogDefs.AutoWarDog);
                if (alpha)
                {
                    // Alpha Pack: every bite feeds the player a little.
                    if (_health == null) _health = Host.Player.GetComponent<Health>();
                    if (_health != null && _health.Current < _health.Max) _health.Heal(_health.Max * AlphaHeal);
                }
                FxPool.Play(a.biteFx, PowerKit.Chest(d.target) - Vector3.up * 0.3f, PowerKit.Flat(a.biteFx), 0.45f);
                Host.Sfx("sfx.creature.canine_small.attack", pos, 0.5f, 0.15f);
            }

            if (d.ring != null)
            {
                d.ring.transform.position = new Vector3(d.tr.position.x, 0.04f, d.tr.position.z);
                var mpb = Host.Block; mpb.Clear();
                mpb.SetColor(ColorId, new Color(0.3f, 0.85f, 1f, 0.7f));
                mpb.SetFloat(RingId, 0.12f); mpb.SetFloat(FillId, 1f);
                d.ring.SetPropertyBlock(mpb);
            }
        }

        static void Play(Dog d, string clip, bool restart = false)
        {
            if (d.anim == null || (!restart && d.clip == clip)) return;
            d.clip = clip;
            if (restart) d.anim.Play(clip); else d.anim.CrossFade(clip, 0.1f);
        }

        Dog Build(Vector3 p, int index)
        {
            var a = A;
            var holder = new GameObject("warDog" + index).transform;
            holder.SetParent(Host.Root, false);
            holder.position = p;
            var body = UnityEngine.Object.Instantiate(a.body, holder);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one * a.bodyScale;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            var ring = Host.MakeGroundRenderer("warDogRing");
            if (ring != null) { ring.transform.localScale = new Vector3(1.3f, 1f, 1.3f); ring.gameObject.SetActive(true); ring.transform.SetParent(holder, true); }
            var d = new Dog { tr = holder, anim = body.GetComponentInChildren<VAT_Animator>(true), ring = ring };
            Play(d, Idle);
            return d;
        }

        public override void ResetForRun()
        {
            foreach (var d in _dogs) { d.target = null; if (d.tr != null) d.tr.gameObject.SetActive(false); }
        }
    }
}
