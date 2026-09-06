using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Mặt gameplay dùng chung — ĐÚNG MỘT collider phẳng cho cả bong bóng vật lý đang active.
    ///
    /// Chunk hiển thị không bao giờ có collider. Khi mục tiêu teleport đi xa, mặt này dời theo tâm
    /// chunk hiện tại; số collider vẫn là một.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class SharedGameplaySurface : MonoBehaviour
    {
        [Tooltip("Bám theo chunk trung tâm khi mục tiêu di chuyển/teleport ra khỏi footprint hiện tại.")]
        [SerializeField] private bool recenterOnTargetChunk = true;

        private BoxCollider _collider;

        public BoxCollider Collider
        {
            get
            {
                if (_collider == null) _collider = GetComponent<BoxCollider>();
                return _collider;
            }
        }

        /// <summary>Đặt kích thước sao cho mặt trên nằm đúng Y = 0.</summary>
        public void Configure(WorldStreamingConfig config)
        {
            if (config == null) return;

            Collider.size = new Vector3(config.SurfaceFootprint, config.SurfaceThickness, config.SurfaceFootprint);
            Collider.center = new Vector3(0f, -config.SurfaceThickness * 0.5f, 0f);
            Collider.isTrigger = false;
        }

        public void RecenterOn(ChunkCoord coord, float chunkSize)
        {
            if (!recenterOnTargetChunk) return;

            Vector3 center = coord.ToWorldCenter(chunkSize);
            transform.position = new Vector3(center.x, 0f, center.z);
        }
    }
}
