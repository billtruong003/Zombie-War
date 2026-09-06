using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar.WorldStreaming;

namespace ZombieWar.Tests
{
    /// EditMode M2A: asset shader/material của mặt đất phải thật sự được nối đủ.
    ///
    /// Đây là loại lỗi im lặng nhất của một milestone hình ảnh: shader biên dịch xong, test logic xanh,
    /// nhưng một slot texture bỏ trống nên mặt đất tô trắng. Bài test này bắt đúng chuyện đó.
    public class WorldStreamingGroundMaterialTests
    {
        // Viết thẳng đường dẫn thay vì dùng hằng của builder: asmdef test EditMode không tham chiếu
        // _Project.Editor, và nới asmdef chỉ để lấy một chuỗi thì không đáng.
        private const string GroundMaterialPath = "Assets/_Project/Art/Materials/M_WorldStreamingGround.mat";
        private const string GroundShaderPath = "Assets/_Project/Art/Shaders/WorldStreamingGround.shader";

        private static Material LoadGroundMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            Assert.IsNotNull(material, $"Không tìm thấy material nền tại {GroundMaterialPath}.");
            return material;
        }

        [Test]
        public void GroundShader_ExistsCompilesAndIsSupported()
        {
            Shader shader = Shader.Find(ChunkDiagnosticAssets.GroundShaderName);
            Assert.IsNotNull(shader, $"Không tìm thấy shader '{ChunkDiagnosticAssets.GroundShaderName}'.");
            Assert.IsTrue(shader.isSupported, "Shader nền không chạy được trên nền tảng hiện tại.");

            int messages = ShaderUtil.GetShaderMessageCount(shader);
            if (messages > 0)
            {
                var details = new System.Text.StringBuilder();
                foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
                    details.Append('\n').Append(m.severity).Append(": ").Append(m.message);

                Assert.Fail($"Shader nền có {messages} thông báo biên dịch:{details}");
            }
        }

        [Test]
        public void GroundMaterial_UsesTheProjectOwnedGroundShader()
        {
            Material material = LoadGroundMaterial();
            Assert.IsNotNull(material.shader);
            Assert.AreEqual(ChunkDiagnosticAssets.GroundShaderName, material.shader.name,
                "Material nền phải dùng shader của project, không phải shader vendor hay URP mặc định.");
        }

        [Test]
        public void GroundMaterial_HasEverySurfaceTextureAssigned()
        {
            Material material = LoadGroundMaterial();

            foreach (string property in new[]
                     {
                         "_DryTex", "_GrassTex", "_SandTex", "_RockTex", "_NoiseTex",
                         "_DryNormal", "_GrassNormal", "_SandNormal", "_RockNormal",
                     })
            {
                Assert.IsTrue(material.HasProperty(property), $"Shader thiếu property '{property}'.");
                Assert.IsNotNull(material.GetTexture(property), $"Property '{property}' chưa được gán texture.");
            }
        }

        [Test]
        public void GroundMaterial_NormalMapsAreImportedAsNormalMaps()
        {
            Material material = LoadGroundMaterial();

            foreach (string property in new[] { "_DryNormal", "_GrassNormal", "_SandNormal", "_RockNormal" })
            {
                var texture = material.GetTexture(property);
                string path = AssetDatabase.GetAssetPath(texture);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                Assert.IsNotNull(importer, $"'{property}' không trỏ tới một texture asset hợp lệ.");
                Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType,
                    $"'{property}' ({path}) phải được import dạng NormalMap.");
            }
        }

        [Test]
        public void GroundMaterial_TilingIsPositiveAndCoarseEnough()
        {
            Material material = LoadGroundMaterial();

            foreach (string property in new[] { "_DryTiling", "_GrassTiling", "_SandTiling", "_RockTiling" })
            {
                float tiling = material.GetFloat(property);
                Assert.Greater(tiling, 0f, $"'{property}' phải > 0, nếu không phép chia UV sẽ hỏng.");

                // Lặp quá dày ở khoảng cách camera của lab sẽ đọc thành lưới ô vuông.
                Assert.GreaterOrEqual(tiling, 4f, $"'{property}' quá nhỏ, hoa tiết sẽ lộ ra là lưới lặp.");
            }
        }

        [Test]
        public void GroundMaterial_StartsInTexturedMode()
        {
            Material material = LoadGroundMaterial();
            Assert.AreEqual((float)GroundDisplayMode.Textured, material.GetFloat("_DebugMode"), 0.001f,
                "Chế độ mặc định phải là mặt đất có texture, không phải chế độ chẩn đoán.");
        }

        [Test]
        public void GroundMaterial_NormalMapKeywordMatchesItsToggle()
        {
            Material material = LoadGroundMaterial();
            bool toggle = material.GetFloat("_UseNormalMap") > 0.5f;
            Assert.AreEqual(toggle, material.IsKeywordEnabled("_NORMALMAP"),
                "Cờ _UseNormalMap và keyword _NORMALMAP phải luôn khớp nhau.");
        }

        [Test]
        public void GroundMaterial_DefaultPresentationIsStylizedRatherThanPbr()
        {
            Material material = LoadGroundMaterial();

            Assert.Less(material.GetFloat("_UseNormalMap"), 0.5f,
                "Stylized lab must disable normal-map relief; source maps remain assigned for A/B.");
            Assert.That(material.GetFloat("_TextureStrength"), Is.InRange(0.1f, 0.4f),
                "Textures must remain secondary to the authored biome color blocks.");
            Assert.LessOrEqual(material.GetFloat("_MacroStrength"), 0.15f,
                "High macro contrast makes the flat plane read like PBR height detail.");
            Assert.That(material.GetFloat("_ToonSteps"), Is.InRange(2f, 4f),
                "The canonical lab lighting must use a small number of readable toon bands.");
        }

        [Test]
        public void GroundMaterial_HasNoWorldOriginOffsetInM2A()
        {
            Material material = LoadGroundMaterial();
            Vector4 offset = material.GetVector("_WorldOriginOffset");
            Assert.AreEqual(Vector4.zero, offset,
                "M2A chưa có floating origin: toạ độ logic phải bằng đúng toạ độ vật lý.");
        }

        [Test]
        public void GroundShader_WeightJitterModulatesLayersDifferently()
        {
            string source = File.ReadAllText(GroundShaderPath);

            StringAssert.Contains("float4 jitterBias", source,
                "Weight jitter phải tạo bias riêng cho từng surface layer.");
            StringAssert.DoesNotContain("raw * (1.0 + (jitter - 0.5) * 2.0 * _WeightJitter)", source,
                "Nhân cả bốn weight với cùng một scalar sẽ bị normalize triệt tiêu hoàn toàn.");
        }

        // --- Mapping toàn cục -------------------------------------------------------------------

        [Test]
        public void WorldMappingInput_IsContinuousAcrossChunkBorders()
        {
            // Shader lấy UV từ world XZ của đỉnh. Nếu vị trí world của hai đỉnh biên không trùng nhau
            // thì hoa tiết sẽ lệch đúng tại biên chunk, dù dữ liệu biome vẫn khớp.
            const float chunkSize = 32f;
            const int resolution = 17;
            int last = resolution - 1;

            foreach ((ChunkCoord a, ChunkCoord b) in new[]
                     {
                         (new ChunkCoord(0, 0), new ChunkCoord(1, 0)),
                         (new ChunkCoord(-1, 0), new ChunkCoord(0, 0)),
                         (new ChunkCoord(0, -1), new ChunkCoord(0, 0)),
                         (new ChunkCoord(-6, -9), new ChunkCoord(-5, -9)),
                         (new ChunkCoord(-620, 391), new ChunkCoord(-619, 391)),
                     })
            {
                bool alongX = b.X != a.X;

                for (int i = 0; i < resolution; i++)
                {
                    float loX = GroundMeshBuilder.VertexWorldAxis(a.X, alongX ? last : i, resolution, chunkSize);
                    float loZ = GroundMeshBuilder.VertexWorldAxis(a.Z, alongX ? i : last, resolution, chunkSize);
                    float hiX = GroundMeshBuilder.VertexWorldAxis(b.X, alongX ? 0 : i, resolution, chunkSize);
                    float hiZ = GroundMeshBuilder.VertexWorldAxis(b.Z, alongX ? i : 0, resolution, chunkSize);

                    Assert.AreEqual(loX, hiX, 0f, $"{a}->{b} i={i}: world X ở biên không trùng.");
                    Assert.AreEqual(loZ, hiZ, 0f, $"{a}->{b} i={i}: world Z ở biên không trùng.");
                }
            }
        }

        [Test]
        public void GroundTopology_IsUnchangedByM2A()
        {
            Mesh mesh = GroundMeshBuilder.CreateGroundMesh("M2ATopology", 17, 32f);
            try
            {
                Assert.AreEqual(289, mesh.vertexCount);
                Assert.AreEqual(1, mesh.subMeshCount);
                Assert.AreEqual(1536, mesh.GetTriangles(0).Length);

                foreach (Vector3 v in mesh.vertices)
                    Assert.AreEqual(0f, v.y, 0f, "M2A không được làm mặt đất gồ ghề — vẫn phải phẳng ở Y = 0.");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void VertexWeights_RemainNormalizedAfterM2A()
        {
            Mesh mesh = GroundMeshBuilder.CreateGroundMesh("M2AWeights", 17, 32f);
            try
            {
                GroundMeshBuilder.ApplyBiome(mesh, new ChunkCoord(-54, 48), 17, 32f, 20260809);

                foreach (Color c in mesh.colors)
                {
                    float sum = c.r + c.g + c.b + c.a;
                    Assert.AreEqual(1f, sum, 0.01f, "Tổng bốn trọng số phải vẫn ≈ 1 — shader không được đụng vào dữ liệu.");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
