using System;
using System.Collections;
using BillGameCore;
using UnityEngine;

namespace ZombieWar.Bosses
{
    /// <summary>
    /// The Titan (backlog #39–#43, boss lab prototype 06/10): a Kaiju that walks slower than the player
    /// (genre rule 3) and cycles three telegraphed attacks (rule 8):
    /// <list type="bullet">
    /// <item><b>Ground slam</b> — a red circle fills for <see cref="slamWindup"/> s, then everything in it
    /// takes <see cref="slamDamage"/> (stomp ring + camera shake).</item>
    /// <item><b>Charge</b> — a red strip toward the player fills, then the Titan rushes down it; touching it
    /// hurts.</item>
    /// <item><b>Call</b> — a roar that brings <see cref="callCount"/> crowd enemies around it.</item>
    /// </list>
    /// After <see cref="enrageAfter"/> s it enrages: faster cadence and a red tint. Damage from the gun
    /// arrives through <see cref="IDamageable"/>; it is a lab prototype, not yet an enemy the skills see.
    /// </summary>
    public sealed class TitanBoss : MonoBehaviour, IDamageable, ITargetable
    {
        [Header("Body")]
        public float maxHealth = 6000f;
        public float walkSpeed = 3.2f;          // player runs 5
        public float contactDamage = 12f;
        public float contactRadius = 2.2f;

        [Header("Ground slam")]
        public float slamRadius = 5.5f, slamWindup = 1.3f, slamDamage = 30f;
        [Header("Charge")]
        public float chargeLength = 14f, chargeWidth = 3.2f, chargeWindup = 1.1f, chargeSpeed = 13f, chargeDamage = 25f;
        [Header("Call")]
        public int callCount = 8;
        public float callCooldown = 18f;
        [Header("Rhythm")]
        public float restBetween = 1.8f;
        public float enrageAfter = 90f, enrageCadence = 0.6f, enrageSpeed = 1.25f;

        public float Health { get; private set; }
        public float Max => maxHealth;
        public bool Dead { get; private set; }
        public bool Enraged { get; private set; }
        public string Phase { get; private set; } = "idle";

        public static event Action<TitanBoss> Spawned, Died;

        public Transform Transform => transform;
        public bool IsTargetable => !Dead && isActiveAndEnabled;

        Animator _anim;
        float _born, _nextCallAt, _nextContactAt;
        static readonly int SpeedId = Animator.StringToHash("Speed");

        void OnEnable()
        {
            Health = maxHealth; Dead = false; Enraged = false;
            _anim = GetComponentInChildren<Animator>();
            _born = Time.time; _nextCallAt = Time.time + 8f;
            TargetRegistry.Register(this);
            Spawned?.Invoke(this);
            StartCoroutine(Brain());
        }

        void OnDisable() => TargetRegistry.Unregister(this);

        public void TakeDamage(float amount)
        {
            if (Dead || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Health > 0f) return;
            Dead = true;
            StopAllCoroutines();
            Trigger("Die");
            Phase = "dead";
            Died?.Invoke(this);
            PickupManager.Instance?.SpawnChest(transform.position + transform.forward * 2f);
            Destroy(gameObject, 4f);
        }

        IEnumerator Brain()
        {
            Trigger("Roar");
            Phase = "entrance";
            yield return new WaitForSeconds(2.2f);
            while (!Dead)
            {
                if (!Enraged && Time.time - _born >= enrageAfter) Enrage();
                var player = PlayerMovement.Instance;
                if (player == null) { yield return null; continue; }

                // Walk in until close enough to slam, or give up after a few seconds and use range.
                float until = Time.time + 3.5f;
                Phase = "walk";
                while (!Dead && Time.time < until && Flat(player.transform.position - transform.position).magnitude > slamRadius * 0.8f)
                {
                    Step(player.transform.position, walkSpeed * (Enraged ? enrageSpeed : 1f));
                    yield return null;
                }
                SetSpeed(0f);
                if (Dead) yield break;

                float d = Flat(player.transform.position - transform.position).magnitude;
                if (Time.time >= _nextCallAt) yield return Call();
                else if (d <= slamRadius * 1.1f) yield return Slam();
                else yield return Charge(player.transform.position);
                yield return new WaitForSeconds(restBetween * (Enraged ? enrageCadence : 1f));
            }
        }

        IEnumerator Slam()
        {
            Phase = "slam";
            float wind = slamWindup * (Enraged ? 0.8f : 1f);
            GroundTelegraph.Show(GroundTelegraph.Shape.Circle, transform.position, Quaternion.identity, new Vector2(slamRadius, 0f), wind);
            Trigger("Slam");
            yield return new WaitForSeconds(wind);
            HitCircle(transform.position, slamRadius, slamDamage);
            Bill.Audio?.PlayCue("sfx.skill.evolve", transform.position, SfxPriority.High, 0.9f);
            FindFirstObjectByType<CameraFollow>()?.Shake(0.5f);
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Charge(Vector3 target)
        {
            Phase = "charge";
            Vector3 dir = Flat(target - transform.position).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            float wind = chargeWindup * (Enraged ? 0.8f : 1f);
            GroundTelegraph.Show(GroundTelegraph.Shape.Strip, transform.position, transform.rotation, new Vector2(chargeWidth, chargeLength), wind);
            Trigger("Charge");
            yield return new WaitForSeconds(wind);
            float run = 0f; bool hit = false;
            while (run < chargeLength && !Dead)
            {
                float step = chargeSpeed * Time.deltaTime;
                Vector3 next = transform.position + dir * step;
                if (WorldNav.MapNavigator.Blocked(next)) break;
                transform.position = next; run += step;
                SetSpeed(2f);
                var p = PlayerMovement.Instance;
                if (!hit && p != null && Flat(p.transform.position - transform.position).magnitude < chargeWidth * 0.6f)
                {
                    hit = true;
                    p.GetComponentInParent<IDamageable>()?.TakeDamage(chargeDamage);
                }
                yield return null;
            }
            SetSpeed(0f);
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator Call()
        {
            Phase = "call";
            _nextCallAt = Time.time + callCooldown * (Enraged ? enrageCadence : 1f);
            Trigger("Roar");
            yield return new WaitForSeconds(1.2f);
            var director = Threat.ThreatDirector.Instance;
            var spawner = FindFirstObjectByType<ZombieSpawner>();
            if (director != null && spawner != null)
                for (int i = 0; i < callCount; i++)
                {
                    var data = director.PickCrowdFodder();
                    if (data != null) spawner.Spawn(data);
                }
            yield return new WaitForSeconds(1.0f);
        }

        void Enrage()
        {
            Enraged = true;
            foreach (var r in GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials) if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * new Color(1.3f, 0.6f, 0.6f));
            Trigger("Roar");
        }

        void Update()
        {
            if (Dead) return;
            var p = PlayerMovement.Instance;
            if (p == null || Time.time < _nextContactAt) return;
            if (Flat(p.transform.position - transform.position).magnitude < contactRadius)
            {
                _nextContactAt = Time.time + 0.6f;
                p.GetComponentInParent<IDamageable>()?.TakeDamage(contactDamage);
            }
        }

        void Step(Vector3 to, float speed)
        {
            Vector3 dir = Flat(to - transform.position);
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            Vector3 next = transform.position + dir * speed * Time.deltaTime;
            if (!WorldNav.MapNavigator.Blocked(next)) transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 6f * Time.deltaTime);
            SetSpeed(1f);
        }

        static void HitCircle(Vector3 at, float radius, float damage)
        {
            var p = PlayerMovement.Instance;
            if (p != null && Flat(p.transform.position - at).magnitude <= radius)
                p.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
        }

        void Trigger(string name) { if (_anim != null && _anim.runtimeAnimatorController != null) _anim.SetTrigger(name); }
        void SetSpeed(float v) { if (_anim != null && _anim.runtimeAnimatorController != null) _anim.SetFloat(SpeedId, v); }
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
