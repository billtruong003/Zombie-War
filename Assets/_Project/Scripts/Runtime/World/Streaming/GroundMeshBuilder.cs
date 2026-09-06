using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Dựng và nạp lại mesh nền phẳng cho một chunk.
    ///
    /// Hình học (vị trí, normal, UV, chỉ số) giống hệt nhau ở mọi chunk vì toạ độ đỉnh là toạ độ
    /// cục bộ trong chunk. Chỉ có vertex color — bốn trọng số biome — thay đổi theo toạ độ logic.
    /// Nhờ vậy mỗi slot pool giữ đúng MỘT <see cref="Mesh"/> suốt đời và mỗi lần tái sử dụng chỉ ghi
    /// lại mảng màu, không cấp phát mesh mới.
    ///
    /// Vị trí lấy mẫu logic được tính bằng SỐ NGUYÊN trước rồi mới nhân bước:
    /// <c>(chunkAxis × cells + index) × step</c>. Đây là điểm mấu chốt chống đường nối — biên phải của
    /// chunk `x` và biên trái của chunk `x+1` cho ra cùng một chỉ số nguyên, nên cùng một giá trị float
    /// từng bit, chứ không phải "gần bằng nhau".
    /// </summary>
    public static class GroundMeshBuilder
    {
        public const int DefaultResolution = 17;

        /// <summary>Tổng số Mesh nền đã tạo trong cả phiên chạy. Sau warmup phải đứng yên.</summary>
        public static int MeshesCreated { get; private set; }

        private static Color[] _colorBuffer;

        public static int VertexCount(int resolution) => resolution * resolution;

        public static int TriangleCount(int resolution)
        {
            int cells = resolution - 1;
            return cells * cells * 2;
        }

        /// <summary>Toạ độ logic toàn cục của một đỉnh trên một trục.</summary>
        public static float VertexWorldAxis(int chunkAxis, int index, int resolution, float chunkSize)
        {
            int cells = resolution - 1;
            return (chunkAxis * cells + index) * (chunkSize / cells);
        }

        /// <summary>Mẫu biome tại một đỉnh — đường dùng chung cho mesh builder và test đường nối.</summary>
        public static BiomeSample SampleVertex(int worldSeed, ChunkCoord coord, int ix, int iz, int resolution, float chunkSize)
        {
            float worldX = VertexWorldAxis(coord.X, ix, resolution, chunkSize);
            float worldZ = VertexWorldAxis(coord.Z, iz, resolution, chunkSize);
            return BiomeSampler.Sample(worldSeed, worldX, worldZ);
        }

        /// <summary>
        /// Tạo một Mesh nền mới với hình học cố định. Gọi đúng một lần cho mỗi slot pool, lúc prewarm.
        /// </summary>
        public static Mesh CreateGroundMesh(string name, int resolution, float chunkSize)
        {
            if (resolution < 2)
                throw new System.ArgumentOutOfRangeException(nameof(resolution), resolution, "resolution phải >= 2.");
            if (chunkSize <= 0f)
                throw new System.ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "chunkSize phải > 0.");

            int cells = resolution - 1;
            float step = chunkSize / cells;
            int vertexCount = VertexCount(resolution);

            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var colors = new Color[vertexCount];

            for (int iz = 0; iz < resolution; iz++)
            {
                for (int ix = 0; ix < resolution; ix++)
                {
                    int v = iz * resolution + ix;
                    positions[v] = new Vector3(ix * step, 0f, iz * step);
                    normals[v] = Vector3.up;
                    uv[v] = new Vector2((float)ix / cells, (float)iz / cells);
                    colors[v] = Color.clear;
                }
            }

            var triangles = new int[TriangleCount(resolution) * 3];
            int t = 0;
            for (int iz = 0; iz < cells; iz++)
            {
                for (int ix = 0; ix < cells; ix++)
                {
                    int v = iz * resolution + ix;
                    triangles[t++] = v;
                    triangles[t++] = v + resolution;
                    triangles[t++] = v + resolution + 1;
                    triangles[t++] = v;
                    triangles[t++] = v + resolution + 1;
                    triangles[t++] = v + 1;
                }
            }

            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertexCount > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.MarkDynamic();   // chỉ vertex color đổi, và nó đổi mỗi lần recycle
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();

            MeshesCreated++;
            return mesh;
        }

        /// <summary>
        /// Ghi lại trọng số biome của một toạ độ logic vào Mesh đã có. Không cấp phát Mesh,
        /// không đụng vào hình học.
        /// </summary>
        public static void ApplyBiome(Mesh mesh, ChunkCoord coord, int resolution, float chunkSize, int worldSeed)
        {
            if (mesh == null) throw new System.ArgumentNullException(nameof(mesh));

            int vertexCount = VertexCount(resolution);
            if (_colorBuffer == null || _colorBuffer.Length != vertexCount) _colorBuffer = new Color[vertexCount];

            int cells = resolution - 1;
            float step = chunkSize / cells;
            int baseX = coord.X * cells;
            int baseZ = coord.Z * cells;

            for (int iz = 0; iz < resolution; iz++)
            {
                float worldZ = (baseZ + iz) * step;
                int row = iz * resolution;

                for (int ix = 0; ix < resolution; ix++)
                {
                    float worldX = (baseX + ix) * step;
                    _colorBuffer[row + ix] = BiomeSampler.Sample(worldSeed, worldX, worldZ).ToColor();
                }
            }

            mesh.colors = _colorBuffer;
        }

        /// <summary>Chỉ dùng cho test: đặt lại bộ đếm để một fixture đo được delta của riêng nó.</summary>
        public static void ResetMeshCounterForTests() => MeshesCreated = 0;
    }
}
