using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode M2B: tính tất định của việc đặt trang trí, quyền sở hữu ở biên, và mesh gộp.
    ///
    /// Đây là phần dễ hỏng âm thầm nhất của milestone: một placement lệch một hạt cũng đủ làm cây cỏ
    /// nhảy chỗ mỗi lần chunk được tái sử dụng, mà mắt thường rất khó bắt.
    public class WorldStreamingDecorationTests
    {
        private const int Seed = 20260809;
        private const float ChunkSize = 32f;
        private const string PalettePath = "Assets/_Project/Data/World/Decoration/DecorationPalette.asset";

        private static DecorationPalette LoadPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<DecorationPalette>(PalettePath);
            Assert.IsNotNull(palette, $"Không tìm thấy palette tại {PalettePath}. Chạy 'Rebuild Decoration Assets'.");
            return palette;
        }

        private static string Checksum(IReadOnlyList<DecorationPlacement> placements)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < placements.Count; i++) sb.Append(placements[i]).Append('|');
            return sb.ToString();
        }

        private static List<DecorationPlacement> Generate(ChunkCoord coord, DecorationPalette palette, int seed = Seed)
        {
            var results = new List<DecorationPlacement>();
            DecorationSampler.Generate(seed, coord, ChunkSize, palette, results);
            return results;
        }

        // --- Palette và nguồn ------------------------------------------------------------------

        [Test]
        public void Palette_IsValidAndCurated()
        {
            DecorationPalette palette = LoadPalette();
            Assert.IsTrue(palette.Validate(out string error), error);

            Assert.GreaterOrEqual(palette.Entries.Count, 3, "Palette MVP phải có ít nhất vài loại.");
            // Trần nâng 10 → 16 ở M4.6CD: bảng màu nay phải mang CẢ cảnh vật rắn (đá, rác, thùng phuy,
            // lốp xe) chứ không chỉ cây cỏ. Trần vẫn còn để giữ nguyên ý ban đầu — đây là bảng màu
            // được chọn lọc, không phải chỗ đổ cả pack vào.
            Assert.LessOrEqual(palette.Entries.Count, 22, "Palette phải được chọn lọc — đây không phải chỗ nhét cả pack.");
        }

        [Test]
        public void Palette_StableIdsAndSaltsAreUnique()
        {
            DecorationPalette palette = LoadPalette();
            var ids = new HashSet<string>();
            var salts = new HashSet<int>();

            foreach (DecorationPaletteEntry entry in palette.Entries)
            {
                Assert.IsTrue(ids.Add(entry.StableId), $"Trùng stableId '{entry.StableId}'.");
                Assert.IsTrue(salts.Add(entry.Salt), $"Trùng muối ở '{entry.StableId}'.");
            }
        }

        [Test]
        public void Salt_DependsOnIdNotListOrder()
        {
            // Muối phải suy từ chuỗi ID. Nếu nó suy từ chỉ số, sắp xếp lại palette sẽ xáo trộn
            // toàn bộ thế giới đã sinh ra.
            Assert.AreEqual(DecorationHash.SaltFromId("grass_b"), DecorationHash.SaltFromId("grass_b"));
            Assert.AreNotEqual(DecorationHash.SaltFromId("grass_b"), DecorationHash.SaltFromId("grass_c"));
            Assert.AreNotEqual(0, DecorationHash.SaltFromId("tree_a"));
        }

        [Test]
        public void Sources_AreValidAndBaked()
        {
            DecorationPalette palette = LoadPalette();

            foreach (DecorationPaletteEntry entry in palette.Entries)
            {
                Assert.IsNotNull(entry.PrimarySource, $"{entry.StableId}: thiếu nguồn chính.");
                Assert.IsTrue(entry.PrimarySource.Validate(out string error), error);
                Assert.Greater(entry.PrimarySource.VertexCount, 0);

                if (entry.SecondarySource == null) continue;
                Assert.IsTrue(entry.SecondarySource.Validate(out string secondaryError), secondaryError);
                Assert.AreNotEqual(entry.PrimarySource.Category, entry.SecondarySource.Category,
                    $"{entry.StableId}: hai nguồn phải chảy vào hai output khác nhau.");
            }
        }

        [Test]
        public void TilingSource_MustOwnItsWholeAtlas()
        {
            // Nguồn có UV lặp không thể chia ô atlas với ai — kẹp về [0,1] sẽ bôi bẹt hoa văn.
            //
            // Đến M4.6CD thì bảng màu KHÔNG còn nguồn nào như vậy: vỏ thân cây (UV chạy 5,92 × 4,66
            // vòng) nay được in sẵn 6×5 vòng vào ô atlas rồi chuẩn hoá UV về [0,1], nhờ đó atlas solid
            // mới chia được cho đá/rác/thùng phuy. Nên bài test đổi từ "phải có ít nhất một nguồn lặp"
            // sang canh đúng BẤT BIẾN: nếu nguồn lặp quay lại thì nó buộc phải chiếm trọn atlas.
            DecorationPalette palette = LoadPalette();

            foreach (DecorationPaletteEntry entry in palette.Entries)
            {
                foreach (DecorationMeshSource source in new[] { entry.PrimarySource, entry.SecondarySource })
                {
                    if (source == null || !source.TilesUv) continue;

                    Assert.IsTrue(source.OccupiesWholeAtlas,
                        $"{source.StableId}: UV lặp nhưng lại nằm trong ô con {source.AtlasRect}.");
                }
            }

            // Thân cây phải thật sự đã được chuẩn hoá, không phải "vô tình không có nguồn lặp nào".
            DecorationMeshSource trunk = LoadPalette().Entries
                .SelectMany(e => new[] { e.PrimarySource, e.SecondarySource })
                .FirstOrDefault(s => s != null && s.StableId == "tree_a_trunk");
            Assert.IsNotNull(trunk, "Không tìm thấy nguồn thân cây.");
            Assert.IsFalse(trunk.TilesUv,
                "Thân cây lại mang UV lặp — bước chuẩn hoá vào ô atlas đã bị bỏ, atlas solid sẽ hỏng.");
            Assert.IsFalse(trunk.OccupiesWholeAtlas,
                "Thân cây lại chiếm trọn atlas solid — cảnh vật rắn sẽ không còn ô nào.");
        }

        // --- Tất định ---------------------------------------------------------------------------

        [Test]
        public void SameSeedAndCoord_ProduceIdenticalPlacements()
        {
            DecorationPalette palette = LoadPalette();

            foreach (var coord in new[]
                     {
                         new ChunkCoord(0, 0), new ChunkCoord(74, 47), new ChunkCoord(-54, 48),
                         new ChunkCoord(-1, -1), new ChunkCoord(-620, 391),
                     })
            {
                string first = Checksum(Generate(coord, palette));

                // Sinh ở chỗ khác xen vào giữa: nếu sampler có trạng thái ẩn, đây là chỗ nó lộ ra.
                Generate(new ChunkCoord(coord.X + 37, coord.Z - 91), palette);
                Generate(coord, palette, Seed + 5);

                Assert.AreEqual(first, Checksum(Generate(coord, palette)), $"{coord}: kết quả không tái lập.");
            }
        }

        [Test]
        public void GenerationOrder_DoesNotChangeResults()
        {
            DecorationPalette palette = LoadPalette();
            var coords = new[]
            {
                new ChunkCoord(3, 4), new ChunkCoord(-8, 2), new ChunkCoord(0, -5), new ChunkCoord(74, 47),
            };

            var forward = new Dictionary<ChunkCoord, string>();
            foreach (ChunkCoord c in coords) forward[c] = Checksum(Generate(c, palette));

            for (int i = coords.Length - 1; i >= 0; i--)
                Assert.AreEqual(forward[coords[i]], Checksum(Generate(coords[i], palette)),
                    $"{coords[i]}: đảo thứ tự sinh làm đổi kết quả.");
        }

        [Test]
        public void DifferentSeed_ChangesPlacements_AndRestoringSeedRestoresThem()
        {
            DecorationPalette palette = LoadPalette();
            var coord = new ChunkCoord(74, 47);

            string original = Checksum(Generate(coord, palette, Seed));
            string other = Checksum(Generate(coord, palette, Seed + 4242));

            Assert.AreNotEqual(original, other, "Đổi hạt giống phải đổi thế giới.");
            Assert.AreEqual(original, Checksum(Generate(coord, palette, Seed)), "Trả lại hạt giống phải trả lại thế giới.");
        }

        [Test]
        public void NegativeAndZeroCrossingCoords_Work()
        {
            DecorationPalette palette = LoadPalette();

            foreach (var coord in new[]
                     {
                         new ChunkCoord(-1, 0), new ChunkCoord(0, -1), new ChunkCoord(-1, -1),
                         new ChunkCoord(-73, -44), new ChunkCoord(-620, 391),
                     })
            {
                List<DecorationPlacement> placements = Generate(coord, palette);

                foreach (DecorationPlacement placement in placements)
                {
                    Assert.GreaterOrEqual(placement.LocalPosition.x, 0f, $"{coord}: X cục bộ âm.");
                    Assert.Less(placement.LocalPosition.x, ChunkSize, $"{coord}: X cục bộ vượt chunk.");
                    Assert.GreaterOrEqual(placement.LocalPosition.z, 0f, $"{coord}: Z cục bộ âm.");
                    Assert.Less(placement.LocalPosition.z, ChunkSize, $"{coord}: Z cục bộ vượt chunk.");
                    Assert.AreEqual(0f, placement.LocalPosition.y, 0f, "Trang trí M2B luôn nằm trên Y = 0.");
                }
            }
        }

        // --- Quyền sở hữu ở biên ------------------------------------------------------------------

        [Test]
        public void EveryPlacementIsOwnedByExactlyOneChunk()
        {
            DecorationPalette palette = LoadPalette();

            // Quét một vùng 4×4 chunk quanh gốc, gồm cả biên âm và mốc 0. Mỗi placement được nhận
            // dạng bằng (muối, ô, chỉ số ứng viên) — nếu hai chunk cùng phát một cái là trùng chủ.
            var seen = new Dictionary<(int, int, int, int), ChunkCoord>();
            int total = 0;

            for (int z = -2; z < 2; z++)
            {
                for (int x = -2; x < 2; x++)
                {
                    var coord = new ChunkCoord(x, z);
                    foreach (DecorationPlacement placement in Generate(coord, palette))
                    {
                        var key = (placement.EntrySalt, placement.CellX, placement.CellZ, placement.CandidateIndex);
                        Assert.IsFalse(seen.ContainsKey(key),
                            $"Placement {key} bị cả {seen.GetValueOrDefault(key)} lẫn {coord} nhận là của mình.");

                        seen[key] = coord;
                        total++;
                    }
                }
            }

            Assert.Greater(total, 50, "Vùng thử phải có đủ placement để kết luận có nghĩa.");
        }

        [Test]
        public void PlacementAnchor_LandsInsideItsOwnerChunk()
        {
            DecorationPalette palette = LoadPalette();

            foreach (var coord in new[] { new ChunkCoord(0, 0), new ChunkCoord(-1, -1), new ChunkCoord(74, 47) })
            {
                foreach (DecorationPlacement placement in Generate(coord, palette))
                {
                    float worldX = coord.X * ChunkSize + placement.LocalPosition.x;
                    float worldZ = coord.Z * ChunkSize + placement.LocalPosition.z;

                    Assert.AreEqual(coord.X, ChunkCoord.AxisToChunk(worldX, ChunkSize), $"{coord}: điểm neo lệch chunk theo X.");
                    Assert.AreEqual(coord.Z, ChunkCoord.AxisToChunk(worldZ, ChunkSize), $"{coord}: điểm neo lệch chunk theo Z.");
                }
            }
        }

        [Test]
        public void SpacingRejection_IsIdenticalSeenFromEitherSideOfABorder()
        {
            // Luật giãn cách chạy trên lưới toàn cục, nên chunk nào hỏi cũng phải nhận cùng câu trả lời.
            // Nếu không, cây sát biên sẽ nhấp nháy mỗi lần chunk đổi chủ.
            DecorationPalette palette = LoadPalette();

            foreach (var coord in new[] { new ChunkCoord(0, 0), new ChunkCoord(-1, 0), new ChunkCoord(0, -1) })
            {
                string first = Checksum(Generate(coord, palette));
                Generate(new ChunkCoord(coord.X + 1, coord.Z), palette);
                Generate(new ChunkCoord(coord.X - 1, coord.Z), palette);
                Generate(new ChunkCoord(coord.X, coord.Z + 1), palette);

                Assert.AreEqual(first, Checksum(Generate(coord, palette)),
                    $"{coord}: sinh chunk hàng xóm làm đổi kết quả của chính nó.");
            }
        }

        [Test]
        public void SpacingRejectsOtherCandidatesInsideTheSameCell()
        {
            DecorationMeshSource source = LoadPalette().Entries[0].PrimarySource;
            const float cellSize = ChunkSize;
            const float spacing = 5f;

            var entry = new DecorationPaletteEntry();
            entry.Configure("same_cell_spacing", source, null,
                cellSize, 2, 1f, spacing, Vector4.one,
                Vector2.one, Vector2.one, 1);

            // Find a deterministic cell whose two candidates are closer than the spacing radius and
            // both sit far enough from the cell edge that no neighbouring cell can affect the result.
            // The only valid rejection is therefore candidate-vs-candidate inside this same cell.
            bool found = false;
            int foundX = 0;
            int foundZ = 0;
            int salt = entry.Salt;

            for (int z = -128; z <= 128 && !found; z++)
            {
                for (int x = -128; x <= 128; x++)
                {
                    var a = new Vector2(
                        DecorationHash.Unit(Seed, x, z, salt, 0, HashLane.JitterX) * cellSize,
                        DecorationHash.Unit(Seed, x, z, salt, 0, HashLane.JitterZ) * cellSize);
                    var b = new Vector2(
                        DecorationHash.Unit(Seed, x, z, salt, 1, HashLane.JitterX) * cellSize,
                        DecorationHash.Unit(Seed, x, z, salt, 1, HashLane.JitterZ) * cellSize);

                    bool interior = a.x > spacing && a.x < cellSize - spacing &&
                                    a.y > spacing && a.y < cellSize - spacing &&
                                    b.x > spacing && b.x < cellSize - spacing &&
                                    b.y > spacing && b.y < cellSize - spacing;
                    if (!interior || Vector2.Distance(a, b) >= spacing) continue;

                    found = true;
                    foundX = x;
                    foundZ = z;
                    break;
                }
            }

            Assert.IsTrue(found, "Không tìm được deterministic fixture cho spacing cùng cell.");

            DecorationPalette palette = DecorationPalette.CreateRuntime(
                new List<DecorationPaletteEntry> { entry });
            try
            {
                List<DecorationPlacement> placements = Generate(new ChunkCoord(foundX, foundZ), palette);
                int fromTargetCell = 0;
                foreach (DecorationPlacement placement in placements)
                {
                    if (placement.EntrySalt == salt && placement.CellX == foundX && placement.CellZ == foundZ)
                        fromTargetCell++;
                }

                Assert.AreEqual(1, fromTargetCell,
                    "Hai candidate cùng cell nằm trong spacing radius phải loại nhau theo priority.");
            }
            finally
            {
                Object.DestroyImmediate(palette);
            }
        }

        // --- Biome ------------------------------------------------------------------------------

        [Test]
        public void GrassRegions_CarryMoreDecorationThanSandRegions()
        {
            DecorationPalette palette = LoadPalette();

            var grass = new ChunkCoord(74, 47);
            var sand = new ChunkCoord(-73, 0);

            BiomeSample grassSample = BiomeSampler.Sample(Seed, grass.X * ChunkSize + 16f, grass.Z * ChunkSize + 16f);
            BiomeSample sandSample = BiomeSampler.Sample(Seed, sand.X * ChunkSize + 16f, sand.Z * ChunkSize + 16f);
            Assert.Greater(grassSample.Grass, sandSample.Grass, "Tiền đề: chunk thử phải thật sự khác biome.");

            int grassCount = Generate(grass, palette).Count;
            int sandCount = Generate(sand, palette).Count;

            Assert.Greater(grassCount, sandCount,
                $"Vùng cỏ ({grassCount}) phải nhiều trang trí hơn vùng cát ({sandCount}) — palette phải nghe theo biome.");
        }

        [Test]
        public void RepresentativeGrassChunk_HitsThePlacementTarget()
        {
            DecorationPalette palette = LoadPalette();
            int count = Generate(new ChunkCoord(74, 47), palette).Count;

            // Mục tiêu 100-200 đặt ở M2B, khi mỗi placement là một mesh vài chục đến vài trăm đỉnh.
            // M4.6CD.1 đổi sang hình học vendor gốc: cỏ 230 đỉnh, bụi 1.863-3.913, cụm đá 1.194-3.372.
            // Cùng một ngân sách đỉnh nay mua được ÍT placement hơn nhưng mỗi cái mang nhiều chi tiết
            // hơn hẳn — đó là chính điều đổi ở milestone này, nên dải mục tiêu hạ xuống theo.
            // Đo được 82 ở chunk mốc. Dải vẫn còn để bắt việc thế giới rỗng đi hoặc dày lên mất kiểm soát.
            Assert.GreaterOrEqual(count, 55, $"Chunk đại diện chỉ có {count} placement, dưới mục tiêu 55.");
            Assert.LessOrEqual(count, 200, $"Chunk đại diện có {count} placement, vượt mục tiêu 200.");
        }

        // --- Ngân sách --------------------------------------------------------------------------

        [Test]
        public void VertexBudget_IsRespectedAndRejectionIsDeterministic()
        {
            DecorationPalette palette = LoadPalette();

            for (int z = -3; z <= 3; z++)
            {
                for (int x = -3; x <= 3; x++)
                {
                    var coord = new ChunkCoord(74 + x, 47 + z);

                    DecorationSampleStats stats = default;
                    var placements = new List<DecorationPlacement>();
                    DecorationSampler.Generate(Seed, coord, ChunkSize, palette, placements, ref stats);

                    Assert.LessOrEqual(stats.SolidVertices, palette.SolidVertexBudget, $"{coord}: vượt ngân sách solid.");
                    Assert.LessOrEqual(stats.FoliageVertices, palette.FoliageVertexBudget, $"{coord}: vượt ngân sách foliage.");
                    Assert.Less(stats.SolidVertices, palette.HardVertexCeiling, $"{coord}: vượt trần UInt16.");
                    Assert.Less(stats.FoliageVertices, palette.HardVertexCeiling, $"{coord}: vượt trần UInt16.");
                    Assert.AreEqual(placements.Count, stats.Accepted);
                }
            }
        }

        [Test]
        public void TightBudget_RejectsDeterministicallyRatherThanOverflowing()
        {
            DecorationPalette source = LoadPalette();
            var entries = new List<DecorationPaletteEntry>(source.Entries);

            // Ngân sách siết chặt: hệ thống phải cắt bớt theo ưu tiên, và cắt y hệt nhau mỗi lần.
            DecorationPalette tight = DecorationPalette.CreateRuntime(entries, solidBudget: 3000, foliageBudget: 4000);
            try
            {
                var coord = new ChunkCoord(74, 47);

                DecorationSampleStats stats = default;
                var first = new List<DecorationPlacement>();
                DecorationSampler.Generate(Seed, coord, ChunkSize, tight, first, ref stats);

                Assert.Greater(stats.RejectedByBudget, 0, "Ngân sách chật phải thật sự loại bớt.");
                Assert.LessOrEqual(stats.FoliageVertices, 4000);
                Assert.LessOrEqual(stats.SolidVertices, 3000);

                var second = new List<DecorationPlacement>();
                DecorationSampler.Generate(Seed, coord, ChunkSize, tight, second);
                Assert.AreEqual(Checksum(first), Checksum(second), "Cắt ngân sách phải tất định.");
            }
            finally
            {
                Object.DestroyImmediate(tight);
            }
        }

        [Test]
        public void DesignerBudgetPriorityOutranksRandomPlacementPriority()
        {
            DecorationMeshSource source = LoadPalette().Entries[0].PrimarySource;

            var high = new DecorationPaletteEntry();
            high.Configure("budget_priority_high", source, null,
                ChunkSize, 1, 1f, 0f, Vector4.one,
                Vector2.one, Vector2.one, 1000);

            var low = new DecorationPaletteEntry();
            low.Configure("budget_priority_low", source, null,
                ChunkSize, 1, 1f, 0f, Vector4.one,
                Vector2.one, Vector2.one, 1);

            // Choose a coordinate where the low-priority entry wins the random hash lane. This proves
            // the test would fail if trimming ignored the designer-authored BudgetPriority.
            ChunkCoord fixture = default;
            bool found = false;
            for (int z = -32; z <= 32 && !found; z++)
            {
                for (int x = -32; x <= 32; x++)
                {
                    uint highRandom = DecorationHash.Hash(
                        Seed, x, z, high.Salt, 0, HashLane.Priority);
                    uint lowRandom = DecorationHash.Hash(
                        Seed, x, z, low.Salt, 0, HashLane.Priority);
                    if (lowRandom <= highRandom) continue;

                    fixture = new ChunkCoord(x, z);
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "Không tìm được deterministic fixture cho budget priority.");

            DecorationPalette palette = DecorationPalette.CreateRuntime(
                new List<DecorationPaletteEntry> { low, high },
                foliageBudget: source.VertexCount);
            try
            {
                List<DecorationPlacement> placements = Generate(fixture, palette);

                Assert.AreEqual(1, placements.Count, "Ngân sách chỉ đủ giữ đúng một entry.");
                Assert.AreEqual(high.Salt, placements[0].EntrySalt,
                    "BudgetPriority của designer phải thắng random placement priority.");
                Assert.AreEqual(high.BudgetPriority, placements[0].BudgetPriority);
            }
            finally
            {
                Object.DestroyImmediate(palette);
            }
        }

        // --- Mesh gộp ---------------------------------------------------------------------------

        [Test]
        public void CombinedMeshes_HaveOneSubmeshValidIndicesAndCorrectBounds()
        {
            DecorationPalette palette = LoadPalette();
            var coord = new ChunkCoord(74, 47);

            var placements = new List<DecorationPlacement>();
            DecorationSampler.Generate(Seed, coord, ChunkSize, palette, placements);
            DecorationMeshBuilder.Build(palette, placements);

            Mesh solid = DecorationMeshBuilder.CreateDecorationMesh("TestSolid");
            Mesh foliage = DecorationMeshBuilder.CreateDecorationMesh("TestFoliage");
            try
            {
                bool hasSolid = DecorationMeshBuilder.Solid.Apply(solid, 0f);
                bool hasFoliage = DecorationMeshBuilder.Foliage.Apply(foliage, palette.FoliageBoundsPadding);

                Assert.IsTrue(hasFoliage, "Chunk đại diện phải có foliage.");
                Assert.IsTrue(hasSolid, "Chunk đại diện phải có ít nhất một thân cây.");

                foreach ((Mesh mesh, string label) in new[] { (solid, "solid"), (foliage, "foliage") })
                {
                    Assert.AreEqual(1, mesh.subMeshCount, $"{label}: phải đúng một submesh.");
                    Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, mesh.indexFormat,
                        $"{label}: phải giữ UInt16, không được âm thầm nhảy lên UInt32.");
                    Assert.Less(mesh.vertexCount, 65535, $"{label}: vượt giới hạn UInt16.");
                    Assert.AreEqual(mesh.vertexCount, mesh.colors32.Length, $"{label}: thiếu vertex color.");
                    Assert.AreEqual(mesh.vertexCount, mesh.normals.Length, $"{label}: thiếu normal.");
                    Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, $"{label}: thiếu UV.");

                    int[] indices = mesh.GetTriangles(0);
                    Assert.AreEqual(0, indices.Length % 3, $"{label}: số chỉ số không chia hết cho 3.");
                    foreach (int index in indices)
                    {
                        Assert.GreaterOrEqual(index, 0, $"{label}: chỉ số âm.");
                        Assert.Less(index, mesh.vertexCount, $"{label}: chỉ số vượt số đỉnh.");
                    }

                    // Hình học nằm trong hệ toạ độ cục bộ của chunk, có thể tràn ra ngoài viền chunk
                    // vì tán cây chờm sang hàng xóm — nhưng không được tràn vô lý.
                    Assert.Greater(mesh.bounds.size.x, 0f, $"{label}: bounds rỗng.");
                    Assert.Less(mesh.bounds.size.x, ChunkSize * 3f, $"{label}: bounds phình bất thường.");
                }
            }
            finally
            {
                Object.DestroyImmediate(solid);
                Object.DestroyImmediate(foliage);
            }
        }

        [Test]
        public void CombinedFoliageUvs_StayInsideTheirAtlasRect()
        {
            DecorationPalette palette = LoadPalette();

            // Chỉ dựng từ những nguồn chia chung atlas; nguồn UV lặp cố ý đi ra ngoài [0,1].
            var placements = new List<DecorationPlacement>();
            DecorationSampler.Generate(Seed, new ChunkCoord(74, 47), ChunkSize, palette, placements);
            DecorationMeshBuilder.Build(palette, placements);

            Mesh foliage = DecorationMeshBuilder.CreateDecorationMesh("TestUvFoliage");
            try
            {
                DecorationMeshBuilder.Foliage.Apply(foliage, 0f);

                foreach (Vector2 uv in foliage.uv)
                {
                    Assert.GreaterOrEqual(uv.x, -0.001f, "UV foliage ra ngoài atlas theo X.");
                    Assert.LessOrEqual(uv.x, 1.001f, "UV foliage ra ngoài atlas theo X.");
                    Assert.GreaterOrEqual(uv.y, -0.001f, "UV foliage ra ngoài atlas theo Y.");
                    Assert.LessOrEqual(uv.y, 1.001f, "UV foliage ra ngoài atlas theo Y.");
                }
            }
            finally
            {
                Object.DestroyImmediate(foliage);
            }
        }

        [Test]
        public void TreePlacement_RoutesTrunkAndLeavesToDifferentOutputs()
        {
            DecorationPalette palette = LoadPalette();

            DecorationPaletteEntry tree = null;
            int treeIndex = -1;
            for (int i = 0; i < palette.Entries.Count; i++)
            {
                if (palette.Entries[i].SecondarySource == null) continue;
                tree = palette.Entries[i];
                treeIndex = i;
            }

            Assert.IsNotNull(tree, "Palette phải có một entry hai nguồn để chứng minh việc tách output.");

            var placements = new List<DecorationPlacement>
            {
                new DecorationPlacement(tree.Salt, treeIndex, 0, 0, 0,
                    new Vector3(16f, 0f, 16f), 0f, 1f, 1f, 1u),
            };

            DecorationMeshBuilder.Build(palette, placements);

            Assert.Greater(DecorationMeshBuilder.Solid.VertexCount, 0, "Thân cây phải chảy vào output Solid.");
            Assert.Greater(DecorationMeshBuilder.Foliage.VertexCount, 0, "Tán lá phải chảy vào output Foliage.");
            Assert.AreEqual(tree.SecondarySource.VertexCount, DecorationMeshBuilder.Solid.VertexCount);
            Assert.AreEqual(tree.PrimarySource.VertexCount, DecorationMeshBuilder.Foliage.VertexCount);
        }

        [Test]
        public void Accumulator_TransformsPositionsAndNormalsWithUniformScale()
        {
            DecorationPalette palette = LoadPalette();
            DecorationPaletteEntry entry = palette.Entries[0];
            DecorationMeshSource source = entry.PrimarySource;

            var accumulator = new DecorationAccumulator();
            accumulator.Append(source, new Vector3(5f, 0f, 7f), 90f, 2f, 1f);

            Mesh mesh = DecorationMeshBuilder.CreateDecorationMesh("TestTransform");
            try
            {
                accumulator.Apply(mesh, 0f);
                Assert.AreEqual(source.VertexCount, mesh.vertexCount);

                Vector3[] positions = mesh.vertices;
                Vector3[] normals = mesh.normals;

                for (int i = 0; i < source.VertexCount; i++)
                {
                    Vector3 p = source.Positions[i] * 2f;
                    // Yaw 90°: (x,z) -> (z, -x), khớp với ma trận xoay trong accumulator.
                    var expected = new Vector3(5f + p.z, p.y, 7f - p.x);

                    Assert.AreEqual(expected.x, positions[i].x, 0.001f, $"đỉnh {i}: X sai.");
                    Assert.AreEqual(expected.y, positions[i].y, 0.001f, $"đỉnh {i}: Y sai.");
                    Assert.AreEqual(expected.z, positions[i].z, 0.001f, $"đỉnh {i}: Z sai.");

                    // Scale đều nên normal chỉ xoay, độ dài giữ nguyên.
                    Assert.AreEqual(source.Normals[i].magnitude, normals[i].magnitude, 0.01f, $"đỉnh {i}: normal bị méo.");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void EmptyAccumulator_ProducesEmptyMeshAndReportsIt()
        {
            var accumulator = new DecorationAccumulator();
            Mesh mesh = DecorationMeshBuilder.CreateDecorationMesh("TestEmpty");
            try
            {
                Assert.IsFalse(accumulator.Apply(mesh, 0f), "Output rỗng phải báo false để phía gọi tắt renderer.");
                Assert.AreEqual(0, mesh.vertexCount);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        // --- Băm --------------------------------------------------------------------------------

        [Test]
        public void Hash_IsStableAcrossRuns_GoldenValues()
        {
            // Giá trị vàng: nếu hàm băm đổi, thế giới của mọi save cũ cũng đổi theo. Sửa các số này
            // là một quyết định có ý thức, không phải chuyện sửa cho test xanh.
            Assert.AreEqual(3830172862u, DecorationHash.Hash(20260809, 0, 0, 1234, 0, HashLane.Acceptance));
            Assert.AreEqual(3614397761u, DecorationHash.Hash(20260809, -1, -1, 1234, 0, HashLane.JitterX));
            Assert.AreEqual(798652705u, DecorationHash.Hash(20260809, 74, 47, 1234, 0, HashLane.Rotation));
        }

        [Test]
        public void HashLanes_AreIndependent()
        {
            var lanes = new HashSet<uint>();
            foreach (HashLane lane in System.Enum.GetValues(typeof(HashLane)))
                Assert.IsTrue(lanes.Add(DecorationHash.Hash(Seed, 3, 4, 99, 0, lane)),
                    $"Làn {lane} cho ra cùng giá trị với một làn khác — chúng phải độc lập.");
        }
    }
}
