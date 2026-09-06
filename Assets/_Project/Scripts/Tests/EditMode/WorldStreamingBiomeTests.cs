using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode: trường biome toàn cục và mesh nền sinh ra từ nó.
    ///
    /// Bài quan trọng nhất ở đây là <see cref="SharedBorderVertices_ProduceIdenticalWeights"/>:
    /// nếu hai chunk kề nhau không lấy mẫu đúng cùng một vị trí logic thì sẽ có đường nối,
    /// và không có mẹo hiển thị nào che được nó.
    public class WorldStreamingBiomeTests
    {
        private const int Seed = 20260809;
        private const float ChunkSize = 32f;
        private const int Resolution = 17;

        private static IEnumerable<Vector2> RepresentativePositions()
        {
            yield return new Vector2(0f, 0f);
            yield return new Vector2(7.5f, 11.25f);
            yield return new Vector2(-7.5f, -11.25f);
            yield return new Vector2(-64f, 96f);
            yield return new Vector2(1024.5f, -2048.25f);
            yield return new Vector2(-3072f, -1600f);
            yield return new Vector2(0.001f, -0.001f);
            yield return new Vector2(31.9f, 32.1f);
        }

        // --- Tính tất định --------------------------------------------------------------------

        [Test]
        public void SameSeedAndPosition_AlwaysProduceIdenticalSample()
        {
            foreach (Vector2 p in RepresentativePositions())
            {
                BiomeSample first = BiomeSampler.Sample(Seed, p.x, p.y);

                // Lấy mẫu chỗ khác xen vào giữa: nếu sampler có trạng thái ẩn, đây là chỗ nó lộ ra.
                BiomeSampler.Sample(Seed, p.x + 137.7f, p.y - 913.3f);
                BiomeSampler.Sample(Seed + 1, p.x, p.y);

                BiomeSample second = BiomeSampler.Sample(Seed, p.x, p.y);

                Assert.AreEqual(first.Dry, second.Dry, 0f, $"{p}: R phải trùng tuyệt đối.");
                Assert.AreEqual(first.Grass, second.Grass, 0f, $"{p}: G phải trùng tuyệt đối.");
                Assert.AreEqual(first.Sand, second.Sand, 0f, $"{p}: B phải trùng tuyệt đối.");
                Assert.AreEqual(first.Rock, second.Rock, 0f, $"{p}: A phải trùng tuyệt đối.");
                Assert.AreEqual(first.Moisture, second.Moisture, 0f, $"{p}: trường thô phải trùng.");
                Assert.AreEqual(first.Fertility, second.Fertility, 0f);
                Assert.AreEqual(first.Rockiness, second.Rockiness, 0f);
                Assert.AreEqual(first.Macro, second.Macro, 0f);
            }
        }

        [Test]
        public void SamplingOrder_DoesNotChangeResults()
        {
            var positions = new List<Vector2>(RepresentativePositions());

            var forward = new List<BiomeSample>();
            for (int i = 0; i < positions.Count; i++)
                forward.Add(BiomeSampler.Sample(Seed, positions[i].x, positions[i].y));

            var backward = new BiomeSample[positions.Count];
            for (int i = positions.Count - 1; i >= 0; i--)
                backward[i] = BiomeSampler.Sample(Seed, positions[i].x, positions[i].y);

            for (int i = 0; i < positions.Count; i++)
            {
                Assert.AreEqual(forward[i].Dry, backward[i].Dry, 0f, $"{positions[i]}: đảo thứ tự làm đổi kết quả.");
                Assert.AreEqual(forward[i].Grass, backward[i].Grass, 0f);
                Assert.AreEqual(forward[i].Sand, backward[i].Sand, 0f);
                Assert.AreEqual(forward[i].Rock, backward[i].Rock, 0f);
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentFields()
        {
            int differing = 0;
            var positions = new List<Vector2>(RepresentativePositions());

            foreach (Vector2 p in positions)
            {
                BiomeSample a = BiomeSampler.Sample(Seed, p.x, p.y);
                BiomeSample b = BiomeSampler.Sample(Seed + 977, p.x, p.y);

                if (Mathf.Abs(a.Dry - b.Dry) > 1e-4f || Mathf.Abs(a.Grass - b.Grass) > 1e-4f ||
                    Mathf.Abs(a.Sand - b.Sand) > 1e-4f || Mathf.Abs(a.Rock - b.Rock) > 1e-4f)
                {
                    differing++;
                }
            }

            Assert.Greater(differing, positions.Count / 2,
                "Đổi hạt giống phải làm đổi phần lớn mẫu đại diện — không đòi hỏi mọi toạ độ đều đổi.");
        }

        // --- Tính hợp lệ ----------------------------------------------------------------------

        [Test]
        public void WeightsAreFinite_Bounded_AndNormalized()
        {
            int samples = 0;
            for (int z = -6; z <= 6; z++)
            {
                for (int x = -6; x <= 6; x++)
                {
                    float worldX = x * 53.5f;
                    float worldZ = z * 47.25f;
                    BiomeSample s = BiomeSampler.Sample(Seed, worldX, worldZ);
                    samples++;

                    foreach ((float value, string name) in new[]
                             { (s.Dry, "R"), (s.Grass, "G"), (s.Sand, "B"), (s.Rock, "A") })
                    {
                        Assert.IsFalse(float.IsNaN(value), $"({worldX},{worldZ}) {name} là NaN.");
                        Assert.IsFalse(float.IsInfinity(value), $"({worldX},{worldZ}) {name} là vô cực.");
                        Assert.GreaterOrEqual(value, 0f, $"({worldX},{worldZ}) {name} < 0.");
                        Assert.LessOrEqual(value, 1f, $"({worldX},{worldZ}) {name} > 1.");
                    }

                    Assert.AreEqual(1f, s.WeightSum, 1e-4f, $"({worldX},{worldZ}) tổng bốn trọng số phải ≈ 1.");
                }
            }

            Assert.Greater(samples, 100);
        }

        [Test]
        public void LargeAndMixedSignCoordinates_StayValid()
        {
            foreach (Vector2 p in new[]
                     {
                         new Vector2(0f, 0f), new Vector2(50000f, -50000f),
                         new Vector2(-123456.5f, 98765.25f), new Vector2(-1f, 1f),
                     })
            {
                BiomeSample s = BiomeSampler.Sample(Seed, p.x, p.y);
                Assert.AreEqual(1f, s.WeightSum, 1e-3f, $"{p}: tổng trọng số hỏng ở toạ độ lớn.");
                Assert.IsFalse(float.IsNaN(s.Grass), $"{p}: NaN ở toạ độ lớn.");
            }
        }

        [Test]
        public void FieldIsNotConstant()
        {
            float minGrass = float.MaxValue, maxGrass = float.MinValue;
            float minRock = float.MaxValue, maxRock = float.MinValue;

            for (int i = 0; i < 400; i++)
            {
                float worldX = (i % 20) * 61f - 600f;
                float worldZ = (i / 20) * 57f - 570f;
                BiomeSample s = BiomeSampler.Sample(Seed, worldX, worldZ);

                minGrass = Mathf.Min(minGrass, s.Grass);
                maxGrass = Mathf.Max(maxGrass, s.Grass);
                minRock = Mathf.Min(minRock, s.Rock);
                maxRock = Mathf.Max(maxRock, s.Rock);
            }

            Assert.Greater(maxGrass - minGrass, 0.15f, "Kênh cỏ gần như không đổi — trường bị phẳng.");
            Assert.Greater(maxRock - minRock, 0.15f, "Kênh đá gần như không đổi — trường bị phẳng.");
        }

        // --- Tính liên tục --------------------------------------------------------------------

        [Test]
        public void NearbySamples_ChangeContinuously()
        {
            // Bước 0.25 m: trường phải nhúc nhích (không phải hằng số theo ô) nhưng không nhảy vọt
            // (không phải gán biome cứng theo ô nguyên).
            const float step = 0.25f;
            float maxDelta = 0f;
            int moved = 0;

            for (int i = 0; i < 600; i++)
            {
                float worldX = -240f + i * 0.83f;
                float worldZ = 137.5f - i * 0.61f;

                BiomeSample a = BiomeSampler.Sample(Seed, worldX, worldZ);
                BiomeSample b = BiomeSampler.Sample(Seed, worldX + step, worldZ + step);

                float delta = Mathf.Abs(a.Dry - b.Dry) + Mathf.Abs(a.Grass - b.Grass)
                              + Mathf.Abs(a.Sand - b.Sand) + Mathf.Abs(a.Rock - b.Rock);

                Assert.IsFalse(float.IsNaN(delta), "Xuất hiện NaN khi đi bước nhỏ.");
                maxDelta = Mathf.Max(maxDelta, delta);

                // Đếm trên trường THÔ: trọng số đã chuẩn hoá có thể đứng yên hợp lệ ở vùng mà ba kênh
                // kia đều bị kẹp về 0, còn trường thô thì không bao giờ.
                if (Mathf.Abs(a.Moisture - b.Moisture) > 1e-7f) moved++;
            }

            Assert.Less(maxDelta, 0.20f, "Một bước 0.25 m làm trường nhảy quá mạnh — nghi gán biome cứng theo ô.");
            Assert.Greater(moved, 550, "Trường gần như không đổi giữa các bước — nghi lấy mẫu theo số nguyên.");
        }

        [Test]
        public void IntegerLatticePositions_AreNotSpecial()
        {
            // Nếu ai đó lỡ lấy mẫu bằng ép kiểu int, giá trị ở x và x+0.5 sẽ bằng nhau.
            for (int i = -4; i <= 4; i++)
            {
                BiomeSample onLattice = BiomeSampler.Sample(Seed, i * 32f, i * 32f);
                BiomeSample offLattice = BiomeSampler.Sample(Seed, i * 32f + 0.5f, i * 32f + 0.5f);

                Assert.AreNotEqual(onLattice.Moisture, offLattice.Moisture,
                    $"i={i}: hai vị trí cách nhau 0.5 m cho kết quả y hệt — trường đang bị lượng tử hoá.");
            }
        }

        // --- Hình học mesh nền ----------------------------------------------------------------

        [Test]
        public void GroundMesh_HasExpectedTopology()
        {
            Mesh mesh = GroundMeshBuilder.CreateGroundMesh("TopologyTest", Resolution, ChunkSize);
            try
            {
                Assert.AreEqual(289, mesh.vertexCount, "17×17 phải cho 289 đỉnh.");
                Assert.AreEqual(1, mesh.subMeshCount, "Mesh nền chỉ được có một submesh.");

                int[] indices = mesh.GetTriangles(0);
                Assert.AreEqual(1536, indices.Length, "512 tam giác → 1536 chỉ số.");
                Assert.AreEqual(512, indices.Length / 3);

                foreach (int index in indices)
                {
                    Assert.GreaterOrEqual(index, 0, "Chỉ số âm.");
                    Assert.Less(index, mesh.vertexCount, "Chỉ số vượt số đỉnh.");
                }

                Assert.AreEqual(mesh.vertexCount, mesh.normals.Length, "Thiếu normal.");
                Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, "Thiếu UV.");
                Assert.AreEqual(mesh.vertexCount, mesh.colors.Length, "Thiếu vertex color.");

                foreach (Vector3 position in mesh.vertices)
                    Assert.AreEqual(0f, position.y, 0f, "Mesh nền M1 phải hoàn toàn phẳng ở Y = 0.");

                foreach (Vector3 normal in mesh.normals)
                    Assert.AreEqual(Vector3.up, normal, "Normal phải hướng lên.");

                Assert.AreEqual(ChunkSize, mesh.bounds.size.x, 0.001f, "Bounds phải phủ đủ 32 m theo X.");
                Assert.AreEqual(ChunkSize, mesh.bounds.size.z, 0.001f, "Bounds phải phủ đủ 32 m theo Z.");
                Assert.AreEqual(0f, mesh.bounds.min.x, 0.001f, "Mesh bắt đầu tại góc min của chunk.");
                Assert.AreEqual(0f, mesh.bounds.min.z, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void GroundMesh_CoversFullChunkWithoutGap()
        {
            Mesh mesh = GroundMeshBuilder.CreateGroundMesh("CoverageTest", Resolution, ChunkSize);
            try
            {
                // M0 chừa khe để nhìn biên; M1 phải phủ kín, biên chỉ còn nhìn qua Gizmo.
                Assert.AreEqual(0f, mesh.bounds.min.x, 1e-4f);
                Assert.AreEqual(0f, mesh.bounds.min.z, 1e-4f);
                Assert.AreEqual(ChunkSize, mesh.bounds.max.x, 1e-4f);
                Assert.AreEqual(ChunkSize, mesh.bounds.max.z, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ApplyBiome_WritesWeightsMatchingDirectSampling()
        {
            Mesh mesh = GroundMeshBuilder.CreateGroundMesh("ApplyTest", Resolution, ChunkSize);
            try
            {
                var coord = new ChunkCoord(-3, 5);
                GroundMeshBuilder.ApplyBiome(mesh, coord, Resolution, ChunkSize, Seed);

                Color[] colors = mesh.colors;
                Assert.AreEqual(289, colors.Length);

                for (int iz = 0; iz < Resolution; iz += 4)
                {
                    for (int ix = 0; ix < Resolution; ix += 4)
                    {
                        BiomeSample expected = GroundMeshBuilder.SampleVertex(Seed, coord, ix, iz, Resolution, ChunkSize);
                        Color actual = colors[iz * Resolution + ix];

                        // Vertex color của Unity lưu 8 bit mỗi kênh, nên so sánh ở dung sai lượng tử hoá.
                        Assert.AreEqual(expected.Dry, actual.r, 1f / 255f, $"({ix},{iz}) R lệch.");
                        Assert.AreEqual(expected.Grass, actual.g, 1f / 255f, $"({ix},{iz}) G lệch.");
                        Assert.AreEqual(expected.Sand, actual.b, 1f / 255f, $"({ix},{iz}) B lệch.");
                        Assert.AreEqual(expected.Rock, actual.a, 1f / 255f, $"({ix},{iz}) A lệch.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        // --- Đường nối: bài kiểm tra chính của M1 ------------------------------------------------

        private static IEnumerable<(ChunkCoord A, ChunkCoord B, string Label)> BorderPairs()
        {
            yield return (new ChunkCoord(0, 0), new ChunkCoord(1, 0), "+X tại gốc");
            yield return (new ChunkCoord(0, 0), new ChunkCoord(-1, 0), "-X qua mốc 0");
            yield return (new ChunkCoord(0, 0), new ChunkCoord(0, 1), "+Z tại gốc");
            yield return (new ChunkCoord(0, 0), new ChunkCoord(0, -1), "-Z qua mốc 0");
            yield return (new ChunkCoord(-4, -7), new ChunkCoord(-3, -7), "âm sang âm theo X");
            yield return (new ChunkCoord(-4, -7), new ChunkCoord(-4, -6), "âm sang âm theo Z");
            yield return (new ChunkCoord(-1, 3), new ChunkCoord(0, 3), "lệch dấu qua mốc 0 theo X");
            yield return (new ChunkCoord(6, -1), new ChunkCoord(6, 0), "lệch dấu qua mốc 0 theo Z");
            yield return (new ChunkCoord(-96, -50), new ChunkCoord(-95, -50), "toạ độ lớn âm");
            yield return (new ChunkCoord(312, -487), new ChunkCoord(312, -486), "toạ độ lớn lệch dấu");
        }

        [Test]
        public void SharedBorderVertices_ProduceIdenticalWeights()
        {
            const float tolerance = 1e-6f;
            int comparedVertices = 0;
            int comparedPairs = 0;

            foreach ((ChunkCoord a, ChunkCoord b, string label) in BorderPairs())
            {
                comparedPairs++;
                bool alongX = b.X != a.X;
                Assert.IsTrue(alongX ^ (b.Z != a.Z), $"{label}: cặp phải kề nhau đúng một trục.");
                Assert.AreEqual(1, alongX ? Mathf.Abs(b.X - a.X) : Mathf.Abs(b.Z - a.Z), $"{label}: hai chunk phải kề nhau.");

                // Biên chung luôn là cạnh "cao" của chunk nhỏ hơn chạm cạnh "thấp" của chunk lớn hơn.
                // Cặp trong danh sách có thể viết theo chiều nào cũng được, nên sắp lại ở đây.
                bool aIsLower = alongX ? a.X < b.X : a.Z < b.Z;
                ChunkCoord lo = aIsLower ? a : b;
                ChunkCoord hi = aIsLower ? b : a;

                int last = Resolution - 1;

                for (int i = 0; i < Resolution; i++)
                {
                    int axLo = alongX ? last : i;
                    int azLo = alongX ? i : last;
                    int axHi = alongX ? 0 : i;
                    int azHi = alongX ? i : 0;

                    float worldXA = GroundMeshBuilder.VertexWorldAxis(lo.X, axLo, Resolution, ChunkSize);
                    float worldZA = GroundMeshBuilder.VertexWorldAxis(lo.Z, azLo, Resolution, ChunkSize);
                    float worldXB = GroundMeshBuilder.VertexWorldAxis(hi.X, axHi, Resolution, ChunkSize);
                    float worldZB = GroundMeshBuilder.VertexWorldAxis(hi.Z, azHi, Resolution, ChunkSize);

                    Assert.AreEqual(worldXA, worldXB, 0f, $"{label} i={i}: hai chunk lấy mẫu X khác nhau.");
                    Assert.AreEqual(worldZA, worldZB, 0f, $"{label} i={i}: hai chunk lấy mẫu Z khác nhau.");

                    BiomeSample sa = GroundMeshBuilder.SampleVertex(Seed, lo, axLo, azLo, Resolution, ChunkSize);
                    BiomeSample sb = GroundMeshBuilder.SampleVertex(Seed, hi, axHi, azHi, Resolution, ChunkSize);

                    Assert.AreEqual(sa.Dry, sb.Dry, tolerance, $"{label} i={i}: R lệch ở biên.");
                    Assert.AreEqual(sa.Grass, sb.Grass, tolerance, $"{label} i={i}: G lệch ở biên.");
                    Assert.AreEqual(sa.Sand, sb.Sand, tolerance, $"{label} i={i}: B lệch ở biên.");
                    Assert.AreEqual(sa.Rock, sb.Rock, tolerance, $"{label} i={i}: A lệch ở biên.");

                    comparedVertices++;
                }
            }

            Assert.AreEqual(10, comparedPairs, "Phải phủ đủ 10 cặp biên đại diện.");
            Assert.AreEqual(comparedPairs * Resolution, comparedVertices,
                "Phải so sánh đủ mọi đỉnh biên của mọi cặp.");
        }

        [Test]
        public void SharedBorderColors_MatchInGeneratedMeshes()
        {
            Mesh left = GroundMeshBuilder.CreateGroundMesh("BorderLeft", Resolution, ChunkSize);
            Mesh right = GroundMeshBuilder.CreateGroundMesh("BorderRight", Resolution, ChunkSize);
            try
            {
                var a = new ChunkCoord(-1, -1);
                var b = new ChunkCoord(0, -1);

                GroundMeshBuilder.ApplyBiome(left, a, Resolution, ChunkSize, Seed);
                GroundMeshBuilder.ApplyBiome(right, b, Resolution, ChunkSize, Seed);

                Color[] leftColors = left.colors;
                Color[] rightColors = right.colors;
                int last = Resolution - 1;

                for (int iz = 0; iz < Resolution; iz++)
                {
                    Color edgeOfLeft = leftColors[iz * Resolution + last];
                    Color edgeOfRight = rightColors[iz * Resolution + 0];

                    Assert.AreEqual(edgeOfLeft.r, edgeOfRight.r, 1e-5f, $"iz={iz}: R lệch giữa hai mesh.");
                    Assert.AreEqual(edgeOfLeft.g, edgeOfRight.g, 1e-5f, $"iz={iz}: G lệch giữa hai mesh.");
                    Assert.AreEqual(edgeOfLeft.b, edgeOfRight.b, 1e-5f, $"iz={iz}: B lệch giữa hai mesh.");
                    Assert.AreEqual(edgeOfLeft.a, edgeOfRight.a, 1e-5f, $"iz={iz}: A lệch giữa hai mesh.");
                }

                // Và mesh phải khớp về mặt hình học: cạnh phải của A nằm đúng chỗ cạnh trái của B.
                Vector3[] leftPositions = left.vertices;
                Vector3[] rightPositions = right.vertices;
                float leftEdgeWorldX = a.ToWorldMin(ChunkSize).x + leftPositions[last].x;
                float rightEdgeWorldX = b.ToWorldMin(ChunkSize).x + rightPositions[0].x;
                Assert.AreEqual(leftEdgeWorldX, rightEdgeWorldX, 1e-4f, "Hai mesh phải chạm nhau, không hở không chồng.");
            }
            finally
            {
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
            }
        }

        [Test]
        public void ChunkOriginVertex_MatchesChunkLogicalOrigin()
        {
            foreach (var coord in new[] { new ChunkCoord(0, 0), new ChunkCoord(-5, 9), new ChunkCoord(312, -487) })
            {
                float worldX = GroundMeshBuilder.VertexWorldAxis(coord.X, 0, Resolution, ChunkSize);
                float worldZ = GroundMeshBuilder.VertexWorldAxis(coord.Z, 0, Resolution, ChunkSize);

                Assert.AreEqual(coord.ToWorldMin(ChunkSize).x, worldX, 1e-3f, $"{coord}: đỉnh gốc lệch trục X.");
                Assert.AreEqual(coord.ToWorldMin(ChunkSize).z, worldZ, 1e-3f, $"{coord}: đỉnh gốc lệch trục Z.");
            }
        }

        // --- Cấu hình -------------------------------------------------------------------------

        [Test]
        public void Config_ExposesSeedAndGroundTopology()
        {
            WorldStreamingConfig config = WorldStreamingConfig.CreateRuntime();
            try
            {
                Assert.AreEqual(20260809, config.WorldSeed);
                Assert.AreEqual(17, config.GroundResolution);
                Assert.AreEqual(289, config.GroundVertexCount);
                Assert.AreEqual(512, config.GroundTriangleCount);
                Assert.IsTrue(config.Validate(out string error), error);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Config_RejectsDegenerateGroundResolution()
        {
            WorldStreamingConfig config = WorldStreamingConfig.CreateRuntime(groundResolution: 1);
            try
            {
                Assert.IsFalse(config.Validate(out string error), "resolution 1 không dựng được mesh.");
                StringAssert.Contains("groundResolution", error);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
