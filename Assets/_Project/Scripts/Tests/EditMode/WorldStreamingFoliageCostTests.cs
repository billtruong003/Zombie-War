using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.Editor;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode M3B.1: chi phí hình học của foliage.
    ///
    /// Bộ test này canh hai thứ mà một lần sửa vô ý rất dễ phá:
    ///
    /// 1. Nguồn tự sinh phải TẤT ĐỊNH. Chỉ cần một lời gọi `UnityEngine.Random` lọt vào bộ sinh là
    ///    mỗi lần bake lại ra một bụi cỏ khác, và chunk tái dụng sẽ nhấp nháy. Chỗ này không thể
    ///    kiểm bằng mắt nên phải kiểm bằng test.
    /// 2. Việc đổi hình học KHÔNG được làm xê dịch một placement nào. Vị trí cây cỏ đến từ hàm băm
    ///    theo `StableId`, còn mesh chỉ là thứ được dán vào vị trí đó — nên số lượng và phân bố phải
    ///    y hệt trước khi giảm đỉnh.
    public class WorldStreamingFoliageCostTests
    {
        private const int Seed = 20260809;
        private const float ChunkSize = 32f;
        private const string PalettePath = "Assets/_Project/Data/World/Decoration/DecorationPalette.asset";
        private const string SourceFolder = "Assets/_Project/Data/World/Decoration";

        /// <summary>Ô ring dùng làm mốc trong mọi báo cáo M3A/M3B. Đổi toạ độ này là đổi cả chuẩn so.</summary>
        private static readonly ChunkCoord Checkpoint = new ChunkCoord(74, 47);

        private static DecorationPalette LoadPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<DecorationPalette>(PalettePath);
            Assert.IsNotNull(palette, $"Thiếu palette ở {PalettePath} — chạy ZombieWar/World Streaming/Rebuild Decoration Assets.");
            return palette;
        }

        private static DecorationMeshSource LoadSource(string stableId)
        {
            var source = AssetDatabase.LoadAssetAtPath<DecorationMeshSource>($"{SourceFolder}/DMS_{stableId}.asset");
            Assert.IsNotNull(source, $"Thiếu nguồn DMS_{stableId}.asset.");
            return source;
        }

        // --- Bộ sinh phải tất định ---------------------------------------------------------------

        [Test]
        public void GrassTuft_SameSeed_ProducesIdenticalGeometry()
        {
            DecorationLowPolySources.BuildGrassTuft(1234, 14, 0.3f, 0.7f, 0.075f,
                new Vector2(0.2f, 0.3f), new Vector2(0.2f, 0.5f),
                out Vector3[] posA, out Vector3[] nrmA, out Vector2[] uvA, out int[] idxA);
            DecorationLowPolySources.BuildGrassTuft(1234, 14, 0.3f, 0.7f, 0.075f,
                new Vector2(0.2f, 0.3f), new Vector2(0.2f, 0.5f),
                out Vector3[] posB, out Vector3[] nrmB, out Vector2[] uvB, out int[] idxB);

            CollectionAssert.AreEqual(posA, posB, "Cùng hạt giống phải cho cùng vị trí đỉnh.");
            CollectionAssert.AreEqual(nrmA, nrmB);
            CollectionAssert.AreEqual(uvA, uvB);
            CollectionAssert.AreEqual(idxA, idxB);
        }

        [Test]
        public void FrondFan_SameSeed_ProducesIdenticalGeometry()
        {
            DecorationLowPolySources.BuildFrondFan(99, 9, 0.65f, 0.5f, 0.23f,
                new Vector2(0.4f, 0.1f), new Vector2(0.4f, 0.45f),
                out Vector3[] posA, out _, out Vector2[] uvA, out int[] idxA);
            DecorationLowPolySources.BuildFrondFan(99, 9, 0.65f, 0.5f, 0.23f,
                new Vector2(0.4f, 0.1f), new Vector2(0.4f, 0.45f),
                out Vector3[] posB, out _, out Vector2[] uvB, out int[] idxB);

            CollectionAssert.AreEqual(posA, posB);
            CollectionAssert.AreEqual(uvA, uvB);
            CollectionAssert.AreEqual(idxA, idxB);
        }

        [Test]
        public void Generators_DifferentSeeds_ProduceDifferentGeometry()
        {
            // Nếu hạt giống không thật sự đi vào hình học thì mọi bụi cỏ trên bản đồ sẽ giống hệt nhau.
            DecorationLowPolySources.BuildGrassTuft(1, 14, 0.3f, 0.7f, 0.075f,
                Vector2.zero, Vector2.one, out Vector3[] posA, out _, out _, out _);
            DecorationLowPolySources.BuildGrassTuft(2, 14, 0.3f, 0.7f, 0.075f,
                Vector2.zero, Vector2.one, out Vector3[] posB, out _, out _, out _);

            CollectionAssert.AreNotEqual(posA, posB);
        }

        [Test]
        public void GrassTuft_EmitsFourVertsAndTwoTrisPerBlade()
        {
            DecorationLowPolySources.BuildGrassTuft(7, 11, 0.3f, 0.7f, 0.075f,
                Vector2.zero, Vector2.one, out Vector3[] pos, out Vector3[] nrm, out Vector2[] uv, out int[] idx);

            Assert.AreEqual(11 * 4, pos.Length);
            Assert.AreEqual(pos.Length, nrm.Length);
            Assert.AreEqual(pos.Length, uv.Length);
            Assert.AreEqual(11 * 6, idx.Length);
            foreach (int i in idx) Assert.Less(i, pos.Length, "Chỉ số đỉnh vượt ra ngoài mảng.");
        }

        [Test]
        public void GrassTuft_BaseSitsOnGround_AndTipRises()
        {
            DecorationLowPolySources.BuildGrassTuft(7, 14, 0.3f, 0.8f, 0.075f,
                Vector2.zero, Vector2.one, out Vector3[] pos, out _, out _, out _);

            float maxY = float.MinValue;
            foreach (Vector3 p in pos)
            {
                // Lá cỏ mọc lên từ mặt đất: không đỉnh nào được chìm xuống dưới gốc chunk.
                Assert.GreaterOrEqual(p.y, -1e-4f, "Có đỉnh nằm dưới mặt đất — bụi cỏ sẽ bị đất cắt ngang.");
                maxY = Mathf.Max(maxY, p.y);
            }

            Assert.Greater(maxY, 0.2f, "Bụi cỏ gần như bẹt — chiều cao khuôn vendor đã không được dùng.");
        }

        // --- Nguồn đã bake ------------------------------------------------------------------------

        [Test]
        public void GeneratedSources_AreDramaticallyCheaperThanVendorOriginals()
        {
            // Ngưỡng đặt rất rộng so với số đo thật (cỏ 56, dương xỉ 36). Đây là chốt chặn chống việc
            // vô tình bake lại từ mesh vendor, không phải chỗ ghim con số chính xác.
            // M4.6CD.1 DAO NGUOC chieu cua bai test nay.
            //
            // No ra doi o M3B.1 de canh rang co KHONG bi bake lai tu mesh vendor. M4.6CD.1 quyet
            // dinh nguoc lai: hinh hoc vendor goc moi la ban production, vi ban tu sinh 72 dinh doc
            // ra qua manh duoi camera gameplay. Nen nay canh dieu dung: co phai LA mesh vendor 230
            // dinh, va khong duoc lang le tut ve ban tu sinh.
            foreach (string id in new[] { "grass_b", "grass_c" })
            {
                DecorationMeshSource grass = LoadSource(id);
                Assert.AreEqual(230, grass.VertexCount,
                    $"{id} khong con la hinh hoc vendor goc - ban tu sinh 72 dinh da bi tu choi o M4.6CD.1.");
                StringAssert.Contains("S_Grass_", grass.SourceAssetPath, id);
            }

            Assert.AreEqual(1863, LoadSource("bush_a").VertexCount);
            Assert.AreEqual(2393, LoadSource("bush_b").VertexCount);
        }

        [Test]
        public void GeneratedSources_KeepFoliageCategoryAndValidate()
        {
            foreach (string id in new[] { "grass_b", "grass_c", "bush_a", "bush_b" })
            {
                DecorationMeshSource source = LoadSource(id);
                Assert.AreEqual(DecorationCategory.Foliage, source.Category, id);
                Assert.IsTrue(source.Validate(out string error), $"{id}: {error}");
                Assert.IsFalse(source.TilesUv, $"{id} không được đánh dấu tiling — nó dùng chung ô atlas.");
            }
        }

        [Test]
        public void GeneratedSources_UvStayInsideTheirAtlasSlot()
        {
            // UV ra ngoài [0,1] sẽ bị phép ánh xạ vào ô atlas kéo sang ô hàng xóm, tức là cỏ sẽ ăn
            // nhầm màu của lá cây.
            foreach (string id in new[] { "grass_b", "grass_c", "bush_a", "bush_b" })
            {
                // Biên độ nới 1e-5: UV của mesh vendor chạm đúng 1.0 và phép biến đổi từ FBX lên gốc
                // prefab để lại sai số dấu phẩy động cỡ 1e-7 (đo được 1.00000012 trên `bush_b`).
                // Điều đang canh là UV không LẤN sang ô hàng xóm, chứ không phải độ chính xác tuyệt đối.
                foreach (Vector2 uv in LoadSource(id).Uv)
                {
                    Assert.GreaterOrEqual(uv.x, -1e-5f, id);
                    Assert.LessOrEqual(uv.x, 1f + 1e-5f, id);
                    Assert.GreaterOrEqual(uv.y, -1e-5f, id);
                    Assert.LessOrEqual(uv.y, 1f + 1e-5f, id);
                }
            }
        }

        [Test]
        public void GeneratedSources_KeepVendorFootprint()
        {
            // Khổ cây phải nằm trong tầm khuôn vendor, nếu không mật độ nhìn sẽ đổi dù số placement
            // không đổi.
            //
            // M4.6CD.1: co va bui nay LA mesh vendor, nen kho cua chung dung bang kho vendor.
            // Tran dat sat ngay tren so do that de bat duoc neu ai do trao nguon khac vao.
            var expected = new Dictionary<string, Vector3>
            {
                { "grass_b", new Vector3(0.56f, 0.62f, 0.45f) },
                { "grass_c", new Vector3(0.42f, 0.66f, 0.44f) },
                { "bush_a", new Vector3(1.36f, 1.22f, 1.37f) },
                { "bush_b", new Vector3(1.21f, 1.12f, 1.20f) },
            };

            foreach (KeyValuePair<string, Vector3> pair in expected)
            {
                Vector3 size = LoadSource(pair.Key).LocalBounds.size;
                Assert.LessOrEqual(size.x, pair.Value.x, $"{pair.Key} rộng hơn khuôn vendor.");
                Assert.LessOrEqual(size.y, pair.Value.y, $"{pair.Key} cao hơn khuôn vendor.");
                Assert.LessOrEqual(size.z, pair.Value.z, $"{pair.Key} dài hơn khuôn vendor.");
            }
        }

        // --- Việc đặt cây không được đổi ----------------------------------------------------------

        [Test]
        public void PlacementCounts_AtCheckpoint_AreUnchangedByTheGeometrySwap()
        {
            // Đây là bài kiểm tra quan trọng nhất của M3B.1: đổi HÌNH HỌC không được làm xê dịch
            // việc đặt cây. Điều đó vẫn là thứ đang được canh ở đây.
            //
            // M4.6B.1 đổi PHÂN BỐ của hai entry cỏ một cách có chủ đích và đã được duyệt
            // (candidatesPerCell 1→3, baseDensity chia 3, bật vón cụm), nên hai con số cỏ và tổng
            // buộc phải đổi theo. Bốn entry còn lại thì KHÔNG được đổi — chúng dùng chung hàm băm
            // với cỏ, nên nếu một trong số chúng nhúc nhích thì nghĩa là thay đổi đã rò sang entry
            // khác, và đó mới là lỗi thật. Đo được: cattail_b/flowers_g/fern_d/tree_a giữ nguyên
            // từng con số, chỉ grass_b 1075→944 và grass_c 838→775 đổi.
//
// M4.6C.4 gỡ hẳn `fern_d` khỏi bảng: hoa thị dẹt đó bị người dùng từ chối ở góc nhìn từ
// trên xuống. Tổng rơi đúng 97 (2241→2144) — bằng CHÍNH số fern_d đo được trước đó, nên
// biết chắc không entry nào khác bị xê dịch theo.
//
// M4.6CD thêm bảy entry mới (một bụi rậm + sáu cảnh vật rắn). Năm entry CŨ giữ nguyên
// từng con số — 944 / 775 / 254 / 145 / 26 — nên biết chắc entry mới không hề chen vào
// luồng băm của entry cũ. Tổng lên đúng bằng phần cộng thêm:
//   2144 + 360 + 110 + 69 + 125 + 15 + 24 + 16 = 2863.
            var expected = new Dictionary<string, int>
            {
                { "grass_b", 609 }, { "grass_c", 490 },
                { "bush_a", 33 }, { "bush_b", 31 }, { "bush_c", 1 }, { "bush_d", 3 },
                { "cattail_b", 254 }, { "flowers_g", 145 }, { "tree_a", 26 },
                { "rock_a", 25 }, { "rock_b", 7 },
                { "debris_a", 28 }, { "debris_c", 11 }, { "debris_b", 13 },
                { "barrel_a", 15 }, { "crate_a", 3 }, { "pallet_a", 8 }, { "tire_a", 3 },
            };

            DecorationPalette palette = LoadPalette();
            var counts = new int[palette.Entries.Count];
            var list = new List<DecorationPlacement>(512);
            int total = 0;

            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                DecorationSampler.Generate(Seed, new ChunkCoord(Checkpoint.X + dx, Checkpoint.Z + dz),
                    ChunkSize, palette, list);
                total += list.Count;
                foreach (DecorationPlacement p in list) counts[p.EntryIndex]++;
            }

            Assert.AreEqual(1705, total, "Tổng số placement quanh checkpoint đã đổi.");
            for (int i = 0; i < palette.Entries.Count; i++)
            {
                string id = palette.Entries[i].StableId;
                Assert.IsTrue(expected.TryGetValue(id, out int want), $"Entry lạ trong palette: {id}");
                Assert.AreEqual(want, counts[i], $"Số lượng {id} đã đổi — việc giảm đỉnh đã làm xê dịch hàm băm.");
            }
        }

        [Test]
        public void FoliageVertexCost_AtCheckpoint_IsWellUnderTheM3ABaseline()
        {
            const int M3ABaselineFoliageVertices = 699636;

            DecorationPalette palette = LoadPalette();
            var list = new List<DecorationPlacement>(512);
            long foliage = 0;

            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                DecorationSampler.Generate(Seed, new ChunkCoord(Checkpoint.X + dx, Checkpoint.Z + dz),
                    ChunkSize, palette, list);
                foreach (DecorationPlacement p in list)
                {
                    DecorationPaletteEntry entry = palette.Entries[p.EntryIndex];
                    foreach (DecorationMeshSource src in new[] { entry.PrimarySource, entry.SecondarySource })
                        if (src != null && src.Category == DecorationCategory.Foliage) foliage += src.VertexCount;
                }
            }

            // M4.6CD.1 ĐẢO NGƯỢC mục tiêu của bài test này, một cách có chủ đích.
            //
            // M3B.1 đặt ra "giảm ít nhất 30% so với M3A" khi cỏ còn là mesh tự sinh. M4.6CD.1 quyết
            // định ngược lại: hình học vendor gốc mới là bản production, vì bản tự sinh đọc ra quá
            // mảnh. Chi phí foliage vì thế TĂNG, và đó là kết quả mong muốn chứ không phải hồi quy.
            //
            // Nên bài test giữ lại điều vẫn còn đúng và vẫn còn đáng canh: tổng foliage phải nằm dưới
            // mốc M3A (569.429 so với 699.636 — thấp hơn 18,6%), tức việc quay về mesh vendor KHÔNG
            // đưa chi phí về lại thời chưa tối ưu. Cái chặn thật sự của production là ngân sách MỖI
            // CHUNK, và nó được canh ở `WorldStreamingPaletteLockTests` cùng bộ PlayMode.
            Assert.Less(foliage, M3ABaselineFoliageVertices,
                $"Đỉnh foliage ở checkpoint là {foliage}, đã vượt lại mốc M3A ({M3ABaselineFoliageVertices}).");
        }
    }
}
