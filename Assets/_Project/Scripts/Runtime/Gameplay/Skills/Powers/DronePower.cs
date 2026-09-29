using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Drone Buddy (evolution: Drone Squadron) — a gunner drone weaving around the player,
    /// firing three-round bursts through the player's own tracer pipeline.</summary>
    public sealed class DronePower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("Drone model. Children by name: 'Muzzle' (gun tip), 'Rotor*' (spun), and every renderer " +
                     "under 'Glow' takes the rank colour. Empty = the old particle placeholder.")]
            public GameObject model;
            public float modelScale = 1f;
            public ParticleSystem bodyFx;
            public float bodyScale = 0.6f;
            public GameObject tracer;
            public ParticleSystem muzzleFx;
            public ParticleSystem hitFx;
            public float baseDamage = 7f;
            public float range = 11f;
        }

        const int MaxDrones = 3;
        const int BurstShots = 3;
        const float BurstGap = 0.08f;
        static readonly int InkColorId = Shader.PropertyToID("_InkColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        readonly ParticleSystem[] _bodies = new ParticleSystem[MaxDrones];
        readonly Vector3[] _vel = new Vector3[MaxDrones];
        readonly float[] _nextShot = new float[MaxDrones];
        readonly Transform[] _rigs = new Transform[MaxDrones];
        readonly Transform[] _muzzle = new Transform[MaxDrones];
        readonly List<Transform>[] _rotors = new List<Transform>[MaxDrones];
        readonly Renderer[][] _glow = new Renderer[MaxDrones][];
        /// Merged drone model (one mesh): its second material is the rotor smear, tinted by rank.
        readonly Renderer[] _rotorMat = new Renderer[MaxDrones];
        readonly TrailRenderer[] _trail = new TrailRenderer[MaxDrones];
        readonly int[] _burstLeft = new int[MaxDrones];
        int _colourKey = -1;

        Assets A => Lib != null ? Lib.drone : null;

        /// Emissive colour by upgrade: cyan → green → gold, and a hot magenta for the Squadron.
        public static Color Colour(SkillRuntime run)
        {
            // Kept at or just over 1: brighter values bloom and tone-map to white and lose the hue.
            if (run.IsEvolved(SkillCatalogDefs.AutoDrone)) return new Color(1.15f, 0.2f, 1.0f, 1f);
            // Five ranks, three colours: ranks 1-2 cyan, 3-4 green, 5 gold.
            switch ((run.RankOf(SkillCatalogDefs.AutoDrone) + 1) / 2)
            {
                case 0:
                case 1: return new Color(0.15f, 0.8f, 1.1f, 1f);
                case 2: return new Color(0.3f, 1.1f, 0.3f, 1f);
                default: return new Color(1.15f, 0.7f, 0.1f, 1f);
            }
        }

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            if (a == null) return;
            if (a.model == null) { TickParticles(run, p); return; }

            int count = Mathf.Min(MaxDrones, run.DroneCount);
            // Same damage per second as single shots, delivered in bursts of three: bursts read as a gunner.
            float burstInterval = BurstShots / Mathf.Max(0.1f, run.DroneShotsPerSecond);
            float t = Time.time;
            bool squad = run.IsEvolved(SkillCatalogDefs.AutoDrone);
            Color colour = Colour(run);
            int key = squad ? 9 : (run.RankOf(SkillCatalogDefs.AutoDrone) + 1) / 2;

            for (int i = 0; i < MaxDrones; i++)
            {
                bool on = i < count;
                if (on && _rigs[i] == null)
                {
                    _rigs[i] = BuildRig(i);
                    _rigs[i].position = p + Vector3.up * 2.2f;
                    _trail[i].Clear();          // no streak from where the rig was built
                    _nextShot[i] = t + burstInterval * (i + 1) / (count + 1);
                }
                var rig = _rigs[i];
                if (rig == null) continue;
                if (rig.gameObject.activeSelf != on) rig.gameObject.SetActive(on);
                if (!on) continue;

                // Flight: a lazy figure-eight around the player, each drone on its own phase, so the
                // squad weaves instead of sitting still at the shoulder.
                float ph = t * 0.9f + i * 2.1f;
                Vector3 off = new(Mathf.Sin(ph) * 1.9f, 2.2f + Mathf.Sin(t * 3f + i) * 0.12f, Mathf.Sin(ph * 2f) * 0.9f + 0.4f);
                Vector3 before = rig.position;
                rig.position = Vector3.SmoothDamp(rig.position, p + off, ref _vel[i], 0.3f);
                Vector3 vel = (rig.position - before) / Mathf.Max(0.0001f, dt);

                // Aim: turn to the nearest enemy in range, else along the flight; bank into the turn.
                Vector3 look = vel; look.y = 0f;
                int found = TargetQuery.GatherEnemies(rig.position, a.range, Host.EnemyMask);
                int best = TargetQuery.Nearest(found, rig.position);
                ZombieBase target = best >= 0 ? TargetQuery.CandidateEnemy(best) : null;
                if (target != null) { look = target.transform.position - rig.position; look.y = 0f; }
                if (look.sqrMagnitude > 0.001f)
                {
                    var yaw = Quaternion.LookRotation(look.normalized);
                    Vector3 local = Quaternion.Inverse(yaw) * vel;
                    var bank = Quaternion.Euler(Mathf.Clamp(local.z * 6f, -18f, 18f), 0f, Mathf.Clamp(-local.x * 8f, -28f, 28f));
                    rig.rotation = Quaternion.Slerp(rig.rotation, yaw * bank, 1f - Mathf.Exp(-10f * dt));
                }
                foreach (var r in _rotors[i]) r.Rotate(0f, 2200f * dt, 0f, Space.Self);

                if (t < _nextShot[i] || target == null || target.IsDead) continue;
                if (_burstLeft[i] <= 0) _burstLeft[i] = BurstShots;
                _burstLeft[i]--;
                _nextShot[i] = t + (_burstLeft[i] > 0 ? BurstGap : burstInterval - (BurstShots - 1) * BurstGap);
                Shoot(run, _muzzle[i].position, target, colour);
            }

            if (key != _colourKey)
            {
                _colourKey = key;
                var mpb = Host.Block;
                for (int i = 0; i < MaxDrones; i++)
                {
                    if (_rotorMat[i] != null)
                    {
                        mpb.Clear();
                        mpb.SetColor(InkColorId, colour);
                        _rotorMat[i].SetPropertyBlock(mpb, 1);
                    }
                    if (_glow[i] == null) continue;
                    foreach (var r in _glow[i])
                    {
                        mpb.Clear();
                        mpb.SetColor(BladeVisuals.BaseColorId, colour);
                        mpb.SetColor(EmissionId, colour);
                        r.SetPropertyBlock(mpb);
                    }
                }
            }
        }

        Transform BuildRig(int i)
        {
            var a = A;
            var go = UnityEngine.Object.Instantiate(a.model, Host.Root);
            go.name = "drone" + i;
            go.transform.localScale = Vector3.one * a.modelScale;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(col);
            _muzzle[i] = FindChild(go.transform, "Muzzle") ?? go.transform;
            _rotors[i] = new List<Transform>(4);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Rotor")) _rotors[i].Add(t);
            var glow = FindChild(go.transform, "Glow");
            _glow[i] = glow != null ? glow.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            _rotorMat[i] = null;
            if (glow == null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterials.Length > 1) { _rotorMat[i] = r; break; }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            // From the tail, above the hull: a trail from the pivot sat under the body and never showed.
            var tail = new GameObject("trail").transform;
            tail.SetParent(go.transform, false);
            var mesh = go.GetComponentInChildren<MeshFilter>(true);
            if (mesh != null && mesh.sharedMesh != null)
            {
                var bb = mesh.sharedMesh.bounds;
                tail.position = mesh.transform.TransformPoint(new Vector3(bb.center.x, bb.max.y, bb.min.z));
            }
            var trail = tail.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.6f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Always a thin white trail that fades out (owner 2026-09-29).
            trail.startColor = new Color(1f, 1f, 1f, 0.8f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            if (Lib.shared.trailMaterial != null) trail.sharedMaterial = Lib.shared.trailMaterial;
            _trail[i] = trail;
            _colourKey = -1;
            return go.transform;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// The old placeholder: a particle sphere per drone (used when no model is assigned).
        void TickParticles(SkillRuntime run, Vector3 p)
        {
            var a = A;
            int count = Mathf.Min(MaxDrones, run.DroneCount);
            float interval = 1f / Mathf.Max(0.1f, run.DroneShotsPerSecond);
            float t = Time.time;

            for (int i = 0; i < MaxDrones; i++)
            {
                bool on = i < count;
                if (on && _bodies[i] == null && a.bodyFx != null)
                {
                    _bodies[i] = UnityEngine.Object.Instantiate(a.bodyFx, Host.Root);
                    _bodies[i].transform.localScale = Vector3.one * a.bodyScale;
                    _bodies[i].transform.position = p + Vector3.up * 2.2f;
                    _nextShot[i] = t + interval * (i + 1) / (count + 1);   // stagger the squad
                }
                var d = _bodies[i];
                if (d == null) continue;
                if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
                if (!on) continue;

                float fan = count == 1 ? -50f : -80f + i * 80f;
                float ang = (fan + t * 25f) * Mathf.Deg2Rad;
                Vector3 goal = p + new Vector3(Mathf.Cos(ang) * 1.3f, 2.1f + Mathf.Sin(t * 3f + i) * 0.12f, Mathf.Sin(ang) * 1.3f);
                d.transform.position = Vector3.SmoothDamp(d.transform.position, goal, ref _vel[i], 0.18f);

                if (t < _nextShot[i]) continue;
                _nextShot[i] = t + interval;
                int found = TargetQuery.GatherEnemies(d.transform.position, a.range, Host.EnemyMask);
                int best = TargetQuery.Nearest(found, d.transform.position);
                var enemy = best >= 0 ? TargetQuery.CandidateEnemy(best) : null;
                if (enemy != null && !enemy.IsDead) Shoot(run, d.transform.position, enemy, Colour(run));
            }
        }

        void Shoot(SkillRuntime run, Vector3 from, ZombieBase enemy, Color colour)
        {
            var a = A;
            Vector3 to = PowerKit.Chest(enemy);
            // The player's own gun pipeline (muzzle, tracer, impact), tinted with the drone's rank colour.
            if (a.tracer != null) TracerPool.Play(a.tracer, from, to, new Color(colour.r, colour.g, colour.b, 1f), 0.45f);
            FxPool.Play(a.muzzleFx, from, Quaternion.LookRotation(to - from), 0.35f);
            FxPool.Play(a.hitFx, to, PowerKit.Flat(a.hitFx), 0.45f);
            Host.Sfx("sfx.skill.drone", from, 0.3f, 0.05f);

            PowerKit.Hit(enemy, run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoDrone), 0.15f,
                         run.Has(SkillCatalogDefs.EvoSquadron) ? SkillCatalogDefs.EvoSquadron : SkillCatalogDefs.AutoDrone);

            // Drone Squadron: a kill can pay out a coin on the spot.
            if (enemy.IsDead && run.IsEvolved(SkillCatalogDefs.AutoDrone) && UnityEngine.Random.value < 0.1f)
                PickupManager.SpawnBonusCoin(enemy.transform.position, 1);
        }
    }
}
