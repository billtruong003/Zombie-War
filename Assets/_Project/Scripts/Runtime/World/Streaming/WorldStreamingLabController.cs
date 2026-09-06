using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Điều khiển CHỈ DÙNG CHO LAB: đẩy quả cầu probe trên mặt phẳng X/Z và bắn teleport.
    ///
    /// Cố tình dùng Input Manager cũ vì project đang chạy activeInputHandler = Both và
    /// PlayerMovement.cs cũng đọc Input cũ. Lab không phải chỗ dựng kiến trúc input mới.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldStreamingLabController : MonoBehaviour
    {
        [SerializeField] private WorldStreamManager manager;

        [Header("Movement")]
        [SerializeField] private float normalSpeed = 14f;
        [SerializeField] private float boostMultiplier = 8f;
        [SerializeField] private KeyCode boostKey = KeyCode.LeftShift;

        [Header("Teleport")]
        [Tooltip("Đích teleport dương, mét. 2048 m = 64 chunk ở chunkSize 32.")]
        [SerializeField] private Vector2 positiveTeleport = new Vector2(2048f, 2048f);

        [Tooltip("Đích teleport âm/lệch dấu, mét.")]
        [SerializeField] private Vector2 negativeTeleport = new Vector2(-3072f, -1600f);

        [SerializeField] private KeyCode positiveTeleportKey = KeyCode.T;
        [SerializeField] private KeyCode negativeTeleportKey = KeyCode.Y;
        [SerializeField] private KeyCode resetKey = KeyCode.R;

        public WorldStreamManager Manager => manager;
        public float CurrentSpeed { get; private set; }

        public void SetManager(WorldStreamManager value) => manager = value;

        private void Update()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            CurrentSpeed = Input.GetKey(boostKey) ? normalSpeed * boostMultiplier : normalSpeed;

            if (horizontal != 0f || vertical != 0f)
            {
                Vector3 direction = new Vector3(horizontal, 0f, vertical);
                if (direction.sqrMagnitude > 1f) direction.Normalize();
                transform.position += direction * (CurrentSpeed * Time.deltaTime);
            }

            if (Input.GetKeyDown(positiveTeleportKey)) TeleportPositive();
            if (Input.GetKeyDown(negativeTeleportKey)) TeleportNegative();
            if (Input.GetKeyDown(resetKey)) ResetToOrigin();
        }

        public void TeleportPositive() => TeleportTo(positiveTeleport);

        public void TeleportNegative() => TeleportTo(negativeTeleport);

        public void ResetToOrigin() => TeleportTo(Vector2.zero);

        public void TeleportTo(Vector2 worldXZ)
        {
            var destination = new Vector3(worldXZ.x, transform.position.y, worldXZ.y);

            if (manager != null) manager.TeleportTargetTo(destination);
            else transform.position = destination;
        }
    }
}
