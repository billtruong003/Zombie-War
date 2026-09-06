using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Phép toán THUẦN giữa tầm nhìn của camera và vùng đất đã stream.
    ///
    /// Bất biến cuối cùng của M5: mọi điểm trên vệt chiếu của camera gameplay xuống mặt phẳng
    /// gameplay phải nằm trong một chunk nền ĐANG BẬT trước khi nó lọt vào Game View.
    ///
    /// Vệt chiếu được tính bằng tia thật của frustum cắt mặt phẳng, không phải bằng một bán kính
    /// ước lượng. Ước lượng bằng bán kính đã từng là lý do lỗi này bị chẩn đoán sai hai lần: nó
    /// không phân biệt được camera phối cảnh với camera trực giao, và nó bỏ qua tỉ lệ khung hình —
    /// trong khi chính hai thứ đó quyết định vệt chiếu rộng tới đâu.
    ///
    /// Lớp này KHÔNG đọc scene, KHÔNG đổi trạng thái và KHÔNG cấp phát: tất cả đều là struct, nên
    /// gọi mỗi frame cũng không sinh rác.
    ///
    /// PHẠM VI: đây là công cụ CHẨN ĐOÁN và KIỂM THỬ. Production không gọi nó.
    ///
    /// Bản R2 từng cho <see cref="WorldStreamManager"/> dùng lớp này để chọn tâm ring theo camera,
    /// nhằm chữa lỗi mất mặt đất. Nguyên nhân thật sau đó được xác định là Occlusion Culling, nên tầng
    /// đó đã gỡ. Phần còn lại vẫn có giá trị: nó trả lời được câu "ring 5×5 có phủ hết tầm nhìn của
    /// camera không", và test dùng nó để canh bất biến đó ở biên chunk và ở toạ độ âm.
    /// </summary>
    public static class GroundCoverage
    {
        /// <summary>Số mẫu trên mỗi cạnh viewport. Cực trị của một frustum lồi luôn nằm trên biên.</summary>
        public const int DefaultSamplesPerEdge = 8;

        /// <summary>Hình chữ nhật bao vệt chiếu của camera trên mặt phẳng gameplay.</summary>
        public readonly struct Footprint
        {
            public readonly float MinX;
            public readonly float MaxX;
            public readonly float MinZ;
            public readonly float MaxZ;

            /// <summary>
            /// Có tia nào của frustum đi lên trên/song song mặt phẳng hay không.
            ///
            /// Khi còn tia như vậy, camera nhìn thấy đường chân trời và KHÔNG ring hữu hạn nào phủ
            /// nổi — nên đây phải là một giá trị báo lỗi, không phải một vệt chiếu "rộng".
            /// </summary>
            public readonly bool SeesHorizon;

            public Footprint(float minX, float maxX, float minZ, float maxZ, bool seesHorizon)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
                SeesHorizon = seesHorizon;
            }

            public bool IsValid => !SeesHorizon && MaxX >= MinX && MaxZ >= MinZ;
            public float SizeX => MaxX - MinX;
            public float SizeZ => MaxZ - MinZ;

            /// <summary>
            /// Giá trị "không có vệt chiếu".
            ///
            /// Phải dùng cái này thay cho <c>default</c>: một struct mặc định có
            /// <see cref="SeesHorizon"/> = false và bốn số 0, nên <see cref="IsValid"/> trả TRUE và nó
            /// mô tả một hình chữ nhật suy biến ngay tại gốc thế giới. Kiểm tra an toàn khi đó sẽ đòi
            /// chunk (0,0) phải có nền, ở bất kỳ đâu trên bản đồ — một báo động giả rất thuyết phục.
            /// Đây đúng là lỗi mà bộ test đã bắt được.
            /// </summary>
            public static Footprint Invalid => new Footprint(0f, 0f, 0f, 0f, true);
        }

        /// <summary>
        /// Vệt chiếu của <paramref name="camera"/> lên mặt phẳng y = <paramref name="planeY"/>.
        ///
        /// Chỉ lấy mẫu trên BIÊN viewport: vệt chiếu là ảnh của một hình lồi qua phép chiếu, nên
        /// điểm xa nhất luôn nằm trên biên. Lấy mẫu cả mặt trong chỉ tốn thời gian mà không đổi kết quả.
        ///
        /// Tia không cắt mặt phẳng trong tầm far clip bị cắt tại far clip: phần đất xa hơn thế không
        /// được vẽ, nên nó không thuộc phần nhìn thấy.
        /// </summary>
        public static Footprint ComputeFootprint(Camera camera, float planeY = 0f,
            int samplesPerEdge = DefaultSamplesPerEdge)
        {
            if (camera == null || samplesPerEdge < 1)
                return new Footprint(0f, 0f, 0f, 0f, true);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            bool seesHorizon = false;

            for (int i = 0; i <= samplesPerEdge; i++)
            for (int j = 0; j <= samplesPerEdge; j++)
            {
                bool onBorder = i == 0 || j == 0 || i == samplesPerEdge || j == samplesPerEdge;
                if (!onBorder) continue;

                Ray ray = camera.ViewportPointToRay(new Vector3(
                    i / (float)samplesPerEdge, j / (float)samplesPerEdge, 0f));

                float height = ray.origin.y - planeY;

                // Tia hướng lên (hoặc song song) trong khi camera ở trên mặt phẳng = nhìn thấy chân trời.
                if (ray.direction.y >= -1e-6f)
                {
                    seesHorizon = true;
                    continue;
                }

                float t = height / -ray.direction.y;
                if (t < 0f) t = 0f;
                if (t > camera.farClipPlane) t = camera.farClipPlane;

                Vector3 hit = ray.origin + ray.direction * t;
                if (hit.x < minX) minX = hit.x;
                if (hit.x > maxX) maxX = hit.x;
                if (hit.z < minZ) minZ = hit.z;
                if (hit.z > maxZ) maxZ = hit.z;
            }

            if (minX > maxX) return new Footprint(0f, 0f, 0f, 0f, true);
            return new Footprint(minX, maxX, minZ, maxZ, seesHorizon);
        }

        /// <summary>Hình chữ nhật phủ bởi ring bán kính <paramref name="radius"/> quanh <paramref name="origin"/>.</summary>
        public static void RingBounds(ChunkCoord origin, int radius, float chunkSize,
            out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = (origin.X - radius) * chunkSize;
            maxX = (origin.X + radius + 1) * chunkSize;
            minZ = (origin.Z - radius) * chunkSize;
            maxZ = (origin.Z + radius + 1) * chunkSize;
        }

        /// <summary>
        /// Khoảng cách nhỏ nhất từ mép vệt chiếu tới mép vùng phủ, mét.
        ///
        /// Dương = còn dư đất ngoài tầm nhìn. Bằng 0 = mép đất trùng đúng mép màn hình. ÂM = đã lộ nền.
        /// </summary>
        public static float MarginToRing(in Footprint footprint, ChunkCoord origin, int radius, float chunkSize)
        {
            if (footprint.SeesHorizon) return float.NegativeInfinity;

            RingBounds(origin, radius, chunkSize, out float minX, out float maxX, out float minZ, out float maxZ);

            float m = footprint.MinX - minX;
            if (maxX - footprint.MaxX < m) m = maxX - footprint.MaxX;
            if (footprint.MinZ - minZ < m) m = footprint.MinZ - minZ;
            if (maxZ - footprint.MaxZ < m) m = maxZ - footprint.MaxZ;
            return m;
        }

        /// <summary>
        /// Vệt chiếu có nằm trọn trong vùng phủ, còn dư ít nhất <paramref name="guardBand"/> mét hay không.
        ///
        /// <paramref name="guardBand"/> là dải đệm phòng xa: đặt bằng một chunk thì đất của lớp kế
        /// tiếp đã có sẵn TRƯỚC khi người chơi bước vào lớp đó, đúng yêu cầu người chơi cảm nhận được.
        /// </summary>
        public static bool IsFullyCovered(in Footprint footprint, ChunkCoord origin, int radius,
            float chunkSize, float guardBand = 0f) =>
            footprint.IsValid && MarginToRing(footprint, origin, radius, chunkSize) >= guardBand;

        /// <summary>Liệt kê mọi chunk mà vệt chiếu chạm tới. Ghi vào danh sách của người gọi.</summary>
        public static void TouchedChunks(in Footprint footprint, float chunkSize, List<ChunkCoord> results)
        {
            if (results == null) return;

            results.Clear();
            if (!footprint.IsValid) return;

            int x0 = ChunkCoord.AxisToChunk(footprint.MinX, chunkSize);
            int x1 = ChunkCoord.AxisToChunk(footprint.MaxX, chunkSize);
            int z0 = ChunkCoord.AxisToChunk(footprint.MinZ, chunkSize);
            int z1 = ChunkCoord.AxisToChunk(footprint.MaxZ, chunkSize);

            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                results.Add(new ChunkCoord(x, z));
        }
    }
}
