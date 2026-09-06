using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Editor
{
    /// <summary>
    /// Dựng bảng trang trí "phương án B" cho phép so sánh nghệ thuật M4.5.
    ///
    /// Phương án B là hướng THUẦN HÌNH HỌC: không card alpha, không mesh vendor nhiều chi tiết, mọi
    /// thứ là khối thấp mặt phẳng cùng ngôn ngữ với nhân vật và zombie.
    ///
    /// Bảng này là một asset RIÊNG. Production vẫn chạy bảng A cho tới khi người dùng chọn hướng —
    /// milestone này chỉ dựng bằng chứng, không triển khai.
    ///
    /// Điểm khác nhau thật sự giữa hai phương án, sau khi soi lại nguồn hiện có:
    ///
    /// | nguồn      | A (hiện tại)                    | B (thuần hình học)              |
    /// |------------|----------------------------------|----------------------------------|
    /// | grass_b/c  | 56 đỉnh, đã thuần hình học       | GIỮ NGUYÊN                       |
    /// | fern_d     | 36 đỉnh, đã thuần hình học       | GIỮ NGUYÊN                       |
    /// | cattail_b  | 245 đỉnh mesh vendor             | bụi cỏ cao sinh sẵn              |
    /// | flowers_g  | 384 đỉnh mesh vendor             | bụi hoa nhỏ sinh sẵn             |
    /// | tán tree_a | 1.840 đỉnh CARD ALPHA            | khối đa diện đặc                 |
    ///
    /// Nói cách khác M3B.1 đã đưa phần lớn thảm cỏ về hình học rồi; phần còn tranh cãi chỉ là ba nguồn
    /// trên, và tán cây alpha là phần nặng nhất trong đó.
    /// </summary>
    public static class CandidateBPaletteBuilder
    {
        public const string PalettePath = "Assets/_Project/Data/World/Decoration/DecorationPalette_CandidateB.asset";
        private const string SourceFolder = "Assets/_Project/Data/World/Decoration";

        [MenuItem("ZombieWar/World Streaming/Build Candidate B Palette (art A/B)", priority = 130)]
        public static void BuildMenu()
        {
            string report = Build();
            Debug.Log("[M4.5] " + report);
            EditorUtility.DisplayDialog("Candidate B palette", report, "OK");
        }

        public static string Build()
        {
            var sb = new StringBuilder();

            // Ô atlas 0 = bảng màu phẳng dùng chung. Lấy đúng dải màu mà nguồn A đang lấy, nên phép so
            // sánh khác nhau ở HÌNH HỌC chứ không ở màu sắc.
            DecorationMeshSource grassRef = Load("grass_b");
            DecorationMeshSource cattailRef = Load("cattail_b");
            DecorationMeshSource flowersRef = Load("flowers_g");
            DecorationMeshSource leafRef = Load("tree_a_leaf");
            if (grassRef == null || cattailRef == null || flowersRef == null || leafRef == null)
                return "THIẾU nguồn phương án A — chạy Rebuild Decoration Assets trước.";

            sb.Append(Bake("b_cattail", cattailRef, GeneratedKind.Tuft, 9, 0.10f, 0.95f)).Append('\n');
            sb.Append(Bake("b_flowers", flowersRef, GeneratedKind.Tuft, 7, 0.12f, 0.55f)).Append('\n');
            // Tan cay B lay o atlas 0 (bang mau phang) chu KHONG phai o 1 (`Leaf_2`).
            // Neu muon o cua la, alpha clip se duc thung khoi hinh hoc dac - dung thu ma phuong an B
            // ton tai de tranh. Kich thuoc van lay tu tan cay A de hai ben cung khoi.
            DecorationMeshSource fernRef = Load("fern_d");
            sb.Append(Bake("b_canopy", fernRef, GeneratedKind.Canopy, 0, 0f, 0f, leafRef)).Append('\n');

            // Bảng B: cùng id, cùng mật độ, cùng ô lưới, cùng ái lực biome như bảng A. Chỉ nguồn hình
            // học đổi — nếu mật độ cũng đổi thì phép so sánh mất nghĩa.
            var palette = AssetDatabase.LoadAssetAtPath<DecorationPalette>(PalettePath);
            bool created = palette == null;
            if (created) palette = ScriptableObject.CreateInstance<DecorationPalette>();

            var entries = new System.Collections.Generic.List<DecorationPaletteEntry>();

            void Add(string id, string primary, string secondary, float cell, float density, float spacing,
                Vector4 affinity, Vector2 scale, int priority, bool eligible)
            {
                var e = new DecorationPaletteEntry();
                e.Configure(id, Load(primary), secondary != null ? Load(secondary) : null,
                    cell, 1, density, spacing, affinity, scale, new Vector2(0.82f, 1.12f), priority,
                    yaw: true, densityEligible: eligible);
                entries.Add(e);
            }

            Add("grass_b",   "grass_b",   null,           2.6f, 0.46f, 0f,   new Vector4(0.25f, 1.00f, 0.05f, 0.05f), new Vector2(0.80f, 1.40f), 20, true);
            Add("grass_c",   "grass_c",   null,           2.9f, 0.44f, 0f,   new Vector4(0.35f, 0.90f, 0.15f, 0.05f), new Vector2(0.80f, 1.35f), 20, true);
            Add("cattail_b", "b_cattail", null,           4.5f, 0.34f, 0f,   new Vector4(0.10f, 1.00f, 0.00f, 0.00f), new Vector2(0.75f, 1.20f), 40, true);
            Add("flowers_g", "b_flowers", null,           5.5f, 0.32f, 0f,   new Vector4(0.15f, 1.00f, 0.05f, 0.00f), new Vector2(0.80f, 1.20f), 50, true);
            Add("fern_d",    "fern_d",    null,           7.0f, 0.32f, 4.0f, new Vector4(0.05f, 1.00f, 0.00f, 0.10f), new Vector2(0.70f, 1.10f), 60, true);
            Add("tree_a",    "b_canopy",  "tree_a_trunk", 16f,  0.60f, 18f,  new Vector4(0.30f, 1.00f, 0.05f, 0.15f), new Vector2(0.85f, 1.25f), 90, false);

            palette.SetEntriesForTests(entries);
            if (created) AssetDatabase.CreateAsset(palette, PalettePath);
            else EditorUtility.SetDirty(palette);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.Append("palette valid=").Append(palette.Validate(out string err)).Append(' ').Append(err);
            return sb.ToString();
        }

        private enum GeneratedKind { Tuft, Canopy }

        private static DecorationMeshSource Load(string id) =>
            AssetDatabase.LoadAssetAtPath<DecorationMeshSource>($"{SourceFolder}/DMS_{id}.asset");

        /// <summary>Sinh một nguồn B, mượn ô atlas và dải màu của nguồn A tương ứng.</summary>
        private static string Bake(string id, DecorationMeshSource reference, GeneratedKind kind,
            int blades, float bladeWidth, float height, DecorationMeshSource sizeReference = null)
        {
            // Dải màu: lấy UV của đỉnh thấp nhất và cao nhất của nguồn A, đúng cách M3B.1 làm.
            Vector2 uvLow = reference.Uv[0], uvHigh = reference.Uv[0];
            float lowY = float.MaxValue, highY = float.MinValue;
            for (int i = 0; i < reference.Positions.Length; i++)
            {
                float y = reference.Positions[i].y;
                if (y < lowY) { lowY = y; uvLow = reference.Uv[i]; }
                if (y > highY) { highY = y; uvHigh = reference.Uv[i]; }
            }

            int seed = DecorationHash.SaltFromId(id);
            Vector3 size = (sizeReference != null ? sizeReference : reference).LocalBounds.size;

            Vector3[] pos; Vector3[] nrm; Vector2[] uv; int[] idx;
            if (kind == GeneratedKind.Tuft)
            {
                DecorationLowPolySources.BuildGrassTuft(seed, blades,
                    Mathf.Max(0.12f, Mathf.Max(size.x, size.z) * 0.45f),
                    Mathf.Max(0.2f, height > 0f ? height : size.y),
                    bladeWidth, uvLow, uvHigh, out pos, out nrm, out uv, out idx);
            }
            else
            {
                DecorationLowPolySources.BuildLowPolyCanopy(seed, rings: 3, segments: 7,
                    radius: Mathf.Max(size.x, size.z) * 0.42f,
                    height: size.y * 0.72f,
                    irregularity: 0.22f,
                    uvBase: uvLow, uvTip: uvHigh, out pos, out nrm, out uv, out idx);
            }

            string path = $"{SourceFolder}/DMS_{id}.asset";
            var source = AssetDatabase.LoadAssetAtPath<DecorationMeshSource>(path);
            bool created = source == null;
            if (created) source = ScriptableObject.CreateInstance<DecorationMeshSource>();

            source.Bake(id, path, 0, DecorationCategory.Foliage, reference.AtlasRect, pos, nrm, uv, idx);
            if (created) AssetDatabase.CreateAsset(source, path);
            else EditorUtility.SetDirty(source);

            return $"{id,-12} v={pos.Length,5} tri={idx.Length / 3,5}  (A ref {reference.name} v={reference.VertexCount})";
        }
    }
}
