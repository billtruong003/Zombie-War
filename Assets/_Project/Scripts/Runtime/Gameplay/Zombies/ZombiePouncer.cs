using System.Collections;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// A runner that closes the last stretch with a leap instead of jogging into melee.
    ///
    /// The fantasy: a pack animal that commits. The pounce covers ground the player cannot outrun,
    /// but it is telegraphed and it ends in a recovery window where the beast is stationary and
    /// takes hits - so baiting a pounce and stepping aside is the counterplay.
    ///
    /// Sequence: Crouch (telegraph, stationary) -> Leap (fast, committed, damages on arrival)
    ///           -> Recover (stationary, vulnerable) -> cooldown.
    ///
    /// Motion is driven through the planar motor under explicit external control, so the committed
    /// leap cannot be bent mid-air by crowd separation and always lands back on the gameplay plane.
    /// </summary>
    public sealed class ZombiePouncer : ZombieRunner
    {
        private enum Phase { None, Crouch, Leap, Recover }

        [Header("Pounce")]
        [SerializeField] private float pounceCooldown = 5f;
        [Tooltip("Band the player must be in to trigger a pounce - too close and it just bites.")]
        [SerializeField] private float pounceMinRange = 3f;
        [SerializeField] private float pounceMaxRange = 8f;
        [Tooltip("Stationary wind-up. This is the player's read on the leap.")]
        [SerializeField] private float crouchDuration = 0.35f;
        [SerializeField] private float leapSpeed = 12f;
        [SerializeField] private float leapDuration = 0.45f;
        [Tooltip("Stationary, fully vulnerable window after landing. The punish opportunity.")]
        [SerializeField] private float recoverDuration = 0.5f;
        [SerializeField] private float pounceRadius = 1.9f;
        [SerializeField] private float pounceDamageMultiplier = 1.5f;

        private Phase _phase = Phase.None;
        private float _cooldownTimer;
        private Coroutine _pounce;
        private bool _holdsSlot;

        // ── 05/10 genre rule 2: pounces are a capped share, never a constant stream ─────────────
        // Each dog used to pounce on its own 5 s timer, so a pack of ten leapt every half second.
        // Now the whole map shares a few pounce slots, opening up as the run goes on.
        public const float NoPounceBefore = 60f, OneSlotUntil = 120f;
        public const int SlotsLater = 2;
        public const float MinWindup = 0.5f;   // the red line must be readable
        static int _activePounces;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _activePounces = 0;

        /// <summary>Pounces allowed at once, <paramref name="runSeconds"/> into a run.</summary>
        public static int PounceSlotsAt(float runSeconds) =>
            runSeconds < NoPounceBefore ? 0 : runSeconds < OneSlotUntil ? 1 : SlotsLater;

        public static int ActivePounces => _activePounces;

        protected override bool SuppressBaseFsm => _phase != Phase.None;

        protected override void OnSpawned()
        {
            StopPounce();
            _phase = Phase.None;
            _cooldownTimer = pounceCooldown * 0.5f;   // don't let a fresh spawn pounce instantly
        }

        protected override void OnDespawned() => StopPounce();

        protected override void OnFullTick(Transform player, float distance)
        {
            if (_phase != Phase.None) return;

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer > 0f) return;
            if (string.IsNullOrEmpty(Data.specialClip)) return;
            if (distance < pounceMinRange || distance > pounceMaxRange) return;
            if (_activePounces >= PounceSlotsAt(RunState.Current?.Duration ?? 0f))
            {
                _cooldownTimer = Random.Range(0.4f, 1.2f);   // wait for a slot without polling every frame
                return;
            }

            _activePounces++;
            _holdsSlot = true;
            _pounce = StartCoroutine(Pounce(player));
        }

        private IEnumerator Pounce(Transform player)
        {
            _cooldownTimer = pounceCooldown;
            CancelPendingAttack();

            // ---- crouch: commit to a direction, then stop steering -------------------------
            _phase = Phase.Crouch;
            Motor.BeginExternalControl();
            Vat.CrossFade(Data.specialClip, 0.1f);
            PlayAttackAudio();

            Vector3 aim = FlattenY(player.position - transform.position).normalized;
            if (aim.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(aim);

            float windup = Mathf.Max(MinWindup, Data.specialWindup > 0f ? Data.specialWindup : crouchDuration);
            ShowLine(true);
            yield return new WaitForSeconds(windup);
            ShowLine(false);
            if (CurrentState == State.Dead) { _phase = Phase.None; ReleaseSlot(); yield break; }

            // ---- leap: committed to the direction chosen at crouch, no mid-air steering ----
            _phase = Phase.Leap;
            float t = 0f;
            while (t < leapDuration)
            {
                t += Time.deltaTime;
                if (CurrentState == State.Dead) break;
                Motor.Move(aim * leapSpeed * Time.deltaTime);
                yield return null;
            }

            if (CurrentState != State.Dead)
                DealAreaDamage(transform.position, pounceRadius, Damage * pounceDamageMultiplier);

            // ---- recover: the punish window ------------------------------------------------
            _phase = Phase.Recover;
            Vat.CrossFade(Data.idleClip, 0.12f);
            yield return new WaitForSeconds(recoverDuration);

            _phase = Phase.None;
            Motor.EndExternalControl();
            _pounce = null;
            ReleaseSlot();
        }

        private void StopPounce()
        {
            if (_pounce != null) { StopCoroutine(_pounce); _pounce = null; }
            ShowLine(false);
            ReleaseSlot();
        }

        void ReleaseSlot()
        {
            if (!_holdsSlot) return;
            _holdsSlot = false;
            _activePounces = Mathf.Max(0, _activePounces - 1);
        }

        // ── The red line on the ground along the leap, shown through the crouch ──────────────────
        static Mesh _lineMesh;
        static Material _lineMaterial;
        static bool _lineLoaded;
        GameObject _line;

        void ShowLine(bool on)
        {
            if (!on) { if (_line != null) _line.SetActive(false); return; }
            if (!_lineLoaded) { _lineMaterial = Resources.Load<Material>("FX/M_PounceLine"); _lineLoaded = true; }
            if (_lineMaterial == null) return;
            if (_line == null)
            {
                if (_lineMesh == null)
                {
                    _lineMesh = new Mesh { name = "PounceLine" };
                    _lineMesh.SetVertices(new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, 1f), new Vector3(-0.5f, 0f, 1f) });
                    _lineMesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                    _lineMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                    _lineMesh.RecalculateBounds();
                }
                _line = new GameObject("PounceLine");
                _line.transform.SetParent(transform, false);
                _line.AddComponent<MeshFilter>().sharedMesh = _lineMesh;
                var mr = _line.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _lineMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            var s = transform.lossyScale;
            _line.transform.localPosition = new Vector3(0f, 0.05f / Mathf.Max(0.01f, s.y), 0f);
            _line.transform.localRotation = Quaternion.identity;
            _line.transform.localScale = new Vector3(0.7f / Mathf.Max(0.01f, s.x), 1f, leapSpeed * leapDuration / Mathf.Max(0.01f, s.z));
            _line.SetActive(true);
        }
    }
}
