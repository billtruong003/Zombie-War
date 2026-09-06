using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Bộ tích luỹ hình học gộp cho MỘT output của MỘT chunk.
    ///
    /// Dùng lại danh sách giữa các lần recycle: sau khi warmup xong, đường này không cấp phát gì nữa.
    /// </summary>
    public class DecorationAccumulator
    {
        private readonly List<Vector3> _positions = new List<Vector3>(4096);
        private readonly List<Vector3> _normals = new List<Vector3>(4096);
        private readonly List<Vector2> _uv = new List<Vector2>(4096);
        private readonly List<Color32> _colors = new List<Color32>(4096);
        // UV1 mang dữ liệu gió: x = pha của từng khóm, y = biên độ theo loại (M4.6C.3).
        private readonly List<Vector2> _uv1 = new List<Vector2>(4096);
        private readonly List<int> _indices = new List<int>(8192);

        private Vector3 _boundsMin;
        private Vector3 _boundsMax;
        private bool _hasBounds;

        public int VertexCount => _positions.Count;
        public int TriangleCount => _indices.Count / 3;
        public bool IsEmpty => _positions.Count == 0;

        public void Clear()
        {
            _positions.Clear();
            _normals.Clear();
            _uv.Clear();
            _colors.Clear();
            _uv1.Clear();
            _indices.Clear();
            _hasBounds = false;
        }

        /// <summary>
        /// Nối một submesh nguồn đã biến đổi vào bộ tích luỹ.
        ///
        /// Chỉ dùng scale ĐỀU: normal khi đó chỉ cần xoay, không cần ma trận nghịch đảo chuyển vị,
        /// và winding tam giác cũng không bị lật.
        /// </summary>
        public void Append(DecorationMeshSource source, Vector3 localPosition, float yawDegrees, float scale, float tint)
            => Append(source, localPosition, yawDegrees, scale, tint, Color.white);

        /// <summary>
        /// Gộp một nguồn vào bộ tích luỹ, nhân thêm hệ số màu riêng của entry.
        ///
        /// Quá tải không có <paramref name="entryTint"/> truyền vào màu trắng, nên mọi lời gọi cũ —
        /// kể cả trong test — cho ra đúng byte như trước.
        /// </summary>
        public void Append(DecorationMeshSource source, Vector3 localPosition, float yawDegrees, float scale,
            float tint, Color entryTint)
            => Append(source, localPosition, yawDegrees, scale, tint, entryTint, 0f, 0f);

        /// <summary>
        /// Nối một nguồn kèm dữ liệu gió của khóm đó.
        ///
        /// <paramref name="phase"/> phải GIỐNG NHAU cho mọi đỉnh của cùng một khóm. Nếu lấy pha theo
        /// từng đỉnh, mỗi đỉnh sẽ lệch pha một chút và bụi cỏ tự xé rách chính nó thay vì nghiêng
        /// theo gió.
        /// </summary>
        public void Append(DecorationMeshSource source, Vector3 localPosition, float yawDegrees, float scale,
            float tint, Color entryTint, float phase, float windAmplitude)
        {
            if (source == null) return;

            Vector3[] sourcePositions = source.Positions;
            Vector3[] sourceNormals = source.Normals;
            Vector2[] sourceUv = source.Uv;
            int[] sourceIndices = source.Indices;
            Rect rect = source.AtlasRect;
            bool tilesUv = source.TilesUv;

            int vertexOffset = _positions.Count;

            float radians = yawDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);

            // Vertex color chỉ có 8 bit mỗi kênh và tint có thể vượt 1.0, nên lưu ở thang một nửa:
            // tint 1.0 → 127. Shader nhân lại với 2. Hằng số này phải khớp với _TintScale trong shader.
            //
            // Ba kênh RGB mang `độ sáng tất định × hệ số màu của entry`. Trước M4.6C cả ba kênh bằng
            // nhau (chỉ có độ sáng); nhân với trắng cho lại đúng ba byte đó, nên dữ liệu cũ không đổi.
            // Alpha nay mang ĐỘ CỨNG gốc→ngọn cho gió (M4.6C.3), không còn là hằng số 255.
            // Độ cứng gốc→ngọn lấy từ chiều cao TRONG KHÔNG GIAN NGUỒN, trước khi xoay/scale.
            // Lấy sau khi biến đổi thì một khóm bị xoay sẽ có "gốc" nằm sai chỗ.
            Bounds sourceBounds = source.LocalBounds;
            float minY = sourceBounds.min.y;
            float heightRange = sourceBounds.size.y;
            // Mesh dẹt (bounds cao 0) chia cho 0 ra NaN và làm hỏng cả mesh gộp — kẹp về cứng hoàn toàn.
            bool hasHeight = heightRange > 1e-4f;
            var windUv = new Vector2(phase, windAmplitude);

            float scaled = tint * 127.5f;
            var color = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(scaled * entryTint.r), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(scaled * entryTint.g), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(scaled * entryTint.b), 0, 255),
                255);

            for (int i = 0; i < sourcePositions.Length; i++)
            {
                Vector3 p = sourcePositions[i];
                p *= scale;

                float rotatedX = p.x * cos + p.z * sin;
                float rotatedZ = -p.x * sin + p.z * cos;
                var world = new Vector3(localPosition.x + rotatedX, localPosition.y + p.y, localPosition.z + rotatedZ);
                _positions.Add(world);

                Vector3 n = sourceNormals[i];
                _normals.Add(new Vector3(n.x * cos + n.z * sin, n.y, -n.x * sin + n.z * cos));

                // Nguồn dùng chung atlas: ép UV về [0,1] rồi ánh xạ vào ô của nó, để không đỉnh nào
                // lấy mẫu lấn sang ô hàng xóm. Nguồn có UV lặp thì đi thẳng — nó chiếm trọn atlas
                // riêng (validate đã bắt buộc như vậy) nên wrap Repeat làm đúng việc của nó.
                // Kẹp [0,1] viết thẳng tại chỗ thay vì gọi `Mathf.Clamp01`. Giá trị ra giống hệt; khác
                // biệt là JIT của Editor không nội tuyến `Mathf.Clamp01`, mà ba lời gọi này chạy trên
                // MỌI đỉnh của mọi chunk.
                Vector2 uv = sourceUv[i];
                if (tilesUv)
                {
                    _uv.Add(uv);
                }
                else
                {
                    float cu = uv.x < 0f ? 0f : (uv.x > 1f ? 1f : uv.x);
                    float cv = uv.y < 0f ? 0f : (uv.y > 1f ? 1f : uv.y);
                    _uv.Add(new Vector2(rect.x + cu * rect.width, rect.y + cv * rect.height));
                }

                float stiffness = 0f;
                if (hasHeight)
                {
                    stiffness = (sourcePositions[i].y - minY) / heightRange;
                    if (stiffness < 0f) stiffness = 0f;
                    else if (stiffness > 1f) stiffness = 1f;
                }
                var vertexColor = color;
                vertexColor.a = (byte)Mathf.Clamp(Mathf.RoundToInt(stiffness * 255f), 0, 255);
                _colors.Add(vertexColor);
                _uv1.Add(windUv);
                Encapsulate(world);
            }

            for (int i = 0; i < sourceIndices.Length; i++)
                _indices.Add(vertexOffset + sourceIndices[i]);
        }

        /// <summary>
        /// Nới bounds cho một đỉnh.
        ///
        /// So sánh theo TỪNG THÀNH PHẦN thay vì gọi <c>Vector3.Min/Max</c>. Kết quả giống hệt từng bit,
        /// nhưng hai lời gọi kia là hàm tĩnh mỗi cái gọi tiếp ba <c>Mathf.Min</c>, và JIT của Editor
        /// không nội tuyến chúng — trên một chunk 53.000 đỉnh (khổ của M4.6CD.1) chỗ này nằm thẳng
        /// trong vòng lặp nóng nhất của cả hệ thống.
        /// </summary>
        private void Encapsulate(Vector3 point)
        {
            if (!_hasBounds)
            {
                _boundsMin = point;
                _boundsMax = point;
                _hasBounds = true;
                return;
            }

            if (point.x < _boundsMin.x) _boundsMin.x = point.x;
            else if (point.x > _boundsMax.x) _boundsMax.x = point.x;
            if (point.y < _boundsMin.y) _boundsMin.y = point.y;
            else if (point.y > _boundsMax.y) _boundsMax.y = point.y;
            if (point.z < _boundsMin.z) _boundsMin.z = point.z;
            else if (point.z > _boundsMax.z) _boundsMax.z = point.z;
        }

        /// <summary>
        /// Đổ dữ liệu vào một Mesh dùng lại. Trả về false khi rỗng, để phía gọi tắt renderer thay vì
        /// huỷ object.
        /// </summary>
        public bool Apply(Mesh mesh, float boundsPadding)
        {
            if (mesh == null) return false;

            mesh.Clear(false);
            if (_positions.Count == 0)
            {
                mesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
                return false;
            }

            mesh.SetVertices(_positions);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uv);
            mesh.SetUVs(1, _uv1);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_indices, 0, false);
            mesh.subMeshCount = 1;

            Vector3 center = (_boundsMin + _boundsMax) * 0.5f;
            Vector3 size = _boundsMax - _boundsMin + Vector3.one * (boundsPadding * 2f);
            mesh.bounds = new Bounds(center, size);
            return true;
        }
    }

    /// <summary>
    /// Biến danh sách placement thành hai mesh gộp: Solid và Foliage.
    ///
    /// Một placement có thể góp hình học cho cả hai output (thân cây và tán lá) nhưng vẫn là MỘT
    /// quyết định đặt duy nhất — quyền sở hữu theo chunk vì thế không bao giờ nhập nhằng.
    ///
    /// Hai bộ tích luỹ là static và dùng chung: việc sinh chạy đồng bộ, dựng xong là apply ngay,
    /// nên không cần một bộ đệm riêng cho từng slot pool.
    /// </summary>
    public static class DecorationMeshBuilder
    {
        private static readonly DecorationAccumulator SolidAccumulator = new DecorationAccumulator();
        private static readonly DecorationAccumulator FoliageAccumulator = new DecorationAccumulator();

        public static DecorationAccumulator Solid => SolidAccumulator;
        public static DecorationAccumulator Foliage => FoliageAccumulator;

        /// <summary>Tổng số Mesh trang trí đã tạo trong cả phiên. Sau warmup phải đứng yên.</summary>
        public static int MeshesCreated { get; private set; }

        public static Mesh CreateDecorationMesh(string name)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.MarkDynamic();

            MeshesCreated++;
            return mesh;
        }

        public static void ResetMeshCounterForTests() => MeshesCreated = 0;

        /// <summary>
        /// Dựng cả hai output từ danh sách placement. Phía gọi tự apply hai bộ tích luỹ vào mesh
        /// của slot mình.
        /// </summary>
        public static void Build(DecorationPalette palette, IReadOnlyList<DecorationPlacement> placements)
        {
            SolidAccumulator.Clear();
            FoliageAccumulator.Clear();

            if (palette == null || placements == null) return;

            IReadOnlyList<DecorationPaletteEntry> entries = palette.Entries;

            for (int i = 0; i < placements.Count; i++)
            {
                DecorationPlacement placement = placements[i];
                if (placement.EntryIndex < 0 || placement.EntryIndex >= entries.Count) continue;

                DecorationPaletteEntry entry = entries[placement.EntryIndex];

                // Pha gió của KHÓM: bám vào danh tính toàn cục của placement (muối entry + ô lưới +
                // chỉ số ứng viên), đúng những trường đã quyết định vị trí của nó. Nhờ vậy pha sống
                // sót qua recycle chunk, qua đổi bậc gần/xa, qua thứ tự sinh đảo ngược và qua toạ độ
                // âm — vì không thứ nào trong đó là đầu vào. Lấy pha theo toạ độ chunk-local hay theo
                // slot pool thì cùng một bụi cỏ sẽ đổi pha mỗi lần người chơi đi vòng lại.
                float phase = DecorationHash.Unit(WindPhaseSeed, placement.CellX, placement.CellZ,
                    placement.EntrySalt, placement.CandidateIndex, HashLane.Rotation);

                AppendSource(entry.PrimarySource, placement, entry.FoliageTint, phase, entry.WindAmplitude);
                AppendSource(entry.SecondarySource, placement, entry.FoliageTint, phase, entry.WindAmplitude);
            }
        }

        /// <summary>Muối cố định cho làn pha gió. Đổi số này là đổi pha của cả thế giới.</summary>
        private const int WindPhaseSeed = 0x57494E44;   // 'WIND'

        private static void AppendSource(DecorationMeshSource source, in DecorationPlacement placement,
            Color entryTint, float phase, float windAmplitude)
        {
            if (source == null) return;

            DecorationAccumulator target = source.Category == DecorationCategory.Solid
                ? SolidAccumulator
                : FoliageAccumulator;

            // Thân/gốc cứng (Solid) không đu đưa: chỉ tán lá (Foliage) nhận biên độ gió.
            float amplitude = source.Category == DecorationCategory.Foliage ? windAmplitude : 0f;
            target.Append(source, placement.LocalPosition, placement.Yaw, placement.Scale, placement.Tint,
                entryTint, phase, amplitude);
        }
    }
}
