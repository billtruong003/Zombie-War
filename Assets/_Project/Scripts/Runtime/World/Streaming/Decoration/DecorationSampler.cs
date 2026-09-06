using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>Một lần đặt trang trí đã được chấp nhận. Thuần dữ liệu — không GameObject nào cả.</summary>
    public readonly struct DecorationPlacement : IEquatable<DecorationPlacement>
    {
        /// <summary>Muối ổn định của entry palette. Đây mới là danh tính, không phải chỉ số danh sách.</summary>
        public readonly int EntrySalt;

        public readonly int CellX;
        public readonly int CellZ;
        public readonly int CandidateIndex;

        /// <summary>Vị trí trong hệ toạ độ cục bộ của chunk sở hữu. Y luôn bằng 0.</summary>
        public readonly Vector3 LocalPosition;

        public readonly float Yaw;
        public readonly float Scale;
        public readonly float Tint;
        public readonly uint Priority;
        public readonly int BudgetPriority;

        /// <summary>Chỉ số entry tại thời điểm sinh — TẠM THỜI, không được lưu ra ngoài.</summary>
        public readonly int EntryIndex;

        public DecorationPlacement(int entrySalt, int entryIndex, int cellX, int cellZ, int candidateIndex,
            Vector3 localPosition, float yaw, float scale, float tint, uint priority, int budgetPriority = 0)
        {
            EntrySalt = entrySalt;
            EntryIndex = entryIndex;
            CellX = cellX;
            CellZ = cellZ;
            CandidateIndex = candidateIndex;
            LocalPosition = localPosition;
            Yaw = yaw;
            Scale = scale;
            Tint = tint;
            Priority = priority;
            BudgetPriority = budgetPriority;
        }

        public bool Equals(DecorationPlacement other) =>
            EntrySalt == other.EntrySalt && CellX == other.CellX && CellZ == other.CellZ &&
            CandidateIndex == other.CandidateIndex &&
            LocalPosition == other.LocalPosition &&
            Yaw.Equals(other.Yaw) && Scale.Equals(other.Scale) && Tint.Equals(other.Tint) &&
            Priority == other.Priority && BudgetPriority == other.BudgetPriority;

        public override bool Equals(object obj) => obj is DecorationPlacement other && Equals(other);

        public override int GetHashCode() => unchecked((EntrySalt * 397) ^ (CellX * 31) ^ CellZ);

        public override string ToString() =>
            $"salt{EntrySalt} cell({CellX},{CellZ})#{CandidateIndex} " +
            $"pos({LocalPosition.x:F4},{LocalPosition.z:F4}) yaw{Yaw:F3} s{Scale:F4} t{Tint:F4} " +
            $"p{Priority} bp{BudgetPriority}";
    }

    /// <summary>Số liệu của một lần sinh, để overlay debug và test đọc.</summary>
    public struct DecorationSampleStats
    {
        public int Candidates;
        public int Accepted;
        public int RejectedByDensity;
        public int RejectedBySpacing;
        public int RejectedByOwnership;
        public int RejectedByBudget;

        /// <summary>Bị loại bởi mật độ theo khoảng cách (M3B.2D). Tách riêng để không lẫn với lý do khác.</summary>
        public int RejectedByDistanceDensity;

        /// <summary>Số placement thuộc diện được phép giảm mật độ, TRƯỚC khi lọc.</summary>
        public int EligibleForDistanceDensity;
        public int SolidVertices;
        public int FoliageVertices;

        public void Reset() => this = default;
    }

    /// <summary>
    /// Sinh vị trí trang trí một cách tất định từ lưới ô toàn cục.
    ///
    /// Mọi quyết định chỉ phụ thuộc `(worldSeed, ô toàn cục, muối entry, chỉ số ứng viên)` và trường
    /// biome tại chính điểm neo. Không có trạng thái nào mang qua giữa các lần gọi, nên slot pool,
    /// lịch sử đi lại và thứ tự lấy mẫu đều không thể ảnh hưởng kết quả.
    ///
    /// Quyền sở hữu tính theo ĐIỂM NEO: chunk chứa điểm neo sở hữu trọn placement, kể cả khi tán cây
    /// tràn sang chunk bên cạnh. Nhờ vậy không bao giờ có prop bị nhân đôi ở biên.
    /// </summary>
    public static class DecorationSampler
    {
        private static readonly Comparison<DecorationPlacement> StableOrder = CompareStable;
        private static readonly Comparison<DecorationPlacement> BudgetOrder = CompareBudget;
        private static readonly List<DecorationPlacement> BudgetScratch = new List<DecorationPlacement>(512);

        /// <summary>Thứ tự phát ổn định: không phụ thuộc thứ tự entry trong palette.</summary>
        private static int CompareStable(DecorationPlacement a, DecorationPlacement b)
        {
            int c = a.EntrySalt.CompareTo(b.EntrySalt);
            if (c != 0) return c;
            c = a.CellZ.CompareTo(b.CellZ);
            if (c != 0) return c;
            c = a.CellX.CompareTo(b.CellX);
            if (c != 0) return c;
            return a.CandidateIndex.CompareTo(b.CandidateIndex);
        }

        /// <summary>Thứ tự cắt ngân sách: ưu tiên cao giữ trước, phần còn lại theo thứ tự ổn định.</summary>
        private static int CompareBudget(DecorationPlacement a, DecorationPlacement b)
        {
            int c = b.BudgetPriority.CompareTo(a.BudgetPriority);
            if (c != 0) return c;

            c = b.Priority.CompareTo(a.Priority);
            return c != 0 ? c : CompareStable(a, b);
        }

        /// <summary>Mật độ đầy đủ — không có bậc khoảng cách nào được áp.</summary>
        public const float FullDensity = 1f;

        public static void Generate(int worldSeed, ChunkCoord coord, float chunkSize,
            DecorationPalette palette, List<DecorationPlacement> results)
        {
            DecorationSampleStats ignored = default;
            Generate(worldSeed, coord, chunkSize, palette, results, FullDensity, ref ignored);
        }

        public static void Generate(int worldSeed, ChunkCoord coord, float chunkSize,
            DecorationPalette palette, List<DecorationPlacement> results, ref DecorationSampleStats stats)
        {
            Generate(worldSeed, coord, chunkSize, palette, results, FullDensity, ref stats);
        }

        /// <summary>
        /// Sinh placement cho một chunk, có thể tỉa bớt cây cỏ nhỏ theo <paramref name="foliageDensity"/>.
        ///
        /// KHÔNG có đường sinh riêng cho vòng ngoài. Chỉ có đúng một lần sinh, rồi một phép lọc tất
        /// định chạy sau cùng — đó là điều làm cho tập vòng ngoài luôn là tập CON của tập gần.
        /// </summary>
        public static void Generate(int worldSeed, ChunkCoord coord, float chunkSize,
            DecorationPalette palette, List<DecorationPlacement> results, float foliageDensity,
            ref DecorationSampleStats stats)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            results.Clear();
            stats.Reset();
            if (palette == null) return;

            IReadOnlyList<DecorationPaletteEntry> entries = palette.Entries;
            float chunkMinX = coord.X * chunkSize;
            float chunkMinZ = coord.Z * chunkSize;

            for (int e = 0; e < entries.Count; e++)
            {
                DecorationPaletteEntry entry = entries[e];
                if (entry == null || entry.PrimarySource == null) continue;

                float cellSize = entry.CellSize;
                int salt = entry.Salt;

                // Duyệt mọi ô chạm vào chunk. Ô nào tràn ra ngoài vẫn được xét, vì điểm neo của nó
                // có thể rơi vào trong chunk này.
                int cellMinX = FloorDiv(chunkMinX, cellSize);
                int cellMaxX = FloorDiv(chunkMinX + chunkSize, cellSize);
                int cellMinZ = FloorDiv(chunkMinZ, cellSize);
                int cellMaxZ = FloorDiv(chunkMinZ + chunkSize, cellSize);

                for (int cz = cellMinZ; cz <= cellMaxZ; cz++)
                {
                    for (int cx = cellMinX; cx <= cellMaxX; cx++)
                    {
                        for (int candidate = 0; candidate < entry.CandidatesPerCell; candidate++)
                        {
                            stats.Candidates++;

                            if (!TryBuildCandidate(worldSeed, entry, salt, cx, cz, candidate, cellSize,
                                    out Vector3 anchor, out uint priority, out float density))
                            {
                                stats.RejectedByDensity++;
                                continue;
                            }

                            // Quyền sở hữu: chỉ chunk chứa điểm neo mới được phát placement này.
                            if (ChunkCoord.AxisToChunk(anchor.x, chunkSize) != coord.X ||
                                ChunkCoord.AxisToChunk(anchor.z, chunkSize) != coord.Z)
                            {
                                stats.RejectedByOwnership++;
                                continue;
                            }

                            if (entry.UsesSpacing &&
                                IsBeatenByNeighbour(worldSeed, entry, salt, cx, cz, candidate,
                                    cellSize, anchor, priority))
                            {
                                stats.RejectedBySpacing++;
                                continue;
                            }

                            float yaw = entry.RandomYaw
                                ? DecorationHash.Unit(worldSeed, cx, cz, salt, candidate, HashLane.Rotation) * 360f
                                : 0f;

                            float scale = Mathf.Lerp(entry.UniformScaleRange.x, entry.UniformScaleRange.y,
                                DecorationHash.Unit(worldSeed, cx, cz, salt, candidate, HashLane.Scale));

                            float tint = Mathf.Lerp(entry.TintRange.x, entry.TintRange.y,
                                DecorationHash.Unit(worldSeed, cx, cz, salt, candidate, HashLane.Tint));

                            var local = new Vector3(anchor.x - chunkMinX, 0f, anchor.z - chunkMinZ);
                            results.Add(new DecorationPlacement(salt, e, cx, cz, candidate, local, yaw, scale, tint,
                                priority, entry.BudgetPriority));
                        }
                    }
                }
            }

            results.Sort(StableOrder);
            ApplyVertexBudget(palette, results, ref stats);
            ApplyDistanceDensity(worldSeed, palette, results, foliageDensity, ref stats);
        }

        /// <summary>
        /// Tỉa cây cỏ nhỏ theo một ngưỡng tất định.
        ///
        /// Chạy SAU khi cắt ngân sách, và đó là điểm mấu chốt chứ không phải chuyện tiện tay. Nếu lọc
        /// trước, chunk vòng ngoài sẽ bước vào phép cắt ngân sách với ít ứng viên hơn, nên có thể GIỮ
        /// được một placement mà chunk gần đã phải bỏ vì hết ngân sách — và khi đó tập ngoài không còn
        /// là tập con của tập gần. Lọc sau thì quan hệ tập con đúng vô điều kiện, không phụ thuộc việc
        /// ngân sách có chạm trần hay không.
        ///
        /// Giá trị chọn lọc lấy từ danh tính TOÀN CỤC của placement (hạt giống, ô lưới, muối entry, chỉ
        /// số ứng viên), nên nó không đổi theo chunk nào đang hỏi, theo thứ tự lập lịch, hay theo hướng
        /// người chơi đi qua.
        /// </summary>
        private static void ApplyDistanceDensity(int worldSeed, DecorationPalette palette,
            List<DecorationPlacement> results, float foliageDensity, ref DecorationSampleStats stats)
        {
            if (foliageDensity >= FullDensity)
            {
                // Vẫn phải đếm phần đủ điều kiện để báo cáo so sánh được giữa hai bậc.
                for (int i = 0; i < results.Count; i++)
                    if (palette.Entries[results[i].EntryIndex].DistanceDensityEligible)
                        stats.EligibleForDistanceDensity++;
                return;
            }

            IReadOnlyList<DecorationPaletteEntry> entries = palette.Entries;
            int write = 0;

            for (int i = 0; i < results.Count; i++)
            {
                DecorationPlacement placement = results[i];
                DecorationPaletteEntry entry = entries[placement.EntryIndex];

                if (!entry.DistanceDensityEligible)
                {
                    results[write++] = placement;
                    continue;
                }

                stats.EligibleForDistanceDensity++;

                float selection = DecorationHash.Unit(worldSeed, placement.CellX, placement.CellZ,
                    placement.EntrySalt, placement.CandidateIndex, HashLane.DistanceDensity);

                if (selection < foliageDensity)
                {
                    results[write++] = placement;
                    continue;
                }

                stats.RejectedByDistanceDensity++;
            }

            results.RemoveRange(write, results.Count - write);

            stats.Accepted = results.Count;
            RecountVertices(palette, results, ref stats);
        }

        private static void RecountVertices(DecorationPalette palette, List<DecorationPlacement> results,
            ref DecorationSampleStats stats)
        {
            int solid = 0;
            int foliage = 0;

            for (int i = 0; i < results.Count; i++)
            {
                DecorationPaletteEntry entry = palette.Entries[results[i].EntryIndex];
                solid += CostOf(entry, DecorationCategory.Solid);
                foliage += CostOf(entry, DecorationCategory.Foliage);
            }

            stats.SolidVertices = solid;
            stats.FoliageVertices = foliage;
        }

        /// <summary>
        /// Cắt theo ngân sách đỉnh: giữ ưu tiên cao trước, bỏ phần thấp nhất. Tất định hoàn toàn vì
        /// độ ưu tiên đến từ băm chứ không từ thứ tự duyệt.
        /// </summary>
        private static void ApplyVertexBudget(DecorationPalette palette, List<DecorationPlacement> results,
            ref DecorationSampleStats stats)
        {
            IReadOnlyList<DecorationPaletteEntry> entries = palette.Entries;

            BudgetScratch.Clear();
            for (int i = 0; i < results.Count; i++) BudgetScratch.Add(results[i]);
            BudgetScratch.Sort(BudgetOrder);

            int solid = 0;
            int foliage = 0;
            results.Clear();

            for (int i = 0; i < BudgetScratch.Count; i++)
            {
                DecorationPlacement placement = BudgetScratch[i];
                DecorationPaletteEntry entry = entries[placement.EntryIndex];

                int solidCost = CostOf(entry, DecorationCategory.Solid);
                int foliageCost = CostOf(entry, DecorationCategory.Foliage);

                if (solid + solidCost > palette.SolidVertexBudget ||
                    foliage + foliageCost > palette.FoliageVertexBudget)
                {
                    stats.RejectedByBudget++;
                    continue;
                }

                solid += solidCost;
                foliage += foliageCost;
                results.Add(placement);
            }

            results.Sort(StableOrder);

            stats.Accepted = results.Count;
            stats.SolidVertices = solid;
            stats.FoliageVertices = foliage;
        }

        private static int CostOf(DecorationPaletteEntry entry, DecorationCategory category)
        {
            int cost = 0;
            if (entry.PrimarySource != null && entry.PrimarySource.Category == category)
                cost += entry.PrimarySource.VertexCount;
            if (entry.SecondarySource != null && entry.SecondarySource.Category == category)
                cost += entry.SecondarySource.VertexCount;
            return cost;
        }

        private static bool TryBuildCandidate(int worldSeed, DecorationPaletteEntry entry, int salt,
            int cellX, int cellZ, int candidate, float cellSize,
            out Vector3 anchor, out uint priority, out float density)
        {
            float jitterX = DecorationHash.Unit(worldSeed, cellX, cellZ, salt, candidate, HashLane.JitterX);
            float jitterZ = DecorationHash.Unit(worldSeed, cellX, cellZ, salt, candidate, HashLane.JitterZ);

            anchor = new Vector3((cellX + jitterX) * cellSize, 0f, (cellZ + jitterZ) * cellSize);
            priority = DecorationHash.Hash(worldSeed, cellX, cellZ, salt, candidate, HashLane.Priority);

            BiomeSample sample = BiomeSampler.Sample(worldSeed, anchor.x, anchor.z);
            density = entry.DensityAt(sample);

            // Vón cụm (M4.6B): nhân thêm một trường tần số thấp lấy theo ĐIỂM NEO toàn cục.
            // Đặt ở đây, trong hàm dựng ứng viên duy nhất, nên `IsBeatenByNeighbour` cũng thấy đúng
            // cùng một mật độ — nếu chỉ sửa ở `Generate` thì luật giãn cách sẽ so với một thế giới khác
            // với thế giới thật sự được phát, và biên chunk sẽ hở.
            // Entry không bật vón cụm trả về đúng 1f, nên kết quả cũ không đổi một bit nào.
            density *= entry.ClumpFactorAt(worldSeed, anchor.x, anchor.z);

            float acceptance = DecorationHash.Unit(worldSeed, cellX, cellZ, salt, candidate, HashLane.Acceptance);
            return acceptance < density;
        }

        /// <summary>
        /// Loại trừ theo độ ưu tiên cục bộ: một ứng viên chỉ sống sót nếu nó thắng mọi ứng viên hợp lệ
        /// khác nằm trong bán kính giãn cách. Kết quả giống blue-noise mà không cần Poisson-disc toàn cục.
        ///
        /// Phép so sánh này chạy trên lưới TOÀN CỤC nên nó không đổi khi hỏi từ chunk khác — đó là lý
        /// do cây ở sát biên không nhấp nháy khi recycle.
        /// </summary>
        private static bool IsBeatenByNeighbour(int worldSeed, DecorationPaletteEntry entry, int salt,
            int cellX, int cellZ, int currentCandidate, float cellSize, Vector3 anchor, uint priority)
        {
            float radius = entry.SpacingRadius;
            float radiusSqr = radius * radius;
            int reach = Mathf.CeilToInt(radius / cellSize);

            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int nx = cellX + dx;
                    int nz = cellZ + dz;

                    for (int candidate = 0; candidate < entry.CandidatesPerCell; candidate++)
                    {
                        if (nx == cellX && nz == cellZ && candidate == currentCandidate) continue;

                        if (!TryBuildCandidate(worldSeed, entry, salt, nx, nz, candidate, cellSize,
                                out Vector3 other, out uint otherPriority, out _))
                            continue;

                        float dxw = other.x - anchor.x;
                        float dzw = other.z - anchor.z;
                        if (dxw * dxw + dzw * dzw > radiusSqr) continue;

                        // Hoà thì phá bằng toạ độ ô, để so sánh luôn phản đối xứng.
                        if (otherPriority > priority) return true;
                        if (otherPriority == priority && ComesBefore(
                                nx, nz, candidate, cellX, cellZ, currentCandidate)) return true;
                    }
                }
            }

            return false;
        }

        private static bool ComesBefore(int cellX, int cellZ, int candidate,
            int otherCellX, int otherCellZ, int otherCandidate)
        {
            if (cellZ != otherCellZ) return cellZ < otherCellZ;
            if (cellX != otherCellX) return cellX < otherCellX;
            return candidate < otherCandidate;
        }

        private static int FloorDiv(float value, float size) => (int)Math.Floor(value / (double)size);
    }
}
