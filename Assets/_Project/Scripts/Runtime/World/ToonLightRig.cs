using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Nguồn hướng sáng cho toon shader — THAY directional light thật.
    ///
    /// Lý do tồn tại: VAT_EnemyToon là shader unlit + specular, trước đây đọc
    /// _MainLightPosition nên hoàn toàn phụ thuộc việc scene có directional light active —
    /// và không phản ứng gì khi map muốn hướng sáng riêng. Component này đẩy hướng sáng qua
    /// Shader.SetGlobalVector, shader đọc global đó; directional light thật chỉ còn phục vụ
    /// shadow map của môi trường.
    ///
    /// Cách dùng: mỗi map đặt đúng MỘT rig (menu GameObject/ZombieWar/Toon Light Rig).
    /// Xoay transform là xoay hướng sáng — quy ước giống hệt directional light
    /// (forward của transform = hướng tia sáng). Mặc định (50, -30, 0).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ToonLightRig : MonoBehaviour
    {
        public static readonly Vector3 DefaultEuler = new Vector3(50f, -30f, 0f);

        private static readonly int ToonLightDirId = Shader.PropertyToID("_ToonLightDirection");
        private static readonly int ToonLightColorId = Shader.PropertyToID("_ToonLightColor");

        [Tooltip("Màu ánh sáng toon — thay hoàn toàn màu directional light khi rig active.")]
        [SerializeField] private Color lightColor = Color.white;

        [Tooltip("Cường độ nhân vào màu (URP light convention: color × intensity).")]
        [Range(0f, 8f)]
        [SerializeField] private float intensity = 1f;

        private void OnEnable() => Push();
        private void OnValidate() => Push();

        private void LateUpdate()
        {
            // Chỉ push lại khi transform thật sự xoay (editor gizmo drag / runtime animation).
            if (!transform.hasChanged) return;
            transform.hasChanged = false;
            Push();
        }

        private void OnDisable()
        {
            // Trả về vector 0 → shader tự fallback _MainLightPosition, scene không rig vẫn sáng.
            Shader.SetGlobalVector(ToonLightDirId, Vector4.zero);
            Shader.SetGlobalColor(ToonLightColorId, Color.clear);
        }

        private void Push()
        {
            // Shader cần hướng TỚI nguồn sáng (L trong N·H) — ngược chiều tia = -forward,
            // cùng quy ước _MainLightPosition.xyz của URP directional light.
            Shader.SetGlobalVector(ToonLightDirId, -transform.forward);
            // alpha = 1 làm cờ "rig có màu" — tránh trường hợp global mặc định (0,0,0,0)
            // đè màu đen lên scene chưa từng push.
            var c = lightColor * intensity;
            c.a = 1f;
            Shader.SetGlobalColor(ToonLightColorId, c);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var origin = transform.position;
            var dir = transform.forward;

            Gizmos.color = new Color(1f, 0.85f, 0.3f, 1f);
            // 3 tia song song cho dễ đọc là "directional", không phải point light.
            var side = transform.right * 0.6f;
            for (int i = -1; i <= 1; i++)
            {
                var from = origin + side * i;
                var to = from + dir * 3f;
                Gizmos.DrawLine(from, to);
                // đầu mũi tên
                var back = -dir * 0.45f;
                Gizmos.DrawLine(to, to + Quaternion.AngleAxis(25f, transform.up) * back);
                Gizmos.DrawLine(to, to + Quaternion.AngleAxis(-25f, transform.up) * back);
            }
            Gizmos.DrawWireSphere(origin, 0.25f);
        }
#endif
    }
}
