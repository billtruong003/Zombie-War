using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Nguồn pha locomotion cho quái — THUẦN, tất định, và không phụ thuộc bất cứ thứ gì bên ngoài.
    ///
    /// Bản trước lấy pha từ <c>GetInstanceID()</c> nhân tỉ lệ vàng. Nó có vẻ chạy được, nhưng instance
    /// id KHÔNG phải một danh tính ổn định để băm: giá trị và bước nhảy giữa hai object liên tiếp phụ
    /// thuộc vào việc Unity đã tạo bao nhiêu object khác trước đó. Khi
    /// <c>CharacterContactShadows.EnsureInstance()</c> bắt đầu tạo một GameObject ngay giữa lúc spawn
    /// quái, bước nhảy id đổi, phép nhân tỉ lệ vàng bị alias, và cả đám 24 con tụt xuống chỉ còn 16
    /// pha khác nhau — đủ để đàn quái nhấp nhô thành từng nhóm trở lại.
    ///
    /// Ở đây thay bằng SỐ THỨ TỰ KHỞI TẠO: một bộ đếm tăng dần, đi qua một phép băm số nguyên
    /// low-discrepancy. Cùng một số thứ tự luôn cho cùng một pha, và không object không liên quan nào
    /// có thể xen vào làm lệch dãy.
    /// </summary>
    public static class LocomotionPhase
    {
        // Knuth multiplicative hash. 2654435761 là số nguyên tố gần 2^32/φ, nên các số thứ tự liên
        // tiếp bị trải ra khắp khoảng thay vì đi thành một dốc đều.
        private const uint Knuth = 2654435761u;

        private static uint _next;

        /// <summary>
        /// Xoá bộ đếm khi bắt đầu một phiên Play mới.
        ///
        /// Bắt buộc vì project tắt Domain Reload: nếu không, bộ đếm mang giá trị của phiên trước sang
        /// phiên sau, và hai lần chạy giống hệt nhau lại cho hai dãy pha khác nhau — đúng kiểu rò rỉ
        /// tĩnh làm test lúc xanh lúc đỏ.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForNewSession() => _next = 0u;

        /// <summary>Cấp số thứ tự kế tiếp và trả về pha của nó. Gọi đúng MỘT lần cho mỗi quái, ở Awake.</summary>
        public static float Next() => ForOrdinal(_next++);

        /// <summary>
        /// Pha của một số thứ tự cho trước — hàm thuần, dùng cho cả production lẫn test.
        ///
        /// Dịch phải 8 bit rồi chia cho 2^24: lấy 24 bit CAO của giá trị băm, tức phần trộn kỹ nhất,
        /// và cho ra một số trong [0,1) không bao giờ chạm 1.
        /// </summary>
        public static float ForOrdinal(uint ordinal)
        {
            uint bits = ordinal * Knuth;
            return (bits >> 8) * (1f / 16777216f);
        }

        /// <summary>Số thứ tự đã cấp trong phiên này. Chỉ dùng để chẩn đoán và cho test.</summary>
        public static uint IssuedCount => _next;
    }
}
