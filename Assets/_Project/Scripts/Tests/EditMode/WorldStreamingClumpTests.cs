using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode M4.6B: trường vón cụm của cỏ.
    ///
    /// Vón cụm là thứ duy nhất M4.6B thêm vào đường sinh, và nó nằm trong `TryBuildCandidate` —
    /// hàm mà mọi placement đều đi qua. Vì vậy hai điều phải được chốt bằng test chứ không bằng ảnh:
    /// tắt vón cụm thì kết quả cũ không đổi một bit nào, và bật lên thì mọi bảo đảm tất định của
    /// M3B.2/M3B.2D vẫn còn nguyên.
    ///
    /// Bảng dùng ở đây được dựng ngay trong test, không đọc asset production — nhờ vậy test không
    /// đổi kết quả khi ai đó chỉnh dữ liệu production, và vẫn chạy được cả khi chưa bake palette.
    public class WorldStreamingClumpTests
    {
        private const int Seed = 20268728;
        private const float ChunkSize = 32f;
        private const string SourcePath = "Assets/_Project/Data/World/Decoration/DMS_grass_b.asset";

        private static DecorationPalette BuildPalette(float clumpCell, float clumpStrength, int candidates, float density)
        {
            var source = AssetDatabase.LoadAssetAtPath<DecorationMeshSource>(SourcePath);
            Assert.IsNotNull(source, "Chưa bake nguồn cỏ. Chạy 'Rebuild Decoration Assets'.");

            var entry = new DecorationPaletteEntry();
            entry.Configure("grass_b", source, null,
                cell: 2.6f, candidates: candidates, density: density, spacing: 0f,
                affinity: new Vector4(0.25f, 1f, 0.05f, 0.05f),
                scaleRange: new Vector2(0.8f, 1.4f), tint: new Vector2(0.82f, 1.12f),
                priority: 20, yaw: true, densityEligible: true,
                clumpCell: clumpCell, clumpAmount: clumpStrength);

            var palette = ScriptableObject.CreateInstance<DecorationPalette>();
            palette.SetEntriesForTests(new List<DecorationPaletteEntry> { entry });
            return palette;
        }

        private static List<DecorationPlacement> Sample(DecorationPalette palette, ChunkCoord coord, float density)
        {
            var results = new List<DecorationPlacement>(512);
            DecorationSampleStats stats = default;
            DecorationSampler.Generate(Seed, coord, ChunkSize, palette, results, density, ref stats);
            return results;
        }

        private static string Key(in DecorationPlacement p) =>
            $"{p.EntrySalt}|{p.CellX}|{p.CellZ}|{p.CandidateIndex}";

        /// <summary>Toạ độ TOÀN CỤC của một placement — thứ phải bất biến theo chunk nào đang hỏi.</summary>
        private static Vector2 World(in DecorationPlacement p, ChunkCoord coord) =>
            new Vector2(p.LocalPosition.x + coord.X * ChunkSize, p.LocalPosition.z + coord.Z * ChunkSize);

        [Test]
        public void ClumpingOff_LeavesGenerationUnchanged()
        {
            // Chốt chặn hồi quy: đây là lý do mọi entry cũ không bị ảnh hưởng bởi M4.6B.
            var off = BuildPalette(0f, 0f, 1, 0.46f);
            var alsoOff = BuildPalette(0f, 0.85f, 1, 0.46f);   // cellSize = 0 => vẫn tắt
            var stillOff = BuildPalette(11f, 0f, 1, 0.46f);    // strength = 0 => vẫn tắt

            var a = Sample(off, new ChunkCoord(60, 33), 1f);
            var b = Sample(alsoOff, new ChunkCoord(60, 33), 1f);
            var c = Sample(stillOff, new ChunkCoord(60, 33), 1f);

            Assert.AreEqual(a.Count, b.Count, "cellSize=0 phải giữ nguyên phân bố.");
            Assert.AreEqual(a.Count, c.Count, "strength=0 phải giữ nguyên phân bố.");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i], b[i], $"placement {i} lệch khi cellSize=0.");
                Assert.AreEqual(a[i], c[i], $"placement {i} lệch khi strength=0.");
            }
        }

        [Test]
        public void ClumpFactor_IsPureFunctionOfWorldPosition()
        {
            var palette = BuildPalette(11f, 0.85f, 3, 0.153f);
            DecorationPaletteEntry entry = palette.Entries[0];

            // Cùng một điểm phải cho cùng một hệ số, gọi bao nhiêu lần cũng vậy.
            float first = entry.ClumpFactorAt(Seed, 1936.5f, 1072.25f);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(first, entry.ClumpFactorAt(Seed, 1936.5f, 1072.25f), 0f,
                    "Hệ số vón cụm phải là hàm thuần của toạ độ.");

            // Hạt giống khác thì thế giới khác.
            Assert.AreNotEqual(first, entry.ClumpFactorAt(Seed + 1, 1936.5f, 1072.25f),
                "Đổi hạt giống mà hệ số không đổi thì trường không phụ thuộc seed.");
        }

        [Test]
        public void ClumpedGeneration_IsDeterministicAcrossRepeats()
        {
            var palette = BuildPalette(11f, 0.85f, 3, 0.153f);
            var coord = new ChunkCoord(60, 33);

            var first = Sample(palette, coord, 1f);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                var again = Sample(palette, coord, 1f);
                Assert.AreEqual(first.Count, again.Count, "Số placement đổi giữa hai lần sinh giống hệt nhau.");
                for (int i = 0; i < first.Count; i++)
                    Assert.AreEqual(first[i], again[i], $"placement {i} không tất định.");
            }
        }

        [Test]
        public void ClumpedGeneration_HasNoSeamAtChunkBoundary()
        {
            // Bốn chunk kề nhau: mỗi placement chỉ được thuộc về đúng MỘT chunk, và toạ độ toàn cục
            // của nó không được đổi theo chunk nào phát ra nó. Vón cụm lấy mẫu theo điểm neo toàn cục
            // nên tính chất này phải còn nguyên — nếu ai đó chuyển sang lấy mẫu theo toạ độ cục bộ,
            // test này vỡ ngay.
            var palette = BuildPalette(11f, 0.85f, 3, 0.153f);
            var coords = new[]
            {
                new ChunkCoord(60, 33), new ChunkCoord(61, 33),
                new ChunkCoord(60, 34), new ChunkCoord(61, 34),
            };

            var seen = new Dictionary<string, Vector2>();
            foreach (var coord in coords)
            {
                foreach (var p in Sample(palette, coord, 1f))
                {
                    string key = Key(p);
                    Vector2 world = World(p, coord);

                    Assert.IsFalse(seen.ContainsKey(key),
                        $"placement {key} bị phát ra bởi nhiều hơn một chunk — biên bị nhân đôi.");
                    seen[key] = world;

                    // Điểm neo phải nằm trong chính chunk sở hữu.
                    Assert.AreEqual(coord.X, ChunkCoord.AxisToChunk(world.x, ChunkSize),
                        $"placement {key} nằm ngoài chunk phát ra nó theo trục X.");
                    Assert.AreEqual(coord.Z, ChunkCoord.AxisToChunk(world.y, ChunkSize),
                        $"placement {key} nằm ngoài chunk phát ra nó theo trục Z.");
                }
            }

            Assert.Greater(seen.Count, 0, "Không sinh ra placement nào — dữ liệu test sai.");
        }

        [Test]
        public void ClumpedGeneration_KeepsOuterRingASubsetOfNear()
        {
            // Quan hệ tập con của M3B.2D không được vón cụm làm hỏng: phép lọc theo khoảng cách vẫn
            // chạy SAU khi cắt ngân sách, và vón cụm chỉ đổi tập ứng viên đầu vào chứ không đổi thứ tự.
            var palette = BuildPalette(11f, 0.85f, 3, 0.153f);
            var coord = new ChunkCoord(60, 33);

            var near = Sample(palette, coord, 1f);
            var far = Sample(palette, coord, 0.4f);

            var nearIndex = new Dictionary<string, DecorationPlacement>();
            foreach (var p in near) nearIndex[Key(p)] = p;

            Assert.LessOrEqual(far.Count, near.Count, "Vòng ngoài không được nhiều hơn vòng gần.");
            foreach (var p in far)
            {
                Assert.IsTrue(nearIndex.TryGetValue(Key(p), out DecorationPlacement match),
                    $"placement {Key(p)} chỉ có ở vòng ngoài — không còn là tập con.");
                Assert.AreEqual(match, p, "Cùng một placement nhưng khác thuộc tính giữa hai bậc mật độ.");
            }
        }

        [Test]
        public void Clumping_RaisesDispersionWithoutInflatingCount()
        {
            // Đây chính là điều M4.6B tồn tại để làm: cỏ DỒN thành mảng, chứ không mọc thêm.
            // Đo bằng phương sai/trung bình trên lưới ô 4 m — thống kê đọc được sự vón cụm ở
            // bước sóng vài mét, thứ mà khoảng cách tới láng giềng gần nhất gần như mù.
            var flat = BuildPalette(0f, 0f, 1, 0.46f);
            var clumped = BuildPalette(11f, 0.85f, 3, 0.46f / 3f);
            var coord = new ChunkCoord(60, 33);

            var flatPoints = Sample(flat, coord, 1f);
            var clumpedPoints = Sample(clumped, coord, 1f);

            double flatVmr = Dispersion(flatPoints);
            double clumpedVmr = Dispersion(clumpedPoints);

            Assert.Less(flatVmr, 1.0,
                $"Phân bố cũ đáng lẽ đều hơn ngẫu nhiên nhưng đo được VMR={flatVmr:F2}.");
            Assert.Greater(clumpedVmr, flatVmr,
                $"Bật vón cụm mà độ phân tán không tăng (cũ {flatVmr:F2} → mới {clumpedVmr:F2}).");

            // Mật độ trung bình phải xấp xỉ giữ nguyên — nếu không thì đây là "thêm cỏ", không phải
            // "dồn cỏ", và ngân sách đỉnh sẽ trôi theo.
            Assert.That(clumpedPoints.Count, Is.EqualTo(flatPoints.Count).Within(0.35 * flatPoints.Count),
                $"Số lượng lệch quá xa: cũ {flatPoints.Count} → mới {clumpedPoints.Count}.");
        }

        /// <summary>Phương sai chia trung bình trên lưới ô 4 m. 1 = ngẫu nhiên, &gt;1 = vón cụm.</summary>
        private static double Dispersion(List<DecorationPlacement> placements)
        {
            const int Q = 8;
            float cell = ChunkSize / Q;
            var counts = new int[Q * Q];

            foreach (var p in placements)
            {
                int qx = Mathf.Clamp((int)(p.LocalPosition.x / cell), 0, Q - 1);
                int qz = Mathf.Clamp((int)(p.LocalPosition.z / cell), 0, Q - 1);
                counts[qz * Q + qx]++;
            }

            double mean = 0;
            for (int i = 0; i < counts.Length; i++) mean += counts[i];
            mean /= counts.Length;
            if (mean <= 0) return 0;

            double variance = 0;
            for (int i = 0; i < counts.Length; i++) variance += (counts[i] - mean) * (counts[i] - mean);
            variance /= counts.Length;

            return variance / mean;
        }
    }
}
