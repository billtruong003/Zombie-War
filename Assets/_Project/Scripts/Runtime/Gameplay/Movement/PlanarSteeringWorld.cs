using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Registry + spatial hash cho toàn bộ enemy đang di chuyển trên mặt phẳng gameplay.
    ///
    /// Vì sao cần: tách khỏi NavMesh nghĩa là mỗi enemy phải tự biết hàng xóm của nó để giãn cách.
    /// Hỏi thẳng "ai đang ở gần tôi" theo kiểu duyệt hết mọi enemy là O(n²) — 60 enemy đã là 3.600
    /// phép so mỗi frame, và số đó tăng theo bình phương đúng lúc trận đánh đông nhất.
    ///
    /// Ở đây dùng lưới băm không gian với TOÀN BỘ bộ nhớ cấp phát sẵn: một mảng "đầu danh sách" theo ô
    /// và một mảng "phần tử kế tiếp" theo agent. Xây lại lưới mỗi frame chỉ là hai vòng lặp tuyến tính
    /// ghi vào mảng có sẵn — không Dictionary, không List mới, không cấp phát nào trong đường chạy nóng.
    ///
    /// Lưới được xây LƯỜI: lần truy vấn đầu tiên của mỗi frame dựng lại, các lần sau dùng lại. Nhờ vậy
    /// thứ tự Update giữa các enemy không quan trọng và không ai phải nhớ gọi "rebuild" trước.
    /// </summary>
    public static class PlanarSteeringWorld
    {
        /// <summary>Cạnh ô lưới, mét. Nên xấp xỉ bán kính giãn cách lớn nhất để mỗi truy vấn chỉ chạm 3×3 ô.</summary>
        public const float CellSize = 2.5f;

        private const int InitialCapacity = 256;

        private static readonly List<PlanarEnemyMotor> Agents = new List<PlanarEnemyMotor>(InitialCapacity);

        // Lưới băm: _cellStart[bucket] = chỉ số agent đầu tiên trong ô đó (hoặc -1),
        // _cellNext[i] = agent kế tiếp cùng ô với agent i (hoặc -1). Cả hai đều tái sử dụng.
        private const int BucketCount = 1024;
        private static readonly int[] CellStart = new int[BucketCount];
        private static int[] _cellNext = new int[InitialCapacity];
        private static Vector3[] _positions = new Vector3[InitialCapacity];

        private static int _builtFrame = -1;
        private static bool _gridValid;

        /// <summary>Số agent đang đăng ký. Dùng cho profiling và test.</summary>
        public static int AgentCount => Agents.Count;

        /// <summary>Số lần truy vấn hàng xóm kể từ lần reset gần nhất. Chỉ để đo, không ảnh hưởng hành vi.</summary>
        public static int NeighbourQueries { get; private set; }

        /// <summary>Số ô lưới đang có ít nhất một agent, tính ở lần dựng gần nhất.</summary>
        public static int OccupiedCells { get; private set; }

        public static void ResetCounters() => NeighbourQueries = 0;

        public static void Register(PlanarEnemyMotor motor)
        {
            if (motor == null || motor.SteeringIndex >= 0) return;

            motor.SteeringIndex = Agents.Count;
            Agents.Add(motor);
            EnsureCapacity(Agents.Count);
            _gridValid = false;
        }

        /// <summary>
        /// Gỡ đăng ký bằng hoán đổi-với-phần-tử-cuối, nên chi phí là O(1) chứ không phải O(n).
        ///
        /// Quan trọng với pooling: một wave kết thúc trả về hàng chục enemy trong cùng một frame, và
        /// <c>List.Remove</c> sẽ quét tuyến tính từng lần một.
        /// </summary>
        public static void Unregister(PlanarEnemyMotor motor)
        {
            if (motor == null) return;

            int index = motor.SteeringIndex;
            if (index < 0 || index >= Agents.Count || Agents[index] != motor) return;

            int last = Agents.Count - 1;
            if (index != last)
            {
                Agents[index] = Agents[last];
                Agents[index].SteeringIndex = index;
            }

            Agents.RemoveAt(last);
            motor.SteeringIndex = -1;
            _gridValid = false;
        }

        /// <summary>Xoá sạch registry. Dùng khi đổi scene để không mang agent chết sang scene mới.</summary>
        public static void Clear()
        {
            for (int i = 0; i < Agents.Count; i++)
                if (Agents[i] != null) Agents[i].SteeringIndex = -1;

            Agents.Clear();
            _gridValid = false;
            _builtFrame = -1;
            OccupiedCells = 0;
        }

        private static void EnsureCapacity(int required)
        {
            if (_cellNext.Length >= required) return;

            int size = _cellNext.Length;
            while (size < required) size *= 2;
            _cellNext = new int[size];
            _positions = new Vector3[size];
        }

        private static int BucketOf(float x, float z)
        {
            int cx = Mathf.FloorToInt(x / CellSize);
            int cz = Mathf.FloorToInt(z / CellSize);
            return BucketOf(cx, cz);
        }

        private static int BucketOf(int cx, int cz)
        {
            // Băm hai số nguyên rồi gấp về số ô cố định. Va chạm ô là chấp nhận được: nó chỉ khiến
            // truy vấn xét thêm vài agent ở xa, không bao giờ bỏ sót agent ở gần, vì bước lọc theo
            // khoảng cách thật vẫn chạy sau.
            unchecked
            {
                int h = cx * 73856093 ^ cz * 19349663;
                return (h & 0x7FFFFFFF) % BucketCount;
            }
        }

        /// <summary>Dựng lại lưới nếu frame này chưa dựng. Gọi nhiều lần trong một frame là no-op.</summary>
        private static void EnsureGrid()
        {
            if (_gridValid && _builtFrame == Time.frameCount) return;

            for (int i = 0; i < BucketCount; i++) CellStart[i] = -1;

            int occupied = 0;
            for (int i = 0; i < Agents.Count; i++)
            {
                PlanarEnemyMotor motor = Agents[i];
                if (motor == null) { _cellNext[i] = -1; continue; }

                Vector3 position = motor.transform.position;
                _positions[i] = position;

                int bucket = BucketOf(position.x, position.z);
                if (CellStart[bucket] < 0) occupied++;
                _cellNext[i] = CellStart[bucket];
                CellStart[bucket] = i;
            }

            OccupiedCells = occupied;
            _builtFrame = Time.frameCount;
            _gridValid = true;
        }

        /// <summary>
        /// Tính vector giãn cách cho một agent: tổng các hướng đẩy ra khỏi hàng xóm trong bán kính.
        ///
        /// Trả về vector CHƯA chuẩn hoá, đã có trọng số theo độ gần — hàng xóm càng sát thì đẩy càng
        /// mạnh. Không cấp phát: kết quả là một struct trả về, không có mảng hay list trung gian nào.
        /// </summary>
        public static Vector3 ComputeSeparation(PlanarEnemyMotor self, float radius, out int neighbourCount)
        {
            neighbourCount = 0;
            var push = Vector3.zero;
            if (self == null || radius <= 0f) return push;

            EnsureGrid();
            NeighbourQueries++;

            Vector3 origin = self.transform.position;
            float radiusSqr = radius * radius;
            int selfIndex = self.SteeringIndex;

            int minX = Mathf.FloorToInt((origin.x - radius) / CellSize);
            int maxX = Mathf.FloorToInt((origin.x + radius) / CellSize);
            int minZ = Mathf.FloorToInt((origin.z - radius) / CellSize);
            int maxZ = Mathf.FloorToInt((origin.z + radius) / CellSize);

            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    for (int i = CellStart[BucketOf(cx, cz)]; i >= 0; i = _cellNext[i])
                    {
                        if (i == selfIndex) continue;

                        Vector3 other = _positions[i];
                        float dx = origin.x - other.x;
                        float dz = origin.z - other.z;
                        float distanceSqr = dx * dx + dz * dz;
                        if (distanceSqr > radiusSqr) continue;

                        neighbourCount++;

                        // Hai enemy chồng khít lên nhau: đẩy theo một hướng ổn định suy từ chỉ số,
                        // chứ không random — nếu random, cả cụm sẽ rung liên tục mỗi frame.
                        if (distanceSqr < 0.0001f)
                        {
                            float angle = (selfIndex * 2.399963f) % (Mathf.PI * 2f);
                            push.x += Mathf.Cos(angle);
                            push.z += Mathf.Sin(angle);
                            continue;
                        }

                        float distance = Mathf.Sqrt(distanceSqr);
                        float strength = 1f - distance / radius;
                        push.x += dx / distance * strength;
                        push.z += dz / distance * strength;
                    }
                }
            }

            push.y = 0f;
            return push;
        }
    }
}
