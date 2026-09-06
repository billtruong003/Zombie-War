using System;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Loại geometry mà một nguồn trang trí chảy vào. Một placement có thể chảy vào cả hai
    /// (thân cây → Solid, tán lá → Foliage) nhưng vẫn chỉ là MỘT placement.
    /// </summary>
    public enum DecorationCategory
    {
        Foliage = 0,
        Solid = 1,
    }

    /// <summary>
    /// Các "làn" băm. Mỗi đại lượng ngẫu nhiên đọc một làn riêng, nên thêm một đại lượng mới
    /// không bao giờ làm xê dịch các đại lượng đã có.
    ///
    /// KHÔNG được đổi giá trị của làn nào đã tồn tại — đổi là thay đổi toàn bộ thế giới đã sinh ra.
    /// </summary>
    public enum HashLane
    {
        Acceptance = 1,
        JitterX = 2,
        JitterZ = 3,
        Rotation = 4,
        Scale = 5,
        SourceChoice = 6,
        Priority = 7,
        Tint = 8,

        /// <summary>
        /// Giá trị chọn lọc cho mật độ theo khoảng cách (M3B.2D).
        ///
        /// Phải là một làn RIÊNG chứ không tái dùng <see cref="Acceptance"/>: nếu dùng chung, tập cây
        /// còn lại ở vòng ngoài sẽ tương quan với chính ngưỡng đã dùng để chấp nhận chúng, và phần bị
        /// bỏ sẽ dồn về một phía của phân bố thay vì rải đều.
        /// </summary>
        DistanceDensity = 9,
    }

    /// <summary>
    /// Bậc mật độ của một chunk, tính theo khoảng cách Chebyshev tới gốc streaming.
    ///
    /// Là kiểu riêng chứ không phải bool để chỗ nào cũng đọc ra nghĩa, và để test khẳng định được
    /// "chunk này thuộc bậc nào" thay vì so một con số float.
    /// </summary>
    public enum DecorationDensityTier
    {
        /// <summary>Trong bán kính gần: giữ trọn tập tất định.</summary>
        Near = 0,

        /// <summary>Vòng ngoài: giữ một tập con tất định của tập gần.</summary>
        Outer = 1,
    }

    /// <summary>
    /// Băm số nguyên tất định cho việc đặt trang trí.
    ///
    /// Không <c>UnityEngine.Random</c>, không <c>System.Random</c> dùng chung, không
    /// <c>GetHashCode()</c> (nó không được bảo đảm ổn định giữa các phiên chạy), không chỉ số danh
    /// sách. Mọi thứ chỉ phụ thuộc: hạt giống thế giới, ô lưới toàn cục, muối của loại, muối của
    /// nguồn, chỉ số ứng viên và làn băm.
    /// </summary>
    public static class DecorationHash
    {
        /// <summary>Băm 32-bit kiểu avalanche. Mọi đầu vào là số nguyên.</summary>
        public static uint Hash(int worldSeed, int cellX, int cellZ, int categorySalt, int candidateIndex, HashLane lane)
        {
            unchecked
            {
                uint h = (uint)worldSeed * 0x9E3779B1u;
                h ^= (uint)cellX * 0x85EBCA6Bu;
                h = (h ^ (h >> 15)) * 0xC2B2AE35u;
                h ^= (uint)cellZ * 0x27D4EB2Fu;
                h = (h ^ (h >> 13)) * 0x165667B1u;
                h ^= (uint)categorySalt * 0x9E3779B1u;
                h = (h ^ (h >> 16)) * 0x7FEB352Du;
                h ^= (uint)candidateIndex * 0x846CA68Bu;
                h ^= (uint)lane * 0xD2511F53u;
                h ^= h >> 15;
                h *= 0x2545F491u;
                h ^= h >> 13;
                return h;
            }
        }

        /// <summary>Giá trị trong <c>[0,1)</c>.</summary>
        public static float Unit(int worldSeed, int cellX, int cellZ, int categorySalt, int candidateIndex, HashLane lane) =>
            (Hash(worldSeed, cellX, cellZ, categorySalt, candidateIndex, lane) & 0x00FFFFFFu) * (1f / 0x01000000);

        /// <summary>Giá trị trong <c>[-1,1)</c>.</summary>
        public static float Signed(int worldSeed, int cellX, int cellZ, int categorySalt, int candidateIndex, HashLane lane) =>
            Unit(worldSeed, cellX, cellZ, categorySalt, candidateIndex, lane) * 2f - 1f;

        /// <summary>Chỉ số trong <c>[0, count)</c>.</summary>
        public static int Index(int worldSeed, int cellX, int cellZ, int categorySalt, int candidateIndex, HashLane lane, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "count phải > 0.");
            return (int)(Hash(worldSeed, cellX, cellZ, categorySalt, candidateIndex, lane) % (uint)count);
        }

        /// <summary>
        /// Muối ổn định suy từ một chuỗi ID. Dùng để lấy muối mặc định cho một entry palette mà
        /// không phụ thuộc vị trí của nó trong danh sách.
        ///
        /// Đây là FNV-1a viết tay: <c>string.GetHashCode()</c> không được bảo đảm ổn định giữa các
        /// phiên chạy và các phiên bản .NET, nên nó không dùng được cho dữ liệu thế giới.
        /// </summary>
        public static int SaltFromId(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return 0;

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < stableId.Length; i++)
                {
                    hash ^= stableId[i];
                    hash *= 16777619u;
                }

                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }
}
