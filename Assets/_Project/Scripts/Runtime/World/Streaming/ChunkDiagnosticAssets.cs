using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>Chế độ hiển thị của mặt đất. Trùng với `_DebugMode` trong shader.</summary>
    public enum GroundDisplayMode
    {
        /// <summary>Mặt đất có texture thật — chế độ bình thường.</summary>
        Textured = 0,

        /// <summary>Trọng số biome thô tô bằng bốn màu chẩn đoán.</summary>
        BiomeWeights = 1,

        /// <summary>Chỉ kênh trội nhất — làm lộ ranh giới vùng và mọi đường nối.</summary>
        DominantSurface = 2,
    }

    /// <summary>
    /// Điểm neo dùng chung cho shader/material mặt đất.
    ///
    /// Material dự phòng ở đây chỉ phục vụ PlayMode test, nơi không có asset nào được nối vào.
    /// Đúng một instance cho cả pool — không có material instance cho từng chunk. Màu từng chunk
    /// đến từ vertex color chứ không từ material, nên một material là đủ cho cả 25 chunk.
    /// </summary>
    public static class ChunkDiagnosticAssets
    {
        public const string GroundShaderName = "ZombieWar/World Streaming/Ground";
        public const string SolidDecorShaderName = "ZombieWar/World Streaming/Solid Decor";
        public const string FoliageShaderName = "ZombieWar/World Streaming/Foliage";

        public static readonly int DebugModeId = Shader.PropertyToID("_DebugMode");

        private static Material _fallbackGroundMaterial;
        private static Material _fallbackSolidMaterial;
        private static Material _fallbackFoliageMaterial;

        /// <summary>Material trang trí đặc dự phòng — dùng chung, chỉ phục vụ PlayMode test.</summary>
        public static Material FallbackSolidMaterial =>
            _fallbackSolidMaterial != null
                ? _fallbackSolidMaterial
                : _fallbackSolidMaterial = CreateFallback(SolidDecorShaderName, "M_WorldStreamingSolidDecor (runtime fallback)");

        /// <summary>Material foliage dự phòng — dùng chung, chỉ phục vụ PlayMode test.</summary>
        public static Material FallbackFoliageMaterial =>
            _fallbackFoliageMaterial != null
                ? _fallbackFoliageMaterial
                : _fallbackFoliageMaterial = CreateFallback(FoliageShaderName, "M_WorldStreamingFoliage (runtime fallback)");

        private static Material CreateFallback(string shaderName, string materialName)
        {
            Shader shader = Shader.Find(shaderName)
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");

            return new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        public static Material FallbackGroundMaterial
        {
            get
            {
                if (_fallbackGroundMaterial != null) return _fallbackGroundMaterial;

                Shader shader = Shader.Find(GroundShaderName)
                                ?? Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Sprites/Default");

                _fallbackGroundMaterial = new Material(shader)
                {
                    name = "M_WorldStreamingGround (runtime fallback)",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                ApplyDiagnosticPalette(_fallbackGroundMaterial);
                return _fallbackGroundMaterial;
            }
        }

        /// <summary>
        /// Bảng màu của chế độ chẩn đoán. Nó tồn tại để lộ ra tính liên tục và đường nối,
        /// không phải để đẹp — chế độ có texture mới là bộ mặt thật của mặt đất.
        /// </summary>
        public static void ApplyDiagnosticPalette(Material material)
        {
            if (material == null || material.shader == null) return;
            if (material.shader.name != GroundShaderName) return;

            material.SetColor("_DryDebugColor", new Color(0.42f, 0.32f, 0.21f));
            material.SetColor("_GrassDebugColor", new Color(0.27f, 0.42f, 0.20f));
            material.SetColor("_SandDebugColor", new Color(0.74f, 0.68f, 0.45f));
            material.SetColor("_RockDebugColor", new Color(0.44f, 0.46f, 0.50f));
            material.SetFloat(DebugModeId, (float)GroundDisplayMode.Textured);
        }
    }
}
