using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Danh tính logic của một chunk trong thế giới stream — KHÔNG phải slot pool, KHÔNG phải instance ID.
    ///
    /// Đây là kiểu giá trị thuần: không tham chiếu scene object nào, nên toàn bộ phép chuyển đổi và
    /// phép liệt kê ring đều test được ở EditMode.
    ///
    /// Quy ước chuyển đổi: <c>floor(worldPosition / chunkSize)</c>. Ép kiểu int của C# cắt về 0
    /// (truncate toward zero) nên SAI với toạ độ âm — <c>(int)(-0.1f / 32f) == 0</c> trong khi đáp án
    /// đúng là <c>-1</c>. Vì vậy mọi chuyển đổi ở đây đi qua <see cref="Math.Floor(double)"/>.
    /// </summary>
    [Serializable]
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly int X;
        public readonly int Z;

        public ChunkCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public static ChunkCoord Zero => new ChunkCoord(0, 0);

        /// <summary>Chuyển một trục world sang chỉ số chunk bằng floor thật sự (đúng cho số âm).</summary>
        public static int AxisToChunk(float worldAxis, float chunkSize)
        {
            if (chunkSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "chunkSize phải > 0.");

            // Chia ở double: float 32.0f/32f có thể trả 0.99999994 ở vài giá trị biên,
            // đủ để làm floor lệch một chunk ngay tại ranh giới.
            return (int)Math.Floor(worldAxis / (double)chunkSize);
        }

        public static ChunkCoord FromWorld(float worldX, float worldZ, float chunkSize) =>
            new ChunkCoord(AxisToChunk(worldX, chunkSize), AxisToChunk(worldZ, chunkSize));

        public static ChunkCoord FromWorld(Vector3 world, float chunkSize) =>
            FromWorld(world.x, world.z, chunkSize);

        /// <summary>Góc nhỏ nhất (min corner) của chunk — pivot vật lý dùng cho chunk root.</summary>
        public Vector3 ToWorldMin(float chunkSize) => new Vector3(X * chunkSize, 0f, Z * chunkSize);

        public Vector3 ToWorldCenter(float chunkSize) =>
            new Vector3((X + 0.5f) * chunkSize, 0f, (Z + 0.5f) * chunkSize);

        /// <summary>Số chunk trong ring vuông bán kính <paramref name="radius"/> — (2r+1)².</summary>
        public static int RingCount(int radius)
        {
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius phải >= 0.");

            int diameter = radius * 2 + 1;
            return diameter * diameter;
        }

        /// <summary>
        /// Liệt kê tập chunk bắt buộc quanh <paramref name="center"/> theo thứ tự ỔN ĐỊNH
        /// (z tăng dần ở vòng ngoài, x tăng dần ở vòng trong).
        ///
        /// Thứ tự cố định là yêu cầu chứ không phải chi tiết trang trí: thứ tự cấp phát lease
        /// phải tái lập được, không được phụ thuộc thứ tự duyệt của HashSet/Dictionary.
        /// </summary>
        public static void EnumerateRing(ChunkCoord center, int radius, List<ChunkCoord> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius phải >= 0.");

            results.Clear();
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
                results.Add(new ChunkCoord(center.X + dx, center.Z + dz));
        }

        public static List<ChunkCoord> EnumerateRing(ChunkCoord center, int radius)
        {
            var results = new List<ChunkCoord>(RingCount(radius));
            EnumerateRing(center, radius, results);
            return results;
        }

        public bool Equals(ChunkCoord other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 73856093) ^ (Z * 19349663);
            }
        }

        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);

        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);

        public override string ToString() => $"({X},{Z})";
    }
}
