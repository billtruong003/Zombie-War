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
        [SerializeField] private float aimRange = 15f;

        private Rigidbody _rb;

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

        private void FixedUpdate()
        {
            Vector3 move = new Vector3(joystick.Direction.x, 0f, joystick.Direction.y);
            _rb.MovePosition(_rb.position + move * moveSpeed * Time.fixedDeltaTime);

            if (move.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move);
                _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRotation, bodyRotationSpeed * Time.fixedDeltaTime));
            }

            UpdateAimDirection(move);
            animator.SetFloat("Speed", move.magnitude);
        }

        private void UpdateAimDirection(Vector3 moveDirection)
        {
            var nearest = TargetRegistry.FindNearest(transform.position, aimRange);
            HasTarget = nearest != null;

            if (nearest != null)
            {
                Vector3 toTarget = nearest.Transform.position - transform.position;
                toTarget.y = 0f;
                AimTargetDistance = toTarget.magnitude;
                if (toTarget.sqrMagnitude > 0.0001f) AimDirection = toTarget.normalized;
            }
            else
            {
                AimTargetDistance = float.MaxValue;
                if (moveDirection.sqrMagnitude > 0.01f) AimDirection = moveDirection.normalized;
            }
        }
    }
}
