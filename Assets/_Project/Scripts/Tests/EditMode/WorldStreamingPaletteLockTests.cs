using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode M4.6CD: chốt bảng màu production sau khi khoá cây cỏ và cảnh vật rắn.
    ///
    /// Bộ test này canh những QUYẾT ĐỊNH, không canh con số. Số lượng placement đã có
    /// <see cref="WorldStreamingFoliageCostTests"/> canh từng entry một. Ở đây canh những thứ mà
    /// một lần chỉnh bảng màu vô ý rất dễ phá mà không ai nhận ra cho tới lúc nhìn ảnh chụp:
    ///
    /// - loài đã bị từ chối không được lẻn về;
    /// - vẫn còn đủ ba tầng thực vật và ba họ cảnh vật rắn;
    /// - nguồn lặp UV không bao giờ bị nhét vào một ô con của atlas;
    /// - ID ổn định của entry cũ không đổi — đổi là đổi cả thế giới đã sinh ra.
    public class WorldStreamingPaletteLockTests
    {
        private const int Seed = 20260809;
        private const float ChunkSize = 32f;
        private const string PalettePath = "Assets/_Project/Data/World/Decoration/DecorationPalette.asset";

        private static readonly ChunkCoord Checkpoint = new ChunkCoord(74, 47);

        /// <summary>Cây cỏ mọc thấp, lặp nhiều, dùng chung shader foliage và gió.</summary>
        private static readonly string[] FoliageIds =
            { "grass_b", "grass_c", "bush_a", "bush_b", "cattail_b", "flowers_g", "tree_a" };

        /// <summary>Ba họ cảnh vật rắn được duyệt cho bảng màu hậu tận thế đầu tiên.</summary>
        private static readonly string[] RockIds = { "rock_a", "rock_b" };
        private static readonly string[] DebrisIds = { "debris_a", "debris_b", "debris_c" };
        private static readonly string[] ManMadeIds = { "barrel_a", "crate_a", "pallet_a", "tire_a" };

        private static DecorationPalette Palette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<DecorationPalette>(PalettePath);
            Assert.IsNotNull(palette, $"Thiếu palette ở {PalettePath} — chạy 'Rebuild Decoration Assets'.");
            return palette;
        }

        private static DecorationPaletteEntry Entry(string id)
        {
            DecorationPaletteEntry entry = Palette().Entries.FirstOrDefault(e => e.StableId == id);
            Assert.IsNotNull(entry, $"Bảng màu production thiếu entry '{id}'.");
            return entry;
        }

        // --- Loài đã bị từ chối --------------------------------------------------------------------

        [Test]
        public void RejectedRosetteFern_StaysOutOfProduction()
        {
            // `fern_d` bị loại ở M4.6C.4: hoa thị dẹt đọc thành vết bẩn dưới camera từ trên xuống.
            // Nó vẫn còn asset trên đĩa (để đảo ngược được), nên chỉ kiểm BẢNG MÀU.
            CollectionAssert.DoesNotContain(Palette().Entries.Select(e => e.StableId).ToArray(), "fern_d");

            foreach (DecorationPaletteEntry entry in Palette().Entries)
            foreach (DecorationMeshSource source in new[] { entry.PrimarySource, entry.SecondarySource })
                if (source != null)
                    Assert.IsFalse(source.SourceAssetPath.Contains("S_Fern_"),
                        $"'{entry.StableId}' lấy hình học từ dương xỉ vendor ({source.SourceAssetPath}) — " +
                        "hoa thị dẹt đã bị từ chối cho production.");
        }

        // --- Ba tầng thực vật ----------------------------------------------------------------------

        [Test]
        public void Vegetation_HasTwoUprightGrassVariants_ThatAreProjectOwnedAndTall()
        {
            foreach (string id in new[] { "grass_b", "grass_c" })
            {
                DecorationMeshSource source = Entry(id).PrimarySource;
                Assert.AreEqual(DecorationCategory.Foliage, source.Category, id);

                Bounds b = source.LocalBounds;
                float footprint = Mathf.Max(b.size.x, b.size.z);
                // Đứng THẲNG nghĩa là cao ít nhất bằng bề ngang. Hoa thị dẹt trượt bài này ngay.
                Assert.GreaterOrEqual(b.size.y, footprint * 0.95f,
                    $"{id}: cao {b.size.y:F2} so với ngang {footprint:F2} — khóm này nằm bẹt chứ không đứng.");
                // Mesh vendor cắm gốc HƠI CHÌM xuống dưới mặt đất — `S_Grass_C` xuống -0,043 m — để
                // chân khóm không hở ra khi mặt đất gợn. Đó là chủ ý của tác giả asset, không phải lỗi.
                // Ngưỡng -0,06 chặn đúng thứ cần chặn: một nguồn cắm sâu tới mức nửa khóm biến mất.
                Assert.GreaterOrEqual(b.min.y, -0.06f, $"{id}: gốc chìm quá sâu dưới mặt đất.");
            }
        }

        [Test]
        public void Vegetation_HasAMediumHeightBush_ThatSitsBetweenGrassAndTree()
        {
            foreach (string id in new[] { "bush_a", "bush_b" })
            {
                DecorationPaletteEntry bush = Entry(id);
                Assert.AreEqual(DecorationCategory.Foliage, bush.PrimarySource.Category);

                float bushHeight = bush.PrimarySource.LocalBounds.size.y;
                float grassHeight = Entry("grass_b").PrimarySource.LocalBounds.size.y;
                float canopyHeight = Entry("tree_a").PrimarySource.LocalBounds.size.y;

                Assert.Greater(bushHeight, grassHeight * 1.4f,
                    $"{id} khong cao hon co du de tao ra mot tang rieng.");
                Assert.Less(bushHeight, canopyHeight,
                    $"{id} cao ngang tan cay - no khong con la tang giua nua.");
            }

            // Bui thuong phai xuat hien du day de doc ra mot TANG. Muc tieu da chot cua dao dien:
            // 1-3 bui S_Bush_A/B moi chunk o vung hop. Vong ring co 25 chunk.
            int bushes = CountAtCheckpoint("bush_a") + CountAtCheckpoint("bush_b");
            float perChunk = bushes / 25f;
            Assert.GreaterOrEqual(perChunk, 1f, $"Chi {perChunk:F2} bui moi chunk - duoi muc da chot.");
            Assert.LessOrEqual(perChunk, 3f, $"{perChunk:F2} bui moi chunk - tren muc da chot.");
        }

        [Test]
        public void Vegetation_UsesOriginalVendorGeometry_NotTheRetiredGeneratedApproximations()
        {
            // M4.6CD.1: co 72 dinh tu sinh va bui 162 dinh tu sinh deu bi RUT khoi production vi bong
            // dang, khong phai vi chi phi. Chung cung bi go khoi bang bake, nen lenh Rebuild khong the
            // lang le khoi phuc lai. Day la chot chan cho ca hai dieu do.
            var expected = new (string id, string vendor, int verts)[]
            {
                ("grass_b", "S_Grass_B.prefab", 230),
                ("grass_c", "S_Grass_C.prefab", 230),
                ("bush_a",  "S_Bush_A.prefab",  1863),
                ("bush_b",  "S_Bush_B.prefab",  2393),
            };

            foreach ((string id, string vendor, int verts) in expected)
            {
                DecorationMeshSource source = Entry(id).PrimarySource;
                StringAssert.Contains(vendor, source.SourceAssetPath,
                    $"{id} khong lay hinh hoc tu {vendor}.");
                Assert.AreEqual(verts, source.VertexCount,
                    $"{id} co {source.VertexCount} dinh - khong phai mesh vendor goc.");
            }

            // Bui diem nhan la nguon GOP hai submesh, nen duong dan van la prefab vendor.
            foreach ((string id, string vendor) in new[] { ("bush_c", "S_Bush_C.prefab"), ("bush_d", "S_Bush_D.prefab") })
                StringAssert.Contains(vendor, Entry(id).PrimarySource.SourceAssetPath, id);
        }

        [Test]
        public void RejectedGrassSources_StayOutOfProduction()
        {
            string[] rejected = { "S_Grass_01A", "S_Grass_02A", "SM_Nature_Grass", "SM_Grass_0" };

            foreach (DecorationPaletteEntry entry in Palette().Entries)
            foreach (DecorationMeshSource source in new[] { entry.PrimarySource, entry.SecondarySource })
            {
                if (source == null) continue;
                foreach (string bad in rejected)
                    Assert.IsFalse(source.SourceAssetPath.Contains(bad),
                        $"'{entry.StableId}' lay hinh hoc tu nguon da bi tu choi: {source.SourceAssetPath}");
            }
        }

        [Test]
        public void RejectedSolidSources_StayOutOfProduction()
        {
            // Garbage_03 doc ra mot cuc den khong nhan dang duoc; MegaCity Rock 01/02 la khoi tam mat;
            // Garbage_09/10/11 la quad phang. Tat ca bi tu choi o M4.6CD.1.
            string[] rejected =
            {
                "SM_Props_Garbage_03", "SM_Props_Garbage_09", "SM_Props_Garbage_10", "SM_Props_Garbage_11",
                "SM_Nature_Rock_01", "SM_Nature_Rock_02", "SM_IndustrialProps_Barrel_01", "SM_Props_Trash_",
            };

            foreach (DecorationPaletteEntry entry in Palette().Entries)
            foreach (DecorationMeshSource source in new[] { entry.PrimarySource, entry.SecondarySource })
            {
                if (source == null) continue;
                foreach (string bad in rejected)
                    Assert.IsFalse(source.SourceAssetPath.Contains(bad),
                        $"'{entry.StableId}' lay hinh hoc tu nguon da bi tu choi: {source.SourceAssetPath}");
            }
        }

        [Test]
        public void SolidScenery_UsesTheApprovedMidDetailSources()
        {
            var expected = new (string id, string prefab)[]
            {
                ("rock_a",   "Stone_Chunks_Small.prefab"),
                ("rock_b",   "Stone_Chunks_Large.prefab"),
                ("debris_a", "Parts_Pile_Small.prefab"),
                ("debris_c", "Parts_Pile_Medium.prefab"),
                ("debris_b", "SM_Props_Garbage_01.prefab"),
                ("barrel_a", "Fuel_A_Barrel_Dirty.prefab"),
                ("crate_a",  "Containers_Crate_Medium_Wood.prefab"),
                ("pallet_a", "Pallet_Wood.prefab"),
                ("tire_a",   "SM_IndustrialProps_Wheel_01.prefab"),
            };

            foreach ((string id, string prefab) in expected)
                StringAssert.Contains(prefab, Entry(id).PrimarySource.SourceAssetPath, id);
        }

        [Test]
        public void DirectorDensityTargets_AreMet()
        {
            float grass = (CountAtCheckpoint("grass_b") + CountAtCheckpoint("grass_c")) / 25f;
            Assert.GreaterOrEqual(grass, 35f, $"Co {grass:F1} khom moi chunk - duoi muc 35 da chot.");
            Assert.LessOrEqual(grass, 60f, $"Co {grass:F1} khom moi chunk - tren muc 60 da chot.");

            float accent = (CountAtCheckpoint("bush_c") + CountAtCheckpoint("bush_d")) / 25f;
            Assert.LessOrEqual(accent, 0.4f, $"Bui diem nhan {accent:F2} moi chunk - tren muc 0,4 da chot.");

            float manMade = (CountAtCheckpoint("barrel_a") + CountAtCheckpoint("crate_a")
                             + CountAtCheckpoint("pallet_a") + CountAtCheckpoint("tire_a")) / 25f;
            Assert.LessOrEqual(manMade, 2f, $"Do nhan tao {manMade:F2} moi chunk - tren muc 2 da chot.");
        }

        [Test]

        public void Bush_IsNotThinnedOnTheOuterRing()
        {
            // Bụi cao gần một mét: nó biến mất giữa chừng là lộ ngay đường ranh giới của ring.
            Assert.IsFalse(Entry("bush_a").DistanceDensityEligible);
        }

        [Test]
        public void Bush_ReadsAsGreenFoliage_NotAsAFlowerColour()
        {
            // M4.6CD.1 doi cach lay mau cua bui: khong con noi suy mot dai mau tren bang mau nua ma
            // dung mat na alpha `Leaf_4` o o atlas 2, mau do chinh o atlas mang san. Nen bai kiem tra
            // doi tu "tung dinh phai xanh" sang "O ATLAS phai xanh va co alpha" - dung cho ma loi
            // magenta cua M4.6CD se lo ra neu no quay lai.
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                atlas.LoadImage(System.IO.File.ReadAllBytes(
                    "Assets/_Project/Art/WorldStreaming/Decoration/T_WorldStreamingFoliageAtlas.png"));

                Rect rect = Entry("bush_a").PrimarySource.AtlasRect;
                int opaque = 0, green = 0;

                for (int gy = 0; gy < 16; gy++)
                for (int gx = 0; gx < 16; gx++)
                {
                    Color c = atlas.GetPixelBilinear(
                        rect.x + (gx + 0.5f) / 16f * rect.width,
                        rect.y + (gy + 0.5f) / 16f * rect.height);
                    if (c.a < 0.5f) continue;
                    opaque++;
                    if (c.g > c.r * 1.15f && c.g > c.b * 1.15f) green++;
                }

                Assert.Greater(opaque, 20, "O atlas cua bui gan nhu trong suot - mat na la da sai.");
                Assert.Greater(green / (float)opaque, 0.9f,
                    $"Chi {green}/{opaque} texel dac cua bui la mau la - o atlas dang mang mau sai.");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
            }
        }

        // --- Ba họ cảnh vật rắn --------------------------------------------------------------------

        [Test]
        public void SolidScenery_HasThreeDistinctFamilies()
        {
            foreach (string id in RockIds.Concat(DebrisIds).Concat(ManMadeIds))
            {
                DecorationPaletteEntry entry = Entry(id);
                Assert.AreEqual(DecorationCategory.Solid, entry.PrimarySource.Category,
                    $"{id} phải đi vào output Solid.");
                Assert.IsNull(entry.SecondarySource, $"{id}: cảnh vật rắn thường chỉ có một nguồn.");
            }

            Assert.GreaterOrEqual(new[] { RockIds, DebrisIds, ManMadeIds }.Count(f => f.Length > 0), 3,
                "Bảng màu phải có ít nhất ba họ cảnh vật rắn khác nhau.");
        }

        [Test]
        public void SolidScenery_IsNeverThinnedOnTheOuterRing()
        {
            foreach (string id in RockIds.Concat(DebrisIds).Concat(ManMadeIds))
                Assert.IsFalse(Entry(id).DistanceDensityEligible,
                    $"{id} có hình học Solid mà lại bật giảm mật độ vòng ngoài.");
        }

        [Test]
        public void SolidScenery_KeepsSpacing_SoPropsNeverGrowThroughEachOther()
        {
            foreach (string id in RockIds.Concat(DebrisIds).Concat(ManMadeIds))
                Assert.Greater(Entry(id).SpacingRadius, 0f,
                    $"{id} không có bán kính giãn cách — hai vật thể rắn sẽ mọc chồng lên nhau.");
        }

        [Test]
        public void SolidScenery_StaysSparse_SoTheWorldIsNotAJunkyard()
        {
            // "Hậu tận thế" chứ không phải "bãi phế liệu".
            //
            // M4.6CD.1 hạ ngưỡng dưới từ 4 xuống 2: cảnh vật rắn đổi từ những khối 136–208 đỉnh sang
            // cụm đá và đống phế liệu 1.194–3.372 đỉnh, nên số LƯỢNG phải giảm để giữ ngân sách trong
            // khi lượng CHI TIẾT trên màn hình lại tăng. Đo được 3,5 vật mỗi chunk ở checkpoint.
            float props = RockIds.Concat(DebrisIds).Concat(ManMadeIds).Sum(CountAtCheckpoint);
            float perChunk = props / 25f;
            Assert.LessOrEqual(perChunk, 12f,
                $"{perChunk:F1} vật rắn mỗi chunk — thế giới đọc thành bãi phế liệu, không phải nơi bị bỏ hoang.");
            Assert.GreaterOrEqual(perChunk, 2f,
                $"Chỉ {perChunk:F1} vật rắn mỗi chunk — quá thưa để đọc ra dấu vết con người.");

            // Món đắt nhất phải là món HIẾM nhất.
            Assert.Less(CountAtCheckpoint("debris_b"), CountAtCheckpoint("debris_a"),
                "Đống rác lớn (1.647 đỉnh) không được xuất hiện dày hơn túi rác lẻ (136 đỉnh).");
        }

        // --- Hợp đồng atlas và nguồn ----------------------------------------------------------------

        [Test]
        public void EverySource_Validates_AndKeepsItsAtlasRectInsideTheAtlas()
        {
            foreach (DecorationPaletteEntry entry in Palette().Entries)
            foreach (DecorationMeshSource source in new[] { entry.PrimarySource, entry.SecondarySource })
            {
                if (source == null) continue;
                Assert.IsTrue(source.Validate(out string error), $"{entry.StableId}: {error}");

                Rect r = source.AtlasRect;
                Assert.GreaterOrEqual(r.xMin, -1e-4f, source.StableId);
                Assert.GreaterOrEqual(r.yMin, -1e-4f, source.StableId);
                Assert.LessOrEqual(r.xMax, 1f + 1e-4f, source.StableId);
                Assert.LessOrEqual(r.yMax, 1f + 1e-4f, source.StableId);
            }
        }

        [Test]
        public void NoSource_TilesItsUv_WhileSharingAnAtlasSlot()
        {
            // Thân cây từng phải chiếm TRỌN atlas solid vì UV của nó chạy 5,92 × 4,66 vòng. M4.6CD in
            // sẵn 6×5 vòng vào ô rồi chuẩn hoá UV thân cây về [0,1] — nhờ đó atlas mới chia được cho
            // cảnh vật rắn. Nếu ai đó bỏ bước chuẩn hoá, `TilesUv` bật lại và ô con sẽ bôi bẹt vỏ cây.
            foreach (DecorationPaletteEntry entry in Palette().Entries)
            foreach (DecorationMeshSource source in new[] { entry.PrimarySource, entry.SecondarySource })
            {
                if (source == null) continue;
                Assert.IsFalse(source.TilesUv && !source.OccupiesWholeAtlas,
                    $"{source.StableId}: UV lặp nhưng lại nằm trong ô con {source.AtlasRect}.");

                foreach (Vector2 uv in source.Uv)
                {
                    Assert.GreaterOrEqual(uv.x, -1e-3f, source.StableId);
                    Assert.LessOrEqual(uv.x, 1f + 1e-3f, source.StableId);
                    Assert.GreaterOrEqual(uv.y, -1e-3f, source.StableId);
                    Assert.LessOrEqual(uv.y, 1f + 1e-3f, source.StableId);
                }
            }
        }

        // --- ID ổn định --------------------------------------------------------------------------

        [Test]
        public void StableIds_AreUnique_AndSoAreTheirSalts()
        {
            var ids = new HashSet<string>();
            var salts = new Dictionary<int, string>();

            foreach (DecorationPaletteEntry entry in Palette().Entries)
            {
                Assert.IsTrue(ids.Add(entry.StableId), $"Trùng stableId '{entry.StableId}'.");
                Assert.IsFalse(salts.TryGetValue(entry.Salt, out string other),
                    $"'{entry.StableId}' trùng muối với '{other}' — hai loài sẽ mọc đúng cùng một chỗ.");
                salts[entry.Salt] = entry.StableId;
            }
        }

        [Test]
        public void PreExistingStableIds_KeepTheirExactSalts()
        {
            // Muối suy từ stableId. Đổi một ID cũ là dời toàn bộ cây cỏ của loài đó trên cả thế giới
            // đã sinh ra — nên chốt bằng giá trị đo được, không phải bằng lời hứa.
            var expected = new Dictionary<string, int>
            {
                { "grass_b", 706372076 },
                { "grass_c", 723149695 },
                { "cattail_b", 1688521260 },
                { "flowers_g", 269180565 },
                { "tree_a", 1261864077 },
                { "bush_a", 1946775343 },
                { "rock_a", 1840979888 },
                { "rock_b", 1891312745 },
                { "debris_a", 120770842 },
                { "debris_b", 103993223 },
                { "barrel_a", 2050726897 },
                { "tire_a", 484319391 },
            };

            foreach (KeyValuePair<string, int> pair in expected)
                Assert.AreEqual(pair.Value, Entry(pair.Key).Salt,
                    $"Muối của '{pair.Key}' đã đổi — thế giới đã sinh ra sẽ không còn khớp.");
        }

        // --- Ngân sách ------------------------------------------------------------------------------

        [Test]
        public void EveryChunkAroundTheCheckpoint_StaysUnderBudgetAndUnderUInt16()
        {
            DecorationPalette palette = Palette();
            var list = new List<DecorationPlacement>(1024);
            int worstFoliage = 0, worstSolid = 0;

            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                DecorationSampler.Generate(Seed, new ChunkCoord(Checkpoint.X + dx, Checkpoint.Z + dz),
                    ChunkSize, palette, list);
                DecorationMeshBuilder.Build(palette, list);

                worstFoliage = Mathf.Max(worstFoliage, DecorationMeshBuilder.Foliage.VertexCount);
                worstSolid = Mathf.Max(worstSolid, DecorationMeshBuilder.Solid.VertexCount);
            }

            Assert.LessOrEqual(worstFoliage, palette.FoliageVertexBudget,
                $"Chunk nặng nhất tốn {worstFoliage} đỉnh foliage, vượt ngân sách.");
            Assert.LessOrEqual(worstSolid, palette.SolidVertexBudget,
                $"Chunk nặng nhất tốn {worstSolid} đỉnh solid, vượt ngân sách.");
            Assert.Less(worstFoliage, 65535, "Mesh foliage sẽ phải rơi sang chỉ số 32-bit.");
            Assert.Less(worstSolid, 65535, "Mesh solid sẽ phải rơi sang chỉ số 32-bit.");
        }

        // --- Gió --------------------------------------------------------------------------------------

        [Test]
        public void WindAmplitude_IsSofterForBushThanForGrass()
        {
            float grass = Entry("grass_b").WindAmplitude;
            float bush = Entry("bush_a").WindAmplitude;

            Assert.Greater(grass, bush,
                "Bụi rậm đu đưa mạnh ngang cỏ mềm — khối lá nặng mà lắc như lá cỏ thì sai vật lý thị giác.");
            Assert.Greater(bush, 0f, "Bụi rậm đứng chết cứng giữa một cánh đồng đang lay.");
        }

        [Test]
        public void SolidGeometry_NeverReceivesWind()
        {
            // Biên độ gió của Solid bị `DecorationMeshBuilder` ép về 0 bất kể palette ghi gì. Đây là
            // chốt chặn cho chính điều đó: dựng một chunk có cảnh vật rắn rồi đọc UV1.y của mesh Solid.
            DecorationPalette palette = Palette();
            var list = new List<DecorationPlacement>(1024);
            DecorationSampler.Generate(Seed, Checkpoint, ChunkSize, palette, list);
            DecorationMeshBuilder.Build(palette, list);

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
            try
            {
                if (!DecorationMeshBuilder.Solid.Apply(mesh, 0.5f))
                    Assert.Ignore("Chunk mốc không có hình học Solid.");

                var uv1 = new List<Vector2>();
                mesh.GetUVs(1, uv1);
                Assert.Greater(uv1.Count, 0, "Mesh Solid không mang UV1.");
                foreach (Vector2 u in uv1)
                    Assert.AreEqual(0f, u.y, 0f, "Cảnh vật rắn nhận biên độ gió — đá sẽ đu đưa.");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        // --- Tất định ------------------------------------------------------------------------------

        [Test]
        public void AddingSolidCategory_DidNotReorderOrDisturbTheExistingEntries()
        {
            // Điều đáng sợ nhất khi thêm entry: một loài mới chen vào luồng băm và làm dời cây cỏ cũ.
            // Năm loài cũ phải giữ ĐÚNG số lượng đo được trước M4.6CD.
            // Ba entry KHONG bi dung mat do o M4.6CD.1 phai giu nguyen tung con so qua moi milestone.
            var before = new Dictionary<string, int>
            {
                { "cattail_b", 254 }, { "flowers_g", 145 }, { "tree_a", 26 },
            };

            foreach (KeyValuePair<string, int> pair in before)
                Assert.AreEqual(pair.Value, CountAtCheckpoint(pair.Key),
                    $"'{pair.Key}' đã xê dịch sau khi thêm cảnh vật rắn.");
        }

        [Test]
        public void GenerationIsIdentical_WhenChunksAreVisitedInReverseOrder()
        {
            DecorationPalette palette = Palette();
            var forward = new List<string>();
            var reverse = new List<string>();
            var list = new List<DecorationPlacement>(1024);

            void Collect(List<string> into, int step)
            {
                for (int dz = step > 0 ? -2 : 2; step > 0 ? dz <= 2 : dz >= -2; dz += step)
                for (int dx = step > 0 ? -2 : 2; step > 0 ? dx <= 2 : dx >= -2; dx += step)
                {
                    var coord = new ChunkCoord(Checkpoint.X + dx, Checkpoint.Z + dz);
                    DecorationSampler.Generate(Seed, coord, ChunkSize, palette, list);
                    foreach (DecorationPlacement p in list)
                        into.Add($"{coord}|{palette.Entries[p.EntryIndex].StableId}|{p}");
                }
            }

            Collect(forward, 1);
            Collect(reverse, -1);

            forward.Sort();
            reverse.Sort();
            CollectionAssert.AreEqual(forward, reverse,
                "Thứ tự duyệt chunk làm đổi kết quả sinh — việc sinh không còn tất định.");
        }

        [Test]
        public void NegativeCoordinates_ProduceEveryFamily()
        {
            var seen = new HashSet<string>();
            var list = new List<DecorationPlacement>(1024);
            DecorationPalette palette = Palette();

            for (int dz = -3; dz <= 3; dz++)
            for (int dx = -3; dx <= 3; dx++)
            {
                DecorationSampler.Generate(Seed, new ChunkCoord(-76 + dx, -50 + dz), ChunkSize, palette, list);
                foreach (DecorationPlacement p in list) seen.Add(palette.Entries[p.EntryIndex].StableId);
            }

            foreach (string id in FoliageIds.Concat(RockIds).Concat(DebrisIds).Concat(ManMadeIds))
                Assert.Contains(id, seen.ToArray(),
                    $"'{id}' không xuất hiện lần nào ở vùng toạ độ âm.");
        }

        private static int CountAtCheckpoint(string stableId)
        {
            DecorationPalette palette = Palette();
            var list = new List<DecorationPlacement>(1024);
            int n = 0;

            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                DecorationSampler.Generate(Seed, new ChunkCoord(Checkpoint.X + dx, Checkpoint.Z + dz),
                    ChunkSize, palette, list);
                foreach (DecorationPlacement p in list)
                    if (palette.Entries[p.EntryIndex].StableId == stableId) n++;
            }

            return n;
        }
    }
}
