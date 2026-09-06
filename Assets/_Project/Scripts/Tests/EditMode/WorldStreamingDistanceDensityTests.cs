using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode M3B.2D: luật mật độ theo khoảng cách, đo trên chính bộ lấy mẫu.
    ///
    /// Yêu cầu quan trọng nhất ở đây là QUAN HỆ TẬP CON: tập vòng ngoài phải nằm gọn trong tập gần, và
    /// những placement có mặt ở cả hai bậc phải giống nhau đến từng thuộc tính. Nếu điều đó vỡ, cây cỏ
    /// sẽ nhảy chỗ mỗi lần người chơi băng qua ranh giới gần/xa — một lỗi rất khó thấy bằng ảnh chụp
    /// nhưng lộ ngay khi chơi.
    public class WorldStreamingDistanceDensityTests
    {
        private const int Seed = 20260809;
        private const float ChunkSize = 32f;
        private const string PalettePath = "Assets/_Project/Data/World/Decoration/DecorationPalette.asset";

        private static DecorationPalette LoadPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<DecorationPalette>(PalettePath);
            Assert.IsNotNull(palette, "Chưa bake palette. Chạy 'Rebuild Decoration Assets'.");
            return palette;
        }

        private static List<DecorationPlacement> Sample(ChunkCoord coord, float density)
        {
            var results = new List<DecorationPlacement>(512);
            DecorationSampleStats stats = default;
            DecorationSampler.Generate(Seed, coord, ChunkSize, LoadPalette(), results, density, ref stats);
            return results;
        }

        private static string Key(in DecorationPlacement p) =>
            $"{p.EntrySalt}|{p.CellX}|{p.CellZ}|{p.CandidateIndex}";

        private static Dictionary<string, DecorationPlacement> Index(List<DecorationPlacement> placements)
        {
            var map = new Dictionary<string, DecorationPlacement>(placements.Count);
            foreach (DecorationPlacement p in placements) map[Key(p)] = p;
            return map;
        }

        // --- Cấu hình -----------------------------------------------------------------------------

        [Test]
        public void Config_DefaultsMatchTheMilestoneSpecification()
        {
            WorldStreamingConfig config = WorldStreamingConfig.CreateRuntime();

            Assert.IsTrue(config.DistanceDensityEnabled);
            Assert.AreEqual(1, config.NearDensityRadius);
            Assert.AreEqual(1f, config.NearFoliageDensity);
            Assert.AreEqual(0.4f, config.OuterFoliageDensity);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void Config_RejectsInvalidDensities()
        {
            WorldStreamingConfig config = WorldStreamingConfig.CreateRuntime();
            try
            {
                config.SetDistanceDensity(true, 1, 1f, -0.1f);
                Assert.IsFalse(config.Validate(out string belowZero), "Mật độ âm phải bị chặn.");
                Assert.IsTrue(belowZero.Contains("outerFoliageDensity"), belowZero);

                config.SetDistanceDensity(true, 1, 1.5f, 0.4f);
                Assert.IsFalse(config.Validate(out string aboveOne), "Mật độ > 1 phải bị chặn.");
                Assert.IsTrue(aboveOne.Contains("nearFoliageDensity"), aboveOne);

                // Vòng ngoài dày hơn vùng gần thì quan hệ tập con đảo chiều — vô nghĩa.
                config.SetDistanceDensity(true, 1, 0.3f, 0.8f);
                Assert.IsFalse(config.Validate(out string inverted));
                Assert.IsTrue(inverted.Contains("không được lớn hơn"), inverted);

                config.SetDistanceDensity(true, 5, 1f, 0.4f);
                Assert.IsFalse(config.Validate(out string radiusTooBig));
                Assert.IsTrue(radiusTooBig.Contains("nearDensityRadius"), radiusTooBig);

                // Bật tính năng nhưng bán kính gần phủ kín ring: không còn bậc ngoài nào.
                config.SetDistanceDensity(true, 2, 1f, 0.4f);
                Assert.IsFalse(config.Validate(out string noOuter));
                Assert.IsTrue(noOuter.Contains("không còn chunk nào ở vòng ngoài"), noOuter);

                config.SetDistanceDensity(true, 1, 1f, 0.4f);
                Assert.IsTrue(config.Validate(out string ok), ok);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Tier_UsesChebyshevDistance()
        {
            WorldStreamingConfig config = WorldStreamingConfig.CreateRuntime();
            var origin = new ChunkCoord(10, 10);

            try
            {
                // Góc chéo (1,1) có khoảng cách Euclid 1.41 nhưng Chebyshev 1 — phải là GẦN.
                Assert.AreEqual(DecorationDensityTier.Near, config.TierFor(new ChunkCoord(11, 11), origin));
                Assert.AreEqual(DecorationDensityTier.Near, config.TierFor(new ChunkCoord(9, 9), origin));
                Assert.AreEqual(DecorationDensityTier.Near, config.TierFor(origin, origin));

                // Chebyshev 2 — vòng ngoài.
                Assert.AreEqual(DecorationDensityTier.Outer, config.TierFor(new ChunkCoord(12, 10), origin));
                Assert.AreEqual(DecorationDensityTier.Outer, config.TierFor(new ChunkCoord(10, 8), origin));
                Assert.AreEqual(DecorationDensityTier.Outer, config.TierFor(new ChunkCoord(12, 12), origin));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void DefaultRadius_Splits25ChunksInto9NearAnd16Outer()
        {
            WorldStreamingConfig config = WorldStreamingConfig.CreateRuntime();
            var origin = new ChunkCoord(-3, 7);

            try
            {
                Assert.AreEqual(9, config.NearChunkCount);
                Assert.AreEqual(16, config.OuterChunkCount);

                var coords = new List<ChunkCoord>();
                ChunkCoord.EnumerateRing(origin, config.RenderRadius, coords);

                int near = 0, outer = 0;
                foreach (ChunkCoord coord in coords)
                    if (config.TierFor(coord, origin) == DecorationDensityTier.Near) near++;
                    else outer++;

                Assert.AreEqual(25, coords.Count);
                Assert.AreEqual(9, near);
                Assert.AreEqual(16, outer);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void DisabledDistanceDensity_TreatsEveryChunkAsNear()
        {
            WorldStreamingConfig config = WorldStreamingConfig.CreateRuntime();
            try
            {
                config.SetDistanceDensity(false, 1, 1f, 0.4f);

                Assert.AreEqual(DecorationDensityTier.Near, config.TierFor(new ChunkCoord(99, 99), ChunkCoord.Zero));
                Assert.AreEqual(DecorationSampler.FullDensity, config.FoliageDensityFor(DecorationDensityTier.Outer));
                Assert.AreEqual(25, config.NearChunkCount);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        // --- Tất định ------------------------------------------------------------------------------

        [Test]
        public void Selection_IsDeterministicAcrossRepeatedCalls()
        {
            var coord = new ChunkCoord(74, 47);
            List<DecorationPlacement> a = Sample(coord, 0.4f);
            List<DecorationPlacement> b = Sample(coord, 0.4f);

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Selection_IsIndependentOfEvaluationOrder()
        {
            // Lấy mẫu chunk khác xen vào giữa: nếu có trạng thái mang qua giữa các lần gọi, kết quả sẽ lệch.
            var coord = new ChunkCoord(74, 47);
            List<DecorationPlacement> first = Sample(coord, 0.4f);

            Sample(new ChunkCoord(-500, 900), 1f);
            Sample(new ChunkCoord(12, -8), 0.4f);
            Sample(new ChunkCoord(74, 48), 0.4f);

            CollectionAssert.AreEqual(first, Sample(coord, 0.4f));
        }

        [Test]
        public void OuterSet_IsStrictSubsetOfNearSet()
        {
            var coord = new ChunkCoord(74, 47);
            List<DecorationPlacement> near = Sample(coord, 1f);
            List<DecorationPlacement> outer = Sample(coord, 0.4f);

            Assert.Less(outer.Count, near.Count, "Ngưỡng 0.4 phải bỏ bớt được thứ gì đó.");

            Dictionary<string, DecorationPlacement> nearIndex = Index(near);
            foreach (DecorationPlacement p in outer)
                Assert.IsTrue(nearIndex.ContainsKey(Key(p)),
                    $"{Key(p)} có ở vòng ngoài nhưng không có ở vùng gần — không phải tập con.");
        }

        [Test]
        public void SharedPlacements_KeepEverySourceAndTransformProperty()
        {
            var coord = new ChunkCoord(74, 47);
            Dictionary<string, DecorationPlacement> near = Index(Sample(coord, 1f));

            foreach (DecorationPlacement p in Sample(coord, 0.4f))
            {
                DecorationPlacement original = near[Key(p)];

                // So bằng Equals của struct: bao trùm vị trí, yaw, scale, tint, priority, budget priority.
                Assert.AreEqual(original, p, $"{Key(p)}: thuộc tính đổi giữa hai bậc.");
                Assert.AreEqual(original.EntrySalt, p.EntrySalt);
                Assert.AreEqual(original.LocalPosition, p.LocalPosition);
                Assert.AreEqual(original.Yaw, p.Yaw);
                Assert.AreEqual(original.Scale, p.Scale);
                Assert.AreEqual(original.Tint, p.Tint);
            }
        }

        [Test]
        public void NestedSubsets_HoldAcrossMultipleThresholds()
        {
            var coord = new ChunkCoord(74, 47);
            var thresholds = new[] { 1f, 0.8f, 0.6f, 0.4f, 0.2f };

            for (int i = 1; i < thresholds.Length; i++)
            {
                Dictionary<string, DecorationPlacement> wider = Index(Sample(coord, thresholds[i - 1]));
                List<DecorationPlacement> narrower = Sample(coord, thresholds[i]);

                Assert.LessOrEqual(narrower.Count, wider.Count);
                foreach (DecorationPlacement p in narrower)
                    Assert.IsTrue(wider.ContainsKey(Key(p)),
                        $"Ngưỡng {thresholds[i]} giữ {Key(p)} mà ngưỡng {thresholds[i - 1]} đã bỏ.");
            }
        }

        [Test]
        public void NegativeCoordinates_RemainDeterministicAndNested()
        {
            var coord = new ChunkCoord(-91, -57);
            CollectionAssert.AreEqual(Sample(coord, 0.4f), Sample(coord, 0.4f));

            Dictionary<string, DecorationPlacement> near = Index(Sample(coord, 1f));
            foreach (DecorationPlacement p in Sample(coord, 0.4f))
                Assert.IsTrue(near.ContainsKey(Key(p)), Key(p));
        }

        [Test]
        public void LargeCoordinates_RemainDeterministicAndNested()
        {
            var coord = new ChunkCoord(120000, -98000);
            CollectionAssert.AreEqual(Sample(coord, 0.4f), Sample(coord, 0.4f));

            Dictionary<string, DecorationPlacement> near = Index(Sample(coord, 1f));
            foreach (DecorationPlacement p in Sample(coord, 0.4f))
                Assert.IsTrue(near.ContainsKey(Key(p)), Key(p));
        }

        [Test]
        public void DifferentWorldSeeds_ProduceDifferentButValidSubsets()
        {
            var coord = new ChunkCoord(74, 47);
            DecorationPalette palette = LoadPalette();

            var a = new List<DecorationPlacement>(512);
            var b = new List<DecorationPlacement>(512);
            DecorationSampleStats sa = default, sb = default;

            DecorationSampler.Generate(Seed, coord, ChunkSize, palette, a, 0.4f, ref sa);
            DecorationSampler.Generate(Seed + 1, coord, ChunkSize, palette, b, 0.4f, ref sb);

            CollectionAssert.AreNotEqual(a, b, "Hạt giống khác phải cho thế giới khác.");

            // Nhưng mỗi bên vẫn phải là tập con hợp lệ của chính tập gần cùng hạt giống.
            var fullB = new List<DecorationPlacement>(512);
            DecorationSampleStats sf = default;
            DecorationSampler.Generate(Seed + 1, coord, ChunkSize, palette, fullB, 1f, ref sf);

            Dictionary<string, DecorationPlacement> nearB = Index(fullB);
            foreach (DecorationPlacement p in b) Assert.IsTrue(nearB.ContainsKey(Key(p)), Key(p));
        }

        // --- Chỉ đúng loại nội dung bị ảnh hưởng ----------------------------------------------------

        [Test]
        public void Palette_MarksOnlySmallRepetitiveFoliageAsEligible()
        {
            DecorationPalette palette = LoadPalette();
            var eligible = new List<string>();
            var notEligible = new List<string>();

            foreach (DecorationPaletteEntry entry in palette.Entries)
                (entry.DistanceDensityEligible ? eligible : notEligible).Add(entry.StableId);

            CollectionAssert.AreEquivalent(
                new[] { "grass_b", "grass_c", "cattail_b", "flowers_g" }, eligible);
            // M4.6CD: bụi rậm cao gần một mét và mọi cảnh vật rắn đều KHÔNG được giảm mật độ ở vòng
            // ngoài. Với bụi rậm đó là quyết định nghệ thuật — nó biến mất giữa chừng thì lộ ranh giới
            // của ring. Với cảnh vật rắn thì `DecorationPaletteEntry.Validate` chặn thẳng.
            CollectionAssert.AreEquivalent(
                new[] { "tree_a", "bush_a", "bush_b", "bush_c", "bush_d",
                        "rock_a", "rock_b", "debris_a", "debris_b", "debris_c",
                        "barrel_a", "crate_a", "pallet_a", "tire_a" },
                notEligible);
        }

        [Test]
        public void SolidDecoration_IsCompletelyUnaffected()
        {
            var coord = new ChunkCoord(74, 47);
            DecorationPalette palette = LoadPalette();

            int SolidVerts(float density)
            {
                int total = 0;
                foreach (DecorationPlacement p in Sample(coord, density))
                {
                    DecorationPaletteEntry entry = palette.Entries[p.EntryIndex];
                    foreach (DecorationMeshSource src in new[] { entry.PrimarySource, entry.SecondarySource })
                        if (src != null && src.Category == DecorationCategory.Solid) total += src.VertexCount;
                }

                return total;
            }

            Assert.AreEqual(SolidVerts(1f), SolidVerts(0.4f), "Hình học Solid không được đụng tới.");
            Assert.AreEqual(SolidVerts(1f), SolidVerts(0f), "Kể cả ở ngưỡng 0.");
        }

        [Test]
        public void NonEligibleEntries_SurviveEveryThreshold()
        {
            var coord = new ChunkCoord(74, 47);
            DecorationPalette palette = LoadPalette();

            int CountNonEligible(float density)
            {
                int n = 0;
                foreach (DecorationPlacement p in Sample(coord, density))
                    if (!palette.Entries[p.EntryIndex].DistanceDensityEligible) n++;
                return n;
            }

            int full = CountNonEligible(1f);
            Assert.Greater(full, 0, "Chunk mốc phải có ít nhất một entry không thuộc diện giảm.");
            Assert.AreEqual(full, CountNonEligible(0.4f));
            Assert.AreEqual(full, CountNonEligible(0f), "Ngưỡng 0 chỉ được xoá cây cỏ nhỏ đủ điều kiện.");
        }

        [Test]
        public void EligibleFoliage_FollowsTheConfiguredThreshold()
        {
            // Đo trên cả vòng ring để cỡ mẫu đủ lớn cho một tỉ lệ có ý nghĩa.
            DecorationPalette palette = LoadPalette();
            var centre = new ChunkCoord(74, 47);

            int CountEligible(float density)
            {
                int n = 0;
                for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                    foreach (DecorationPlacement p in Sample(new ChunkCoord(centre.X + dx, centre.Z + dz), density))
                        if (palette.Entries[p.EntryIndex].DistanceDensityEligible) n++;
                return n;
            }

            int full = CountEligible(1f);
            int reduced = CountEligible(0.4f);
            float ratio = reduced / (float)full;

            Assert.AreEqual(0.40f, ratio, 0.03f,
                $"Giữ lại {reduced}/{full} = {ratio:F3}, lệch quá xa ngưỡng 0.40 đã cấu hình.");
        }

        [Test]
        public void FullDensity_PreservesTheEntireDeterministicSet()
        {
            var coord = new ChunkCoord(74, 47);

            var withDensityArgument = new List<DecorationPlacement>(512);
            DecorationSampleStats stats = default;
            DecorationSampler.Generate(Seed, coord, ChunkSize, LoadPalette(), withDensityArgument,
                DecorationSampler.FullDensity, ref stats);

            // Đường gọi cũ (không có tham số mật độ) phải cho kết quả y hệt — M3B.1 không được đổi.
            var legacy = new List<DecorationPlacement>(512);
            DecorationSampler.Generate(Seed, coord, ChunkSize, LoadPalette(), legacy);

            CollectionAssert.AreEqual(legacy, withDensityArgument);
            Assert.AreEqual(0, stats.RejectedByDistanceDensity);
        }

        [Test]
        public void ZeroDensity_RemovesEveryEligiblePlacementAndNothingElse()
        {
            var coord = new ChunkCoord(74, 47);
            DecorationPalette palette = LoadPalette();

            foreach (DecorationPlacement p in Sample(coord, 0f))
                Assert.IsFalse(palette.Entries[p.EntryIndex].DistanceDensityEligible,
                    "Ngưỡng 0 mà vẫn còn cây cỏ thuộc diện giảm.");
        }

        [Test]
        public void Stats_ReportEligibleAndRejectedCountsConsistently()
        {
            var coord = new ChunkCoord(74, 47);
            var results = new List<DecorationPlacement>(512);
            DecorationSampleStats stats = default;

            DecorationSampler.Generate(Seed, coord, ChunkSize, LoadPalette(), results, 0.4f, ref stats);

            Assert.Greater(stats.EligibleForDistanceDensity, 0);
            Assert.Greater(stats.RejectedByDistanceDensity, 0);
            Assert.AreEqual(results.Count, stats.Accepted);
            Assert.Less(stats.RejectedByDistanceDensity, stats.EligibleForDistanceDensity);
        }

        [Test]
        public void EligibleEntryWithSolidGeometry_IsRejectedByValidation()
        {
            // Chốt chặn chống đánh dấu nhầm: một entry có hình học Solid không bao giờ được giảm mật độ.
            DecorationPalette palette = LoadPalette();
            DecorationMeshSource trunk = null;
            DecorationMeshSource leaf = null;

            foreach (DecorationPaletteEntry entry in palette.Entries)
            {
                if (entry.SecondarySource == null) continue;
                trunk = entry.SecondarySource.Category == DecorationCategory.Solid
                    ? entry.SecondarySource : entry.PrimarySource;
                leaf = entry.SecondarySource.Category == DecorationCategory.Solid
                    ? entry.PrimarySource : entry.SecondarySource;
                break;
            }

            Assert.IsNotNull(trunk, "Palette phải có một entry mang hình học Solid để thử.");

            var offender = new DecorationPaletteEntry();
            offender.Configure("offender", leaf, trunk, 8f, 1, 0.5f, 0f, Vector4.one,
                new Vector2(1f, 1f), new Vector2(1f, 1f), 10, yaw: true, densityEligible: true);

            Assert.IsFalse(offender.Validate(out string error));
            Assert.IsTrue(error.Contains("hình học Solid"), error);
        }
    }
}
