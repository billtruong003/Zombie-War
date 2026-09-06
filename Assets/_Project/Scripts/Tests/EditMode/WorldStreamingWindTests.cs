using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode M4.6C.3: hợp đồng dữ liệu gió trong mesh gộp.
    ///
    /// Gió chạy hoàn toàn trên GPU, nên phần duy nhất kiểm được bằng test là DỮ LIỆU mà mesh mang
    /// sang shader: độ cứng gốc→ngọn, một pha cho mỗi khóm, biên độ theo loại. Chuyển động thật sự
    /// phải chứng minh bằng ảnh chụp — test này chốt phần khiến ảnh đó có thể tin được.
    public class WorldStreamingWindTests
    {
        private const int Seed = 20268728;
        private const float ChunkSize = 32f;
        private const string PalettePath = "Assets/_Project/Data/World/Decoration/DecorationPalette.asset";

        private readonly List<Mesh> _meshes = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            foreach (Mesh m in _meshes) if (m != null) Object.DestroyImmediate(m);
            _meshes.Clear();
        }

        private static DecorationPalette Palette()
        {
            var p = AssetDatabase.LoadAssetAtPath<DecorationPalette>(PalettePath);
            Assert.IsNotNull(p, "Chưa bake palette production.");
            return p;
        }

        private Mesh BuildFoliage(ChunkCoord coord, float density = 1f)
        {
            var results = new List<DecorationPlacement>(512);
            DecorationSampleStats stats = default;
            DecorationSampler.Generate(Seed, coord, ChunkSize, Palette(), results, density, ref stats);
            DecorationMeshBuilder.Build(Palette(), results);

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
            DecorationMeshBuilder.Foliage.Apply(mesh, 0.5f);
            _meshes.Add(mesh);
            return mesh;
        }

        private static List<Vector2> Uv1(Mesh mesh)
        {
            var uv1 = new List<Vector2>();
            mesh.GetUVs(1, uv1);
            return uv1;
        }

        /// <summary>Gom chỉ số đỉnh theo pha — mỗi nhóm là một khóm cây.</summary>
        private static Dictionary<float, List<int>> ByTuft(List<Vector2> uv1)
        {
            var map = new Dictionary<float, List<int>>();
            for (int i = 0; i < uv1.Count; i++)
            {
                if (!map.TryGetValue(uv1[i].x, out List<int> list))
                {
                    list = new List<int>();
                    map[uv1[i].x] = list;
                }
                list.Add(i);
            }
            return map;
        }

        [Test]
        public void Uv1_CountMatchesVertexCount_AndSingleSubmeshUInt16()
        {
            Mesh mesh = BuildFoliage(new ChunkCoord(60, 33));
            Assert.AreEqual(mesh.vertexCount, Uv1(mesh).Count, "UV1 thiếu so với số đỉnh.");
            Assert.AreEqual(1, mesh.subMeshCount, "Gió không được thêm submesh.");
            Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16, mesh.indexFormat,
                "Chỉ số 16-bit phải giữ nguyên.");
        }

        [Test]
        public void Stiffness_RootIsLowerThanTip_ForEveryTuft()
        {
            Mesh mesh = BuildFoliage(new ChunkCoord(60, 33));
            Vector3[] pos = mesh.vertices;
            Color32[] cols = mesh.colors32;

            int checkedTufts = 0;
            foreach (KeyValuePair<float, List<int>> tuft in ByTuft(Uv1(mesh)))
            {
                List<int> idx = tuft.Value;
                if (idx.Count < 4) continue;

                int lo = idx[0], hi = idx[0];
                foreach (int i in idx)
                {
                    if (pos[i].y < pos[lo].y) lo = i;
                    if (pos[i].y > pos[hi].y) hi = i;
                }
                // Khóm quá dẹt thì gốc và ngọn gần như trùng nhau — không có gì để so.
                if (pos[hi].y - pos[lo].y < 0.05f) continue;

                checkedTufts++;
                Assert.Less(cols[lo].a, cols[hi].a,
                    $"Khóm pha {tuft.Key:F4}: gốc cứng {cols[lo].a} không thấp hơn ngọn {cols[hi].a}.");
            }

            Assert.Greater(checkedTufts, 10, "Không đủ khóm để kết luận.");
        }

        [Test]
        public void Stiffness_StaysWithinByteRange()
        {
            Color32[] cols = BuildFoliage(new ChunkCoord(60, 33)).colors32;
            int min = 255, max = 0;
            foreach (Color32 c in cols) { min = Mathf.Min(min, c.a); max = Mathf.Max(max, c.a); }
            Assert.GreaterOrEqual(min, 0);
            Assert.LessOrEqual(max, 255);
            Assert.Less(min, max, "Độ cứng không biến thiên — mọi đỉnh cùng một giá trị.");
        }

        [Test]
        public void EachTuft_HasExactlyOnePhaseAndOneAmplitude()
        {
            Mesh mesh = BuildFoliage(new ChunkCoord(60, 33));
            List<Vector2> uv1 = Uv1(mesh);

            foreach (KeyValuePair<float, List<int>> tuft in ByTuft(uv1))
            {
                float amplitude = uv1[tuft.Value[0]].y;
                foreach (int i in tuft.Value)
                {
                    Assert.AreEqual(tuft.Key, uv1[i].x, 0f,
                        "Hai đỉnh trong cùng một khóm mang pha khác nhau — khóm sẽ tự xé rách.");
                    Assert.AreEqual(amplitude, uv1[i].y, 0f,
                        "Biên độ không đồng nhất trong một khóm.");
                }
            }
        }

        [Test]
        public void SeparatePlacements_ReceiveVariedPhases()
        {
            Mesh mesh = BuildFoliage(new ChunkCoord(60, 33));
            var phases = new HashSet<float>();
            foreach (Vector2 u in Uv1(mesh)) phases.Add(u.x);

            Assert.Greater(phases.Count, 20,
                "Quá ít pha khác nhau — cả cánh đồng sẽ đu đưa đồng loạt như một tấm.");

            float min = 1f, max = 0f;
            foreach (float p in phases) { min = Mathf.Min(min, p); max = Mathf.Max(max, p); }
            Assert.Less(min, 0.2f, "Pha không trải xuống vùng thấp.");
            Assert.Greater(max, 0.8f, "Pha không trải lên vùng cao.");
        }

        [Test]
        public void WindMetadata_IsDeterministicAcrossRebuildAndRecycle()
        {
            Mesh first = BuildFoliage(new ChunkCoord(60, 33));
            List<Vector2> uvA = Uv1(first);
            Color32[] colA = first.colors32;

            // Dựng một chunk khác xen giữa: mô phỏng đúng việc slot pool bị tái sử dụng.
            BuildFoliage(new ChunkCoord(-76, -50));

            Mesh again = BuildFoliage(new ChunkCoord(60, 33));
            List<Vector2> uvB = Uv1(again);
            Color32[] colB = again.colors32;

            Assert.AreEqual(uvA.Count, uvB.Count, "Số đỉnh đổi sau khi recycle.");
            for (int i = 0; i < uvA.Count; i++)
            {
                Assert.AreEqual(uvA[i], uvB[i], $"UV1 đỉnh {i} đổi sau recycle — pha gió sẽ nhảy.");
                Assert.AreEqual(colA[i].a, colB[i].a, $"Độ cứng đỉnh {i} đổi sau recycle.");
            }
        }

        [Test]
        public void NegativeCoordinates_ProduceValidWindMetadata()
        {
            Mesh mesh = BuildFoliage(new ChunkCoord(-76, -50));
            List<Vector2> uv1 = Uv1(mesh);
            Assert.Greater(uv1.Count, 0, "Toạ độ âm không sinh ra cây cỏ nào.");

            foreach (Vector2 u in uv1)
            {
                Assert.IsFalse(float.IsNaN(u.x) || float.IsNaN(u.y), "UV1 chứa NaN ở toạ độ âm.");
                Assert.GreaterOrEqual(u.x, 0f);
                Assert.LessOrEqual(u.x, 1f);
            }
        }

        [Test]
        public void FarTier_KeepsTheSamePhasesAsNearTier()
        {
            Mesh near = BuildFoliage(new ChunkCoord(60, 33), 1f);
            var nearPhases = new HashSet<float>();
            foreach (Vector2 u in Uv1(near)) nearPhases.Add(u.x);

            Mesh far = BuildFoliage(new ChunkCoord(60, 33), 0.4f);
            foreach (Vector2 u in Uv1(far))
                Assert.IsTrue(nearPhases.Contains(u.x),
                    "Bậc vòng ngoài sinh ra pha mới — cây giữ lại sẽ đổi nhịp khi đổi bậc mật độ.");
        }

        [Test]
        public void SolidGeometry_ReceivesZeroWindAmplitude()
        {
            // Build() đổ cả hai output; lấy Solid ngay sau đó.
            BuildFoliage(new ChunkCoord(-76, -50));

            var solid = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
            _meshes.Add(solid);
            if (!DecorationMeshBuilder.Solid.Apply(solid, 0.5f)) Assert.Pass("Chunk này không có hình học Solid.");

            foreach (Vector2 u in Uv1(solid))
                Assert.AreEqual(0f, u.y, 0f, "Thân/gốc cứng lại nhận biên độ gió — trụ cây sẽ đu đưa.");
        }

        [Test]
        public void FoliageMaterial_ExposesWindContract_AndStaysAlphaClipped()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/M_WorldStreamingFoliage.mat");
            Assert.IsNotNull(mat);

            foreach (string p in new[] { "_WindStrength", "_WindFrequency", "_WindSpeed",
                                         "_GustStrength", "_GustScale", "_WorldOriginOffset" })
                Assert.IsTrue(mat.HasProperty(p), $"Vật liệu cây cỏ thiếu tham số gió '{p}'.");

            Assert.IsTrue(mat.HasProperty("_Cutoff"), "Mất ngưỡng alpha clip.");
            Assert.AreEqual("ZombieWar/World Streaming/Foliage", mat.shader.name,
                "Cây cỏ production phải dùng shader của project, không phải shader vendor.");
            Assert.AreEqual(1, mat.shader.passCount, "Gió không được thêm pass mới.");
        }
    }
}
