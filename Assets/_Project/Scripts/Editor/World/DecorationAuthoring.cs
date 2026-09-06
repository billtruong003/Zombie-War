using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Đường bake ở Editor cho trang trí: từ prefab vendor → atlas của project + asset
    /// <see cref="DecorationMeshSource"/> của project.
    ///
    /// Vì sao phải bake: mesh và texture của vendor đều có <c>isReadable = false</c>. Runtime không
    /// được phép phụ thuộc chuyện đó, và ta cũng không được sửa import setting của vendor. Nên ở đây
    /// đọc hình học qua API Editor và đọc pixel qua một lần Blit vào RenderTexture — cả hai đều không
    /// đụng vào asset gốc.
    ///
    /// Chạy lại bao nhiêu lần cũng ra kết quả như nhau (idempotent): asset đã có thì ghi đè nội dung
    /// chứ không tạo bản sao, nên GUID và mọi tham chiếu vẫn giữ nguyên.
    /// </summary>
    public static class DecorationAuthoring
    {
        public const string SourceFolder = "Assets/_Project/Data/World/Decoration";
        public const string AtlasFolder = "Assets/_Project/Art/WorldStreaming/Decoration";
        public const string PalettePath = SourceFolder + "/DecorationPalette.asset";
        public const string FoliageAtlasPath = AtlasFolder + "/T_WorldStreamingFoliageAtlas.png";
        public const string SolidAtlasPath = AtlasFolder + "/T_WorldStreamingSolidAtlas.png";

        private const int SlotSize = 1024;
        private const int SlotPadding = 16;

        /// <summary>Một submesh nguồn được duyệt cho MVP, kèm ô atlas mà UV của nó ánh xạ vào.</summary>
        private readonly struct SourceSpec
        {
            public readonly string StableId;
            public readonly string PrefabPath;
            public readonly int SubMesh;
            public readonly DecorationCategory Category;
            public readonly int AtlasSlot;

            /// <summary>
            /// Chuẩn hoá UV nguồn về <c>[0,1]</c> theo đúng dải mà nó đang chiếm, trước khi ánh xạ vào ô.
            ///
            /// Chỉ bật cho nguồn có UV LẶP. Nguồn như vậy không thể chia ô với nguồn khác — kẹp thẳng
            /// về <c>[0,1]</c> sẽ bôi bẹt hoa văn. Nhưng nếu ô atlas của nó được vẽ sẵn đúng số lần lặp
            /// đó (xem <see cref="AtlasSlotSpec.TileX"/>), thì phép chuẩn hoá tuyến tính này giữ NGUYÊN
            /// tần số hoa văn: N vòng vỏ cây trên mesh vẫn ăn N vòng vỏ cây đã in trong ô.
            /// </summary>
            public readonly bool NormalizeUv;

            public SourceSpec(string stableId, string prefabPath, int subMesh, DecorationCategory category,
                int atlasSlot, bool normalizeUv = false)
            {
                StableId = stableId;
                PrefabPath = prefabPath;
                SubMesh = subMesh;
                Category = category;
                AtlasSlot = atlasSlot;
                NormalizeUv = normalizeUv;
            }
        }

        private const string VegRoot = "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios";
        private const string CityRoot = "Assets/JC_LP_MegaCity/Models";
        private const string CityPrefabRoot = "Assets/JC_LP_MegaCity/Prefabs";
        private const string KayRoot = "Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)";

        /// <summary>
        /// Bảng nguồn đã chọn sau khi soi chi phí mesh và material thật của cả pack.
        ///
        /// Ô atlas foliage 0 = `T_Plant_Atlas_D` (mọi cây cỏ nhỏ dùng chung), ô 1 = `Leaf_2` (tán cây).
        /// Atlas solid: ô 0 = `T_Trunk_D` in sẵn 6×5 vòng, ô 1 = `T_MegaCity_01`.
        ///
        /// M4.6CD: cả pack MegaCity dùng CHUNG một bảng màu `T_MegaCity_01`, mọi prop chỉ có một
        /// submesh, và UV của chúng đều nằm gọn trong `[0,1]` trên những mảng màu rất nhỏ. Đó là lý do
        /// đá/rác/thùng phuy nhập được vào một ô atlas duy nhất mà không phải tách vật liệu nào.
        /// </summary>
        private static readonly SourceSpec[] Sources =
        {
            // --- Cây cỏ. M4.6CD.1 quay lại HÌNH HỌC VENDOR GỐC cho cỏ và bụi. ---
            //
            // M4.6CD tự sinh cỏ 72 đỉnh và bụi 162 đỉnh để tiết kiệm. Đó là tối ưu sai thứ tự: nhìn
            // qua camera gameplay thật, cỏ tự sinh quá mảnh còn bụi tự sinh đọc ra một khối đa diện
            // rỗng gấp nếp. Mesh vendor 230 và 1.863-2.393 đỉnh đắt hơn nhiều lần nhưng có bóng dáng
            // ba chiều thật, và ngân sách vẫn chịu được sau khi chỉnh MẬT ĐỘ — nên chất lượng bóng
            // dáng thắng số đỉnh tối thiểu.
            new SourceSpec("grass_b",     VegRoot + "/Prefabs/Bushes/S_Grass_B.prefab",   0, DecorationCategory.Foliage, 0),
            new SourceSpec("grass_c",     VegRoot + "/Prefabs/Bushes/S_Grass_C.prefab",   0, DecorationCategory.Foliage, 0),
            // Bụi thường: một submesh, mặt nạ `Leaf_4` → ô foliage 2.
            new SourceSpec("bush_a",      VegRoot + "/Prefabs/Bushes/S_Bush_A.prefab",    0, DecorationCategory.Foliage, 2),
            new SourceSpec("bush_b",      VegRoot + "/Prefabs/Bushes/S_Bush_B.prefab",    0, DecorationCategory.Foliage, 2),
            new SourceSpec("cattail_b",   VegRoot + "/Prefabs/Bushes/S_Cattail_B.prefab", 0, DecorationCategory.Foliage, 0),
            new SourceSpec("flowers_g",   VegRoot + "/Prefabs/Bushes/S_Flowers_G.prefab", 0, DecorationCategory.Foliage, 0),
            // S_Tree_A: submesh 0 = thân (M_Trunk) → Solid, submesh 1 = tán (S_Tree_01A) → Foliage.
            // Thân cây có UV lặp 5,92 × 4,66 vòng → chuẩn hoá vào ô 0, ô đó được in sẵn 6×5 vòng.
            new SourceSpec("tree_a_trunk", VegRoot + "/ScenesSetUps/Trees/S_Tree_A.prefab", 0, DecorationCategory.Solid, 0, normalizeUv: true),
            new SourceSpec("tree_a_leaf",  VegRoot + "/ScenesSetUps/Trees/S_Tree_A.prefab", 1, DecorationCategory.Foliage, 1),

            // --- Cảnh vật rắn. M4.6CD.1 thay toàn bộ bằng nguồn có độ chi tiết trung bình. ---
            //
            // Đá KayKit là CỤM nhiều mảnh, không phải khối tám mặt như `SM_Nature_Rock_01/02`.
            // Cả pack KayKit chỉ một vật liệu + một texture → tất cả vào ô solid 2.
            new SourceSpec("rock_a",   KayRoot + "/Prefabs/Stone_Chunks_Small.prefab",           0, DecorationCategory.Solid, 2),
            new SourceSpec("rock_b",   KayRoot + "/Prefabs/Stone_Chunks_Large.prefab",           0, DecorationCategory.Solid, 2),
            // Phế liệu: đống nhỏ là nền, đống vừa hiếm hơn.
            new SourceSpec("debris_a", KayRoot + "/Prefabs/Parts_Pile_Small.prefab",             0, DecorationCategory.Solid, 2),
            new SourceSpec("debris_c", KayRoot + "/Prefabs/Parts_Pile_Medium.prefab",            0, DecorationCategory.Solid, 2),
            // Đống rác lớn của MegaCity ở lại: nó là điểm nhấn kể chuyện, rất hiếm. Ô solid 1.
            new SourceSpec("debris_b", CityPrefabRoot + "/Props/SM_Props_Garbage_01.prefab",     0, DecorationCategory.Solid, 1),
            // Nhân tạo.
            new SourceSpec("barrel_a", KayRoot + "/Prefabs/Fuel_A_Barrel_Dirty.prefab",          0, DecorationCategory.Solid, 2),
            new SourceSpec("crate_a",  KayRoot + "/Prefabs/Containers_Crate_Medium_Wood.prefab", 0, DecorationCategory.Solid, 2),
            new SourceSpec("pallet_a", KayRoot + "/Prefabs/Pallet_Wood.prefab",                  0, DecorationCategory.Solid, 2),
            new SourceSpec("tire_a",   CityPrefabRoot + "/IndustrialProps/SM_IndustrialProps_Wheel_01.prefab", 0, DecorationCategory.Solid, 1),
        };

        /// <summary>Một mảnh của nguồn gộp: submesh nào của prefab, ánh xạ vào ô atlas nào.</summary>
        private readonly struct MergePart
        {
            public readonly int SubMesh;
            public readonly int AtlasSlot;
            public MergePart(int subMesh, int atlasSlot) { SubMesh = subMesh; AtlasSlot = atlasSlot; }
        }

        /// <summary>
        /// Nguồn gộp NHIỀU submesh của cùng một prefab thành MỘT luồng hình học.
        ///
        /// Vì sao cần: `S_Bush_C/D` có hai submesh dùng hai vật liệu khác nhau — thân/cành lấy màu trên
        /// `T_Plant_Atlas_D`, còn tán lá dùng mặt nạ `Leaf_4`. Kiến trúc runtime chỉ có MỘT vật liệu
        /// foliage và MỘT submesh cho mỗi chunk, và `DecorationPaletteEntry` chỉ nhận nguồn phụ khi nó
        /// thuộc CATEGORY KHÁC (thân cây Solid + tán Foliage), nên không thể nhét hai mảnh Foliage vào
        /// một entry. Cách đúng là gộp ở Editor: mỗi submesh được ánh xạ vào ô atlas riêng của nó rồi
        /// nối lại thành một mảng đỉnh duy nhất. Runtime không biết gì về chuyện đã có hai vật liệu.
        /// </summary>
        private readonly struct MergedSourceSpec
        {
            public readonly string StableId;
            public readonly string PrefabPath;
            public readonly DecorationCategory Category;
            public readonly MergePart[] Parts;

            public MergedSourceSpec(string stableId, string prefabPath, DecorationCategory category, MergePart[] parts)
            {
                StableId = stableId;
                PrefabPath = prefabPath;
                Category = category;
                Parts = parts;
            }
        }

        private static readonly MergedSourceSpec[] MergedSources =
        {
            // Bụi điểm nhấn hiếm: submesh 0 = tán lá (`Leaf_4`, ô 2), submesh 1 = cành (`T_Plant_Atlas_D`, ô 0).
            new MergedSourceSpec("bush_c", VegRoot + "/Prefabs/Bushes/S_Bush_C.prefab", DecorationCategory.Foliage,
                new[] { new MergePart(0, 2), new MergePart(1, 0) }),
            new MergedSourceSpec("bush_d", VegRoot + "/Prefabs/Bushes/S_Bush_D.prefab", DecorationCategory.Foliage,
                new[] { new MergePart(0, 2), new MergePart(1, 0) }),
        };

        /// <summary>
        /// Một nguồn do project TỰ SINH, thay cho mesh vendor quá đắt.
        ///
        /// M3B.1 đo được rằng ba mục này ngốn 76,2% tổng đỉnh foliage của một vòng ring, trong khi
        /// pack không còn nguồn nào rẻ hơn để đổi sang: cỏ 230 đỉnh đã là mesh nhẹ nhất, LOD1/LOD2 của
        /// cây bụi đều rỗng (chỉ để cull), và ô atlas 0 là bảng màu phẳng nên không có silhouette alpha
        /// để thay hình học bằng card.
        ///
        /// Nên thay vì hạ mật độ — vốn là phương án cuối cùng — mỗi mục ở đây được sinh lại bằng hình
        /// học tối thiểu, còn KÍCH THƯỚC và TOẠ ĐỘ UV thì kế thừa từ chính mesh vendor mà nó thay thế.
        /// Kế thừa như vậy là điểm mấu chốt: cây mới đứng đúng khổ cũ và ăn đúng ô màu cũ, nên khối
        /// lượng và tông màu của thảm thực vật giữ nguyên, chỉ số đỉnh giảm.
        /// </summary>
        private readonly struct GeneratedSpec
        {
            public readonly string StableId;
            /// <summary>Mesh vendor được dùng làm khuôn: lấy khổ và UV, không lấy hình học.</summary>
            public readonly string TemplatePath;
            public readonly int TemplateSubMesh;
            public readonly int AtlasSlot;
            public readonly GeneratedShape Shape;
            public readonly int PartCount;
            public readonly float PartWidth;
            /// <summary>Chỉ dùng cho <see cref="GeneratedShape.BushBlob"/>: số vòng của khối cầu thấp.</summary>
            public readonly int Rings;

            /// <summary>
            /// Dải màu chỉ định thẳng trên ô atlas, thay cho dải kế thừa từ khuôn vendor.
            /// <c>null</c> = kế thừa (mặc định).
            ///
            /// Kế thừa UV từ khuôn chỉ đúng khi hình sinh ra THAY THẾ đúng cái cây mà khuôn mô tả — cỏ
            /// thay cỏ thì lấy đúng màu cỏ. Với bụi rậm thì không: khuôn `S_Bush_D` là một bụi nhiều
            /// tầng, và hai đỉnh thấp nhất/cao nhất của nó rơi vào mảng HOA MÀU TÍM trên bảng màu
            /// (`#A704AA`), nên bụi sinh ra là một khối tím chói. Đo được bằng cách đọc thẳng pixel
            /// atlas, không phải đoán. Khi hình học không còn là bản sao của khuôn thì màu phải được
            /// chọn tường minh.
            /// </summary>
            public readonly Vector2? UvBase;
            public readonly Vector2? UvTip;

            public GeneratedSpec(string stableId, string templatePath, int templateSubMesh, int atlasSlot,
                GeneratedShape shape, int partCount, float partWidth, int rings = 0,
                Vector2? uvBase = null, Vector2? uvTip = null)
            {
                UvBase = uvBase;
                UvTip = uvTip;
                StableId = stableId;
                TemplatePath = templatePath;
                TemplateSubMesh = templateSubMesh;
                AtlasSlot = atlasSlot;
                Shape = shape;
                PartCount = partCount;
                PartWidth = partWidth;
                Rings = rings;
            }
        }

        private enum GeneratedShape
        {
            GrassTuft = 0,
            FrondFan = 1,
            BushBlob = 2,
        }

        /// <summary>
        /// Nguồn tự sinh đang dùng cho production. RỖNG kể từ M4.6CD.1.
        ///
        /// Cỏ 72 đỉnh và bụi 162 đỉnh đã bị RÚT khỏi production vì bóng dáng, không phải vì chi phí, và
        /// chúng bị gỡ khỏi chính bảng này chứ không chỉ khỏi palette — nếu để lại đây thì mỗi lần bấm
        /// "Rebuild Decoration Assets" sẽ lặng lẽ ghi đè `DMS_grass_b`/`DMS_grass_c`/`DMS_bush_a` bằng
        /// hình học tự sinh và khôi phục đúng bảng đã bị từ chối.
        /// `DecorationLowPolySources` vẫn còn (test tất định vẫn dùng), nó chỉ không còn nuôi production.
        /// </summary>
        private static readonly GeneratedSpec[] GeneratedSources = System.Array.Empty<GeneratedSpec>();

        /// <summary>Một ô của atlas project: texture vendor nguồn, màu nhân vào, và số lần lặp in sẵn.</summary>
        private readonly struct AtlasSlotSpec
        {
            public readonly string TexturePath;

            /// <summary>
            /// Mau nhan vao o khi bake.
            ///
            /// `T_Plant_Atlas_D` da la anh mau day du nen giu nguyen. `Leaf_2` thi khong: no chi la mat
            /// na xam, mau la that nam o `_BaseColor` cua material vendor `S_Tree_01A`. Khong nhan mau
            /// vao thi tan cay ra mau trang. Atlas cua project phai tu chua mau cuoi cung, vi runtime
            /// chi co MOT material foliage dung chung, khong the tint rieng tung nguon.
            /// </summary>
            public readonly Color Tint;

            /// <summary>
            /// Số lần lặp hoa văn in sẵn vào ô. <c>1×1</c> = chép nguyên ảnh.
            ///
            /// Lớn hơn 1 chỉ dành cho nguồn có UV lặp: in sẵn đúng số vòng mà mesh cần rồi chuẩn hoá UV
            /// của mesh về <c>[0,1]</c>. Nhờ vậy nguồn lặp mới chia được atlas với nguồn khác mà TẦN SỐ
            /// hoa văn không đổi — thay vì bị kẹp phẳng ra một vòng duy nhất.
            /// </summary>
            public readonly int TileX;
            public readonly int TileY;

            public AtlasSlotSpec(string texturePath, Color tint, int tileX = 1, int tileY = 1)
            {
                TexturePath = texturePath;
                Tint = tint;
                TileX = tileX;
                TileY = tileY;
            }

            public bool Tiles => TileX > 1 || TileY > 1;
        }

        private static readonly AtlasSlotSpec[] FoliageAtlasSlots =
        {
            new AtlasSlotSpec(VegRoot + "/Textures/T_Bush_Atlas/T_Plant_Atlas_D.png", Color.white),
            // Mau goc cua vendor la (0.263, 1.000, 0.513) — qua ruc so voi mat dat olive cua M2A,
            // tan cay nhin nhu day len tren nen. Ha do bao hoa mot chut de hai lop ngoi cung nhau.
            new AtlasSlotSpec(VegRoot + "/Textures/T_AlphasLeafs/Leaf_2.psd", new Color(0.340f, 0.480f, 0.280f, 1f)),
            // M4.6CD.1 — ô 2: mặt nạ lá của bụi rậm LuxArt (`Leaf_4`), dùng chung cho S_Bush_A/B/C/D.
            //
            // `Leaf_4` chỉ là mặt nạ xám; màu thật nằm ở `_BaseColor` của vật liệu vendor `S_Bush_01A`
            // = (0.000, 0.736, 0.447). Nhân thẳng màu đó vào một mặt nạ xám cho ra một sắc lục-lam
            // KHÔNG CÓ THÀNH PHẦN ĐỎ — đúng cái "neon cyan" mà hợp đồng cấm, vì shader toon của project
            // không cộng thêm ánh sáng môi trường như shader vendor. Nên ở đây làm y hệt điều đã làm cho
            // tán cây ở ô 1: giữ nguyên sắc độ của vendor nhưng nâng đỏ và hạ bão hoà. Bụi được đặt sáng
            // và ấm hơn tán cây một chút để hai tầng không dính vào nhau khi nhìn từ trên xuống.
            new AtlasSlotSpec(VegRoot + "/Textures/T_AlphasLeafs/Leaf_4.psd", new Color(0.300f, 0.520f, 0.300f, 1f)),
        };

        /// <summary>
        /// Atlas solid.
        ///
        /// Ô 0 giữ vỏ cây. UV của thân cây chạy <c>[-2.39,-1.84]..[3.53,2.82]</c> — tức 5,92 × 4,66
        /// vòng — nên trước M4.6CD nó phải chiếm TRỌN atlas và không chừa chỗ cho prop nào khác. In sẵn
        /// 6×5 vòng vào ô rồi chuẩn hoá UV thân cây là cách mở khoá atlas mà vẫn giữ nguyên mật độ hoa
        /// văn (sai số tần số 6/5,92 ≈ 1,3%).
        ///
        /// Ô 1 là bảng màu dùng chung của cả pack MegaCity — đá, rác, thùng phuy, lốp xe đều lấy màu
        /// từ đây, nên toàn bộ cảnh vật rắn nhập được vào MỘT vật liệu.
        /// </summary>
        private static readonly AtlasSlotSpec[] SolidAtlasSlots =
        {
            new AtlasSlotSpec(VegRoot + "/Textures/T_Trunk/T_Trunk_D.png", Color.white, 6, 5),
            new AtlasSlotSpec("Assets/JC_LP_MegaCity/Textures/T_MegaCity_01.png", Color.white),
            // M4.6CD.1 — ô 2: bảng màu dùng chung của cả pack KayKit Resource Bits. Giống MegaCity,
            // cả pack chỉ có MỘT vật liệu và MỘT texture, mọi prop một submesh, UV đều nằm trong [0,1]
            // trên những mảng màu nhỏ — nên đá, đống phế liệu, thùng phuy, thùng gỗ và pallet đều nhập
            // được vào đây mà không cần thêm vật liệu hay submesh nào.
            new AtlasSlotSpec(KayRoot + "/Textures/resource_bits_texture.png", Color.white),
        };

        [MenuItem("ZombieWar/World Streaming/Rebuild Decoration Assets", priority = 110)]
        public static void RebuildMenu()
        {
            string report = Rebuild();
            Debug.Log("[WorldStreaming] " + report);
            EditorUtility.DisplayDialog("Decoration assets", report, "OK");
        }

        /// <summary>Bake atlas + nguồn mesh. Trả về báo cáo dạng text.</summary>
        public static string Rebuild()
        {
            Directory.CreateDirectory(SourceFolder);
            Directory.CreateDirectory(AtlasFolder);

            var report = new System.Text.StringBuilder();

            // Cả hai atlas nay đều nhiều ô → đệm + Clamp. Sau khi thân cây được chuẩn hoá UV về [0,1]
            // (xem `SolidAtlasSlots`), không còn nguồn nào lấy mẫu ra ngoài ô của nó, nên atlas solid
            // không cần wrap Repeat nữa.
            BakeAtlas(FoliageAtlasSlots, FoliageAtlasPath, SlotPadding, TextureWrapMode.Clamp, report);
            BakeAtlas(SolidAtlasSlots, SolidAtlasPath, SlotPadding, TextureWrapMode.Clamp, report);

            AssetDatabase.Refresh();

            int baked = 0;
            foreach (SourceSpec spec in Sources)
                if (BakeSource(spec, report)) baked++;

            foreach (GeneratedSpec spec in GeneratedSources)
                if (BakeGenerated(spec, report)) baked++;

            foreach (MergedSourceSpec spec in MergedSources)
                if (BakeMerged(spec, report)) baked++;

            EnsurePalette(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Insert(0, $"Đã bake {baked}/{Sources.Length + GeneratedSources.Length + MergedSources.Length} nguồn trang trí.\n");
            return report.ToString();
        }

        private static DecorationMeshSource LoadSource(string stableId) =>
            AssetDatabase.LoadAssetAtPath<DecorationMeshSource>($"{SourceFolder}/DMS_{stableId}.asset");

        /// <summary>
        /// Dựng (hoặc cập nhật tại chỗ) palette MVP.
        ///
        /// Mật độ được chọn để một chunk giàu cỏ rơi vào khoảng 100–200 lần đặt mà vẫn nằm dưới
        /// ngân sách đỉnh. Ô lưới và ái lực biome ở đây là số khởi điểm để chỉnh, không phải hằng số.
        /// </summary>
        private static void EnsurePalette(System.Text.StringBuilder report)
        {
            var palette = AssetDatabase.LoadAssetAtPath<DecorationPalette>(PalettePath);
            bool created = palette == null;
            if (created) palette = ScriptableObject.CreateInstance<DecorationPalette>();

            var entries = new List<DecorationPaletteEntry>();

            // `densityEligible`: chỉ bật cho cây cỏ nhỏ, lặp lại nhiều, không mang ý nghĩa gameplay.
            // Phân loại nằm ở đây — dữ liệu do project sở hữu — chứ không dò theo tên prefab lúc chạy.
            void Add(string id, string primary, string secondary, float cell, int candidates, float density,
                float spacing, Vector4 affinity, Vector2 scale, int priority, bool densityEligible,
                float clumpCell = 0f, float clumpAmount = 0f, Color? tint = null, float wind = 1f)
            {
                var entry = new DecorationPaletteEntry();
                entry.Configure(id, LoadSource(primary), secondary != null ? LoadSource(secondary) : null,
                    cell, candidates, density, spacing, affinity, scale, new Vector2(0.82f, 1.12f), priority,
                    yaw: true, densityEligible: densityEligible,
                    clumpCell: clumpCell, clumpAmount: clumpAmount, foliageColor: tint, wind: wind);
                entries.Add(entry);
            }

            // =======================================================================================
            // M4.6CD.1 — BẢNG MÀU ĐÃ CHỐT.
            //
            // Nguyên tắc đổi so với M4.6CD: CHẤT LƯỢNG BÓNG DÁNG đứng trước số đỉnh tối thiểu. Cỏ và
            // bụi quay về hình học vendor gốc; đá và phế liệu đổi sang nguồn có độ chi tiết trung bình.
            // Chi phí tăng được trả bằng MẬT ĐỘ, không bằng cách hạ chất lượng mesh.
            //
            // Mục tiêu mật độ cho một chunk 32×32 m ở vùng hợp:
            //   cỏ (grass_b + grass_c)      35–60
            //   bụi thường (bush_a + bush_b) 1–3
            //   bụi điểm nhấn (bush_c + d)   ≤ 0,4 trung bình
            //   cụm đá (rock_a + rock_b)     2–5
            //   nhân tạo (phuy+thùng+pallet+lốp) 0–2
            // =======================================================================================

            //   id           primary     secondary       cell cand density spacing  affinity(D,G,S,R)                       scale                    prio  xa     vón cụm      màu riêng   gió
            Add("grass_b",    "grass_b",  null,           2.6f, 3, 0.1000f, 0f,   new Vector4(0.25f, 1.00f, 0.05f, 0.05f), new Vector2(0.80f, 1.40f), 20, true,  11f, 0.85f);
            Add("grass_c",    "grass_c",  null,           2.9f, 3, 0.0965f, 0f,   new Vector4(0.35f, 0.90f, 0.15f, 0.05f), new Vector2(0.80f, 1.35f), 20, true,  11f, 0.85f);

            // Bụi thường: TẦNG GIỮA. Không giảm mật độ ở vòng ngoài — bụi cao hơn một mét, nó biến mất
            // giữa chừng thì lộ ngay ranh giới của ring. Gió nhẹ hơn cỏ: khối lá nặng không lắc như lá cỏ.
            // M5.1.1 CP5: bush scale +25% (0.80–1.20 → 1.00–1.50). Codex judged the old size "visibly
            // too small, sinks into the terrain" against the player; A/B captures in
            // Temp/CodexReview/M5.1_Full/props/ (H0 vs H1 at one deterministic anchor) back the call.
            Add("bush_a",     "bush_a",   null,          11.0f, 1, 0.225f,   2.4f, new Vector4(0.35f, 1.00f, 0.05f, 0.20f), new Vector2(1.00f, 1.50f), 70, false, 9f, 0.40f,
                wind: 0.35f);
            Add("bush_b",     "bush_b",   null,          11.0f, 1, 0.225f,   2.4f, new Vector4(0.30f, 1.00f, 0.05f, 0.25f), new Vector2(1.00f, 1.50f), 70, false, 9f, 0.40f,
                wind: 0.35f);
            // Bụi điểm nhấn: hai submesh đã được GỘP ở Editor (lá + cành), nên vẫn một nguồn duy nhất.
            // Đắt gấp đôi bụi thường và chỉ để làm biến thể màu — giữ dưới 0,4 mỗi chunk.
            Add("bush_c",     "bush_c",   null,          26.0f, 1, 0.11f,   5.0f, new Vector4(0.25f, 1.00f, 0.05f, 0.20f), new Vector2(0.94f, 1.38f), 68, false, 0f, 0f,
                wind: 0.30f);
            Add("bush_d",     "bush_d",   null,          26.0f, 1, 0.11f,   5.0f, new Vector4(0.30f, 1.00f, 0.05f, 0.20f), new Vector2(0.94f, 1.38f), 68, false, 0f, 0f,
                wind: 0.30f);

            Add("cattail_b",  "cattail_b", null,          4.5f, 1, 0.34f,   0f,   new Vector4(0.10f, 1.00f, 0.00f, 0.00f), new Vector2(0.75f, 1.20f), 40, true);
            Add("flowers_g",  "flowers_g", null,          5.5f, 1, 0.32f,   0f,   new Vector4(0.15f, 1.00f, 0.05f, 0.00f), new Vector2(0.80f, 1.20f), 50, true);
            // Một placement duy nhất: thân đi vào Solid, tán lá đi vào Foliage.
            Add("tree_a",     "tree_a_leaf", "tree_a_trunk", 16f, 1, 0.60f, 18f,  new Vector4(0.30f, 1.00f, 0.05f, 0.15f), new Vector2(0.85f, 1.25f), 90, false);

            // --- Cảnh vật rắn ---------------------------------------------------------------------
            //
            // Ái lực biome: ĐÁ nghiêng hẳn về vùng đá/sỏi — đó là chuyện địa chất. Nhưng PHẾ LIỆU và
            // ĐỒ NHÂN TẠO thì không: nơi từng có người ở giờ bị cỏ phủ lại, nên chúng phải xuất hiện cả
            // trong vùng cỏ rậm, nếu không thì đúng chỗ người chơi hay đứng nhất lại sạch bong.
            //
            // Không mục nào được bật `densityEligible`: chúng có hình học Solid và `Validate` sẽ chặn.

            //   id           primary     secondary  cell  cand density spacing  affinity(D,G,S,R)                       scale                    prio  xa
            Add("rock_a",     "rock_a",   null,       8.5f, 1, 0.24f,  2.4f, new Vector4(0.45f, 0.25f, 0.35f, 1.00f), new Vector2(0.60f, 1.15f), 80, false);
            Add("rock_b",     "rock_b",   null,      13.0f, 1, 0.16f,  3.2f, new Vector4(0.40f, 0.30f, 0.30f, 1.00f), new Vector2(0.55f, 1.00f), 82, false);
            Add("debris_a",   "debris_a", null,      12.0f, 1, 0.20f,  2.4f, new Vector4(1.00f, 0.60f, 0.50f, 0.40f), new Vector2(0.70f, 1.15f), 55, false);
            Add("debris_c",   "debris_c", null,      20.0f, 1, 0.18f,  4.0f, new Vector4(1.00f, 0.55f, 0.45f, 0.40f), new Vector2(0.65f, 1.00f), 58, false);
            // Đống rác lớn MegaCity: điểm nhấn kể chuyện, dưới một cái mỗi chunk.
            Add("debris_b",   "debris_b", null,      22.0f, 1, 0.30f,  9.0f, new Vector4(1.00f, 0.55f, 0.40f, 0.35f), new Vector2(0.75f, 1.15f), 65, false);
            Add("barrel_a",   "barrel_a", null,      18.0f, 1, 0.22f,  4.0f, new Vector4(0.85f, 0.60f, 0.45f, 0.50f), new Vector2(0.75f, 1.00f), 75, false);
            Add("crate_a",    "crate_a",  null,      30.0f, 1, 0.22f,  5.0f, new Vector4(0.90f, 0.55f, 0.45f, 0.45f), new Vector2(0.70f, 0.95f), 72, false);
            Add("pallet_a",   "pallet_a", null,      26.0f, 1, 0.24f,  4.0f, new Vector4(0.90f, 0.55f, 0.45f, 0.45f), new Vector2(0.70f, 1.00f), 62, false);
            // Lốp xe rời: món "tire" thật sự tìm được trong dự án, không bóc ra từ xe hay từ toà nhà.
            // M5.1.1 CP5: +12% (0.80–1.15 → 0.90–1.30) — đọc được ở gameplay scale mà không thành POI.
            Add("tire_a",     "tire_a",   null,      20.0f, 1, 0.22f,  4.0f, new Vector4(0.90f, 0.55f, 0.45f, 0.45f), new Vector2(0.90f, 1.30f), 60, false);

            // Ngân sách Solid nâng 15.000 → 45.000: cảnh vật rắn đổi từ khối 143 đỉnh sang cụm đá và
            // đống phế liệu 1.194–3.372 đỉnh. Trần cứng UInt16 giữ nguyên 60.000.
            palette.SetBudgets(45000, 45000, 60000);

            palette.SetEntriesForTests(entries);

            if (created) AssetDatabase.CreateAsset(palette, PalettePath);
            else EditorUtility.SetDirty(palette);

            report.Append(palette.Validate(out string error)
                ? $"Palette OK — {entries.Count} entry.\n"
                : $"PALETTE KHÔNG HỢP LỆ: {error}\n");
        }

        // --- Atlas -------------------------------------------------------------------------------

        /// <summary>
        /// Ghép các texture nguồn thành một atlas của project.
        ///
        /// Đọc pixel bằng Blit qua RenderTexture nên chạy được với texture nén, không readable, và
        /// tuyệt đối không cần sửa importer của vendor. Mỗi ô được vẽ vào phần lõi rồi giãn viền ra
        /// vùng đệm, để mip không kéo màu của ô bên cạnh sang.
        /// </summary>
        private static void BakeAtlas(AtlasSlotSpec[] slotSpecs, string outputPath, int padding,
            TextureWrapMode wrapMode, System.Text.StringBuilder report)
        {
            int slots = slotSpecs.Length;
            AtlasGrid(slots, out int columns, out int rows);
            int width = SlotSize * columns;
            int height = SlotSize * rows;

            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = rt;
                GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, width, height, 0);

                for (int i = 0; i < slots; i++)
                {
                    AtlasSlotSpec slot = slotSpecs[i];
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(slot.TexturePath);
                    if (texture == null)
                    {
                        report.Append("THIẾU texture atlas: ").Append(slot.TexturePath).Append('\n');
                        continue;
                    }

                    // `GL.LoadPixelMatrix(0, w, h, 0)` cho trục Y hướng XUỐNG, còn `AtlasRectFor` nói
                    // bằng UV (Y hướng LÊN). Nên hàng vẽ là hàng UV bị lật lại. Bỏ qua chi tiết này thì
                    // atlas ba ô sẽ tráo hai hàng cho nhau và mọi nguồn lấy nhầm ô.
                    int cx = i % columns;
                    int cy = i / columns;
                    int drawX = cx * SlotSize;
                    int drawY = (rows - 1 - cy) * SlotSize;

                    var inner = new Rect(
                        drawX + padding,
                        drawY + padding,
                        SlotSize - padding * 2,
                        SlotSize - padding * 2);

                    if (!slot.Tiles)
                    {
                        Graphics.DrawTexture(inner, texture);
                        continue;
                    }

                    // Ô lặp: vẽ TRÀN cả vùng đệm, và vẽ bằng cách kéo dài chính hoa văn đó ra ngoài
                    // biên. Vùng đệm khi ấy chứa phần tiếp diễn thật của hoa văn chứ không phải một
                    // dải màu bị giãn, nên mip lấy mẫu ở biên vẫn ra đúng vỏ cây — và không nuốt sang
                    // màu của ô hàng xóm. Sau đó `DilatePadding` được bỏ qua cho ô này.
                    float padU = padding / (float)(SlotSize - padding * 2);
                    var full = new Rect(drawX, drawY, SlotSize, SlotSize);
                    var source = new Rect(
                        -padU * slot.TileX,
                        -padU * slot.TileY,
                        slot.TileX * (1f + padU * 2f),
                        slot.TileY * (1f + padU * 2f));

                    Graphics.DrawTexture(full, texture, source, 0, 0, 0, 0, Color.white);
                }

                GL.PopMatrix();

                var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply(false);

                ApplyTints(readback, slotSpecs);
                if (padding > 0) DilatePadding(readback, slotSpecs, padding);

                File.WriteAllBytes(outputPath, readback.EncodeToPNG());
                Object.DestroyImmediate(readback);

                report.Append("Atlas ").Append(Path.GetFileName(outputPath))
                      .Append(' ').Append(width).Append('x').Append(height)
                      .Append(" (").Append(slots).Append(" ô, đệm ").Append(padding)
                      .Append(" px, wrap ").Append(wrapMode).Append(")");
                for (int i = 0; i < slots; i++)
                    report.Append("\n    ô").Append(i).Append(' ')
                          .Append(Path.GetFileNameWithoutExtension(slotSpecs[i].TexturePath))
                          .Append(slotSpecs[i].Tiles ? $" lặp {slotSpecs[i].TileX}×{slotSpecs[i].TileY}" : "");
                report.Append('\n');
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            ConfigureAtlasImporter(outputPath, wrapMode);
        }

        /// <summary>Nhan mau vao tung o. Giu nguyen alpha — alpha la hinh dang cua chiec la.</summary>
        private static void ApplyTints(Texture2D atlas, AtlasSlotSpec[] slotSpecs)
        {
            if (slotSpecs == null) return;

            Color32[] pixels = atlas.GetPixels32();
            int width = atlas.width;
            bool changed = false;

            AtlasGrid(slotSpecs.Length, out int columns, out _);

            for (int slot = 0; slot < slotSpecs.Length; slot++)
            {
                Color tint = slotSpecs[slot].Tint;
                if (tint == Color.white) continue;

                changed = true;
                int x0 = (slot % columns) * SlotSize;
                int y0 = (slot / columns) * SlotSize;
                for (int y = y0; y < y0 + SlotSize && y < atlas.height; y++)
                {
                    for (int x = x0; x < x0 + SlotSize && x < width; x++)
                    {
                        int i = y * width + x;
                        Color32 c = pixels[i];
                        pixels[i] = new Color32(
                            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * tint.r), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * tint.g), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * tint.b), 0, 255),
                            c.a);
                    }
                }
            }

            if (!changed) return;
            atlas.SetPixels32(pixels);
            atlas.Apply(false);
        }

        /// <summary>Kéo pixel viền của phần lõi ra vùng đệm để mip không lấy màu của ô bên cạnh.</summary>
        private static void DilatePadding(Texture2D atlas, AtlasSlotSpec[] slotSpecs, int padding)
        {
            Color32[] pixels = atlas.GetPixels32();
            int width = atlas.width;
            int height = atlas.height;
            AtlasGrid(slotSpecs.Length, out int columns, out _);

            for (int slot = 0; slot < slotSpecs.Length; slot++)
            {
                // Ô lặp đã được vẽ tràn sang vùng đệm bằng chính hoa văn tiếp diễn của nó — giãn viền
                // ở đây sẽ ĐÈ MẤT phần tiếp diễn đó và làm hiện một vệt màu dọc trên vỏ cây.
                if (slotSpecs[slot].Tiles) continue;

                int x0 = (slot % columns) * SlotSize;
                int y0 = (slot / columns) * SlotSize;
                int innerMinX = x0 + padding;
                int innerMaxX = x0 + SlotSize - padding - 1;
                int innerMinY = y0 + padding;
                int innerMaxY = y0 + SlotSize - padding - 1;

                for (int y = y0; y < y0 + SlotSize && y < height; y++)
                {
                    int clampedY = Mathf.Clamp(y, innerMinY, innerMaxY);
                    for (int x = x0; x < x0 + SlotSize && x < width; x++)
                    {
                        if (x >= innerMinX && x <= innerMaxX && y >= innerMinY && y <= innerMaxY) continue;

                        int clampedX = Mathf.Clamp(x, innerMinX, innerMaxX);
                        pixels[y * width + x] = pixels[clampedY * width + clampedX];
                    }
                }
            }

            atlas.SetPixels32(pixels);
            atlas.Apply(false);
        }

        private static void ConfigureAtlasImporter(string path, TextureWrapMode wrapMode)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = wrapMode;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Bố cục lưới của atlas: số cột và số hàng cho <paramref name="slotCount"/> ô.
        ///
        /// Xếp thành LƯỚI chứ không thành một hàng ngang. Một hàng ngang ba ô 1024 px sẽ ra atlas
        /// 3072 px, mà 3072 vượt trần 2048 của importer nên Unity sẽ ép nó xuống 2048×683 — méo hình.
        /// Lưới 2×2 giữ nguyên 1024 px cho mỗi ô ở đúng khổ 2048×2048 quen thuộc với thiết bị di động.
        /// Với 2 ô, lưới suy biến về đúng hàng ngang cũ (2048×1024), nên bố cục trước đó không đổi.
        /// </summary>
        private static void AtlasGrid(int slotCount, out int columns, out int rows)
        {
            columns = Mathf.CeilToInt(Mathf.Sqrt(slotCount));
            rows = Mathf.CeilToInt(slotCount / (float)columns);
        }

        /// <summary>Ô atlas của một slot, đã chuẩn hoá về [0,1] và đã trừ vùng đệm.</summary>
        private static Rect AtlasRectFor(int slot, int slotCount, int padding)
        {
            AtlasGrid(slotCount, out int columns, out int rows);
            float atlasWidth = SlotSize * columns;
            float atlasHeight = SlotSize * rows;
            int cx = slot % columns;
            int cy = slot / columns;

            return new Rect(
                (cx * SlotSize + padding) / atlasWidth,
                (cy * SlotSize + padding) / atlasHeight,
                (SlotSize - padding * 2) / atlasWidth,
                (SlotSize - padding * 2) / atlasHeight);
        }

        // --- Nguồn tự sinh ------------------------------------------------------------------------

        /// <summary>
        /// Sinh một nguồn rẻ và ghi đè lên đúng asset cũ (`DMS_&lt;stableId&gt;.asset`).
        ///
        /// Ghi đè tại chỗ chứ không tạo asset mới là có chủ đích: GUID được giữ, nên tham chiếu trong
        /// palette không đứt, id không đổi, và do id mới là thứ nuôi hàm băm nên VỊ TRÍ ĐẶT CÂY không
        /// suy chuyển một placement nào. Chỉ hình học bên trong nhẹ đi.
        /// </summary>
        private static bool BakeGenerated(GeneratedSpec spec, System.Text.StringBuilder report)
        {
            if (!TryReadTemplate(spec, out Bounds bounds, out Vector2 uvLow, out Vector2 uvHigh,
                    out int templateVerts, out string error))
            {
                report.Append("KHÔNG đọc được khuôn cho ").Append(spec.StableId).Append(": ").Append(error).Append('\n');
                return false;
            }

            int slotCount = FoliageAtlasSlots.Length;
            Rect rect = AtlasRectFor(spec.AtlasSlot, slotCount, SlotPadding);

            // Hạt giống lấy từ chính stableId nên mỗi lần bake lại ra đúng một hình học — không có
            // `UnityEngine.Random`, không có `string.GetHashCode()` (giá trị đó đổi giữa các phiên).
            int seed = DecorationHash.SaltFromId(spec.StableId);

            Vector3[] positions;
            Vector3[] normals;
            Vector2[] uv;
            int[] indices;

            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float height = Mathf.Max(bounds.size.y, 0.05f);

            if (spec.UvBase.HasValue) uvLow = spec.UvBase.Value;
            if (spec.UvTip.HasValue) uvHigh = spec.UvTip.Value;

            switch (spec.Shape)
            {
                case GeneratedShape.GrassTuft:
                    DecorationLowPolySources.BuildGrassTuft(seed, spec.PartCount, radius, height, spec.PartWidth,
                        uvLow, uvHigh, out positions, out normals, out uv, out indices);
                    break;
                case GeneratedShape.BushBlob:
                    // `PartCount` = số đoạn quanh trục, `PartWidth` = độ méo. Khối cầu thấp bắt đầu từ
                    // y=0 nên bụi ngồi đúng mặt đất, và mọi mặt đều phẳng (normal riêng cho từng tam
                    // giác) để nó ăn đúng ba dải sáng toon như nhân vật.
                    DecorationLowPolySources.BuildLowPolyCanopy(seed, spec.Rings, spec.PartCount, radius, height,
                        spec.PartWidth, uvLow, uvHigh, out positions, out normals, out uv, out indices);
                    break;
                default:
                    DecorationLowPolySources.BuildFrondFan(seed, spec.PartCount, radius, height, spec.PartWidth,
                        uvLow, uvHigh, out positions, out normals, out uv, out indices);
                    break;
            }

            string assetPath = $"{SourceFolder}/DMS_{spec.StableId}.asset";
            var source = AssetDatabase.LoadAssetAtPath<DecorationMeshSource>(assetPath);
            bool created = source == null;
            if (created) source = ScriptableObject.CreateInstance<DecorationMeshSource>();

            source.Bake(spec.StableId, spec.TemplatePath, spec.TemplateSubMesh, DecorationCategory.Foliage,
                rect, positions, normals, uv, indices);

            if (created) AssetDatabase.CreateAsset(source, assetPath);
            else EditorUtility.SetDirty(source);

            if (!source.Validate(out string invalid))
            {
                report.Append("KHÔNG HỢP LỆ ").Append(spec.StableId).Append(": ").Append(invalid).Append('\n');
                return false;
            }

            report.Append(spec.StableId).Append(" → TỰ SINH ").Append(spec.Shape)
                  .Append(" slot ").Append(spec.AtlasSlot)
                  .Append("  v=").Append(positions.Length)
                  .Append(" tri=").Append(indices.Length / 3)
                  .Append("  (khuôn vendor v=").Append(templateVerts)
                  .Append(", giảm ").Append((100f * (templateVerts - positions.Length) / templateVerts).ToString("F1")).Append("%)")
                  .Append("  size=").Append(source.LocalBounds.size.ToString("F2")).Append('\n');
            return true;
        }

        /// <summary>
        /// Đọc mesh vendor chỉ để lấy KHỔ và TOẠ ĐỘ UV, rồi bỏ hình học đi.
        ///
        /// `uvLow`/`uvHigh` là UV của đỉnh thấp nhất và cao nhất — với cỏ, đó chính là hai màu gốc và
        /// ngọn mà vendor đang lấy trên bảng màu, nên bụi cỏ mới chuyển sắc y hệt bụi cũ.
        /// </summary>
        private static bool TryReadTemplate(GeneratedSpec spec, out Bounds bounds,
            out Vector2 uvLow, out Vector2 uvHigh, out int templateVerts, out string error)
        {
            bounds = default;
            uvLow = Vector2.zero;
            uvHigh = Vector2.zero;
            templateVerts = 0;
            error = null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.TemplatePath);
            if (prefab == null) { error = "thiếu prefab " + spec.TemplatePath; return false; }

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0 || filters[0].sharedMesh == null) { error = "prefab không có mesh"; return false; }

            Mesh mesh = filters[0].sharedMesh;
            Matrix4x4 toPrefabRoot = prefab.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix;

            if (!TryExtract(mesh, spec.TemplateSubMesh, new Rect(0f, 0f, 1f, 1f), toPrefabRoot,
                    out Vector3[] positions, out _, out Vector2[] uv, out _, out _, out _))
            {
                error = "không trích được submesh " + spec.TemplateSubMesh;
                return false;
            }

            templateVerts = positions.Length;

            bounds = new Bounds(positions[0], Vector3.zero);
            int lowIndex = 0;
            int highIndex = 0;
            for (int i = 1; i < positions.Length; i++)
            {
                bounds.Encapsulate(positions[i]);
                if (positions[i].y < positions[lowIndex].y) lowIndex = i;
                if (positions[i].y > positions[highIndex].y) highIndex = i;
            }

            uvLow = uv[lowIndex];
            uvHigh = uv[highIndex];

            return true;
        }

        // --- Nguồn gộp nhiều submesh ---------------------------------------------------------------

        /// <summary>
        /// Gộp nhiều submesh của một prefab thành MỘT <see cref="DecorationMeshSource"/>.
        ///
        /// Mỗi mảnh được trích riêng rồi ánh xạ UV vào ô atlas CỦA NÓ ngay tại đây — chứ không để
        /// runtime làm, vì runtime chỉ nhớ được một ô atlas cho cả nguồn. Sau khi ánh xạ xong, UV của
        /// mọi mảnh đã nằm trong toạ độ atlas tuyệt đối, nên nguồn gộp khai báo ô của mình là TRỌN
        /// atlas và `DecorationAccumulator` chuyển tiếp UV nguyên vẹn.
        /// </summary>
        private static bool BakeMerged(MergedSourceSpec spec, System.Text.StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null)
            {
                report.Append("THIẾU prefab: ").Append(spec.PrefabPath).Append('\n');
                return false;
            }

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0 || filters[0].sharedMesh == null)
            {
                report.Append("KHÔNG có mesh trong: ").Append(spec.PrefabPath).Append('\n');
                return false;
            }

            Mesh mesh = filters[0].sharedMesh;
            Matrix4x4 toPrefabRoot = prefab.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix;

            bool foliage = spec.Category == DecorationCategory.Foliage;
            int slotCount = foliage ? FoliageAtlasSlots.Length : SolidAtlasSlots.Length;

            var positions = new List<Vector3>(4096);
            var normals = new List<Vector3>(4096);
            var uv = new List<Vector2>(4096);
            var indices = new List<int>(8192);

            foreach (MergePart part in spec.Parts)
            {
                if (part.SubMesh < 0 || part.SubMesh >= mesh.subMeshCount)
                {
                    report.Append(spec.StableId).Append(": submesh ").Append(part.SubMesh)
                          .Append(" không tồn tại (có ").Append(mesh.subMeshCount).Append(")\n");
                    return false;
                }

                Rect rect = AtlasRectFor(part.AtlasSlot, slotCount, SlotPadding);
                if (!TryExtract(mesh, part.SubMesh, rect, toPrefabRoot,
                        out Vector3[] p, out Vector3[] n, out Vector2[] u, out int[] idx, out _, out _))
                {
                    report.Append(spec.StableId).Append(": không trích được submesh ").Append(part.SubMesh).Append('\n');
                    return false;
                }

                int offset = positions.Count;
                positions.AddRange(p);
                normals.AddRange(n);
                for (int i = 0; i < u.Length; i++)
                    uv.Add(new Vector2(
                        rect.x + Mathf.Clamp01(u[i].x) * rect.width,
                        rect.y + Mathf.Clamp01(u[i].y) * rect.height));
                for (int i = 0; i < idx.Length; i++) indices.Add(offset + idx[i]);
            }

            string assetPath = $"{SourceFolder}/DMS_{spec.StableId}.asset";
            var source = AssetDatabase.LoadAssetAtPath<DecorationMeshSource>(assetPath);
            bool created = source == null;
            if (created) source = ScriptableObject.CreateInstance<DecorationMeshSource>();

            // Ô = trọn atlas: UV đã ở toạ độ atlas tuyệt đối rồi, ánh xạ thêm lần nữa sẽ bóp méo.
            source.Bake(spec.StableId, spec.PrefabPath, -1, spec.Category,
                new Rect(0f, 0f, 1f, 1f), positions.ToArray(), normals.ToArray(), uv.ToArray(), indices.ToArray());

            if (created) AssetDatabase.CreateAsset(source, assetPath);
            else EditorUtility.SetDirty(source);

            if (!source.Validate(out string error))
            {
                report.Append("KHÔNG HỢP LỆ ").Append(spec.StableId).Append(": ").Append(error).Append('\n');
                return false;
            }

            report.Append(spec.StableId).Append(" → GỘP ").Append(spec.Parts.Length).Append(" submesh → ")
                  .Append(spec.Category)
                  .Append("  v=").Append(positions.Count).Append(" tri=").Append(indices.Count / 3)
                  .Append(" size=").Append(source.LocalBounds.size.ToString("F2")).Append('\n');
            return true;
        }

        // --- Nguồn mesh ---------------------------------------------------------------------------

        private static bool BakeSource(SourceSpec spec, System.Text.StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null)
            {
                report.Append("THIẾU prefab: ").Append(spec.PrefabPath).Append('\n');
                return false;
            }

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0 || filters[0].sharedMesh == null)
            {
                report.Append("KHÔNG có mesh trong: ").Append(spec.PrefabPath).Append('\n');
                return false;
            }

            Mesh mesh = filters[0].sharedMesh;
            if (spec.SubMesh < 0 || spec.SubMesh >= mesh.subMeshCount)
            {
                report.Append("Submesh ").Append(spec.SubMesh).Append(" không tồn tại trong ")
                      .Append(mesh.name).Append(" (có ").Append(mesh.subMeshCount).Append(")\n");
                return false;
            }

            bool foliage = spec.Category == DecorationCategory.Foliage;
            int slotCount = foliage ? FoliageAtlasSlots.Length : SolidAtlasSlots.Length;
            Rect rect = AtlasRectFor(spec.AtlasSlot, slotCount, SlotPadding);

            // Nung luon phep bien doi tu MeshFilter len goc prefab: FBX thuong co scale import va
            // node con co offset. Bo qua no thi co se to bang cai cay.
            Matrix4x4 toPrefabRoot = prefab.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix;

            if (!TryExtract(mesh, spec.SubMesh, rect, toPrefabRoot,
                    out Vector3[] positions, out Vector3[] normals, out Vector2[] uv, out int[] indices,
                    out Vector2 uvMin, out Vector2 uvMax))
            {
                report.Append("Không trích được submesh của ").Append(spec.StableId).Append('\n');
                return false;
            }

            if (spec.NormalizeUv)
            {
                // Kéo dải UV thật về đúng [0,1]. Ô atlas tương ứng đã được in sẵn đúng số vòng lặp, nên
                // đây KHÔNG phải phép kẹp làm bẹt hoa văn: nó chỉ đổi hệ quy chiếu.
                Vector2 span = uvMax - uvMin;
                if (span.x <= 1e-5f || span.y <= 1e-5f)
                {
                    report.Append(spec.StableId).Append(": dải UV suy biến ").Append(span.ToString("F4"))
                          .Append(" — không chuẩn hoá được.\n");
                    return false;
                }

                for (int i = 0; i < uv.Length; i++)
                    uv[i] = new Vector2((uv[i].x - uvMin.x) / span.x, (uv[i].y - uvMin.y) / span.y);
            }

            string assetPath = $"{SourceFolder}/DMS_{spec.StableId}.asset";
            var source = AssetDatabase.LoadAssetAtPath<DecorationMeshSource>(assetPath);
            bool created = source == null;
            if (created) source = ScriptableObject.CreateInstance<DecorationMeshSource>();

            source.Bake(spec.StableId, spec.PrefabPath, spec.SubMesh, spec.Category,
                rect, positions, normals, uv, indices);

            if (created) AssetDatabase.CreateAsset(source, assetPath);
            else EditorUtility.SetDirty(source);

            if (!source.Validate(out string error))
            {
                report.Append("KHÔNG HỢP LỆ ").Append(spec.StableId).Append(": ").Append(error).Append('\n');
                return false;
            }

            report.Append(spec.StableId).Append(" → ").Append(spec.Category)
                  .Append(" slot ").Append(spec.AtlasSlot)
                  .Append("  v=").Append(positions.Length)
                  .Append(" tri=").Append(indices.Length / 3)
                  .Append(" size=").Append(source.LocalBounds.size.ToString("F2"))
                  .Append("  uv[").Append(uvMin.x.ToString("F2")).Append(',').Append(uvMin.y.ToString("F2"))
                  .Append("]..[").Append(uvMax.x.ToString("F2")).Append(',').Append(uvMax.y.ToString("F2")).Append(']')
                  .Append(spec.NormalizeUv ? "  → CHUẨN HOÁ về [0,1] (ô in sẵn số vòng lặp)" : "")
                  .Append(source.TilesUv ? "  TILING (atlas riêng, wrap Repeat)" : "").Append('\n');
            return true;
        }

        /// <summary>
        /// Trích một submesh và đánh số lại đỉnh, chỉ giữ những đỉnh mà submesh đó thật sự dùng.
        ///
        /// Bước đánh số lại rất đáng: thân cây và tán lá dùng chung một mảng đỉnh, nếu copy nguyên
        /// mảng thì mỗi nguồn sẽ mang theo toàn bộ chi phí của cả cây.
        /// </summary>
        private static bool TryExtract(Mesh mesh, int subMesh, Rect atlasRect, Matrix4x4 toPrefabRoot,
            out Vector3[] positions, out Vector3[] normals, out Vector2[] uv, out int[] indices,
            out Vector2 uvMin, out Vector2 uvMax)
        {
            positions = null;
            normals = null;
            uv = null;
            indices = null;
            uvMin = Vector2.zero;
            uvMax = Vector2.zero;

            int[] sourceIndices = mesh.GetTriangles(subMesh);
            if (sourceIndices == null || sourceIndices.Length == 0) return false;

            Vector3[] allPositions = mesh.vertices;
            Vector3[] allNormals = mesh.normals;
            Vector2[] allUv = mesh.uv;
            if (allPositions == null || allPositions.Length == 0) return false;

            var remap = new Dictionary<int, int>(sourceIndices.Length);
            var newPositions = new List<Vector3>(sourceIndices.Length);
            var newNormals = new List<Vector3>(sourceIndices.Length);
            var newUv = new List<Vector2>(sourceIndices.Length);
            var newIndices = new int[sourceIndices.Length];

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < sourceIndices.Length; i++)
            {
                int oldIndex = sourceIndices[i];
                if (!remap.TryGetValue(oldIndex, out int newIndex))
                {
                    newIndex = newPositions.Count;
                    remap[oldIndex] = newIndex;

                    newPositions.Add(toPrefabRoot.MultiplyPoint3x4(allPositions[oldIndex]));
                    newNormals.Add((allNormals != null && allNormals.Length == allPositions.Length
                        ? toPrefabRoot.MultiplyVector(allNormals[oldIndex])
                        : Vector3.up).normalized);

                    Vector2 sourceUv = allUv != null && allUv.Length == allPositions.Length
                        ? allUv[oldIndex]
                        : Vector2.zero;
                    newUv.Add(sourceUv);

                    min = Vector2.Min(min, sourceUv);
                    max = Vector2.Max(max, sourceUv);
                }

                newIndices[i] = newIndex;
            }

            positions = newPositions.ToArray();
            normals = newNormals.ToArray();
            uv = newUv.ToArray();
            indices = newIndices;
            uvMin = min;
            uvMax = max;

            // Ô atlas đã nằm trong asset; mesh builder mới là chỗ áp dụng nó. Ở đây chỉ kiểm tra
            // rằng UV nguồn thật sự nằm trong [0,1], vì nếu nó tile thì phép ánh xạ vào ô sẽ sai.
            return atlasRect.width > 0f;
        }
    }
}
