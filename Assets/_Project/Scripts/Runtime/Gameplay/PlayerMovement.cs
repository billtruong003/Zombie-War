using UnityEngine;

namespace ZombieWar
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private VirtualJoystick joystick;
        [SerializeField] private Animator animator;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float bodyRotationSpeed = 720f;
        [Tooltip("Auto-aim acquisition radius. Must stay close to what the portrait camera actually " +
                 "shows on the ground (~6 m forward, ~4 m sideways) or the player auto-fires at " +
                 "enemies that are entirely off-screen. Weapon ranges are tuned into this same band.")]
        [SerializeField] private float aimRange = 11f;
        [SerializeField] private float animDamp = 0.1f;

                [Header("Auto-aim (nearest zombie)")]
        [Tooltip("A new zombie must be this many metres CLOSER than the current lock to steal aim (anti-jitter).")]
        [SerializeField] private float aimStickiness = 0.75f;
        [Tooltip("Minimum seconds locked on a target before we're allowed to switch (anti-jitter).")]
        [SerializeField] private float aimMinDwell = 0.2f;
        [Tooltip("Seconds between target recomputes (cheap: not every frame).")]
        [SerializeField] private float aimRecomputeInterval = 0.1f;
        [Tooltip("Zombies inside this radius are an immediate threat: the nearest one ALWAYS wins the aim lock, ignoring dwell/stickiness.")]
        [SerializeField] private float aimDangerRadius = 7f;

        private Rigidbody _rb;
        private ITargetable _aimTarget;
        private float _dwellTimer;
        private float _recomputeTimer;
        private Vector3 _desiredAim = Vector3.forward;

        public static PlayerMovement Instance { get; private set; }

        // Direction the upper body/gun should aim - nearest zombie in range, falling back to
        // movement direction. Body rotation (below) and aim rotation are deliberately independent:
        // the two-axis feel is what makes strafing-while-shooting read correctly.
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

        // Weapon.cs auto-fires off these instead of a fire button - no manual shoot input at all,
        // moving into weapon range is what triggers firing.
        public bool HasTarget { get; private set; }
        public float AimTargetDistance { get; private set; } = float.MaxValue;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable() => Instance = this;
        private void OnDisable() { if (Instance == this) Instance = null; }

        // Wired at runtime by PlayerSpawner, since the player is instantiated into the map (not baked)
        // and the joystick lives on the persistent HUD.
        public void SetJoystick(VirtualJoystick j) => joystick = j;

        private void FixedUpdate()
        {
            // Mobile joystick is the primary input; fall back to keyboard (WASD/arrows) when there's
            // no joystick assigned or it's idle, so the map is playable on desktop for dev/QA.
            Vector2 input = joystick != null ? joystick.Direction : Vector2.zero;
            if (input.sqrMagnitude < 0.01f)
                input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 move = new Vector3(input.x, 0f, input.y);
            _rb.MovePosition(_rb.position + move * moveSpeed * Time.fixedDeltaTime);

            // Aim first: the body faces the AIM axis, NOT the move axis. This twin-stick decoupling is
            // what lets the player strafe/backpedal while keeping the gun on the target - the legs
            // handle direction via the 2D blend tree below.
            float dt = Time.fixedDeltaTime;
            UpdateAimTarget(dt);

            // Rate-limit the aim VECTOR itself (not just the body) so Weapon.cs - which raycasts along
            // AimDirection - always fires exactly where the gun visually points, even mid-turn. The
            // stable target pick kills logical jitter; this turn cap kills the visual snap on switches.
            Vector3 desired = HasTarget
                ? _desiredAim
                : (move.sqrMagnitude > 0.01f ? move.normalized : AimDirection);
            AimDirection = Vector3.RotateTowards(AimDirection, desired, bodyRotationSpeed * Mathf.Deg2Rad * dt, 0f);

            if (AimDirection.sqrMagnitude > 0.0001f)
                _rb.MoveRotation(Quaternion.LookRotation(AimDirection));

            // Feed the 2D strafe blend tree with velocity expressed in the body's LOCAL frame, so the
            // matching directional clip (N/E/S/W + diagonals) plays no matter which way the body faces.
            if (animator != null)
            {
                float localX = Vector3.Dot(move, transform.right);
                float localY = Vector3.Dot(move, transform.forward);
                animator.SetFloat("MoveX", localX, animDamp, Time.fixedDeltaTime);
                animator.SetFloat("MoveY", localY, animDamp, Time.fixedDeltaTime);
                animator.SetFloat("Speed", move.magnitude, animDamp, Time.fixedDeltaTime);
            }
        }

        // Auto-aim: lock onto the NEAREST targetable zombie. A stickiness margin + min-dwell stop the
        // lock twitching between two near-equidistant zombies; AimDirection is still rate-limited in
        // FixedUpdate so even a real target switch reads as a smooth sweep, never a snap.
        private void UpdateAimTarget(float dt)
        {
            _dwellTimer += dt;
            _recomputeTimer -= dt;

            float rangeSqr = aimRange * aimRange;
            bool currentValid = _aimTarget != null && _aimTarget.IsTargetable
                && (_aimTarget.Transform.position - transform.position).sqrMagnitude <= rangeSqr;

            if (_recomputeTimer <= 0f)
            {
                _recomputeTimer = aimRecomputeInterval;
                var nearest = TargetRegistry.FindNearest(transform.position, aimRange);

                if (!currentValid)
                {
                    // No valid lock - grab the nearest immediately (or null if the field is clear).
                    _aimTarget = nearest;
                    _dwellTimer = 0f;
                }
                else if (nearest != null && nearest != _aimTarget)
                {
                    float curDist = (_aimTarget.Transform.position - transform.position).magnitude;
                    float newDist = (nearest.Transform.position - transform.position).magnitude;

                    if (newDist <= aimDangerRadius)
                    {
                        // Point-blank threat: always shoot the zombie in our face - no dwell, no stickiness.
                        _aimTarget = nearest;
                        _dwellTimer = 0f;
                    }
                    else if (_dwellTimer >= aimMinDwell && newDist < curDist - aimStickiness)
                    {
                        // Steal aim only if the new zombie is meaningfully closer than the current lock.
                        _aimTarget = nearest;
                        _dwellTimer = 0f;
                    }
                }
            }
            else if (!currentValid)
            {
                // Target died / left range between recomputes - drop it now so we never aim at a corpse.
                _aimTarget = null;
            }

            HasTarget = _aimTarget != null;
            if (_aimTarget != null)
            {
                Vector3 to = _aimTarget.Transform.position - transform.position;
                to.y = 0f;
                AimTargetDistance = to.magnitude;
                if (to.sqrMagnitude > 0.0001f) _desiredAim = to.normalized;
            }
            else
            {
                AimTargetDistance = float.MaxValue;
            }
        }
    }
}
