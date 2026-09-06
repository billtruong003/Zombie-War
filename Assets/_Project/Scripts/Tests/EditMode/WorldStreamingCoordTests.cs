using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode: chuyển đổi toạ độ chunk và tập ring bắt buộc.
    /// Đây là phần thuần tính toán của M0 — không đụng scene object nào.
    public class WorldStreamingCoordTests
    {
        private const float ChunkSize = 32f;

        // --- Chuyển đổi world → chunk -------------------------------------------------------

        [TestCase(0f, 0)]
        [TestCase(0.1f, 0)]
        [TestCase(31.9f, 0)]
        [TestCase(32f, 1)]
        [TestCase(32.1f, 1)]
        [TestCase(63.9f, 1)]
        [TestCase(64f, 2)]
        [TestCase(1024f, 32)]
        [TestCase(-0.1f, -1)]
        [TestCase(-31.9f, -1)]
        [TestCase(-32f, -1)]
        [TestCase(-32.1f, -2)]
        [TestCase(-63.9f, -2)]
        [TestCase(-64f, -2)]
        [TestCase(-64.1f, -3)]
        [TestCase(-1024f, -32)]
        public void AxisToChunk_UsesFloorSemantics(float worldAxis, int expected)
        {
            Assert.AreEqual(expected, ChunkCoord.AxisToChunk(worldAxis, ChunkSize),
                $"floor({worldAxis} / {ChunkSize}) phải bằng {expected}.");
        }

        [Test]
        public void AxisToChunk_IsFloor_AroundEveryBoundary_NotTruncation()
        {
            // Chứng minh tính floor một cách tổng quát chứ không chỉ khớp vài ví dụ:
            // tại mọi ranh giới k×chunkSize, lệch âm một chút phải rơi xuống k-1, lệch dương giữ nguyên k.
            const float epsilon = 0.01f;

            for (int k = -6; k <= 6; k++)
            {
                float boundary = k * ChunkSize;

                Assert.AreEqual(k, ChunkCoord.AxisToChunk(boundary, ChunkSize),
                    $"Đúng tại ranh giới {boundary} phải thuộc chunk {k}.");
                Assert.AreEqual(k, ChunkCoord.AxisToChunk(boundary + epsilon, ChunkSize),
                    $"{boundary}+ε vẫn phải thuộc chunk {k}.");
                Assert.AreEqual(k - 1, ChunkCoord.AxisToChunk(boundary - epsilon, ChunkSize),
                    $"{boundary}-ε phải rơi xuống chunk {k - 1}.");
            }
        }

        [Test]
        public void AxisToChunk_DiffersFromIntegerCast_OnNegativeCoordinates()
        {
            // Bẫy kinh điển: (int)(-0.1f / 32f) == 0 vì C# cắt về 0. Đáp án đúng là -1.
            foreach (float value in new[] { -0.1f, -1f, -31.9f })
            {
                Assert.AreEqual(0, (int)(value / ChunkSize), "Tiền đề: ép kiểu int trả 0 ở đây.");
                Assert.AreEqual(-1, ChunkCoord.AxisToChunk(value, ChunkSize),
                    $"{value} phải thuộc chunk -1, không phải 0.");
            }
        }

        [Test]
        public void FromWorld_ConvertsBothAxesIndependently()
        {
            var coord = ChunkCoord.FromWorld(new Vector3(-0.1f, 99f, 64.5f), ChunkSize);
            Assert.AreEqual(-1, coord.X);
            Assert.AreEqual(2, coord.Z);

            var mixed = ChunkCoord.FromWorld(70f, -70f, ChunkSize);
            Assert.AreEqual(2, mixed.X);
            Assert.AreEqual(-3, mixed.Z);
        }

        [Test]
        public void FromWorld_ResolvesLargeTeleportCoordinates()
        {
            Assert.AreEqual(new ChunkCoord(64, 64), ChunkCoord.FromWorld(2048f, 2048f, ChunkSize));
            Assert.AreEqual(new ChunkCoord(-96, -50), ChunkCoord.FromWorld(-3072f, -1600f, ChunkSize));
            Assert.AreEqual(new ChunkCoord(-97, -51), ChunkCoord.FromWorld(-3072.5f, -1600.5f, ChunkSize));
        }

        [Test]
        public void ToWorldMin_RoundTripsBackToSameCoord()
        {
            foreach (var coord in new[]
                     {
                         new ChunkCoord(0, 0), new ChunkCoord(3, -7), new ChunkCoord(-11, -13), new ChunkCoord(-1, 5),
                     })
            {
                Vector3 min = coord.ToWorldMin(ChunkSize);
                Assert.AreEqual(coord, ChunkCoord.FromWorld(min, ChunkSize), "Góc min phải nằm trong chính chunk đó.");

                Vector3 center = coord.ToWorldCenter(ChunkSize);
                Assert.AreEqual(coord, ChunkCoord.FromWorld(center, ChunkSize), "Tâm phải nằm trong chính chunk đó.");
            }
        }

        [Test]
        public void AxisToChunk_RejectsNonPositiveChunkSize()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ChunkCoord.AxisToChunk(10f, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ChunkCoord.AxisToChunk(10f, -32f));
        }

        [Test]
        public void Equality_AndHash_AreValueBased()
        {
            Assert.AreEqual(new ChunkCoord(-4, 9), new ChunkCoord(-4, 9));
            Assert.AreEqual(new ChunkCoord(-4, 9).GetHashCode(), new ChunkCoord(-4, 9).GetHashCode());
            Assert.AreNotEqual(new ChunkCoord(-4, 9), new ChunkCoord(9, -4));
            Assert.IsTrue(new ChunkCoord(1, 2) == new ChunkCoord(1, 2));
            Assert.IsTrue(new ChunkCoord(1, 2) != new ChunkCoord(2, 1));
            Assert.AreEqual("(-4,9)", new ChunkCoord(-4, 9).ToString());
        }

        // --- Tập ring bắt buộc ---------------------------------------------------------------

        private static IEnumerable<ChunkCoord> RepresentativeCenters()
        {
            yield return new ChunkCoord(0, 0);
            yield return new ChunkCoord(7, 11);
            yield return new ChunkCoord(-7, -11);
            yield return new ChunkCoord(-5, 8);
            yield return new ChunkCoord(9, -14);
            yield return new ChunkCoord(-1, -1);
            yield return new ChunkCoord(64, 64);
            yield return new ChunkCoord(-96, -50);
        }

        [Test]
        public void RingCount_MatchesSquareOfDiameter()
        {
            Assert.AreEqual(1, ChunkCoord.RingCount(0));
            Assert.AreEqual(9, ChunkCoord.RingCount(1));
            Assert.AreEqual(25, ChunkCoord.RingCount(2));
            Assert.AreEqual(49, ChunkCoord.RingCount(3));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ChunkCoord.RingCount(-1));
        }

        [Test]
        public void EnumerateRing_ProducesExactly25UniqueCoords_AroundEveryRepresentativeCenter()
        {
            foreach (ChunkCoord center in RepresentativeCenters())
            {
                List<ChunkCoord> ring = ChunkCoord.EnumerateRing(center, 2);

                Assert.AreEqual(25, ring.Count, $"Tâm {center}: phải đúng 25 toạ độ.");
                Assert.AreEqual(25, new HashSet<ChunkCoord>(ring).Count, $"Tâm {center}: không được trùng toạ độ.");
                CollectionAssert.Contains(ring, center, $"Tâm {center}: tập bắt buộc phải chứa chính tâm.");

                int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
                foreach (ChunkCoord c in ring)
                {
                    minX = Mathf.Min(minX, c.X);
                    maxX = Mathf.Max(maxX, c.X);
                    minZ = Mathf.Min(minZ, c.Z);
                    maxZ = Mathf.Max(maxZ, c.Z);
                }

                Assert.AreEqual(center.X - 2, minX, $"Tâm {center}: minX.");
                Assert.AreEqual(center.X + 2, maxX, $"Tâm {center}: maxX.");
                Assert.AreEqual(center.Z - 2, minZ, $"Tâm {center}: minZ.");
                Assert.AreEqual(center.Z + 2, maxZ, $"Tâm {center}: maxZ.");
            }
        }

        [Test]
        public void EnumerateRing_IsDeterministic_InSetAndOrder()
        {
            var center = new ChunkCoord(-5, 8);

            List<ChunkCoord> first = ChunkCoord.EnumerateRing(center, 2);

            // Gọi xen một tâm khác để chắc chắn không có trạng thái nhớ giữa các lần gọi.
            ChunkCoord.EnumerateRing(new ChunkCoord(1000, -1000), 2);

            List<ChunkCoord> second = ChunkCoord.EnumerateRing(center, 2);

            CollectionAssert.AreEqual(first, second, "Cùng tâm + cùng radius phải cho cùng thứ tự, không chỉ cùng tập.");

            var reused = new List<ChunkCoord>();
            ChunkCoord.EnumerateRing(center, 2, reused);
            CollectionAssert.AreEqual(first, reused, "Bản ghi vào list tái sử dụng phải trùng bản cấp phát mới.");

            ChunkCoord.EnumerateRing(center, 2, reused);
            Assert.AreEqual(25, reused.Count, "Gọi lại trên cùng list phải Clear trước, không được cộng dồn.");
        }

        [Test]
        public void EnumerateRing_RejectsInvalidArguments()
        {
            Assert.Throws<System.ArgumentNullException>(() => ChunkCoord.EnumerateRing(ChunkCoord.Zero, 2, null));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ChunkCoord.EnumerateRing(ChunkCoord.Zero, -1, new List<ChunkCoord>()));
        }

        // --- Cấu hình ------------------------------------------------------------------------

        [Test]
        public void Config_DefaultsMatchM0Hypothesis()
        {
            WorldStreamingConfig config = WorldStreamingConfig.CreateRuntime();
            try
            {
                Assert.AreEqual(32f, config.ChunkSize);
                Assert.AreEqual(2, config.RenderRadius);
                Assert.AreEqual(5, config.ActiveDiameter);
                Assert.AreEqual(25, config.ActiveChunkCount);
                Assert.AreEqual(25, config.PoolCapacity);
                Assert.IsTrue(config.Validate(out string error), error);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [TestCase(0f, 2, 256f, "chunkSize")]
        [TestCase(-32f, 2, 256f, "chunkSize")]
        [TestCase(32f, -1, 256f, "renderRadius")]
        [TestCase(32f, 2, 64f, "surfaceFootprint")]
        public void Config_FailsLoudly_OnInvalidValues(float chunkSize, int radius, float footprint, string expectedMention)
        {
            WorldStreamingConfig config = WorldStreamingConfig.CreateRuntime(chunkSize, radius, 0, footprint);
            try
            {
                Assert.IsFalse(config.Validate(out string error), "Cấu hình sai phải bị từ chối.");
                StringAssert.Contains(expectedMention, error);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
